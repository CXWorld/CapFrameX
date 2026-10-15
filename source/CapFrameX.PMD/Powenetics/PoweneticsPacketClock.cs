using System;
using System.Collections.Generic;

namespace CapFrameX.PMD.Powenetics
{
    /// <summary>
    /// Maps the Powenetics packet stream onto the QPC clock.
    /// </summary>
    /// <remarks>
    /// The device numbers its packets but sends no time, and it does not run at its nominal 1 kHz:
    /// a measured unit sends 979 packets/s without losing any, so counting packets as milliseconds
    /// compresses a 600 s capture by 12 s. Neither the serial API nor usbser.sys provides receive
    /// timestamps, so the driver stamps each read with QPC and this clock maps the packet index
    /// onto those arrival times.
    ///
    /// Arrivals are coarse: SerialPortStream hands the data over about every 106 ms, roughly 104
    /// packets at once, and a stalled reader receives a burst of late packets. Every arrival is
    /// therefore an upper bound of the packet's time, and only the last packet of a read arrives
    /// right after it was sent. The clock takes the rate from an exponentially weighted
    /// least-squares line over all packets (the read latency does not change the slope) and the
    /// offset from the lower envelope of the arrivals: a packet below the line pulls it down
    /// immediately, otherwise the line rises by <see cref="EnvelopeLeakSecondsPerSecond"/> to
    /// follow slow changes. The envelope is anchored at the latest packet, so a correction of the
    /// rate only bends the line from there on. A packet's time is then the moment it was sent,
    /// give or take the time the device needs to get it on the bus.
    ///
    /// Packets arriving in the first <see cref="SettleSeconds"/> of a segment are ignored: opening
    /// the port delivers a backlog of buffered packets at once. Until the rate is known
    /// (<see cref="WarmUpSeconds"/>), a packet's time is its arrival time.
    /// </remarks>
    internal sealed class PoweneticsPacketClock
    {
        // Packet numbers are 16 bit.
        private const int PacketNumberModulus = 65536;

        // A larger jump is not a loss but a restarted or resynchronized device.
        internal const int MaxPacketGap = 5000;

        // Time constant of the exponential forgetting, in packets (about a minute).
        private const double FitTimeConstantPackets = 60000;

        internal const double SettleSeconds = 0.5;

        internal const double WarmUpSeconds = 5.0;

        internal const int WarmUpPackets = 1000;

        // How fast the envelope rises without a packet touching it.
        internal const double EnvelopeLeakSecondsPerSecond = 0.001;

        // Later packets come from a stalled reader and are not fitted.
        internal const double MaxFitLatencySeconds = 1.0;

        // A packet cannot arrive before it was sent; this much earlier means the line is wrong.
        internal const double MaxEarlySeconds = 0.05;

        // Packets later than LateSeconds for longer than LatePersistenceSeconds mean the same.
        internal const double LateSeconds = 0.5;
        internal const double LatePersistenceSeconds = 2.0;

        // Plausible packet periods; outside of them the clock starts over.
        private const double MinPeriodSeconds = 0.0005;
        private const double MaxPeriodSeconds = 0.002;

        private static readonly double ForgettingFactor = 1.0 - 1.0 / FitTimeConstantPackets;

        private bool _hasPacket;
        private int _lastPacketNumber;
        private long _packetIndex;
        private double _lastTime = double.NegativeInfinity;

        // Segment origin; line coordinates are relative to it to keep them small.
        private long _originIndex;
        private double _originArrival;

        // Exponentially weighted fit state: weight, means and co-moments.
        private double _weight;
        private double _meanX;
        private double _meanY;
        private double _cxx;
        private double _cxy;
        private int _fittedPackets;
        private double _firstFittedY = double.NaN;

        private bool _isWarmedUp;
        // Envelope point of the latest packet; the envelope continues from it with the fitted slope.
        private double _anchorX;
        private double _anchorY;
        private double _previousX;
        private double _lateSince = double.NaN;

        // Fitted packets of the warm-up, to place the envelope once the rate is known.
        private readonly List<(double X, double Y)> _warmUpPoints = new List<(double X, double Y)>();

        /// <summary>
        /// Packets the device numbered but that never arrived, since the last <see cref="Reset"/>.
        /// </summary>
        public int LostPackets { get; private set; }

        /// <summary>
        /// Whether packet times come from the fitted line rather than from arrival times.
        /// </summary>
        public bool IsWarmedUp => _isWarmedUp;

        /// <summary>
        /// Fitted packet rate in packets per second, or NaN while warming up.
        /// </summary>
        public double PacketRate => _isWarmedUp ? 1.0 / Slope : double.NaN;

        private double Slope => _cxx > 0 ? _cxy / _cxx : double.NaN;

        /// <summary>
        /// Returns the time of a packet in seconds on the QPC clock. Times never decrease.
        /// </summary>
        /// <param name="packetNumber">The 16 bit packet number from the packet header.</param>
        /// <param name="arrivalSeconds">QPC time of the read that completed the packet, in seconds.</param>
        public double OnPacket(int packetNumber, double arrivalSeconds)
        {
            if (!_hasPacket)
            {
                _hasPacket = true;
                StartSegment(0, arrivalSeconds);
            }
            else
            {
                int delta = (packetNumber - _lastPacketNumber + PacketNumberModulus) % PacketNumberModulus;
                if (delta == 0 || delta > MaxPacketGap)
                {
                    StartSegment(_packetIndex + 1, arrivalSeconds);
                }
                else
                {
                    LostPackets += delta - 1;
                    _packetIndex += delta;
                }
            }

            _lastPacketNumber = packetNumber;

            double time = Estimate(arrivalSeconds);
            if (time < _lastTime)
                time = _lastTime;

            _lastTime = time;
            return time;
        }

        /// <summary>
        /// Forgets everything, for a new connection.
        /// </summary>
        public void Reset()
        {
            _hasPacket = false;
            _packetIndex = 0;
            _lastTime = double.NegativeInfinity;
            LostPackets = 0;
            ClearFit();
        }

        private double Estimate(double arrivalSeconds)
        {
            double x = _packetIndex - _originIndex;
            double y = arrivalSeconds - _originArrival;

            if (!_isWarmedUp)
            {
                if (y >= SettleSeconds)
                {
                    AddToFit(x, y);
                    _warmUpPoints.Add((x, y));
                    TryCompleteWarmUp(y);
                }

                return arrivalSeconds;
            }

            double slope = Slope;
            double envelope = _anchorY + slope * (x - _anchorX);
            double latency = y - envelope;

            if (latency < -MaxEarlySeconds || !IsPlausiblePeriod(slope))
            {
                StartSegment(_packetIndex, arrivalSeconds);
                return arrivalSeconds;
            }

            if (latency > LateSeconds)
            {
                if (double.IsNaN(_lateSince))
                {
                    _lateSince = arrivalSeconds;
                }
                else if (arrivalSeconds - _lateSince > LatePersistenceSeconds)
                {
                    StartSegment(_packetIndex, arrivalSeconds);
                    return arrivalSeconds;
                }
            }
            else
            {
                _lateSince = double.NaN;
            }

            // Lower envelope: down at once, up slowly.
            _anchorX = x;
            _anchorY = latency < 0 ? y : envelope + Math.Min(latency, EnvelopeLeakSecondsPerSecond * slope * (x - _previousX));
            _previousX = x;

            if (latency <= MaxFitLatencySeconds)
                AddToFit(x, y);

            return _originArrival + _anchorY;
        }

        private void TryCompleteWarmUp(double y)
        {
            double slope = Slope;
            if (_fittedPackets < WarmUpPackets || y - _firstFittedY < WarmUpSeconds || !IsPlausiblePeriod(slope))
                return;

            // The fitted line runs through the mean read latency. The envelope starts at the packet
            // of the last second that arrived soonest after it was sent; within a second, a slightly
            // wrong rate hardly matters.
            double lowest = double.PositiveInfinity;
            foreach (var point in _warmUpPoints)
            {
                if (point.Y >= y - 1.0 && point.Y - slope * point.X < lowest)
                {
                    lowest = point.Y - slope * point.X;
                    _anchorX = point.X;
                    _anchorY = point.Y;
                }
            }

            _previousX = _warmUpPoints[^1].X;
            _warmUpPoints.Clear();
            _isWarmedUp = true;
        }

        private static bool IsPlausiblePeriod(double slope) => slope >= MinPeriodSeconds && slope <= MaxPeriodSeconds;

        private void StartSegment(long packetIndex, double arrivalSeconds)
        {
            _packetIndex = packetIndex;
            _originIndex = packetIndex;
            _originArrival = arrivalSeconds;
            ClearFit();
        }

        private void ClearFit()
        {
            _weight = 0;
            _meanX = 0;
            _meanY = 0;
            _cxx = 0;
            _cxy = 0;
            _fittedPackets = 0;
            _firstFittedY = double.NaN;
            _isWarmedUp = false;
            _anchorX = 0;
            _anchorY = 0;
            _previousX = 0;
            _lateSince = double.NaN;
            _warmUpPoints.Clear();
        }

        // Weighted incremental update (West 1979) with all previous weights scaled by the
        // forgetting factor before the new point is added with weight 1.
        private void AddToFit(double x, double y)
        {
            if (_fittedPackets == 0)
                _firstFittedY = y;

            _weight = _weight * ForgettingFactor + 1.0;
            double dx = x - _meanX;
            _meanX += dx / _weight;
            _meanY += (y - _meanY) / _weight;
            _cxx = _cxx * ForgettingFactor + dx * (x - _meanX);
            _cxy = _cxy * ForgettingFactor + dx * (y - _meanY);
            _fittedPackets++;
        }
    }
}

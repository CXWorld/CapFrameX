using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.PMD;
using CapFrameX.PMD.Powenetics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PMD
{
    /// <summary>
    /// The PMD time axis: packet clock, time-based downsampling and the resampling onto the grid of
    /// a capture file. The device is simulated with the behaviour measured on a Powenetics unit:
    /// 979 packets/s instead of the nominal 1000, delivered in 1 ms USB frames, and handed over by
    /// SerialPortStream about every 106 ms.
    /// </summary>
    [TestClass]
    public class PmdTimingTest
    {
        private const double DeviceRate = 979.0;

        // Delivery as measured with SerialPortStream: one read about every 106 ms.
        private const double BatchedDelivery = 0.106;

        // The clock measures from the moment a packet is readable; anything beyond a constant offset
        // is a timing error. Batched delivery leaves fewer packets that arrive right after sending.
        private const double FineDeliveryTolerance = 0.0015;
        private const double BatchedDeliveryTolerance = 0.0025;

        // Long enough for the warm-up of the clock.
        private const int SettledFrom = (int)(8 * DeviceRate);

        private struct SimulatedPacket
        {
            public int Number;
            public double Production;
            public double Arrival;
        }

        // Packet n is produced at start + n / rate, becomes readable at the next USB frame (1 ms) and
        // is read up to 0.4 ms later, or at the next read when the delivery is batched. Reads keep
        // the byte order, so arrivals never decrease.
        private static List<SimulatedPacket> Simulate(int count, double start, int firstNumber, int seed,
            double deliveryPeriod = 0, Func<double, double, double> delay = null)
        {
            var random = new Random(seed);
            var packets = new List<SimulatedPacket>(count);
            double lastArrival = double.NegativeInfinity;
            double nextDelivery = start + random.NextDouble() * deliveryPeriod;

            for (int n = 0; n < count; n++)
            {
                double production = start + n / DeviceRate;
                double arrival = Math.Ceiling(production * 1000.0) / 1000.0 + random.NextDouble() * 0.0004;
                if (deliveryPeriod > 0)
                {
                    while (nextDelivery < arrival)
                        nextDelivery += deliveryPeriod * (0.98 + 0.04 * random.NextDouble());
                    arrival = nextDelivery;
                }

                if (delay != null)
                    arrival = delay(production, arrival);

                arrival = Math.Max(arrival, lastArrival);
                lastArrival = arrival;

                packets.Add(new SimulatedPacket
                {
                    Number = (firstNumber + n) % 65536,
                    Production = production,
                    Arrival = arrival
                });
            }

            return packets;
        }

        // Spread of (estimated - produced) over the packets from index `from` on: a constant offset is
        // the time a packet needs to become readable, anything beyond it is a timing error.
        private static double ErrorSpread(IList<SimulatedPacket> packets, IList<double> times, int from)
        {
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int i = from; i < packets.Count; i++)
            {
                double error = times[i] - packets[i].Production;
                min = Math.Min(min, error);
                max = Math.Max(max, error);
            }

            return max - min;
        }

        private static double Tolerance(double deliveryPeriod)
            => deliveryPeriod > 0 ? BatchedDeliveryTolerance : FineDeliveryTolerance;

        private static List<double> Run(PoweneticsPacketClock clock, IEnumerable<SimulatedPacket> packets)
            => packets.Select(p => clock.OnPacket(p.Number, p.Arrival)).ToList();

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(BatchedDelivery)]
        public void PacketClock_FollowsTheDeviceRateInsteadOfOneMillisecondPerPacket(double deliveryPeriod)
        {
            var packets = Simulate((int)(120 * DeviceRate), start: 1000, firstNumber: 34186, seed: 1, deliveryPeriod);
            var clock = new PoweneticsPacketClock();

            var times = Run(clock, packets);

            Assert.IsTrue(clock.IsWarmedUp);
            Assert.AreEqual(DeviceRate, clock.PacketRate, 0.05);
            Assert.AreEqual(0, clock.LostPackets);
            // Two minutes of packets, 2.1 % too slow for 1 kHz: counting them would be 2.5 s off.
            double spread = ErrorSpread(packets, times, SettledFrom);
            Assert.IsTrue(spread < Tolerance(deliveryPeriod), $"Packet times drift against the production times ({spread * 1000:F2} ms).");
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(BatchedDelivery)]
        public void PacketClock_PlacesTheBurstAfterAStallAtItsProductionTimes(double deliveryPeriod)
        {
            // The reader stalls for 300 ms at 60 s; everything produced meanwhile arrives at once.
            const double stallStart = 1060.0;
            const double stallEnd = 1060.3;
            var packets = Simulate((int)(90 * DeviceRate), start: 1000, firstNumber: 0, seed: 2, deliveryPeriod,
                delay: (production, arrival) => production >= stallStart && production < stallEnd
                    ? Math.Max(arrival, stallEnd) : arrival);
            var clock = new PoweneticsPacketClock();

            var times = Run(clock, packets);

            double spread = ErrorSpread(packets, times, SettledFrom);
            Assert.IsTrue(spread < Tolerance(deliveryPeriod),
                $"The stall burst is placed at its arrival instead of its production time ({spread * 1000:F2} ms).");
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(BatchedDelivery)]
        public void PacketClock_IgnoresTheBacklogDeliveredWhenThePortOpens(double deliveryPeriod)
        {
            // The device streamed before the port was opened; 108 buffered packets arrive at once.
            const int backlog = 108;
            var packets = Simulate((int)(20 * DeviceRate), start: 1000, firstNumber: 500, seed: 3, deliveryPeriod,
                delay: (production, arrival) => Math.Max(arrival, 1000 + backlog / DeviceRate));
            var clock = new PoweneticsPacketClock();

            var times = Run(clock, packets);

            double spread = ErrorSpread(packets, times, SettledFrom);
            Assert.IsTrue(spread < Tolerance(deliveryPeriod), $"The start-up backlog skews the clock ({spread * 1000:F2} ms).");
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(BatchedDelivery)]
        public void PacketClock_CountsGapsAcrossTheWrapOfThePacketNumber(double deliveryPeriod)
        {
            // The 16 bit packet number wraps at index 9936, after the clock has warmed up.
            var packets = Simulate(20000, start: 1000, firstNumber: 55600, seed: 4, deliveryPeriod);
            Assert.AreEqual(63, packets[9999].Number);
            // Lose packets 64 to 66.
            packets.RemoveRange(10000, 3);
            var clock = new PoweneticsPacketClock();

            var times = Run(clock, packets);

            Assert.AreEqual(3, clock.LostPackets);
            // The lost packets still take their time: the next packet is four periods later, not one.
            Assert.AreEqual(4 / DeviceRate, times[10000] - times[9999], Tolerance(deliveryPeriod));
        }

        [TestMethod]
        public void PacketClock_TreatsALargeJumpAsARestartNotAsLoss()
        {
            var clock = new PoweneticsPacketClock();
            var before = Simulate(10000, start: 1000, firstNumber: 100, seed: 5, BatchedDelivery);
            var after = Simulate(10000, start: before.Last().Production + 0.5, firstNumber: 40000, seed: 6, BatchedDelivery);

            var times = Run(clock, before.Concat(after));

            Assert.AreEqual(0, clock.LostPackets);
            for (int i = 1; i < times.Count; i++)
                Assert.IsTrue(times[i] >= times[i - 1], $"Packet time decreased at {i}.");
        }

        [TestMethod]
        public void Downsampler_BinsPacketsByTimeNotByCount()
        {
            var downsampler = new PoweneticsDownsampler(10);
            var completed = new List<(long Start, int Count)>();

            // 979 packets/s from t = 1000.000 s: about 9.79 packets per 10 ms bin.
            for (int n = 0; n < 1000; n++)
            {
                long timeStamp = (long)Math.Floor((1000.0 + n / DeviceRate) * 1000.0);
                if (downsampler.TryAdd(Packet(timeStamp), out var bin, out var binStart))
                    completed.Add((binStart, bin.Count));
            }

            Assert.AreEqual(1000000, completed[0].Start);
            for (int i = 1; i < completed.Count; i++)
                Assert.AreEqual(completed[i - 1].Start + 10, completed[i].Start, "Bins must follow each other.");

            // 1000 packets cover 1.021 s; the last bin is still open.
            Assert.AreEqual(102, completed.Count);
            Assert.IsTrue(completed.All(b => b.Count == 9 || b.Count == 10));
        }

        [TestMethod]
        public void Downsampler_SkipsBinsWithoutPackets()
        {
            var downsampler = new PoweneticsDownsampler(10);

            Assert.IsFalse(downsampler.TryAdd(Packet(1003), out _, out _));
            Assert.IsFalse(downsampler.TryAdd(Packet(1009), out _, out _));
            Assert.IsTrue(downsampler.TryAdd(Packet(1042), out var bin, out var binStart));
            Assert.AreEqual(1000, binStart);
            Assert.AreEqual(2, bin.Count);
            Assert.IsTrue(downsampler.TryAdd(Packet(1050), out bin, out binStart));
            Assert.AreEqual(1040, binStart);
            Assert.AreEqual(1, bin.Count);
        }

        [TestMethod]
        public void Resampler_KeepsSamplesThatAlreadyMatchTheGrid()
        {
            var times = Enumerable.Range(0, 50).Select(i => 5.0 + i * 0.01).ToArray();
            var values = Enumerable.Range(0, 50).Select(i => (float)i).ToArray();

            var result = PmdTimeSeriesResampler.Resample(times, values, 5.0, 0.01, 50);

            CollectionAssert.AreEqual(values, result);
        }

        [TestMethod]
        public void Resampler_RepeatsTheNearestValueOutsideTheSamples()
        {
            var result = PmdTimeSeriesResampler.Resample(new[] { 1.0, 1.1 }, new[] { 10f, 20f }, 0.8, 0.1, 6);

            CollectionAssert.AreEqual(new[] { 10f, 10f, 10f, 20f, 20f, 20f }, result);
        }

        [TestMethod]
        public void Resampler_PlacesIrregularSamplesByTheirTimes()
        {
            // BENCHLAB-like: a ramp sampled every 108 ms instead of 100 ms.
            var times = Enumerable.Range(0, 200).Select(i => 3.0 + i * 0.108).ToArray();
            var values = times.Select(t => (float)(t - 3.0)).ToArray();

            var result = PmdTimeSeriesResampler.Resample(times, values, 3.0, 0.1, 200);

            for (int i = 0; i < 200; i++)
                Assert.AreEqual(i * 0.1, result[i], 0.06, $"Value {i} is not near its grid time.");
        }

        [TestMethod]
        public void Resampler_KeepsTheAveragePower()
        {
            var random = new Random(7);
            var times = Enumerable.Range(0, 5000).Select(i => i * 0.0102 + random.NextDouble() * 0.001).ToArray();
            var values = times.Select(_ => (float)(100 + random.NextDouble() * 50)).ToArray();

            var result = PmdTimeSeriesResampler.Resample(times, values, times[0], 0.01, (int)((times[^1] - times[0]) / 0.01));

            Assert.AreEqual(values.Average(), result.Average(), 0.5);
        }

        [TestMethod]
        public void CaptureTimeAxis_PutsAnEventAtItsTimeAfterTenMinutes()
        {
            // End to end: device packets -> clock -> 10 ms bins -> grid of the capture file. A load
            // spike at 416 s, like the frame time spike of the reported Hogwarts Legacy capture,
            // used to show up 9 s early because 979 packets/s were counted as 1000.
            const double captureStart = 2000.0;
            const double spikeStart = captureStart + 416.0;
            const double spikeEnd = spikeStart + 0.05;
            const int binSize = 10;

            var packets = Simulate((int)(625 * DeviceRate), start: captureStart - 10, firstNumber: 0, seed: 8, BatchedDelivery);
            var clock = new PoweneticsPacketClock();
            var downsampler = new PoweneticsDownsampler(binSize);
            var times = new List<double>();
            var values = new List<float>();

            foreach (var packet in packets)
            {
                double time = clock.OnPacket(packet.Number, packet.Arrival);
                float power = packet.Production >= spikeStart && packet.Production < spikeEnd ? 300f : 100f;
                var channels = new[] { new PoweneticsChannel { Value = power, TimeStamp = (long)Math.Floor(time * 1000.0) } };

                if (downsampler.TryAdd(channels, out var bin, out var binStart))
                {
                    times.Add((binStart + 0.5 * binSize) * 1E-03);
                    values.Add(bin.Average(c => c[0].Value));
                }
            }

            int count = (int)(600000.0 / binSize) + 1;
            var grid = PmdTimeSeriesResampler.Resample(times, values, captureStart, binSize * 1E-03, count);

            Assert.AreEqual(60001, grid.Length);
            // The cells at either end of the 50 ms spike overlap it by half.
            int firstSpikeCell = Array.FindIndex(grid, v => v > 150f);
            Assert.AreEqual(41600, firstSpikeCell, 1, "The spike is not where it happened.");
            Assert.AreEqual(6, grid.Count(v => v > 150f), 1);
        }

        private static PoweneticsChannel[] Packet(long timeStamp)
            => new[] { new PoweneticsChannel { TimeStamp = timeStamp } };
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace CapFrameX.Data
{
    /// <summary>
    /// https://www.danielwjudge.com/atomic-time-in-c/
    /// </summary>
    public static class AtomicTime
    {
        private static DateTime _currentAtomicTime;
        private static bool _canConnectToServer = true;
        private static Stopwatch _timeSinceLastValue = new Stopwatch();
        private static readonly object Locker = new object();
        private static Countdown _countdown; //used to help en

        public static DateTime Now
        {
            get
            {
                //we have attempted to connect to the server and had no luck, no need to try again
                if (_canConnectToServer == false)
                    return DateTime.MinValue;

                //keep track so we don't have to keep connecting to the servers
                if (_currentAtomicTime != DateTime.MinValue)
                {
                    _currentAtomicTime += _timeSinceLastValue.Elapsed;
                    _timeSinceLastValue.Reset();
                    _timeSinceLastValue.Start();
                }
                else
                {
                    //ensure we aren't doing this multiple times from multiple locations
                    lock (Locker)
                    {
                        if (_currentAtomicTime != DateTime.MinValue) //we got the time already, pass it along
                        {
                            _currentAtomicTime += _timeSinceLastValue.Elapsed;
                            _timeSinceLastValue.Reset();
                            _timeSinceLastValue.Start();
                        }
                        else
                        {
                            // Initialize the list of NIST time servers
                            // http://tf.nist.gov/tf-cgi/servers.cgi
                            var servers = new[]
                            {
                                "time-a-g.nist.gov",
                                "time-b-g.nist.gov",
                                "time-c-g.nist.gov",
                                "time-d-g.nist.gov",
                                "time-d-g.nist.gov",
                                "time-e-g.nist.gov",
                                "time-e-g.nist.gov",
                                "time-a-wwv.nist.gov",
                                "time-b-wwv.nist.gov",
                                "time-c-wwv.nist.gov",
                                "time-d-wwv.nist.gov",
                                "time-d-wwv.nist.gov",
                                "time-e-wwv.nist.gov",
                                "time-e-wwv.nist.gov",
                                "time-a-b.nist.gov",
                                "time-b-b.nist.gov",
                                "time-c-b.nist.gov",
                                "time-d-b.nist.gov",
                                "time-d-b.nist.gov",
                                "time-e-b.nist.gov",
                                "time-e-b.nist.gov",
                                "time.nist.gov",
                                "utcnist.colorado.edu",
                                "utcnist2.colorado.edu",
                          };

                            // Try 5 servers in random order to spread the load
                            var rnd = new Random();
                            _countdown = new Countdown(5);
                            foreach (string server in servers.OrderBy(s => rnd.NextDouble()).Take(5))
                            {
                                string server1 = server;
                                var t = new Thread(o => GetDateTimeFromServer(server1));
                                t.SetApartmentState(ApartmentState.STA);
                                t.Start();
                            }
                            _countdown.Wait();
                            if (_currentAtomicTime == DateTime.MinValue)
                                _canConnectToServer = false;
                        }
                    }
                }

                return _currentAtomicTime;
            }
        }

        private static void GetDateTimeFromServer(string server)
        {
            if (_currentAtomicTime == DateTime.MinValue)
            {
                try
                {
                    // Connect to the server (at port 13) and get the response
                    string serverResponse;
                    using (var reader = new StreamReader(new System.Net.Sockets.TcpClient(server, 13).GetStream()))
                        serverResponse = reader.ReadToEnd();

                    // If a response was received
                    if (!string.IsNullOrEmpty(serverResponse) || _currentAtomicTime != DateTime.MinValue)
                    {
                        // Split the response string ("55596 11-02-14 13:54:11 00 0 0 478.1 UTC(NIST) *")
                        //format is RFC-867, see example here: http://www.kloth.net/software/timesrv1.php
                        //some other examples of how to parse can be found in this: http://cosinekitty.com/nist/
                        string[] tokens = serverResponse.Replace("n", "").Split(' ');

                        // Check the number of tokens
                        if (tokens.Length >= 6)
                        {
                            // Check the health status
                            string health = tokens[5];
                            if (health == "0")
                            {
                                // Get date and time parts from the server response
                                string[] dateParts = tokens[1].Split('-');
                                string[] timeParts = tokens[2].Split(':');

                                // Create a DateTime instance
                                var utcDateTime = new DateTime(
                                Convert.ToInt32(dateParts[0]) + 2000,
                                Convert.ToInt32(dateParts[1]), Convert.ToInt32(dateParts[2]),
                                Convert.ToInt32(timeParts[0]), Convert.ToInt32(timeParts[1]),
                                Convert.ToInt32(timeParts[2]));

                                //subject milliseconds from it
                                if (Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," && tokens[6].Contains("."))
                                    tokens[6] = tokens[6].Replace(".", ",");
                                else if (Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator == "." && tokens[6].Contains(","))
                                    tokens[6] = tokens[6].Replace(",", ".");

                                double.TryParse(tokens[6], out double millis);
                                utcDateTime = utcDateTime.AddMilliseconds(-millis);

                                // disabled(!): Convert received (UTC) DateTime value to the local timezone
                                if (_currentAtomicTime == DateTime.MinValue)
                                {
                                    _currentAtomicTime = utcDateTime; //.ToLocalTime();
                                    _timeSinceLastValue = new Stopwatch();
                                    _timeSinceLastValue.Start();
                                    _countdown.PulseAll(); //we got a valid time, move on and no need to worry about results from other threads
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore exception and try the next server
                }
            }

            //let CountdownEvent know that we're done here
            _countdown.Signal();
        }
    }

    public class Countdown
    {
        readonly object _locker = new object();
        int _value;

        public Countdown() { }
        public Countdown(int initialCount) { _value = initialCount; }
        public void Signal() { AddCount(-1); }
        public void PulseAll()
        {
            lock (_locker)
            {
                _value = 0;
                Monitor.PulseAll(_locker);
            }
        }

        public void AddCount(int amount)
        {
            lock (_locker)
            {
                _value += amount;
                if (_value <= 0) Monitor.PulseAll(_locker);
            }
        }
        public void Wait()
        {
            lock (_locker)
                while (_value > 0)
                    Monitor.Wait(_locker);
        }
    }
}

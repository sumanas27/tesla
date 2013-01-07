﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using TeslaSQL.DataUtils;
using System.IO;
using System.Diagnostics;
using log4net;

namespace TeslaSQL {
    public class Logger {

        private LogLevel logLevel { get; set; }
        private string errorLogDB { get; set; }
        public IDataUtils dataUtils { private get; set; }
        private readonly string logFile;
        private static ILog log = LogManager.GetLogger(typeof(Logger));

        private StatsdPipe statsd;
    
        public Logger(LogLevel logLevel, string statsdHost, string statsdPort, string errorLogDB, string logFile) {
            this.logLevel = logLevel;
     
            this.errorLogDB = errorLogDB;
            try {
                if (!File.Exists(logFile)) {
                    //holds onto a file handle if you dont close it.
                    File.Create(logFile).Close();
                }
                this.logFile = logFile;
            } catch (Exception) {
                this.logFile = null;
            }      
            
                try {
                    this.statsd = new StatsdPipe(statsdHost, int.Parse(statsdPort));
                    Log(String.Format("Building statsdpipe: {0}:{1}", statsdHost, statsdPort), LogLevel.Trace);
                } catch {
                    Log("Invalid or empty config values for statsdHost and statsdPipe; not logging to StatsD this run", LogLevel.Warn);
                }
            
        }

        public Logger(LogLevel logLevel, string statsdHost, string statsdPort, string errorLogDB, string logFile, IDataUtils dataUtils)
            : this(logLevel, statsdHost, statsdPort, errorLogDB, logFile) {
            this.dataUtils = dataUtils;
        }

        public void Timing(string key, int value, double sampleRate = 1.0) {
            if (statsd == null) { return; }
            statsd.Timing(key, value, sampleRate);
            Log(String.Format("Timing: {0}, {1} @{2}", key, value, sampleRate), LogLevel.Trace);
        }

        /// <summary>
        /// Logs information and writes it to the console
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="logLevel">LogLevel value, gets compared to the configured logLevel variable</param>
        public void Log(string message, LogLevel logLevel) {
            //compareto method returns a number less than 0 if logLevel is less than configured
            //if (logLevel.CompareTo(this.logLevel) >= 0) {
            //    var frame = new StackFrame(1);
            //    var method = frame.GetMethod();
            //    var obj = method.DeclaringType.ToString();
            //    var firstLine = DateTime.Now + ": " + obj + " " + method.Name;
            //    var secondLine = logLevel + ": " + message;
            //    Console.WriteLine(firstLine);
            //    Console.WriteLine(secondLine);
            //    if (logFile != null) {
            //        using (var writer = new StreamWriter(logFile, true)) {
            //            writer.WriteLine(firstLine);
            //            writer.WriteLine(secondLine);
            //            writer.Flush();
            //        }
            //    }
            //}
            log.Logger.Log(message, logLevel.ToLog4Net());

            //errors are special - they are exceptions that don't stop the program but we want to write them to a database
            //table
            if (logLevel.Equals(LogLevel.Error) && errorLogDB != null &&  dataUtils != null) {
                dataUtils.LogError(message);
            }
        }
        public void Log(Exception e, string message = null) {
            if (message == null) {
                message = e.StackTrace.ToString();
            } else {
                message = message + "\r\n" + e.ToString();
            }
            Log(message, LogLevel.Error);
        }

        /// <summary>
        /// Subclass for logging statistics to statsd
        /// Taken from https://github.com/etsy/statsd/blob/master/examples/csharp_example.cs
        ///
        /// Class to send UDP packets to a statsd instance.
        /// It is advisable to use StatsdSingleton instead of this class directly, due to overhead of opening/closing connections.
        /// </summary>
        /// <example>
        /// //Non-singleton version
        /// StatsdPipe statsd = new StatsdPipe("10.20.30.40", "8125");
        /// statsd.Increment("mysuperstat");
        /// </example>
        public class StatsdPipe : IDisposable {
            private readonly UdpClient udpClient;

            [ThreadStatic]
            private static Random random;

            private static Random Random {
                get {
                    return random ?? (random = new Random());
                }
            }

            public StatsdPipe(string host, int port) {
                udpClient = new UdpClient(host, port);
            }

            public bool Gauge(string key, int value) {
                return Gauge(key, value, 1.0);
            }

            public bool Gauge(string key, int value, double sampleRate) {
                return Send(sampleRate, String.Format("{0}:{1:d}|g", key, value));
            }

            public bool Timing(string key, int value) {
                return Timing(key, value, 1.0);
            }

            public bool Timing(string key, int value, double sampleRate) {
                return Send(sampleRate, String.Format("{0}:{1:d}|ms", key, value));
            }

            public bool Decrement(string key) {
                return Increment(key, -1, 1.0);
            }

            public bool Decrement(string key, int magnitude) {
                return Decrement(key, magnitude, 1.0);
            }

            public bool Decrement(string key, int magnitude, double sampleRate) {
                magnitude = magnitude < 0 ? magnitude : -magnitude;
                return Increment(key, magnitude, sampleRate);
            }

            public bool Decrement(params string[] keys) {
                return Increment(-1, 1.0, keys);
            }

            public bool Decrement(int magnitude, params string[] keys) {
                magnitude = magnitude < 0 ? magnitude : -magnitude;
                return Increment(magnitude, 1.0, keys);
            }

            public bool Decrement(int magnitude, double sampleRate, params string[] keys) {
                magnitude = magnitude < 0 ? magnitude : -magnitude;
                return Increment(magnitude, sampleRate, keys);
            }

            public bool Increment(string key) {
                return Increment(key, 1, 1.0);
            }

            public bool Increment(string key, int magnitude) {
                return Increment(key, magnitude, 1.0);
            }

            public bool Increment(string key, int magnitude, double sampleRate) {
                string stat = String.Format("{0}:{1}|c", key, magnitude);
                return Send(stat, sampleRate);
            }

            public bool Increment(int magnitude, double sampleRate, params string[] keys) {
                return Send(sampleRate, keys.Select(key => String.Format("{0}:{1}|c", key, magnitude)).ToArray());
            }

            protected bool Send(String stat, double sampleRate) {
                return Send(sampleRate, stat);
            }

            protected bool Send(double sampleRate, params string[] stats) {
                var retval = false; // didn't send anything
                if (sampleRate < 1.0) {
                    foreach (var stat in stats) {
                        if (Random.NextDouble() <= sampleRate) {
                            var statFormatted = String.Format("{0}|@{1:f}", stat, sampleRate);
                            if (DoSend(statFormatted)) {
                                retval = true;
                            }
                        }
                    }
                } else {
                    foreach (var stat in stats) {
                        if (DoSend(stat)) {
                            retval = true;
                        }
                    }
                }

                return retval;
            }

            protected bool DoSend(string stat) {
                var data = Encoding.Default.GetBytes(stat + "\n");

                udpClient.Send(data, data.Length);
                return true;
            }

            #region IDisposable Members

            public void Dispose() {
                try {
                    if (udpClient != null) {
                        udpClient.Close();
                    }
                } catch {
                }
            }

            #endregion
        }


    }
}

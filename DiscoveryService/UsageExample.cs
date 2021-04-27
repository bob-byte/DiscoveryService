using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Common.Logging;
using Common.Logging.Simple;

namespace LUC.DiscoveryService
{
    class UsageExample
    {
        static readonly Object ttyLock = new Object();

        private static void OnBadMessage(Object sender, Byte[] packet)
        {
            lock (ttyLock)
            {
                Console.WriteLine(">>> {0:O} <<<", DateTime.Now);
                Console.WriteLine("Malformed message (base64)");
                Console.WriteLine(Convert.ToBase64String(packet));
                // log message
            }

            Environment.Exit(1);
        }

        private static void OnGoodTcpMessage(Object sender, MessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== TCP {0:O} ===", DateTime.Now);
                Console.WriteLine(e.Message.ToString());
                // TODO take IP and groups from the message and save them in memory.
            }
        }

        private static void OnGoodUdpMessage(Object sender, MessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== UDP {0:O} ===", DateTime.Now);
                Console.WriteLine(e.Message.ToString());
                // do nothing, this is for debugging only
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Starting sending multicast messages, in order to receive TCP answers.");

            // set logger factory
            var properties = new Common.Logging.Configuration.NameValueCollection
            {
                ["level"] = "TRACE",
                ["showLogName"] = "true",
                ["showDateTime"] = "true",
                ["dateTimeFormat"] = "HH:mm:ss.fff"
            };
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

            foreach (var a in Service.GetIPAddresses())
            {
                Console.WriteLine($"IP address {a}");
            }

            ConcurrentDictionary<String, List<String>> groupsSupported = new ConcurrentDictionary<String, List<String>>();
            groupsSupported.TryAdd("192.168.1.100:17500", new List<String>
            {
                "the-dubstack-engineers-res",
                "the-dubstack-architects-res"
            });

            groupsSupported.TryAdd("192.168.13.140:17500", new List<String>
            {
                "the-dubstack-engineers-res",
                "the-dubstack-architects-res"
            });

            var serviceDiscovery = ServiceDiscovery.GetInstance(groupsSupported);
            serviceDiscovery.Start(out _);

            serviceDiscovery.Service.AnswerReceived += OnGoodTcpMessage;
            serviceDiscovery.Service.QueryReceived += OnGoodUdpMessage;
            serviceDiscovery.Service.MalformedMessage += OnBadMessage;

            serviceDiscovery.Service.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"discovered NIC '{nic.Name}'");
                };
            };

            DateTime start = DateTime.Now;
            Double intervalInMin = default;
            Int32 sendForInMin = 5;
            do
            {
                serviceDiscovery.QueryAllServices();
                Thread.Sleep(1000);

                DateTime end = DateTime.Now;
                intervalInMin = end.Subtract(start).TotalMinutes;
            }
            while (intervalInMin <= sendForInMin);

            serviceDiscovery.Stop();
            Console.ReadKey();
        }
    }
}

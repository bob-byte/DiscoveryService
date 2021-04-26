using System;
using System.Threading;
using LUC.DiscoveryService;
using Common.Logging;
using Common.Logging.Simple;
using System.Security.Cryptography.X509Certificates;

namespace LUC.DiscoveryService
{
    class UsageExample
    {
        static readonly object ttyLock = new object();

        private static void OnBadMessage(object sender, byte[] packet)
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

        private static void OnGoodTcpMessage(object sender, MessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== TCP {0:O} ===", DateTime.Now);
                Console.WriteLine(e.Message.ToString());
                // TODO take IP and groups from the message and save them in memory.
            }
        }

        private static void OnGoodUdpMessage(object sender, MessageEventArgs e)
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

            var serviceDiscovery = ServiceDiscovery.GetInstance(new X509Certificate());
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

            Thread.Sleep(9000000);
            
            serviceDiscovery.Stop();
            Console.ReadKey();
        }
    }
}

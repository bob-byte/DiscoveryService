using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using Common.Logging;
using Common.Logging.Simple;
using LUC.DiscoveryService.Messages;

namespace LUC.DiscoveryService
{
    class UsageExample
    {
        private static readonly Object ttyLock = new Object();

        /// <summary>
        ///   User Groups with their SSL certificates.
        ///   SSL should have SNI ( Server Name Indication ) feature enabled
        ///   This allows us to tell which group we are trying to connect to, so that the server knows which certificate to use.
        ///
        ///   We generate SSL and key/certificate pairs for every group. These are distributed from server to user’s computers 
        ///   which are authenticated for the buckets later.
        ///
        ///   These are rotated any time membership changes e.g., when someone is removed from a group/shared folder. 
        ///   We can require both ends of the HTTPS connection to authenticate with the same certificate (the certificate for the group).
        ///   This proves that both ends of the connection are authenticated.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public static ConcurrentDictionary<String, String> OurSupportedGroups { get; set; }

        public static ConcurrentDictionary<String, String> GroupsDiscovered { get; set; }

        public static ConcurrentDictionary<String, String> KnownIps { get; set; }


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

                if ((e.Message is TcpMessage message) && (e.RemoteEndPoint is IPEndPoint endPoint))
                {
                    var network = $"{endPoint.Address}:{message.KadPort}";
                    foreach (var group in message.GroupIds)
                    {
                        if(!GroupsDiscovered.TryAdd(network, group))
                        {
                            GroupsDiscovered.TryRemove(network, out _);
                            GroupsDiscovered.TryAdd(network, group);
                        }
                    }
                }
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

            ConcurrentDictionary<String, String> knownIps = new ConcurrentDictionary<String, String>();
            knownIps.TryAdd("the-dubstack-engineers-res", "192.168.1.100:17500");
            knownIps.TryAdd("the-dubstack-architects-res", "192.168.13.140:17500");

            ConcurrentDictionary<String, String> groupsSupported = new ConcurrentDictionary<String, String>();
            groupsSupported.TryAdd("the-dubstack-engineers-res", "<SSL-Cert1>");
            groupsSupported.TryAdd("the-dubstack-architects-res", "<SSL-Cert2>");

            var serviceDiscovery = ServiceDiscovery.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1));
            serviceDiscovery.Start();

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
            Double intervalInMin = 0;
            Int32 sendForInMin = 5;
            do
            {
                serviceDiscovery.QueryAllServices();
                Thread.Sleep(1000 * 60);

                DateTime end = DateTime.Now;
                intervalInMin = end.Subtract(start).TotalMinutes;
            }
            while (intervalInMin <= sendForInMin);

            serviceDiscovery.Stop();
            Console.ReadKey();
        }
    }
}

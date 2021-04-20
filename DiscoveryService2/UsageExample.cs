//
// This program demonstrates usage of DiscoveryService.
//
﻿using Common.Logging;
using Common.Logging.Simple;
using System;
using System.Linq;

namespace LUC.DiscoveryService
{
    class Program
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

            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            Random random = new Random();
            var machineId = $"{deviceIdBuilder.GetDeviceId()}-{random.GenerateRandomSymbols(5)}";

	    var groups = new Dictionary() {
		{"the-mybrand-engineers-res", "<SSL Cert>"},
		{"the-mybrand-anothergroup-res", "<SSL Cert>"}
	    };
	    var protocolVersion = 1;
            var serviceProfile = new ServiceProfile(protocolVersion, machineId, 17500, 17510, groups);

            var service = new MulticastService();
	    // TODO: find possibility to get those values from profile within the MulticastService
	    service.ProtocolVersion = protocolVersion;
	    service.MachineId = machineId;

            foreach (var a in MulticastService.GetIPAddresses())
            {
                Console.WriteLine($"IP address {a}");
            }

            service.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"discovered NIC '{nic.Name}'");
                };
		// Send muticast message
		service.SendQuery();
            };
            service.AnswerReceived += OnGoodTcpMessage;
            service.QueryReceived += OnGoodUdpMessage;
            service.MalformedMessage += OnBadMessage;

            var sd = new ServiceDiscovery(service);
	    sd.QueryAllServices();

	    // here we add profile to registered profiles list, in order to be able to stop service
	    // and to let know other hosts we are stopping serving TCP port.
            sd.Advertise(serviceProfile);

            service.Start();
            Console.ReadKey();
        }
    }
}

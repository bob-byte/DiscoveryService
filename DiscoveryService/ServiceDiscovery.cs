using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using LUC.DiscoveryService.Messages;
using LUC.Interfaces;
using LUC.Services.Implementation;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN.
    /// </summary>
    public class ServiceDiscovery
    {
        private const Int32 MillisecondsPerSecond = 1000;
        private const Int32 PeriodSendingsInMs = 60 * MillisecondsPerSecond;

        private const Int32 UdpPort = 17500;
        private const Int32 MinValueTcpPort = 17500;
        private const Int32 MaxValueTcpPort = 17510;

        [Import(typeof(ILoggingService))]
        private static readonly ILoggingService log = new LoggingService();
        private static ServiceDiscovery instance;

        private readonly ServiceProfile profile;
        private Service service;

        private CancellationTokenSource sourceInnerService, sourceOuterService;

        private Boolean isDiscoveryServiceStarted = false;

        private ServiceDiscovery(Boolean useIpv4, Boolean useIpv6, X509Certificate certificate, Dictionary<EndPoint, List<X509Certificate>> groupsSupported = null)
        {
            profile = new ServiceProfile(MinValueTcpPort, MaxValueTcpPort, UdpPort, 
                Message.ProtocolVersion, certificate, groupsSupported);
            service = new Service(profile);

            service.UseIpv4 = useIpv4;
            service.UseIpv6 = useIpv6;
        }

        private ServiceDiscovery(X509Certificate certificate, Dictionary<EndPoint, List<X509Certificate>> groupsSupported = null)
        {
            profile = new ServiceProfile(MinValueTcpPort, MaxValueTcpPort, UdpPort, 
                Message.ProtocolVersion, certificate, groupsSupported);
            service = new Service(profile);
        }

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~ServiceDiscovery()
        {
            Stop();
        }

        public Dictionary<EndPoint, List<X509Certificate>> GroupsSupported
        {
            get
            {
                lock (Lock.lockGroupsSupported)
                {
                    return profile.GroupsSupported;
                }
            }
        }

        /// <summary>
        ///    Stop listening TCP, UDP messages and sending them
        /// </summary>
        public void Stop()
        {
            if (isDiscoveryServiceStarted)
            {
                //Stop inner tasks of sending and listening
                sourceInnerService.Cancel();

                //Stop outer tasks of sending and listening
                sourceOuterService.Cancel();

                service.Stop();
                service = null;

                isDiscoveryServiceStarted = false;
            }
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public static ServiceDiscovery GetInstance(X509Certificate certificate, Dictionary<EndPoint, List<X509Certificate>> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.lockService, new ServiceDiscovery(certificate, groupsSupported), ref instance);
            return instance;
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public static ServiceDiscovery GetInstance(Boolean useIpv4, Boolean useIpv6, X509Certificate certificate, Dictionary<EndPoint, List<X509Certificate>> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.lockService, new ServiceDiscovery(useIpv4, useIpv6, certificate, groupsSupported), ref instance);
            return instance;
        }

        /// <summary>
        ///    Start listening TCP, UDP messages and sending them
        /// </summary>
        public void Start(out String machineId)
        {
            service.Start();

            sourceOuterService = new CancellationTokenSource();
            var tokenMulticastSendings = sourceOuterService.Token;
            sourceInnerService = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                while (!tokenMulticastSendings.IsCancellationRequested)
                {
                    await service.SendQuery(PeriodSendingsInMs, sourceInnerService, tokenMulticastSendings);

                    //if we don't initialize this, token will have property IsCancellationRequested equals to true
                    sourceInnerService = new CancellationTokenSource();
                }
            }, tokenMulticastSendings);

            machineId = profile.MachineId;
            isDiscoveryServiceStarted = true;
        }
    }
}
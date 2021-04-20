using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN.
    /// </summary>
    public class ServiceDiscovery : IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ServiceDiscovery));

        List<ServiceProfile> profiles = new List<ServiceProfile>();

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public ServiceDiscovery()
            : this(new MulticastService())
        {
            // Auto start.
            Service.Start();
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class with
        ///   the specified <see cref="MulticastService"/>.
        /// </summary>
        /// <param name="service">
        ///   The underlaying <see cref="MulticastService"/> to use.
        /// </param>
        public ServiceDiscovery(MulticastService service)
        {
            this.Service = service;
            mdns.QueryReceived += OnQuery;
            mdns.AnswerReceived += OnAnswer;
        }

        /// <summary>
        ///   Gets the multicasting service.
        /// </summary>
        /// <value>
        ///   It ss used to send and recieve multicast <see cref="Message">messages</see>.
        /// </value>
        public MulticastService Service { get; private set; }

        /// <summary>
        ///   Raised when a response is received.
        /// </summary>
        /// <value>
        ///   Contains the service IP.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers
        ///   When an anwser is received this event is raised.
        ///   <para>
        ///   Use <see cref="QueryAllServices"/> to initiate question.
        ///   </para>
        /// </remarks>
        public event EventHandler<string> ServiceDiscovered;

        /// <summary>
        ///   Raised when a servive instance is discovered.
        /// </summary>
        /// <value>
        ///   Contains the service instance ip.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers.
        ///   When an answer containing a PTR to a service instance is received 
        ///   this event is raised.
        /// </remarks>
        public event EventHandler<ServiceInstanceDiscoveryEventArgs> ServiceInstanceDiscovered;

        /// <summary>
        ///   Raised when a servive instance is shutting down.
        /// </summary>
        /// <value>
        ///   Contains the service instance ip.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers.
        ///   When an answer containing type 1 received this event is raised.
        /// </remarks>
        public event EventHandler<ServiceInstanceShutdownEventArgs> ServiceInstanceShutdown;

        /// <summary>
        ///    Asks other services to send their IP addresses.
        /// </summary>
        /// <remarks>
        ///   When an answer is received the <see cref="ServiceDiscovered"/> event is raised.
        /// </remarks>
        public void QueryAllServices()
        {
            Service.SendQuery(tcpPort);
        }

        /// <summary>
        ///   Advertise a service profile.
        /// </summary>
        /// <param name="service">
        ///   The service profile.
        /// </param>
        /// <remarks>
        ///   Any queries for the service will be answered with information from the profile.
        /// </remarks>
        public void Advertise(ServiceProfile service)
        {
            profiles.Add(service);
        }

        /// <summary>
        /// Sends a goodbye message for the provided
        /// profile and removes its pointer from the name sever.
        /// </summary>
        /// <param name="profile">The profile to send a goodbye message for.</param>
        public void Unadvertise(ServiceProfile profile)
        {
            var message = new Message { Type = 1 };

            Service.SendAnswer(message);
        }

        /// <summary>
        /// Sends a goodbye message for each anounced service.
        /// </summary>
        public void Unadvertise()
        {
            profiles.ForEach(profile => Unadvertise(profile));
        }

        /// <summary>
        ///    Sends Service response describing the service profile.
        /// </summary>
        /// <param name="profile">
        ///   The profile to describe.
        /// </param>
        /// <remarks>
        ///   Sends a Service response <see cref="Message"/> containing the pointer
        ///   and resource records of the <paramref name="profile"/>.
        ///   <para>
        ///   To provide increased robustness against packet loss,
        ///   two unsolicited responses are sent one second apart.
        ///   </para>
        /// </remarks>
        public void Announce(ServiceProfile profile)
        {
            var message = new Message {
		ProtocolVersion = profile.ProtocolVersion,
		Groups = profile.Groups
	    };

            Service.SendAnswer(message, checkDuplicate: false);
            Task.Delay(1000).Wait();
            Service.SendAnswer(message, checkDuplicate: false);
        }

        void OnQuery(object sender, MessageEventArgs e)
        {
            var request = e.Message;

            if (log.IsDebugEnabled)
            {
                log.Debug($"Query from {e.RemoteEndPoint}");
            }
            if (log.IsTraceEnabled)
            {
                log.Trace(request);
            }

            var response = NameServer.ResolveAsync(request).Result;

            if (response.Status != MessageStatus.NoError)
            {
                return;
            }

            // Many bonjour browsers don't like DNS-SD response
            // with additional records.
            if (response.Answers.Any(a => a.Name == ServiceName))
            {
                response.AdditionalRecords.Clear();
            }

            if (!response.Answers.Any(a => a.Name == ServiceName))
            {
                ;
            }

            Service.SendAnswer(response, e);

            if (log.IsDebugEnabled)
            {
                log.Debug($"Sending answer");
            }
            if (log.IsTraceEnabled)
            {
                log.Trace(response);
            }
            // Console.WriteLine($"Response time {(DateTime.Now - request.CreationTime).TotalMilliseconds}ms");
        }

        #region IDisposable Support

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Service != null)
                {
                    Service.QueryReceived -= OnQuery;
                    Service.AnswerReceived -= OnAnswer;
                    Service.Dispose();
                    Service = null;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}

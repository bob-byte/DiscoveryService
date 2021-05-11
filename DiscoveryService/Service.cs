using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using LUC.Interfaces;
using LUC.Services.Implementation;
using Makaretu.Dns;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Muticast LightUpon.Cloud Service.
    /// </summary>
    /// <remarks>
    ///   Sends UDP queries via the multicast mechachism
    ///   defined in <see href="https://tools.ietf.org/html/rfc6762"/>.
    ///   Receives UDP queries and answers with TCP/IP/SSL responses.
    ///   <para>
    ///   One of the events, <see cref="QueryReceived"/> or <see cref="AnswerReceived"/>, is
    ///   raised when a <see cref="Message"/> is received.
    ///   </para>
    /// </remarks>
    public class Service
    {
        [Import(typeof(ILoggingService))]
        static readonly ILoggingService log = new LoggingService();
        // 169.254.0.0/16 -- a link-local IPv4 address in accordance with RFC 3927.
        //private static readonly IPNetwork[] LinkLocalNetworks = new[] { IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("fe80::/10") };

        internal static List<NetworkInterface> KnownNics { get; private set; } = new List<NetworkInterface>();
        
        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private readonly RecentMessages receivedMessages = new RecentMessages();

        /// <summary>
        ///   The multicast client.
        /// </summary>
        private Client client;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        private readonly Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkInterfacesFilter;

        private readonly ServiceProfile profile;

        /// <summary>
        ///   Set the default TTLs.
        /// </summary>
        /// <seealso cref="ResourceRecord.DefaultTTL"/>
        /// <seealso cref="ResourceRecord.DefaultHostTTL"/>
        static Service()
        {
            // https://tools.ietf.org/html/rfc6762 section 10
            ResourceRecord.DefaultTTL = TimeSpan.FromMinutes(75);
            ResourceRecord.DefaultHostTTL = TimeSpan.FromSeconds(120);
        }

        /// <summary>
        ///   Raised when any service sends a query.
        /// </summary>
        /// <value>
        ///   Contains the query <see cref="Message"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        /// <seealso cref="SendQuery(Message)"/>
        public event EventHandler<MessageEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when one or more network interfaces are discovered. 
        /// </summary>
        /// <value>
        ///   Contains the network interface(s).
        /// </value>
        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        /// <summary>
        ///   Raised when any link-local service responds to a query.
        /// </summary>
        /// <value>
        ///   Contains the answer <see cref="TcpMessage"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        public event EventHandler<MessageEventArgs> AnswerReceived;

        /// <summary>
        ///   Raised when message is received that cannot be decoded.
        /// </summary>
        /// <value>
        ///   The message as a byte array.
        /// </value>
        public event EventHandler<Byte[]> MalformedMessage;

        /// <summary>
        ///   Create a new instance of the <see cref="Service"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        internal Service(ServiceProfile profile, Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
        {
            this.profile = profile;

            networkInterfacesFilter = filter;
            UseIpv4 = Socket.OSSupportsIPv4;
            UseIpv6 = Socket.OSSupportsIPv6;

            IgnoreDuplicateMessages = true;
        }

        /// <summary>
        ///   Send and receive on IPv4.
        /// </summary>
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        public Boolean UseIpv4 { get; set; }

        /// <summary>
        ///   Send and receive on IPv6.
        /// </summary>
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        public Boolean UseIpv6 { get; set; }

        /// <summary>
        ///   Determines if received messages are checked for duplicates.
        /// </summary>
        /// <value>
        ///   <b>true</b> to ignore duplicate messages. Defaults to <b>true</b>.
        /// </value>
        /// <remarks>
        ///   When set, a message that has been received within the last minute
        ///   will be ignored.
        /// </remarks>
        public Boolean IgnoreDuplicateMessages { get; set; }

        /// <summary>
        ///   Get the network interfaces that are useable.
        /// </summary>
        /// <returns>
        ///   A sequence of <see cref="NetworkInterface"/>.
        /// </returns>
        /// <remarks>
        ///   The following filters are applied
        ///   <list type="bullet">
        ///   <item><description>interface is enabled</description></item>
        ///   <item><description>interface is not a loopback</description></item>
        ///   </list>
        ///   <para>
        ///   If no network interface is operational, then the loopback interface(s)
        ///   are included (127.0.0.1 and/or ::1).
        ///   </para>
        /// </remarks>
        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();
            if (nics.Length > 0)
                return nics;

            // Special case: no operational NIC, then use loopbacks.
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up);
        }

        /// <summary>
        ///   Get the IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses of the local machine.
        /// </returns>
        /// <remarks>
        ///   The loopback addresses (127.0.0.1 and ::1) are NOT included in the
        ///   returned sequences.
        /// </remarks>
        public static IEnumerable<IPAddress> GetIPAddresses()
        {
            return GetNetworkInterfaces()
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        /// <summary>
        ///   Get the link local IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses.
        /// </returns>
        /// <remarks>
        ///   All IPv4 addresses are considered link local.
        /// </remarks>
        /// <seealso href="https://en.wikipedia.org/wiki/Link-local_address"/>
        public static IEnumerable<IPAddress> GetLinkLocalAddresses()
        {
            return GetIPAddresses()
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                    (a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal));
        }

        /// <summary>
        ///   Start the service.
        /// </summary>
        public void Start()
        {
            KnownNics.Clear();

            FindNetworkInterfaces();
        }

        /// <summary>
        ///   Stop the service.
        /// </summary>
        /// <remarks>
        ///   Clears all the event handlers.
        /// </remarks>
        public void Stop()
        {
            // All event handlers are cleared.
            QueryReceived = null;
            AnswerReceived = null;
            NetworkInterfaceDiscovered = null;

            // Stop current UDP listener
            client?.Dispose();
            client = null;
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e) => FindNetworkInterfaces();

        private void FindNetworkInterfaces()
        {
            //log.LogInfo("Finding network interfaces");

            try
            {
                var currentNics = GetNetworkInterfaces().ToList();

                var newNics = new List<NetworkInterface>();
                var oldNics = new List<NetworkInterface>();

                foreach (var nic in KnownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
                {
                    oldNics.Add(nic);

                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Removed nic '{nic.Name}'.");
                    //}
                }

                foreach (var nic in currentNics.Where(nic => !KnownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);

                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Found nic '{nic.Name}'.");
                    //}
                }

                KnownNics = currentNics;

                // Only create client if something has change.
                if (newNics.Any() || oldNics.Any())
                {
                    client?.Dispose();
                    client = new Client(profile, UseIpv4, UseIpv6, networkInterfacesFilter?.Invoke(KnownNics) ?? KnownNics);
                    client.UdpMessageReceived += OnUdpMessage;
                    client.TcpMessageReceived += OnTcpMessage;
                }

                if(newNics.Any())
                {
                    NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                    {
                        NetworkInterfaces = newNics
                    });
                }

                // Situation has seen when NetworkAddressChanged is not triggered 
                // (wifi off, but NIC is not disabled, wifi - on, NIC was not changed 
                // so no event). Rebinding fixes this.
                //
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
            catch (Exception e)
            {
                //log.Error("FindNics failed", e);
            }
        }

        /// <summary>
        ///   Called by the MulticastClient when a UDP message is received.
        /// </summary>
        /// <param name="sender">
        ///   The <see cref="Client"/> that got the message.
        /// </param>
        /// <param name="result">
        ///   The received message <see cref="UdpReceiveResult"/>.
        /// </param>
        /// <remarks>
        ///   Decodes the <paramref name="result"/> and then raises
        ///   either the <see cref="QueryReceived"/> event.
        ///   <para>
        ///   Multicast messages received with different protocol version or 
        ///   the same machine Id as ours are silently ignored.
        ///   </para>
        ///   <para>
        ///   If the message cannot be decoded, then the <see cref="MalformedMessage"/>
        ///   event is raised.
        ///   </para>
        /// </remarks>
        public void OnUdpMessage(object sender, UdpReceiveResult result)
        {
            // If recently received, then ignore.
            if (IgnoreDuplicateMessages && !receivedMessages.TryAdd(result.Buffer))
            {
                return;
            }

            MulticastMessage message = new MulticastMessage();
            try
            {
                message.Read(result.Buffer);
                
                
            }
            catch (Exception e)
            {
                //log.LogError("Received malformed message", e);
                MalformedMessage.Invoke(sender, result.Buffer);
                return; // eat the exception
            }

            if ((message.VersionOfProtocol != Messages.Message.ProtocolVersion) || 
                (message.MachineId == profile.MachineId))
            {
                return;
            }

            // Dispatch the message.
            try
            {
                QueryReceived?.Invoke(this, new MessageEventArgs { Message = message, RemoteEndPoint = result.RemoteEndPoint });
            }
            catch (Exception e)
            {
                //log.LogError("Receive handler failed", e);
                // eat the exception
            }
        }

        /// <summary>
        /// Called by <see cref="Client.TcpMessageReceived"/> in method <see cref="Client.ListenTcp(TcpListener)"/>
        /// </summary>
        /// <param name="message">
        /// Received message
        /// </param>
        internal void OnTcpMessage(Object sender, MessageEventArgs receiveResult)
        {
            if(receiveResult.Message is TcpMessage message)
            {
                AnswerReceived.Invoke(sender, new MessageEventArgs
                {
                    Message = message,
                    RemoteEndPoint = receiveResult.RemoteEndPoint
                });
            }
        }

        /// <summary>
        ///   Sends UDP messages
        /// </summary>
        /// <returns>
        /// <see cref="Task"/> of sending UDP message
        /// </returns>
        public Task SendQuery()
        {
            return Task.Run(async () =>
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    try
                    {
                        Random random = new Random();
                        var mess = new MulticastMessage(messageId: (UInt32)random.Next(0, Int32.MaxValue), profile.MachineId, profile.RunningTcpPort);
                        var bytes = mess.ToByteArray();

                        if (client != null)
                        {
                            await client.SendUdpAsync(bytes).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        //loggingService.LogError(ex,ex.Message);
                    }
                }
            });
        }

    }
}

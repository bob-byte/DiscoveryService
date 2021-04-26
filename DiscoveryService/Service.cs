using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Extensions;
using LUC.DiscoveryService.Messages;
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
        //[Import(typeof(ILoggingService))]
        //static readonly ILoggingService log = new LoggingService();
        // 169.254.0.0/16 -- a link-local IPv4 address in accordance with RFC 3927.
        private static readonly IPNetwork[] LinkLocalNetworks = new[] { IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("fe80::/10") };

        private List<NetworkInterface> knownNics = new List<NetworkInterface>();

        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private RecentMessages receivedMessages = new RecentMessages();

        /// <summary>
        ///   The multicast client.
        /// </summary>
        private Client client;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        private Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkInterfacesFilter;

        private ServiceProfile profile;

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

        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        public event EventHandler<MessageEventArgs> AnswerReceived;

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
            QueryReceived += SendTcp;
        }

        internal void SendTcp(Object sender, MessageEventArgs e)
        {
            var parsingSsl = new ParsingTcpData();
            try
            {
                Byte[] bytes = parsingSsl.GetDecodedData(new TcpMessage(e.Message.VersionOfProtocol, profile.GroupsSupported));

                TcpClient client = new TcpClient(e.RemoteEndPoint.AddressFamily);
                if(!(e.Message is MulticastMessage message))
                {
                    throw new ArgumentException("Bad format of the message");
                }
                client.Connect(((IPEndPoint)e.RemoteEndPoint).Address, message.TcpPort);

                var stream = client.GetStream();
                stream.Write(bytes, 0, bytes.Length);

                stream.Close();
                client.Close();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        ///   Send and receive on IPv4.
        /// </summary>
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        public bool UseIpv4 { get; set; }

        /// <summary>
        ///   Send and receive on IPv6.
        /// </summary>
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        public bool UseIpv6 { get; set; }

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
        public bool IgnoreDuplicateMessages { get; set; }

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
            knownNics.Clear();

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

                foreach (var nic in knownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
                {
                    oldNics.Add(nic);

                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Removed nic '{nic.Name}'.");
                    //}
                }

                foreach (var nic in currentNics.Where(nic => !knownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);

                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Found nic '{nic.Name}'.");
                    //}
                }

                knownNics = currentNics;

                // Only create client if something has change.
                if (newNics.Any() || oldNics.Any())
                {
                    client?.Dispose();
                    client = new Client(profile, UseIpv4, UseIpv6, networkInterfacesFilter?.Invoke(knownNics) ?? knownNics);
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

                // Magic from @eshvatskyi
                //
                // I've seen situation when NetworkAddressChanged is not triggered 
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
            if (!IgnoreDuplicateMessages && !receivedMessages.TryAdd(result.Buffer))
            {
                return;
            }

            //var msg = new Messages.BroadcastMessage();
            Parsing<MulticastMessage> parsing = new ParsingMulticastData();
            MulticastMessage message;
            try
            {
                message = parsing.GetEncodedData(result.Buffer);
            }
            catch (Exception e)
            {
                //log.LogError("Received malformed message", e);
                MalformedMessage.Invoke(sender, result.Buffer);
                return; // eat the exception
            }

            //if ((message.VersionOfProtocol != Messages.Message.ProtocolVersion)
            //    ||
            //    (message.MachineId == profile.MachineId)
            //    ||
            //    (message.Status != MessageStatus.NoError))
            //{
            //    return;
            //}

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
        internal void OnTcpMessage(Object sender, TcpMessage message)
        {
            Boolean isRightMessage = true;

            try
            {
                foreach (var group in message.GroupsSupported)
                {
                    if (!profile.GroupsSupported.ContainsValue(group.Value))
                    {
                        if (profile.GroupsSupported.ContainsKey(group.Key))
                        {
                            profile.GroupsSupported.Remove(group.Key);
                        }

                        profile.GroupsSupported.Add(group.Key, group.Value);
                        isRightMessage = true;
                    }
                }
            }
            catch
            {
                Parsing<TcpMessage> parsing = new ParsingTcpData();
                var bytes = parsing.GetDecodedData(message);

                MalformedMessage.Invoke(sender, bytes);
            }

            if(isRightMessage)
            {
                AnswerReceived.Invoke(sender, new MessageEventArgs
                {
                    Message = message,
                    GroupsSupported = message.GroupsSupported
                });
            }
        }

        /// <summary>
        ///   Sends udp messages
        /// </summary>
        /// <param name="period">
        /// How often to send messages
        /// </param>
        /// <param name="innerTokenSource">
        /// TokenSource for cancellation task
        /// </param>
        /// <param name="tokenOuter">
        /// Token of outer task, which is created in <see cref="ServiceDiscovery"/>
        /// </param>
        /// <returns></returns>
        public Task SendQuery(Int32 period, CancellationTokenSource innerTokenSource, CancellationToken tokenOuter)
        {
            CancellationToken token = innerTokenSource.Token;
            Task taskClient = null;

            taskClient = Task.Run(async () =>
            {
                while (taskClient.WhetherToContinueTask(token))
                {
                    try
                    {
                        Parsing<MulticastMessage> parsing = new ParsingMulticastData();
                        var bytes = parsing.GetDecodedData(new MulticastMessage(profile.MachineId, profile.RunningTcpPort/*, Messages.Message.ProtocolVersion*/));

                        await client.SendUdpAsync(bytes, period, tokenOuter).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        innerTokenSource.Cancel();
                        //loggingService.LogError(ex, ex.Message);
                    }
                }
            }, token);

            return taskClient;
        }
    }
}

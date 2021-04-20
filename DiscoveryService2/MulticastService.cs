using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

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
    ///   Use <see cref="Start"/> to start listening for multicast messages.
    ///   One of the events, <see cref="QueryReceived"/> or <see cref="AnswerReceived"/>, is
    ///   raised when a <see cref="Message"/> is received.
    ///   </para>
    /// </remarks>
    public class MulticastService : IResolver, IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(MulticastService));
        // 169.254.0.0/16 -- a link-local IPv4 address in accordance with RFC 3927.
        static readonly IPNetwork[] LinkLocalNetworks = new[] { IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("fe80::/10") };

        List<NetworkInterface> knownNics = new List<NetworkInterface>();

        /// <summary>
        ///   Recently sent messages.
        /// </summary>
        RecentMessages sentMessages = new RecentMessages();

        /// <summary>
        ///   Recently received messages.
        /// </summary>
        RecentMessages receivedMessages = new RecentMessages();

        /// <summary>
        ///   The multicast client.
        /// </summary>
        MulticastClient client;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkInterfacesFilter;

        /// <summary>
        ///   Protocol version ( the same as in profile ).
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
	public ushort ProtocolVersion { get; set; }

        /// <summary>
        ///   A unique identifier for the service instance ( the same as in profile ).
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; set; }

        /// <summary>
        ///   Set the default TTLs.
        /// </summary>
        /// <seealso cref="ResourceRecord.DefaultTTL"/>
        /// <seealso cref="ResourceRecord.DefaultHostTTL"/>
        static MulticastService()
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
        ///   Raised when any link-local service responds to a query.
        /// </summary>
        /// <value>
        ///   Contains the answer <see cref="Message"/>.
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
        public event EventHandler<byte[]> MalformedMessage;

        /// <summary>
        ///   Raised when one or more network interfaces are discovered. 
        /// </summary>
        /// <value>
        ///   Contains the network interface(s).
        /// </value>
        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        /// <summary>
        ///   Create a new instance of the <see cref="MulticastService"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        public MulticastService(Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
        {
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

        void OnNetworkAddressChanged(object sender, EventArgs e) => FindNetworkInterfaces();

        void FindNetworkInterfaces()
        {
            log.Debug("Finding network interfaces");

            try
            {
                var currentNics = GetNetworkInterfaces().ToList();

                var newNics = new List<NetworkInterface>();
                var oldNics = new List<NetworkInterface>();

                foreach (var nic in knownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
                {
                    oldNics.Add(nic);

                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Removed nic '{nic.Name}'.");
                    }
                }

                foreach (var nic in currentNics.Where(nic => !knownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);

                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Found nic '{nic.Name}'.");
                    }
                }

                knownNics = currentNics;

                // Only create client if something has change.
                if (newNics.Any() || oldNics.Any())
                {
                    client?.Dispose();
                    client = new MulticastClient(UseIpv4, UseIpv6, networkInterfacesFilter?.Invoke(knownNics) ?? knownNics);
                    client.UdpMessageReceived += OnUdpMessage;
                    client.TcpMessageReceived += OnTcpMessage;
                }

                // Tell others.
                if (newNics.Any())
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
                log.Error("FindNics failed", e);
            }
        }

        /// <inheritdoc />
        public Task<Message> ResolveAsync(Message request, CancellationToken cancel = default(CancellationToken))
        {
            var tsc = new TaskCompletionSource<Message>();

            void checkResponse(object s, MessageEventArgs e)
            {
                var response = e.Message;
                if (request.Questions.All(q => response.Answers.Any(a => a.Name == q.Name)))
                {
                    AnswerReceived -= checkResponse;
                    tsc.SetResult(response);
                }
            }

            cancel.Register(() =>
            {
                AnswerReceived -= checkResponse;
                tsc.TrySetCanceled();
            });

            AnswerReceived += checkResponse;
            SendQuery(request);

            return tsc.Task;
        }

        /// <summary>
        ///   Ask for answers.
        /// </summary>
        /// <param name="tcpPort">
        ///   TCP port is the port we are listening on for responses
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        public void SendQuery(string machineId, ushort protocolVersion, ushort tcpPort)
        {
            var msg = new Message
            {
                ProtocolVersion = protocolVersion,
                TcpPort = tcpPort,
                MachineId = MachineId
		Type = 0  // receiving side should reply with TCP/IP connection
            };
            SendQuery(msg);
        }

        /// <summary>
        ///   Ask for answers.
        /// </summary>
        /// <param name="msg">
        ///   A query message.
        /// </param>
        /// <remarks>
        ///   Answers to any query are obtained on the <see cref="AnswerReceived"/>
        ///   event.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   When the serialised <paramref name="msg"/> is too large.
        /// </exception>
        public void SendQuery(Message msg)
        {
            Send(msg, false);
        }

        /// <summary>
        ///   Send an TCP/IP/SSL answer to a query.
        /// </summary>
        /// <param name="answer">
        ///   The answer message.
        /// </param>
        /// <param name="checkDuplicate">
        ///   If <b>true</b>, then if the same <paramref name="answer"/> was
        ///   recently sent it will not be sent again.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   When the serialised <paramref name="answer"/> is too large.
        /// </exception>
        /// <remarks>
        /// </remarks>
        /// <see cref="QueryReceived"/>
        /// <seealso cref="Message.CreateResponse"/>
        public void SendAnswer(Message answer, bool checkDuplicate = true)
        {
            // answer.Questions.Clear();
            Send(answer, checkDuplicate);
        }

	//
	// TODO: establish TCP/IP/SSL connection here
	// and send an answer with <version, groups supported>.
	//
        void Send(Message msg, bool checkDuplicate)
        {
            var packet = msg.ToByteArray();

            if (checkDuplicate && !sentMessages.TryAdd(packet))
            {
                return;
            }

            client?.SendAsync(packet).GetAwaiter().GetResult();
        }

        /// <summary>
        ///   Called by the MulticastClient when a UDP message is received.
        /// </summary>
        /// <param name="sender">
        ///   The <see cref="MulticastClient"/> that got the message.
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

            var msg = new Message();
            try
            {
                msg.Read(result.Buffer, 0, result.Buffer.Length);
            }
            catch (Exception e)
            {
                log.Warn("Received malformed message", e);
                MalformedMessage?.Invoke(this, result.Buffer);
                return; // eat the exception
            }

            if (msg.ProtocolVersion != ProtocolVersion || msg.MachineId == MachineId || msg.Status != MessageStatus.NoError)
            {
                return;
            }

            // Dispatch the message.
            try
            {
                QueryReceived?.Invoke(this, new MessageEventArgs { Message = msg, RemoteEndPoint = result.RemoteEndPoint });
            }
            catch (Exception e)
            {
                log.Error("Receive handler failed", e);
                // eat the exception
            }
        }

        /// <summary>
        ///   Called by the MulticastClient when a TCP message is received.
        /// </summary>
        /// <param name="sender">
        ///   The <see cref="MulticastClient"/> that got the message.
        /// </param>
        /// <param name="result">
        ///   The received message <see cref="TcpReceiveResult"/>.
        /// </param>
        /// <remarks>
        ///   Decodes the <paramref name="result"/> and then raises
        ///   either the <see cref="AnswerReceived"/> event.
        ///   <para>
        ///   Multicast messages received with different protocol version or 
	///   the same machine Id as ours are silently ignored.
        ///   </para>
        ///   <para>
        ///   If the message cannot be decoded, then the <see cref="MalformedMessage"/>
        ///   event is raised.
        ///   </para>
        /// </remarks>
        public void OnTcpMessage(object sender, UdpReceiveResult result)
        {
            // If recently received, then ignore.
            if (IgnoreDuplicateMessages && !receivedMessages.TryAdd(result.Buffer))
            {
                return;
            }

            var msg = new Message();
            try
            {
                msg.Read(result.Buffer, 0, result.Buffer.Length);
            }
            catch (Exception e)
            {
                log.Warn("Received malformed message", e);
                MalformedMessage?.Invoke(this, result.Buffer);
                return; // eat the exception
            }

            if (msg.ProtocolVersion != ProtocolVersion || msg.MachineId == MachineId || msg.Status != MessageStatus.NoError)
            {
                return;
            }

            // Dispatch the message.
            try
            {
                AnswerReceived?.Invoke(this, new MessageEventArgs { Message = msg, RemoteEndPoint = result.RemoteEndPoint });
            }
            catch (Exception e)
            {
                log.Error("Receive handler failed", e);
                // eat the exception
            }
        }


#region IDisposable Support

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
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

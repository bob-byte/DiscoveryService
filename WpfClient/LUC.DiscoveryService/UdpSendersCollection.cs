//#define TEST_WITH_ONE_IP_ADDRESS

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Common;
using LUC.Interfaces.Constants;

namespace LUC.DiscoveryServices
{
    class UdpSendersCollection : IDisposable
    {
        private readonly ConcurrentDictionary<IPAddress, UdpClient> m_sendersUdp = new ConcurrentDictionary<IPAddress, UdpClient>();

        private Boolean m_disposedValue;

        /// <summary>
        ///   Creates a new instance of the <see cref="Listeners"/> class.
        /// </summary>
        /// <param name="runningIpAddresses">
        /// NetworkInterfaces wherefrom we should send to
        /// </param>
        public UdpSendersCollection( ICollection<IPAddress> runningIpAddresses, Int32 udpPort )
        {
            foreach ( IPAddress address in runningIpAddresses )
            {
                if ( m_sendersUdp.Keys.Contains( address ) )
                {
                    DsLoggerSet.DefaultLogger.LogError( logRecord: $"{address} already exist in {nameof( m_sendersUdp )}" );
                    continue;
                }

                var localEndpoint = new IPEndPoint( address, udpPort );
                var senderUdp = new UdpClient( address.AddressFamily );

                try
                {
                    ConfigureMulticastSocket( senderUdp, localEndpoint );

                    m_sendersUdp.AddOrUpdate(
                        key: address,
                        addValueFactory: _ => senderUdp,
                        updateValueFactory: ( key, previousValue ) =>
                        {
                            DsLoggerSet.DefaultLogger.LogFatal( message: $"Updated value of {nameof( m_sendersUdp )}" );
                            return senderUdp;
                        }
                    );
                }
                // VPN NetworkInterfaces
                catch ( SocketException ex ) when ( ex.SocketErrorCode == SocketError.AddressNotAvailable )
                {
                    senderUdp.Dispose();
                }
                catch ( Exception ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Cannot configure UDP sender with IP-address {address}: {ex.Message}", ex );
                    senderUdp.Dispose();
                }
            }
        }

        private void ConfigureMulticastSocket( UdpClient multicastSocket, IPEndPoint localEndpoint )
        {
            //to bind to endpoint which is already in use
            multicastSocket.Client.SetSocketOption( 
                SocketOptionLevel.Socket, 
                SocketOptionName.ReuseAddress, 
                optionValue: true 
            );

            multicastSocket.Client.Bind( localEndpoint );

            Object multicastOption;
            SocketOptionLevel socketOptionLevel;
            Byte[] interfaceArray;
            switch ( multicastSocket.Client.AddressFamily )
            {
                case AddressFamily.InterNetwork:
                {
                    multicastOption = new MulticastOption( DsConstants.MulticastAddressIpv4 );
                    socketOptionLevel = SocketOptionLevel.IP;
                    interfaceArray = localEndpoint.Address.GetAddressBytes();

                    break;
                }

                case AddressFamily.InterNetworkV6:
                {
                    multicastOption = new IPv6MulticastOption( DsConstants.MulticastAddressIpv6 );
                    socketOptionLevel = SocketOptionLevel.IPv6;
                    interfaceArray = BitConverter.GetBytes( (Int32)localEndpoint.Address.ScopeId );

                    break;
                }

                default:
                {
                    throw new NotSupportedException( message: $"Address family {multicastSocket.Client.AddressFamily}." );
                }
            }

            //join socket to multicast group
            multicastSocket.Client.SetSocketOption( socketOptionLevel, SocketOptionName.AddMembership, multicastOption );

            //send multicast packages only in LAN
            Int32 idOfMulticastDataIsOnlyInLan = 1;
            multicastSocket.Client.SetSocketOption( socketOptionLevel, SocketOptionName.MulticastTimeToLive, idOfMulticastDataIsOnlyInLan );

            //Because multicast addresses are not routable, the network stack simply
            //picks the first interface in the routing table with a multicast route. In order
            //to change this behavior, the MulticastInterface option can be used to set the
            //local interface on which all outgoing multicast traffic is to be sent (for this
            //socket only). This is done by converting the 4 byte IPv4 address (or 16 byte
            //IPv6 address) into a byte array.
            multicastSocket.Client.SetSocketOption(
                socketOptionLevel,
                SocketOptionName.MulticastInterface,
                interfaceArray
            );
        }

        public Task SendMulticastsAsync( Byte[] packet, IoBehavior ioBehavior, AddressFamily addressFamily ) =>
            SendMulticastsAsync( packet, ioBehavior, m_sendersUdp.Where( c => c.Key.AddressFamily == addressFamily ) );

        /// <summary>
        /// It sends udp messages from each address, which is initialized in this class
        /// </summary>
        /// <param name="packet">
        /// Bytes of message to send
        /// </param>
        /// <returns>
        /// Task which allow to see any exception. Async void method doens't allow it
        /// </returns>
        public Task SendMulticastsAsync( Byte[] packet, IoBehavior ioBehavior ) =>
            SendMulticastsAsync( packet, ioBehavior, m_sendersUdp.ToArray() );

        private async Task SendMulticastsAsync( Byte[] packet, IoBehavior ioBehavior, IEnumerable<KeyValuePair<IPAddress, UdpClient>> udpSenders )
        {
            foreach ( KeyValuePair<IPAddress, UdpClient> sender in udpSenders )
            {
                try
                {
                    IPEndPoint endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ?
                        DsConstants.MulticastEndpointIpv4 : DsConstants.MulticastEndpointIpv6;

                    switch ( ioBehavior )
                    {
                        case IoBehavior.Asynchronous:
                        {
                            await sender.Value.SendAsync(
                                packet,
                                packet.Length,
                                endpoint
                            ).ConfigureAwait( continueOnCapturedContext: false );
                            break;
                        }

                        case IoBehavior.Synchronous:
                        {
                            sender.Value.Send( packet, packet.Length, endpoint );
                            break;
                        }

                        default:
                        {
                            throw new ArgumentException( message: "Incorrect value", paramName: nameof( ioBehavior ) );
                        }
                    }

                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Successfully sent UDP message ({packet.Length} bytes) by {sender.Value.Client.LocalEndPoint}" );

#if TEST_WITH_ONE_IP_ADDRESS
                    break;
#endif
                }
                catch ( SocketException e )
                {
                    DsLoggerSet.DefaultLogger.LogFatal( $"Failed to send UDP message, SocketException: {e.Message}" );
                }
                catch ( InvalidOperationException e )
                {
                    DsLoggerSet.DefaultLogger.LogFatal( $"Failed to send UDP message, InvalidOperationException: {e.Message}" );
                }
            }
        }

        protected virtual void Dispose( Boolean disposing )
        {
            if ( !m_disposedValue )
            {
                foreach ( IPAddress address in m_sendersUdp.Keys )
                {
                    if ( m_sendersUdp.TryRemove( address, out UdpClient sender ) )
                    {
                        try
                        {
                            sender.Dispose();
                        }
                        catch
                        {
                            // eat it.
                        }
                    }
                }

                m_sendersUdp.Clear();

                m_disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose( disposing: true );
            GC.SuppressFinalize( this );
        }
    }
}

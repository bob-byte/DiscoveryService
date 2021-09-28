using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryService.Common;

namespace LUC.DiscoveryService
{
    class UdpSenders : IDisposable
    {
        private readonly ConcurrentDictionary<IPAddress, UdpClient> m_sendersUdp = new ConcurrentDictionary<IPAddress, UdpClient>();

        private Boolean m_disposedValue;

        /// <summary>
        ///   Creates a new instance of the <see cref="Listeners"/> class.
        /// </summary>
        /// <param name="profile">
        /// Info about current peer
        /// </param>
        /// <param name="useIpv4">
        /// Send and receive on IPv4.
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="useIpv6">
        /// Send and receive on IPv6.
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="runningIpAddresses">
        /// NetworkInterfaces wherefrom we should send to
        /// </param>
        public UdpSenders( ICollection<IPAddress> runningIpAddresses )
        {
            foreach ( IPAddress address in runningIpAddresses )
            {
                if ( m_sendersUdp.Keys.Contains( address ) )
                {
                    continue;
                }

                IPEndPoint localEndpoint = new IPEndPoint( address, AbstractService.DEFAULT_PORT );
                UdpClient senderUdp = new UdpClient( address.AddressFamily );

                try
                {
                    switch ( address.AddressFamily )
                    {
                        case AddressFamily.InterNetwork:
                        {
                            senderUdp.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );

                            senderUdp.Client.Bind( localEndpoint );

                            senderUdp.Client.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue: new MulticastOption( Constants.MulticastAddressIp4 ) );
                            senderUdp.Client.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, optionValue: true );

                            break;
                        }

                        case AddressFamily.InterNetworkV6:
                        {
                            senderUdp.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );

                            senderUdp.Client.Bind( localEndpoint );
                            senderUdp.Client.SetSocketOption( SocketOptionLevel.IPv6, SocketOptionName.AddMembership, optionValue: new IPv6MulticastOption( Constants.MulticastAddressIp6 ) );
                            senderUdp.Client.SetSocketOption( SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, optionValue: true );

                            break;
                        }

                        default:
                        {
                            throw new NotSupportedException( $"Address family {address.AddressFamily}." );
                        }
                    }

                    if ( !m_sendersUdp.TryAdd( address, senderUdp ) ) // Should not fail
                    {
                        senderUdp.Dispose();
                    }
                }
                catch ( SocketException ex ) when ( ex.SocketErrorCode == SocketError.AddressNotAvailable )
                {
                    // VPN NetworkInterfaces
                    senderUdp.Dispose();
                }
                catch ( Exception e )
                {
                    AbstractService.LoggingService.LogError( $"Cannot setup send socket for {address}: {e.Message}" );
                    senderUdp.Dispose();
                }
            }
        }

        /// <summary>
        /// It sends udp messages from each address, which is initialized in this class
        /// </summary>
        /// <param name="message">
        /// Bytes of message to send
        /// </param>
        /// <returns>
        /// Task which allow to see any exception. Async void method doens't allow it
        /// </returns>
        public async Task SendUdpAsync( Byte[] message )
        {
            foreach ( KeyValuePair<IPAddress, UdpClient> sender in m_sendersUdp )
            {
                try
                {
                    IPEndPoint endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ?
                        Constants.MulticastEndpointIp4 : Constants.MulticastEndpointIp6;
                    await sender.Value.SendAsync( message, message.Length, endpoint ).
                        ConfigureAwait( continueOnCapturedContext: false );

                    break;
                }
                catch ( SocketException e )
                {
                    AbstractService.LoggingService.LogError( $"Failed to send UDP message, SocketException: {e.Message}" );
                }
                catch ( InvalidOperationException e )
                {
                    AbstractService.LoggingService.LogError( $"Failed to send UDP message, InvalidOperationException: {e.Message}" );
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

        ~UdpSenders()
        {
            Dispose( disposing: false );
        }

        public void Dispose()
        {
            Dispose( disposing: true );
            GC.SuppressFinalize( this );
        }
    }
}

using System;
using System.Net;

using AutoFixture;
using AutoFixture.Kernel;

using DiscoveryServices.Kademlia.ClientPool;
using DiscoveryServices.Test.Builders;
using DiscoveryServices.Test.InternalTests.Builders;

namespace DiscoveryServices.Test.InternalTests.Customizations
{
    class SocketBasicCustomization : ICustomization
    {
        private readonly BuildEndPointRequest m_endPointRequest;

        public SocketBasicCustomization( BuildEndPointRequest endPointRequest )
        {
            m_endPointRequest = endPointRequest;
        }

        public virtual void Customize( IFixture fixture )
        {
            fixture.Customize<ConnectionPool.Socket>( c => c.OmitAutoProperties() );
            fixture.Customizations.Add( new TypeRelay( from: typeof( EndPoint ), to: typeof( IPEndPoint ) ) );

            var endPointBuilder = new EndPointsBuilder( m_endPointRequest );
            IPEndPoint endPoint = endPointBuilder.Create<IPEndPoint>();
            if ( endPoint != null )
            {
                fixture.Customizations.Add( new SocketBuilder( DsSetUpTests.LoggingService, endPoint ) );
            }
            else
            {
                throw new InvalidOperationException( $"{nameof( DiscoveryService )} hasn't reachable {nameof( IPAddress )}" );
            }
        }
    }
}

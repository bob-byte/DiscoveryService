using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using AutoFixture;
using AutoFixture.Kernel;

using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Test.Builders;
using LUC.DiscoveryService.Test.InternalTests.Builders;

namespace LUC.DiscoveryService.Test.InternalTests.Customizations
{
    class ConnectionPoolSocketBasicCustomization : ICustomization
    {
        private readonly BuildEndPointRequest m_endPointRequest;

        public ConnectionPoolSocketBasicCustomization( BuildEndPointRequest endPointRequest )
        {
            m_endPointRequest = endPointRequest;
        }

        public virtual void Customize( IFixture fixture )
        {
            fixture.Customize<ConnectionPoolSocket>( c => c.OmitAutoProperties() );
            fixture.Customizations.Add( new TypeRelay( from: typeof( EndPoint ), to: typeof( IPEndPoint ) ) );

            EndPointsBuilder endPointBuilder = new EndPointsBuilder( m_endPointRequest );
            IPEndPoint endPoint = endPointBuilder.Create<IPEndPoint>();
            if ( endPoint != null )
            {
                fixture.Customizations.Add( new ConnectionPoolSocketBuilder( SetUpTests.LoggingService, endPoint ) );
            }
            else
            {
                throw new InvalidOperationException( $"{nameof( DiscoveryService )} hasn't reachable {nameof( IPAddress )}" );
            }
        }
    }
}

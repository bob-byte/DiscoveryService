using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using AutoFixture.Kernel;
using AutoFixture;
using LUC.DiscoveryService.Common;
using System.ComponentModel;
using System.Reflection;
using System.Net.NetworkInformation;

namespace LUC.DiscoveryService.Test.Builders
{
    class EndPointsBuilder : AbstractSeededBuilder<BuildEndPointRequest>
    {
        public EndPointsBuilder(BuildEndPointRequest request)
            : base(request)
        {
            ;//do nothing
        }

        public override Object Create(Object request, ISpecimenContext specimenContext)
        {
            Object createdObject = null;
            Boolean isRightRequest = IsRightRequest( request );

            if ( isRightRequest)
            {
                switch ( Request )
                {
                    case ( BuildEndPointRequest.RandomEndPoint ):
                    {
                        Fixture specimens = new Fixture();
                        createdObject = specimens.Create<IPEndPoint>();

                        break;
                    }

                    case ( BuildEndPointRequest.ReachableDsEndPoint ):
                    {
                        createdObject = AllReachableDsEndPoints().First();

                        break;
                    }

                    case ( BuildEndPointRequest.AllReachableDsEndPoints ):
                    {
                        createdObject = AllReachableDsEndPoints().ToList();

                        break;
                    }
                }
            }

            if(createdObject == null)
            {
                createdObject = new NoSpecimen();
            }

            return createdObject;
        }

        private IEnumerable<IPEndPoint> AllReachableDsEndPoints()
        {
            IEnumerable<NetworkInterface> networkInterfaces = NetworkEventInvoker.NetworkInterfaces();
            List<IPAddress> runningIpAddresses = Listeners.IpAddressesOfInterfaces(
                networkInterfaces,
                SetUpTests.UseIpv4,
                SetUpTests.UseIpv6
            );

            foreach ( IPAddress ipAddress in runningIpAddresses )
            {
                Boolean isReacheable;
                try
                {
                    isReacheable = IpAddressFilter.IsIpAddressInTheSameNetwork( ipAddress, networkInterfaces.ToList() );
                }
                catch ( Win32Exception )
                {
                    continue;
                }

                if ( isReacheable )
                {
                    IPEndPoint reacheableEndPoint = new IPEndPoint( ipAddress, DiscoveryService.DEFAULT_PORT );
                    yield return reacheableEndPoint;
                }
            }
        }

        protected override Boolean IsRightRequest( Object request )
        {
            Type requestType = base.RequestType( request );

            Boolean isRightRequest = requestType == typeof( IPEndPoint ) || requestType == typeof( List<IPEndPoint> );
            return isRightRequest;
        }
    }
}

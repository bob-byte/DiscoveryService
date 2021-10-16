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
    class EndPointBuilder : ISpecimenBuilder
    {
        public EndPointBuilder(BuildEndPointRequest request)
        {
            Request = request;
        }

        public BuildEndPointRequest Request { get; set; }

        public Object Create(Object request, ISpecimenContext specimenContext)
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
                        //get working IP-address
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
                                createdObject = new IPEndPoint( ipAddress, DiscoveryService.DEFAULT_PORT );
                            }
                        }

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

        private Boolean IsRightRequest( Object request )
        {
            Boolean isRightRequest = false;
            if ( ( request is SeededRequest seededRequest ) && ( seededRequest.Request is Type type ) && ( type == typeof( IPEndPoint ) ) )
            {
                isRightRequest = true;
            }

            return isRightRequest;
        }
    }
}

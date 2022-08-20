using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using AutoFixture.Kernel;
using AutoFixture;
using LUC.DiscoveryServices.Common;
using System.ComponentModel;
using System.Net.NetworkInformation;

using System.Reflection;
using LUC.Interfaces.Constants;
using LUC.DiscoveryServices.Common.Extensions;

namespace LUC.DiscoveryServices.Test.Builders
{
    class EndPointsBuilder : AbstractSeededBuilder<BuildEndPointRequest>
    {
        public EndPointsBuilder( BuildEndPointRequest request )
            : base( request )
        {
            ;//do nothing
        }

        public override Object Create( Object request, ISpecimenContext specimenContext )
        {
            Object createdObject = null;
            Boolean isRightRequest = IsRightRequest( request );

            if ( isRightRequest )
            {
                switch ( Request )
                {
                    case BuildEndPointRequest.RandomEndPoint:
                    {
                        var specimens = new Fixture();
                        createdObject = specimens.Create<IPEndPoint>();

                        break;
                    }

                    case BuildEndPointRequest.ReachableDsEndPoint:
                    {
                        createdObject = AllReachableDsEndPoints().First();

                        break;
                    }

                    case BuildEndPointRequest.AllReachableDsEndPoints:
                    {
                        createdObject = AllReachableDsEndPoints().ToList();

                        break;
                    }
                }
            }

            if ( createdObject == null )
            {
                createdObject = new NoSpecimen();
            }

            return createdObject;
        }

        private IEnumerable<IPEndPoint> AllReachableDsEndPoints()
        {
            IEnumerable<NetworkInterface> networkInterfaces = NetworkEventInvoker.AllTransmitableNetworkInterfaces();
            List<IPAddress> runningIpAddresses = NetworkEventInvoker.IpAddressesOfInterfaces(
                networkInterfaces,
                DsSetUpTests.UseIpv4,
                DsSetUpTests.UseIpv6
            ).ToList();

            foreach ( IPAddress ipAddress in runningIpAddresses )
            {
                Boolean isReacheable;
                try
                {
                    isReacheable = IpAddressExtension.CanBeReachable( ipAddress, networkInterfaces.ToList() );
                }
                catch ( Win32Exception )
                {
                    continue;
                }

                if ( isReacheable )
                {
                    var reacheableEndPoint = new IPEndPoint( ipAddress, DsConstants.DEFAULT_PORT );
                    yield return reacheableEndPoint;
                }
            }
        }

        protected override Boolean IsRightRequest( Object request )
        {
            Boolean isRightRequest;
            Type requestType;
            try
            {
                requestType = base.RequestType( request );
            }
            catch ( IllegalRequestException )
            {
                PropertyInfo reflectedTypeProp = request.GetType().GetProperty( name: "ParameterType" );
                if ( reflectedTypeProp != null )
                {
                    requestType = reflectedTypeProp.GetValue( request ) as Type;
                }
                else
                {
                    throw new IllegalRequestException();
                }
            }

            isRightRequest = requestType == typeof( IPEndPoint ) || requestType == typeof( List<IPEndPoint> );

            return isRightRequest;
        }
    }
}

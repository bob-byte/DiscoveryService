using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;

using AutoFixture;
using AutoFixture.Kernel;

using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.Extensions;
using LUC.DiscoveryServices.Test.InternalTests.Requests;
using LUC.Interfaces.Discoveries;
using LUC.Services.Implementation;

namespace LUC.DiscoveryServices.Test.InternalTests.Builders
{
    class ContactBuilder : AbstractSeededBuilder<BuildContactRequest>
    {
        private readonly DiscoveryService m_discoveryService;

        private KademliaId m_contactId;

        public ContactBuilder( DiscoveryService discoveryService, KademliaId contactId, BuildContactRequest request )
            : this( discoveryService, request )
        {
            m_contactId = contactId;
        }

        public ContactBuilder( DiscoveryService discoveryService, BuildContactRequest request )
            : base( request )
        {
            m_discoveryService = discoveryService;
        }

        public override Object Create( Object request, ISpecimenContext specimenContext )
        {
            Object createdObject = null;
            Boolean isRightRequest = IsRightRequest( request );

            if ( isRightRequest )
            {
                switch ( Request )
                {
                    case BuildContactRequest.RandomOnlineContact:
                    {
                        List<IContact> contacts = m_discoveryService.OnlineContacts();

                        var random = new Random();
                        Int32 rndContactIndex = random.Next( contacts.Count );
                        createdObject = contacts[ rndContactIndex ];

                        break;
                    }

                    case BuildContactRequest.Dummy:
                    {
                        DefineBasicParameters( out String machineId, out IEnumerable<String> bucketsSupported );

                        createdObject = new Contact( machineId, KademliaId.Zero, m_discoveryService.RunningTcpPort, bucketsSupported );

                        break;
                    }

                    case BuildContactRequest.Default:
                    {
                        DefineBasicParameters( out String machineId, out IEnumerable<String> bucketsSupported );

                        try
                        {
                            if ( default( BigInteger ) == m_contactId )
                            {
                                m_contactId = KademliaId.RandomIDInKeySpace;
                            }
                        }
                        catch ( NullIDException )
                        {
                            m_contactId = KademliaId.RandomIDInKeySpace;
                        }

                        createdObject = new Contact( machineId, m_contactId, m_discoveryService.RunningTcpPort, bucketsSupported );

                        break;
                    }

                    case BuildContactRequest.OurContactWithIpAddresses:
                    {
                        m_discoveryService.Start();

                        var endPointBuilder = new EndPointsBuilder( BuildEndPointRequest.AllReachableDsEndPoints );
                        List<IPEndPoint> endPoints = endPointBuilder.Create<List<IPEndPoint>>();

                        m_discoveryService.NetworkEventInvoker.OurContact.TryAddIpAddressRange( endPoints.Select( c => c.Address ) );
                        createdObject = m_discoveryService.NetworkEventInvoker.OurContact;

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

        protected override Boolean IsRightRequest( Object fixtureRequest )
        {
            Type requestType = base.RequestType( fixtureRequest );

            Boolean isRightRequest = requestType == typeof( IContact );
            return isRightRequest;
        }

        private void DefineBasicParameters( out String machineId, out IEnumerable<String> bucketsSupported )
        {
            machineId = DsSetUpTests.Fixture.Create<String>();

            DsBucketsSupported.Define( DsSetUpTests.CurrentUserProvider, out ConcurrentDictionary<String, String> dictBucketsSupported );
            bucketsSupported = dictBucketsSupported.Keys;
        }
    }
}

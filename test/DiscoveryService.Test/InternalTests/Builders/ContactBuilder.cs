using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using AutoFixture;
using AutoFixture.Kernel;

using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Test.Builders;
using LUC.DiscoveryService.Test.InternalTests.Requests;

namespace LUC.DiscoveryService.Test.InternalTests.Builders
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
            : base(request)
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
                    case ( BuildContactRequest.RandomOnlineContact ):
                    {
                        List<Contact> contacts = m_discoveryService.OnlineContacts();

                        Random random = new Random();
                        Int32 rndContactIndex = random.Next( contacts.Count );
                        createdObject = contacts[ rndContactIndex ];

                        break;
                    }

                    case ( BuildContactRequest.Dummy ):
                    {
                        DefineBasicParameters( out String machineId, out IEnumerable<String> bucketsSupported );

                        createdObject = new Contact( machineId, KademliaId.Zero, m_discoveryService.RunningTcpPort, bucketsSupported );

                        break;
                    }

                    case ( BuildContactRequest.Default ):
                    {
                        DefineBasicParameters( out String machineId, out IEnumerable<String> bucketsSupported );

                        try
                        {
                            if ( default( BigInteger ) == m_contactId )
                            {
                                m_contactId = KademliaId.RandomIDInKeySpace;
                            }
                        }
                        catch(NullIDException)
                        {
                            m_contactId = KademliaId.RandomIDInKeySpace;
                        }

                        createdObject = new Contact( machineId, m_contactId, m_discoveryService.RunningTcpPort, bucketsSupported );

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

        protected override Boolean IsRightRequest( Object fixtureRequest )
        {
            Type requestType = base.RequestType( fixtureRequest );

            Boolean isRightRequest = requestType == typeof( Contact );
            return isRightRequest;
        }

        private void DefineBasicParameters( out String machineId, out IEnumerable<String> bucketsSupported )
        {
            MachineId.Create( out machineId );

            DsBucketsSupported.Define( SetUpTests.CurrentUserProvider, out ConcurrentDictionary<String, String> dictBucketsSupported );
            bucketsSupported = dictBucketsSupported.Keys;
        }
    }
}

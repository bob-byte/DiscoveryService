using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AutoFixture.Kernel;
using AutoFixture;

using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Requests;

namespace LUC.DiscoveryServices.Test.InternalTests.Builders
{
    class BucketListBuilder : AbstractSeededBuilder<BuildBucketListRequest>
    {
        private readonly DiscoveryService m_discoveryService;

        public BucketListBuilder(DiscoveryService discoveryService, BuildBucketListRequest request)
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
                    case BuildBucketListRequest.Dummy:
                    {
                        ContactBuilder contactSeeded = new ContactBuilder( m_discoveryService, BuildContactRequest.Dummy );
                        Contact dummyContact = contactSeeded.Create<Contact>();
                        createdObject = new BucketList( KademliaId.RandomIDInKeySpace, dummyContact, m_discoveryService.ProtocolVersion );

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

        protected override Boolean IsRightRequest( Object request )
        {
            Type requestType = base.RequestType( request );

            Boolean isRightRequest = requestType == typeof( BucketList );
            return isRightRequest;
        }
    }
}
using System;

using AutoFixture.Kernel;
using AutoFixture;

using DiscoveryServices.Kademlia;
using DiscoveryServices.Test.Builders;
using DiscoveryServices.Test.InternalTests.Requests;
using LUC.Interfaces.Discoveries;

namespace DiscoveryServices.Test.InternalTests.Builders
{
    class BucketListBuilder : AbstractSeededBuilder<BuildBucketListRequest>
    {
        private readonly DiscoveryService m_discoveryService;

        public BucketListBuilder( DiscoveryService discoveryService, BuildBucketListRequest request )
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
                    case BuildBucketListRequest.Dummy:
                    {
                        var contactSeeded = new ContactBuilder( m_discoveryService, BuildContactRequest.Dummy );
                        IContact dummyContact = contactSeeded.Create<IContact>();
                        createdObject = new BucketList( KademliaId.RandomIDInKeySpace, dummyContact, m_discoveryService.ProtocolVersion );

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

        protected override Boolean IsRightRequest( Object request )
        {
            Type requestType = base.RequestType( request );

            Boolean isRightRequest = requestType == typeof( BucketList );
            return isRightRequest;
        }
    }
}
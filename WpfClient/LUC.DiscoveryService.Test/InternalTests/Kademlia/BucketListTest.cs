using System;

using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.Interfaces;
using LUC.DiscoveryServices.Test.InternalTests.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Requests;

using NUnit.Framework;
using AutoFixture;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using FluentAssertions;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Test.InternalTests.Kademlia
{
    class BucketListTest
    {
        public const String CRLF = "\r\n";

        [Test]
        public void AddContact_AddKContacts_WithoutSplitBuckets()
        {
            DiscoveryService discoveryService = DsSetUpTests.DiscoveryService;
            discoveryService.Start();

            var bucketListBuilder = new BucketListBuilder( discoveryService, BuildBucketListRequest.Dummy );
            IBucketList bucketList = bucketListBuilder.Create<BucketList>();

            Parallel.For(fromInclusive: 0, toExclusive: DsConstants.K, _ =>
            {
                var contactBuilder = new ContactBuilder(discoveryService, KademliaId.Random(), BuildContactRequest.Default);

                IContact newContact = contactBuilder.Create<IContact>();
                bucketList.AddContact(newContact);
            });

            bucketList.Buckets.Count.Should().Be(expected: 1, because: "No split should have taken place" );
            bucketList.Buckets[0].Contacts.Count.Should().Be(DsConstants.K, "K contacts should have been added");
        }

        //[Test]
        //public void AddContact_AddSameContact_ContainsOneContactWithoutSplitBuckets()
        //{
        //    DiscoveryService discoveryService = DsSetUpTests.DiscoveryService;

        //    var bucketListBuilder = new BucketListBuilder(discoveryService, BuildBucketListRequest.Dummy);
        //    IBucketList bucketList = bucketListBuilder.Create<BucketList>();

        //    var id = KademliaId.RandomIDInKeySpace;
        //    IContact contact1 = ContactFactory.Default(SetupTests.DiscoveryService, id);
        //    bucketList.AddContact(ref contact1);
        //    var contactBuilder = new ContactBuilder(discoveryService, id, BuildContactRequest.Default);

        //    IContact newContact1 = contactBuilder.Create<IContact>();
        //    bucketList.AddContact(newContact1);

        //    IContact contact2 = ContactFactory.Default(SetupTests.DiscoveryService, id);
        //    bucketList.AddContact(ref contact2);

        //    Assert.IsTrue(condition: bucketList.Buckets.Count == 1, message: "No split should have taken place");
        //    Assert.IsTrue(bucketList.Buckets[0].Contacts.Count == 1, "Bucket should have 1 contact");
        //}

        //[Test]
        //public void AddContact_AddContactsToOverflowBucketList_SplitOneOrMoreBuckets()
        //{
        //    IBucketList bucketList = BucketListFactory.Dummy( SetupTests.DiscoveryService );
        //    for ( Int32 numContact = 0; numContact <= DsConstants.K; numContact++ )
        //    {
        //        IContact newContact = ContactFactory.Default( SetupTests.DiscoveryService );
        //        bucketList.AddContact( ref newContact );
        //    }

        //    Assert.IsTrue( condition: bucketList.Buckets.Count > 1, message: "Bucket should have split into two or more buckets" );
        //}

        //[Test]
        //public void A()
        //{
        //    SetupSplitFailure( out IBucketList bucketList );
        //    Assert.IsTrue( bucketList.Buckets.Count == 2, "Bucket split should have occured" );
        //}

        //protected void SetupSplitFailure( out IBucketList outBucketList, IBucketList inBucketList = null )
        //{
        //    //force host node ID < 2^159, so the node ID is not in the 2^159 ... 2^160 range
        //    Byte[] bHostId = new Byte[ 20 ];

        //    // 0x7F = 127
        //    bHostId[ 19 ] = 0x7F;
        //    ID hostId = new ID( bHostId );

        //    IContact dummyContact = ContactFactory.Dummy( SetupTests.DiscoveryService );
        //    if ( inBucketList != null )
        //    {
        //        outBucketList = (IBucketList)inBucketList.Clone();
        //    }
        //    else
        //    {
        //        outBucketList = new BucketList( hostId, dummyContact );
        //    }

        //    //Also add a contact in this 0 - 2^159 range, arbitrarily something not our host ID
        //    //This ensures that only one bucket split will occur after 20 nodes with ID >= 2^159 are added,
        //    //otherwise, buckets will in the 2^159 ... 2^160 space
        //    dummyContact = new IContact( ID.One, SetupTests.DiscoveryService.RunningTcpPort );
        //    IContact contactToAdd = new IContact( ID.One, dummyContact.TcpPort );
        //    outBucketList.AddContact( ref contactToAdd );

        //    Assert.IsTrue( condition: outBucketList.Buckets.Count == 1, message: "Bucket split should not have occurred" );
        //    Assert.IsTrue( condition: outBucketList.Buckets[ 0 ].Contacts.Count == 1, message: "Expected 1 contact in bucket 0" );

        //    //make sure contact ID's all have the same 5 bit prefix and are in the 2^159 ... 2^160 - 1 space
        //    Byte[] bytesofContactId = new Byte[ 20 ];

        //}
    }
}

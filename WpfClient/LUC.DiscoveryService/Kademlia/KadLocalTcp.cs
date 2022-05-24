using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.Interfaces;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;
using System.Linq;

namespace LUC.DiscoveryServices.Kademlia
{
    /// <summary>
    /// Kademlia TCP in local networks
    /// </summary>
    public class KadLocalTcp
    {
        private readonly UInt16 m_protocolVersion;

        public KadLocalTcp( UInt16 protocolVersion )
        {
            m_protocolVersion = protocolVersion;
        }

        /// <inheritdoc/>
        public RpcError Ping( IContact sender, IContact remoteContact )
        {
            var request = new PingRequest( sender.KadId.Value, sender.MachineId );

            request.GetResult<PingResponse>( remoteContact, m_protocolVersion, response: out _, out RpcError rpcError );
            return rpcError;
        }

        ///<inheritdoc/>
        public RpcError Store( IContact sender, KademliaId key, String val, IContact remoteContact, Boolean isCached = false, Int32 expirationTimeSec = 0 )
        {
            var request = new StoreRequest( sender.KadId.Value, sender.MachineId )
            {
                KeyToStore = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
            };

            request.GetResult<StoreResponse>( remoteContact, m_protocolVersion, response: out _, out RpcError rpcError );
            return rpcError;
        }

        /// <inheritdoc/>
        public (List<IContact> contacts, RpcError error) FindNode( IContact sender, KademliaId keyToFindContacts, IContact remoteContact )
        {
            var request = new FindNodeRequest( sender.KadId.Value, sender.MachineId )
            {
                KeyToFindCloseContacts = keyToFindContacts.Value,
                BucketIds = sender.Buckets(),
                TcpPort = sender.TcpPort
            };
            request.GetResult( remoteContact, m_protocolVersion, out FindNodeResponse response, out RpcError rpcError );
            List<IContact> closeContacts = response?.CloseSenderContacts?.ToList() ?? EmptyContactList();

            if ( !rpcError.HasError )
            {
                try
                {
                    UpdateContactInDht( remoteContact, response );
                }
                catch ( ArgumentException ex )
                {
                    rpcError.RemoteError = true;
                    rpcError.ErrorMessage = ex.Message;

                    DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                }
            }

            return (closeContacts, rpcError);
        }

        /// <inheritdoc/>
        public (List<IContact> contacts, String val, RpcError error) FindValue( IContact sender, KademliaId keyToFindContact, IContact remoteContact )
        {
            var request = new FindValueRequest( sender.KadId.Value, sender.MachineId )
            {
                KeyToFindCloseContacts = keyToFindContact.Value,
            };

            request.GetResult( remoteContact, m_protocolVersion, out FindValueResponse response, out RpcError rpcError );
            List<IContact> closeContacts = response?.CloseContacts?.ToList() ?? EmptyContactList();

            return (closeContacts, response?.ValueInResponsingPeer, rpcError);
        }

        private List<IContact> EmptyContactList() => 
            new List<IContact>();

        private void UpdateContactInDht( IContact remoteContact, FindNodeResponse findNodeResponse )
        {
            remoteContact.ExchangeLocalBucketRange( findNodeResponse.BucketIds );
            remoteContact.TcpPort = findNodeResponse.TcpPort;

            Dht dht = NetworkEventInvoker.DistributedHashTable( m_protocolVersion );
            IBucketList bucketList = dht.Node.BucketList;

            if ( bucketList.ContactExists( remoteContact ) )
            {
                KBucket kBucket = bucketList.GetKBucket( remoteContact.KadId );
                kBucket.ReplaceContact( remoteContact );
            }
            else
            {
                IContact oldInfoAboutContact = dht.PendingContacts.SingleOrDefault( c => c.Equals( remoteContact ) );
                if ( oldInfoAboutContact != null )
                {
                    oldInfoAboutContact.UpdateAccordingToNewState( remoteContact );
                }
            }
        }
    }
}

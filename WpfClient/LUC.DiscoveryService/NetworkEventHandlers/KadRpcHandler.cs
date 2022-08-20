using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.NetworkEventHandlers
{
    partial class KadRpcHandler : INetworkEventHandler
    {
        private readonly Dht m_distributedHashTable;
        private readonly Node m_node;
        private readonly IContact m_ourContact;
        private readonly KadLocalTcp m_kadRpcProtocol;

        public KadRpcHandler( Dht distributedHashTable, UInt16 protocolVersion )
        {
            m_distributedHashTable = distributedHashTable;
            m_node = distributedHashTable.Node;
            m_ourContact = m_node.OurContact;
            m_kadRpcProtocol = new KadLocalTcp( protocolVersion );
        }

        //Sometimes to send response, we need to call server kademlia operation and then send
        public virtual async void SendResponse( Object sender, TcpMessageEventArgs eventArgs )
        {
            //read request
            Message message = eventArgs.Message<Message>();

            //switch between MessageOperation where we are creating response, sending it and executing kademlia server operation
            switch ( message.MessageOperation )
            {
                case MessageOperation.Ping:
                {
                    PingRequest request = eventArgs.Message<PingRequest>( whetherReadMessage: false );
                    var response = new PingResponse( request.RandomID );

                    await HandleKademliaRequestAsync<PingRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRpc.FirstSendResponse,
                        kadServerOp: ( sendingContact ) =>
                        {
                            if ( sendingContact != null )
                            {
                                m_node.Ping( sendingContact );
                            }
                        }
                    ).ConfigureAwait( continueOnCapturedContext: false );

                    break;
                }

                case MessageOperation.Store:
                {
                    StoreRequest request = eventArgs.Message<StoreRequest>();
                    var response = new StoreResponse( request.RandomID );

                    await HandleKademliaRequestAsync<StoreRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRpc.FirstSendResponse,
                        ( sendingContact ) =>
                        {
                            if ( sendingContact != null )
                            {
                                m_node.Store( sendingContact, new KademliaId( request.KeyToStore ), request.Value, request.IsCached, request.ExpirationTimeSec );
                            }
                        }
                    ).ConfigureAwait( false );

                    break;
                }

                case MessageOperation.FindNode:
                {
                    FindNodeRequest request = eventArgs.Message<FindNodeRequest>();
                    var response = new FindNodeResponse( request.RandomID, m_ourContact.TcpPort, m_ourContact.Buckets() );

                    await HandleKademliaRequestAsync<FindNodeRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRpc.FirstExecuteProcedureCall,
                        ( sendingContact ) =>
                        {
                            GetCloseContacts( sendingContact, request, out List<IContact> closeContacts );

                            response.CloseSenderContacts = closeContacts;
                        }
                    ).ConfigureAwait( false );

                    Boolean isValidTcpPort = AbstractDsData.IsInPortRange( request.TcpPort );
                    if ( ( eventArgs.RemoteEndPoint is IPEndPoint ipEndPoint ) && isValidTcpPort )
                    {
                        IContact contact = new Contact( request.SenderMachineId, contactID: new KademliaId( request.SenderKadId ), request.TcpPort, ipEndPoint.Address, request.BucketIds );

                        if ( !m_distributedHashTable.OnlineContacts.Any( c => c.Equals( contact ) ) )
                        {
                            String baseOfLogRecord = $"execute {nameof( m_kadRpcProtocol.Ping )} and {nameof( m_node.BucketList.AddContact )} " +
                                $"on {nameof( contact )} {contact.MachineId} in method {nameof( SendResponse )}, " +
                                $"which doesn't exist before in a {nameof( Dht )}";

                            DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Started {baseOfLogRecord}" );

                            //we don't execute m_distributedHashTable.Bootstrap, because it
                            //will cause refresh bucket, so too many find node operations
                            RpcError rpcError = m_kadRpcProtocol.Ping( m_ourContact, contact );
                            if ( ( rpcError == null ) || !rpcError.HasError )
                            {
                                m_node.BucketList.AddContact( contact );
                            }

                            DsLoggerSet.DefaultLogger.LogInfo( $"Finished {baseOfLogRecord}" );
                        }
                    }
                    else
                    {
                        DsLoggerSet.DefaultLogger.LogCriticalError( new MalfactorAttackException( message: $"{nameof( eventArgs.RemoteEndPoint )} is {eventArgs.RemoteEndPoint.GetType().FullName} and has TCP port {request.TcpPort}" ) );
                    }

                    break;
                }

                case MessageOperation.FindValue:
                {
                    FindValueRequest request = eventArgs.Message<FindValueRequest>();
                    var response = new FindValueResponse( request.RandomID );

                    await HandleKademliaRequestAsync<FindValueRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRpc.FirstExecuteProcedureCall,
                        ( sendingContact ) =>
                        {
                            FindValueOrGetCloseContacts( sendingContact, request, out List<IContact> closeContacts, out String remoteNodeValue );

                            response.CloseContacts = closeContacts;
                            response.ValueInResponsingPeer = remoteNodeValue;
                        }
                    ).ConfigureAwait( false );

                    break;
                }
            }
        }

        private async ValueTask HandleKademliaRequestAsync<TRequest>( 
            Response response, 
            TcpMessageEventArgs eventArgs, 
            PriorityHandleKadRpc priority, 
            Action<IContact> kadServerOp )

            where TRequest : Request
        {
            String requestTypeShortName = typeof( TRequest ).Name;

#if DEBUG
            DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Started to handle {requestTypeShortName}" );
#endif

            TRequest request = eventArgs.Message<TRequest>();
            IContact sender = m_distributedHashTable.OnlineContacts.SingleOrDefault( c => c.MachineId.Equals( request.SenderMachineId, StringComparison.Ordinal ) );

            try
            {
                if ( priority == PriorityHandleKadRpc.FirstSendResponse )
                {
                    await response.SendAsync( eventArgs.AcceptedSocket ).ConfigureAwait( continueOnCapturedContext: false );

                    kadServerOp( sender );
                }
                else if ( priority == PriorityHandleKadRpc.FirstExecuteProcedureCall )
                {
                    kadServerOp( sender );

                    await response.SendAsync( eventArgs.AcceptedSocket ).ConfigureAwait( false );
                }
                else
                {
                    throw new ArgumentException( Display.VariableWithValue( nameof( priority ), priority ) );
                }
            }
            catch ( SocketException ex )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Failed to answer at a {requestTypeShortName}: {ex}" );
            }
            catch ( TimeoutException ex )
            {
                DsLoggerSet.DefaultLogger.LogInfo( $"Failed to answer at a {requestTypeShortName}: {ex}" );
            }

#if DEBUG
            DsLoggerSet.DefaultLogger.LogInfo( $"Finished to handle {requestTypeShortName}" );
#endif
        }

        private void GetCloseContacts( IContact sendingContact, FindNodeRequest request, out List<IContact> closeContacts )
        {
            if ( sendingContact != null )
            {
                m_node.FindNode( sendingContact, new KademliaId( request.KeyToFindCloseContacts ), out closeContacts );
            }
            else
            {
                closeContacts = m_node.BucketList.GetCloseContacts( new KademliaId( request.KeyToFindCloseContacts ), request.SenderMachineId );
            }
        }

        private void FindValueOrGetCloseContacts( IContact sendingContact, FindValueRequest request, out List<IContact> closeContacts, out String nodeValue )
        {
            nodeValue = null;

            if ( sendingContact != null )
            {
                m_node.FindValue( sendingContact, new KademliaId( request.KeyToFindCloseContacts ),
                    out closeContacts, out nodeValue );
            }
            else
            {
                closeContacts = m_node.BucketList.GetCloseContacts( new KademliaId( request.KeyToFindCloseContacts ), request.SenderMachineId );
            }
        }
    }
}

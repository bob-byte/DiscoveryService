using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.NetworkEventHandlers
{
    partial class KadOperationRequestHandler : INetworkEventHandler
    {
        private readonly Dht m_distributedHashTable;
        private readonly Node m_node;

        static KadOperationRequestHandler()
        {
            LoggingService = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        public KadOperationRequestHandler( Dht distributedHashTable )
        {
            m_distributedHashTable = distributedHashTable;
            m_node = distributedHashTable.Node;
        }

        [Import( typeof( ILoggingService ) )]
        public static ILoggingService LoggingService { get; set; }

        //Sometimes to send response, we need to call server kademlia operation and then send
        public void SendResponse( Object sender, TcpMessageEventArgs eventArgs )
        {
            //read request
            Message message = eventArgs.Message<Message>();

            //switch between MessageOperation where we are creating response, sending it and executing kademlia server operation
            switch ( message.MessageOperation )
            {
                case MessageOperation.Ping:
                {
                    PingRequest request = eventArgs.Message<PingRequest>( whetherReadMessage: false );
                    PingResponse response = new PingResponse( request.RandomID );

                    HandleKademliaRequest<PingRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRequest.FirstSendResponse,
                        kadServerOp: ( sendingContact ) =>
                        {
                            if ( sendingContact != null )
                            {
                                m_node.Ping( sendingContact );
                            }
                        } );

                    break;
                }

                case MessageOperation.Store:
                {
                    StoreRequest request = eventArgs.Message<StoreRequest>();
                    StoreResponse response = new StoreResponse( request.RandomID );

                    HandleKademliaRequest<StoreRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRequest.FirstSendResponse,
                        ( sendingContact ) =>
                        {
                            if ( sendingContact != null )
                            {
                                m_node.Store( sendingContact, new KademliaId( request.KeyToStore ), request.Value, request.IsCached, request.ExpirationTimeSec );
                            }
                        }
                    );

                    break;
                }

                case MessageOperation.FindNode:
                {
                    FindNodeRequest request = eventArgs.Message<FindNodeRequest>();
                    FindNodeResponse response = new FindNodeResponse( request.RandomID );

                    HandleKademliaRequest<FindNodeRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRequest.FirstExecuteKadServerOp,
                        ( sendingContact ) =>
                        {
                            GetCloseContacts( sendingContact, request, out List<Contact> closeContacts );

                            response.CloseSenderContacts = closeContacts;
                        }
                    );

                    break;
                }

                case MessageOperation.FindValue:
                {
                    FindValueRequest request = eventArgs.Message<FindValueRequest>();
                    FindValueResponse response = new FindValueResponse( request.RandomID );

                    HandleKademliaRequest<FindValueRequest>(
                        response,
                        eventArgs,
                        PriorityHandleKadRequest.FirstExecuteKadServerOp,
                        ( sendingContact ) =>
                        {
                            FindValueOrGetCloseContacts( sendingContact, request, out List<Contact> closeContacts, out String remoteNodeValue );

                            response.CloseContacts = closeContacts;
                            response.ValueInResponsingPeer = remoteNodeValue;
                        }
                    );

                    break;
                }
            }
        }

        private void HandleKademliaRequest<T>( Response response, TcpMessageEventArgs eventArgs, PriorityHandleKadRequest priority, Action<Contact> kadServerOp )
            where T : Request, new()
        {
            Contact sender = null;
            T request = null;
            try
            {
                request = eventArgs.Message<T>();
                sender = m_distributedHashTable.OnlineContacts.Single( c => c.KadId == new KademliaId( request.Sender ) );
            }
            catch ( InvalidOperationException ex )
            {
                LoggingService.LogInfo( $"Cannot find sender of {typeof( T ).Name}: {ex.Message}" );

#if !RECEIVE_TCP_FROM_OURSELF
                return;
#endif
            }

            try
            {
                if ( priority == PriorityHandleKadRequest.FirstSendResponse )
                {
                    response.Send( eventArgs.AcceptedSocket );

                    kadServerOp( sender );
                }
                else if ( priority == PriorityHandleKadRequest.FirstExecuteKadServerOp )
                {
                    kadServerOp( sender );

                    response.Send( eventArgs.AcceptedSocket );
                }
                else
                {
                    throw new ArgumentException( Display.PropertyWithValue( nameof( priority ), priority ) );
                }
            }
            catch ( SocketException ex )
            {
                LoggingService.LogInfo( $"Failed to answer at a {typeof( T ).Name}: {ex}" );
            }
            catch ( TimeoutException ex )
            {
                LoggingService.LogInfo( $"Failed to answer at a {typeof( T ).Name}: {ex}" );
            }
        }

        private void GetCloseContacts( Contact sendingContact, FindNodeRequest request, out List<Contact> closeContacts )
        {
            if ( sendingContact != null )
            {
                m_node.FindNode( sendingContact, new KademliaId( request.KeyToFindCloseContacts ), out closeContacts );
            }
            else
            {
                closeContacts = m_node.BucketList.GetCloseContacts( new KademliaId( request.KeyToFindCloseContacts ), new KademliaId( request.Sender ) );
            }
        }

        private void FindValueOrGetCloseContacts( Contact sendingContact, FindValueRequest request, out List<Contact> closeContacts, out String nodeValue )
        {
            nodeValue = null;

            if ( sendingContact != null )
            {
                m_node.FindValue( sendingContact, new KademliaId( request.KeyToFindCloseContacts ),
                    out closeContacts, out nodeValue );
            }
            else
            {
                closeContacts = m_node.BucketList.GetCloseContacts( new KademliaId( request.KeyToFindCloseContacts ), new KademliaId( request.Sender ) );
            }
        }
    }
}

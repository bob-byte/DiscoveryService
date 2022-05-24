using DiscoveryServices.CodingData;
using DiscoveryServices.Common;
using DiscoveryServices.Interfaces;
using DiscoveryServices.Kademlia;
using DiscoveryServices.Kademlia.ClientPool;
using DiscoveryServices.Kademlia.Exceptions;
using DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Services.Implementation;

using Nito.AsyncEx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DiscoveryServices.Messages.KademliaRequests
{
    internal abstract class Request : Message
    {
        private static readonly RemoteProcedureCaller s_remoteProcedureCaller = new RemoteProcedureCaller();

        protected Request( BigInteger senderKadId, String senderMachineId )
            : this()
        {
            SenderKadId = senderKadId;
            SenderMachineId = senderMachineId;
        }

        protected Request( Byte[] receivedBytes )
            : base(receivedBytes)
        {
            ;//do nothing
        }

        private Request()
        {
            //We don't use DefaultInit, because it can be overridden in child classes
            RandomID = KademliaId.Random().Value;
            IsReceivedLastRightResp = false;
        }

        public BigInteger RandomID { get; private set; }

        public BigInteger SenderKadId { get; set; }

        public String SenderMachineId { get; private set; }


        /// <summary>
        /// Returns whether received right response to <a href="last"/> request
        /// </summary>
        //It is internal to not show it in log(see method Display.ObjectToString)
        private Boolean IsReceivedLastRightResp { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                SenderKadId = reader.ReadBigInteger();
                SenderMachineId = reader.ReadAsciiString();
                RandomID = reader.ReadBigInteger();

                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
            }
        }

        /// <inheritdoc/>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( SenderKadId );
                writer.WriteAsciiString( SenderMachineId );
                writer.Write( RandomID );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        public void GetResult<TResponse>( IContact remoteContact, UInt16 protocolVersion, out TResponse response, out RpcError rpcError )
            where TResponse : Response
        {
            //ResultAsync completes synchronously, so we can use GetAwaiter().GetResult()
            (response, rpcError) = ResultAsync<TResponse>( remoteContact, IoBehavior.Synchronous, protocolVersion ).GetAwaiter().GetResult();
        }

        public async ValueTask<(TResponse, RpcError)> ResultAsync<TResponse>( IContact remoteContact, UInt16 protocolVersion )
            where TResponse : Response => await ResultAsync<TResponse>( remoteContact, IoBehavior.Asynchronous, protocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

        /// <summary>
        /// Execute RPC and construct <seealso cref="Kademlia.RpcError"/> according to received response or it absence
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="remoteContact"></param>
        /// <param name="ioBehavior"></param>
        /// <param name="protocolVersion"></param>
        /// <returns></returns>
        public async ValueTask<(TResponse, RpcError)> ResultAsync<TResponse>( IContact remoteContact, IoBehavior ioBehavior, UInt16 protocolVersion )
            where TResponse : Response
        {
            TResponse response = null;
            RpcError rpcError = null;

            List<IPAddress> clonedListOfIpAddresses = remoteContact.IpAddresses();

            //start from the last active IP-address and go to the oldest
            for (  Int32 numAddress = clonedListOfIpAddresses.Count - 1;
                 ( numAddress >= 0 ) && ( response == null ); 
                   numAddress-- )
            {
                var ipEndPoint = new IPEndPoint( clonedListOfIpAddresses[ numAddress ], remoteContact.TcpPort );
                (response, rpcError) = await s_remoteProcedureCaller.PostAsync<TResponse>( this, ipEndPoint, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );

                if ( response != null )
                {
                    remoteContact.LastActiveIpAddress = clonedListOfIpAddresses[ numAddress ];

                    //remoteContact sent response, so it is online
                    ResetEvictionCount( remoteContact, protocolVersion );
                }
            }

            IsReceivedLastRightResp = rpcError != null && !rpcError.HasError;

            if ( !IsReceivedLastRightResp )
            {
                TryToEvictContact( remoteContact, protocolVersion );
            }

            return (response, rpcError);
        }

        private void ResetEvictionCount( IContact remoteContact, UInt16 protocolVersion )
        {
            Dht dht = NetworkEventInvoker.DistributedHashTable( protocolVersion );

            BigInteger contactId = remoteContact.KadId.Value;
            if ( dht.EvictionCount.ContainsKey( contactId ) )
            {
                dht.EvictionCount[ contactId ] = 0;
            }
        }

        private void TryToEvictContact( IContact remoteContact, UInt16 protocolVersion )
        {
            Dht dht = NetworkEventInvoker.DistributedHashTable( protocolVersion );

            try
            {
                //"toReplace: null", because we don't get new contact in Kademlia request
                dht.DelayEviction( remoteContact, toReplace: null );
            }
            //contact can already be evicted by another thread
            catch ( BucketDoesNotContainContactToEvictException )
            {
                ;//do nothing
            }
        }
    }
}

using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using Nito.AsyncEx;
using LUC.DiscoveryService.Common;

namespace LUC.DiscoveryService.Kademlia.Downloads
{
    public partial class Download
    {
        private IEnumerable<Contact> ContactsWithFile( 
            IEnumerable<Contact> onlineContacts, 
            DownloadFileRequest sampleRequest, 
            CancellationToken cancellationToken )
        {
            BlockingCollection<Contact> contactsWithFile = new BlockingCollection<Contact>( onlineContacts.Count() );

            //we need parallel execution. Task.Run is async
            Task.Factory.StartNew( async () =>
             {
                 ExecutionDataflowBlockOptions parallelOptions = ParallelOptions(cancellationToken);

                 try
                 {
                     var checkFileExistsInContact = new ActionBlock<Contact>( async ( contact ) =>
                     {
                         var isExistInContact = await IsFileExistsInContactAsync( sampleRequest, contact ).ConfigureAwait( continueOnCapturedContext: false );

                         if ( cancellationToken.IsCancellationRequested )
                         {
                             return;
                         }

                         if ( isExistInContact )
                         {
                             contactsWithFile.Add( contact );
                         }
                     }, parallelOptions );

                     foreach ( var contact in onlineContacts )
                     {
                         checkFileExistsInContact.Post( contact );
                     }

                     //Signals that we will not post more Contact.
                     //checkFileExistsInContact.Completion will never be completed without this
                     checkFileExistsInContact.Complete();

                     //await getting all contactsWithFile
                     await checkFileExistsInContact.Completion.ConfigureAwait( false );
                 }
                 catch(OperationCanceledException)
                 {
                     ;//do nothing
                 }
                 finally
                 {
                     //contactsWithFile.GetConsumingEnumerable will never be completed without this
                     contactsWithFile.CompleteAdding();
                 }
             } );

            return contactsWithFile.GetConsumingEnumerable( cancellationToken );
        }

        private async Task<Boolean> IsFileExistsInContactAsync( DownloadFileRequest sampleRequest, Contact contact )
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest( m_ourContact.KadId.Value )
            {
                BucketId = sampleRequest.BucketId,
                FileOriginalName = sampleRequest.FileOriginalName,
                HexPrefix = sampleRequest.HexPrefix,
            };
            (CheckFileExistsResponse response, RpcError rpcError) = await request.ResultAsync<CheckFileExistsResponse>( contact,
                IOBehavior, m_discoveryService.ProtocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

            Boolean existRequiredFile;
            if ( !rpcError.HasError )
            {
                Boolean isTheSameRequiredFile = ( response.FileSize == sampleRequest.ChunkRange.Total ) && ( response.FileVersion == sampleRequest.FileVersion );

                existRequiredFile = isTheSameRequiredFile;
            }
            else
            {
                existRequiredFile = false;
            }

            return existRequiredFile;
        }
    }
}

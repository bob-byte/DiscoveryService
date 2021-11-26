using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using Nito.AsyncEx;
using LUC.DiscoveryServices.Common;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public partial class Download
    {
        private IEnumerable<Contact> ContactsWithFile(
            IEnumerable<Contact> onlineContacts,
            DownloadFileRequest sampleRequest,
            CancellationToken cancellationToken,
            Int64 undownloadedfileBytesCount
        ){
            Int32 contactCountWithFileCapacity = ContactCountWithFileCapacity( sampleRequest, undownloadedfileBytesCount );

            IEnumerable<Contact> contactsWithFile = ContactsWithFile( onlineContacts, sampleRequest, cancellationToken, contactCountWithFileCapacity );
            return contactsWithFile;
        }

        private Int32 ContactCountWithFileCapacity(DownloadFileRequest downloadFileRequest, Int64 undownloadedfileBytesCount )
        {
            //get total bytes / default capacity. if it is > max chunk size then contactCountWithFileCapacity = default capacity, else undownloaded bytes / maxChunkSize
            Int64 bytesPerContact = (Int64)Math.Ceiling( downloadFileRequest.ChunkRange.Total / (Double)CONTACT_COUNT_WITH_FILE_CAPACITY );

            Int32 maxChunkSize = Constants.MAX_CHUNK_SIZE;
            Int32 contactCountWithFileCapacity = bytesPerContact > maxChunkSize ? 
                CONTACT_COUNT_WITH_FILE_CAPACITY : 
                (Int32)Math.Ceiling( (Double)undownloadedfileBytesCount / maxChunkSize );

            return contactCountWithFileCapacity;
        }

        private IEnumerable<Contact> ContactsWithFile(
            IEnumerable<Contact> onlineContacts,
            DownloadFileRequest sampleRequest,
            CancellationToken cancellationToken,
            Int32 contactCountWithFileCapacity 
        ){
            //try change to using AsyncCollection
            BlockingCollection<Contact> contactsWithFile = new BlockingCollection<Contact>( contactCountWithFileCapacity );

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

                         if( contactsWithFile.Count < contactCountWithFileCapacity )
                         {
                             contactsWithFile.CompleteAdding();
                         }
                         else if ( isExistInContact )
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
                 //added too many contactsWithFile (called contactsWithFile.Add( contact ) after contactsWithFile.CompleteAdding() (thread race))
                 catch ( InvalidOperationException)
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

        private async ValueTask<Boolean> IsFileExistsInContactAsync( DownloadFileRequest sampleRequest, Contact contact )
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest( m_ourContact.KadId.Value, m_ourContact.MachineId )
            {
                LocalBucketId = sampleRequest.LocalBucketId,
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

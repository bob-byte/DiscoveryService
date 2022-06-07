using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Constants;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public partial class DownloaderFromLocalNetwork
    {
        private Int32 ContactCountWithFileCapacity( UInt64 bytesFileCount, Int64 undownloadedFileBytesCount )
        {
            //get total bytes / default capacity. if it is > max chunk size then contactCountWithFileCapacity = default capacity, else undownloaded bytes / maxChunkSize
            Int64 bytesPerContact = (Int64)Math.Ceiling( bytesFileCount / (Double)CONTACT_COUNT_WITH_FILE_CAPACITY );

            //for 1 contact will be more optimized if it has at least 2 chunks for download
            Int32 twoChunksSize = DsConstants.MAX_CHUNK_SIZE * 2;
            Int32 contactCountWithFileCapacity = bytesPerContact > twoChunksSize ?
                CONTACT_COUNT_WITH_FILE_CAPACITY :
                (Int32)Math.Ceiling( (Double)undownloadedFileBytesCount / twoChunksSize );

            return contactCountWithFileCapacity;
        }

        /// <remarks>
        /// We need to define which contacts have file in order to distribute evenly chunks per each contact
        /// </remarks>
        private IEnumerable<IContact> ContactsWithFile(
            IEnumerable<IContact> onlineContacts,
            DownloadChunkRequest sampleRequest,
            UInt64 bytesFileCount,
            Int64 undownloadedfileBytesCount,
            CancellationToken cancellationToken
        )
        {
            Int32 contactCountWithFileCapacity = ContactCountWithFileCapacity( bytesFileCount, undownloadedfileBytesCount );

            IEnumerable<IContact> contactsWithFile = ContactsWithFile( onlineContacts, sampleRequest, contactCountWithFileCapacity, bytesFileCount, cancellationToken );
            return contactsWithFile;
        }
        private IEnumerable<IContact> ContactsWithFile(
            IEnumerable<IContact> onlineContacts,
            DownloadChunkRequest sampleRequest,
            Int32 contactCountWithFileCapacity,
            UInt64 bytesFileCount,
            CancellationToken cancellationToken
        )
        {
            var contactsWithFile = new BlockingCollection<IContact>( contactCountWithFileCapacity );

            Task.Run( async () =>
             {
                 ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );

                 Int32 countContactsWithFile = 0;

                 try
                 {
                     var checkFileExistsInContact = new ActionBlock<IContact>( async ( contact ) =>
                     {
                         Boolean isExistInContact = await IsFileExistsInContactAsync( sampleRequest, contact, bytesFileCount ).ConfigureAwait( continueOnCapturedContext: false );

                         if ( cancellationToken.IsCancellationRequested )
                         {
                             return;
                         }

                         if ( countContactsWithFile >= contactCountWithFileCapacity )
                         {
                             contactsWithFile.CompleteAdding();
                         }
                         else if ( isExistInContact )
                         {
                             contactsWithFile.Add( contact );
                             countContactsWithFile++;
                         }
                     }, parallelOptions );

                     foreach ( IContact contact in onlineContacts )
                     {
                         checkFileExistsInContact.Post( contact );
                     }

                    //Signals that we will not post more IContact.
                    //checkFileExistsInContact.Completion will never be completed without this
                    checkFileExistsInContact.Complete();

                    //await getting all contactsWithFile
                    await checkFileExistsInContact.Completion.ConfigureAwait( false );
                 }
                 catch ( OperationCanceledException )
                 {
                     ;//do nothing
                }
                //added too many contactsWithFile (called contactsWithFile.Add( contact ) after contactsWithFile.CompleteAdding() (thread race))
                catch ( InvalidOperationException )
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

        private async Task<Boolean> IsFileExistsInContactAsync( DownloadChunkRequest sampleRequest, IContact contact, UInt64 bytesFileCount )
        {
            var request = new CheckFileExistsRequest( m_ourContact.KadId.Value, m_ourContact.MachineId )
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
                Boolean isTheSameRequiredFile = ( response.FileSize == bytesFileCount ) && response.FileVersion.Equals( sampleRequest.FileVersion, StringComparison.Ordinal );

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

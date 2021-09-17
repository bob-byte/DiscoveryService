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

namespace LUC.DiscoveryService
{
    public partial class Download
    {
        private async Task<List<Contact>> ContactsWithFileAsync( IList<Contact> onlineContacts, DownloadFileRequest sampleRequest, CancellationToken cancellationToken )
        {
            List<Contact> contactsWithFile = new List<Contact>();

            ActionBlock<Contact> checkFileExistsInContact = new ActionBlock<Contact>( async ( contact ) =>
             {
                 Boolean isExistInContact = await IsFileExistsInContactAsync( sampleRequest, contact ).ConfigureAwait( continueOnCapturedContext: false );

                 cancellationToken.ThrowIfCancellationRequested();

                 if ( isExistInContact )
                 {
                     contactsWithFile.Add( contact );
                 }
             } );

            for ( Int32 numContact = 0; numContact < onlineContacts.Count; numContact++ )
            {
                checkFileExistsInContact.Post( onlineContacts[ numContact ] );
            }

            //Signals that we will not post more Contact
            checkFileExistsInContact.Complete();

            //await getting all contactsWithFile
            await checkFileExistsInContact.Completion.ConfigureAwait( false );

            return contactsWithFile;
        }

        private async Task<Boolean> IsFileExistsInContactAsync( DownloadFileRequest sampleRequest, Contact contact )
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest( m_ourContact.KadId.Value )
            {
                BucketName = sampleRequest.BucketName,
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

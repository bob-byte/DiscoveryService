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
        private async Task<List<Contact>> ContactsWithFileAsync(IList<Contact> onlineContacts, DownloadFileRequest sampleRequest, CancellationToken cancellationToken)
        {
            var contactsWithFile = new List<Contact>();

            var checkFileExistsInContact = new ActionBlock<Contact>(async (contact) =>
            {
                var isExistInContact = await IsFileExistsInContactAsync(sampleRequest, contact).ConfigureAwait(continueOnCapturedContext: false);

                cancellationToken.ThrowIfCancellationRequested();

                if (isExistInContact)
                {
                    contactsWithFile.Add(contact);
                }
            });

            for (Int32 numContact = 0; numContact < onlineContacts.Count; numContact++)
            {
                checkFileExistsInContact.Post(onlineContacts[numContact]);
            }

            //Signals that we will not post more Contact
            checkFileExistsInContact.Complete();

            //await getting all contactsWithFile
            await checkFileExistsInContact.Completion.ConfigureAwait(false);

            return contactsWithFile;
        }

        private async Task<Boolean> IsFileExistsInContactAsync(DownloadFileRequest sampleRequest, Contact contact)
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest
            {
                BucketName = sampleRequest.BucketName,
                FileOriginalName = sampleRequest.FileOriginalName,
                FilePrefix = sampleRequest.FilePrefix,
                Sender = discoveryService.NetworkEventInvoker.OurContact.ID.Value
            };
            (CheckFileExistsResponse response, RpcError rpcError) = await request.ResultAsync<CheckFileExistsResponse>(contact,
                IOBehavior, discoveryService.ProtocolVersion).ConfigureAwait(continueOnCapturedContext: false);

            Boolean existRequiredFile;
            if (!rpcError.HasError)
            {
                Boolean isTheSameRequiredFile = (response.FileSize == sampleRequest.Range.Total) && (response.FileVersion == sampleRequest.FileVersion);

                if ((response.FileExists) && (isTheSameRequiredFile))
                {
                    existRequiredFile = true;
                }
                else
                {
                    existRequiredFile = false;
                }
            }
            else
            {
                existRequiredFile = false;
            }

            return existRequiredFile;
        }
    }
}

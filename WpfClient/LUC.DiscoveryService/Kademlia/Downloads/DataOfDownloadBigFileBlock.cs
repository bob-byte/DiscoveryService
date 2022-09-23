using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    internal class DataOfDownloadBigFileBlock
    {
        public DataOfDownloadBigFileBlock( IContact contact, DownloadChunkRequest request, List<ChunkRange> chunkRanges )
        {
            Contact = contact;
            Request = request;
            ChunkRanges = chunkRanges;
        }

        public IContact Contact { get; }

        public DownloadChunkRequest Request { get; set; }

        public List<ChunkRange> ChunkRanges { get; }
    }
}

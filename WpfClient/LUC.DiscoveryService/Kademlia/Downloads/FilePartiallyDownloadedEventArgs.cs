using System;
using System.Collections.Generic;
using System.Linq;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Messages;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public class FilePartiallyDownloadedEventArgs : EventArgs
    {
        public FilePartiallyDownloadedEventArgs( IEnumerable<ChunkRange> ranges )
        {
            UndownloadedRanges = ranges.ToList();
        }

        public List<ChunkRange> UndownloadedRanges { get; }
    }
}
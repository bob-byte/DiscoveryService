using System;
using System.Collections.Generic;
using System.Linq;

using DiscoveryServices.Messages;

namespace DiscoveryServices.Kademlia.Downloads
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
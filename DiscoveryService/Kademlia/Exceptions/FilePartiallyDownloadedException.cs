using LUC.DiscoveryServices.Messages;

using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    public class FilePartiallyDownloadedException : Exception
    {
        public FilePartiallyDownloadedException( IEnumerable<ChunkRange> ranges, String tempFullFileName )
        {
            DefaultInit( ranges, tempFullFileName );
        }

        public FilePartiallyDownloadedException( IEnumerable<ChunkRange> ranges, String tempFullFileName, String messageException )
            : base( messageException )
        {
            DefaultInit( ranges, tempFullFileName );
        }

        public IEnumerable<ChunkRange> UndownloadedRanges { get; private set; }

        public String TempFullFileName { get; private set; }

        private void DefaultInit( IEnumerable<ChunkRange> ranges, String tempFullFileName )
        {
            UndownloadedRanges = ranges;
            TempFullFileName = tempFullFileName;
        }
    }
}

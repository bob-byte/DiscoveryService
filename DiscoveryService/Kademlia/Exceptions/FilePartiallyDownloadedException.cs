using LUC.DiscoveryServices.Messages;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    class FilePartiallyDownloadedException : Exception
    {
        public FilePartiallyDownloadedException( String messageException )
            : base( messageException )
        {
            Ranges = new List<ChunkRange>();
        }

        public FilePartiallyDownloadedException( IEnumerable<ChunkRange> ranges )
        {
            Ranges = ranges.ToList();
        }

        public FilePartiallyDownloadedException( IEnumerable<ChunkRange> ranges, String messageException )
            : base( messageException )
        {
            Ranges = ranges.ToList();
        }

        /// <summary>
        /// Ranges which should be downloaded
        /// </summary>
        public List<ChunkRange> Ranges { get; set; }
    }
}

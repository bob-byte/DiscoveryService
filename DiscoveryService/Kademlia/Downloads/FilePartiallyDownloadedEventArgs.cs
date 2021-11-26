using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Messages;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public class FilePartiallyDownloadedEventArgs : EventArgs
    {
        public FilePartiallyDownloadedEventArgs( IEnumerable<ChunkRange> ranges )
        {
            Ranges = ranges.ToList();
        }

        /// <summary>
        /// To know whether range is downloaded see <seealso cref="ChunkRange.IsDownloaded"/>
        /// </summary>
        public List<ChunkRange> Ranges { get; }
    }
}
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
        public FilePartiallyDownloadedException()
            : base()
        {
            ;//do nothing
        }

        public FilePartiallyDownloadedException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }

        public List<ChunkRange> Ranges { get; set; }
    }
}

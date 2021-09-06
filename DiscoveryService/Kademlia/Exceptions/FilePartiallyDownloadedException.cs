using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    class FilePartiallyDownloadedException : Exception
    {
        public FilePartiallyDownloadedException()
        {
            ;//do nothing
        }

        public FilePartiallyDownloadedException(String messageException)
            : base(messageException)
        {
            ;
        }
    }
}

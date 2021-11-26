using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public class DownloadErrorHappenedEventArgs : EventArgs
    {
        public DownloadErrorHappenedEventArgs( Exception error, String originalFullFileName )
        {
            Error = error;
            OriginalFullFileName = originalFullFileName;
        }

        public Exception Error { get; }

        public String OriginalFullFileName { get; }
    }
}

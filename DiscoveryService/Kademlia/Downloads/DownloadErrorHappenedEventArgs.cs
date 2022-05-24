using System;

namespace DiscoveryServices.Kademlia.Downloads
{
    public class DownloadErrorHappenedEventArgs : EventArgs
    {
        public DownloadErrorHappenedEventArgs( Exception error, String fullFileName )
        {
            Error = error;
            FullFileName = fullFileName;
        }

        public Exception Error { get; }

        public String FullFileName { get; }
    }
}

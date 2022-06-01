using System;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public class FileDownloadedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of the <see cref="FileDownloadedEventArgs"/> class
        /// </summary>
        /// <param name="fullFileName">
        /// Full downloaded file name. Includes path to this file, it name and extension 
        /// </param>
        /// <param name="version">
        /// Version of the file created by ADS
        /// </param>
        public FileDownloadedEventArgs( String fullFileName, String version, String guid )
        {
            FullFileName = fullFileName;
            Version = version;
            Guid = guid;
        }

        /// <summary>
        /// Full downloaded file name. Includes path to this file, it name and extension 
        /// </summary>
        public String FullFileName { get; }

        /// <summary>
        /// Version of file created by ADS
        /// </summary>
        public String Version { get; }

        public String Guid { get; }
    }
}

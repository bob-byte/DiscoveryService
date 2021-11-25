using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public class FileDownloadedEventArgs
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
        public FileDownloadedEventArgs(String fullFileName, String version)
        {
            FullFileName = fullFileName;
            Version = version;
        }

        /// <summary>
        /// Full downloaded file name. Includes path to this file, it name and extension 
        /// </summary>
        public String FullFileName { get; }

        /// <summary>
        /// Version of file created by ADS
        /// </summary>
        public String Version { get; }
    }
}

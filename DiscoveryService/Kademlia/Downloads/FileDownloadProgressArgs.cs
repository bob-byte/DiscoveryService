using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Messages;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public class FileDownloadProgressArgs
    {
        /// <summary>
        /// Creates a new instance of the <see cref="FileDownloadProgressArgs"/> class.
        /// </summary>
        /// <param name="chunkRange">
        /// Range of downloaded chunk
        /// </param>
        /// <param name="fullFileName">
        /// If you are downloading big file (size > <seealso cref="Constants.MAX_CHUNK_SIZE"/>), 
        /// then it is temp full file name(path, file name and extension). 
        /// If you are downloading small file, 
        /// then it is simply full path to already downloaded file
        /// </param>
        public FileDownloadProgressArgs( ChunkRange chunkRange, String fullFileName )
        {
            ChunkRange = chunkRange;
            FullFileName = fullFileName;
        }

        /// <summary>
        /// Range of downloaded chunk
        /// </summary>
        public ChunkRange ChunkRange { get; }

        /// <summary>
        /// If you are downloading big file (size > <seealso cref="Constants.MAX_CHUNK_SIZE"/>), 
        /// then it is temp full file name(path, file name and extension). 
        /// If you are downloading small file, 
        /// then it is simply full path to already downloaded file
        /// </summary>
        public String FullFileName { get; }
    }
}

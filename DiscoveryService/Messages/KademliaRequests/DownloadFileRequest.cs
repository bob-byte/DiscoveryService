using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class DownloadFileRequest : Request, ICloneable
    {
        public DownloadFileRequest()
        {
            MessageOperation = MessageOperation.DownloadFile;
        }

        public String FileOriginalName { get; set; }

        public String Prefix { get; set; }

        public Range ContantRange { get; set; }

        public UInt64 CountDownloadedBytes { get; set; }

        /// <summary>
        /// Whether <see cref="Contact"/> has downloaded all the bytes for which it is responsible
        /// </summary>
        public Boolean WasDownloadedAllBytes => ContantRange.TotalPerContact - CountDownloadedBytes == 0;

        public Object Clone()
        {
            var clone = (DownloadFileRequest)MemberwiseClone();
            clone.ContantRange = (Range)ContantRange.Clone();

            return clone;
        }
    }
}

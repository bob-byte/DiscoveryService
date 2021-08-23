using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class DownloadFileResponse : Response
    {
        public Byte[] Buffer { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            base.Read(reader);

            var bytesCount = reader.ReadUInt32();
            Buffer = reader.ReadBytes((Int32)bytesCount);

            return this;
        }
    }
}

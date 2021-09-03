using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class DownloadFileResponse : FileResponse
    {
        public DownloadFileResponse()
        {
            MessageOperation = MessageOperation.DownloadFileResponse;
        }

        public Byte[] Chunk { get; set; }

        public override void Write(WireWriter writer)
        {
            if(writer != null)
            {
                base.Write(writer);

                writer.Write((UInt32)Chunk.Length);
                writer.WriteBytes(Chunk);
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            base.Read(reader);

            var bytesCount = reader.ReadUInt32();
            Chunk = reader.ReadBytes((Int32)bytesCount);

            return this;
        }
    }
}

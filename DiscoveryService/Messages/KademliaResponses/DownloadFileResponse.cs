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
    class DownloadFileResponse : Response
    {
        public DownloadFileResponse()
        {
            MessageOperation = MessageOperation.DownloadFileResponse;
        }

        public Byte[] Buffer { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            base.Read(reader);

            var bytesCount = reader.ReadUInt32();
            Buffer = reader.ReadBytes((Int32)bytesCount);

            return this;
        }

        public void Send(Socket socket, DownloadFileRequest request)
        {
            if((request?.RandomID != default) && 
               (request?.Prefix != null) && 
               (request?.FileOriginalName != null) && 
               (request?.ContantRange != null))
            {
                socket.SendTimeout = (Int32)Constants.SendTimeout.TotalMilliseconds;
                Byte[] buffer = this.ToByteArray();
                socket.Send(buffer);

                LogResponse(socket, this);
            }
            else
            {
                throw new ArgumentException($"Bad format of {request}");
            }
        }
    }
}

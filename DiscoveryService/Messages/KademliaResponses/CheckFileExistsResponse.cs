using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class CheckFileExistsResponse : Response
    {
        public CheckFileExistsResponse()
        {
            MessageOperation = MessageOperation.CheckFileExistsResponse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <value>
        /// Default value is <a href="false"/> to set it if <see cref="IsRightBucket"/> = <a href="false"/> and you can use this value in every case to define whether file exists in remote <see cref="Contact"/>
        /// </value>
        public Boolean FileExists { get; set; } = false;

        public Boolean IsRightBucket { get; set; }

        public UInt64 Version { get; set; }

        public UInt64 FileSize { get; set; }

        public override void Write(WireWriter writer)
        {
            if(writer != null)
            {
                base.Write(writer);

                writer.Write(IsRightBucket);
                writer.Write(FileExists);

                if (IsRightBucket)
                {

                    if(FileExists)
                    {
                        writer.Write(Version);
                        writer.Write(FileSize);
                    }
                }
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader != null)
            {
                base.Read(reader);

                IsRightBucket = reader.ReadBoolean();

                if(IsRightBucket)
                {
                    FileExists = reader.ReadBoolean();

                    if(FileExists)
                    {
                        Version = reader.ReadUInt64();
                        FileSize = reader.ReadUInt64();
                    }
                }
            }

            return this;
        }

        public void Send(CheckFileExistsRequest request, Socket sender, TimeSpan timeoutToSend)
        {
            if (request != null)
            {
                

                sender.SendTimeout = (Int32)timeoutToSend.TotalMilliseconds;
                var buffer = ToByteArray();
                sender.Send(buffer);

                LogResponse(sender, this);
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(request)}");
            }
        }


    }
}

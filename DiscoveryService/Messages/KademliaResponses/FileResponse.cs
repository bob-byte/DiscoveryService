using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    abstract class FileResponse : Response, ICloneable
    {
        /// <value>
        /// Default value is <a href="false"/> to set it if <see cref="IsRightBucket"/> = <a href="false"/> and you can use this value in every case to define whether file exists in remote <see cref="Contact"/>
        /// </value>
        public Boolean FileExists { get; set; }

        public Boolean IsRightBucket { get; set; }

        public String FileVersion { get; set; }

        public UInt64 FileSize { get; set; }

        public FileResponse()
        {
            FileExists = false;
        }

        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                base.Write(writer);

                writer.Write(IsRightBucket);
                writer.Write(FileExists);

                if (IsRightBucket)
                {
                    if (FileExists)
                    {
                        writer.WriteAsciiString(FileVersion);
                        writer.Write(FileSize);
                    }
                }
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                base.Read(reader);

                IsRightBucket = reader.ReadBoolean();

                if (IsRightBucket)
                {
                    FileExists = reader.ReadBoolean();

                    if (FileExists)
                    {
                        FileVersion = reader.ReadAsciiString();
                        FileSize = reader.ReadUInt64();
                    }
                }
            }

            return this;
        }

        public Object Clone() =>
            MemberwiseClone();
    }
}

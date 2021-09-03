using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    abstract class FileRequest : Request
    {
        public String FileOriginalName { get; set; }

        public String FilePrefix { get; set; }

        public String BucketName { get; set; }

        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                base.Write(writer);

                writer.WriteUtf32String(FileOriginalName);
                writer.WriteUtf32String(FilePrefix);
                writer.WriteAsciiString(BucketName);
            }
            else
            {
                throw new ArgumentNullException($"{nameof(writer)} is null");
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                base.Read(reader);

                FileOriginalName = reader.ReadUtf32String();
                FilePrefix = reader.ReadUtf32String();
                BucketName = reader.ReadAsciiString();
            }
            else
            {
                throw new ArgumentNullException($"{nameof(reader)} is null");
            }

            return this;
        }
    }
}

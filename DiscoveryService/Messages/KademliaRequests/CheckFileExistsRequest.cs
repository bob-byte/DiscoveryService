using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class CheckFileExistsRequest : Request
    {
        public CheckFileExistsRequest()
        {
            MessageOperation = MessageOperation.CheckFileExists;//try to put in static constructor
        }

        public String OriginalName { get; set; }

        public String FilePrefix { get; set; }

        public String BucketName { get; set; }

        public override void Write(WireWriter writer)
        {
            if(writer != null)
            {
                base.Write(writer);

                writer.Write(OriginalName);
                writer.Write(FilePrefix);
                writer.Write(BucketName);
            }
            else
            {
                throw new ArgumentNullException($"{nameof(writer)} is null");
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader != null)
            {
                base.Read(reader);

                OriginalName = reader.ReadString();
                FilePrefix = reader.ReadString();
                BucketName = reader.ReadString();
            }
            else
            {
                throw new ArgumentNullException($"{nameof(reader)} is null");
            }

            return this;
        }
    }
}

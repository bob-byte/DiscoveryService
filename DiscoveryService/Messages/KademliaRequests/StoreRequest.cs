using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    public class StoreRequest : Request
    {
        public BigInteger KeyToStore { get; set; }
        public String Value { get; set; }
        public Boolean IsCached { get; set; }
        public Int32 ExpirationTimeSec { get; set; }

        public StoreRequest()
        {
            ;//do nothing
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                base.Read(reader);

                KeyToStore = BigInteger.Parse(reader.ReadString());
                Value = reader.ReadString();
                IsCached = reader.ReadBoolean();
                ExpirationTimeSec = (Int32)reader.ReadUInt32();

                return this;
            }
            else
            {
                throw new ArgumentNullException("ReaderNullException");
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                base.Write(writer);

                writer.Write(KeyToStore.ToString());
                writer.Write(Value);
                writer.Write(IsCached);
                writer.Write((UInt32)ExpirationTimeSec);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }

        public override String ToString()
        {
            using (var writer = new StringWriter())
            {
                writer.WriteLine($"{GetType().Name}:\n" +
                                 $"{PropertyWithValue(nameof(RandomID), RandomID)};\n" +
                                 $"{PropertyWithValue(nameof(Sender), Sender)};\n" +
                                 $"{PropertyWithValue(nameof(KeyToStore), KeyToStore)};\n" +
                                 $"{PropertyWithValue(nameof(Value), Value)};\n" +
                                 $"{PropertyWithValue(nameof(IsCached), IsCached)};\n" +
                                 $"{PropertyWithValue(nameof(ExpirationTimeSec), ExpirationTimeSec)}");

                return writer.ToString();
            }
        }
    }
}

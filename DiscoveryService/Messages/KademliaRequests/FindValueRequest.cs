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
    public class FindValueRequest : Request
    {
        public BigInteger KeyToFindCloseContacts { get; set; }

        public FindValueRequest()
        {
            MessageOperation = MessageOperation.FindValue;
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                base.Read(reader);
                KeyToFindCloseContacts = reader.ReadBigInteger();

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
                writer.Write(KeyToFindCloseContacts);
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
                                 $"{PropertyWithValue(nameof(KeyToFindCloseContacts), KeyToFindCloseContacts)}");

                return writer.ToString();
            }
        }
    }
}

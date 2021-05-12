using LUC.DiscoveryService.CodingData;
using System;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.Messages
{
    public abstract class Message : IWireSerialiser
    {
        public const UInt32 ProtocolVersion = 1;

        public Message()
        {
            ;
        }

        /// <summary>
        ///   Create a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="messageId">
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        public Message(UInt32 messageId)
        {
            MessageId = messageId;
        }

        /// <summary>
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </summary>
        public UInt32 MessageId { get; set; }

        /// <summary>
        ///   Supported version of protocol of the remote application.
        /// </summary>
        public UInt32 VersionOfProtocol { get; set; }

        /// <summary>
        ///   Length in bytes of the object when serialised.
        /// </summary>
        /// <returns>
        ///   Numbers of bytes when serialised.
        /// </returns>
        public Int32 Length()
        {
            var writer = new WireWriter(Stream.Null);
            Write(writer);

            return writer.Position;
        }

        /// <summary>
        ///   Reads the Message object from a byte array.
        /// </summary>
        /// <param name="buffer">
        ///   The source for the Message object.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="buffer"/> is equal to null
        /// </exception>
        public IWireSerialiser Read(Byte[] buffer) =>
            Read(buffer, offset: 0, buffer.Length);

        /// <summary>
        ///   Reads the DNS object from a byte array.
        /// </summary>
        /// <param name="buffer">
        ///   The source for the DNS object.
        /// </param>
        /// <param name="offset">
        ///   The offset into the <paramref name="buffer"/>.
        /// </param>
        /// <param name="count">
        ///   The number of bytes in the <paramref name="buffer"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="buffer"/> is equal to null
        /// </exception>
        public IWireSerialiser Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            using(var stream = new MemoryStream(buffer, offset, count))
            {
                return Read(new CodingData.WireReader(stream));
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="reader"/> is equal to null
        /// </exception>
        public abstract IWireSerialiser Read(CodingData.WireReader reader);

        /// <summary>
        ///   Writes the Message object to a byte array.
        /// </summary>
        /// <returns>
        ///   A byte array containing the binary representaton of the Message object.
        /// </returns>
        public Byte[] ToByteArray()
        {
            using(var stream = new MemoryStream())
            {
                Write(stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        ///   Writes the Message object to a stream.
        /// </summary>
        /// <param name="stream">
        ///   The destination for the Message object.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="writer"/> is equal to null
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// 
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// 
        /// </exception>
        public void Write(Stream stream) =>
            Write(new WireWriter(stream));

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="writer"/> is equal to null
        /// </exception>
        public abstract void Write(WireWriter writer);
    }
}
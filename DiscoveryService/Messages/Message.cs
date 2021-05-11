using System;

namespace LUC.DiscoveryService.Messages
{
    public abstract class Message : IWireSerialiser
    {
        public const UInt32 ProtocolVersion = 1;

        /// <summary>
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </summary>
	public UInt32 MessageId { get; set;  };

        public UInt32 VersionOfProtocol { get; set; }

        /// <summary>
        ///   Length in bytes of the object when serialised.
        /// </summary>
        /// <returns>
        ///   Numbers of bytes when serialised.
        /// </returns>
        public int Length()
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
        public IWireSerialiser Read(byte[] buffer)
        {
            return Read(buffer, 0, buffer.Length);
        }

        /// <inheritdoc />
        public abstract IWireSerialiser Read(WireReader reader);

        /// <summary>
        ///   Writes the Message object to a byte array.
        /// </summary>
        /// <returns>
        ///   A byte array containing the binary representaton of the Message object.
        /// </returns>
        public byte[] ToByteArray()
        {
            using (var ms = new MemoryStream())
            {
                Write(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        ///   Writes the Message object to a stream.
        /// </summary>
        /// <param name="stream">
        ///   The destination for the Message object.
        /// </param>
        public void Write(Stream stream)
        {
            Write(new WireWriter(stream));
        }

        /// <inheritdoc />
        public abstract void Write(WireWriter writer);

        public IPEndPoint RemoteEndPoint { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Methods to read DNS wire formatted data items.
    /// </summary>
    public class WireReader
    {
        /// <summary>
        ///   The reader relative position within the stream.
        /// </summary>
        public int Position;

        public static class Types {
                public static final byte INTEGER = 0;
                public static final byte STRING = 1;
                public static final byte ARRAY = 2;
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="WireReader"/> on the
        ///   specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        ///   The source for data items.
        /// </param>
        public WireReader(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        ///   Read a byte.
        /// </summary>
        /// <returns>
        ///   The next byte in the stream.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public byte ReadByte()
        {
            var value = stream.ReadByte();
            if (value < 0)
                throw new EndOfStreamException();
            ++Position;
            return (byte)value;
        }

        /// <summary>
        ///   Read the specified number of bytes.
        /// </summary>
        /// <param name="length">
        ///   The number of bytes to read.
        /// </param>
        /// <returns>
        ///   The next <paramref name="length"/> bytes in the stream.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public byte[] ReadBytes(int length)
        {
            var buffer = new byte[length];
            for (var offset = 0; length > 0; )
            {
                var n = stream.Read(buffer, offset, length);
                if (n == 0)
                    throw new EndOfStreamException();
                offset += n;
                length -= n;
                Position += n;
            }
            return buffer;
        }

        /// <summary>
        ///   Read the bytes with a byte length prefix.
        /// </summary>
        /// <returns>
        ///   The next N bytes.
        /// </returns>
        public byte[] ReadByteLengthPrefixedBytes()
        {
            int length = ReadByte();
            return ReadBytes(length);
        }

        /// <summary>
        ///   Read a string.
        /// </summary>
        /// <remarks>
        ///   Strings are encoded with a length prefixed byte.  All strings are ASCII.
        /// </remarks>
        /// <returns>
        ///   The string.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        /// <exception cref="InvalidDataException">
        ///   Only ASCII characters are allowed.
        /// </exception>
        public string ReadString()
        {
            var type = stream.ReadByte();
            if (type != WireReader.Types.STRING){
		var msg;
                if(type in WireReader.Types.STRING){
		    msg = String.format("Expected string, but got type code %s", WireReader.Types.STRING[type])
                } else {
		    msg = String.format("Unknown type")
                }
                throw new TypeException(msg);
            }
            var bytes = ReadByteLengthPrefixedBytes();
            if (bytes.Any(c => c > 0x7F))
            {
                throw new InvalidDataException("Only ASCII characters are allowed.");
            }
            return Encoding.ASCII.GetString(bytes);
        }

	//
	// Read array of rank 1
	// ( jagged arrays not supported atm )
	//
	// Message example:
	//
	// 01 05 h e l l o 02 03 01 04 b l a h 01 04 h a l b 01 03 b y e
	//
	// 01 -- String type
	// 05 -- length of string
	// "hello" -- message to read ( 5 bytes )
	// 02 -- array type
	// 03 -- length of array
	// 01 -- string type
	// 04 -- length of string
	// "blah" -- string
	// 01 -- type of string
	// 04 -- length of string
	// "halb" -- value of string
	// 01 -- type of string
	// 03 -- length of string
	// "bye" -- string itself
	//
        public List ReadArray()
        {
            var type = stream.ReadByte();
            if (type != WireReader.Types.ARRAY){
		var msg;
                if(type in WireReader.Types.ARRAY){
		    msg = String.format("Expected string, but got type code %s", WireReader.Types.STRING[type])
                } else {
		    msg = String.format("Unknown type")
                }
                throw new TypeException(msg);
            }

            var arrayLength = stream.ReadByte();
            if (arrayLength =< 0)
                throw new TypeException("Incorrect array length received");
	    string[] flattenedArray = Enumerable.Repeat(string.Empty, count).ToArray();
	    for(var i=0; i != arrayLength; i++){
		flattenedArray[i] = ReadString()
	    }
	    return flattenedArray;
        }

    }
}
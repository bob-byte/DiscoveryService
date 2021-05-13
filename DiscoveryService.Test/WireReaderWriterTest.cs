using LUC.DiscoveryService.CodingData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscoveryService.Test
{
    /// <summary>
    /// Testing Wire Read/Write
    /// </summary>
    [TestClass]
    public class WireReaderWriterTest
    {
        /// <summary>
        ///   Testing write/read 
        /// </summary>
        [TestMethod]
        public void Roundtrip()
        {
            var someBytes = new Byte[] { 1, 2, 3 };
            UInt32 test = 35;

            var ms = new MemoryStream();
            var writer = new WireWriter(ms);

            writer.Write("alpha");
            writer.Write(test);
            writer.WriteBytes(someBytes);
            writer.WriteByteLengthPrefixedBytes(someBytes);

            ms.Position = 0;
            var reader = new WireReader(ms);
            Assert.AreEqual("alpha", reader.ReadString());
            Assert.AreEqual(test, reader.ReadUInt32());
            CollectionAssert.AreEqual(someBytes, reader.ReadBytes(3));
            CollectionAssert.AreEqual(someBytes, reader.ReadByteLengthPrefixedBytes());
        }

        /// <summary>
        ///   Testing Buffer Overflow with string
        /// </summary>
        [TestMethod]
        public void BufferOverflow_String()
        {
            var ms = new MemoryStream(new byte[] { 10, 1 });
            var reader = new WireReader(ms);
            Assert.ThrowsException<EndOfStreamException>(() => reader.ReadString());
        }

        /// <summary>
        ///   Testing write Big Prefixed Array
        /// </summary>
        [TestMethod]
        public void BytePrefixedArray_TooBig()
        {
            var bytes = new byte[byte.MaxValue + 1];
            var writer = new WireWriter(new MemoryStream());
            Assert.ThrowsException<ArgumentException>(() => writer.WriteByteLengthPrefixedBytes(bytes));
        }

        /// <summary>
        ///   Testing write Not Ascii symbols in string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void WriteString_NotAscii()
        {
            var writer = new WireWriter(Stream.Null);
            writer.Write("δοκιμή"); // test in Greek
        }

        /// <summary>
        ///   Testing write big string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void WriteString_TooBig()
        {
            var writer = new WireWriter(Stream.Null);
            writer.Write(new string('a', 256));
        }

        /// <summary>
        ///   Testing read Not Ascii symbols in string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidDataException))]
        public void ReadString_NotAscii()
        {
            var ms = new MemoryStream(new byte[] { 1, 255 });
            var reader = new WireReader(ms);
            reader.ReadString();
        }

        /// <summary>
        ///   Testing write/read IEnumerable string
        /// </summary>
        [TestMethod]
        public void IEnumerable()
        {
            List<String> data = new List<String> { "a","c","b" };

                MemoryStream ms = new MemoryStream();
                WireWriter writer = new WireWriter(ms);

                writer.WriteEnumerable(data);

                ms.Position = 0;
                WireReader reader = new WireReader(ms);
                CollectionAssert.AreEquivalent(data, reader.ReadStringList());
          
        }
    }
}

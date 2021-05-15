using LUC.DiscoveryService.CodingData;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscoveryService.Test
{
    /// <summary>
    /// Testing Wire Read/Write
    /// </summary>
    [TestFixture]
    public class WireReaderWriterTest
    {
        /// <summary>
        ///   Testing write/read 
        /// </summary>
        [Test]
        public void RoundTrip()
        {
            var expectedBytes = new Byte[] { 1, 2, 3 };
            UInt32 expectedUintValue = 35;
            String expectedStrValue = "alpha";
            var memoryStream = new MemoryStream();
            var writer = new WireWriter(memoryStream);

            writer.Write(expectedStrValue);
            writer.Write(expectedUintValue);
            writer.WriteBytes(expectedBytes);
            writer.WriteByteLengthPrefixedBytes(expectedBytes);
            memoryStream.Position = 0;
            var reader = new WireReader(memoryStream);

            Assert.AreEqual(expectedStrValue, actual: reader.ReadString());
            Assert.AreEqual(expectedUintValue, reader.ReadUInt32());
            CollectionAssert.AreEqual(expectedBytes, actual: reader.ReadBytes(3));
            CollectionAssert.AreEqual(expectedBytes, reader.ReadByteLengthPrefixedBytes());
        }

        /// <summary>
        ///   Testing Buffer Overflow with string
        /// </summary>
        [Test]
        public void BufferOverflow_String()
        {
            var memoryStream = new MemoryStream(new Byte[] { 10, 1 });
            var reader = new WireReader(memoryStream);

            Assert.That(code: () => reader.ReadString(), constraint: Throws.TypeOf(typeof(EndOfStreamException)));
        }

        /// <summary>
        ///   Testing write Big Prefixed Array
        /// </summary>
        [Test]
        public void BytePrefixedArray_TooBig()
        {
            var bytes = new Byte[Byte.MaxValue + 1];
            var writer = new WireWriter(new MemoryStream());

            Assert.That(code: () => writer.WriteByteLengthPrefixedBytes(bytes), constraint: Throws.TypeOf(typeof(ArgumentException)));
        }

        /// <summary>
        ///   Testing write Not Ascii symbols in string
        /// </summary>
        [Test]
        public void WriteString_NotAscii()
        {
            var writer = new WireWriter(Stream.Null);

            Assert.That(code: () => writer.Write("δοκιμή"), constraint: Throws.TypeOf(typeof(ArgumentException))); // test in Greek
        }

        /// <summary>
        ///   Testing write big string
        /// </summary>
        [Test]
        public void WriteString_TooBig()
        {
            var writer = new WireWriter(Stream.Null);

            Assert.That(code: () => writer.Write(new String('a', count: 256)), constraint: Throws.TypeOf(typeof(ArgumentException)));
        }

        /// <summary>
        ///   Testing read Not Ascii symbols in string
        /// </summary>
        [Test]
        public void ReadString_NotAscii()
        {
            var memoryStream = new MemoryStream(new Byte[] { 1, 255 });
            var reader = new WireReader(memoryStream);

             Assert.That(code: () => reader.ReadString(), constraint: Throws.TypeOf(typeof(InvalidDataException)));
        }

        /// <summary>
        ///   Testing write/read IEnumerable string
        /// </summary>
        [Test]
        public void IEnumerable()
        {
            List<String> data = new List<String> { "a", "c", "b" };
            MemoryStream ms = new MemoryStream();
            WireWriter writer = new WireWriter(ms);

            writer.WriteEnumerable(data);
            ms.Position = 0;
            WireReader reader = new WireReader(ms);

            CollectionAssert.AreEquivalent(expected: data, actual: reader.ReadListOfStrings());
        }
    }
}

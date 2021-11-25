using LUC.DiscoveryServices.CodingData;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;

namespace LUC.DiscoveryServices.Test
{
    /// <summary>
    /// Testing Wire Read/Write
    /// </summary>
    [TestFixture]
    public class WireReaderWriterTest
    {
        [Test]
        public void WireReaderWriter_GeneralTests_Equals()
        {
            Byte[] expectedBytes = new Byte[] { 1, 2, 3 };
            UInt32 expectedUintValue = 35;
            String expectedStrValue = "alpha";
            MemoryStream memoryStream = new MemoryStream();
            WireWriter writer = new WireWriter( memoryStream );

            writer.WriteAsciiString( expectedStrValue );
            writer.Write( expectedUintValue );
            writer.WriteBytes( expectedBytes );
            writer.WriteByteLengthPrefixedBytes( expectedBytes );
            memoryStream.Position = 0;
            WireReader reader = new WireReader( memoryStream );

            Assert.AreEqual( expected: expectedStrValue, actual: reader.ReadAsciiString() );
            Assert.AreEqual( expected: expectedUintValue, actual: reader.ReadUInt32() );
            CollectionAssert.AreEqual( expected: expectedBytes, actual: reader.ReadBytes( 3 ) );
            CollectionAssert.AreEqual( expected: expectedBytes, actual: reader.ReadByteLengthPrefixedBytes() );
        }

        [Test]
        public void ReadString_BufferOverflow_EndOfStreamException()
        {
            MemoryStream memoryStream = new MemoryStream( new Byte[] { 10, 1 } );
            WireReader reader = new WireReader( memoryStream );

            Assert.That( code: () => reader.ReadAsciiString(), constraint: Throws.TypeOf( typeof( EndOfStreamException ) ) );
        }

        [Test]
        public void WriteByteLengthPrefixedBytes_TooBigByteArray_ArgumentException()
        {
            Byte[] bytes = new Byte[ Byte.MaxValue + 1 ];
            WireWriter writer = new WireWriter( new MemoryStream() );

            Assert.That( code: () => writer.WriteByteLengthPrefixedBytes( bytes ), constraint: Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void WriteString_NotAscii_ArgumentException()
        {
            WireWriter writer = new WireWriter( Stream.Null );

            Assert.That( code: () => writer.WriteAsciiString( "δοκιμή" ), constraint: Throws.TypeOf( typeof( ArgumentException ) ) ); // test in Greek
        }

        [Test]
        public void WriteString_TooBigString_ArgumentException()
        {
            WireWriter writer = new WireWriter( Stream.Null );

            Assert.That( code: () => writer.WriteAsciiString( new String( 'a', count: 256 ) ), constraint: Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void ReadString_NotAscii_InvalidDataException()
        {
            MemoryStream memoryStream = new MemoryStream( new Byte[] { 1, 255 } );
            WireReader reader = new WireReader( memoryStream );

            Assert.That( code: () => reader.ReadAsciiString(), constraint: Throws.TypeOf( typeof( InvalidDataException ) ) );
        }

        [Test]
        public void IEnumerable_GeneralTest_AreEquivalent()
        {
            List<String> data = new List<String> { "a", "c", "b" };
            MemoryStream ms = new MemoryStream();
            WireWriter writer = new WireWriter( ms );

            writer.WriteEnumerable( data );
            ms.Position = 0;
            WireReader reader = new WireReader( ms );

            CollectionAssert.AreEquivalent( expected: data, actual: reader.ReadListOfAsciiStrings() );
        }
    }
}

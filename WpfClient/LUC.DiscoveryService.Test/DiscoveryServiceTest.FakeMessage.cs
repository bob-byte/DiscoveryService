using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.DiscoveryServices.Messages;

using System;

namespace LUC.DiscoveryServices.Test
{
    partial class DiscoveryServiceTest
    {
        private class FakeMessage : Message
        {
            public FakeMessage( Int32 byteCount )
            {
                var random = new Random();

                RndBytes = new Byte[ byteCount ];
                random.NextBytes( RndBytes );

                MessageOperation = MessageOperation.Acknowledge;
            }

            public Byte[] RndBytes { get; set; }

            public override void Write( WireWriter writer )
            {
                base.Write( writer );

                writer.Write( (UInt32)RndBytes.Length );
                writer.WriteBytes( RndBytes );
            }

            public override IWireSerialiser Read( WireReader reader )
            {
                base.Read( reader );

                Int32 byteCount = (Int32)reader.ReadUInt32();
                RndBytes = reader.ReadBytes( byteCount );

                return this;
            }
        }
    }
}

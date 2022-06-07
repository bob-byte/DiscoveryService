using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common.Interfaces;

using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    abstract class AbstractFileResponse : Response
    {
        protected AbstractFileResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        protected AbstractFileResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            ;
        }

        protected AbstractFileResponse()
        {
            ;
        }

        /// <value>
        /// Default value is <a href="false"/> to set it if <see cref="IsRightBucket"/> = <a href="false"/> and you can use this value in every case to define whether file exists in remote <see cref="IContact"/>
        /// </value>
        public Boolean FileExists { get; set; }

        public Boolean IsRightBucket { get; set; }

        public String FileVersion { get; set; }

        public UInt64 FileSize { get; set; }

        public override void Write( WireWriter writer )
        {
            if ( writer == null )
            {
                return;
            }

            base.Write( writer );

            writer.Write( IsRightBucket );
            writer.Write( FileExists );

            if ( !IsRightBucket || !FileExists )
            {
                return;
            }

            writer.WriteAsciiString( FileVersion );
            writer.Write( FileSize );
        }

        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader == null )
            {
                return this;
            }

            base.Read( reader );

            IsRightBucket = reader.ReadBoolean();
            FileExists = reader.ReadBoolean();

            if ( !IsRightBucket || !FileExists )
            {
                return this;
            }

            FileVersion = reader.ReadAsciiString();
            FileSize = reader.ReadUInt64();

            return this;
        }
    }
}

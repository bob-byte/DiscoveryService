using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;
using LUC.DiscoveryServices.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    abstract class AbstactFileResponse : Response, ICloneable
    {
        public AbstactFileResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            FileExists = false;
        }

        /// <value>
        /// Default value is <a href="false"/> to set it if <see cref="IsRightBucket"/> = <a href="false"/> and you can use this value in every case to define whether file exists in remote <see cref="Contact"/>
        /// </value>
        public Boolean FileExists { get; set; }

        //TODO delete it
        public Boolean IsRightBucket { get; set; }

        public String FileVersion { get; set; }

        public UInt64 FileSize { get; set; }

        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( IsRightBucket );
                writer.Write( FileExists );

                if ( ( IsRightBucket ) && ( FileExists ) )
                {
                    writer.WriteAsciiString( FileVersion );
                    writer.Write( FileSize );
                }
            }
        }

        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                IsRightBucket = reader.ReadBoolean();

                if ( IsRightBucket )
                {
                    FileExists = reader.ReadBoolean();

                    if ( FileExists )
                    {
                        FileVersion = reader.ReadAsciiString();
                        FileSize = reader.ReadUInt64();
                    }
                }
            }

            return this;
        }

        public Object Clone() =>
            MemberwiseClone();
    }
}

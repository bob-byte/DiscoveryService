﻿using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    abstract class AbstractFileRequest : Request
    {
        public AbstractFileRequest( BigInteger sender )
            : base( sender )
        {
            ;//do nothing
        }

        public AbstractFileRequest()
        {
            ;//do nothing
        }

        public String FileOriginalName { get; set; }

        public String HexPrefix { get; set; }

        public String BucketName { get; set; }

        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.WriteUtf32String( FileOriginalName );
                writer.WriteAsciiString( HexPrefix );
                writer.WriteAsciiString( BucketName );
            }
            else
            {
                throw new ArgumentNullException( $"{nameof( writer )} is null" );
            }
        }

        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                FileOriginalName = reader.ReadUtf32String();
                HexPrefix = reader.ReadAsciiString();
                BucketName = reader.ReadAsciiString();
            }
            else
            {
                throw new ArgumentNullException( $"{nameof( reader )} is null" );
            }

            return this;
        }
    }
}

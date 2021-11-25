using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    abstract class Response : Message
    {
        protected readonly static LoggingService s_log;

        static Response()
        {
            s_log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        /// <summary>
        /// Use this constructor when you want to read remote response
        /// </summary>
        public Response()
        {
            ;//do nothing
        }

        /// <summary>
        /// Use this constructor when you want to send response
        /// </summary>
        public Response( BigInteger requestRandomId )
        {
            RandomID = requestRandomId;
        }

        public BigInteger RandomID { get; private set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );
                RandomID = reader.ReadBigInteger();

                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
            }
        }

        /// <inheritdoc/>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );
                writer.Write( RandomID );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        protected void LogResponse( Socket sender, Int32 sentBytesCount ) =>
            s_log.LogInfo( $"Sent response ({sentBytesCount} bytes) to {sender.RemoteEndPoint}:\n" +
                         $"{this}" );
    }
}

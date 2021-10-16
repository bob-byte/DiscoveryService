using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
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

        public virtual void Send( Socket sender )
        {
            sender.SendTimeout = (Int32)Constants.SendTimeout.TotalMilliseconds;
            Byte[] buffer = ToByteArray();
            sender.Send( buffer );

            LogResponse( sender, buffer.Length );
        }

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

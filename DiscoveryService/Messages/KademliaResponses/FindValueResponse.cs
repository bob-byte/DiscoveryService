using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class FindValueResponse : Response
    {
        public FindValueResponse( BigInteger randomId )
            : base( randomId )
        {
            DefaultInit();
        }

        public ICollection<Contact> CloseContacts { get; set; }
        public String ValueInResponsingPeer { get; set; }

        public override void Send( Socket sender )
        {
            if ( ValueInResponsingPeer != null )
            {
                MessageOperation = MessageOperation.FindValueResponseWithValue;
            }
            else if ( CloseContacts != null )
            {
                MessageOperation = MessageOperation.FindValueResponseWithCloseContacts;
            }
            else
            {
                throw new ArgumentNullException( $"Both {nameof( CloseContacts )} and {nameof( ValueInResponsingPeer )} are equal to {null}" );
            }

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

                if ( MessageOperation == MessageOperation.FindValueResponseWithValue )
                {
                    ValueInResponsingPeer = reader.ReadAsciiString();
                }
                else if ( MessageOperation == MessageOperation.FindValueResponseWithCloseContacts )
                {
                    CloseContacts = reader.ReadListOfContacts( Constants.LAST_SEEN_FORMAT );
                }

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

                if ( ValueInResponsingPeer != null )
                {
                    writer.WriteAsciiString( ValueInResponsingPeer );
                }
                else if ( CloseContacts != null )
                {
                    writer.WriteEnumerable( CloseContacts, Constants.LAST_SEEN_FORMAT );
                }
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        public override String ToString()
        {
            using ( StringWriter writer = new StringWriter() )
            {
                writer.WriteLine( $"{GetType().Name}:\n" +
                                 $"{PropertyWithValue( nameof( RandomID ), RandomID )};\n" +
                                 $"{PropertyWithValue( nameof( ValueInResponsingPeer ), ValueInResponsingPeer )};\n" +
                                 $"{nameof( CloseContacts )}:" );

                if ( CloseContacts != null )
                {
                    foreach ( Contact closeContact in CloseContacts )
                    {
                        writer.WriteLine( $"Close contact: {closeContact}\n" );
                    }
                }

                return writer.ToString();
            }
        }

        protected override void DefaultInit( params Object[] args )
        {
            ;//without setting MessageOperation, because it is depand on other properties (see method FindValueResponse.Send)
        }
    }
}

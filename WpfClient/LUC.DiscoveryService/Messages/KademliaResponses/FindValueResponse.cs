using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class FindValueResponse : Response
    {
        public FindValueResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public FindValueResponse( BigInteger randomId )
            : base( randomId )
        {
            DefaultInit();
        }

        public String ValueInResponsingPeer { get; set; }

        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal ICollection<IContact> CloseContacts { get; set; }

        public override Task SendAsync( Socket sender )
        {
            CheckSendParsAndObjectState( sender );
            return base.SendAsync( sender );
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
                    CloseContacts = reader.ReadListOfContacts( DsConstants.LAST_SEEN_FORMAT );
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
                    writer.WriteEnumerable( CloseContacts, DsConstants.LAST_SEEN_FORMAT );
                }
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        public override String ToString() =>
            Display.ResponseWithCloseContacts( this, CloseContacts );

        protected override void DefaultInit( params Object[] args )
        {
            ;//without setting MessageOperation, because it is depand on
             //other properties (see method FindValueResponse.CheckSendParsAndObjectState)
        }

        private void CheckSendParsAndObjectState( Socket sender )
        {
            if ( sender == null )
            {
                throw new ArgumentNullException( nameof( sender ) );
            }
            else if ( ValueInResponsingPeer != null )
            {
                MessageOperation = MessageOperation.FindValueResponseWithValue;
            }
            else if ( CloseContacts != null )
            {
                MessageOperation = MessageOperation.FindValueResponseWithCloseContacts;
            }
            else
            {
                throw new InvalidOperationException( message: $"Both {nameof( CloseContacts )} and {nameof( ValueInResponsingPeer )} are equal to {null}" );
            }
        }
    }
}

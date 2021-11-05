using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;

namespace LUC.DiscoveryService.Common.Extensions
{
    static class SocketExtension
    {
        /// <summary>
        ///   Reads all available data
        /// </summary>
        public async static Task<Byte[]> ReadAllAvailableBytesAsync( this Socket socket, EventWaitHandle receiveDone, UInt16 chunkSizeToReadPerOneTime, Int32 maxReadBytes, CancellationToken cancellationToken )
        {
            List<Byte> allMessage = new List<Byte>();
            Int32 availableDataToRead = socket.Available;

            UInt32 messageLength = 0;
            Int32 chunkSize;
            Int32 readTimesCount = 0;
            do
            {
                if ( cancellationToken.IsCancellationRequested )
                {
                    break;
                }

                chunkSize = ChunkSize( availableDataToRead, chunkSizeToReadPerOneTime );
                Boolean isTooBigMessage = ( maxReadBytes < chunkSize + allMessage.Count );

                if ( (!isTooBigMessage) && ( availableDataToRead != 0 ) )
                {
                    ArraySegment<Byte> buffer = new ArraySegment<Byte>( new Byte[ chunkSize ] );
                    await socket.ReceiveAsync( buffer, SocketFlags.None ).ConfigureAwait( continueOnCapturedContext: false );
                    allMessage.AddRange( buffer );

                    availableDataToRead = socket.Available;

                    readTimesCount++;

                    if(readTimesCount == 1)
                    {
                        Message message = new Message();
                        message.Read( buffer.Array );

                        messageLength = message.MessageLength;
                    }
                }
                else if( isTooBigMessage )
                {
                    throw new InvalidOperationException( "Received too big message" );
                }
            }
            while ( allMessage.Count < messageLength );

            receiveDone.SafeSet(isSet: out _);

            return allMessage.ToArray();
        }

        private static Int32 ChunkSize( Int32 availableDataToRead, Int32 chunkSizeToReadPerOneTime )
        {
            Int32 chunkSize;

            if ( availableDataToRead < chunkSizeToReadPerOneTime )
            {
                chunkSize = availableDataToRead;
            }
            else
            {
                chunkSize = chunkSizeToReadPerOneTime;
            }

            return chunkSize;
        }
    }
}

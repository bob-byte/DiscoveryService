using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common.Extensions
{
    static class SocketExtension
    {
        /// <summary>
        ///   Reads all available data
        /// </summary>
        public async static Task<Byte[]> ReadAllAvailableBytesAsync( this Socket socket, EventWaitHandle receiveDone, Int32 chunkSizeToReadPerOneTime, Int32 maxReadBytes )
        {
            List<Byte> allMessage = new List<Byte>();
            Int32 availableDataToRead = socket.Available;
            
            Int32 chunkSize;
            Int32 countReadBytes;
            do
            {
                chunkSize = ChunkSize( availableDataToRead, chunkSizeToReadPerOneTime );

                if ( ( maxReadBytes >= chunkSize + allMessage.Count ) && ( availableDataToRead != 0 ) )
                {
                    ArraySegment<Byte> buffer = new ArraySegment<Byte>( new Byte[ chunkSize ] );
                    countReadBytes = await socket.ReceiveAsync( buffer, SocketFlags.None ).ConfigureAwait( continueOnCapturedContext: false );
                    allMessage.AddRange( buffer );

                    availableDataToRead = socket.Available;
                }
                else if( availableDataToRead == 0 )
                {
                    countReadBytes = 0;
                }
                else
                {
                    throw new InvalidOperationException( "Received too big message" );
                }
            }
            while ( ( countReadBytes > 0 ) && ( availableDataToRead > 0 ) );

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
            else if ( chunkSizeToReadPerOneTime <= availableDataToRead )
            {
                chunkSize = Constants.MAX_CHUNK_SIZE;
            }
            else
            {
                throw new InvalidOperationException();
            }

            return chunkSize;
        }
    }
}

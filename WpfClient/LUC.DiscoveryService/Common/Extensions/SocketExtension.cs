using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Messages;

using Nito.AsyncEx.Synchronous;

namespace LUC.DiscoveryServices.Common.Extensions
{
    static class SocketExtension
    {
        public static Byte[] ReadMessageBytes( this Socket socket, Int32 chunkSizeToReadPerOneTime, Int32 maxReadBytes, CancellationToken cancellationToken )
        {
            var allMessage = new List<Byte>();
            Int32 messageLength = 0;

            Int32 chunkSize;
            Boolean isFirstRead = true;
            Int32 maxNeededDataToRead;

            do
            {
                if ( isFirstRead )
                {
                    maxNeededDataToRead = Message.MIN_LENGTH;//bytes in Message.MessageOperation(Byte type) and Message.MessageLength(UInt32, so 4 bytes)
                }
                else
                {
                    maxNeededDataToRead = messageLength - allMessage.Count;
                }

                chunkSize = ChunkSize( maxNeededDataToRead, socket.Available, chunkSizeToReadPerOneTime );

                Boolean isTooBigMessage = maxReadBytes < chunkSize + allMessage.Count;

                if ( ( !isTooBigMessage ) && ( chunkSize != 0 ) && ( socket.Available != 0 ) )
                {
                    var buffer = new Byte[ chunkSize ];
                    Int32 countOfReadBytes = socket.ReceiveAsync( new ArraySegment<Byte>( buffer ), SocketFlags.None ).WaitAndUnwrapException( cancellationToken );

                    if ( countOfReadBytes != 0 )
                    {
                        IEnumerable<Byte> bufferEnumerable = buffer;

                        if ( countOfReadBytes < chunkSize )
                        {
                            bufferEnumerable = buffer.Take( countOfReadBytes );
                        }

                        allMessage.AddRange( bufferEnumerable );

                        if ( isFirstRead && allMessage.Count >= Message.MIN_LENGTH )
                        {
                            var message = new Message();
                            message.Read( allMessage.ToArray() );

                            messageLength = (Int32)message.MessageLength;
                            isFirstRead = false;
                        }
                    }
                }
                else if ( isTooBigMessage )
                {
                    throw new InvalidOperationException( message: "Received too big message" );
                }
            }
            while ( ( ( allMessage.Count < messageLength ) || isFirstRead ) && !cancellationToken.IsCancellationRequested );

            Boolean isInTimeCompleted = !cancellationToken.IsCancellationRequested;
            if ( isInTimeCompleted )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Read message with {allMessage.Count} bytes" );

                return allMessage.ToArray();
            }
            else
            {
                throw new TimeoutException( message: "Timeout to read message" );
            }
        }

        private static Int32 ChunkSize( Int32 maxNeededData, Int32 availableDataToRead, Int32 chunkSizeToReadPerOneTime )
        {
            Int32 chunkSize;

            if ( maxNeededData < chunkSizeToReadPerOneTime )
            {
                chunkSize = maxNeededData;
            }
            else
            {
                chunkSize = chunkSizeToReadPerOneTime;
            }

            if ( availableDataToRead < chunkSize )
            {
                chunkSize = availableDataToRead;
            }

            return chunkSize;
        }
    }
}

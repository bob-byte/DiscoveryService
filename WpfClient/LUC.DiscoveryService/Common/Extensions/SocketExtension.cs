using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Messages;

using Nito.AsyncEx;

namespace LUC.DiscoveryServices.Common.Extensions
{
    static class SocketExtension
    {
        /// <summary>
        ///   Reads all available data
        /// </summary>
        public async static Task<Byte[]> ReadMessageBytesAsync(this Socket socket, AsyncAutoResetEvent receiveDone, Int32 chunkSizeToReadPerOneTime, Int32 maxReadBytes, CancellationToken cancellationToken)
        {
            var allMessage = new List<Byte>();
            Int32 messageLength = 0;

            await Task.Run(async () =>
            {
                Int32 chunkSize;
                Boolean isFirstRead = true;
                Int32 maxNeededDataToRead;

                do
                {
                    if (isFirstRead)
                    {
                        maxNeededDataToRead = Message.MIN_LENGTH;//bytes in Message.MessageOperation(Byte type) and Message.MessageLength(UInt32, so 4 bytes)
                    }
                    else
                    {
                        maxNeededDataToRead = messageLength - allMessage.Count;
                    }

                    chunkSize = ChunkSize(maxNeededDataToRead, socket.Available, chunkSizeToReadPerOneTime);

                    Boolean isTooBigMessage = maxReadBytes < chunkSize + allMessage.Count;

                    if ((!isTooBigMessage) && (chunkSize != 0) && (socket.Available != 0))
                    {
                        var buffer = new ArraySegment<Byte>(new Byte[chunkSize]);
                        Int32 countOfReadBytes = await socket.ReceiveAsync(buffer, SocketFlags.None);

#if DEBUG
                        DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Read {countOfReadBytes} bytes" );
#endif

                        if (countOfReadBytes != 0)
                        {
                            IEnumerable<Byte> bufferEnumerable = buffer;

                            if (countOfReadBytes < chunkSize)
                            {
                                bufferEnumerable = buffer.Take(countOfReadBytes);
                            }

                            allMessage.AddRange(bufferEnumerable);

                            if (isFirstRead && allMessage.Count >= Message.MIN_LENGTH)
                            {
                                var message = new Message();
                                message.Read(allMessage.ToArray());

                                messageLength = (Int32)message.MessageLength;
                                isFirstRead = false;
                            }
                        }
                    }
                    else if (isTooBigMessage)
                    {
                        throw new InvalidOperationException(message: "Received too big message");
                    }
                }
                while (((allMessage.Count < messageLength) || isFirstRead) && !cancellationToken.IsCancellationRequested);

            }).ConfigureAwait(continueOnCapturedContext: false);

            receiveDone.Set();

            DsLoggerSet.DefaultLogger.LogInfo( $"Read message with {allMessage.Count} bytes" );

            return allMessage.ToArray();
        }

        private static Int32 ChunkSize(Int32 maxNeededData, Int32 availableDataToRead, Int32 chunkSizeToReadPerOneTime)
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

            if (availableDataToRead < chunkSize)
            {
                chunkSize = availableDataToRead;
            }

            return chunkSize;
        }
    }
}

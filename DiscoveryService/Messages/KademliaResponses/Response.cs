using LUC.DiscoveryService.CodingData;
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
    public abstract class Response : Message
    {
        protected static ILoggingService log;

        static Response()
        {
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        public BigInteger RandomID { get; set; }

        protected static void LogResponse(Socket sender, Response response)
        {
            log.LogInfo($"Sent response to {sender.RemoteEndPoint}:\n" +
                            $"{response}");
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                MessageOperation = (MessageOperation)reader.ReadUInt32();
                RandomID = BigInteger.Parse(reader.ReadString());

                return this;
            }
            else
            {
                throw new ArgumentNullException("ReaderNullException");
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                writer.Write((UInt32)MessageOperation);
                writer.Write(RandomID.ToString());
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }

        //protected virtual void Send(Socket sender, TimeSpan timeoutToSend, Byte[] bytesOfResponse)
        //{
        //    sender.SendTimeout = timeoutToSend.Milliseconds;
        //    sender.Send(bytesOfResponse);
        //}
    }
}

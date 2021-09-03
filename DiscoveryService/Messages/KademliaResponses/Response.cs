using LUC.DiscoveryService.CodingData;
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
        protected static LoggingService log;

        static Response()
        {
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        //public Response(BigInteger requestRandomId)
        //{
        //    RandomID = requestRandomId;
        //}

        public BigInteger RandomID { get; set; }

        public virtual void Send(Request request, Socket sender)
        {
            if (request != null)
            {
                sender.SendTimeout = (Int32)Constants.SendTimeout.TotalMilliseconds;
                var buffer = ToByteArray();
                sender.Send(buffer);

                LogResponse(sender, this);
            }
            else
            {
                throw new ArgumentNullException($"Bad format of {nameof(request)}");
            }
        }

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
                base.Read(reader);
                RandomID = reader.ReadBigInteger();

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
                base.Write(writer);
                writer.Write(RandomID);
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

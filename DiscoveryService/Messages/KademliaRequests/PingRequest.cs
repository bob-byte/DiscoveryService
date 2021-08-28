﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    public class PingRequest : Request
    {
        public PingRequest()
        {
            MessageOperation = MessageOperation.Ping;
        }

        public override string ToString()
        {
            using (var writer = new StringWriter())
            {
                writer.Write($"{GetType().Name}:\n" +
                             $"Random ID = {RandomID}");

                return writer.ToString();
            }
        }
    }
}

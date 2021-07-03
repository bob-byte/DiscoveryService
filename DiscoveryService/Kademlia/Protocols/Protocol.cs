﻿using System;
using LUC.DiscoveryService.Kademlia.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LUC.DiscoveryService.Kademlia.Protocols
{
    public static class Protocol
    {
        /// <summary>
        /// Returns the contact's protocol or, if not supported, null.
        /// </summary>
        public static IProtocol InstantiateProtocol(object protocol, string protocolName)
        {
            IProtocol ret = null;
            Type t = Type.GetType("Clifton.Kademlia.Protocols." + protocolName + ", Clifton.Kademlia.Protocols");

            if (t != null)
            {
                JObject jobj = (JObject)protocol;
                ret = (IProtocol)JsonConvert.DeserializeObject(protocol.ToString(), t);
            }

            return ret;
        }
    }
}

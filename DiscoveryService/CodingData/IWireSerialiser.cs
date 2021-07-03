﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LUC.DiscoveryService.Messages;

namespace LUC.DiscoveryService.CodingData
{
    public interface IWireSerialiser
    {
        /// <summary>
        ///   Reads the <see cref="DiscoveryServiceMessage"/> object that is encoded in the wire format.
        /// </summary>
        /// <param name="reader">
        ///   The source of the <see cref="DiscoveryServiceMessage"/> object.
        /// </param>
        /// <returns>
        ///   The final Message object.
        /// </returns>
        IWireSerialiser Read(WireReader reader);

        /// <summary>
        ///   Writes the <see cref="DiscoveryServiceMessage"/> object encoded in the wire format.
        /// </summary>
        /// <param name="writer">
        ///   The destination of the <see cref="DiscoveryServiceMessage"/> object.
        /// </param>
        void Write(WireWriter writer);
    }
}

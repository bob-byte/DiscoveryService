using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using LUC.DiscoveryService.Messages;

namespace LUC.DiscoveryService.Interfaces
{
    interface IWireSerialiser
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
        IWireSerialiser Read( Byte[] buffer );

        /// <summary>
        ///   Writes the <see cref="DiscoveryServiceMessage"/> object encoded in the wire format.
        /// </summary>
        /// <param name="writer">
        ///   The destination of the <see cref="DiscoveryServiceMessage"/> object.
        /// </param>
        void Write( Stream stream );
    }
}

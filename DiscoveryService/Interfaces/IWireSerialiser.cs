using System;
using System.IO;

using LUC.DiscoveryServices.Messages;

namespace LUC.DiscoveryServices.Interfaces
{
    interface IWireSerialiser
    {
        /// <summary>
        ///   Reads the <see cref="DiscoveryMessage"/> object that is encoded in the wire format.
        /// </summary>
        /// <param name="reader">
        ///   The source of the <see cref="DiscoveryMessage"/> object.
        /// </param>
        /// <returns>
        ///   The final Message object.
        /// </returns>
        IWireSerialiser Read( Byte[] buffer );

        /// <summary>
        ///   Writes the <see cref="DiscoveryMessage"/> object encoded in the wire format.
        /// </summary>
        /// <param name="writer">
        ///   The destination of the <see cref="DiscoveryMessage"/> object.
        /// </param>
        void Write( Stream stream );
    }
}

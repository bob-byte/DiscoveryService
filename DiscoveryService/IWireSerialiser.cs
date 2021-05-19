using System;
using System.Collections.Generic;
using System.Text;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Wire format serialisation of Message object.
    /// </summary>
    public interface IWireSerialiser
    {
        /// <summary>
        ///   Reads the Message object that is encoded in the wire format.
        /// </summary>
        /// <param name="reader">
        ///   The source of the Message object.
        /// </param>
        /// <returns>
        ///   The final Message object.
        /// </returns>
        IWireSerialiser Read(WireReader reader);

        /// <summary>
        ///   Writes the Message object encoded in the wire format.
        /// </summary>
        /// <param name="writer">
        ///   The destination of the Message object.
        /// </param>
        void Write(WireWriter writer);
    }
}

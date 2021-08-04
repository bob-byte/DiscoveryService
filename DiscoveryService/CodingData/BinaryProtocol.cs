using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.CodingData
{
    /// <summary>
    /// <a href="Abstract">Abstract</a> class, which contains common data for descendant classes
    /// </summary>
    public abstract class BinaryProtocol
    {
        /// <summary>
        /// 0x7F = 127
        /// </summary>
        protected const Byte MaxValueCharInAscii = 0x7F;

        /// <summary>
        /// Default count bits in 1 byte
        /// </summary>
        protected const Int32 BitsInOneByte = 8;

        /// <summary>
        /// Stream which is used to write and read bytes
        /// </summary>
        protected Stream stream;
    }
}

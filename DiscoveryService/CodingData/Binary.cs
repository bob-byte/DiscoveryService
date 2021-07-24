using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.CodingData
{
    public abstract class Binary
    {
        /// <summary>
        /// 0x7F = 127
        /// </summary>
        protected const Byte MaxValueCharInAscii = 0x7F;

        protected const Int32 BitsInOneByte = 8;

        protected Stream stream;
    }
}

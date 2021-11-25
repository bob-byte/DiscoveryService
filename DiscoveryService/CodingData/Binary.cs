using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.CodingData
{
    abstract class Binary
    {
        /// <summary>
        /// 0x7F = 127
        /// </summary>
        protected const Byte MAX_VALUE_CHAR_IN_ASCII = 0x7F;

        protected const Int32 BITS_IN_ONE_BYTE = 8;

        protected Stream m_stream;
    }
}

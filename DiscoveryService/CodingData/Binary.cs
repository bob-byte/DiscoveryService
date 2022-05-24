using System;
using System.IO;

namespace DiscoveryServices.CodingData
{
    public abstract class Binary
    {
        /// <summary>
        /// 0x7F = 127
        /// </summary>
        protected const Byte MAX_VALUE_CHAR_IN_ASCII = 0x7F;

        protected const Int32 BITS_IN_ONE_BYTE = 8;

        protected Stream m_stream;
    }
}

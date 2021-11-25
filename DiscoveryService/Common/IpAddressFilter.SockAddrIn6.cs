using LUC.DiscoveryServices.Kademlia;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    partial class IpAddressFilter
    {
        //We use fields instead of propetries to successfully marshal them to C++ functions
        [StructLayout( LayoutKind.Sequential )]
        private struct SockAddrIn6
        {
            private const Int32 BYTES_COUNT_IN_IPV6_ADDRESS = 16;

            public UInt16 Family;
            public UInt16 Port;
            public UInt32 FlowInfo;

            [MarshalAs( UnmanagedType.ByValArray, SizeConst = BYTES_COUNT_IN_IPV6_ADDRESS )]
            public Byte[] Addr;

            public UInt32 ScopeId;
        }
    }
}

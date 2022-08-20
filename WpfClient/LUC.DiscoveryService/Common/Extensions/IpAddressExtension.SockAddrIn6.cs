
using System;
using System.Runtime.InteropServices;

namespace LUC.DiscoveryServices.Common.Extensions
{
    static partial class IpAddressExtension
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

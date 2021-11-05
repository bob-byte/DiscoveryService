using LUC.DiscoveryService.Kademlia;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    partial class IpAddressFilter
    {
        private const String PATH_TO_IP_HELPER_API = "iphlpapi.dll";

        private const Int32 CODE_WIN_32_SUCCESS_OPERATION = 0;

        public static Boolean IsIpAddressInTheSameNetwork( IPAddress destinationAddress, IList<NetworkInterface> ourInterfaces = null )
        {
            UInt32 interfaceIndex;
            UInt32 gatewayForDest;
            if ( destinationAddress.AddressFamily == AddressFamily.InterNetwork )
            {
                UInt32 destAddr = BitConverter.ToUInt32( destinationAddress.GetAddressBytes(), startIndex: 0 );
                gatewayForDest = (UInt32)GetBestInterface( destAddr, out interfaceIndex );
            }
            else
            {
                SockAddrIn6 sockaddr = new SockAddrIn6
                {
                    ScopeId = (UInt32)destinationAddress.ScopeId,
                    Addr = destinationAddress.GetAddressBytes(),
                    Family = (UInt16)destinationAddress.AddressFamily
                };

                gatewayForDest = GetBestInterfaceEx( ref sockaddr, out Int32 ipv6InterfaceIndex );
                interfaceIndex = (UInt32)ipv6InterfaceIndex;
            }

            Boolean foundApprociateNetwork = false;

            if ( gatewayForDest == CODE_WIN_32_SUCCESS_OPERATION )
            {
                if ( ( ourInterfaces == null ) || ( ourInterfaces.Count == 0 ) )
                {
                    ourInterfaces = NetworkEventInvoker.NetworkInterfaces().ToList();
                }

                for ( Int32 numInterface = 0; ( numInterface < ourInterfaces.Count ) && ( !foundApprociateNetwork ); numInterface++ )
                {
                    IPInterfaceProperties ipProps = ourInterfaces[ numInterface ].GetIPProperties();
                    if ( ipProps != null )
                    {
                        IPAddress gateway = ipProps.GatewayAddresses?.FirstOrDefault()?.Address;

                        if ( gateway != null )
                        {
                            if ( ourInterfaces[ numInterface ].Supports( NetworkInterfaceComponent.IPv4 ) )
                            {
                                IPv4InterfaceProperties v4Props = ipProps.GetIPv4Properties();
                                if ( v4Props?.Index == interfaceIndex )
                                {
                                    foundApprociateNetwork = true;
                                }
                            }

                            if ( ( !foundApprociateNetwork ) && ( ourInterfaces[ numInterface ].Supports( NetworkInterfaceComponent.IPv6 ) ) )
                            {
                                IPv6InterfaceProperties v6Props = ipProps.GetIPv6Properties();
                                if ( v6Props?.Index == interfaceIndex )
                                {
                                    foundApprociateNetwork = true;
                                }
                            }
                        }
                    }
                }
            }            

            return foundApprociateNetwork;
        }

        [DllImport( PATH_TO_IP_HELPER_API, CharSet = CharSet.Auto )]
        private static extern UInt32 GetBestInterfaceEx( ref SockAddrIn6 ipAddress, out Int32 bestIfIndex );

        [DllImport( PATH_TO_IP_HELPER_API, CharSet = CharSet.Auto )]
        private static extern Int32 GetBestInterface( UInt32 destAddr, out UInt32 bestIfIndex );
    }
}

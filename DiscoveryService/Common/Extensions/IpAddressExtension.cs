
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DiscoveryServices.Common
{
    static partial class IpAddressExtension
    {
        private const String PATH_TO_IP_HELPER_API = "iphlpapi.dll";

        private const Int32 ERROR_SUCCESS = 0;
        private const Int32 ERROR_INVALID_PARAMETER = 87;

        public static Boolean CanBeReachableInCurrentNetwork( this IPAddress destinationAddress, IList<NetworkInterface> ourInterfaces = null )
        {
            UInt32 interfaceIndex = default;
            UInt32 codeOfError;

            switch ( destinationAddress.AddressFamily )
            {
                case AddressFamily.InterNetwork:
                {
                    UInt32 destAddr = BitConverter.ToUInt32( destinationAddress.GetAddressBytes(), startIndex: 0 );
                    codeOfError = (UInt32)GetBestInterface( destAddr, out interfaceIndex );

                    break;
                }
            
                case AddressFamily.InterNetworkV6:
                {
                    var sockaddr = new SockAddrIn6
                    {
                        ScopeId = (UInt32)destinationAddress.ScopeId,
                        Addr = destinationAddress.GetAddressBytes(),
                        Family = (UInt16)destinationAddress.AddressFamily
                    };

                    codeOfError = GetBestInterfaceEx( ref sockaddr, out Int32 ipv6InterfaceIndex );
                    interfaceIndex = (UInt32)ipv6InterfaceIndex;

                    break;
                }

                default:
                {
                    codeOfError = ERROR_INVALID_PARAMETER;
                    break;
                }
            }

            Boolean foundApprociateNetworkInterface = false;

            if ( codeOfError == ERROR_SUCCESS )
            {
                if ( ( ourInterfaces == null ) || ( ourInterfaces.Count == 0 ) )
                {
                    ourInterfaces = NetworkEventInvoker.AllTransmitableNetworkInterfaces().ToList();
                }

                for ( Int32 numInterface = 0; ( numInterface < ourInterfaces.Count ) && ( !foundApprociateNetworkInterface ); numInterface++ )
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
                                    foundApprociateNetworkInterface = true;
                                }
                            }

                            if ( ( !foundApprociateNetworkInterface ) && ourInterfaces[ numInterface ].Supports( NetworkInterfaceComponent.IPv6 ) )
                            {
                                IPv6InterfaceProperties v6Props = ipProps.GetIPv6Properties();
                                if ( v6Props?.Index == interfaceIndex )
                                {
                                    foundApprociateNetworkInterface = true;
                                }
                            }
                        }
                    }
                }

                if ( foundApprociateNetworkInterface )
                {
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Found IP {destinationAddress} which is reachable in current network" );
                }
                else
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"IP {destinationAddress} isn\'t reachable in current network" );
                }
            }
            else
            {
                var win32Exception = new Win32Exception( (Int32)codeOfError );

                String methodWithException = destinationAddress.AddressFamily == AddressFamily.InterNetwork ? nameof( GetBestInterface ) : nameof( GetBestInterfaceEx );
                DsLoggerSet.DefaultLogger.LogFatal( message: $"Method {methodWithException} returned Win32 code {win32Exception.ErrorCode}, so {win32Exception.Message}" );
            }

            return foundApprociateNetworkInterface;
        }

        [DllImport( PATH_TO_IP_HELPER_API, CharSet = CharSet.Auto )]
        private static extern UInt32 GetBestInterfaceEx( ref SockAddrIn6 ipAddress, out Int32 bestIfIndex );

        [DllImport( PATH_TO_IP_HELPER_API, CharSet = CharSet.Auto )]
        private static extern Int32 GetBestInterface( UInt32 destAddr, out UInt32 bestIfIndex );
    }
}

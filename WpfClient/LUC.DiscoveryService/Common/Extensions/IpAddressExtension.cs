using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LUC.DiscoveryServices.Common.Extensions
{
    static partial class IpAddressExtension
    {
        private const String PATH_TO_IP_HELPER_API = "iphlpapi.dll";

        private const Int32 ERROR_SUCCESS = 0;
        private const Int32 ERROR_INVALID_PARAMETER = 87;

        public static void DefineWhetherCanBeReachable( this IPAddress destinationAddress, out Boolean canBeReachable, out NetworkInterface connectedNetworkInterface, out IPAddress gateway )
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

            canBeReachable = false;
            connectedNetworkInterface = null;
            gateway = null;

            Boolean foundApprociateNetworkInterface = false;

            if ( codeOfError == ERROR_SUCCESS )
            {
                IList<NetworkInterface> ourInterfaces = NetworkEventInvoker.CurrentAllTransmitableNetworkInterfaces();

                for ( Int32 numInterface = 0; ( numInterface < ourInterfaces.Count ) && ( !foundApprociateNetworkInterface ); numInterface++ )
                {
                    connectedNetworkInterface = ourInterfaces[ numInterface ];
                    IPInterfaceProperties ipProps = connectedNetworkInterface.GetIPProperties();
                    if ( ipProps != null )
                    {
                        gateway = ipProps.GatewayAddresses?.FirstOrDefault()?.Address;

                        if ( gateway != null )
                        {
                            if ( connectedNetworkInterface.Supports( NetworkInterfaceComponent.IPv4 ) )
                            {
                                IPv4InterfaceProperties v4Props = ipProps.GetIPv4Properties();
                                if ( v4Props?.Index == interfaceIndex )
                                {
                                    foundApprociateNetworkInterface = true;
                                }
                            }

                            if ( ( !foundApprociateNetworkInterface ) &&
                                 connectedNetworkInterface.Supports( NetworkInterfaceComponent.IPv6 ) )
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
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Found IP {destinationAddress} which is reachable" );
                    canBeReachable = true;
                }
                else
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"IP {destinationAddress} isn\'t reachable" );
                }
            }
            else
            {
                var win32Exception = new Win32Exception( (Int32)codeOfError );

                String methodWithException = destinationAddress.AddressFamily == AddressFamily.InterNetwork ? nameof( GetBestInterface ) : nameof( GetBestInterfaceEx );
                DsLoggerSet.DefaultLogger.LogFatal( message: $"Method {methodWithException} returned Win32 code {win32Exception.ErrorCode}, so {win32Exception.Message}" );
            }
        }

        public static Boolean CanBeReachableInCurrentNetwork( this IPAddress destinationAddress )
        {
            DefineWhetherCanBeReachable( destinationAddress, out Boolean canBeReachable, out NetworkInterface connectedNetworkInterface, out IPAddress gateway );
            Boolean canBeReachableInOurNetwork;

            if ( canBeReachable )
            {
                Boolean isSameNetwork;
                if ( destinationAddress.AddressFamily == AddressFamily.InterNetwork )
                {
                    var ipv4Network = IPNetwork.Parse( gateway, SubnetMask.ClassC );
                    isSameNetwork = ipv4Network.Contains( destinationAddress );
                }
                else
                {
                    IPAddress ipv6LinkLocalAddress = connectedNetworkInterface.GetIPProperties().UnicastAddresses.FirstOrDefault( c => c.Address.IsIPv6LinkLocal )?.Address;

                    IPNetwork ipNetwork = ipv6LinkLocalAddress != null ? IPNetwork.Parse( ipv6LinkLocalAddress, SubnetMask.ClassC ) : null;
                    isSameNetwork = ( ipNetwork != null ) && ipNetwork.Contains( destinationAddress );
                }

                if ( isSameNetwork )
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"IP {destinationAddress} in the same network" );
                }
                else
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"IP {destinationAddress} in another network" );
                }

                canBeReachableInOurNetwork = canBeReachable && isSameNetwork;
            }
            else
            {
                canBeReachableInOurNetwork = false;
            }

            return canBeReachableInOurNetwork;
        }

        public static Boolean CanBeReachable( this IPAddress destinationAddress )
        {
            destinationAddress.DefineWhetherCanBeReachable( out Boolean canBeReachable, connectedNetworkInterface: out _, gateway: out _ );
            return canBeReachable;
        }

        public static Boolean IsInSubnet( this IPAddress address, string subnetMask )
        {
            var slashIdx = subnetMask.IndexOf( "/" );
            if ( slashIdx == -1 )
            { // We only handle netmasks in format "IP/PrefixLength".
                throw new NotSupportedException( "Only SubNetMasks with a given prefix length are supported." );
            }

            // First parse the address of the netmask before the prefix length.
            var maskAddress = IPAddress.Parse( subnetMask.Substring( 0, slashIdx ) );

            if ( maskAddress.AddressFamily != address.AddressFamily )
            { // We got something like an IPV4-Address for an IPv6-Mask. This is not valid.
                return false;
            }

            // Now find out how long the prefix is.
            int maskLength = int.Parse( subnetMask.Substring( slashIdx + 1 ) );

            if ( maskLength == 0 )
            {
                return true;
            }

            if ( maskLength < 0 )
            {
                throw new NotSupportedException( "A Subnetmask should not be less than 0." );
            }

            if ( maskAddress.AddressFamily == AddressFamily.InterNetwork )
            {
                // Convert the mask address to an unsigned integer.
                var maskAddressBits = BitConverter.ToUInt32( maskAddress.GetAddressBytes().Reverse().ToArray(), 0 );

                // And convert the IpAddress to an unsigned integer.
                var ipAddressBits = BitConverter.ToUInt32( address.GetAddressBytes().Reverse().ToArray(), 0 );

                // Get the mask/network address as unsigned integer.
                uint mask = uint.MaxValue << ( 32 - maskLength );

                // https://stackoverflow.com/a/1499284/3085985
                // Bitwise AND mask and MaskAddress, this should be the same as mask and IpAddress
                // as the end of the mask is 0000 which leads to both addresses to end with 0000
                // and to start with the prefix.
                return ( maskAddressBits & mask ) == ( ipAddressBits & mask );
            }

            if ( maskAddress.AddressFamily == AddressFamily.InterNetworkV6 )
            {
                // Convert the mask address to a BitArray. Reverse the BitArray to compare the bits of each byte in the right order.
                var maskAddressBits = new BitArray( maskAddress.GetAddressBytes().Reverse().ToArray() );

                // And convert the IpAddress to a BitArray. Reverse the BitArray to compare the bits of each byte in the right order.
                var ipAddressBits = new BitArray( address.GetAddressBytes().Reverse().ToArray() );
                var ipAddressLength = ipAddressBits.Length;

                if ( maskAddressBits.Length != ipAddressBits.Length )
                {
                    throw new ArgumentException( "Length of IP Address and Subnet Mask do not match." );
                }

                // Compare the prefix bits.
                for ( var i = ipAddressLength - 1; i >= ipAddressLength - maskLength; i-- )
                {
                    try
                    {
                        if ( ipAddressBits[ i ] != maskAddressBits[ i ] )
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }

                return true;
            }

            throw new NotSupportedException( "Only InterNetworkV6 or InterNetwork address families are supported." );
        }

        [DllImport( PATH_TO_IP_HELPER_API, CharSet = CharSet.Auto )]
        private static extern UInt32 GetBestInterfaceEx( ref SockAddrIn6 ipAddress, out Int32 bestIfIndex );

        [DllImport( PATH_TO_IP_HELPER_API, CharSet = CharSet.Auto )]
        private static extern Int32 GetBestInterface( UInt32 destAddr, out UInt32 bestIfIndex );
    }
}

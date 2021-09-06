using LUC.DiscoveryService.Kademlia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    class IpAddressFilter
    {

        [DllImport("iphlpapi.dll", CharSet = CharSet.Auto)]
        private static extern Int32 GetBestInterface(UInt32 destAddr, out UInt32 bestIfIndex);

        public static Boolean IsIpAddressInTheSameNetwork(IPAddress destinationAddress)
        {
            UInt32 destAddr = BitConverter.ToUInt32(destinationAddress.GetAddressBytes(), startIndex: 0);

            Int32 gatewayForDest = GetBestInterface(destAddr, out UInt32 interfaceIndex);
            if (gatewayForDest != 0)
            {
                throw new Win32Exception(gatewayForDest);
            }

            Boolean approciateNetwork = false;
            foreach (var networkInterface in NetworkEventInvoker.KnownNics)
            {
                IPInterfaceProperties ipProps = networkInterface.GetIPProperties();
                if(ipProps != null)
                {
                    IPAddress gateway = ipProps.GatewayAddresses?.FirstOrDefault()?.Address;

                    if(gateway != null)
                    {
                        if (networkInterface.Supports(NetworkInterfaceComponent.IPv4))
                        {
                            IPv4InterfaceProperties v4Props = ipProps.GetIPv4Properties();
                            if (v4Props?.Index == interfaceIndex)
                            {
                                approciateNetwork = true;
                            }
                        }
                        else if (networkInterface.Supports(NetworkInterfaceComponent.IPv6))
                        {
                            IPv6InterfaceProperties v6Props = ipProps.GetIPv6Properties();
                            if (v6Props?.Index == interfaceIndex)
                            {
                                approciateNetwork = true;
                            }
                        }
                    }
                }
            }

            return approciateNetwork;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DiscoveryServices.Extensions.IPExtensions
{
    static class IPAddressExtension
    {
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            Byte[] ipAddressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();
            var countBytes = ipAddressBytes.Length;

            if(countBytes != subnetMaskBytes.Length)
            {
                throw new ArgumentException("Lengths of IP address and subnet mask do not match");
            }
            else
            {
                var broadcastAddress = new Byte[countBytes];

                for (int i = 0; i < countBytes; i++)
                {
                    broadcastAddress[i] = (Byte)(ipAddressBytes[i] | (subnetMaskBytes[i] ^ 255));
                }
                var ipBroadcast = new IPAddress(broadcastAddress);

                return ipBroadcast;
            }
        }
    }
}

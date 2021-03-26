using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DiscoveryServices.Extensions
{
    public static class IPAddressExtension
    {
        private const Int32 ERROR_INVALID_PARAMETER = 87;
        private const Int32 ERROR_UNEXP_NET_ERR = 59;

        public static string GetLocalIPAddress(AddressFamily addressFamily)
        {
            Boolean isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            if (isNetworkAvailable)
            {
                String hostName = Dns.GetHostName();
                var host = Dns.GetHostEntry(hostName);

                foreach (var ip in host.AddressList)
                {
                    if(ip.AddressFamily == addressFamily)
                    {
                        return ip.ToString();
                    }
                }

                //$"Host {hostName} doesn\'t have ip in {addressFamily} format"
                throw new NetworkInformationException(ERROR_INVALID_PARAMETER);
            }
            else
            {
                throw new NetworkInformationException(ERROR_UNEXP_NET_ERR);
            }
        }
    }
}

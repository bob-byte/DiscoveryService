using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DiscoveryServices.Extensions.IPExtensions
{
    class Local_IP
    {
        private const Int32 ErrorInvalidParameter = 87;
        private const Int32 ErrorUnexpNetErr = 59;

        public static List<IPAddress> GetLocalIPAddresses()
        {
            Boolean isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();

            if (isNetworkAvailable)
            {
                String hostName = Dns.GetHostName();
                var host = Dns.GetHostEntry(hostName);

                return host.AddressList.ToList();
            }
            else
            {
                throw new NetworkInformationException(ErrorUnexpNetErr);
            }
        }

        public static String GetLocalIPAddress(AddressFamily addressFamily)
        {
            Boolean isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            if (isNetworkAvailable)
            {
                String hostName = Dns.GetHostName();
                var host = Dns.GetHostEntry(hostName);

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == addressFamily)
                    {
                        return ip.ToString();
                    }
                }

                //$"Host {hostName} doesn\'t have ip in {addressFamily} format"
                throw new NetworkInformationException(ErrorInvalidParameter);
            }
            else
            {
                throw new NetworkInformationException(ErrorUnexpNetErr);
            }
        }
    }
}

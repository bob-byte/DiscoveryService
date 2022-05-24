using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices.Common.Extensions
{
    internal static class NetworkInterfaceExtension
    {
        public static IEnumerable<NetworkInterface> TransmitableNetworkInterfaces( this IEnumerable<NetworkInterface> networkInterfaces ) =>
            networkInterfaces.Where( nic => ( nic.OperationalStatus == OperationalStatus.Up ) &&
                ( nic.NetworkInterfaceType != NetworkInterfaceType.Loopback ) );
    }
}

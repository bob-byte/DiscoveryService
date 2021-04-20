using System.Net;

namespace DiscoveryServices.Extensions.IPExtensions
{
    class IPAddressClass
    {
        public readonly static IPAddress ClassA = IPAddress.Parse("10.0.0.0");

        public readonly static IPAddress ClassB = IPAddress.Parse("172.16.0.0");

        public readonly static IPAddress ClassC = IPAddress.Parse("192.168.0.0");
    }
}

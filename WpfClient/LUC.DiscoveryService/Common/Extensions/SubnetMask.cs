using System.Net;

namespace LUC.DiscoveryServices.Common.Extensions
{
    static class SubnetMask
    {
        public readonly static IPAddress ClassA = IPAddress.Parse("255.0.0.0");

        public readonly static IPAddress ClassB = IPAddress.Parse("255.255.0.0");

        public readonly static IPAddress ClassC = IPAddress.Parse("255.255.255.0");
    }
}

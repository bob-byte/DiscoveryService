using DeviceId;
using System;

namespace DiscoveryServices.Extensions
{
    static class DeviceIdBuilderExtension
    {
        public static String GetMachineId(this DeviceIdBuilder deviceIdBuilder)
        {
            var motherboard = deviceIdBuilder.AddMotherboardSerialNumber().ToString();

            return $"{motherboard}{Guid.NewGuid()}";
        }
    }
}

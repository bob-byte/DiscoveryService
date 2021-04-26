using DeviceId;
using System;

namespace LUC.DiscoveryService.Extensions
{
    public static class DeviceIdBuilderExtension
    {
        public static String GetMachineId(this DeviceIdBuilder deviceIdBuilder)
        {
            var motherboard = deviceIdBuilder.AddMotherboardSerialNumber().ToString();

            return $"{motherboard}{Guid.NewGuid()}";
        }
    }
}

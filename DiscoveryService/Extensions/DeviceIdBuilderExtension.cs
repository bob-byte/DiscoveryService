using DeviceId;
using System;

namespace LUC.DiscoveryService.Extensions
{
    static class DeviceIdBuilderExtension
    {
        /// <summary>
        /// Get unique machine identifier
        /// </summary>
        /// <returns>
        /// Unique machine identifier
        /// </returns>
        public static String MachineId(this DeviceIdBuilder idBuilder)
        {
            var motherboard = idBuilder.AddMotherboardSerialNumber();
            return $"{motherboard}-{Guid.NewGuid()}";
        }
    }
}

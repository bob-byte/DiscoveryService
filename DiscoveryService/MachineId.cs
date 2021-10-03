using DeviceId;

using System;

namespace LUC.DiscoveryService
{
    class MachineId
    {
        /// <summary>
        /// Get unique machine identifier
        /// </summary>
        public static void Create(out String machineId)
        {
            DeviceIdBuilder machineIdBuilder = new DeviceIdBuilder();
            DeviceIdBuilder motherboard = machineIdBuilder.AddMotherboardSerialNumber();

            machineId = $"{motherboard}-{Guid.NewGuid()}";
        }
    }
}

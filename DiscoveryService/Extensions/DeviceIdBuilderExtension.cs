using DeviceId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Extensions
{
    static class DeviceIdBuilderExtension
    {
        public static String MachineId(this DeviceIdBuilder idBuilder)
        {
            var motherboard = idBuilder.AddMotherboardSerialNumber();
            return $"{motherboard}-{Guid.NewGuid()}";
        }
    }
}

using DeviceId;
using System;

namespace DiscoveryServices.Extensions
{
    public static class DeviceIdBuilderExtension
    {
        public static String GetDeviceId(this DeviceIdBuilder deviceIdBuilder) =>
            deviceIdBuilder.AddMotherboardSerialNumber().ToString();
    }
}

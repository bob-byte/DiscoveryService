//#define IS_IN_LUC

using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Services.Implementation;

using System;

namespace LUC.DiscoveryServices.Test.Extensions
{
    internal static class MachineIdExtensions
    {
        /// <summary>
        /// Get unique machine identifier. If it isn't exist, it will be written in .txt file in folder where .exe file exists
        /// </summary>
        public static String Create() =>
            MachineId.Create( fileName: $"{DsConstants.FILE_WITH_MACHINE_ID}{DsConstants.FILE_WITH_MACHINE_ID_EXTENSION}" );
    }
}

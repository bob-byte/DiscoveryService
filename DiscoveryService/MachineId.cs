using DeviceId;

using LUC.DiscoveryService.Common;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Reflection;

namespace LUC.DiscoveryService
{
    class MachineId
    {
        /// <summary>
        /// Get unique machine identifier
        /// </summary>
        public static void Create(out String machineId) =>
            Create( fileName: $"{Constants.FILE_WITH_MACHINE_ID}{Constants.FILE_WITH_MACHINE_ID_EXTENSION}", out machineId );

        internal static void Create( String fileName, out String machineId )
        {
#if DEBUG
            String fullFileNameWithMachineId = FullFileNameWithMachineId( fileName );

            if ( File.Exists( fullFileNameWithMachineId ) )
            {
                using ( StreamReader streamReader = new StreamReader( fullFileNameWithMachineId ) )
                {
                    machineId = streamReader.ReadToEnd();
                }
            }
            else
            {
#endif
                DeviceIdBuilder machineIdBuilder = new DeviceIdBuilder();
                DeviceIdBuilder motherboard = machineIdBuilder.AddMotherboardSerialNumber();

                machineId = $"{motherboard}-{Guid.NewGuid()}";

#if DEBUG
                using ( StreamWriter streamWriter = new StreamWriter( fullFileNameWithMachineId ) )
                {
                    streamWriter.Write( machineId );
                }
            }
#endif
        }

#if DEBUG
        private static String FullFileNameWithMachineId( String fileName )
        {
            String fullExeName = Assembly.GetExecutingAssembly().Location;
            String pathToExeFile = Path.GetDirectoryName( fullExeName );

            String fullFileNameWithMachineId = $"{pathToExeFile}\\{fileName}";

            return fullFileNameWithMachineId;
        }
#endif
    }
}

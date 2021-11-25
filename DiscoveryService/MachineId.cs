//#define IS_IN_LUC

using DeviceId;

using LUC.DiscoveryServices.Common;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Reflection;

namespace LUC.DiscoveryServices
{
    class MachineId
    {
        /// <summary>
        /// Get unique machine identifier
        /// </summary>
        public static void Create(out String machineId)
        {
#if IS_IN_LUC
            machineId = Created();
#else
            Create( fileName: $"{Constants.FILE_WITH_MACHINE_ID}{Constants.FILE_WITH_MACHINE_ID_EXTENSION}", out machineId );
#endif
        }

        internal static void Create( String fileName, out String machineId )
        {
#if ( !IS_IN_LUC ) || ( INTEGRATION_TESTS )
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
                machineId = Created();

#if ( !IS_IN_LUC ) || ( INTEGRATION_TESTS )
                using ( StreamWriter streamWriter = new StreamWriter( fullFileNameWithMachineId ) )
                {
                    streamWriter.Write( machineId );
                }
            }
#endif
        }

        private static String Created()
        {
            DeviceIdBuilder machineIdBuilder = new DeviceIdBuilder();
            DeviceIdBuilder motherboard = machineIdBuilder.AddMotherboardSerialNumber();

            String machineId = $"{motherboard}-{Guid.NewGuid()}";
            return machineId;
        }

#if ( !IS_IN_LUC ) || ( INTEGRATION_TESTS )
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

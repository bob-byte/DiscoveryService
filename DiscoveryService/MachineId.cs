﻿using DeviceId;

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
        public static void Create(out String machineId)
        {
#if ( RECEIVE_TCP_FROM_OURSELF ) || ( INTEGRATION_TESTS )
            String fullFileNameWithMachineId = FullFileNameWithMachineId();

            if (File.Exists(fullFileNameWithMachineId))
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

#if ( RECEIVE_TCP_FROM_OURSELF ) || ( INTEGRATION_TESTS )
                using ( StreamWriter streamWriter = new StreamWriter( fullFileNameWithMachineId ) )
                {
                    streamWriter.Write( machineId );
                }
            }
#endif
        }

#if ( RECEIVE_TCP_FROM_OURSELF ) || ( INTEGRATION_TESTS )
        private static String FullFileNameWithMachineId()
        {
            String fullExeName = Assembly.GetExecutingAssembly().Location;
            String pathToExeFile = Path.GetDirectoryName( fullExeName );

            String fullFileNameWithMachineId = $"{pathToExeFile}\\{Constants.FILE_WITH_MACHINE_ID}";

            return fullFileNameWithMachineId;
        }
#endif
    }
}

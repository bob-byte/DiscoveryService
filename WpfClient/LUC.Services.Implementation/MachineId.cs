using DeviceId;

using LUC.Interfaces.Extensions;

using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace LUC.Services.Implementation
{
    public static class MachineId
    {
        /// <summary>
        /// Get unique machine identifier with GUID. If it isn't exist, it will be 
        /// written in <paramref name="fileName"/> in folder where .exe-file exists
        /// </summary>
        public static String Create( String fileName )
        {
            String fullFileNameWithMachineId = FullFileNameWithMachineId( fileName );

            String machineId;
            if ( File.Exists( fullFileNameWithMachineId ) )
            {
                using ( var streamReader = new StreamReader( fullFileNameWithMachineId ) )
                {
                    machineId = streamReader.ReadToEnd();
                }
            }
            else
            {
                machineId = $"{Create()}---{Guid.NewGuid()}";

                using ( var streamWriter = new StreamWriter( fullFileNameWithMachineId ) )
                {
                    streamWriter.Write( machineId );
                }
            }

            return machineId;
        }

        /// <summary>
        /// Created machine ID
        /// </summary>
        public static String Create()
        {
            var deviceIdBuilder = new DeviceIdBuilder();

            String encodedMachineId = deviceIdBuilder.
                AddProcessorId().
                AddMotherboardSerialNumber().
                ToString();

            Byte[] encodedIdAsBytes = Encoding.UTF8.GetBytes( encodedMachineId );

            //to get ASCII encoding
            encodedMachineId = ArrayExtensions.CalculateMd5Hash( encodedIdAsBytes );
            return encodedMachineId;
        }

        private static String FullFileNameWithMachineId( String fileName )
        {
            String fullExeName = Assembly.GetEntryAssembly().Location;
            String pathToExeFile = Path.GetDirectoryName( fullExeName );

            String fullFileNameWithMachineId = $"{pathToExeFile}\\{fileName}";

            return fullFileNameWithMachineId;
        }
    }
}

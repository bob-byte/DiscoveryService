using System;
using System.Management;

namespace LUC.Interfaces.Helpers
{
    public static class OsVersionHelper
    {
        public static String Version()
        {
            String version = String.Empty;

            using ( var searcher = new ManagementObjectSearcher( queryString: "SELECT * FROM Win32_OperatingSystem" ) )
            {
                ManagementObjectCollection queryResult = searcher.Get();
                if ( queryResult != null )
                {
                    foreach ( ManagementBaseObject managementObject in queryResult )
                    {
                        version = $"{managementObject[ propertyName: "Caption" ]} - {managementObject[ "OSArchitecture" ]}";
                    }
                }
            }

            version = version.Replace( oldValue: "NT 5.1.2600", newValue: "XP" );
            version = version.Replace( "NT 5.2.3790", "Server 2003" );
            return version;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test.Extensions
{
    class PathExtensions
    {
        public static Boolean IsValidFullFileName( String fullFileName )
        {
            String fileName = Path.GetFileName( fullFileName );
            return ( IsValidPath(fullFileName) ) &&
                ( !fileName.Intersect( Path.GetInvalidFileNameChars() ).Any() );
        }

        public static Boolean IsValidPath( String pathToFile ) =>
            !pathToFile.Intersect( Path.GetInvalidPathChars() ).Any();

        public static String PathToExeFile()
        {
            String fullExeFileName = Assembly.GetEntryAssembly().Location;
            String pathToExeFile = Path.GetDirectoryName( fullExeFileName );

            return pathToExeFile;
        }
    }
}

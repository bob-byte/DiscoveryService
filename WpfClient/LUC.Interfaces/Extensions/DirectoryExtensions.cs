using LUC.Interfaces.Constants;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.Interfaces.Extensions
{
    public static class DirectoryExtensions
    {
        public static DirectoryInfo DirectoryForDownloadingTempFiles(String syncFolder)
        {
            String pathToDirectoryWithDownloadTempFiles = Path.Combine( syncFolder, DownloadConstants.FOLDER_NAME_FOR_DOWNLOADING_FILES );

            DirectoryInfo directoryInfo = Directory.CreateDirectory( pathToDirectoryWithDownloadTempFiles );
            directoryInfo.Attributes = FileAttributes.Hidden;
            return directoryInfo;
        }
    }
}

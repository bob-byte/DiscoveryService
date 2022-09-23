
using LUC.DiscoveryServices.Common.Extensions;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Extensions;
using LUC.Services.Implementation;

using Microsoft.Win32.SafeHandles;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public partial class DownloaderFromLocalNetwork
    {
        /// <summary>
        /// It is thread safe class. That is the reason why it has only methods
        /// </summary>
        private sealed class DownloadingFile
        {
            public String FileNameFromSyncFolder( String syncFolder, String fullFileName ) =>
                fullFileName.Substring( syncFolder.Length + 1 );

            public void SetTempFileAttributes( String fullPathToTempFile, SafeFileHandle fileHandle )
            {
                FileExtensions.SetAttributesToTempDownloadingFile( fullPathToTempFile );
                MarkAsSparseFile( fileHandle, fullPathToTempFile );
            }

            public String UniqueTempFullFileName( String fullTempFileName )
            {
                String pathToTempFile = Path.GetDirectoryName( fullTempFileName );
                String tempFileName = Path.GetFileName( fullTempFileName );

                String uniqueTempFileName = (String)tempFileName.Clone();
                String fullUniqueTempFileName = $"{pathToTempFile}\\{uniqueTempFileName}";

                var random = new Random();
                while ( File.Exists( fullUniqueTempFileName ) )
                {
                    String rndSymbol = random.RandomSymbols( count: 1 );
                    uniqueTempFileName = uniqueTempFileName.Insert( startIndex: 2, rndSymbol );

                    fullUniqueTempFileName = $"{pathToTempFile}\\{uniqueTempFileName}";
                }

                return fullUniqueTempFileName;
            }


            public void TryDeleteFile( String fullFileName )
            {
                try
                {
                    File.Delete( fullFileName );
                }
                //user opened this file
                catch ( IOException )
                {
                    ;//do nothing
                }
            }

            public String TempFullFileName( String realFullFileName, String syncFolder ) =>
                PathExtensions.TempFullFileNameForDownload( realFullFileName, syncFolder );

            public String TempFullFileName( String fullFileName )
            {
                String fileExtension = Path.GetExtension( fullFileName );

                String tempFullFileName;
                String tempFileExtension = DownloadConstants.TEMP_FILE_NAME_EXTENSION;

                if ( fileExtension.Equals( tempFileExtension, StringComparison.Ordinal ) )
                {
                    tempFullFileName = (String)fullFileName.Clone();
                }
                else
                {
                    tempFullFileName = fullFileName.Replace( fileExtension, tempFileExtension );
                }

                return tempFullFileName;
            }

            [DllImport( "Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto )]
            private static extern Boolean DeviceIoControl(
                SafeFileHandle handleDevice,
                Int32 dwIoControlCode,
                IntPtr inBuffer,
                Int32 nInBufferSize,
                IntPtr outBuffer,
                Int32 nOutBufferSize,
                ref Int32 pBytesReturned,
                [In] ref NativeOverlapped lpOverlapped
            );

            private static void MarkAsSparseFile( SafeFileHandle fileHandle, String fullFileName )
            {
                Int32 bytesReturned = 0;
                var lpOverlapped = new NativeOverlapped();
                Int32 fsctlSetSparse = 590020;

                Boolean isFileMarkedAsSparse = DeviceIoControl(
                    fileHandle,
                    fsctlSetSparse,
                    inBuffer: IntPtr.Zero,
                    nInBufferSize: 0,
                    outBuffer: IntPtr.Zero,
                    nOutBufferSize: 0,
                    ref bytesReturned,
                    ref lpOverlapped
                );

                if ( !isFileMarkedAsSparse )
                {
                    throw new InvalidOperationException( $"Cannot mark downloading file with temp name {fullFileName} as sparse" );
                }
            }
        }
    }
}

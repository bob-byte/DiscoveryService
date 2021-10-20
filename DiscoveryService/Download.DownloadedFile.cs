using LUC.DiscoveryService.Kademlia;

using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    public partial class Download
    {
        /// <summary>
        /// It is thread safe class. That is the reason why it has only methods
        /// </summary>
        private class DownloadedFile
        {
            ///<summary>
            /// Full file name is path to file, file name and extension
            /// </summary>
            public String FullFileName( String localFolderPath, String localOriginalName )
            {
                String fullPathToFile;

#if RECEIVE_TCP_FROM_OURSELF
                //download in root directory, because you cannot create file with the same path and name
                String rootFolder = Path.GetDirectoryName( localFolderPath );
                fullPathToFile = Path.Combine( rootFolder, localOriginalName );

#else
                fullPathToFile = Path.Combine( localFolderPath, localOriginalName );
#endif

                return fullPathToFile;
            }

            public void SetTempFileAttributes( String fullPathToTempFile, SafeFileHandle fileHandle )
            {
                FileAttributes tempFileAttributes = FileAttributes.Hidden | FileAttributes.ReadOnly;
                File.SetAttributes( fullPathToTempFile, tempFileAttributes );

                try
                {
                    MarkAsSparseFile( fileHandle );
                }
                catch ( InvalidOperationException )
                {
                    InvalidOperationException exWithMessage = new InvalidOperationException( $"Cannot to mark downloaded file with temp name {fullPathToTempFile} as sparse" );
                    throw exWithMessage;
                }
            }

            public String UniqueTempFullFileName( String tempFullPath )
            {
                String pathToTempFile = Path.GetDirectoryName( tempFullPath );
                String tempFileName = Path.GetFileName( tempFullPath );

                String uniqueTempFileName = (String)tempFileName.Clone();
                String fullUniqueTempFileName = $"{pathToTempFile}\\{uniqueTempFileName}";
                while ( File.Exists( fullUniqueTempFileName ) )
                {
                    uniqueTempFileName = uniqueTempFileName.Insert( startIndex: 2, value: "_" );
                    fullUniqueTempFileName = $"{pathToTempFile}\\{uniqueTempFileName}";
                }

                return fullUniqueTempFileName;
            }

            public void RenameFile( String sourceFileName, String destFileName ) => File.Move( sourceFileName, destFileName );

            public void TryDeleteFile( String fullPathToFile )
            {
                if ( File.Exists( fullPathToFile ) )
                {
                    File.Delete( fullPathToFile );
                }
            }

            public String TempFullFileName( String fullBigFileName )
            {
                String pathToBigFile = Path.GetDirectoryName( fullBigFileName );
                String nameBigFile = Path.GetFileName( fullBigFileName );

                String tempFullFileName = $"{pathToBigFile}\\~.{nameBigFile}";
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

            private static void MarkAsSparseFile( SafeFileHandle fileHandle )
            {
                Int32 bytesReturned = 0;
                NativeOverlapped lpOverlapped = new NativeOverlapped();
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
                    throw new InvalidOperationException();
                }
            }
        }
    }
}

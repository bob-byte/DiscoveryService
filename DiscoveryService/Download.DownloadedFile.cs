using LUC.DiscoveryService.Kademlia;
using LUC.DVVSet;
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
        private class DownloadedFile
        {
            /// <returns>
            /// If it is Functional test where is in use only current PC, return will be <paramref name="localFolderPath"/> + <paramref name="filePrefix"/> + <paramref name="localOriginalName"/>, else <paramref name="bucketName"/> also will be used
            /// </returns>
            public String FullPathToFile(ICollection<Contact> onlineContacts, String ourMachineId, String localFolderPath,
                String bucketName, String localOriginalName, String filePrefix)
            {
                String fullPathToFile;
                Boolean canReceivedAnswerFromYourself = onlineContacts.Any(c => (ourMachineId == onlineContacts.First().MachineId));
                if (canReceivedAnswerFromYourself)
                {
                    fullPathToFile = Path.Combine(localFolderPath, filePrefix, localOriginalName);
                }
                else
                {
                    fullPathToFile = Path.Combine(localFolderPath, bucketName, filePrefix, localOriginalName);
                }

                return fullPathToFile;
            }

            public void SetTempFileAttributes(String fullPathToTempFile, SafeFileHandle fileHandle)
            {
                FileAttributes tempFileAttributes = FileAttributes.Hidden | FileAttributes.ReadOnly;
                File.SetAttributes(fullPathToTempFile, tempFileAttributes);

                MarkAsSparseFile(fileHandle);
            }

            public String UniqueTempFullFileName(String tempFullPath)
            {
                String pathToTempFile = Path.GetDirectoryName(tempFullPath);
                String tempFileName = Path.GetFileName(tempFullPath);

                String uniqueTempFileName = (String)tempFileName.Clone();
                while (File.Exists(tempFullPath))
                {
                    uniqueTempFileName.Insert(startIndex: 2, value: "_");
                }

                return $"{pathToTempFile}\\{uniqueTempFileName}";
            }

            public void RenameFile(String sourceFileName, String destFileName)
            {
                File.Move(sourceFileName, destFileName);
            }

            public void TryDeleteFile(String fullPathToFile)
            {
                if (File.Exists(fullPathToFile))
                {
                    File.Delete(fullPathToFile);
                }
            }

            public String TempFullFileName(String fullBigFileName)
            {
                String pathToBigFile = Path.GetDirectoryName(fullBigFileName);
                String nameBigFile = Path.GetFileName(fullBigFileName);

                String tempFullFileName = $"{pathToBigFile}\\~.{nameBigFile}";
                return tempFullFileName;
            }

            [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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

            private static void MarkAsSparseFile(SafeFileHandle fileHandle)
            {
                Int32 bytesReturned = 0;
                var lpOverlapped = new NativeOverlapped();
                Int32 fsctlSetSparse = 590020;

                Boolean result = DeviceIoControl(
                    fileHandle,
                    fsctlSetSparse,
                    inBuffer: IntPtr.Zero,
                    nInBufferSize: 0,
                    outBuffer: IntPtr.Zero,
                    nOutBufferSize: 0,
                    ref bytesReturned,
                    ref lpOverlapped);

                if (!result)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}

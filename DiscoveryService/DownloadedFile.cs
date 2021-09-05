using LUC.DiscoveryService.Kademlia;
using LUC.DVVSet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    class DownloadedFile
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

    }
}

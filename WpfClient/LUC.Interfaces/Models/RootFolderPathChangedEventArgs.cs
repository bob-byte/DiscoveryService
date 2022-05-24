using System;

namespace LUC.Interfaces.Models
{
    public class RootFolderPathChangedEventArgs : EventArgs
    {
        public RootFolderPathChangedEventArgs( String oldRootFolder, String newRootFolder )
        {
            OldRootFolder = oldRootFolder;
            NewRootFolder = newRootFolder;
        }

        public String OldRootFolder { get; }

        public String NewRootFolder { get; }
    }
}

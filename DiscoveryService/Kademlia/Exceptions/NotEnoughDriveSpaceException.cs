using System;
using System.IO;

namespace DiscoveryServices.Kademlia.Exceptions
{
    public class NotEnoughDriveSpaceException : IOException
    {
        public NotEnoughDriveSpaceException( String fullPathToFile )
        {
            DefaultInit( fullPathToFile );
        }

        public NotEnoughDriveSpaceException( String fullPathToFile, String message )
            : base( message )
        {
            DefaultInit( fullPathToFile );
        }

        public String FullPathToFile { get; private set; }

        private void DefaultInit( String fullPathToFile ) =>
            FullPathToFile = fullPathToFile;
    }
}

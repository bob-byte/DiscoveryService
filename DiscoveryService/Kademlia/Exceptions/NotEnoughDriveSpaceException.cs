using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    class NotEnoughDriveSpaceException : Exception
    {
        public NotEnoughDriveSpaceException(String fullPathToFile)
        {
            FullPathToFile = fullPathToFile;
        }

        public NotEnoughDriveSpaceException( String fullPathToFile, String message )
            : base(message)
        {
            FullPathToFile = fullPathToFile;
        }

        public String FullPathToFile { get; }
    }
}

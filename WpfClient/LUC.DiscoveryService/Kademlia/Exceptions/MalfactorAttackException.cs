using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    internal class MalfactorAttackException : Exception
    {
        public MalfactorAttackException() 
        {
            ;//do nothing
        }

        public MalfactorAttackException( String message ) 
            : base( message ) 
        {
            ;
        }
    }
}

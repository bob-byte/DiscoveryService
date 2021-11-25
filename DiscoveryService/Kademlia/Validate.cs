using System;

namespace LUC.DiscoveryServices.Kademlia
{
    public static class Validate
    {
        public static void IsTrue<T>( Boolean b, String errorMessage ) where T : Exception, new()
        {
            if ( !b )
            {
                throw (T)Activator.CreateInstance( typeof( T ), new Object[] { errorMessage } );
            }
        }

        public static void IsFalse<T>( Boolean b, String errorMessage ) where T : Exception, new()
        {
            if ( b )
            {
                throw (T)Activator.CreateInstance( typeof( T ), new Object[] { errorMessage } );
            }
        }
    }
}

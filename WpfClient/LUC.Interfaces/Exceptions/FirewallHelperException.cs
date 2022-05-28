using System;

namespace LUC.Interfaces.Exceptions
{
    public class FirewallHelperException : Exception
    {
        public FirewallHelperException( String message )
          : base( message )
        {
            ;//do nothing
        }
    }
}

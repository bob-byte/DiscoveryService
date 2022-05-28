using System;

namespace Common.Exceptions
{
    public class IntegrationTestException : ApplicationException
    {
        public IntegrationTestException( String message ) : base( message )
        {
        }
    }
}

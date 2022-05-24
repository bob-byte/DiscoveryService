using System;

namespace LUC.Interfaces.Exceptions
{
    public class InconsistencyException : ApplicationException
    {
        public InconsistencyException( String message ) : base( message )
        {
        }
    }
}

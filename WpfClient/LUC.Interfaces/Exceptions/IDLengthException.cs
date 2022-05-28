using System;

namespace LUC.Interfaces.Exceptions
{
    public class IDLengthException : Exception
    {
        public IDLengthException()
               : base()
        {
            ;//do nothing
        }

        public IDLengthException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}

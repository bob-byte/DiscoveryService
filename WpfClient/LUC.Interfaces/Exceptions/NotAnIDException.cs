using System;

namespace LUC.Interfaces.Exceptions
{
    public class NotAnIDException : Exception
    {
        public NotAnIDException()
            : base()
        {
            ;//do nothing
        }

        public NotAnIDException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}

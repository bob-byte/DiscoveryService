using System;

namespace LUC.Interfaces.Models
{
    public class BucketName : IBucketName
    {
        public BucketName( String serverName, String localName )
        {
            ServerName = serverName;
            LocalName = localName;
            IsSuccess = true;
            ErrorMessage = String.Empty;
        }

        public BucketName( String errorMessage )
        {
            ErrorMessage = errorMessage;
            IsSuccess = false;
        }

        public String ServerName { get; }

        public String LocalName { get; }

        public Boolean IsSuccess { get; }

        public String ErrorMessage { get; }

        public override String ToString() => $"{ServerName} => {LocalName}";
    }
}

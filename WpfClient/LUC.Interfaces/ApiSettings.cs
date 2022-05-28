using LUC.Interfaces.Models;

using System;
using System.Configuration;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo( assemblyName: "LUC.UnitTests" )]
namespace LUC.Interfaces
{
    public class ApiSettings : ICloneable
    {
        public ApiSettings()
        {
            Host = AppSettings.RestApiHost;
        }

        public ApiSettings( String accessToken )
            : this()
        {
            AccessToken = accessToken;
        }

        public String Host { get; private set; }

        public String AccessToken { get; private set; }

        public void InitializeAccessToken( String accessToken ) => 
            AccessToken = accessToken;

        public Object Clone() =>
            MemberwiseClone();
    }
}

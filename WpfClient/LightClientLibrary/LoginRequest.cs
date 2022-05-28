using Newtonsoft.Json;

using System;

namespace LightClientLibrary
{
    internal class LoginRequest
    {
        [JsonProperty( "login" )]
        public String Login { get; set; }

        [JsonProperty( "password" )]
        public String Password { get; set; }
    }
}
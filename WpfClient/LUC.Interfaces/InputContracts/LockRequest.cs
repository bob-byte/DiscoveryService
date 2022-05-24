using Newtonsoft.Json;

namespace LUC.Interfaces.InputContracts
{
    public class LockRequest
    {
        [JsonProperty( "op" )]
        public System.String Operation { get; set; }

        [JsonProperty( "prefix" )]
        public System.String Prefix { get; set; }

        [JsonProperty( "objects" )]
        public System.String[] Objects { get; set; }
    }
}

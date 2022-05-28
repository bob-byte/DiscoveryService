using Newtonsoft.Json;

using System.Collections.Generic;

namespace LUC.Interfaces.InputContracts
{
    public class DeleteRequest
    {
        [JsonProperty( "object_keys" )]
        public List<System.String> ObjectKeys { get; set; }

        [JsonProperty( "prefix" )]
        public System.String Prefix { get; set; }
    }
}

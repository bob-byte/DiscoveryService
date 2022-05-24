using Newtonsoft.Json;

namespace LUC.Interfaces.InputContracts
{
    public class RenameRequest
    {
        [JsonProperty( "prefix" )]
        public System.String Prefix { get; set; }

        [JsonProperty( "dst_object_name" )]
        public System.String DestinationObjectName { get; set; }

        [JsonProperty( "src_object_key" )]
        public System.String SourceObjectKey { get; set; }
    }
}

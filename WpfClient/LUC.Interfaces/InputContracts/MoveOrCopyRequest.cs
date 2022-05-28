using Newtonsoft.Json;

using System.Collections.Generic;

namespace LUC.Interfaces.InputContracts
{
    public class MoveOrCopyRequest
    {
        [JsonProperty( "dst_bucket_id" )]
        public System.String DestinationBucketId { get; set; }

        [JsonProperty( "dst_prefix" )]
        public System.String DestinationPrefix { get; set; }

        [JsonProperty( "src_object_keys" )] // TODO Release 2.0. Rename on server. Source keys and destination names
        public Dictionary<System.String, System.String> SourceObjectKeys { get; set; }

        [JsonProperty( "src_prefix" )]
        public System.String SourcePrefix { get; set; }
    }
}

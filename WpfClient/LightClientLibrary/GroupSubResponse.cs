using Newtonsoft.Json;

using System;

namespace LightClientLibrary
{
    public class GroupSubResponse
    {
        [JsonProperty( "id" )]
        public String Id { get; set; }

        [JsonProperty( "name" )]
        public String Name { get; set; }

        [JsonProperty( "bucket_id" )]
        public String BucketId { get; set; }
    }
}
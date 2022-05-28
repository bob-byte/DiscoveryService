using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class ObjectFileDescriptionSubResponse
    {
        [JsonProperty( "guid" )]
        public System.String Guid { get; set; }

        [JsonProperty( "version" )]
        public System.String Version { get; set; }

        [JsonProperty( "object_key" )]
        public System.String ObjectKey { get; set; }

        [JsonProperty( "orig_name" )]
        public System.String OriginalName { get; set; }

        [JsonProperty( "md5" )]
        public System.String Md5 { get; set; }

        [JsonProperty( "is_deleted" )]
        public System.Boolean IsDeleted { get; set; }

        [JsonProperty( "last_modified_utc" )]
        public System.Int64 LastModifiedUtc { get; set; }

        [JsonProperty( "bytes" )]
        public System.Int64 Bytes { get; set; }

        [JsonProperty( "is_locked" )]
        public System.Boolean IsLocked { get; set; }

        [JsonProperty( "lock_user_id" )]
        public System.String LockUserId { get; set; }

        [JsonProperty( "lock_user_name" )]
        public System.String LockUserName { get; set; }

        [JsonProperty( "lock_user_tel" )]
        public System.String LockUserTel { get; set; }

        [JsonProperty( "lock_modified_utc" )]
        public System.Int64? LockModifiedUtc { get; set; }
    }
}

using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class LockedUploadResponse
    {
        [JsonProperty( "lock_user_id" )]
        public System.String LockUserId { get; set; }

        [JsonProperty( "lock_user_name" )]
        public System.String LockUserName { get; set; }

        [JsonProperty( "lock_user_tel" )]
        public System.String LockUserTel { get; set; }

        [JsonProperty( "lock_modified_utc" )]
        public System.Int64 LockModifiedUtc { get; set; }
    }
}

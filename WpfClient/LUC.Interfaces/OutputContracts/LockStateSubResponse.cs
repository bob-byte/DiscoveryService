using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class LockStateSubResponse : LockedUploadResponse
    {
        [JsonProperty( "object_key" )]
        public System.String ObjectKey { get; set; }

        [JsonProperty( "is_locked" )]
        public System.Boolean IsLocked { get; set; }
    }
}

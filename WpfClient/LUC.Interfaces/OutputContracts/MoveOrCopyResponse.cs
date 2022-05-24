using Newtonsoft.Json;

using System;

namespace LUC.Interfaces.OutputContracts
{
    public class MoveOrCopyResponse : BaseResponse
    {

        public MoveOrCopyResponse() : base()
        {
        }

        public MoveOrCopyResponse( Boolean isSuccess, Boolean isForbidden, String message ) : base( isSuccess, isForbidden, message )
        {
        }

        [JsonProperty( "src_prefix" )]
        public String SourcePrefix { get; set; }

        [JsonProperty( "dst_prefix" )]
        public String DestinationPrefix { get; set; }

        [JsonProperty( "old_key" )]
        public String OldKey { get; set; }

        [JsonProperty( "new_key" )]
        public String NewKey { get; set; }

        [JsonProperty( "src_orig_name" )]
        public String SourceOriginalName { get; set; }

        [JsonProperty( "dst_orig_name" )]
        public String DestinationOriginalName { get; set; }

        [JsonProperty( "bytes" )]
        public Int32 Bytes { get; set; }

        [JsonProperty( "renamed" )]
        public Boolean IsRenamed { get; set; }

        [JsonProperty( "src_locked" )]
        public Boolean Src_locked { get; set; }

        [JsonProperty( "src_lock_user_id" )]
        public String Src_lock_user_id { get; set; }

        [JsonProperty( "guid" )]
        public String Guid { get; set; }
    }
}

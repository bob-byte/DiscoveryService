using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LightClientLibrary
{
    public class FileUploadResponse : BaseResponse
    {
        [JsonProperty( "guid" )]
        public String Guid { get; set; }

        [JsonProperty( "orig_name" )]
        public String OriginalName { get; set; }

        [JsonProperty( "version" )]
        public String Version { get; set; }

        [JsonProperty( "object_key" )]
        public String ObjectKey { get; set; }

        [JsonProperty( "upload_id" )]
        public String UploadId { get; set; }

        [JsonProperty( "end_byte" )]
        public String EndByte { get; set; }

        [JsonProperty( "md5" )]
        public String Md5 { get; set; }

        [JsonProperty( "upload_time" )]
        public Int64 UploadTime { get; set; }

        [JsonProperty( "author_id" )]
        public String UserId { get; set; }

        [JsonProperty( "author_name" )]
        public String UserName { get; set; }

        [JsonProperty( "author_tel" )]
        public String UserTel { get; set; }

        [JsonProperty( "is_locked" )]
        public Boolean IsLocked { get; set; }

        [JsonProperty( "lock_modified_utc" )]
        public String LockModifiedUtc { get; set; }

        [JsonProperty( "lock_user_id" )]
        public String LockUserId { get; set; }

        [JsonProperty( "lock_user_name" )]
        public String LockUserName { get; set; }

        [JsonProperty( "lock_user_tel" )]
        public String LockUserTel { get; set; }

        [JsonProperty( "is_deleted" )]
        public Boolean IsDeleted { get; set; }

        [JsonProperty( "bytes" )]
        public Int64 Bytes { get; set; }

        [JsonProperty( "width" )]
        public String Width { get; set; }

        [JsonProperty( "height" )]
        public String Height { get; set; }

    }
}
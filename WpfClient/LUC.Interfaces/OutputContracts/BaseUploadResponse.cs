using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class BaseUploadResponse
    {
        [JsonProperty( "guid" )]
        public System.String Guid { get; set; }

        [JsonProperty( "end_byte" )]
        public System.String EndByte { get; set; }

        [JsonProperty( "upload_id" )]
        public System.String UploadId { get; set; }

        //[JsonProperty("version")]
        //public string Version { get; set; }

        [JsonProperty( "md5" )]
        public System.String Md5 { get; set; }
    }
}

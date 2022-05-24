using Newtonsoft.Json;

using System;

namespace LUC.Interfaces.OutputContracts
{
    public class FileUploadResponse : BaseResponse
    {
        public FileUploadResponse() : base()
        {
        }

        [JsonProperty( propertyName: "orig_name" )]
        public String OriginalName { get; set; }

        public Int64 UploadTime { get; set; }

        [JsonProperty( "guid" )]
        public String Guid { get; set; }

        [JsonProperty( "version" )]
        public String FileVersion { get; set; }

        public override String ToString()
        {
            String responseStr = $"{nameof( FileUploadResponse )}:\n" +
                $"{nameof( OriginalName )} = {OriginalName};\n" +
                $"{nameof( UploadTime )} = {UploadTime};\n" +
                $"{nameof( Guid )} = {Guid}";

            return responseStr;
        }
    }
}

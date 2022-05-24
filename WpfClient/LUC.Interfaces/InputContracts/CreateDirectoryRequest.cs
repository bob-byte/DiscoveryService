using Newtonsoft.Json;

namespace LUC.Interfaces.InputContracts
{
    public class CreateDirectoryRequest
    {
        [JsonProperty( "directory_name" )]
        public System.String DirectoryName { get; set; }

        [JsonProperty( "prefix" )]
        public System.String Prefix { get; set; }
    }
}

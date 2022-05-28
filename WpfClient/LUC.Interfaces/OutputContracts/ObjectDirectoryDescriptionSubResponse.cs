using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class ObjectDirectoryDescriptionSubResponse
    {
        [JsonProperty( "prefix" )]
        public System.String HexPrefix { get; set; }

        [JsonProperty( "is_deleted" )]
        public System.Boolean IsDeleted { get; set; }
    }
}

using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class GroupSubResponse
    {
        [JsonProperty( "id" )]
        public System.String Id { get; set; }

        [JsonProperty( "name" )]
        public System.String Name { get; set; }
    }
}

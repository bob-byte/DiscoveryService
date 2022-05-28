using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class CopySubResponse : MoveOrCopyResponse
    {
        [JsonProperty( "renamed" )]
        public System.Boolean Renamed { get; set; }
    }
}

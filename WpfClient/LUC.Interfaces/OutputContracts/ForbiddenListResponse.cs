using Newtonsoft.Json;

using System.Collections.Generic;

namespace LUC.Interfaces.OutputContracts
{
    public class ForbiddenListResponse : BaseResponse
    {
        public ForbiddenListResponse()
        {
            Groups = new List<GroupSubResponse>();
        }

        [JsonProperty( "error" )]
        public System.Int32 Error { get; set; }

        [JsonProperty( "groups" )]
        public List<GroupSubResponse> Groups { get; set; }

        public System.String RequestedPrefix { get; set; }
    }
}

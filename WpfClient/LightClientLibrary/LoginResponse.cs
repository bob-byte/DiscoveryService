using Newtonsoft.Json;

using System;
using System.Collections.Generic;

namespace LightClientLibrary
{
    public class LoginResponse : BaseResponse
    {
        [JsonProperty( "id" )]
        public String Id { get; set; }

        [JsonProperty( "token" )]
        public String Token { get; set; }

        [JsonProperty( "tenant_Id" )]
        public String TenantId { get; set; }

        [JsonProperty( "login" )]
        public String Login { get; set; }

        [JsonProperty( "staff" )]
        public Boolean IsAdmin { get; set; }

        [JsonProperty( "groups" )]
        public List<GroupSubResponse> Groups { get; set; }
    }
}
using Newtonsoft.Json;

namespace LUC.Interfaces.OutputContracts
{
    public class RenameResponse : BaseResponse
    {
        public RenameResponse() : base()
        {
        }

        public RenameResponse( System.Boolean isSuccess, System.Boolean isForbidden, System.String message ) : base( isSuccess, isForbidden, message )
        {
        }

        [JsonProperty( "orig_name" )]
        public System.String OriginalName { get; set; }
    }
}

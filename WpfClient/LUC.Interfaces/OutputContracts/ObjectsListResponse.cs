using Newtonsoft.Json;

using System.Collections.Generic;

namespace LUC.Interfaces.OutputContracts
{
    public class ObjectsListResponse : BaseResponse
    {
        public ObjectsListResponse( System.Boolean isSuccess, System.Boolean isForbidden, System.String message ) : base( isSuccess, isForbidden, message )
        {
            ObjectFileDescriptions = new ObjectFileDescriptionSubResponse[ 0 ];
            Directories = new ObjectDirectoryDescriptionSubResponse[ 0 ];
            Groups = new List<GroupSubResponse>();
        }

        public ObjectsListResponse() : base()
        {
            ObjectFileDescriptions = new ObjectFileDescriptionSubResponse[ 0 ];
            Directories = new ObjectDirectoryDescriptionSubResponse[ 0 ];
            Groups = new List<GroupSubResponse>();
        }

        [JsonProperty( "list" )]
        public ObjectFileDescriptionSubResponse[] ObjectFileDescriptions { get; set; }

        [JsonProperty( "dirs" )]
        public ObjectDirectoryDescriptionSubResponse[] Directories { get; set; }

        [JsonProperty( "server_utc" )]
        public System.Int64 ServerUtc { get; set; }

        [JsonProperty( "groups" )]
        public List<GroupSubResponse> Groups { get; set; }

        public System.String RequestedPrefix { get; set; }
    }
}

using LUC.Interfaces.Models;

using System;
using System.Collections.Generic;

namespace LUC.Interfaces
{
    public interface ICurrentUserProvider
    {
        event EventHandler<RootFolderPathChangedEventArgs> RootFolderPathChanged;

        String RootFolderPath { get; set; }

        Boolean IsLoggedIn { get; }

        LoginServiceModel LoggedUser { get; set; }

        void UpdateLoggedUserGroups( List<GroupServiceModel> groups );

        IBucketName TryExtractBucket( String fileFullPath );

        String ExtractPrefix( String fileFullPath );

        IBucketName GetBucketNameByDirectoryPath( String directoryPath );

        IList<String> ProvideBucketDirectoryPaths();

        String LocalBucketPath( String serverBucketName );

        IList<String> GetServerBuckets();

        String LocalBucketIdentifier( String serverBucketName );
    }
}

using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;
using LUC.Services.Implementation.BusinessLogics;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;

namespace LUC.Services.Implementation
{
    [Export( typeof( ICurrentUserProvider ) )]
    public class CurrentUserProvider : ICurrentUserProvider
    {
        private const String PATH_SEPARATOR = PathExtensions.PATH_SEPARATOR;

        public event EventHandler<RootFolderPathChangedEventArgs> RootFolderPathChanged;

        private String m_rootFolderPath;
        private LoginServiceModel m_loggedUser;

        private static readonly Object s_lockUsingLoggedUser = new Object();

        [Import( typeof( ILoggingService ) )]
        public ILoggingService LoggingService { get; set; }

        [Import( typeof( IPathFiltrator ) )]
        public IPathFiltrator PathFiltrator { get; set; }

        public Boolean IsLoggedIn => ( LoggedUser != null ) && !LoggedUser.Id.Equals( String.Empty, StringComparison.Ordinal );

        public LoginServiceModel LoggedUser
        {
            get => m_loggedUser;
            set
            {
                if ( value != null )
                {
                    lock ( s_lockUsingLoggedUser )
                    {
                        m_loggedUser = value;
                        _ = TryCreateLocalBuckets();
                    }
                }
                else
                {
                    Logout();
                }
            }
        }

        public String RootFolderPath
        {
            get => m_rootFolderPath;
            set
            {
                if ( m_rootFolderPath == null || !m_rootFolderPath.Equals( value, StringComparison.OrdinalIgnoreCase ) )
                {
                    String oldRootFolder = (String)m_rootFolderPath?.Clone();

                    //thread-safe set value
                    Interlocked.Exchange( ref m_rootFolderPath, value );

                    _ = TryCreateLocalBuckets();

                    RootFolderPathChanged?.Invoke( this, new RootFolderPathChangedEventArgs( oldRootFolder, m_rootFolderPath ) );
                }
            }
        }

        public void Logout()
        {
            lock ( s_lockUsingLoggedUser )
            {
                //empty model
                m_loggedUser = new LoginServiceModel();
            }
        }

        public IBucketName TryExtractBucket( String fileFullPath )
        {
            if ( String.IsNullOrEmpty( fileFullPath ) )
            {
                throw new ArgumentNullException( nameof( fileFullPath ) );
            }


            String localBucketName;

            String afterRootPath = fileFullPath.Substring( RootFolderPath.Length + 1 );
            if ( afterRootPath.StartsWith( PATH_SEPARATOR ) )
            {
                afterRootPath = afterRootPath.TrimStart( PATH_SEPARATOR.ToCharArray() );
            }

            Int32 dashIndex = afterRootPath.IndexOf( PATH_SEPARATOR );

            localBucketName = dashIndex == -1 ? afterRootPath : afterRootPath.Substring( 0, dashIndex );

            IBucketName result;
            lock ( s_lockUsingLoggedUser )
            {
                GroupServiceModel group = m_loggedUser.Groups.SingleOrDefault( x => String.Equals( x.Name, localBucketName, StringComparison.InvariantCultureIgnoreCase ) );

                if ( group == null )
                {
                    String currentGroups = String.Join( ";", m_loggedUser.Groups.Select( g => "id=" + g.Id + "name=" + g.Name ) );
                    return new BucketName( $"No group by name '{localBucketName}'. Local file path is '{fileFullPath}'. Current groups: {currentGroups}" );
                }

                result = new BucketName( BusinessLogic.GenerateBucketName( m_loggedUser.TenantId, group.Id ), localBucketName );
            }

            return result;
        }

        // TODO 1.0 Unit tests
        public IBucketName GetBucketNameByDirectoryPath( String directoryPath )
        {
            String directoryPathWithoutSeparatorInEnd = directoryPath;
            Int32 dashIndex = directoryPath.LastIndexOf( '\\' );

            if ( directoryPath.EndsWith( PathExtensions.PATH_SEPARATOR ) )
            {
                directoryPathWithoutSeparatorInEnd = directoryPath.Remove( dashIndex );
            }

            dashIndex = directoryPathWithoutSeparatorInEnd.LastIndexOf( '\\' );

            String localBucketName = directoryPathWithoutSeparatorInEnd.Substring( dashIndex + 1, directoryPathWithoutSeparatorInEnd.Length - dashIndex - 1 );
            String serverBucketName;

            lock ( s_lockUsingLoggedUser )
            {
                GroupServiceModel appropriateGroup = m_loggedUser.Groups.SingleOrDefault( x => x.Name.ToLowerInvariant() == localBucketName.ToLowerInvariant() );

                if ( appropriateGroup == null )
                {
                    return new BucketName( $"Can't get group for local bucket {localBucketName}." );
                }

                serverBucketName = BusinessLogic.GenerateBucketName( m_loggedUser.TenantId, appropriateGroup.Id );
            }

            return new BucketName( serverBucketName, localBucketName );
        }

        public String LocalBucketPath( String serverBucketName )
        {
            if ( !String.IsNullOrWhiteSpace( serverBucketName ) )
            {
                String localBucketId = LocalBucketIdentifier( serverBucketName );
                String localBucketName = m_loggedUser.Groups.Single( g => g.Id.Equals( localBucketId, StringComparison.Ordinal ) ).Name;

                String currentBucketDirectoryPath = Path.Combine( RootFolderPath, localBucketName );
                return currentBucketDirectoryPath;
            }
            else
            {
                throw new ArgumentException( $"{nameof( serverBucketName )} is null or white space" );
            }
        }

        public IList<String> GetServerBuckets()
        {
            var result = new List<String>();

            lock ( s_lockUsingLoggedUser )
            {
                foreach ( GroupServiceModel group in m_loggedUser.Groups )
                {
                    result.Add( BusinessLogic.GenerateBucketName( m_loggedUser.TenantId, group.Id ) );
                }
            }

            return result;
        }

        public String LocalBucketIdentifier( String serverBucketName )
        {
            if ( serverBucketName != null )
            {
                String[] partsOfServerBucketName = serverBucketName.Split( BusinessLogic.SEPARATOR_IN_SERVER_BUCKET_NAME );
                if ( partsOfServerBucketName.Length == BusinessLogic.PART_COUNT_IN_SERVER_BUCKET_NAME )
                {
                    String localBucketName = partsOfServerBucketName[ BusinessLogic.INDEX_OF_PART_OF_GROUP_ID_IN_SERVER_BUCKET_NAME ];

                    Boolean isSupportedLocalBucketName = LoggedUser.Groups.Select( c => c.Id ).
                        Any( c => c.Equals( localBucketName, StringComparison.OrdinalIgnoreCase ) );
                    return isSupportedLocalBucketName ? localBucketName : throw new ArgumentException( "Name of local bucket is not supported" );
                }
                else
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                throw new ArgumentNullException( "serverBucketName", "serverBucketName is null" );
            }
        }

        public String ExtractPrefix( String fileFullPath )
        {
            IBucketName bucket = TryExtractBucket( fileFullPath );

            if ( bucket.IsSuccess )
            {
                String bucketName = bucket.LocalName;
                String afterRootPath = fileFullPath.Substring( RootFolderPath.Length + 1 );
                String relativeDirectoryPath = Path.GetDirectoryName( afterRootPath );

                if ( bucketName == relativeDirectoryPath || bucketName == afterRootPath )
                {
                    return String.Empty;
                }
                else
                {
                    String prefix = relativeDirectoryPath.Substring( bucketName.Length + 1 );
                    String result = "";
                    if ( prefix.Length > 0 )
                    {
                        if ( prefix.EndsWith( "/" ) )
                        {
                            result += prefix.Remove( prefix.Length - 1 );
                        }
                        else
                        {
                            result += prefix;
                        }
                    }

                    result = result.ToHexPrefix();
                    return result;
                }
            }
            else
            {
                throw new ArgumentException( bucket.ErrorMessage, paramName: nameof( fileFullPath ) );
            }
        }

        public void UpdateLoggedUserGroups( List<GroupServiceModel> groups )
        {
            if ( LoggedUser.Id.Equals( String.Empty, StringComparison.Ordinal ) )
            {
                throw new ArgumentNullException( String.Empty, "Need autorization, LoggedUser is null" );
            }

            LoggedUser.Groups = groups;

            _ = TryCreateLocalBuckets();
        }

        private Boolean TryCreateLocalBuckets()
        {
            if ( String.IsNullOrEmpty( RootFolderPath ) )
            {
                return false;
            }

            if ( LoggedUser == null || LoggedUser.Id.Equals( String.Empty, StringComparison.Ordinal ) )
            {
                return false;
            }

            IList<String> bucketDirectoryPathes = ProvideBucketDirectoryPaths();

            var existedBuckets = new List<String>();

            foreach ( String bucket in bucketDirectoryPathes )
            {
                if ( Directory.Exists( bucket ) )
                {
                    existedBuckets.Add( bucket );
                }
                else
                {
                    try
                    {
                        _ = Directory.CreateDirectory( bucket );
                        existedBuckets.Add( bucket );
                    }
                    catch ( UnauthorizedAccessException )
                    {
                        MessageBoxHelper.ShowMessageBox( String.Format( Strings.MessageTemplate_CantCreateBucket, bucket ), Strings.Label_Attention );
                        return false;
                    }

                    LoggingService.LogInfoWithLongTime( $"Bucket '{bucket}' was created." );
                }
            }

            if ( PathFiltrator != null )
            {
                PathFiltrator.UpdateCurrentBuckets( existedBuckets );
            }

            return true;
        }

        // TODO 1.0 dublicated.
        public IList<String> ProvideBucketDirectoryPaths() => LoggedUser.Groups.Select( @group => Path.Combine( RootFolderPath, @group.Name ) ).ToList();
    }
}

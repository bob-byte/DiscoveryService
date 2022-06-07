using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.Interfaces
{
    public class ObjectNameProvider
    {
        #region Fields

        private readonly Int32 m_secondsToCacheServerList;
        private readonly Dictionary<String, ObjectsListModel> m_objectsListCache = new Dictionary<String, ObjectsListModel>();

        private DateTime m_cacheInitTime = DateTime.UtcNow;

        #endregion

        #region Constructors

        public ObjectNameProvider( ICurrentUserProvider currentUserProvider, ILoggingService loggingService, IApiClient apiClient )
        {
            m_secondsToCacheServerList = AppSettings.SecondsToCacheServerList;

            CurrentUserProvider = currentUserProvider;
            LoggingService = loggingService;
            ApiClient = apiClient;
        }

        #endregion

        #region Properties

        public ICurrentUserProvider CurrentUserProvider { get; }

        private IApiClient ApiClient { get; }

        public ILoggingService LoggingService { get; }

        #endregion

        public async Task<ServerObjectDescription> GetExistingObjectDescription( String currentObjectFullPath )
        {
            DateTime now = DateTime.UtcNow;

            if ( ( now - m_cacheInitTime ).TotalSeconds > m_secondsToCacheServerList )
            {
                m_cacheInitTime = now;
                m_objectsListCache.Clear();
            }

            IBucketName bucket = CurrentUserProvider.TryExtractBucket( currentObjectFullPath );

            if ( !bucket.IsSuccess )
            {
                return new ServerObjectDescription
                {
                    IsSuccess = false,
                    Message = bucket.ErrorMessage
                };
            }

            String localPrefix = CurrentUserProvider.ExtractPrefix( currentObjectFullPath );
            String localObjectName = Path.GetFileName( currentObjectFullPath );

            String cacheKey = bucket.ServerName + "/" + localPrefix;

            if ( m_objectsListCache.ContainsKey( cacheKey ) )
            {
                //strange rows
                var notExistsResult = new ServerObjectDescription
                {
                    IsSuccess = false,
                    Message = $"Object '{localObjectName}' does not exist on server yet or already."
                };

                if ( m_objectsListCache[ cacheKey ].ObjectDescriptions.Count( x => x.OriginalName == localObjectName ) > 1 )
                {
                    LoggingService.LogFatal( $"List has several records with '{localObjectName}' OriginalName" );
                    return notExistsResult;
                }

                ObjectDescriptionModel possibleFileObject = m_objectsListCache[ cacheKey ].ObjectDescriptions.SingleOrDefault( x => x.OriginalName == localObjectName );

                if ( possibleFileObject == null )
                {
                    String directoryPrefixAndName = localPrefix + localObjectName.ToHexPrefix();

                    if ( m_objectsListCache[ cacheKey ].DirectoryDescriptions.Count( x => x.Prefix.FromHexString().ToHexPrefix() == directoryPrefixAndName ) > 1 )
                    {
                        LoggingService.LogFatal( $"List has several records with '{directoryPrefixAndName}' HexPrefix.FromHexString().ToHexPrefix()" );
                        return notExistsResult;
                    }

                    DirectoryDescriptionModel possibleDirectory = m_objectsListCache[ cacheKey ].DirectoryDescriptions.SingleOrDefault( x => x.Prefix.FromHexString().ToHexPrefix() == directoryPrefixAndName );

                    if ( possibleDirectory == null )
                    {
                        _ = m_objectsListCache.Remove( cacheKey );
                        return await LoadExistingObjectNameFromServerAndAddToCache( bucket.ServerName, localPrefix, cacheKey, localObjectName );
                    }
                    else
                    {
                        return new ServerObjectDescription
                        {
                            IsSuccess = true,
                            ObjectPrefix = possibleDirectory.Prefix,
                            ObjectKey = possibleDirectory.Prefix.LastHexPrefixPart() + '/' // TODO Dublicated.
                        };
                    }
                }
                else
                {
                    return new ServerObjectDescription
                    {
                        Guid = possibleFileObject.Guid,
                        IsSuccess = true,
                        ObjectPrefix = m_objectsListCache[ cacheKey ].RequestedPrefix,
                        ObjectKey = possibleFileObject.ObjectKey,
                        LastModifiedDateTimeUtc = possibleFileObject.LastModifiedDateTimeUtc,
                        Version = possibleFileObject.Version,
                        ByteCount = possibleFileObject.ByteCount
                    };
                }
            }
            else
            {
                return await LoadExistingObjectNameFromServerAndAddToCache( bucket.ServerName, localPrefix, cacheKey, localObjectName );
            }
        }

        public async Task<String> ServerPrefix( String fullObjectPath )
        {
            String prefix = CurrentUserProvider.ExtractPrefix( fullObjectPath );

            if ( prefix.EndsWith( "/" ) )
            {
                prefix = prefix.Remove( prefix.Length - 1 );
            }

            // NOTE below uncommented code, if don't work then return Task.FromResult(prefix);
            if ( prefix == String.Empty )
            {
                return String.Empty;
            }

            DirectoryInfo parent = Directory.GetParent( fullObjectPath );
            String parentDirectory = parent?.FullName;

            IBucketName bucket = CurrentUserProvider.TryExtractBucket( parentDirectory );

            if ( !bucket.IsSuccess )
            {
                return String.Empty;
            }

            String localPrefix = CurrentUserProvider.ExtractPrefix( parentDirectory );

            ObjectsListResponse list = await FindListByPrefix( bucket.ServerName, localPrefix, true );

            var listModel = list.ToObjectsListModel();

            String localObjectName = Path.GetFileName( parentDirectory );
            ServerObjectDescription parentDirectoryDescription = FindExistingObjectDescription( listModel, localPrefix, localObjectName );

            String result = parentDirectoryDescription.ObjectPrefix;

            return result;
        }

        private async Task<ObjectsListResponse> FindListByPrefix( String bucketName, String localPrefix, Boolean isShowDeleted = false )
        {
            // Add '/' to each splitting part
            String[] prefixParts = localPrefix.Split( '/' );

            if ( localPrefix == "/" )
            {
                prefixParts = Array.Empty<String>();
            }
            else
            {
                if ( prefixParts.Last() == String.Empty )
                {
                    prefixParts = prefixParts.Take( prefixParts.Length - 1 ).ToArray();
                }

                prefixParts = Array.ConvertAll( prefixParts, x => x + '/' );
            }

            ObjectsListResponse lastList = await ApiClient.ListWithCancelDownloadAsync( bucketName, "", isShowDeleted );

            var currentPrefix = new StringBuilder();

            foreach ( String part in prefixParts )
            {
                currentPrefix.Append( part );

                if ( lastList.Directories.Count( x => x.HexPrefix.FromHexString().ToHexPrefix() == currentPrefix.ToString() ) > 1 )
                {
                    LoggingService.LogFatal( $"List (directories) have several records with '{currentPrefix}' HexPrefix" );
                    continue;
                }

                ObjectDirectoryDescriptionSubResponse directoryDescription = lastList.Directories.SingleOrDefault( x => x.HexPrefix.FromHexString().ToHexPrefix() == currentPrefix.ToString() );

                if ( directoryDescription == null )
                {
                    //TODO Release 2.0: cancel download in directoryDescription

                    String error = $"No list for prefix '{currentPrefix}' = '{currentPrefix.ToString().FromHexString()}'";

                    LoggingService.LogError( error );

                    return new ObjectsListResponse
                    {
                        IsSuccess = false,
                        Message = error
                    };
                }

                String serverPrefix = directoryDescription.HexPrefix;

                //TODO: maybe here should give isShowDeleted
                lastList = await ApiClient.ListWithCancelDownloadAsync( bucketName, serverPrefix );

                if ( !lastList.IsSuccess )
                {
                    return lastList;
                }
            }

            return lastList;
        }

        private ServerObjectDescription FindExistingObjectDescription( ObjectsListModel listModel, String localPrefix, String localObjectName )
        {
            var notExistsResult = new ServerObjectDescription
            {
                IsSuccess = false,
                Message = $"Object '{localObjectName}' does not exist on server yet or already."
            };

            if ( listModel.ObjectDescriptions.Count( x => x.OriginalName == localObjectName ) > 1 )
            {
                LoggingService.LogFatal( $"List has several records with '{localObjectName}' OriginalName" );
                return notExistsResult;
            }

            ObjectDescriptionModel possibleDescription = listModel.ObjectDescriptions.SingleOrDefault( x => x.IsDeleted is false && x.OriginalName == localObjectName );

            if ( possibleDescription == null )
            {
                String directoryHexPrefixPlusName = localPrefix + localObjectName.ToHexPrefix();

                if ( listModel.DirectoryDescriptions.Count( x => x.Prefix.FromHexString().ToHexPrefix() == directoryHexPrefixPlusName ) > 1 )
                {
                    LoggingService.LogFatal( $"List has several records with '{directoryHexPrefixPlusName}' HexPrefix.FromHexString().ToHexPrefix()" );
                    return notExistsResult;
                }

                DirectoryDescriptionModel possibleDirectory = listModel.DirectoryDescriptions.SingleOrDefault( x => x.IsDeleted is false && x.Prefix.FromHexString().ToHexPrefix() == directoryHexPrefixPlusName );

                switch ( possibleDirectory )
                {
                    case null:
                        return notExistsResult;
                    default:
                        return new ServerObjectDescription
                        {
                            IsSuccess = true,
                            ObjectPrefix = possibleDirectory.Prefix,
                            ObjectKey = possibleDirectory.Prefix.LastHexPrefixPart() + '/'
                        };
                }
            }
            else
            {
                return new ServerObjectDescription
                {
                    Guid = possibleDescription.Guid,
                    IsSuccess = true,
                    ObjectPrefix = listModel.RequestedPrefix,
                    ObjectKey = possibleDescription.ObjectKey,
                    LastModifiedDateTimeUtc = possibleDescription.LastModifiedDateTimeUtc,
                    Version = possibleDescription.Version,
                    ByteCount = possibleDescription.ByteCount
                };
            }
        }

        private async Task<ServerObjectDescription> LoadExistingObjectNameFromServerAndAddToCache( String bucketName, String localPrefix, String cacheKey, String localObjectName )
        {
            ObjectsListResponse listResponse = await FindListByPrefix( bucketName, localPrefix, true );

            if ( listResponse.IsSuccess )
            {
                var listModel = listResponse.ToObjectsListModel();

                // Cache may be old. Clear value by old cache key.
                if ( m_objectsListCache.ContainsKey( cacheKey ) )
                {
                    _ = m_objectsListCache.Remove( cacheKey );
                }

                m_objectsListCache.Add( cacheKey, listModel );

                ServerObjectDescription result = FindExistingObjectDescription( listModel, localPrefix, localObjectName );

                return result;
            }
            else
            {
                return new ServerObjectDescription
                {
                    IsSuccess = false,
                    Message = listResponse.Message
                };
            }
        }
    }
}

using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.InputContracts;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;
using LUC.ViewModels;

using Newtonsoft.Json;

using Nito.AsyncEx.Synchronous;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace LUC.ApiClient
{
    internal class FileOperation : SyncServicesProvider
    {
        private readonly Object m_lockAddNewUploadingFile;
        private readonly IApiClient m_apiClient;

        public FileOperation( ApiClient apiClient )
            : base( apiClient, apiClient.ObjectNameProvider )
        {
            m_lockAddNewUploadingFile = new Object();
            m_apiClient = apiClient;
        }

        // TODO: don't ignore zero length files.
        internal void CheckFileAndAddToUploadingFiles( FileInfo fileInfo )
        {
            if ( ( fileInfo != null ) && ( fileInfo.Length > 0 ) )
            {
                ObjectStateType objectStateType = PathExtensions.GetObjectState( fileInfo.FullName );
                if ( objectStateType == ObjectStateType.Ok )
                {
                    lock ( m_lockAddNewUploadingFile )
                    {
                        if ( !SyncingObjectsList.IsUploadingNow( fileInfo.FullName ) )
                        {
                            SyncingObjectsList.AddUploadingFile( fileInfo.FullName );
                        }
                        else
                        {
                            throw new InvalidOperationException( message: $"{fileInfo.Name} is uploading now" );
                        }
                    }
                }
                else
                {
                    throw new ArgumentException( $"{nameof( ObjectStateType )} is {objectStateType} of file {fileInfo.FullName}", nameof( fileInfo ) );
                }
            }
            else
            {
                throw new ArgumentException( $"Is null or file length is zero. {fileInfo?.FullName}", nameof( fileInfo ) );
            }
        }

        public async Task<ObjectsListResponse> ListWithCancelDownloadAsync( String bucketName, String prefix = "", Boolean showDeleted = false )
        {
            ObjectsListResponse objectsListResponse = await ListAsync( bucketName, prefix, showDeleted );
            SyncingObjectsList.TryCancelAllDownloadingFilesWithDifferentVersions( objectsListResponse, bucketName, isCancelledAnyDownload: out _ );

            return objectsListResponse;
        }

        public async Task<ObjectsListResponse> ListAsync( String bucketName, String prefix = "", Boolean showDeleted = false )
        {
            String requestUri = BuildUriExtensions.GetListUri( m_apiSettings.Host, bucketName, prefix );

            if ( requestUri.EndsWith( "/" ) && prefix != String.Empty )
            {
                requestUri = requestUri.Remove( requestUri.Length - 1, 1 );
            }

            if ( showDeleted )
            {
                requestUri = prefix == ""
                    ? requestUri + "?" + GeneralConstants.SHOW_DELETED_URI
                    : requestUri + "&" + GeneralConstants.SHOW_DELETED_URI;
            }

            ObjectsListResponse result;

            using ( var client = new RepeatableHttpClient( m_apiSettings.AccessToken ) )
            {
                HttpResponseMessage response = await client.SendRepeatableAsync( requestUri, null, HttpMethod.Get );

                if ( response.IsSuccessStatusCode )
                {
                    result = await UpdateLoggedUserGroups( response );
                }
                else if ( response.StatusCode == HttpStatusCode.Forbidden )
                {
                    return await HandleResponse.HandleResponse403<ObjectsListResponse>( response, $"Status code = '{response.StatusCode}'.", CurrentUserProvider );
                }
                else
                {
                    String str = await response.Content.ReadAsStringAsync();
                    if ( str.Equals( "{\"error\":38}" ) ||
                         str.Equals( "{\"error\":28}" ) ) //Token expired or Incorrect access token
                    {
                        LoggingService.LogError( "Token expired or Incorrect access token" );
                        ISettingsService settings = AppSettings.ExportedValue<ISettingsService>();

                        String login = settings.ReadRememberedLogin();
                        String pass = settings.ReadBase64Password().Base64Decode();

                        if ( login.Length > 0 && pass.Length > 0 )
                        {
                            LoginResponse secondResponse =
                                await m_apiClient.LoginAsync( login, pass ).ConfigureAwait( false );

                            if ( secondResponse.IsSuccess )
                            {
                                return await ListAsync( bucketName, prefix, showDeleted ).ConfigureAwait( false );
                            }
                        }
                        else
                        {
                            WebRestorable.IsTokenExpiredOrIncorrectAccessToken = true;
                        }
                    }

                    result = await HandleResponse.HandleBadListResponse( response, LoggingService );
                }

                result.RequestedPrefix = prefix;
            }

            return result;
        }

        private async Task<ObjectsListResponse> UpdateLoggedUserGroups( HttpResponseMessage response )
        {
            ObjectsListResponse listResponse;

            try
            {
                listResponse = JsonConvert.DeserializeObject<ObjectsListResponse>( await response.Content.ReadAsStringAsync() );
                CurrentUserProvider.UpdateLoggedUserGroups( listResponse.Groups.ToGroupServiceModelList() );
            }
            catch ( Exception ex )
            {
                LoggingService.LogError( ex, ex.Message );

                listResponse = new ObjectsListResponse
                {
                    IsSuccess = false,
                    Message = "Can't read content from the response."
                };
            }

            return listResponse;
        }

        public async Task<HttpResponseMessage> DeleteAsync( DeleteRequest requestBody, String bucketName )
        {
            using ( var client = new RepeatableHttpClient( m_apiSettings.AccessToken ) )
            {
                var content = new StringContent( JsonConvert.SerializeObject( requestBody ) );

                String requestUri = BuildUriExtensions.DeleteObjectUri( m_apiSettings.Host, bucketName );

                HttpResponseMessage response = await client.SendRepeatableAsync( requestUri,
                                                                contentReciever: () => RepeatableHttpClient.CloneHttpContentAsync( content ),
                                                                HttpMethod.Delete );

                return response;
            }
        }

        public Task<DeleteResponse> DeleteAsync( params String[] fullPathes ) // TODO 1.0 Delete from the same prefix by one call.
        {
            // TODO check count of deleted objects. if different - something was not deleted - some is locked.
            if ( fullPathes == null || !fullPathes.Any() )
            {
                throw new ArgumentNullException( String.Empty, $@"Variable {nameof( fullPathes )} is null or empty." );
            }

            IBucketName bucket = CurrentUserProvider.TryExtractBucket( fullPathes[ 0 ] );

            //add method LogInfoAboutDeletedFiles
            LoggingService.LogInfo( $"API Delete request for {fullPathes.Length} items starting from {fullPathes[ 0 ]}" );

            Int32 countToDeleteEachTime = 1000;
            Int32 batchAmount = BatchAmount( fullPathes.Length, countToDeleteEachTime );

            return DeleteFiles( batchAmount, fullPathes, countToDeleteEachTime, bucket );
        }

        private Int32 BatchAmount( Int32 countPaths, Int32 countToDeleteEachTime )
        {
            Int32 batchAmount = countPaths / countToDeleteEachTime;
            if ( countPaths % countToDeleteEachTime != 0 )
            {
                batchAmount++;
            }

            return batchAmount;
        }

        private async Task<DeleteResponse> DeleteFiles( Int32 batchAmount, IEnumerable<String> fullPathes,
            Int32 countToDeleteEachTime, IBucketName bucket )
        {
            if ( !bucket.IsSuccess )
            {
                return new DeleteResponse
                {
                    IsSuccess = false,
                    Message = bucket.ErrorMessage
                };
            }

            Int32 countOfDeletedObjects = 0;

            for ( Int32 i = 0; i < batchAmount; i++ )
            {
                List<DeleteRequest> requestBodyList;
                try
                {
                    requestBodyList = await RequestBodyToDeleteFiles( fullPathes.Skip( i * countToDeleteEachTime ).Take( countToDeleteEachTime ) );
                }
                catch ( ArgumentException )
                {
                    continue;
                }

                if ( !requestBodyList.Any() )
                {
                    return new DeleteResponse
                    {
                        Message = $"Objects for delete is not present."
                    };
                }

                foreach ( DeleteRequest requestBody in requestBodyList )
                {
                    HttpResponseMessage response = await DeleteAsync( requestBody, bucket.ServerName );

                    if ( response.IsSuccessStatusCode )
                    {
                        LoggingService.LogInfo( "API delete is OK for prefix " + requestBody.Prefix );
                        countOfDeletedObjects += requestBody.ObjectKeys.Count;
                    }
                    else if ( response.StatusCode == HttpStatusCode.Forbidden )
                    {
                        return await HandleResponse.HandleResponse403<DeleteResponse>( response, $"Status code = '{response.StatusCode}'.", CurrentUserProvider );
                    }
                    else
                    {
                        String error = await response.Content.ReadAsStringAsync();

                        LoggingService.LogError( "Can't delete: " + error );
                    }
                }
            }

            return new DeleteResponse
            {
                Message = $"{countOfDeletedObjects} objects was deleted."
            };

        }

        //private async Task<Int32> CountOfDeletedObjects(IEnumerable<String> fullPathes, Int32 countToDeleteEachTime, IBucketName bucketName)
        //{
        //    var countOfDeletedObjects = 0;

        //    for (var i = 0; i < countToDeleteEachTime; i++)
        //    {
        //        var requestBodies = await RequestBodyToDeleteFiles(fullPathes.Skip(i * countToDeleteEachTime).Take(countToDeleteEachTime));
        //        foreach (var requestBody in requestBodies)
        //        {
        //            var response = await DeleteAsync(requestBody, bucketName.ServerName);

        //            if (response.IsSuccessStatusCode)
        //            {
        //                Console.WriteLine("API delete is OK");
        //                countOfDeletedObjects += requestBody.ObjectKeys.Count;
        //            }
        //            else if (response.StatusCode == HttpStatusCode.Forbidden)
        //            {
        //                HandleResponse handleResponse;
        //                return await HandleResponse403<DeleteResponse>(response, $"Status code = '{response.StatusCode}'.", CurrentUserProvider);
        //            }
        //            else
        //            {
        //                var error = await response.Content.ReadAsStringAsync();
        //                // "34": "Empty \"object_keys\".",

        //                LoggingService.LogError("Can't delete: " + error);
        //            }
        //        }
        //    }
        //}

        private async Task<List<DeleteRequest>> RequestBodyToDeleteFiles( IEnumerable<String> fullPathes )
        {
            var objectKeys = new Dictionary<String, List<String>>();   //key is prefix, values is a files with this prefix

            foreach ( String path in fullPathes )
            {
                ServerObjectDescription serverDesc = await ObjectNameProvider.GetExistingObjectDescription( path );

                if ( serverDesc.IsSuccess )
                {
                    String prefix = serverDesc.ObjectPrefix;
                    String objectKey = serverDesc.ObjectKey;

                    if ( prefix.EndsWith( objectKey ) )
                    {
                        prefix = prefix.Remove( prefix.Length - objectKey.Length, objectKey.Length );
                    }

                    if ( objectKeys.ContainsKey( prefix ) )
                    {
                        objectKeys[ prefix ].Add( objectKey );
                    }
                    else
                    {
                        objectKeys.Add( prefix, new List<String> { objectKey } );
                    }
                }
                else
                {
                    LoggingService.LogError( $"Object {path} does not exist on server or you do not have access to it." );
                }
            }

            if ( objectKeys.Count == 0 )
            {
                return new List<DeleteRequest>();
            }

            var requestBody = new List<DeleteRequest>();
            foreach ( KeyValuePair<String, List<String>> objectKey in objectKeys )
            {
                var request = new DeleteRequest
                {
                    Prefix = objectKey.Key,
                    ObjectKeys = objectKey.Value
                };
                requestBody.Add( request );
            }

            return requestBody;
        }
    }
}

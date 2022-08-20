using LightClientLibrary;

using LUC.DiscoveryServices;
using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.InputContracts;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Serilog;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BaseResponse = LUC.Interfaces.OutputContracts.BaseResponse;
using FileUploadResponse = LUC.Interfaces.OutputContracts.FileUploadResponse;
using LoginResponse = LUC.Interfaces.OutputContracts.LoginResponse;

//TODO 1.0 Directory was deleted during sync to server when created offline.

// TODO Release 2.0 Remember downloaded chunks for big file even if app was closed.
[assembly: InternalsVisibleTo(assemblyName: "LUC.UnitTests")]
namespace LUC.ApiClient
{
    [Export( typeof( IApiClient ) )]
    public class ApiClient : WebRestorable, IApiClient
    {
        #region Fields

        private readonly Object m_lockLucDowloaderInit;

        private Upload m_upload;
        private Downloader m_downloader;
        private FileOperation m_fileOperation;
        private Lock m_fileLocker;

        private Boolean m_isOperationsInitialized;

        #endregion Fields

        #region Properties

        public ICurrentUserProvider CurrentUserProvider { get; set; }

        public ObjectNameProvider ObjectNameProvider { get; set; }

        [Import( typeof( ISyncingObjectsList ) )]
        public ISyncingObjectsList SyncingObjectsList { get; set; }

        [Import( typeof( ILoggingService ) )]
        public ILoggingService LoggingService { get; set; }

        [Import( typeof( ISettingsService ) )]
        public ISettingsService SettingsService { get; set; }

        [Import( typeof( INotifyService ) )]
        public INotifyService NotifyService { get; set; }

        public Byte[] EncryptionKey { get; set; }

        public ApiSettings Settings { get; }

        protected override Action StopOperation => new Action( () => { } );

        protected override Action RerunOperation { get; }

        internal IDiscoveryService DiscoveryService { get; private set; }

        internal Downloader Downloader => m_downloader;

        #endregion Properties

        #region Constructors

        [ImportingConstructor]
        public ApiClient( ICurrentUserProvider currentUserProvider, ILoggingService loggingService, ISyncingObjectsList syncingObjectsList, ISettingsService settingsService, INotifyService notifyService = null )
        {
            Settings = new ApiSettings();

            if ( notifyService != null )
            {
                NotifyService = notifyService;
            }
            else
            {
                //NotifyService will be initialized using ImportAttribute
            }

            LoggingService = loggingService;
            SyncingObjectsList = syncingObjectsList;
            SettingsService = settingsService;
            CurrentUserProvider = currentUserProvider;
            ObjectNameProvider = new ObjectNameProvider( currentUserProvider, loggingService, this );

            m_isOperationsInitialized = false;
            m_lockLucDowloaderInit = new Object();

            InitOperations();
        }

        #endregion Constructors

        #region Login/Logout Methods

        public async Task<LoginResponse> LoginAsync( String email, String password )
        {
            var lightClient = new LightClient();

            try
            {
                HttpResponseMessage response = await lightClient.LoginAsync( email, password, Settings.Host );

                if ( response.IsSuccessStatusCode )
                {
                    LoginResponse result;

                    try
                    {
                        result = JsonConvert.DeserializeObject<LoginResponse>( await response.Content.ReadAsStringAsync() );

                        var model = result.ToLoginServiceModel();
                        CurrentUserProvider.LoggedUser = model;
                    }
                    catch ( Exception ex )
                    {
                        LoggingService.LogError( ex, ex.Message );
                        return new LoginResponse
                        {
                            IsSuccess = false,
                            Message = "Can't read content from the response."
                        };
                    }

                    Settings.InitializeAccessToken( result.Token );
                    IsTokenExpiredOrIncorrectAccessToken = false;

                    result.Message = $"Logged as '{result.Login}' at {DateTime.UtcNow}";
                    LoggingService.LogInfo( result.Message );

                    try
                    {
                        //init DS in order to send UDP messages to update our buckets IDs in remote nodes
                        InitLucDownloader( updateOurBucketsIdsInRemoteNodes: true );
                    }
                    catch
                    {
                        ;//do nothing
                    }
                    
                    return result;
                }
                else
                {
                    String stringResult = null;
                    if ( response.Content != null )
                    {
                        stringResult = await response.Content?.ReadAsStringAsync();
                    }

                    LoggingService.LogError( $"Can't login: {stringResult}. Status code = {response.StatusCode}" );

                    switch ( response.StatusCode )
                    {
                        case HttpStatusCode.Forbidden:
                            return new LoginResponse
                            {
                                IsSuccess = false,
                                Message = Strings.Message_WrongLoginOrPassword
                            };
                        default:
                            return new LoginResponse
                            {
                                IsSuccess = false,
                                Message = String.Format( Strings.MessageTemplate_CantLogin, response.StatusCode )
                            };
                    }
                }
            }
            catch ( HttpRequestException )
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = Strings.Message_NoConnection
                };
            }
            catch ( WebException )
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = Strings.Message_NoConnection
                };
            }
            catch ( SocketException )
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = Strings.Message_NoConnection
                };
            }
        }

        public async Task<LogoutResponse> LogoutAsync()
        {
            var result = new LogoutResponse();

            using ( var client = new HttpClient() )
            {
                HttpResponseMessage response = await client.GetAsync( BuildUriExtensions.GetLogoutUri( Settings.Host ) );

                if ( response.IsSuccessStatusCode )
                {
                    Settings.InitializeAccessToken( String.Empty );
                    NotifyServicesAboutLogout();

                    return result;
                }

                String stringResult = await response.Content.ReadAsStringAsync();

                LoggingService.LogError( "Can't logout: " + stringResult );

                result.IsSuccess = false;
                result.Message = stringResult;
                return result;
            }
        }

        #endregion Login/Logout Methods

        public async Task<CreateDirectoryResponse> CreateDirectoryOnServerAsync( String fullPath )
        {
            IBucketName bucket = CurrentUserProvider.TryExtractBucket( fullPath );

            if ( !bucket.IsSuccess )
            {
                LoggingService.LogError( bucket.ErrorMessage );
                return new CreateDirectoryResponse
                {
                    IsSuccess = false,
                    Message = bucket.ErrorMessage
                };
            }

            LoggingService.LogInfo( $"API CreatePseudoDirectory {fullPath}" );

            String prefix = await ObjectNameProvider.ServerPrefix( fullPath );

            String stringContent = JsonConvert.SerializeObject( new CreateDirectoryRequest
            {
                DirectoryName = new DirectoryInfo( fullPath ).Name,
                Prefix = prefix
            } );

            using ( var client = new RepeatableHttpClient( Settings.AccessToken ) )
            {
                using ( var content = new StringContent( stringContent, Encoding.UTF8, "application/json" ) )
                {
                    String requestUri = BuildUriExtensions.PostCreatePseudoDirectoryUri( Settings.Host, bucket.ServerName );

                    HttpResponseMessage response = await client.SendRepeatableAsync( requestUri, () => RepeatableHttpClient.CloneHttpContentAsync( content ), HttpMethod.Post ).ConfigureAwait( continueOnCapturedContext: false );

                    if ( response.IsSuccessStatusCode )
                    {
                        LoggingService.LogInfo( $"OK. Directory '{fullPath}' was created." );
                    }
                    else if ( response.StatusCode == HttpStatusCode.Forbidden )
                    {
                        ForbiddenListResponse parsed = JsonConvert.DeserializeObject<ForbiddenListResponse>( await response.Content.ReadAsStringAsync() );

                        CurrentUserProvider.UpdateLoggedUserGroups( parsed.Groups.ToGroupServiceModelList() );

                        return new CreateDirectoryResponse
                        {
                            IsSuccess = false,
                            IsForbidden = true
                        };
                    }
                    else
                    {
                        // {"error":10} Directory exists already.
                        // "{\"error\":29}" "Object exists already.". Ignore for the moment. Later add conflict UI.
                        String plainStringResponse = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

                        if ( plainStringResponse.Contains( "10" ) )
                        {
                            return new CreateDirectoryResponse
                            {
                                IsSuccess = true
                            };
                        }
                        else
                        {
                            LoggingService.LogError( "Directory was not created: " + plainStringResponse );
                        }
                    }

                    return new CreateDirectoryResponse
                    {
                        IsSuccess = response.IsSuccessStatusCode,
                        Message = response.IsSuccessStatusCode ? "Directory " + fullPath + " was created." : "Directory " + fullPath + " was NOT created."
                    };
                }
            }
        }

        #region Upload Methods

        public async Task<FileUploadResponse> TryUploadAsync( FileInfo fileInfo )
        {
            DateTime startUpload = DateTime.Now;
            FileUploadResponse response = await m_upload.TryUploadAsync( fileInfo );
            response.UploadTime = (Int64)( DateTime.Now - startUpload ).TotalSeconds;

            return response;
        }

        // TODO Release 2.0 Implement for second release https://github.com/GrzegorzBlok/FastRsyncNet
        #endregion Upload Methods

        public async Task<ObjectsListResponse> ListWithCancelDownloadAsync( String bucketName, String prefix = "", Boolean showDeleted = false ) =>
            await m_fileOperation.ListWithCancelDownloadAsync( bucketName, prefix, showDeleted ).ConfigureAwait( continueOnCapturedContext: false );

        public async Task<ObjectsListResponse> ListAsync( String bucketName, String prefix = "", Boolean showDeleted = false ) =>
            await m_fileOperation.ListAsync( bucketName, prefix, showDeleted ).ConfigureAwait( continueOnCapturedContext: false );

        public async Task<DeleteResponse> DeleteAsync( params String[] fullPathes ) => await m_fileOperation.DeleteAsync( fullPathes );

        public async Task<HttpResponseMessage> DeleteAsync( DeleteRequest requestBody, String bucketName ) => await m_fileOperation.DeleteAsync( requestBody, bucketName );

        // TODO Release 3.0 Should be setting how to sync per bucket.
        // They should be shared and synchronized during each API List
        // Like what to do for move of file which not exists on server? Other cases.

        // It sends list of moved objects.
        // TODO Server Handle 202 response! Not all objects was moved. Test with Vitaly over repetable logic.
        public async Task<MoveOrCopyResponse> MoveAsync( String oldFullPath, String newFullPath )
        {
            IBucketName bucket = CurrentUserProvider.TryExtractBucket( oldFullPath );

            if ( !bucket.IsSuccess )
            {
                return new MoveOrCopyResponse
                {
                    IsSuccess = false,
                    Message = bucket.ErrorMessage
                };
            }

            String requestUrl = BuildUriExtensions.PostMoveUri( Settings.Host, bucket.ServerName );

            LoggingService.LogInfo( $"API Move from {oldFullPath} to {newFullPath}" );

            MoveOrCopyResponse result = await MoveOrCopy( oldFullPath, newFullPath, requestUrl );

            return result;
        }

        private async Task<MoveOrCopyResponse> MoveOrCopy( String oldFullPath, String newFullPath, String requestUrl )
        {
            ServerObjectDescription serverDesc = await ObjectNameProvider.GetExistingObjectDescription( oldFullPath );

            FileSystemInfo fi = FileInfoHelper.TryGetFileInfo( newFullPath );

            if ( fi == null )
            {
                fi = FileInfoHelper.TryGetDirectoryInfo( newFullPath );
            }

            if ( serverDesc.IsSuccess )
            {
                if ( fi == null )
                {
                    Log.Warning( $"No object {newFullPath}" );

                    return new MoveOrCopyResponse
                    {
                        IsSuccess = false,
                        Message = String.Format( FileInfoHelper.ERROR_DESCRIPTION, newFullPath )
                    };
                }

                String sourcePrefix = await ObjectNameProvider.ServerPrefix( oldFullPath );

                if ( sourcePrefix == null )
                {
                    return new MoveOrCopyResponse
                    {
                        IsSuccess = false,
                        Message = $"Can't get server prefix for {oldFullPath}"
                    };
                }

                String destinationPrefix = await ObjectNameProvider.ServerPrefix( newFullPath );

                var sourceKeys = new Dictionary<String, String>();

                if ( Directory.Exists( newFullPath ) || File.Exists( newFullPath ) )
                {
                    sourceKeys.Add( serverDesc.ObjectKey, Path.GetFileName( newFullPath ) );
                }
                else
                {
                    return new MoveOrCopyResponse { IsSuccess = false, Message = "Object does not exist." };
                }

                IBucketName bucket = CurrentUserProvider.TryExtractBucket( newFullPath );

                if ( !bucket.IsSuccess )
                {
                    return new MoveOrCopyResponse
                    {
                        IsSuccess = false,
                        Message = bucket.ErrorMessage
                    };
                }

                var moveRequest = new MoveOrCopyRequest
                {
                    SourcePrefix = sourcePrefix,
                    SourceObjectKeys = sourceKeys,
                    DestinationPrefix = destinationPrefix,
                    DestinationBucketId = bucket.ServerName,
                };

                String stringContent = JsonConvert.SerializeObject( moveRequest );

                using ( var client = new RepeatableHttpClient( Settings.AccessToken, HttpStatusCode.Accepted ) )
                {
                    var content = new StringContent( stringContent, Encoding.UTF8, "application/json" );

                    HttpResponseMessage response = await client.SendRepeatableAsync( requestUrl, () => RepeatableHttpClient.CloneHttpContentAsync( content ), HttpMethod.Post );

                    // NOTE For file which does not exist on server response will be 200 also.
                    if ( response.StatusCode == HttpStatusCode.OK )
                    {
                        String parsed2 = await response.Content.ReadAsStringAsync();
                        MoveOrCopyResponse parsed;
                        LoggingService.LogInfo( $"Plain Move/Copy API response = {parsed2}" );
                        try
                        {
                            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented,
                                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                NullValueHandling = NullValueHandling.Include
                            };
                            parsed = JsonConvert.DeserializeObject<MoveOrCopyResponse>( parsed2 );
                        }
                        catch ( Exception ex )
                        {
                            LoggingService.LogError( ex, ex.Message );

                            return new MoveOrCopyResponse
                            {
                                IsSuccess = false,
                                Message = "Can't read content from the response."
                            };
                        }

                        if ( parsed.ToString() == String.Empty )
                        {
                            return new MoveOrCopyResponse
                            {
                                IsSuccess = false
                            };
                        }

                        //if (!String.IsNullOrEmpty(parsed.SkippedReason))
                        //{
                        //    if (File.Exists(newFullPath))
                        //    {
                        //        _ = await TryUploadAsync(FileInfoHelper.TryGetFileInfo(newFullPath));
                        //    }
                        //    else if (Directory.Exists(newFullPath))
                        //    {
                        //        _ = CreateDirectoryOnServer(newFullPath);
                        //    }

                        //    Console.WriteLine("Can't Move/Copy: Reason " + parsed.SkippedReason + " Object was created from scratch.");
                        //    return new MoveOrCopyResponse
                        //    {
                        //        IsSuccess = false,
                        //        Message = "Can't Move/Copy: Reason " + parsed.SkippedReason + " Object was created from scratch."
                        //    };
                        //}

                        //if (String.IsNullOrEmpty(parsed.SkippedReason))
                        //{
                        AdsExtensions.Write( newFullPath, text: newFullPath, AdsExtensions.Stream.LocalPathMarker ); // NOTE. Do it before renaming.

                        // TODO 1.0 Test after Vitaly fix. Move/Copy file which is deleted on server. check response with empty list. upload with new path if old is absent on server.
                        parsed.IsSuccess = true;
                        parsed.Message = "Object " + oldFullPath + " was moved/copied.";

                        if ( parsed.IsRenamed ) // TODO Release 2.0 discuss renaming of directories.
                        {
                            if ( !File.Exists( oldFullPath ) )
                            {
                                INotificationResult result = SyncingObjectsList.RenameFileOnlyLocal( fi as FileInfo, parsed.DestinationOriginalName, fi.LastWriteTimeUtc, parsed.Guid );
                                if ( result.IsSuccess )
                                {
                                    parsed.Message = "Object " + oldFullPath + " was moved to the " + newFullPath;
                                    return parsed;
                                }
                            }
                            else
                            {
                                FileUploadResponse uploadResult = await TryUploadAsync( fi as FileInfo );

                                return new MoveOrCopyResponse
                                {
                                    IsSuccess = uploadResult.IsSuccess,
                                    Guid = uploadResult.Guid,
                                };
                            }
                        }

                        LoggingService.LogInfo( parsed.Message );
                        return parsed;
                        //}
                        //else
                        //{
                        //    // TODO 1.0 How about folder for the same case?
                        //    var uploadResult = await TryUploadAsync(FileInfoHelper.TryGetFileInfo(newFullPath));
                        //    return new MoveOrCopyResponse
                        //    {
                        //        IsSuccess = uploadResult.IsSuccess,
                        //        Guid = uploadResult.Guid,
                        //        FinalName = Path.GetFileName(newFullPath)
                        //    };
                        //}
                    }
                    else if ( response.StatusCode == HttpStatusCode.Forbidden )
                    {
                        return await HandleResponse403<MoveOrCopyResponse>( response, $"Status code = '{response.StatusCode}'." );
                    }
                    else
                    {
                        // "11": "Incorrect prefix.",
                        // "32": "Source pseudo-directory does not exist.",
                        // "30": "Source object does not exist.",
                        // "13": "No files to move. The destination directory might be subdirectory of the source directory.",
                        // "12": "Directory name is required. Can't contain any of the following characters: \" < > \\ | / : * ?",
                        // "14": "Incorrect source or destination prefix.",

                        String error = await response.Content.ReadAsStringAsync();

                        LoggingService.LogError( $"Can't move/copy from {oldFullPath} to {newFullPath}: {error}" );
                        LoggingService.LogError( $"Can't move/copy from {oldFullPath} to {newFullPath}. Plain request = {stringContent}" );

                        LoggingService.LogError( $"Move response: {response.StatusCode}, error: {error}" );

                        return new MoveOrCopyResponse
                        {
                            IsSuccess = false,
                            Message = $"Cant move/copy. Server status code is '{response.StatusCode}'"
                        };
                    }
                }
            }
            else
            {
                if ( Directory.Exists( newFullPath ) )
                {
                    CreateDirectoryResponse response = await CreateDirectoryOnServerAsync( newFullPath );

                    return new MoveOrCopyResponse
                    {
                        IsSuccess = response.IsSuccess,
                        Message = response.Message
                    };
                }
                else if ( File.Exists( newFullPath ) )
                {
                    FileUploadResponse uploadResult = await TryUploadAsync( fi as FileInfo );

                    return new MoveOrCopyResponse
                    {
                        IsSuccess = uploadResult.IsSuccess,
                        Guid = uploadResult.Guid,
                    };
                }
                else
                {
                    String message = $"Can't Move/Copy: {serverDesc.Message} Cant get server object name for path '{ oldFullPath}' and local file info for '{newFullPath}'";
                    LoggingService.LogError( message );

                    return new MoveOrCopyResponse
                    {
                        IsSuccess = false,
                        Message = message
                    };
                }
            }
        }

        public async Task<MoveOrCopyResponse> CopyAsync( String oldFullPath, String newFullPath )
        {
            IBucketName bucket = CurrentUserProvider.TryExtractBucket( oldFullPath );//it doesn't work correctly

            if ( !bucket.IsSuccess )
            {
                return new MoveOrCopyResponse
                {
                    IsSuccess = false,
                    Message = bucket.ErrorMessage
                };
            }

            String requestUrl = BuildUriExtensions.PostCopyUri( Settings.Host, bucket.ServerName );

            LoggingService.LogInfo( $"API Copy from {oldFullPath} to {newFullPath}" );

            MoveOrCopyResponse result = await MoveOrCopy( oldFullPath, newFullPath, requestUrl );

            if ( result.IsSuccess )
            {
                String finalName = result.IsRenamed ? result.DestinationOriginalName : result.SourceOriginalName;
                String finalPath = Path.Combine( Path.GetDirectoryName( newFullPath ), finalName );

                AdsExtensions.WriteGuidAndLocalPathMarkersIfNotTheSame( finalPath, result.Guid );
            }

            return result;
        }

        public async Task<RenameResponse> RenameAsync( String oldFullPath, String newFullPath )
        {
            // TODO Check rename when something is locked inside. 202.
            LoggingService.LogInfo( $"API request for rename from {oldFullPath} to {newFullPath}" );

            ServerObjectDescription serverDesc = await ObjectNameProvider.GetExistingObjectDescription( oldFullPath );

            if ( serverDesc.IsSuccess )
            {
                IBucketName bucket = CurrentUserProvider.TryExtractBucket( oldFullPath );

                if ( !bucket.IsSuccess )
                {
                    return new RenameResponse
                    {
                        IsSuccess = false,
                        Message = bucket.ErrorMessage
                    };
                }

                var renameRequest = new RenameRequest
                {
                    Prefix = await ObjectNameProvider.ServerPrefix( oldFullPath ),
                    DestinationObjectName = Path.GetFileName( newFullPath ),
                    SourceObjectKey = serverDesc.ObjectKey
                };

                using ( var client = new RepeatableHttpClient( Settings.AccessToken ) )
                {
                    var content = new StringContent( JsonConvert.SerializeObject( renameRequest ), Encoding.UTF8, "application/json" );

                    String requestUri = BuildUriExtensions.PostRenameUri( Settings.Host, bucket.ServerName );

                    HttpResponseMessage response = await client.SendRepeatableAsync( requestUri, contentReciever: () => RepeatableHttpClient.CloneHttpContentAsync( content ), HttpMethod.Post );

                    LoggingService.LogInfo( $"API response for rename from {oldFullPath} to {newFullPath} is {response.StatusCode}" );

                    if ( response.IsSuccessStatusCode )
                    {
                        AdsExtensions.Write( newFullPath, text: newFullPath, AdsExtensions.Stream.LocalPathMarker );

                        return new RenameResponse
                        {
                            Message = "Object " + oldFullPath + " was renamed."
                        };
                    }
                    else if ( response.StatusCode == HttpStatusCode.Forbidden )
                    {
                        return await HandleResponse403<RenameResponse>( response, $"Status code = '{response.StatusCode}'." );
                    }
                    else
                    {
                        // {"error":10} Directory exists already.
                        // "29": "Object exists already."
                        // "11": "HexPrefix does not exist.",
                        // "{\"error\":9}" "Incorrect object name."
                        //"43": "Locked",

                        // Release 2.0 Later show conflict files UI
                        String error = await response.Content.ReadAsStringAsync();

                        if ( error.Contains( "29" ) || error.Contains( "43" ) )
                        {
                            FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( newFullPath );
                            if ( fileInfo != null )
                            {
                                String guid = AdsExtensions.Read( newFullPath, AdsExtensions.Stream.Guid );
                                _ = SyncingObjectsList.RenameFileOnlyLocal( fileInfo, oldFullPath, fileInfo.LastWriteTimeUtc, guid );
                            }
                        }
                        else
                        {
                            LoggingService.LogError( "Can't rename: " + error );
                        }

                        return new RenameResponse
                        {
                            IsSuccess = false,
                            Message = $"Cant rename. Server status code is '{response.StatusCode}'. Error is '{error}'."
                        };
                    }
                }
            }
            else
            {
                FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( newFullPath );
                if ( fileInfo == null )
                {
                    if ( Directory.Exists( newFullPath ) )
                    {
                        LoggingService.LogInfo( $"Directory to rename {oldFullPath} does not exist on server. So the directory {newFullPath} will be created." );
                        CreateDirectoryResponse response = await CreateDirectoryOnServerAsync( newFullPath );
                    }
                    else
                    {
                        return new RenameResponse
                        {
                            IsSuccess = false,
                            Message = $"'{oldFullPath}' was NOT uploaded, not renamed. No info about old full path '{oldFullPath}' and on server, and the new path '{newFullPath}' is NOT exists."
                        };
                    }
                }
                else
                {
                    LoggingService.LogInfo( $"File to rename {oldFullPath} does not exist on server. So the file {newFullPath} will be uploaded from scratch." );
                    _ = await TryUploadAsync( fileInfo );
                }

                return new RenameResponse
                {
                    IsSuccess = false,
                    Message = $"'{oldFullPath}' was uploaded, not renamed. No info about old full path '{oldFullPath}' on server."
                };
            }
        }

        private async Task<T> HandleResponse403<T>( HttpResponseMessage response, String message ) where T : BaseResponse
        {
            ForbiddenListResponse parsed = JsonConvert.DeserializeObject<ForbiddenListResponse>( await response.Content.ReadAsStringAsync() );
            //ForbiddenListResponse parsed = await response.Content.ReadAsync<ForbiddenListResponse>();

            CurrentUserProvider.UpdateLoggedUserGroups( parsed.Groups.ToGroupServiceModelList() );

            Object result = Activator.CreateInstance( typeof( T ), new Object[] { false, true, message } );

            return result as T;
        }

        #region Lock/Unlock Methods

        public async Task LockFile( String fullPath ) =>
            await m_fileLocker.LockFile( fullPath );

        public async Task UnlockFile( String fullPath ) =>
            await m_fileLocker.UnlockFile( fullPath );

        #endregion Lock/Unlock Methods

        public async Task DownloadFileAsync(
            String bucketName,
            String prefix,
            String localFolderPath,
            String localOriginalName,
            ObjectDescriptionModel objectDescription,
            CancellationToken cancellationToken = default
        )
        {
            InitLucDownloader( updateOurBucketsIdsInRemoteNodes: false );

            await Downloader.DownloadFileAsync(
                  bucketName,
                  prefix,
                  localFolderPath,
                  localOriginalName,
                  objectDescription,
                  cancellationToken
             ).ConfigureAwait( continueOnCapturedContext: false );
        }

        public Task<ServerObjectDescription> GetExistingObjectDescription( String objectFullPath ) =>
            ObjectNameProvider.GetExistingObjectDescription( objectFullPath );

        private void InitOperations()
        {
            if ( !m_isOperationsInitialized )
            {
                m_fileOperation = new FileOperation( this );
                m_fileLocker = new Lock( Settings, ObjectNameProvider, NotifyService );

                m_upload = new Upload( this );

                m_isOperationsInitialized = true;
            }
        }

        private void InitLucDownloader( Boolean updateOurBucketsIdsInRemoteNodes )
        {
            if ( updateOurBucketsIdsInRemoteNodes )
            {
                DiscoveryService = DiscoveryServiceFacade.FullyInitialized( CurrentUserProvider, SettingsService );
            }

            SingletonInitializer.ThreadSafeInit(
                value: () =>
                {
                    if ( DiscoveryService == null )
                    {
                        DiscoveryService = DiscoveryServiceFacade.FullyInitialized( CurrentUserProvider, SettingsService );
                    }

                    var downloader = new Downloader( this );
                    return downloader;
                },
                m_lockLucDowloaderInit,
                ref m_downloader
            );
        }

        private void NotifyServicesAboutLogout()
        {
            try
            {
                var discoveryService = DiscoveryServices.DiscoveryService.BeforeCreatedInstance( GeneralConstants.PROTOCOL_VERSION );
                discoveryService.ClearAllLocalBuckets();

                SyncingObjectsList.CancelDownloadingAllFilesWhichBelongPath( CurrentUserProvider.RootFolderPath );
            }
            catch ( Exception ex )
            {
                LoggingService.LogError( ex.ToString() );
            }
        }
    }
}
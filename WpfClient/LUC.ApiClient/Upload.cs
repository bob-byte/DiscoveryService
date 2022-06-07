using LightClientLibrary;

using LUC.ApiClient.Models;
using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FileUploadResponse = LUC.Interfaces.OutputContracts.FileUploadResponse;

namespace LUC.ApiClient
{
    internal class Upload : SyncServicesProvider
    {
        private static readonly TimeSpan s_timeWaitAfterUploadProcess = TimeSpan.FromSeconds( value: 0.5 );

        private readonly FileOperation m_fileOperation;

        public Upload( ApiClient apiClient )
            : base( apiClient, apiClient.ObjectNameProvider )
        {
            NotifyService = apiClient.NotifyService;
            m_fileOperation = new FileOperation( apiClient );
        }

        public INotifyService NotifyService { get; set; }

        public async Task<FileUploadResponse> TryUploadAsync( FileInfo fileInfo )
        {
            FileUploadResponse uploadResponse = null;

            try
            {
                m_fileOperation.CheckFileAndAddToUploadingFiles( fileInfo );
                await CheckGuid( fileInfo.FullName );

                String logRecord = $"API Upload {fileInfo.FullName} started by user {CurrentUserProvider.LoggedUser.Login}. ID= {CurrentUserProvider.LoggedUser.Id}...";
                LoggingService.LogInfoWithLongTime( logRecord );

                uploadResponse = await UploadBigFileAsync( fileInfo );

                // - folder with the same name already exists on server.
                // - file is locked by another user.
                if ( uploadResponse.IsSuccess )
                {
                    CheckWhetherRenameFile( fileInfo, uploadResponse );

                    await DefineWhetherLockFile( fileInfo.FullName );
                }
                else
                {
                    LoggingService.LogError( uploadResponse.Message );
                }
            }
            catch ( FileNotFoundException exception )
            {
                uploadResponse = new FileUploadResponse
                {
                    IsSuccess = false,
                    Message = exception.Message
                };
            }
            catch ( Exception exception )
            {
                LoggingService.LogError( $"Upload client exception: {exception}" );

                uploadResponse = new FileUploadResponse
                {
                    IsSuccess = false,
                    Message = exception.Message
                };
            }
            finally
            {
                if ( uploadResponse?.IsSuccess == true )
                {
                    //wait while FileSystemFacade is checking file change (alternation in the ADS raises this;
                    //FileSystemFacade ignors change in uploading files)
                    await Task.Delay( s_timeWaitAfterUploadProcess ).ConfigureAwait( false );
                }

                SyncingObjectsList.RemoveUploadingFile( fileInfo.FullName );
                LoggingService.LogInfoWithLongTime( $"... finished API Upload {fileInfo.FullName}" );
            }

            return uploadResponse;
        }

        private async Task CheckGuid( String fileFullName )
        {
            String possibleGuid = AdsExtensions.Read( fileFullName, AdsExtensions.Stream.Guid );
            if ( !String.IsNullOrEmpty( possibleGuid ) )
            {
                IBucketName bucket = CurrentUserProvider.TryExtractBucket( fileFullName );

                if ( bucket.IsSuccess )
                {
                    String prefix = CurrentUserProvider.ExtractPrefix( fileFullName );
                    Interfaces.OutputContracts.ObjectsListResponse list = await m_fileOperation.ListAsync( bucket.ServerName, prefix, true );

                    if ( !list.ObjectFileDescriptions.Any( x => x.Guid == possibleGuid ) )
                    {
                        AdsExtensions.Remove( fileFullName, AdsExtensions.Stream.Guid );
                    }
                }
            }
        }

        private void CheckWhetherRenameFile( FileInfo fileInfo, FileUploadResponse uploadResult )
        {
            if ( !fileInfo.Name.IsEqualFilePathesInCurrentOs( uploadResult.OriginalName ) )
            {
                String destinationFileName = Path.Combine( fileInfo.Directory.FullName, uploadResult.OriginalName );
                DateTime uploadDateTime = uploadResult.UploadTime.FromUnixTimeStampToDateTime();

                INotificationResult result = SyncingObjectsList.RenameFileOnlyLocal( fileInfo, destinationFileName, uploadDateTime, uploadResult.Guid );

                if ( result?.IsSuccess == true )
                {
                    MessageBoxHelper.ShowMessageBox( SentenceTranslator.ProvideMessageAboutRenamedFile( fileInfo.Name, uploadResult.OriginalName ), Strings.Label_Attention );
                }
            }
        }

        //replace to class Lock
        private async Task DefineWhetherLockFile( String fileFullName )
        {
            ILockDescription lockDescription = AdsExtensions.ReadLockDescription( fileFullName );
            AdsLockState lockState = lockDescription.LockState;

            var locker = new Lock( m_apiSettings, ObjectNameProvider, NotifyService );
            if ( lockState == AdsLockState.ReadyToLock )
            {
                await locker.LockFile( fileFullName );
            }
            else if ( lockState == AdsLockState.ReadyToUnlock )
            {
                await locker.UnlockFile( fileFullName );
            }
        }

        private void GetInitialUploadState( FileInfo fileInfo, out ChunkUploadState chunkUploadState, out IBucketName bucket )
        {
            String fileFullPath = fileInfo.FullName;

            bucket = CurrentUserProvider.TryExtractBucket( fileFullPath );
            //create upload state
            String requestUri = BuildUriExtensions.PostUploadUri( m_apiSettings.Host, bucket.ServerName );
            chunkUploadState = new ChunkUploadState
            {
                ChunkRequestUri = requestUri,
                Guid = AdsExtensions.Read( fileFullPath, AdsExtensions.Stream.Guid ),
                IsLastChunk = false
            };
        }

        // TODO Release 2.0 Implement for second release https://github.com/GrzegorzBlok/FastRsyncNet
        private async Task<FileUploadResponse> UploadBigFileAsync( FileInfo fileInfo )
        {
            String fullPath = fileInfo.FullName;
            String filePrefix;

            var lightClient = new LightClient();

            try
            {
                GetInitialUploadState( fileInfo, out ChunkUploadState uploadState, out IBucketName bucket );

                filePrefix = await ObjectNameProvider.ServerPrefix( fileInfo.FullName ).ConfigureAwait( continueOnCapturedContext: false );
                String userId = CurrentUserProvider.LoggedUser.Id;
                String version = AdsExtensions.Read( fullPath, AdsExtensions.Stream.LastSeenVersion );

                System.Net.Http.HttpResponseMessage response = await lightClient.Upload( m_apiSettings.Host, CurrentUserProvider.LoggedUser.Token, userId, bucket.ServerName,
                                                        fullPath, filePrefix, version );

                FileUploadResponse responseUpload;
                if (response.Content != null)
                {
                    String str = await response.Content?.ReadAsStringAsync();
                    responseUpload = response.IsSuccessStatusCode
                        ? JsonConvert.DeserializeObject<FileUploadResponse>( str )
                        : new FileUploadResponse
                        {
                            IsSuccess = false,
                            Message = str ?? String.Empty
                        };
                }
                else
                {
                    responseUpload = new FileUploadResponse
                    {
                        IsSuccess = false,
                        Message = "Content is null"
                    };
                }

                if ( responseUpload.IsSuccess )
                {
                    try
                    {
                        AdsExtensions.WriteInfoAboutNewFileVersion( fileInfo, responseUpload.FileVersion, responseUpload.Guid );
                    }
                    catch ( Exception ex )
                    {
                        LoggingService.LogError( ex, logRecord: $"Cannot write version ({responseUpload.FileVersion}) or guid ({responseUpload.Guid}) for {fileInfo.FullName}" );
                    }
                }

                return responseUpload;
            }
            catch ( ArgumentException ex )
            {
                return new FileUploadResponse
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }
    }
}
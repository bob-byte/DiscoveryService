using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.InputContracts;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;

using Newtonsoft.Json;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LUC.ApiClient
{
    class Lock : ApiSettingsProvider
    {
        public INotifyService NotifyService { get; set; }

        internal Lock( ApiSettings apiSettings, ObjectNameProvider objectNameProvider, INotifyService notifyService )
            : base( apiSettings, objectNameProvider )
        {
            NotifyService = notifyService;
        }

        public async Task LockFile( String fullPath )
        {
            HttpResponseMessage response = await PatchObject( fullPath, "lock" );
            await ProceedPatchResponse( fullPath, response, "locked", true );
        }

        public async Task UnlockFile( String fullPath )
        {
            HttpResponseMessage response = await PatchObject( fullPath, "unlock" );
            await ProceedPatchResponse( fullPath, response, "unlocked", false );
        }

        // locked, unlocked. // TODO Translate
        private async Task ProceedPatchResponse( String fullPath, HttpResponseMessage response,
            String expectedLockDescription, Boolean isLockedExpected )
        {
            if ( response.IsSuccessStatusCode )
            {
                LockStateSubResponse fromResponse;
                try
                {
                    fromResponse = await PatchResponse( fullPath, response );
                }
                catch
                {
                    NotifyService.NotifyError( $"File {fullPath} is not {expectedLockDescription}. Server issue. Server sent state for other files, not for mentioned." );
                    return;
                }

                NotifySuccessResponse( fullPath, fromResponse, expectedLockDescription, isLockedExpected );
            }
            else if ( response.StatusCode == HttpStatusCode.Forbidden )
            {
                ForbiddenListResponse parsed = JsonConvert.DeserializeObject<ForbiddenListResponse>( await response.Content.ReadAsStringAsync() );
                //ForbiddenListResponse parsed = await response.Content.ReadAsync<ForbiddenListResponse>();

                CurrentUserProvider.UpdateLoggedUserGroups( parsed.Groups.ToGroupServiceModelList() );

                NotifyService.NotifyError( $"File {fullPath} is not {expectedLockDescription} because you do not have access to it alreasy." ); // TODO 1.0 Translate.
            }
            else
            {
                String t = await response.Content.ReadAsStringAsync();
                NotifyService.NotifyError( $"File {fullPath} is not {expectedLockDescription}..." );
            }
        }

        private async Task<LockStateSubResponse> PatchResponse( String fullPath, HttpResponseMessage response )
        {
            String json = await response.Content.ReadAsStringAsync();
            LockStateSubResponse[] parsed;

            try
            {
                parsed = JsonConvert.DeserializeObject<LockStateSubResponse[]>( json );
                //parsed = await response.Content.ReadAsync<LockStateSubResponse[]>();
            }
            catch ( Exception ex )
            {
                LoggingService.LogError( ex, ex.Message );
                throw;
            }

            ServerObjectDescription desc = await ObjectNameProvider.GetExistingObjectDescription( fullPath );
            String key = desc.ObjectKey;

            LockStateSubResponse fromResponse = parsed.FirstOrDefault( x => x.ObjectKey.Equals( key ) );

            return fromResponse;
        }

        private void NotifySuccessResponse( String fullPath, LockStateSubResponse fromResponse, String expectedLockDescription, Boolean isLockedExpected )
        {
            AdsLockState stateFromServer = fromResponse.IsLocked ? AdsLockState.LockedOnServer : AdsLockState.Default;

            ILockDescription description = new LockDescription( stateFromServer )
            {
                LockTimeUtc = fromResponse.LockModifiedUtc.FromUnixTimeStampToDateTime(),
                LockUserId = fromResponse.LockUserId,
                LockUserTel = fromResponse.LockUserTel,
                LockUserName = fromResponse.LockUserName
            };

            AdsExtensions.WriteLockDescription( fullPath, description );

            String fileName = fullPath;

            System.IO.FileInfo fi = FileInfoHelper.TryGetFileInfo( fullPath );
            if ( fi != null )
            {
                fileName = fi.Name;
            }

            if ( fromResponse.IsLocked != isLockedExpected )
            {
                NotifyService.NotifyError( $"File {fileName} is not {expectedLockDescription}..." );
            }

            if ( fromResponse.LockUserId != CurrentUserProvider.LoggedUser.Id )
            {
                NotifyService.NotifyError( SentenceTranslator.ProvideMessageAboutLockedFile( fileName, fromResponse.LockUserName, fromResponse.LockUserTel ) );
            }
        }

        private async Task<HttpResponseMessage> PatchObject( String fullPath, String operation )
        {
            ServerObjectDescription desc = await ObjectNameProvider.GetExistingObjectDescription( fullPath );

            if ( !desc.IsSuccess )
            {
                return new HttpResponseMessage( HttpStatusCode.NotFound );
            }

            IBucketName bucket = CurrentUserProvider.TryExtractBucket( fullPath );

            if ( !bucket.IsSuccess )
            {
                return new HttpResponseMessage( HttpStatusCode.NotFound );
            }

            String bucketName = bucket.ServerName;
            String requestUri = BuildUriExtensions.GetLockUri( m_apiSettings.Host, bucketName );

            using ( var client = new RepeatableHttpClient( m_apiSettings.AccessToken ) )
            {
                var lockRequest = new LockRequest
                {
                    Objects = new String[] { desc.ObjectKey },
                    Operation = operation,
                    Prefix = desc.ObjectPrefix
                };

                var content = new StringContent( JsonConvert.SerializeObject( lockRequest ), Encoding.UTF8, "application/json" );

                HttpResponseMessage response = await client.SendRepeatableAsync( requestUri, contentReciever: () => RepeatableHttpClient.CloneHttpContentAsync( content ), new HttpMethod( "PATCH" ) );

                return response;
            }
        }
    }
}

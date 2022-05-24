using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.OutputContracts;

using Newtonsoft.Json;

using Serilog;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace LUC.ApiClient
{
    static class HandleResponse
    {
        internal static async Task<T> HandleResponse403<T>( HttpResponseMessage response, String message, ICurrentUserProvider currentUserProvider )
            where T : BaseResponse
        {
            ForbiddenListResponse parsed = JsonConvert.DeserializeObject<ForbiddenListResponse>( await response.Content.ReadAsStringAsync() );
            //ForbiddenListResponse parsed = await response.Content.ReadAsync<ForbiddenListResponse>();

            currentUserProvider.UpdateLoggedUserGroups( parsed.Groups.ToGroupServiceModelList() );

            Object result = Activator.CreateInstance( typeof( T ), new Object[] { false, true, message } );

            return result as T;
        }

        internal static async Task<ObjectsListResponse> HandleBadListResponse( HttpResponseMessage response, ILoggingService loggingService )
        {
            String contentAsString = await response.Content.ReadAsStringAsync();

            String error = await response.Content.ReadAsStringAsync();
            loggingService.LogError( $"List API error response: {contentAsString} {error}" );

            return new ObjectsListResponse
            {
                IsSuccess = false,
                Message = $"Status code = '{response.StatusCode}'. Content as string = '{contentAsString}'"
            };
        }

        internal static async Task<FileUploadResponse> BuildNotSuccessResult( HttpResponseMessage response, FileInfo fileInfo, String filePrefix, ILoggingService loggingService )
        {
            if ( response.StatusCode == HttpStatusCode.NotModified )
            {
                String message = $"File {fileInfo.Name} has more recent or the same version on server side";
                loggingService.LogError( message );

                return new FileUploadResponse
                {
                    IsSuccess = false,
                    Message = message,
                    OriginalName = fileInfo.Name
                };
            }
            else if ( response.StatusCode == (HttpStatusCode)423 )
            {
                LockedUploadResponse lockResponse;

                try
                {
                    lockResponse = await response.Content.ReadAsAsync<LockedUploadResponse>();
                }
                catch ( Exception ex )
                {
                    loggingService.LogError( ex, ex.Message );

                    return new FileUploadResponse
                    {
                        IsSuccess = false,
                        Message = "Can't read content from the response."
                    };
                }

                MessageBoxHelper.ShowMessageBox( SentenceTranslator.ProvideMessageAboutLockedFile( fileInfo.Name, lockResponse.LockUserName, lockResponse.LockUserTel ), Strings.Label_Attention );
                return new FileUploadResponse
                {
                    IsSuccess = false,
                    Message = $"File {fileInfo.Name} is locked."
                };
            }
            else
            {
                String error = await response.Content.ReadAsStringAsync();

                loggingService.LogError( $"Upload error. Status code: {response.StatusCode}. Response.Content.AsString = {error}" );

#if DEBUG
                Log.Debug( filePrefix );
                String parsed = filePrefix.FromHexString();
                Log.Debug( parsed );
#endif

                return new FileUploadResponse
                {
                    IsSuccess = false,
                    Message = "File " + fileInfo.FullName + " was NOT uploaded."
                };
            }
        }
    }
}

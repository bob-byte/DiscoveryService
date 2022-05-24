using LUC.Interfaces.InputContracts;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.Interfaces
{
    public interface IApiClient
    {
        Byte[] EncryptionKey { get; set; }

        ISyncingObjectsList SyncingObjectsList { get; }

        ApiSettings Settings { get; }

        Task<LoginResponse> LoginAsync( String email, String password );

        Task<LogoutResponse> LogoutAsync();

        Task<CreateDirectoryResponse> CreateDirectoryOnServerAsync( String fullPath );

        Task<FileUploadResponse> TryUploadAsync( FileInfo fileInfo );

        Task<ObjectsListResponse> ListAsync( String bucketName, String prefix = "", Boolean showDeleted = false );

        Task<ObjectsListResponse> ListWithCancelDownloadAsync( String bucketName, String prefix = "", Boolean showDeleted = false );

        Task<DeleteResponse> DeleteAsync( params String[] fullPathes );

        Task<HttpResponseMessage> DeleteAsync( DeleteRequest requestBody, String bucketName );

        Task<MoveOrCopyResponse> MoveAsync( String oldFullPath, String newFullPath );

        Task<MoveOrCopyResponse> CopyAsync( String oldFullPath, String newFullPath );

        Task<RenameResponse> RenameAsync( String oldFullPath, String newFullPath );

        Task LockFile( String fullPath );

        Task UnlockFile( String fullPath );

        Task DownloadFileAsync( String bucketName, String prefix, String localFolderPath, String localOriginalName, ObjectDescriptionModel objectDescription, CancellationToken cancellationToken = default );

        Task<ServerObjectDescription> GetExistingObjectDescription( String objectFullPath );
    }
}

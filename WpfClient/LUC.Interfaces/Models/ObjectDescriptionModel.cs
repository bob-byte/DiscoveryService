using LUC.Interfaces.Abstract;
using LUC.Interfaces.Extensions;

using System;
using System.Diagnostics;

namespace LUC.Interfaces.Models
{
    [DebuggerDisplay( "Guid = {Guid} OrigName = {OriginalName} ObjKey = {ObjectKey} IsDel = {IsDeleted}" )]
    public class ObjectDescriptionModel : AbstractServerObjectDescription
    {
        public String OriginalName { get; set; }

        public Boolean IsDeleted { get; set; }

        public String Md5 { get; set; }

        public DateTime LockModifiedDateTimeUtc { get; set; }

        public String LockUserId { get; set; }

        public String LockUserTel { get; set; }

        public String LockUserName { get; set; }

        public Boolean IsLocked { get; set; }

        public DownloadingFileInfo ToDownloadingFileInfo(
            String serverBucketName,
            String fullLocalFilePath,
            String fileHexPrefix )
        {
            DownloadingFileInfo downloadingFileInfo = AppSettings.Mapper.Map<DownloadingFileInfo>( this );

            ICurrentUserProvider currentUserProvider = AppSettings.ExportedValue<ICurrentUserProvider>();
            downloadingFileInfo.BucketId = currentUserProvider.LocalBucketIdentifier( serverBucketName );
            downloadingFileInfo.ServerBucketName = serverBucketName;

            downloadingFileInfo.LocalFilePath = fullLocalFilePath;
            downloadingFileInfo.PathWhereDownloadFileFirst = PathExtensions.TempFullFileNameForDownload( fullLocalFilePath, currentUserProvider.RootFolderPath );
            downloadingFileInfo.FileHexPrefix = fileHexPrefix;

            return downloadingFileInfo;
        }
    }
}

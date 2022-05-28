using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using Serilog;

using System;
using System.IO;
using System.Linq;

namespace LUC.Interfaces
{
    public static class ApiContractExtensions
    {
        public static ObjectDescriptionModel ToObjectDescriptionModel( this ObjectFileDescriptionSubResponse item )
        {
            var result = new ObjectDescriptionModel
            {
                Guid = item.Guid,
                Version = item.Version,
                ObjectKey = item.ObjectKey,
                OriginalName = item.OriginalName,
                IsDeleted = item.IsDeleted,
                Md5 = item.Md5,
                LastModifiedDateTimeUtc = item.LastModifiedUtc.FromUnixTimeStampToDateTime(),
                LockModifiedDateTimeUtc = item.LockModifiedUtc.GetValueOrDefault().FromUnixTimeStampToDateTime(),
                ByteCount = item.Bytes,
                IsLocked = item.IsLocked,
                LockUserId = item.LockUserId,
                LockUserTel = item.LockUserTel,
                LockUserName = item.LockUserName
            };
            return result;
        }

        public static ObjectsListModel ToObjectsListModel( this ObjectsListResponse response )
        {
            var result = new ObjectsListModel();
            foreach ( ObjectFileDescriptionSubResponse item in from item in response.ObjectFileDescriptions
                                                               where item.OriginalName.IsSupportableFileName()
                                                               select item )
            {
                if ( result.ObjectDescriptions.Any( x => x.Guid == item.Guid ) )
                {
                    continue;
                }

                var model = item.ToObjectDescriptionModel();

                if ( String.IsNullOrEmpty( item.Guid ) )
                {
                    Log.Warning( "item.Guid in method ToObjectsListModel is empty" );
                }

                if ( item.IsLocked && ( item.LockUserId == null || item.LockUserName == null || item.LockUserTel == null || item.LockModifiedUtc == null ) )
                {
                    Log.Warning( item.OriginalName + " is locked but at least one locked key is empty" );
                }

                if ( item.LockModifiedUtc.HasValue && item.LockModifiedUtc.Value.FromUnixTimeStampToDateTime().Year == 1 )
                {
                    Log.Warning( item.OriginalName + " have incorrect lock date" );
                }

                result.ObjectDescriptions.Add( model );
            }

            //strange directories
            foreach ( ObjectDirectoryDescriptionSubResponse item in response.Directories )
            {
                if ( !item.HexPrefix.LastHexPrefixPart().FromHexString().HasWindowsReservedCharachters() )
                {
                    result.DirectoryDescriptions.Add( new DirectoryDescriptionModel
                    {
                        IsDeleted = item.IsDeleted,
                        Prefix = item.HexPrefix,
                        StringName = new DirectoryInfo( item.HexPrefix.FromHexString() ).Name,
                        PrefixFromHexString = item.HexPrefix.FromHexString()
                    } );
                }
            }

            result.ServerUtc = response.ServerUtc.FromUnixTimeStampToDateTime();
            result.RequestedPrefix = response.RequestedPrefix;

            return result;
        }
    }
}

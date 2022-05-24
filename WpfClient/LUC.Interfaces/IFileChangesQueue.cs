﻿using LUC.Interfaces.Models;

using System;
using System.IO;
using System.Threading.Tasks;

namespace LUC.Interfaces
{
    public interface IFileChangesQueue
    {
        void AddEvent( FileSystemEventArgs args );

        Task HandleLockedFilesAsync();

        Task HandleDownloadedNotMovedFilesAsync();

        Boolean IsPathAvaliableInActiveList( String path );

        void AddDownloadedNotMovedFile( DownloadingFileInfo downloadingFileInfo );

        void TryRemoveDownloadedNotMovedFile( DownloadingFileInfo downloadingFileInfo, out Boolean isRemoved );

        void Clear();
    }
}

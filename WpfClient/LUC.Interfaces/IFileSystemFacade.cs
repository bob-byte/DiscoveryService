using System;

namespace LUC.Interfaces
{
    public interface IFileSystemFacade
    {
        ISyncingObjectsList SyncingObjectsList { get; }

        Boolean IsObjectHandling( String fullObjectName );

        void RunMonitoring();

        void StopMonitoring();
    }
}

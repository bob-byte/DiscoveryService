using System;
using System.Collections.Generic;

namespace LUC.Interfaces
{
    public interface IPathFiltrator
    {
        IList<String> FoldersToIgnore { get; }

        Boolean IsPathPertinent( String fullPath );

        void ReadFromSettings();

        void AddNewFoldersToIgnore( params String[] newFoldersToIgnore );

        void UpdateSubFoldersToIgnore( IList<String> pathes );

        void UpdateCurrentBuckets( List<String> pathes );
    }
}

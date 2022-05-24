using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LUC.Services.Implementation
{
    [Export( typeof( IPathFiltrator ) )]
    public class PathFiltrator : IPathFiltrator
    {
        private readonly Object m_lockObject = new Object();

        private readonly ISettingsService m_settingsService;
        private readonly ILoggingService m_loggingService;

        private List<String> m_currentBuckets = new List<String>();

        [ImportingConstructor]
        public PathFiltrator( ISettingsService settingsService, ILoggingService loggingService )
        {
            m_settingsService = settingsService;
            m_loggingService = loggingService;
        }

        public Boolean IsPathPertinent( String fullPath )
        {
            lock ( m_lockObject )
            {
                return !FoldersToIgnore.Any( f => fullPath.Contains( f ) ) && m_currentBuckets.Any( b => fullPath.Contains( b ) );
            }
        }

        public IList<String> FoldersToIgnore { get; private set; } = new List<String>();

        public void AddNewFoldersToIgnore( params String[] newFoldersToIgnore )
        {
            //check parameter(has any elements and each one is not null or white space)
            if ( newFoldersToIgnore.Any() && newFoldersToIgnore.All( newIgnoredFolder => !String.IsNullOrWhiteSpace( newIgnoredFolder ) ) )
            {
                //if everything is right then lock and filter items which belong to m_currentBuckets
                lock ( m_lockObject )
                {
                    IEnumerable<String> newIgnoredFoldersWhichBelongsToAnyBucket = newFoldersToIgnore.Where( newIgnoredFolder => m_currentBuckets.Any( bucketPath => bucketPath.Contains( newIgnoredFolder ) ) );

                    //if count of filtered elements is more than 0, then
                    //add range to FoldersToIgnore and rewrite all folders to ignore in the app settings 
                    if ( newIgnoredFoldersWhichBelongsToAnyBucket.Any() )
                    {
                        foreach ( String newIgnoredFolder in newIgnoredFoldersWhichBelongsToAnyBucket )
                        {
                            FoldersToIgnore.Add( newIgnoredFolder );
                        }

                        m_settingsService.WriteFoldersToIgnore( FoldersToIgnore );
                    }
                }
            }
            else
            {
                throw new ArgumentException( message: "Doesn\'t has any element or any of them is null or white space", nameof( newFoldersToIgnore ) );
            }
        }

        public void UpdateCurrentBuckets( List<String> pathes )
        {
            Boolean isAnyDifferentPath;

            lock ( m_lockObject )
            {
                isAnyDifferentPath = !m_currentBuckets.SequenceEqual( pathes );
                if ( isAnyDifferentPath )
                {
                    m_currentBuckets = pathes;
                }
            }

            if ( isAnyDifferentPath )
            {
                try
                {
                    IDiscoveryService discoveryService = AppSettings.ExportedValue<IDiscoveryService>();

                    DsBucketsSupported.Define( m_settingsService.CurrentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported );
                    discoveryService.ReplaceAllBuckets( bucketsSupported );
                }
                catch ( Exception ex )
                {
                    m_loggingService.LogCriticalError( ex );
                }
            }
        }

        public void ReadFromSettings()
        {
            lock ( m_lockObject )
            {
                FoldersToIgnore = m_settingsService.ReadFoldersToIgnore();
            }
        }

        public void UpdateSubFoldersToIgnore( IList<String> pathes )
        {
            lock ( m_lockObject )
            {
                FoldersToIgnore = pathes;
            }

            m_settingsService.WriteFoldersToIgnore( pathes );
        }
    }
}

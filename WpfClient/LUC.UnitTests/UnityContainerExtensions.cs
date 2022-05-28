using LUC.DiscoveryServices;
using LUC.Interfaces;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Models;
using LUC.Services.Implementation;

using Moq;

using Prism.Events;
using Prism.Mef.Events;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Unity;
using Unity.Lifetime;

namespace LUC.UnitTests
{
    public static partial class UnityContainerExtensions
    {
        public static void SetupWithoutDs( this IUnityContainer unityContainer, String testRootFolderPath )
        {
            AppSettings.SetExportValueProvider( new ExportedValueProviderAdapter( unityContainer ) );

            var userProvider = new CurrentUserProvider
            {
                RootFolderPath = testRootFolderPath
            };

            ISettingsService settingsService = new SettingsService
            {
                CurrentUserProvider = userProvider
            };
            _ = unityContainer.RegisterInstance( settingsService );

            ILoggingService loggingService = new LoggingService
            {
                SettingsService = settingsService
            };
            _ = unityContainer.RegisterInstance( loggingService );

            userProvider.LoggingService = loggingService;

            IPathFiltrator pathFiltator = new PathFiltrator( settingsService, loggingService );
            _ = unityContainer.RegisterInstance( pathFiltator );

            userProvider.PathFiltrator = pathFiltator;
            _ = unityContainer.RegisterInstance<ICurrentUserProvider>( userProvider );

            ISyncingObjectsList syncingObjectsList = new SyncingObjectsList( userProvider );
            _ = unityContainer.RegisterInstance( syncingObjectsList );

            IEventAggregator eventAggregator = new MefEventAggregator();
            _ = unityContainer.RegisterInstance( eventAggregator );

            IBackgroundSynchronizer backgroundSynchronizer = new BackgroundSynchronizer( eventAggregator, pathFiltator );
            _ = unityContainer.RegisterInstance( backgroundSynchronizer );

            IApiClient apiClient = new ApiClient.ApiClient( userProvider, loggingService, syncingObjectsList, settingsService );
            _ = unityContainer.RegisterInstance( apiClient );

            IFileChangesQueue fileChangesQueue = new FileChangesQueue( eventAggregator, loggingService, userProvider, apiClient );
            _ = unityContainer.RegisterInstance( fileChangesQueue );
        }

        public static void Setup( this IUnityContainer unityContainer, String testRootFolderPath )
        {
            unityContainer.SetupWithoutDs( testRootFolderPath );

            var currentUserProvider = unityContainer.Resolve<ICurrentUserProvider>();
            var settingsService = unityContainer.Resolve<ISettingsService>();

            IDiscoveryService discoveryService = DiscoveryServiceFacade.FullyInitialized( currentUserProvider, settingsService );
            unityContainer.RegisterInstance( discoveryService );
        }
    }
}

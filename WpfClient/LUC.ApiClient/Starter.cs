using LUC.Services.Implementation;
using Prism.Events;

namespace LUC.ApiClient
{
    public class Starter
    {
        static Starter()
        {
            var eventAggregator = new EventAggregator();
            var aesCryptographyService = new AesCryptographyService();
            var client = new ApiClient(null, null); // TODO Think up how properly should be references to realization.
            var currentUserProvider = new CurrentUserProvider();
            var settingsService = new SettingsService();
            var pathFiltrator = new PathFiltrator(settingsService);
            var syncingObjectsList = new SyncingObjectsList();
            var loggingService = new LoggingService();
            var fileChangesQueue = new FileChangesQueue(eventAggregator, loggingService);
            var fileSystemFacade = new FileSystemFacade(pathFiltrator);
            var backgroundSynchronizer = new BackgroundSynchronizer(eventAggregator, pathFiltrator);
        }

        //public static void GetStartTimeForOperation(String fullPath, 
        //    out DateTime nowUtc, out DateTime originalModifiedDateTime, out String timeStamp)
        //{
        //    nowUtc = DateTime.UtcNow;
        //    originalModifiedDateTime = DateTimeExtensions.LastWriteTimeUtcWithCorrectOffset(fullPath);
        //    timeStamp = originalModifiedDateTime.FromDateTimeToUnixTimeStamp().ToString();
        //}
    }
}

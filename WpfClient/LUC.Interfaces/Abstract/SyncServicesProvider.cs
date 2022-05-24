namespace LUC.Interfaces.Abstract
{
    public abstract class SyncServicesProvider : ApiSettingsProvider
    {
        protected SyncServicesProvider( IApiClient apiClient, ObjectNameProvider objectNameProvider )
            : base( apiClient.Settings, objectNameProvider )
        {
            SyncingObjectsList = apiClient.SyncingObjectsList;
            ApiClient = apiClient;
        }

        public ISyncingObjectsList SyncingObjectsList { get; }

        public IApiClient ApiClient { get; }
    }
}

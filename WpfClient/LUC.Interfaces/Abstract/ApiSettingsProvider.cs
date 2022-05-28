namespace LUC.Interfaces.Abstract
{
    public abstract class ApiSettingsProvider
    {
        protected ApiSettings m_apiSettings;

        public ILoggingService LoggingService { get; set; }

        public ICurrentUserProvider CurrentUserProvider { get; set; }

        public ObjectNameProvider ObjectNameProvider { get; set; }

        protected ApiSettingsProvider( ApiSettings apiSettings, ObjectNameProvider objectNameProvider )
        {
            m_apiSettings = apiSettings;
            ObjectNameProvider = objectNameProvider;
            LoggingService = ObjectNameProvider.LoggingService;
            CurrentUserProvider = ObjectNameProvider.CurrentUserProvider;
        }
    }
}

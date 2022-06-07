using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

using AutoFixture;

using LUC.DiscoveryServices;
using LUC.Interfaces;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;

using NUnit.Framework;

using Unity;
using Unity.Lifetime;

namespace LUC.UnitTests
{
    [SetUpFixture]
    public class SetUpTests
    {
        public const String DEFAULT_USER_LOGIN_FOR_TEST = "integration1";

        public static String SyncFolder { get; private set; }

        public static IUnityContainer UnityContainer { get; protected set; }

        public static IFixture Fixture { get; protected set; }

        public static IApiClient ApiClient { get; set; }

        public static ICurrentUserProvider CurrentUserProvider { get; protected set; }

        public static ISettingsService SettingsService { get; protected set; }

        public static ILoggingService LoggingService { get; protected set; }

        public static ISyncingObjectsList SyncingObjectsList { get; protected set; }

        public static void SetupServicesContainer( String syncFolder ) =>
            UnityContainer.Setup( syncFolder );

        public static void Init( String login, Action<String> setupServicesContainer )
        {
            Fixture = new Fixture();

            AppSettings appSettings = Services.Implementation.SettingsService.AppSettingsFromFile();
            UserSetting testUserSettings = appSettings.SettingsPerUser.SingleOrDefault( c => c.Login.Equals( login, StringComparison.Ordinal ) );
            Boolean isTestUserLoginned = testUserSettings != null;

            SyncFolder = isTestUserLoginned ? testUserSettings.RootFolderPath : String.Empty;

            UnityContainer = new UnityContainer();
            setupServicesContainer( SyncFolder );

            SettingsService = AppSettings.ExportedValue<ISettingsService>();
            LoggingService = AppSettings.ExportedValue<ILoggingService>();
            CurrentUserProvider = AppSettings.ExportedValue<ICurrentUserProvider>();
            ApiClient = AppSettings.ExportedValue<IApiClient>();
            SyncingObjectsList = AppSettings.ExportedValue<ISyncingObjectsList>();
        }

        [OneTimeSetUp]
        public void Init() =>
            Init( DEFAULT_USER_LOGIN_FOR_TEST, SetupServicesContainer );

        protected internal async static Task<(ApiClient.ApiClient apiClient, LoginResponse loginResponse, ICurrentUserProvider userProvider)> LoginAsync(
            String login = "integration1",
            String password = "integration1" )
        {
            LoginResponse loginResponse = await ApiClient.LoginAsync( login, password ).ConfigureAwait( continueOnCapturedContext: false );

            SyncFolder = CurrentUserProvider.RootFolderPath;
            return (ApiClient as ApiClient.ApiClient, loginResponse, CurrentUserProvider);
        }
    }
}

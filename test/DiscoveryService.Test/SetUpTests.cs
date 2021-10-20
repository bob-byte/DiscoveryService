using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using AutoFixture;

using LightClient;

using LUC.Interfaces;
using LUC.Services.Implementation;

using Moq;

using NUnit.Framework;

namespace LUC.DiscoveryService.Test
{
    [SetUpFixture]
    static class SetUpTests
    {
        private static ICurrentUserProvider s_currentUserProvider = null;
        private static DiscoveryService s_discoveryService = null;

        public static ISettingsService s_settingsService;

        public static DiscoveryService DiscoveryService
        {
            get
            {
                if ( s_discoveryService == null )
                {
                    s_settingsService = new SettingsService
                    {
                        CurrentUserProvider = CurrentUserProvider
                    };

                    s_currentUserProvider.RootFolderPath = s_settingsService.ReadUserRootFolderPath();
                    DsBucketsSupported.Define( CurrentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported );

                    s_discoveryService = new DiscoveryService( new ServiceProfile( UseIpv4, UseIpv6, DefaultProtocolVersion, bucketsSupported ), s_currentUserProvider );
                }

                return s_discoveryService;
            }
        }

        public static UInt16 DefaultProtocolVersion { get; set; } = 1;

        public static Boolean UseIpv4 { get; set; } = true;

        public static Boolean UseIpv6 { get; set; } = true;

        public static Boolean IsUserLogged { get; set; } = false;

        public static ICurrentUserProvider CurrentUserProvider 
        { 
            get
            {
                if(!IsUserLogged)
                {
                    LUC.Interfaces.OutputContracts.LoginResponse loginResponse;
                    (_, loginResponse, s_currentUserProvider) = LoginAsync().GetAwaiter().GetResult();

                    IsUserLogged = loginResponse.IsSuccess;
                    if(!IsUserLogged)
                    {
                        throw new InvalidOperationException( "Cannot login" );
                    }
                }

                return s_currentUserProvider;
            }
        }

        internal static LoggingService LoggingService { get; private set; } = new LoggingService
        {
            SettingsService = new SettingsService()
        };

        internal async static Task<(ApiClient.ApiClient apiClient, LUC.Interfaces.OutputContracts.LoginResponse loginResponse, ICurrentUserProvider userProvider)> LoginAsync( String login = "integration1", String password = "integration1" )
        {
            ICurrentUserProvider currentUserProvider = new CurrentUserProvider();

            ApiClient.ApiClient apiClient = new ApiClient.ApiClient( currentUserProvider, LoggingService );
            apiClient.SyncingObjectsList = new SyncingObjectsList();

            LUC.Interfaces.OutputContracts.LoginResponse loginResponse = await apiClient.LoginAsync( login, password ).ConfigureAwait( continueOnCapturedContext: false );

            return (apiClient, loginResponse, currentUserProvider);
        }

        [ OneTimeSetUp ]
        public static void AssemblyInitialize()
        {

            //set logger factory
            //LoggingService = new LoggingService
            //{
            //    SettingsService = new SettingsService()
            //};
        }
    }
}

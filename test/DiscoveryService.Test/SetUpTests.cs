using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using AutoFixture;

using LUC.Interfaces;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;

using Moq;

using NUnit.Framework;

namespace LUC.DiscoveryService.Test
{
    [SetUpFixture]
    static class SetUpTests
    {
        private static ICurrentUserProvider s_currentUserProvider;
        private static DiscoveryService s_discoveryService = null;

        private static ISettingsService s_settingsService;

        static SetUpTests()
        {
            Fixture = new Fixture();

            s_currentUserProvider = null;
            s_discoveryService = null;

            DefaultProtocolVersion = 1;

            LoggingService = new LoggingService
            {
                SettingsService = new SettingsService()
            };

            UseIpv4 = true;
            UseIpv6 = true;
        }

        public static IFixture Fixture { get; }

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

        public static UInt16 DefaultProtocolVersion { get; set; }

        public static Boolean UseIpv4 { get; set; }

        public static Boolean UseIpv6 { get; set; }

        public static Boolean IsUserLogged { get; set; }

        public static ICurrentUserProvider CurrentUserProvider 
        { 
            get
            {
                if(!IsUserLogged)
                {
                    LoginResponse loginResponse;
                    (_, loginResponse, s_currentUserProvider) = LoginAsync().GetAwaiter().GetResult();

                    IsUserLogged = loginResponse.IsSuccess;
                    if(!IsUserLogged)
                    {
                        throw new InvalidOperationException( "Cannot login" );
                    }
                }

                return s_currentUserProvider;
            }

            set => s_currentUserProvider = value;
        }

        internal static LoggingService LoggingService { get; private set; }

        internal async static Task<(ApiClient.ApiClient apiClient, LoginResponse loginResponse, ICurrentUserProvider userProvider)> LoginAsync( 
            String login = "integration1", 
            String password = "integration1" )
        {
            ICurrentUserProvider currentUserProvider = new CurrentUserProvider
            {
                LoggingService = LoggingService,
                //RootFolderPath = LoggingService.SettingsService.ReadUserRootFolderPath()
            };

            ApiClient.ApiClient apiClient = new ApiClient.ApiClient( currentUserProvider, LoggingService )
            {
                SyncingObjectsList = new SyncingObjectsList()
            };

            LoginResponse loginResponse = await apiClient.LoginAsync( login, password ).ConfigureAwait( continueOnCapturedContext: false );

            return (apiClient, loginResponse, currentUserProvider);
        }
    }
}

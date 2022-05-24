﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AutoFixture;

using DiscoveryServices.Common;
using LUC.Interfaces;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;
using LUC.UnitTests;

using Nito.AsyncEx.Synchronous;

using NUnit.Framework;

using Unity;
using Unity.Lifetime;

namespace DiscoveryServices.Test
{
    [SetUpFixture]
    class DsSetUpTests : SetUpTests
    {
        private static DiscoveryService s_discoveryService = null;

        private static ISettingsService s_settingsService;

        static DsSetUpTests()
        {
            Init( DEFAULT_USER_LOGIN_FOR_TEST, SetupServicesContainerWithoutDs );

            DefaultProtocolVersion = 1;

            UseIpv4 = true;
            UseIpv6 = true;

            AppSettings.AddNewMap<ObjectDescriptionModel, DownloadingFileInfo>();
        }

        //public static String Login { get; }

        //public static String Password { get; }

        public static DiscoveryService DiscoveryService
        {
            get
            {
                if ( s_discoveryService == null )
                {
                    s_settingsService = AppSettings.ExportedValue<ISettingsService>();

                    CurrentUserProvider.RootFolderPath = s_settingsService.ReadUserRootFolderPath();
                    DsBucketsSupported.Define( CurrentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported );

                    s_discoveryService = DiscoveryService.Instance( new ServiceProfile( MachineId.Create(), UseIpv4, UseIpv6, DefaultProtocolVersion, bucketsSupported ), CurrentUserProvider );
                }

                return s_discoveryService;
            }
        }

        public static UInt16 DefaultProtocolVersion { get; set; }

        public static Boolean UseIpv4 { get; set; }

        public static Boolean UseIpv6 { get; set; }

        [OneTimeSetUp]
        public Task SetUpTests()
        {
            var currentUserProvider = UnityContainer.Resolve<ICurrentUserProvider>();
            var settingsService = UnityContainer.Resolve<ISettingsService>();

            IDiscoveryService discoveryService = DiscoveryServiceFacade.FullyInitialized( currentUserProvider, settingsService );
            UnityContainer.RegisterInstance( discoveryService );

            return LoginAsync();
        }

        [ OneTimeTearDown ]
        public void TearDownTests() => DiscoveryService?.Stop();

        public static void TestOfChangingStateInTime(
            Int32 countOfNewThreads,
            Action initTest,
            Action opWhichIsExecutedByThreadSet,
            Action opWhichMainThreadExecutes,
            TimeSpan timeThreadSleep,
            TimeSpan precisionOfExecution
        )
        {
            initTest();

            var timeExecution = TimeSpan.FromMilliseconds( timeThreadSleep.TotalMilliseconds * countOfNewThreads );
            TimeSpan waitSecondsForThread = timeThreadSleep;

            var newThreads = new Thread[ countOfNewThreads ];
            var allThreadIsEnded = new AutoResetEvent( initialState: false );
            Object lockSetWaitSecondsForThread = new Object();

            Task.Factory.StartNew( () =>
            {
                for ( Int32 numThread = 0; numThread < countOfNewThreads; numThread++ )
                {
                    newThreads[ numThread ] = new Thread( ( state ) =>
                    {
                        DateTime start = DateTime.Now;
                        opWhichIsExecutedByThreadSet();
                        DateTime end = DateTime.Now;

                        lock ( lockSetWaitSecondsForThread )
                        {
                            TimeSpan realTimeWaitToChangeState = end.Subtract( start );
                            waitSecondsForThread = TimeSpan.FromMilliseconds( waitSecondsForThread.TotalMilliseconds + timeThreadSleep.TotalMilliseconds );
                            TimeSpan realPrecisionForThread = realTimeWaitToChangeState.Subtract( waitSecondsForThread );

                            if ( realPrecisionForThread > precisionOfExecution )
                            {
                                Assert.Fail( message: $"{nameof( realPrecisionForThread )} = {realPrecisionForThread}, but precision is {precisionOfExecution}" );
                            }
                        }

                        Boolean isLastIteration = ( ( timeExecution.TotalMilliseconds - precisionOfExecution.TotalMilliseconds ) <= waitSecondsForThread.TotalMilliseconds ) &&
                               ( waitSecondsForThread.TotalMilliseconds <= ( timeExecution.TotalMilliseconds + precisionOfExecution.TotalMilliseconds ) );
                        if ( isLastIteration )
                        {
                            allThreadIsEnded.Set();
                        }
                    } );
                    newThreads[ numThread ].Start( numThread );
                }
            } );

            Int32 countOfOpExecution = countOfNewThreads;
            for ( Int32 numSettingIsInPool = 0; numSettingIsInPool < countOfOpExecution; numSettingIsInPool++ )
            {
                Thread.Sleep( timeThreadSleep );
                opWhichMainThreadExecutes();
            }

            Boolean isExecutedLastIterationInTime = allThreadIsEnded.WaitOne( (Int32)timeThreadSleep.TotalMilliseconds * ( countOfNewThreads + 2 ) );
            //isExecutedLastIterationInTime.Should().BeTrue();
        }

        //DS is setup in functional tests or in DsSetUpTests.SetUpTests
        private static void SetupServicesContainerWithoutDs( String syncFolder ) =>
            UnityContainer.SetupWithoutDs( syncFolder );
    }
}
using System;

using AutoFixture;

using LUC.Interfaces;
using LUC.Services.Implementation;
using NUnit.Framework;

namespace LUC.DiscoveryService.Test
{
    [SetUpFixture]
    static class SetUpTests
    {
        public static Boolean UseIpv4 { get; set; } = true;

        public static Boolean UseIpv6 { get; set; } = true;

        internal static LoggingService LoggingService { get; private set; } = new LoggingService
        {
            SettingsService = new SettingsService()
        };

        [OneTimeSetUp]
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

using AutoMapper;
using AutoMapper.Configuration;

using LUC.Interfaces;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.IO;

namespace LUC.Interfaces.Models
{
    public class AppSettings : IAppSettings
    {
        //locks Mapper initialization and adding new map
        private static readonly Object s_lockObject;

        private static IMapper s_mapper;

        private static IExportValueProvider s_exportValueProvider;

        public static readonly Int32 SecondsToCacheServerList = Convert.ToInt32(ConfigurationManager.AppSettings[name: "SecondsToCacheServerList"]);

        public static readonly Int32 SecondsToСheckChangedFiles = Convert.ToInt32(ConfigurationManager.AppSettings["SecondsToСheckChangedFiles"]);

        public static readonly String FilePath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "LightUponCloud", "appsettings.json" );

        public static readonly String SentryDsn = ConfigurationManager.AppSettings["SentryDsn"];

        public static readonly String RestApiHost = ConfigurationManager.AppSettings["RestApiHost"];

        public static readonly TimeSpan FlushToDiskInterval = new TimeSpan(hours: 0, minutes: 5, seconds: 0);

        public static readonly String LocalAppData;

        public static readonly String PathToLogFiles;

        static AppSettings()
        {
            LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            PathToLogFiles = Path.Combine(LocalAppData, $"LightUponCloudLogs");

            s_lockObject = new Object();

            MapperConfigurationExpression = new MapperConfigurationExpression();
            AddNewMapWithoutThreadSync<ObjectFileDescriptionSubResponse, ObjectDescriptionModel>();
            AddNewMapWithoutThreadSync<ObjectDescriptionModel, DownloadingFileInfo>();
        }

        public AppSettings()
        {
            SettingsPerUser = new List<UserSetting>();
            IsShowConsole = false;
            IsLogToTxtFile = true;
            MachineId = String.Empty;
        }

        [JsonIgnore]
        public static MapperConfigurationExpression MapperConfigurationExpression { get; private set; }

        [JsonIgnore]
        public static IMapper Mapper
        {
            get => s_mapper;
            set
            {
                lock (s_lockObject)
                {
                    s_mapper = value;
                }
            }
        }

        public static void SetExportValueProvider( IExportValueProvider exportValueProvider) =>
            SingletonInitializer.ThreadSafeInit( value: () => exportValueProvider, s_lockObject, ref s_exportValueProvider );

        /// <remarks>
        /// Using this method you can get any import of LUC service you want
        /// </remarks>
        /// <typeparam name="T">
        /// Service type
        /// </typeparam>
        /// <returns>
        /// Exported value of service
        /// </returns>
        public static T ExportedValue<T>()
        {
            if (s_exportValueProvider != null)
            {
                T exportedValue = s_exportValueProvider.ExportedValue<T>();
                return exportedValue;
            }
            else
            {
                throw new InvalidOperationException(message: $"Before you should call {nameof(SetExportValueProvider)} with not null value");
            }
        }

        public List<UserSetting> SettingsPerUser { get; set; }

        public Boolean IsShowConsole { get; set; }

        public Boolean IsLogToTxtFile { get; set; }

        public String MachineId { get; set; }

        public String LanguageCulture { get; set; }

        public static void AddNewMap<TSource, TDestionation>()
        {
            lock ( s_lockObject )
            {
                AddNewMapWithoutThreadSync<TSource, TDestionation>();
            }
        }

        private static void AddNewMapWithoutThreadSync<TSource, TDestionation>()
        {
            _ = MapperConfigurationExpression.CreateMap<TSource, TDestionation>();

            var mapperConfig = new MapperConfiguration( MapperConfigurationExpression );
            Mapper = mapperConfig.CreateMapper();
        }
    }
}

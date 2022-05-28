using LUC.Common.PrismEvents;
using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Services.Implementation;

using Prism.Commands;
using Prism.Events;

using Serilog;
using Serilog.Events;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace LUC.WpfClient
{
    [Export]
    public class ShellViewModel
    {
        #region Constructors

        [ImportingConstructor]
        public ShellViewModel(IEventAggregator eventAggregator, ISettingsService settingsService, INavigationManager navigationManager)
        {
            navigationManager.SetAppCurrentDispatcher(Application.Current.Dispatcher);

            StaticMessages = new ObservableCollection<String>();

            InitSerilog(settingsService);
            SetUpCurrentCultures(settingsService.ReadLanguageCulture());

            MinimizeCommand = new DelegateCommand(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
                Application.Current.MainWindow.ShowInTaskbar = false;
            });

            ChangeFolderForMonitoringCommand = new DelegateCommand(() => eventAggregator.GetEvent<RequestChangeFolderForMonitoringEvent>().Publish(true));

            _ = eventAggregator.GetEvent<NeedsToBeMinimizedEvent>().Subscribe((Boolean param) => MinimizeCommand.Execute());
        }

        #endregion

        public ObservableCollection<String> StaticMessages { get; set; }

        public DelegateCommand MinimizeCommand { get; private set; }

        public DelegateCommand ChangeFolderForMonitoringCommand { get; private set; }

        private static void SetUpCurrentCultures(String settingsCulture)
        {
            CultureInfo cultureInfo = String.IsNullOrEmpty( settingsCulture ) ? CultureInfo.CurrentCulture : new CultureInfo( settingsCulture );

            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
            TranslationSource.Instance.CurrentCulture = cultureInfo;
        }

        private static void InitSerilog(ISettingsService settingsService, String logId = "")
        {
            String logFileNamePostfix = logId + DateTime.Today.ToString("d").Replace('/', '.');

            LoggerConfiguration loggerConfig = LoggerConfigExtension.BaseLucLoggerConfig( logFileNamePostfix, settingsService.IsLogToTxtFile );
            loggerConfig = loggerConfig.WriteTo.Sink( new LoggerToServer( OsVersionHelper.Version() ), LogEventLevel.Warning );

            Log.Logger = loggerConfig.CreateLogger();

            Log.Information("Serilog succesfully initialized. :)");
        }
    }
}

using LUC.DiscoveryServices;
using LUC.Interfaces;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Models;
using LUC.WpfClient.Views;

using Prism.Mef;
using Prism.Modularity;

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace LUC.WpfClient
{
    [Export]
    public sealed class CustomBootstrapper : MefBootstrapper
    {
        protected override void ConfigureAggregateCatalog()
        {
            base.ConfigureAggregateCatalog();

            AggregateCatalog.Catalogs.Add( new AssemblyCatalog( typeof( CustomBootstrapper ).Assembly ) );

            var directoryCatalog = new DirectoryCatalog( Environment.CurrentDirectory, "*.dll" );
            AggregateCatalog.Catalogs.Add( directoryCatalog );

            RegisterDefaultTypesIfMissing();
        }

        protected override DependencyObject CreateShell()
        {
            var shellView = Container.GetExportedValue<ShellView>();

            AppSettings.SetExportValueProvider( new ExportedValueProviderAdapter( Container ) );
            InitDiscoveryService();

            return shellView;
        }

        protected override void InitializeShell()
        {
            Application.Current.MainWindow = (ShellView)Shell;

            CultureInfo cultureInfo = AppCultureInfo();
            Application.Current.MainWindow.Language = XmlLanguage.GetLanguage( cultureInfo.IetfLanguageTag );

            Application.Current.MainWindow.Show();
        }

        private CultureInfo AppCultureInfo()
        {
            ISettingsService settingsService = AppSettings.ExportedValue<ISettingsService>();
            String settingsCulture = settingsService.ReadLanguageCulture();

            CultureInfo cultureInfo = String.IsNullOrEmpty( settingsCulture ) ? CultureInfo.CurrentCulture : new CultureInfo( settingsCulture );
            return cultureInfo;
        }

        protected override void ConfigureModuleCatalog()
        {
            Type module1Type = typeof( MainModule );
            String path = module1Type.Assembly.Location;

            ModuleCatalog.AddModule( new ModuleInfo
            {
                InitializationMode = InitializationMode.WhenAvailable,
                ModuleType = typeof( MainModule ).FullName,
                ModuleName = nameof( MainModule ),
                Ref = new Uri( path, UriKind.RelativeOrAbsolute ).AbsoluteUri
            } );

            base.ConfigureModuleCatalog();
        }

        private void InitDiscoveryService()
        {
            var currentUserProvider = Container.GetExportedValue<ICurrentUserProvider>();
            var settingsService = Container.GetExportedValue<ISettingsService>();

            IDiscoveryService discoveryService = DiscoveryServiceFacade.InitWithoutForceToStart( currentUserProvider, settingsService );
            Container.ComposeExportedValue( discoveryService );
        }
    }
}

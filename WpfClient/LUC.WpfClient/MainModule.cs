using LUC.Interfaces.Constants;
using LUC.WpfClient.Views;

using Prism.Mef.Modularity;
using Prism.Modularity;
using Prism.Regions;

using System.ComponentModel.Composition;

namespace LUC.WpfClient
{
    [ModuleExport( typeof( MainModule ) )]
    public class MainModule : IModule
    {
        [Import( typeof( IRegionManager ) )]
#pragma warning disable CS0649 // Параметру IRegionManager не присваивается значение
#pragma warning disable IDE0044 // Добавить модификатор только для чтения
        private IRegionManager m_regionManager;
#pragma warning restore IDE0044 // Добавить модификатор только для чтения
#pragma warning restore CS0649

        public void Initialize()
        {
            _ = m_regionManager.RegisterViewWithRegion( RegionNames.SHELL_REGION, typeof( LoginView ) );
            _ = m_regionManager.RegisterViewWithRegion( RegionNames.SHELL_REGION, typeof( DesktopView ) );
            _ = m_regionManager.RegisterViewWithRegion( RegionNames.SHELL_REGION, typeof( PasswordForEncryptionKeyView ) );
            _ = m_regionManager.RegisterViewWithRegion( RegionNames.SHELL_REGION, typeof( SelectFoldersForIgnoreView ) );

            var parameters = new NavigationParameters { { NavigationParameterNames.IS_NAVIGATION_FROM_MAIN_MODULE, true } };
            m_regionManager.RequestNavigate( RegionNames.SHELL_REGION, ViewNames.LOGIN_VIEW_NAME, parameters );
        }
    }
}

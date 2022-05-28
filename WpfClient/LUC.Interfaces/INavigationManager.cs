using System;
using System.Windows.Threading;

namespace LUC.Interfaces
{
    public interface INavigationManager
    {
        void SetAppCurrentDispatcher( Dispatcher dispatcher );

        void TryNavigateToDesktopView();

        void NavigateToLoginView();

        void NavigateToDesktopView();

        void NavigateToSelectFoldersForIgnoreView();

        void TrySelectSyncFolder( out Boolean isUserSelectedRightPath, out String syncFolder );
    }
}

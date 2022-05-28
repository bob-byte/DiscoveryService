using LUC.Interfaces;
using LUC.ViewModels.Models;

using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace LUC.ViewModels
{
    [Export]
    public class SelectFoldersForIgnoreViewModel : BindableBase, INavigationAware
    {
        private readonly ICurrentUserProvider m_currentUserProvider;
        private readonly IPathFiltrator m_pathFiltrator;
        private readonly INavigationManager m_navigationManager;

        [ImportingConstructor]
        public SelectFoldersForIgnoreViewModel( IPathFiltrator pathFiltrator, ICurrentUserProvider currentUserProvider, INavigationManager navigationManager )
        {
            this.m_pathFiltrator = pathFiltrator;
            this.m_currentUserProvider = currentUserProvider;
            this.m_navigationManager = navigationManager;
        }

        private IList<SelectableFolderDescription> m_buckets;
        public IList<SelectableFolderDescription> Buckets
        {
            get => m_buckets;
            set
            {
                m_buckets = value;
                RaisePropertyChanged( nameof( Buckets ) );
            }
        }

        public System.Boolean IsNavigationTarget( NavigationContext navigationContext ) => true;

        public void OnNavigatedFrom( NavigationContext navigationContext )
        {
            //Investigate
        }

        public void OnNavigatedTo( NavigationContext navigationContext )
        {
            m_pathFiltrator.ReadFromSettings(); // TODO When to read?
            Buckets = m_currentUserProvider.ProvideBucketDirectoryPaths().Select( x => new SelectableFolderDescription( x, m_pathFiltrator.FoldersToIgnore ) ).ToList();
            foreach ( SelectableFolderDescription bucket in Buckets.Where( bucket => m_pathFiltrator.FoldersToIgnore.Contains( bucket.FullPath ) ) )
            {
                bucket.SetAsUnselected();
            }
        }

        private DelegateCommand m_okCommand;
        public DelegateCommand OkCommand
        {
            get
            {
                if ( m_okCommand == null )
                {
                    m_okCommand = new DelegateCommand( ExecuteOkCommand );
                }

                return m_okCommand;
            }
        }

        private DelegateCommand m_cancelCommand;
        public DelegateCommand CancelCommand
        {
            get
            {
                if ( m_cancelCommand == null )
                {
                    m_cancelCommand = new DelegateCommand( ExecuteCancelCommand );
                }

                return m_cancelCommand;
            }
        }

        private void ExecuteOkCommand()
        {
            var unselectedFolders = new List<System.String>();

            GatherUnselectedPathes( unselectedFolders, Buckets );

            m_pathFiltrator.UpdateSubFoldersToIgnore( unselectedFolders );

            m_navigationManager.TryNavigateToDesktopView();
        }

        private void ExecuteCancelCommand() => m_navigationManager.TryNavigateToDesktopView();

        private void GatherUnselectedPathes( IList<System.String> currentResult, IList<SelectableFolderDescription> children )
        {
            if ( children == null )
            {
                return;
            }

            foreach ( SelectableFolderDescription item in children )
            {
                if ( !item.IsSelected )
                {
                    currentResult.Add( item.FullPath );
                }

                GatherUnselectedPathes( currentResult, item.Children );
            }
        }
    }
}

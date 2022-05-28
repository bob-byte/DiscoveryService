using Prism.Mvvm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LUC.ViewModels.Models
{
    public class SelectableFolderDescription : BindableBase
    {
        private readonly IList<String> m_unselectedForInitialize;
        public SelectableFolderDescription( String path, IList<String> unselectedForInitialize )
        {
            FullPath = path;
            this.m_unselectedForInitialize = unselectedForInitialize;
            RecognizeChildren();
        }

        private Boolean m_isSelected = true;
        public Boolean IsSelected
        {
            get => m_isSelected;
            set
            {
                m_isSelected = value;
                RaisePropertyChanged( nameof( IsSelected ) );

                if ( Children != null )
                {
                    foreach ( SelectableFolderDescription child in Children )
                    {
                        child.IsEnabled = m_isSelected;
                    }
                }
            }
        }

        private Boolean m_isEnabled = true;
        public Boolean IsEnabled
        {
            get => m_isEnabled;
            set
            {
                m_isEnabled = value;
                RaisePropertyChanged( nameof( IsEnabled ) );

                if ( Children != null )
                {
                    foreach ( SelectableFolderDescription child in Children )
                    {
                        child.IsEnabled = m_isEnabled;
                    }
                }
            }
        }

        public String FullPath { get; private set; }

        private Boolean m_isExpanded;
        public Boolean IsExpanded
        {
            get => m_isExpanded;
            set
            {
                m_isExpanded = value;

                if ( m_isExpanded )
                {
                    foreach ( SelectableFolderDescription item in Children )
                    {
                        item.RecognizeChildren();
                    }
                }

                RaisePropertyChanged( nameof( IsExpanded ) );
            }
        }

        private IList<SelectableFolderDescription> m_children;
        public IList<SelectableFolderDescription> Children
        {
            get => m_children;
            set
            {
                m_children = value;
                RaisePropertyChanged( nameof( Children ) );
            }
        }

        private void RecognizeChildren()
        {
            if ( Children != null )
            {
                return;
            }

            Children = Directory.EnumerateDirectories( FullPath ).Select( x => new SelectableFolderDescription( x, m_unselectedForInitialize ) ).ToList();
            foreach ( SelectableFolderDescription child in Children.Where( child => m_unselectedForInitialize.Contains( child.FullPath ) ) )
            {
                child.SetAsUnselected();
            }
        }

        internal void SetAsUnselected()
        {
            m_isSelected = false;
            RaisePropertyChanged( nameof( IsSelected ) );
        }
    }
}

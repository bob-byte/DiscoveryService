using LUC.ViewModels;

using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace LUC.WpfClient.Views
{
    [Export]
    public partial class SelectFoldersForIgnoreView : UserControl
    {
        public SelectFoldersForIgnoreView()
        {
            InitializeComponent();
        }

        [Import]
        public SelectFoldersForIgnoreViewModel ViewModel
        {
            set => DataContext = value;
        }
    }
}

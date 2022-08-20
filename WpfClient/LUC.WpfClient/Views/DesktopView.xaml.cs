using LUC.ViewModels;

using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;

namespace LUC.WpfClient.Views
{
    [Export]
    public partial class DesktopView : UserControl
    {
        [Import]
        public DesktopViewModel ViewModel
        {
            set => DataContext = value;
        }

        public DesktopView()
        {
            InitializeComponent();
        }
    }
}

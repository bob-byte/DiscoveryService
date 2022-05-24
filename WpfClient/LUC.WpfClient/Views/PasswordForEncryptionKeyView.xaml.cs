using LUC.ViewModels;

using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace LUC.WpfClient.Views
{
    [Export]
    public partial class PasswordForEncryptionKeyView : UserControl
    {
        public PasswordForEncryptionKeyView()
        {
            InitializeComponent();
        }

        [Import]
        public PasswordForEncryptionKeyViewModel ViewModel
        {
            set => DataContext = value;
        }
    }
}

using LUC.ViewModels;

using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace LUC.WpfClient.Views
{
    [Export]
    public partial class LoginView
    {
        public LoginView()
        {
            InitializeComponent();
        }

        [Import]
        public LoginViewModel ViewModel
        {
            set => DataContext = value;
        }

        private void PasswordBox_PasswordChanged( System.Object sender, System.Windows.RoutedEventArgs e ) => ( DataContext as LoginViewModel ).Password = ( sender as PasswordBox ).Password;

        private void PasswordBox_Loaded( System.Object sender, System.Windows.RoutedEventArgs e ) => _passwordBox.Password = System.String.Empty;
    }
}

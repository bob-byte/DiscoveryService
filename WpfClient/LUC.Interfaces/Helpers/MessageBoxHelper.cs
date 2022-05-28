using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;

using Microsoft.Win32;

using System;
using System.Windows;

namespace LUC.Interfaces.Helpers
{
    public static class MessageBoxHelper
    {
        public static MessageBoxResult ShowMessageBox( String message, String caption ) =>
            ShowMessageBox( message, caption, MessageBoxButton.OK );

        public static MessageBoxResult ShowMessageBox( String message, String caption, MessageBoxButton buttons )
        {
            MessageBoxResult messageBoxResult = default;
            Application.Current.Dispatcher.Invoke( () =>
            {
                Style defaultStyle = DefaultAppStyle();
                messageBoxResult = Xceed.Wpf.Toolkit.MessageBox.Show( Application.Current.MainWindow, message, caption, buttons, defaultStyle );
            } );

            return messageBoxResult;
        }
            

        private static Style DefaultAppStyle()
        {
            var myResourceDictionary = new ResourceDictionary
            {
                Source = new Uri( "/LUC.WpfClient;component/StylesDictionary.xaml", UriKind.RelativeOrAbsolute )
            };

            var defaultStyle = myResourceDictionary[ "DefaultMessageBoxStyle" ] as Style;

            return defaultStyle;
        }

        public static void ShowSelectFile() => Application.Current.Dispatcher.Invoke( () =>
                                              {
                                                  var collectionControlDialog = new Xceed.Wpf.Toolkit.CollectionControlDialog( typeof( OpenFileDialog ) );
                                                  collectionControlDialog.Show();
                                              } );
    }
}

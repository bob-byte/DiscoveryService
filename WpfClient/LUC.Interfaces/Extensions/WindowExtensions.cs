using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LUC.Interfaces.Extensions
{
    public static class WindowExtensions
    {
        public static void BringToForeground( this Window window)
        {
            if ( window.WindowState == WindowState.Minimized || window.Visibility == Visibility.Hidden )
            {
                window.Show();
                window.WindowState = WindowState.Normal;
            }

            // According to some sources these steps gurantee that an app will be brought to foreground.
            _ = window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            _ = window.Focus();
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace Hardcodet.Wpf.TaskbarNotification.Interop
{
    /// <summary>
    /// Win32 API imports.
    /// </summary>
    internal static class WinApi
    {
        /// <summary>
        /// Creates, updates or deletes the taskbar icon.
        /// </summary>
        [DllImport( "shell32.Dll", CharSet = CharSet.Unicode )]
        public static extern Boolean Shell_NotifyIcon( NotifyCommand cmd, [In] ref NOTIFYICONDATA data );

        /// <summary>
        /// Creates the helper window that receives messages from the taskar icon.
        /// </summary>
        [DllImport( "USER32.DLL", EntryPoint = "CreateWindowExW", SetLastError = true )]
        public static extern IntPtr CreateWindowEx( Int32 dwExStyle, [MarshalAs( UnmanagedType.LPWStr )] String lpClassName,
            [MarshalAs( UnmanagedType.LPWStr )] String lpWindowName, Int32 dwStyle, Int32 x, Int32 y,
            Int32 nWidth, Int32 nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance,
            IntPtr lpParam );

        /// <summary>
        /// Processes a default windows procedure.
        /// </summary>
        [DllImport( "USER32.DLL" )]
        public static extern IntPtr DefWindowProc( IntPtr hWnd, UInt32 msg, IntPtr wparam, IntPtr lparam );

        /// <summary>
        /// Registers the helper window class.
        /// </summary>
        [DllImport( "USER32.DLL", EntryPoint = "RegisterClassW", SetLastError = true )]
        public static extern Int16 RegisterClass( ref WindowClass lpWndClass );

        /// <summary>
        /// Registers a listener for a window message.
        /// </summary>
        /// <param name="lpString"></param>
        /// <returns></returns>
        [DllImport( "User32.Dll", EntryPoint = "RegisterWindowMessageW" )]
        public static extern UInt32 RegisterWindowMessage( [MarshalAs( UnmanagedType.LPWStr )] String lpString );

        /// <summary>
        /// Used to destroy the hidden helper window that receives messages from the
        /// taskbar icon.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport( "USER32.DLL", SetLastError = true )]
        public static extern Boolean DestroyWindow( IntPtr hWnd );

        /// <summary>
        /// Gives focus to a given window.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport( "USER32.DLL" )]
        public static extern Boolean SetForegroundWindow( IntPtr hWnd );

        /// <summary>
        /// Gets the maximum number of milliseconds that can elapse between a
        /// first click and a second click for the OS to consider the
        /// mouse action a double-click.
        /// </summary>
        /// <returns>The maximum amount of time, in milliseconds, that can
        /// elapse between a first click and a second click for the OS to
        /// consider the mouse action a double-click.</returns>
        [DllImport( "user32.dll", CharSet = CharSet.Auto, ExactSpelling = true )]
        public static extern Int32 GetDoubleClickTime();

        /// <summary>
        /// Gets the screen coordinates of the current mouse position.
        /// </summary>
        [DllImport( "USER32.DLL", SetLastError = true )]
        public static extern Boolean GetPhysicalCursorPos( ref Point lpPoint );

        [DllImport( "USER32.DLL", SetLastError = true )]
        public static extern Boolean GetCursorPos( ref Point lpPoint );
    }
}
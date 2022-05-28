using System;
using System.Runtime.InteropServices;

namespace Hardcodet.Wpf.TaskbarNotification.Interop
{
    /// <summary>
    /// Callback delegate which is used by the Windows API to
    /// submit window messages.
    /// </summary>
    public delegate IntPtr WindowProcedureHandler( IntPtr hwnd, UInt32 uMsg, IntPtr wparam, IntPtr lparam );

    /// <summary>
    /// Win API WNDCLASS struct - represents a single window.
    /// Used to receive window messages.
    /// </summary>
    [StructLayout( LayoutKind.Sequential )]
    public struct WindowClass
    {
        public UInt32 style;
        public WindowProcedureHandler lpfnWndProc;
        public Int32 cbClsExtra;
        public Int32 cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs( UnmanagedType.LPWStr )] public String lpszMenuName;
        [MarshalAs( UnmanagedType.LPWStr )] public String lpszClassName;
    }
}
using Microsoft.Win32.SafeHandles;

using Serilog;

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LUC.Services.Implementation.Helpers
{
    // https://developercommunity.visualstudio.com/content/problem/12166/console-output-is-gone-in-vs2017-works-fine-when-d.html
    static class ConsoleHelper
    {
        private const Int32 MY_CODE_PAGE = 65001;
        private const UInt32 GENERIC_WRITE = 0x40000000;
        private const UInt32 FILE_SHARE_WRITE = 0x2;
        private const UInt32 OPEN_EXISTING = 0x3;

        // P/Invoke required:
        private const UInt32 STD_OUTPUT_HANDLE = 0xFFFFFFF5;

        [DllImport( "kernel32.dll" )]
        private static extern IntPtr GetStdHandle( UInt32 nStdHandle );

        [DllImport( "kernel32.dll" )]
        private static extern void SetStdHandle( UInt32 nStdHandle, IntPtr handle );

        [DllImport( "kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall )]
        private static extern Int32 AllocConsole();

        [DllImport( "kernel32.dll", SetLastError = true )]
        private static extern IntPtr CreateFile( String lpFileName, UInt32 dwDesiredAccess, UInt32 dwShareMode, UInt32 lpSecurityAttributes, UInt32 dwCreationDisposition, UInt32 dwFlagsAndAttributes, UInt32 hTemplateFile );

        internal static void CreateConsole()
        {
            AllocConsole();

            try
            {
                /// Console.OpenStandardOutput eventually calls into GetStdHandle. As per MSDN documentation of GetStdHandle: https://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx
                /// will return the redirected handle and not the allocated console:
                /// "The standard handles of a process may be redirected by a call to SetStdHandle, in which case GetStdHandle returns
                /// the redirected handle. If the standard handles have been redirected, you can specify the CONIN$ value in a call 
                /// to the CreateFile function to get a handle to a console's input buffer.
                /// Similarly, you can specify the CONOUT$ value to get a handle to a console's active screen buffer."
                /// Get the handle to CONOUT$.    
                IntPtr stdHandle = CreateFile( "CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0 );
                var safeFileHandle = new SafeFileHandle( stdHandle, true );
                var fileStream = new FileStream( safeFileHandle, FileAccess.Write );
                var encoding = Encoding.GetEncoding( MY_CODE_PAGE );
                var standardOutput = new StreamWriter( fileStream, encoding )
                {
                    AutoFlush = true
                };
                Console.SetOut( standardOutput );

                Console.OutputEncoding = Encoding.UTF8;
#if DEBUG
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
#endif
                //Console.OutputEncoding = Encoding.GetEncoding( "Cyrillic" );
                //Console.InputEncoding = Encoding.GetEncoding( "Cyrillic" );
            }
            catch ( Exception e )
            {
                Log.Error( e, "Can't create console." );
            }
        }
    }
}

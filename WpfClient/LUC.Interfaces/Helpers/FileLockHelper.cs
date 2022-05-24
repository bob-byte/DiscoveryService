using System;
using System.Runtime.InteropServices;

namespace LUC.Interfaces.Helpers
{
    static class FileLockHelper
    {
        const Int32 ERROR_SHARING_VIOLATION = 32;
        const Int32 ERROR_LOCK_VIOLATION = 33;

        public static Boolean IsFileLocked( Exception exception )
        {
            Int32 errorCode = Marshal.GetHRForException( exception ) & ( ( 1 << 16 ) - 1 );
            return errorCode == ERROR_SHARING_VIOLATION || errorCode == ERROR_LOCK_VIOLATION;
        }
    }
}

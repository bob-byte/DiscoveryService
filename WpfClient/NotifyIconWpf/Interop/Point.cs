using System.Runtime.InteropServices;

namespace Hardcodet.Wpf.TaskbarNotification.Interop
{
    /// <summary>
    /// Win API struct providing coordinates for a single point.
    /// </summary>
    [StructLayout( LayoutKind.Sequential )]
    public struct Point
    {
        /// <summary>
        /// X coordinate.
        /// </summary>
        public System.Int32 X;
        /// <summary>
        /// Y coordinate.
        /// </summary>
        public System.Int32 Y;
    }
}
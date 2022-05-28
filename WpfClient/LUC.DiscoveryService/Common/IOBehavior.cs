using System;

namespace LUC.DiscoveryServices.Common
{
    /// <summary>
    /// Specifies whether to perform synchronous or asynchronous I/O.
    /// </summary>
    public enum IoBehavior : Byte
    {
        /// <summary>
        /// Use synchronous I/O.
        /// </summary>
        Synchronous,

        /// <summary>
        /// Use asynchronous I/O.
        /// </summary>
        Asynchronous,
    }
}

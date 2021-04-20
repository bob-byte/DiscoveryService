using System;

namespace LUC.DiscoveryService
{
    static class Lock
    {
        internal static readonly Object lockService;
        internal static readonly Object lockGroupsSupported;
        internal static readonly Object lockSendTcp;

        //if we don't use static constructor we will not actually know when fields are inizialized
        static Lock()
        {
            lockService = new Object();
            lockGroupsSupported = new Object();
            lockSendTcp = new Object();
        }

        internal static void InitWithLock<T>(Object locker, T value, ref T prop)
        {
            if (prop == null)
            {
                lock (locker)
                {
                    if (prop == null)
                    {
                        prop = value;
                    }
                }
            }
        }
    }
}

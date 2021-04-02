using System;

namespace DiscoveryServices
{
    static class Lock
    {
        internal static readonly Object lockerService;
        internal static readonly Object lockerCurrentPeer;

        //if we don't use static constructor we will not actuually know when fields are inizialized
        static Lock()
        {
            lockerService = new Object();
            lockerCurrentPeer = new Object();
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

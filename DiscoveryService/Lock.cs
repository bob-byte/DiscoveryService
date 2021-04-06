using System;

namespace DiscoveryServices
{
    static class Lock
    {
        internal static readonly Object lockService;
        internal static readonly Object lockCurrentPeer;
        internal static readonly Object lockChangeKnownPeers;

        //if we don't use static constructor we will not actuually know when fields are inizialized
        static Lock()
        {
            lockService = new Object();
            lockCurrentPeer = new Object();
            lockChangeKnownPeers = new Object();
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

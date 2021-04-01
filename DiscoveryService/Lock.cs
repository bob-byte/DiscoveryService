using System;

namespace DiscoveryServices
{
    static class Lock
    {
        internal static readonly Object lockId;
        internal static readonly Object lockIp;
        internal static readonly Object lockGroupsSupported;
        internal static readonly Object lockPort;
        internal static readonly Object lockServerPort;
        internal static readonly Object lockerService;
        internal static readonly Object lockerAddPeer;
        internal static readonly Object lockerCurrentPeer;

        //if we don't use static constructor we will not actuually know when fields are inizialized
        static Lock()
        {
            lockId = new Object();
            lockIp = new Object();
            lockGroupsSupported = new Object();
            lockPort = new Object();
            lockServerPort = new Object();
            lockerService = new Object();
            lockerAddPeer = new Object();
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

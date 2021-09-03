using System;

namespace LUC.DiscoveryService.Kademlia
{
	public static class Constants
	{
        public const Int32 MaxChunkSize = 2000000;

        public const int B = 5;
	public const int K = 20;
	public const int ID_LENGTH_BYTES = 20;
	public const int ID_LENGTH_BITS = 160;
        public const int MaxCheckAvailableData = 20;//FindValue can be too long if look up algorithm is started

        public const int MAX_THREADS = 4;
        public const int QUERY_TIME = 500;  // in ms.
        public const int RESPONSE_WAIT_TIME = 1 * 1000;   // in ms.

#if DEBUG       // For unit tests
        public const int ALPHA = 20;
        public const double BUCKET_REFRESH_INTERVAL = 30 * 60 * 1000;       // every hour.
        public const double KEY_VALUE_REPUBLISH_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double KEY_VALUE_EXPIRE_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double ORIGINATOR_REPUBLISH_INTERVAL = 24 * 60 * 60 * 1000;       // every 24 hours in milliseconds.
        public const int EXPIRATION_TIME_SECONDS = 24 * 60 * 60;                // every 24 hours in seconds.
        public const int EVICTION_LIMIT = 3;
#else
        public const int ALPHA = 3;
        public const double BUCKET_REFRESH_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double KEY_VALUE_REPUBLISH_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double KEY_VALUE_EXPIRE_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double ORIGINATOR_REPUBLISH_INTERVAL = 24 * 60 * 60 * 1000;       // every 24 hours in milliseconds.
        public const int EXPIRATION_TIME_SECONDS = 24 * 60 * 60;                // every 24 hours in seconds.
        public const int EVICTION_LIMIT = 5;
#endif

        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(value: 3);
        public static readonly TimeSpan DisconnectTimeout = TimeSpan.FromMinutes(3);
        public static readonly TimeSpan SendTimeout = TimeSpan.FromMinutes(3);
        public static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMinutes(3);

        /// <summary>
        /// it's max of execution Kademlia operation (first we connect to new contact, then if it is failed, we will be failed to send, then we will try to connect and send again. If we send actually, we will try to receive 
        /// </summary>
        /// <value>
        /// <see cref="ConnectTimeout"/> + <see cref="SendTimeout"/> + <see cref="ConnectTimeout"/> + <see cref="SendTimeout"/> + <see cref="ReceiveTimeout"/>
        /// </value>
        public static readonly TimeSpan TimeWaitReturnToPool = ConnectTimeout + SendTimeout + ConnectTimeout + SendTimeout + ReceiveTimeout;

        public static readonly TimeSpan TimeCheckDataToRead = TimeSpan.FromSeconds(1);

        public const String LastSeenFormat = "yyyy-MM-dd HH:mm:ss";

    }
}

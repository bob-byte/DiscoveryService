using System;
using System.Net;

using LUC.DiscoveryService.Common;

namespace LUC.DiscoveryService.Common
{
    static class Constants
    {
        public const Int32 MAX_CHUNK_SIZE = 2000000;

        public const Int32 B = 5;
        public const Int32 K = 20;
        public const Int32 ID_LENGTH_BYTES = 20;
        public const Int32 ID_LENGTH_BITS = 160;
        public const Int32 MAX_CHECK_AVAILABLE_DATA = 20;//FindValue can be too long if look up algorithm is started

        public const Int32 MAX_THREADS = 4;
        public const Int32 QUERY_TIME = 500;  // in ms.
        public const Int32 RESPONSE_WAIT_TIME = 1 * 1000;   // in ms.

#if DEBUG       // For unit tests
        public const Int32 ALPHA = 20;
        public const Double BUCKET_REFRESH_INTERVAL = 30 * 60 * 1000;       // every half-hour.
        public const Double KEY_VALUE_REPUBLISH_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const Double KEY_VALUE_EXPIRE_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const Double ORIGINATOR_REPUBLISH_INTERVAL = 24 * 60 * 60 * 1000;       // every 24 hours in milliseconds.
        public const Int32 EXPIRATION_TIME_SECONDS = 24 * 60 * 60;                // every 24 hours in seconds.
        public const Int32 EVICTION_LIMIT = 3;
#else
        public const int ALPHA = 3;
        public const double BUCKET_REFRESH_INTERVAL = 30 * 60 * 1000;       // every half-hour.
        public const double KEY_VALUE_REPUBLISH_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double KEY_VALUE_EXPIRE_INTERVAL = 60 * 60 * 1000;       // every hour.
        public const double ORIGINATOR_REPUBLISH_INTERVAL = 24 * 60 * 60 * 1000;       // every 24 hours in milliseconds.
        public const int EXPIRATION_TIME_SECONDS = 24 * 60 * 60;                // every 24 hours in seconds.
        public const int EVICTION_LIMIT = 3;
#endif

        //TODO return from TimeSpan.FromSeconds to TimeSpan.FromMinutes
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds( value: 3 );
        public static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds( 3 );
        public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds( 3 );
        public static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds( 3 );

        /// <summary>
        /// it's max of execution Kademlia operation (first we connect to new contact, then if it is failed, we will be failed to send, 
        /// then we will try to connect and send again. If we send actually, we will try to receive 
        /// </summary>
        /// <value>
        /// <see cref="ConnectTimeout"/> + <see cref="SendTimeout"/> + <see cref="ConnectTimeout"/> + <see cref="SendTimeout"/> + <see cref="ReceiveTimeout"/>
        /// </value>
        public static readonly TimeSpan TimeWaitReturnToPool = ConnectTimeout + ReceiveTimeout + SendTimeout + ConnectTimeout + SendTimeout + ReceiveTimeout;

        public static readonly TimeSpan TimeCheckDataToRead = TimeSpan.FromSeconds( 1 );

        public static readonly IPAddress MulticastAddressIp4;
        public static readonly IPAddress MulticastAddressIp6;

        public static readonly IPEndPoint MulticastEndpointIp4;
        public static readonly IPEndPoint MulticastEndpointIp6;

        public const String LAST_SEEN_FORMAT = "yyyy-MM-dd HH:mm:ss";

        public const Int32 MAX_AVAILABLE_IP_ADDRESSES_IN_CONTACT = 4;

        static Constants()
        {
            MulticastAddressIp4 = IPAddress.Parse( "224.0.0.251" );
            MulticastAddressIp6 = IPAddress.Parse( "FF02::FB" );

            MulticastEndpointIp4 = new IPEndPoint( MulticastAddressIp4, AbstractService.DEFAULT_PORT );
            MulticastEndpointIp6 = new IPEndPoint( MulticastAddressIp6, AbstractService.DEFAULT_PORT );
        }
    }
}

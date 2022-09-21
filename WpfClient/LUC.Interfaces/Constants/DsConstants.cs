//#define DS_TEST

using System;
using System.Net;

namespace LUC.Interfaces.Constants
{
    public static class DsConstants
    {
        /// <summary>
        /// Default port which is used in LAN
        /// </summary>
        public const UInt16 DEFAULT_PORT = 17500;

        public const String FILE_WITH_MACHINE_ID = "CurrentMachineId";
        public const String FILE_WITH_MACHINE_ID_EXTENSION = ".txt";

        public const String DOWNLOAD_TEST_NAME_FOLDER = "DownloadTest";

        /// <value>
        /// 2 MB
        /// </value>
        public const Int32 MAX_CHUNK_SIZE = 2000000;

        /// <summary>
        /// <a href="http://www.regatta.cs.msu.su/doc/usr/share/man/info/ru_RU/a_doc_lib/aixbman/prftungd/2365c93.htm">
        /// Performance tuning guide
        /// </a>
        /// </summary>
        public const Int32 MAX_CHUNK_READ_PER_ONE_TIME = 4096;

        public const Int32 MAX_AVAILABLE_READ_BYTES = (Int32)( MAX_CHUNK_SIZE * 1.5 );

        //TODO: define whether it is valid value
        public const Int32 B = 5;
        public const Int32 K = 3;
        public const Int32 KID_LENGTH_BYTES = 20;
        public const Int32 KID_LENGTH_BITS = 160;
        public const Int32 MAX_CHECK_AVAILABLE_DATA = 40;//FindValue can be too long if look up algorithm is started

        public const Int32 MAX_THREADS = 4;
        public const Int32 QUERY_TIME = 500;  // in ms.
        public const Int32 RESPONSE_WAIT_TIME = 1 * 1000;   // in ms.





#if DEBUG
        public const Int32 ALPHA = 20;

#if DS_TEST
        public const Double BUCKET_REFRESH_INTERVAL = 5 * 1000;       // every 5 seconds.
#else
        public const Double BUCKET_REFRESH_INTERVAL = 10 * 60 * 1000;       // every 10 minutes.
#endif

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

        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds( value: 3 );
        public static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds( 3 );
        public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds( 3 );
        public static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds( 10 );
        public static readonly TimeSpan ReceiveOneChunkTimeout = TimeSpan.FromSeconds(6);

#if CONNECTION_POOL_TEST
        public static readonly TimeSpan IntervalFindNewServices = TimeSpan.FromSeconds( value: 3 );
#else
        public static readonly TimeSpan IntervalFindNewServices = TimeSpan.FromMinutes( 1 );
#endif

        /// <summary>
        /// it's max of execution Kademlia operation (first we connect to new contact, then if it is failed, we will be failed to send, 
        /// then we will try to connect and send again. If we send actually, we will try to receive.
        /// </summary>
        /// <remarks>
        /// We also add 0.3 second is in order to only one thread always uses socket
        /// </remarks>
        /// <value>
        /// 3 seconds + <seealso cref="ConnectTimeout"/> + <seealso cref="SendTimeout"/> + <seealso cref="ConnectTimeout"/> + <seealso cref="SendTimeout"/> + <seealso cref="ReceiveTimeout"/> + <seealso cref="TimeCheckDataToRead"/> * <seealso cref="MAX_CHECK_AVAILABLE_DATA"/>
        /// </value>
        public static readonly TimeSpan TimeWaitSocketReturnedToPool;

        public static readonly TimeSpan TimeCheckDataToRead = TimeSpan.FromSeconds( 0.25 );

        public static readonly IPAddress MulticastAddressIpv4;
        public static readonly IPAddress MulticastAddressIpv6;

        public static readonly IPEndPoint MulticastEndpointIpv4;
        public static readonly IPEndPoint MulticastEndpointIpv6;

        public const String LAST_SEEN_FORMAT = "yyyy-MM-dd HH:mm:ss";

        static DsConstants()
        {
            MulticastAddressIpv4 = IPAddress.Parse( ipString: "239.255.0.251" );
            MulticastAddressIpv6 = IPAddress.Parse( "FF02::D" );

            MulticastEndpointIpv4 = new IPEndPoint( MulticastAddressIpv4, DEFAULT_PORT );
            MulticastEndpointIpv6 = new IPEndPoint( MulticastAddressIpv6, DEFAULT_PORT );

            TimeWaitSocketReturnedToPool = TimeSpan.FromSeconds( 0.3 ) + ConnectTimeout + ReceiveTimeout + SendTimeout + ConnectTimeout + SendTimeout + ReceiveTimeout + TimeSpan.FromSeconds( TimeCheckDataToRead.TotalSeconds * MAX_CHECK_AVAILABLE_DATA );

        }
    }
}

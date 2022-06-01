using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace LUC.Interfaces.Discoveries
{
    /// <summary>
    /// Defines methods for discover another nodes in local network
    /// </summary>
    public interface IDiscoveryService
    {
        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        String MachineId { get; }

        Boolean IsRunning { get; }

        /// <summary>
        ///   Protocol version.
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
        UInt16 ProtocolVersion { get; }

        ICurrentUserProvider CurrentUserProvider { get; }

        IContact OurContact { get; }

        /// <summary>
        /// Groups which current peer belongs to.
        /// Key is a local name of bucket, which current peer supports.
        /// Value is path to a SSL certificate of group
        /// </summary>
        ConcurrentDictionary<String, String> LocalBuckets { get; }

        List<IContact> OnlineContacts();

        /// <summary>
        /// Adds new bucket, which current node belongs to.
        /// </summary>
        /// <remarks>
        /// It also adds <paramref name="bucketLocalName"/> to <seealso cref="IContact"/>
        /// </remarks>
        /// <param name="bucketLocalName">
        /// Name of bucket local name
        /// </param>
        /// <param name="pathToSslCert">
        /// Path to SSL-certificate
        /// </param>
        /// <param name="isAdded">
        /// Does bucket is successfully added to DS and <seealso cref="NetworkEventInvoker.OurContact"/>. 
        /// Returns <a href="false"/> if this instance and <seealso cref="NetworkEventInvoker.OurContact"/> 
        /// already supports <paramref name="bucketLocalName"/>, else returns <a href="true"/>
        /// </param>
        void TryAddNewBucket( String bucketLocalName, String pathToSslCert, out Boolean isAdded );

        void ReplaceAllBuckets( IDictionary<String, String> bucketsWithPathToSslCert );

        /// <summary>
        /// Removes <paramref name="bucketLocalName"/> which current node belong to.
        /// </summary>
        /// <param name="bucketLocalName">
        /// Local name of bucket
        /// </param>
        /// <param name="isRemoved">
        /// Is bucket successfully removed from DS and <seealso cref="NetworkEventInvoker.OurContact"/>
        /// </param>
        void TryRemoveBucket( String bucketLocalName, out Boolean isRemoved );

        void ClearAllLocalBuckets();

        /// <summary>
        /// Start listening TCP, UDP messages and sending them
        /// </summary>
        void Start();

        /// <summary>
        /// Sends multicast message
        /// </summary>
        void TryFindAllNodes();

        /// <summary>
        /// Stop listening and sending TCP and UDP messages
        /// </summary>
        void Stop();
    }
}

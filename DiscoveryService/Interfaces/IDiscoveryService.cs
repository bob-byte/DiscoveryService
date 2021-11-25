using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Interfaces
{
    /// <summary>
    /// Defines methods for discover another nodes in local network
    /// </summary>
    public interface IDiscoveryService
    {
        /// <summary>
        /// IP address of peers which were discovered.
        /// Key is a network in a format "IP-address:port".
        /// Value is a list of group names, which peer supports
        /// </summary>
        ConcurrentDictionary<EndPoint, String> KnownIps { get; }

        /// <summary>
        /// Groups which current peer belongs to.
        /// Key is a local name of bucket, which current peer supports.
        /// Value is path to a SSL certificate of group
        /// </summary>
        ConcurrentDictionary<String, String> LocalBuckets();

        /// <summary>
        /// Adds new bucket, which current node belongs to.
        /// </summary>
        /// <remarks>
        /// It also adds <paramref name="bucketLocalName"/> to <seealso cref="Contact"/>
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
        void TryAddNewBucketLocalName( String bucketLocalName, String pathToSslCert, out Boolean isAdded);


        /// <summary>
        /// Removes <paramref name="bucketLocalName"/> which current node belong to.
        /// </summary>
        /// <param name="bucketLocalName">
        /// Local name of bucket
        /// </param>
        /// <param name="isRemoved">
        /// Is bucket successfully removed from DS and <seealso cref="NetworkEventInvoker.OurContact"/>
        /// </param>
        void TryRemoveBucket(String bucketLocalName, out Boolean isRemoved);

        /// <summary>
        /// Start listening TCP, UDP messages and sending them
        /// </summary>
        void Start();

        /// <summary>
        /// Sends multicast message
        /// </summary>
        void QueryAllServices();

        /// <summary>
        /// Stop listening and sending TCP and UDP messages
        /// </summary>
        void Stop();
    }
}

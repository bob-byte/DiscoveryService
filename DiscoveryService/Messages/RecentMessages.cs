using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DiscoveryServices.Messages
{
    /// <summary>
    ///   Maintains a sequence of recent messages.
    /// </summary>
    /// <remarks>
    ///   <b>RecentMessages</b> is used to determine if a message has already been
    ///   processed within the specified <see cref="Interval"/>.
    /// </remarks>
    class RecentMessages
    {
        /// <summary>
        ///   Recent messages.
        /// </summary>
        /// <value>
        ///   The key is the Base64 encoding of the MD5 hash of 
        ///   a message and the value is when the message was seen.
        /// </value>
        public ConcurrentDictionary<BigInteger, DateTime> Messages { get; set; } = new ConcurrentDictionary<BigInteger, DateTime>();

        /// <summary>
        ///   The time interval used to determine if a message is recent.
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds( value: 5 );

        /// <summary>
        ///   Try adding a message to the recent message list.
        /// </summary>
        /// <param name="message">
        ///   The binary representation of a message.
        /// </param>
        /// <returns>
        ///   <b>true</b> if the message, did not already exist; otherwise,
        ///   <b>false</b> the message exists within the <see cref="Interval"/>.
        /// </returns>
        public Boolean TryAdd( BigInteger messageId )
        {
            Prune();
            return Messages.TryAdd( messageId, DateTime.UtcNow );
        }

        /// <summary>
        ///   Remove any messages that are stale.
        /// </summary>
        /// <returns>
        ///   The number messages that were pruned.
        /// </returns>
        /// <remarks>
        ///   Anything older than an <see cref="Interval"/> ago is removed.
        /// </remarks>
        public Int32 Prune()
        {
            DateTime dead = DateTime.UtcNow - Interval;
            Int32 count = 0;

            foreach ( KeyValuePair<BigInteger, DateTime> stale in Messages.Where( x => x.Value < dead ) )
            {
                if ( Messages.TryRemove( stale.Key, out _ ) )
                {
                    ++count;
                }
            }

            return count;
        }
    }
}

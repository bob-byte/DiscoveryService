using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    ///   Maintains a sequence of recent messages.
    /// </summary>
    /// <remarks>
    ///   <b>RecentMessages</b> is used to determine if a message has already been
    ///   processed within the specified <see cref="Interval"/>.
    /// </remarks>
    public class RecentMessages
    {
        /// <summary>
        ///   Recent messages.
        /// </summary>
        /// <value>
        ///   The key is the Base64 encoding of the MD5 hash of 
        ///   a message and the value is when the message was seen.
        /// </value>
        public ConcurrentDictionary<string, DateTime> Messages = new ConcurrentDictionary<UInt32, DateTime>();

        /// <summary>
        ///   The time interval used to determine if a message is recent.
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

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
        public bool TryAdd(UInt32 messageId)
        {
            Prune();
            return Messages.TryAdd(messageId, DateTime.Now);
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
        public int Prune()
        {
            var dead = DateTime.Now - Interval;
            var count = 0;

            foreach (var stale in Messages.Where(x => x.Value < dead))
            {
                if (Messages.TryRemove(stale.Key, out _))
                {
                    ++count;
                }
            }
            return count;
        }
    }
}

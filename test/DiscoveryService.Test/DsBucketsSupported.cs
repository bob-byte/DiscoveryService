using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LUC.Interfaces;

namespace LUC.DiscoveryService.Test
{
    class DsBucketsSupported
    {
        public static void Define(ICurrentUserProvider currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported)
        {
            IList<String> serverBuckets = currentUserProvider.GetServerBuckets();
            String sslCert = "<SSL-Cert>";

            bucketsSupported = new ConcurrentDictionary<String, String>();
            foreach ( String bucketOnServer in serverBuckets )
            {
                bucketsSupported.TryAdd( bucketOnServer, sslCert );
            }
        }
    }
}

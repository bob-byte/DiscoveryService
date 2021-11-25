using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LUC.Interfaces;

namespace LUC.DiscoveryServices.Test
{
    class DsBucketsSupported
    {
        public static void Define(ICurrentUserProvider currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported)
        {
            IEnumerable<String> serverBuckets = currentUserProvider.ProvideBucketDirectoryPathes().Select( c => Path.GetFileName( c ) );
            String sslCert = "<SSL-Cert>";

            bucketsSupported = new ConcurrentDictionary<String, String>();
            foreach ( String bucketOnServer in serverBuckets )
            {
                bucketsSupported.TryAdd( bucketOnServer, sslCert );
            }
        }
    }
}

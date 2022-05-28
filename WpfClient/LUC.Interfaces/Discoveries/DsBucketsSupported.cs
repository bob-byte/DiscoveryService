using System;
using System.Collections.Concurrent;
using System.Linq;

namespace LUC.Interfaces.Discoveries
{
    public static class DsBucketsSupported
    {
        public static void Define( ICurrentUserProvider currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported )
        {
            bucketsSupported = new ConcurrentDictionary<String, String>();

            if ( ( currentUserProvider != null ) && currentUserProvider.IsLoggedIn )
            {
                String sslCert = "<SSL-Cert>";

                foreach ( String bucketLocalName in currentUserProvider.LoggedUser.Groups.Select( c => c.Id ) )
                {
                    bucketsSupported.TryAdd( bucketLocalName, sslCert );
                }
            }
        }
    }
}

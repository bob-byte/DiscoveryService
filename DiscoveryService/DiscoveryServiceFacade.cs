using DiscoveryServices;
using DiscoveryServices.Common.Extensions;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Services.Implementation;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices
{
    public static class DiscoveryServiceFacade
    {
        public static DiscoveryService InitWithoutForceToStart( ICurrentUserProvider currentUserProvider, ISettingsService settingsService )
        {
            DsBucketsSupported.Define( currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported );

            var discoveryService = DiscoveryService.Instance(
                new ServiceProfile(
                    settingsService.MachineId,
                    GeneralConstants.PROTOCOL_VERSION,
                    bucketsSupported
                ),
                currentUserProvider
            );
            return discoveryService;
        }

        public static DiscoveryService FullyInitialized( ICurrentUserProvider currentUserProvider, ISettingsService settingsService )
        {
            DiscoveryService discoveryService = InitWithoutForceToStart( currentUserProvider, settingsService );

            DsBucketsSupported.Define( currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported );

            Boolean isReplacedBuckets;
            if( !discoveryService.LocalBuckets.Equals<String, String>( bucketsSupported ) )
            {
                discoveryService.ReplaceAllBuckets( bucketsSupported );
                isReplacedBuckets = true;
            }
            else
            {
                isReplacedBuckets = false;
            }

            if ( discoveryService.IsRunning && isReplacedBuckets )
            {
#if DEBUG
                discoveryService.TryFindAllNodes();
#endif
            }
            else if ( !discoveryService.IsRunning )
            {
                discoveryService.Start();
            }

            return discoveryService;
        }
    }
}

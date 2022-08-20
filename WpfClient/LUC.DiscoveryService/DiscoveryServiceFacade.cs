using LUC.DiscoveryServices;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;
using LUC.Services.Implementation;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices
{
    public static class DiscoveryServiceFacade
    {
        public static DiscoveryService InitWithoutForceToStart( ICurrentUserProvider currentUserProvider, ISettingsService settingsService )
        {
            DsBucketsSupported.Define( currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported );

            DiscoveryService discoveryService = InternalInitWithoutForceToStart( currentUserProvider, bucketsSupported, settingsService.MachineId );
            return discoveryService;
        }

        public static DiscoveryService FullyInitialized( ICurrentUserProvider currentUserProvider, ISettingsService settingsService )
        {
            DsBucketsSupported.Define(currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported);

            DiscoveryService discoveryService = InternalInitWithoutForceToStart( currentUserProvider, bucketsSupported, settingsService.MachineId );

            if ( discoveryService.IsRunning )
            {
#if DEBUG
                Boolean isReplacedBuckets = !bucketsSupported.Equals<String, String>(discoveryService.LocalBuckets);

                if ( isReplacedBuckets )
                {
                    discoveryService.TryFindAllNodes();
                }
#endif
            }
            else
            {
                discoveryService.Start();
            }

            return discoveryService;
        }

        private static DiscoveryService InternalInitWithoutForceToStart(ICurrentUserProvider currentUserProvider, ConcurrentDictionary<String, String> bucketsSupported, String machineId) =>
            DiscoveryService.Instance(
                machineId,
                GeneralConstants.PROTOCOL_VERSION,
                currentUserProvider,
                bucketsSupported
            );
    }
}

using LUC.DiscoveryServices;
using LUC.DiscoveryServices.Common.Extensions;
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

namespace LUC.DiscoveryServices
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
            DsBucketsSupported.Define(currentUserProvider, out ConcurrentDictionary<String, String> bucketsSupported);

            DiscoveryService discoveryService = InternalInitWithoutForceToStart( currentUserProvider, bucketsSupported, settingsService.MachineId );

            if ( discoveryService.IsRunning )
            {
#if DEBUG
                Boolean isReplacedBuckets = !bucketsSupported.SequenceEqual(discoveryService.LocalBuckets);

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

        private static DiscoveryService InternalInitWithoutForceToStart(ICurrentUserProvider currentUserProvider, ConcurrentDictionary<String, String> bucketsSupported, String machineId)
        {
            var discoveryService = DiscoveryService.Instance(
                new ServiceProfile(
                    machineId,
                    GeneralConstants.PROTOCOL_VERSION,
                    bucketsSupported
                ),
                currentUserProvider
            );
            return discoveryService;
        }
    }
}

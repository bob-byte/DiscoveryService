﻿using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Services.Implementation;

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LUC.DiscoveryServices.NetworkEventHandlers
{
    class CheckFileExistsRequestHandler : INetworkEventHandler
    {
        private readonly DiscoveryService m_discoveryService;

        protected readonly ISettingsService m_settingsService;
        protected readonly ICurrentUserProvider m_currentUserProvider;

        protected readonly Dht m_distributedHashTable;

        public CheckFileExistsRequestHandler( ICurrentUserProvider currentUserProvider, DiscoveryService discoveryService )
        {
            m_discoveryService = discoveryService;

            m_distributedHashTable = NetworkEventInvoker.DistributedHashTable( m_discoveryService.ProtocolVersion );

            m_currentUserProvider = currentUserProvider;
            m_settingsService = AppSettings.ExportedValue<ISettingsService>();
        }

        public virtual async void SendResponse( Object sender, TcpMessageEventArgs eventArgs )
        {
            CheckFileExistsRequest request = eventArgs.Message<CheckFileExistsRequest>( whetherReadMessage: false );
            if ( request != null )
            {
                AbstractFileResponse response = new CheckFileExistsResponse( request, m_currentUserProvider.LoggedUser?.Groups, m_currentUserProvider.RootFolderPath );

                try
                {
                    await response.SendAsync( eventArgs.AcceptedSocket ).ConfigureAwait( continueOnCapturedContext: false );
                }
                catch ( TimeoutException ex )
                {
                    DsLoggerSet.DefaultLogger.LogError( ex, ex.Message );
                }
            }
        }

        protected AbstractFileResponse FileResponse( AbstractFileRequest request )
        {
            AbstractFileResponse response = new CheckFileExistsResponse( request.RandomID )
            {
                IsRightBucket = m_discoveryService.LocalBuckets.Any( c => c.Key.Equals( request.LocalBucketId, StringComparison.OrdinalIgnoreCase ) ),
            };

            if ( response.IsRightBucket )
            {
                String fullFileName;
                try
                {
                    fullFileName = FullFileName( request );

                    response.FileExists = File.Exists( fullFileName );
                    if ( response.FileExists )
                    {
                        var fileInfo = new FileInfo( fullFileName );
                        response.FileSize = (UInt64)fileInfo.Length;

                        response.FileVersion = AdsExtensions.Read( fullFileName, AdsExtensions.Stream.LastSeenVersion );
                    }
                }
                catch ( ArgumentException )
                {
                    response.IsRightBucket = false;
                }
            }

            return response;
        }

        protected String FullFileName( AbstractFileRequest request )
        {
            String fullFileName;

            GroupServiceModel groupServiceModel = m_currentUserProvider.LoggedUser?.Groups?.SingleOrDefault( c => c.Id.Equals( request.LocalBucketId, StringComparison.Ordinal ) );

            if ( groupServiceModel != null )
            {
                String filePrefix = request.HexPrefix.FromHexString();
                String localBucketDirectoryPath = Path.Combine( m_currentUserProvider.RootFolderPath, groupServiceModel.Name );

                fullFileName = Path.Combine( localBucketDirectoryPath, filePrefix, request.FileOriginalName );
            }
            else
            {
                var messageException = new StringBuilder( $"Server bucket name {request.LocalBucketId} doesn't match to:\n" );
                foreach ( String bucketId in m_currentUserProvider.LoggedUser?.Groups?.Select( c => c.Id ) )
                {
                    messageException.Append( $"{bucketId}; " );
                }

                throw new ArgumentException( messageException.ToString() );
            }

            return fullFileName;
        }
    }
}

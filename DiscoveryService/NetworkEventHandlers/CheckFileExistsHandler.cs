﻿using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.NetworkEventHandlers
{
    class CheckFileExistsHandler : INetworkEventHandler
    {
        protected readonly ISettingsService m_settingsService;
        protected readonly ICurrentUserProvider m_currentUserProvider;

        private readonly DiscoveryService m_discoveryService;

        public CheckFileExistsHandler( ICurrentUserProvider currentUserProvider, DiscoveryService discoveryService )
        {
            m_discoveryService = discoveryService;

            m_currentUserProvider = currentUserProvider;
            m_settingsService = new SettingsService
            {
                CurrentUserProvider = currentUserProvider
            };
        }

        public virtual void SendResponse( Object sender, TcpMessageEventArgs eventArgs )
        {
            CheckFileExistsRequest request = eventArgs.Message<CheckFileExistsRequest>( whetherReadMessage: false );
            AbstactFileResponse response = FileResponse( request );

            response.Send( eventArgs.AcceptedSocket );
        }

        protected AbstactFileResponse FileResponse( AbstractFileRequest request )
        {
            CheckFileExistsResponse response = new CheckFileExistsResponse( request.RandomID )
            {
                IsRightBucket = m_discoveryService.GroupsSupported.ContainsKey( request.BucketName ),
            };

            if ( response.IsRightBucket )
            {
                String fullPath = FullFileName( request );
                response.FileExists = File.Exists( fullPath );

                if ( response.FileExists )
                {
                    FileInfo fileInfo = new FileInfo( fullPath );
                    response.FileSize = (UInt64)fileInfo.Length;

                    response.FileVersion = AdsExtensions.ReadLastSeenVersion( fullPath );
                }
            }

            return response;
        }

        protected String FullFileName( AbstractFileRequest request )
        {
            IList<String> bucketDirectoryPathes = m_currentUserProvider.ProvideBucketDirectoryPathes();
            String localBucketDirectoryPath = bucketDirectoryPathes.SingleOrDefault( bucketFullName =>
             {
                 String bucketDirectoryName = Path.GetFileName( bucketFullName );
                 return request.BucketName.ToLowerInvariant().Contains( bucketDirectoryName.ToLowerInvariant() );
             } );

            if ( localBucketDirectoryPath != null )
            {
//#if INTEGRATION_TESTS
//                String fullDllFileName = Assembly.GetEntryAssembly().Location;
//                String directoryName = Path.GetFileName( localBucketDirectoryPath );

//                localBucketDirectoryPath = Path.Combine( fullDllFileName, directoryName );
//#endif

                String filePrefix = request.HexPrefix.FromHexString();

                String fullFileName = Path.Combine( localBucketDirectoryPath, filePrefix, request.FileOriginalName );
                return fullFileName;
            }
            else
            {
                StringBuilder messageException = new StringBuilder( $"Server bucket name {request.BucketName} doesn't match to:\n" );
                foreach ( String bucketFullName in bucketDirectoryPathes )
                {
                    messageException.Append( $"{bucketFullName}; " );
                }

                throw new ArgumentException( messageException.ToString() );
            }
        }
    }
}

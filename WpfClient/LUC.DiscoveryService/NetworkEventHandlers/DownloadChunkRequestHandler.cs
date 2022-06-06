using AutoMapper;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Models;

using System;
using System.IO;

namespace LUC.DiscoveryServices.NetworkEventHandlers
{
    class DownloadChunkRequestHandler : INetworkEventHandler
    {
        private readonly IMapper m_mapper;

        private readonly Object m_lockReadFile = new Object();

        private readonly DiscoveryService m_discoveryService;

        private readonly ISettingsService m_settingsService;
        private readonly ICurrentUserProvider m_currentUserProvider;

        public DownloadChunkRequestHandler( ICurrentUserProvider currentUserProvider, DiscoveryService discoveryService )
        {
            m_discoveryService = discoveryService;

            m_currentUserProvider = currentUserProvider;
            m_settingsService = AppSettings.ExportedValue<ISettingsService>();

            AppSettings.
                MapperConfigurationExpression.
                CreateMap<AbstractFileResponse, DownloadChunkResponse>().
                ForMember( destinationMember: dest => dest.MessageOperation, memberOptions: act => act.Ignore() );

            var mapperConfig = new MapperConfiguration( AppSettings.MapperConfigurationExpression );
            AppSettings.Mapper = mapperConfig.CreateMapper();

            m_mapper = AppSettings.Mapper;
        }

        public async void SendResponse( Object sender, TcpMessageEventArgs eventArgs )
        {
            DownloadChunkRequest request = eventArgs.Message<DownloadChunkRequest>( whetherReadMessage: false );

            if ( request != null )
            {
                Boolean isRightBucket = m_discoveryService.LocalBuckets.ContainsKey( request.LocalBucketId );
                AbstractFileResponse abstractResponse;
                String fullFileName = String.Empty;

                if ( isRightBucket )
                {
                    fullFileName = CheckFileExistsResponse.FullFileName( request, m_currentUserProvider.LoggedUser?.Groups, m_currentUserProvider.RootFolderPath );
                    abstractResponse = new CheckFileExistsResponse( request, fullFileName );
                }
                else
                {
                    abstractResponse = new CheckFileExistsResponse( request.RandomID )
                    {
                        IsRightBucket = false,
                        FileExists = false
                    };
                }
                
                try
                {
                    DownloadChunkResponse downloadFileResponse = m_mapper.Map<DownloadChunkResponse>( abstractResponse );

                    if ( downloadFileResponse.FileExists )
                    {
                        Byte[] fileBytes;

                        if ( request.ChunkRange.Total <= DsConstants.MAX_CHUNK_SIZE )
                        {
                            fileBytes = File.ReadAllBytes( fullFileName );
                        }
                        else
                        {
                            Int32 countBytesToRead = (Int32)( request.ChunkRange.End + 1 - request.ChunkRange.Start );
                            fileBytes = new Byte[ countBytesToRead ];

                            //it is needed to use lock in case several contacts want to download the same file
                            lock ( m_lockReadFile )
                            {
                                using ( var stream = new FileStream( fullFileName, FileMode.Open, FileAccess.Read ) )
                                {
                                    if ( (Int64)request.ChunkRange.Start != stream.Position )
                                    {
                                        stream.Seek( (Int64)request.ChunkRange.Start, origin: SeekOrigin.Begin );
                                    }

                                    stream.Read( fileBytes, offset: 0, countBytesToRead );
                                }
                            }
                        }

                        downloadFileResponse.Chunk = fileBytes;
                    }
                    else
                    {
                        downloadFileResponse.Chunk = new Byte[ 0 ];
                    }

                    await downloadFileResponse.SendAsync( eventArgs.AcceptedSocket ).ConfigureAwait( continueOnCapturedContext: false );
                }
                catch ( IOException ex )
                {
                    DsLoggerSet.DefaultLogger.LogInfo( ex.ToString() );

                    var errorResponse = new ErrorResponse( request.RandomID, ex.Message );
                    await errorResponse.SendAsync( eventArgs.AcceptedSocket ).ConfigureAwait( false );
                }
            }
        }
    }
}

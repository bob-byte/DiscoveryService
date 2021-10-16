using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
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
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.NetworkEventHandlers
{
    class DownloadFileHandler : CheckFileExistsHandler
    {
        private readonly ILoggingService m_loggingService;

        private readonly Object m_lockReadFile = new Object();

        public DownloadFileHandler( ICurrentUserProvider currentUserProvider, DiscoveryService discoveryService )
            : base( currentUserProvider, discoveryService )
        {
            m_loggingService = new LoggingService
            {
                SettingsService = m_settingsService
            };
        }

        public override void SendResponse( Object sender, TcpMessageEventArgs eventArgs )
        {
            DownloadFileRequest request = eventArgs.Message<DownloadFileRequest>( whetherReadMessage: false );

            if ( request != null )
            {
                try
                {
                    AbstactFileResponse checkFileResponse = FileResponse( request );
                    DownloadFileResponse downloadFileResponse = new DownloadFileResponse( request.RandomID )
                    {
                        FileExists = checkFileResponse.FileExists,
                        FileSize = checkFileResponse.FileSize,
                        FileVersion = checkFileResponse.FileVersion,
                        IsRightBucket = checkFileResponse.IsRightBucket
                    };

                    if ( downloadFileResponse.FileExists )
                    {
                        Byte[] fileBytes;

                        String fullFileName = FullFileName( request );
                        if ( request.ChunkRange.Total <= Constants.MAX_CHUNK_SIZE )
                        {
                            fileBytes = File.ReadAllBytes( fullFileName );
                        }
                        else
                        {
                            Int32 countBytesToRead = (Int32)( ( request.ChunkRange.End + 1 ) - request.ChunkRange.Start );
                            fileBytes = new Byte[ countBytesToRead ];

                            //it is needed to use lock in case several contacts want to download the same file
                            lock ( m_lockReadFile )
                            {
                                using ( FileStream stream = new FileStream( fullFileName, FileMode.Open, FileAccess.Read ) )
                                {
                                    if( (Int64)request.ChunkRange.Start != stream.Position )
                                    {
                                        stream.Seek( (Int64)request.ChunkRange.Start, origin: SeekOrigin.Begin );
                                    }

                                    stream.Read( fileBytes, offset: 0, countBytesToRead );
                                }
                            }
                        }

                        downloadFileResponse.Chunk = fileBytes;
                        downloadFileResponse.Send( eventArgs.AcceptedSocket );
                    }
                }
                catch ( IOException ex )
                {
                    m_loggingService.LogInfo( ex.ToString() );

                    ErrorResponse errorResponse = new ErrorResponse( request.RandomID, ex.Message );
                    errorResponse.Send( eventArgs.AcceptedSocket );
                }
            }
        }
    }
}

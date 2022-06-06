
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class CheckFileExistsResponse : AbstractFileResponse
    {
        public CheckFileExistsResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public CheckFileExistsResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        public CheckFileExistsResponse( AbstractFileRequest request, String localFullFileName )
            : base( request.RandomID )
        {
            Init( localFullFileName, isRightBucket: true );
        }

        public CheckFileExistsResponse( AbstractFileRequest request, IEnumerable<GroupServiceModel> userGroups, String syncFolder )
            : this( request.RandomID )
        {
            String localFullFileName = String.Empty;

            try
            {
                localFullFileName = FullFileName( request, userGroups, syncFolder );
            }
            catch(ArgumentException)
            {
                ;//isn't right bucket in request
            }

            Boolean isRightBucket = !String.IsNullOrWhiteSpace( localFullFileName );
            Init( localFullFileName, isRightBucket );
        }

        public static String FullFileName( AbstractFileRequest request, IEnumerable<GroupServiceModel> userGroups, String syncFolder )
        {
            #region Check parameters 
            //request cannot be null
            if ( userGroups == null )
            {
                throw new ArgumentNullException( nameof( userGroups ) );
            }
            else if ( String.IsNullOrWhiteSpace( syncFolder ) )
            {
                throw new ArgumentNullException( nameof( syncFolder ) );
            }
            #endregion

            GroupServiceModel groupServiceModel = userGroups.SingleOrDefault( c => c.Id.Equals( request.LocalBucketId, StringComparison.Ordinal ) );

            if ( groupServiceModel != null )
            {
                String filePrefix = request.HexPrefix.FromHexString();
                String localBucketDirectoryPath = Path.Combine( syncFolder, groupServiceModel.Name );

                String fullFileName = Path.Combine( localBucketDirectoryPath, filePrefix, request.FileOriginalName );
                return fullFileName;
            }
            else
            {
                var messageException = new StringBuilder( $"Server bucket name {request.LocalBucketId} doesn't match to:\n" );
                foreach ( String bucketId in userGroups.Select( c => c.Id ) )
                {
                    messageException.Append( $"{bucketId}; " );
                }

                throw new ArgumentException( messageException.ToString() );
            }
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.CheckFileExistsResponse;

        private void Init( String localFullFileName, Boolean isRightBucket )
        {
            DefaultInit();

            //because localFullFileName is received
            IsRightBucket = isRightBucket;

            try
            {
                FileExists = File.Exists( localFullFileName );
                if ( FileExists )
                {
                    var fileInfo = new FileInfo( localFullFileName );
                    FileSize = (UInt64)fileInfo.Length;

                    FileVersion = AdsExtensions.Read( localFullFileName, AdsExtensions.Stream.LastSeenVersion );
                }
            }
            catch ( ArgumentException )
            {
                IsRightBucket = false;
            }
        }
    }
}

using LUC.Interfaces.OutputContracts;

using System;

namespace LUC.ApiClient.Models
{
    internal class ChunkUploadState // TODO Release 2.0 Range for download Range: 65545-
    {
        internal ChunkUploadState()
        {
            IsFirstChunk = true;
            IsLastChunk = false;
            PartNumber = 0;
        }

        internal BaseUploadResponse lastResponse;
        internal BaseUploadResponse LastResponse
        {
            get => lastResponse;
            set
            {
                lastResponse = value;

                if ( lastResponse != null )
                {
                    Guid = lastResponse.Guid;
                }
            }
        }

        internal String ChunkRequestUri { get; set; }

        internal Boolean IsFirstChunk { get; set; }

        internal Boolean IsLastChunk { get; set; }

        internal Int64 PartNumber { get; private set; }

        internal String Guid { get; set; }
    }
}
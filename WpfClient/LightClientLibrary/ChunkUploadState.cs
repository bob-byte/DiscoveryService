using System;

namespace LightClientLibrary
{
    internal class ChunkUploadState
    {
        private FileUploadResponse m_lastResponse;

        internal String ChunkRequestUri { get; set; }

        internal String Guid { get; set; }

        internal Boolean IsFirstChunk { get; set; }

        internal Boolean IsLastChunk { get; set; }

        internal FileUploadResponse LastResponse
        {
            get => m_lastResponse;
            set
            {
                m_lastResponse = value;

                if ( m_lastResponse != null )
                {
                    Guid = m_lastResponse.Guid;
                }
            }
        }

        internal Int32 PartNumber { get; private set; }

        internal void IncreasePartNumber() => PartNumber++;
    }
}
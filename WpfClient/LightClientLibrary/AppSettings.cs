using System;

namespace LightClientLibrary
{
    public class AppSettings
    {

        public AppSettings()
        {
            UploadId = String.Empty;
            FileName = String.Empty;
            Version = String.Empty;
            ChunkNumber = -1;
        }

        public String UploadId { get; set; }
        public String FileName { get; set; }
        public String Version { get; set; }
        public Int32 ChunkNumber { get; set; }

    }
}
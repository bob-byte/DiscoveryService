using Newtonsoft.Json;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;

namespace LightClientLibrary
{
    public class UploadSettings
    {
        /// <summary>
        /// Path to the file with progress of upload
        /// </summary>
        private String AppSettingsFilePath => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "LightUponCloud", "uploadsettings.json" );

        public UploadSettings()
        {
            CancelledUpload = new List<AppSettings>();
        }

        public List<AppSettings> CancelledUpload { get; set; }

        public Int16 FindCurrentFileInSettings( String filepath, List<AppSettings> settings )
        {
            CancelledUpload = settings;

            if ( settings == null )
            {
                return -1;
            }

            for ( Int16 i = 0; i < settings.Count; i++ )
            {
                if ( settings[ i ].FileName.Equals( filepath ) )
                {
                    return i;
                }
            }

            return -1;
        }

        public UploadSettings ReadSettingsFromFile()
        {
            UploadSettings settings;

            if ( File.Exists( AppSettingsFilePath ) )
            {
                String json = File.ReadAllText( AppSettingsFilePath );
                settings = String.IsNullOrEmpty( json ) ? new UploadSettings() : JsonConvert.DeserializeObject<UploadSettings>( json );
            }
            else
            {
                settings = new UploadSettings();
            }

            return settings;
        }

        public void WriteProgressToFile( UploadSettings settings )
        {
            try
            {
                String directoryWithSettings = Path.GetDirectoryName( AppSettingsFilePath );
                if ( !Directory.Exists( directoryWithSettings ) )
                {
                    _ = Directory.CreateDirectory( directoryWithSettings );
                }

                var serializer = new JsonSerializer
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

                using ( var sw = new StreamWriter( AppSettingsFilePath ) )
                {
                    using ( JsonWriter writer = new JsonTextWriter( sw ) )
                    {
                        serializer.Serialize( writer, settings );
                    }
                }
            }
            catch ( IOException ex )
            {
                Log.Error( "Can't write upload settings to file, IO Exception", ex );
            }

            catch ( Exception ex )
            {
                Log.Error( "WriteProgressToFile error", ex );
            }
        }

    }
}
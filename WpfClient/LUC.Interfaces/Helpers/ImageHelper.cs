using LUC.Interfaces.Models;

using System;
using System.Drawing;

namespace LUC.Interfaces.Helpers
{
    public class ImageHelper : IDisposable
    {
        private Boolean m_disposed = false;
        public static ImageMetadata TryReadImageMetadata( String filePath )
        {
            try
            {
                var image = Image.FromFile( filePath );
                var imageSize = new ImageMetadata( image.Height, image.Width );
                image.Dispose();
                return imageSize;
            }
            catch ( Exception )
            {
                return null;
            }
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( Boolean disposing )
        {
            if ( m_disposed )
            {
                return;
            }

            m_disposed = true; //помечаем флаг что метод Dispose уже был вызван
        }
    }
}

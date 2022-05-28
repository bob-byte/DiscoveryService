namespace LUC.Interfaces.Models
{
    public class ImageMetadata
    {
        public ImageMetadata( System.Int32 height, System.Int32 width )
        {
            Height = height;
            Width = width;
        }

        public System.Int32 Height { get; }

        public System.Int32 Width { get; }
    }
}
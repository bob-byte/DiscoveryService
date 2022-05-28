namespace LUC.Interfaces.Models
{
    public interface IBucketName
    {
        System.String ServerName { get; }

        System.String LocalName { get; }

        System.Boolean IsSuccess { get; }

        System.String ErrorMessage { get; }
    }
}

namespace LUC.Interfaces.Models
{
    public interface INotificationResult
    {
        System.Boolean IsSuccess { get; set; }

        System.String Message { get; set; }
    }
}

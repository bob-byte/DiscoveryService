namespace LUC.Interfaces.Models
{
    public class NotificationResult : INotificationResult
    {
        public System.Boolean IsSuccess { get; set; }

        public System.String Message { get; set; }
    }
}

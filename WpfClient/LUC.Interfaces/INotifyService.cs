using LUC.Interfaces.Models;

using System.Threading.Tasks;
using System.Windows;

namespace LUC.Interfaces
{
    public interface INotifyService
    {
        void NotifyInfo( System.String message );

        void NotifyError( System.String message );

        void Notify( INotificationResult notificationResult );

        void NotifyStaticMessage( System.String message );

        void ClearStaticMessages();

        MessageBoxResult ShowMessageBox( System.String message, System.String caption, MessageBoxButton buttons );

        Task<MessageBoxResult> ShowMessageBoxAsync( System.String message, System.String caption, MessageBoxButton buttons );
    }
}

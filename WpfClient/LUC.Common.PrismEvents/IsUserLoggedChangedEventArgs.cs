using Prism.Events;

namespace LUC.Common.PrismEvents
{
    public class IsUserLoggedChangedEventArgs : EventBase
    {
        public System.String UserName { get; set; }
    }
}

using LUC.Interfaces.Models;

using System;

namespace LUC.Interfaces.OutputContracts
{
    public abstract class BaseResponse : INotificationResult
    {
        protected BaseResponse()
        {
        }

        protected BaseResponse( Boolean isSuccess, Boolean isForbidden, String message )
        {
            IsSuccess = isSuccess;
            IsForbidden = isForbidden;
            Message = message;
        }

        public Boolean IsSuccess { get; set; } = true;

        public String Message { get; set; }

        public Boolean IsForbidden { get; set; } = false;
    }
}

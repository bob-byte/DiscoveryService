using System;

namespace LightClientLibrary
{
    public abstract class BaseResponse
    {
        public Boolean IsSuccess { get; set; } = false;
        protected String Message { get; set; }
    }
}
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using Serilog;

using System;
using System.IO;

namespace LUC.Interfaces.Models
{
    public class ServerObjectDescription : AbstractServerObjectDescription, INotificationResult
    {
        public Boolean IsSuccess { get; set; }

        public String Message { get; set; }

        /// <remarks>
        /// For directory it includes object name already
        /// </remarks>
        public String ObjectPrefix { get; set; }

        public override Boolean ShouldLocalFileBeUploaded( FileInfo localFileInfo ) =>
            !IsSuccess || base.ShouldLocalFileBeUploaded( localFileInfo );//if !IsSuccess then file never ever existed on server
    }
}

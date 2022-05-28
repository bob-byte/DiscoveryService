using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.Interfaces.Extensions
{
    public static partial class AdsExtensions
    {
        public enum Stream
        {
            None = 0,
            Guid = 1,
            LastSeenVersion = 2,
            LocalPathMarker = 3,
            Lock = 4,
            Md5 = 5,
            IsDownloadedButNotMovedFile = 6
        }
    }
}

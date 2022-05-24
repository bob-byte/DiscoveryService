using System;

namespace LUC.ApiClient
{
    static class Speed
    {
        public static String ToString( Int64 bytes, Int64 milliseconds ) => $"Speed = {bytes * 8 / ( milliseconds / 1000.0 ) / 1024.0 / 1024.0:n2} Mbps";
    }
}

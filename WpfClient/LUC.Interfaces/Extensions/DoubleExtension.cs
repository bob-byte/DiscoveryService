using System;

namespace LUC.Interfaces.Extensions
{
    public static class DoubleExtension
    {
        public static Boolean ApproximatelyEquals( this Double d, Double val, Double range ) =>
            ( d >= val - range ) && ( d <= val + range );
    }
}

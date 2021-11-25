using System;
using System.Text;

namespace LUC.DiscoveryServices.Test.Extensions
{
    static class RandomExtension
    {
        public static String RandomSymbols(this Random random, Int32 count)
        {
            StringBuilder randomSymbols = new StringBuilder();
            String setOfAllSymbols = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            for (Int32 i = 0; i < count; i++)
            {
                Int32 indexOfChar = random.Next( minValue: 0, maxValue: setOfAllSymbols.Length );
                randomSymbols.Append( setOfAllSymbols[ indexOfChar ] );
            }

            return randomSymbols.ToString();
        }
    }
}

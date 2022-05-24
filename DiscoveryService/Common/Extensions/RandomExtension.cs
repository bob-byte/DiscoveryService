using System;
using System.Text;

namespace DiscoveryServices.Common.Extensions
{
    public static class RandomExtension
    {
        private const String SET_OF_ALL_SYMBOLS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static String RandomSymbols( this Random random, Int32 count )
        {
            var randomSymbols = new StringBuilder();

            for ( Int32 numSymbol = 0; numSymbol < count; numSymbol++ )
            {
                Int32 indexOfChar = random.Next( minValue: 0, maxValue: SET_OF_ALL_SYMBOLS.Length );
                randomSymbols.Append( SET_OF_ALL_SYMBOLS[ indexOfChar ] );
            }

            return randomSymbols.ToString();
        }
    }
}

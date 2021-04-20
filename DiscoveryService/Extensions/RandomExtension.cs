using System;
using System.Text;

namespace LUC.DiscoveryService.Extensions
{
    static class RandomExtension
    {
        public static StringBuilder GenerateRandomSymbols(this Random random, Int32 count)
        {
            StringBuilder randomSymbols = new StringBuilder();
            String setOfAllSymbols = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            for (Int32 i = 0; i < count; i++)
            {
                var indexOfChar = random.Next(0, setOfAllSymbols.Length);
                randomSymbols.Append(setOfAllSymbols[indexOfChar]);
            }

            return randomSymbols;
        }
    }
}

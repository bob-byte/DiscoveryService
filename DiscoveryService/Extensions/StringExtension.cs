using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Extensions
{
    static class StringExtension
    {
        public static String FormatInvariant(this String format, params Object[] args) =>
            String.Format(CultureInfo.InvariantCulture, format, args);
    }
}

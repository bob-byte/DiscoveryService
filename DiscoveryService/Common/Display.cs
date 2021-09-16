using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    static class Display
    {
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static String PropertyWithValue<T>( String nameProp, T value ) =>
            $"{nameProp} = {value}";
    }
}

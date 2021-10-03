using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using LUC.Interfaces;
using LUC.Services.Implementation;

namespace LUC.DiscoveryService.Common
{
    class Display
    {
        [Import( typeof( ILoggingService ) )]
        public static ILoggingService LoggingService { get; set; }

        static Display()
        {
            LoggingService = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        public static String ObjectToString( Object objectToConvert, String memberName = "" ) 
        {
            if ( objectToConvert != null )
            {
                if(memberName == "")
                {
                    memberName = objectToConvert.GetType().Name;
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append( $"{memberName}:\n" );

                foreach ( PropertyInfo prop in objectToConvert.GetType().
                    GetProperties().
                    OrderBy( c => c.Name ) )
                {
                    if ( ( prop.PropertyType != typeof( String ) ) && ( typeof( IEnumerable ).IsAssignableFrom( prop.PropertyType ) ) )
                    {
                        IEnumerable enumerable = prop.GetValue( objectToConvert ) as IEnumerable;
                        stringBuilder.Append( $"\t{prop.Name}:\n" );

                        foreach ( Object item in enumerable )
                        {
                            stringBuilder.Append( $"\t\t{item};\n" );
                        }
                    }
                    else
                    {
                        stringBuilder.Append( $"{PropertyWithValue( prop.Name, prop.GetValue( objectToConvert, index: null ) )};\n" );
                    }
                }

                return stringBuilder.ToString();
            }
            else
            {
                throw new ArgumentNullException( nameof( objectToConvert ) );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static String PropertyWithValue<T>( String nameProp, T value ) =>
            $"\t{nameProp} = {value}";
    }
}

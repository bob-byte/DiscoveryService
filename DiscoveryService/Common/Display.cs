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
        public const String TABULATION = "\t";

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
                        stringBuilder.Append( $"{TABULATION}{prop.Name}:\n" );

                        if (prop.GetValue( objectToConvert ) is IEnumerable enumerable )
                        {
                            foreach ( Object item in enumerable )
                            {
                                stringBuilder.Append( $"{TABULATION}{TABULATION}{item};\n" );
                            }
                        }
                    }
                    else
                    {
                        stringBuilder.Append( $"{VariableWithValue( prop.Name, prop.GetValue( objectToConvert, index: null ) )};\n" );
                    }
                }

                return stringBuilder.ToString();
            }
            else
            {
                throw new ArgumentNullException( nameof( objectToConvert ) );
            }
        }

        internal static String StringWithAttention( String logRecord ) =>
            $"\n*************************\n{logRecord}\n*************************\n";

        /// <summary>
        /// With tabulation in start
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static String VariableWithValue<T>( String nameProp, T value, Boolean useTab = true )
        {
            String tab = useTab ? TABULATION : String.Empty;

            String propertyWithValue = $"{tab}{nameProp} = {value}";
            return propertyWithValue;
        }
    }
}

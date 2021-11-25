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

using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Services.Implementation;

namespace LUC.DiscoveryServices.Common
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

        public static String ObjectToString( Object objectToConvert, String memberName = "" ) =>
            ObjectToString( objectToConvert, initialTabulation: String.Empty, memberName );

        public static String ResponseWithCloseContacts(Response response, ICollection<Contact> contacts)
        {
            String responseAsStrWithoutContacts = ObjectToString( response );

            StringBuilder stringBuilder = new StringBuilder( responseAsStrWithoutContacts );

            if ( contacts != null )
            {
                String countCloseContacts = VariableWithValue(nameProp: "CountCloseContacts", contacts.Count );
                stringBuilder.AppendLine( countCloseContacts );

                if(contacts.Count > 0)
                {
                    stringBuilder.AppendLine( $"{TABULATION}CloseContacts:" );
                    foreach ( Contact closeContact in contacts )
                    {
                        String contactAsStr = ObjectToString( closeContact, initialTabulation: $"{TABULATION}{TABULATION}" );
                        stringBuilder.AppendLine( contactAsStr );
                    }
                }
            }

            return stringBuilder.ToString();
        }

        public static String ObjectToString( Object objectToConvert, String initialTabulation, String memberName = "" )
        {
            if ( objectToConvert != null )
            {
                if ( memberName == "" )
                {
                    memberName = objectToConvert.GetType().Name;
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append( $"{initialTabulation}{memberName}:\n" );

                foreach ( PropertyInfo prop in objectToConvert.GetType().
                    GetProperties().
                    OrderBy( c => c.Name ) )
                {
                    if ( ( prop.PropertyType != typeof( String ) ) && ( typeof( IEnumerable ).IsAssignableFrom( prop.PropertyType ) ) )
                    {
                        stringBuilder.Append( $"{initialTabulation}{TABULATION}{prop.Name}:\n" );

                        if ( prop.GetValue( objectToConvert ) is IEnumerable enumerable )
                        {
                            foreach ( Object item in enumerable )
                            {
                                stringBuilder.Append( $"{initialTabulation}{TABULATION}{TABULATION}{item};\n" );
                            }
                        }
                    }
                    else
                    {
                        stringBuilder.Append( $"{initialTabulation}{VariableWithValue( prop.Name, prop.GetValue( objectToConvert, index: null ) )};\n" );
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

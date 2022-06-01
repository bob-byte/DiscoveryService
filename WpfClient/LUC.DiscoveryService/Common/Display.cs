using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Discoveries;

namespace LUC.DiscoveryServices.Common
{
    internal static class Display
    {
        public const Char TABULATION = '\t';
        public const String TABULATION_AS_STR = "\t";

        public static String ResponseWithCloseContacts( Response response, ICollection<IContact> contacts )
        {
            String responseAsStrWithoutContacts = ToString( response );

            var stringBuilder = new StringBuilder( responseAsStrWithoutContacts );

            if (contacts != null)
            {
                String countCloseContacts = VariableWithValue( nameProp: "CountCloseContacts", contacts.Count );
                stringBuilder.AppendLine( countCloseContacts );

                if (contacts.Count > 0)
                {
                    stringBuilder.AppendLine( $"{TABULATION}CloseContacts:" );
                    foreach (IContact closeContact in contacts)
                    {
                        String contactAsStr = ToString( closeContact, initialTabulation: $"{TABULATION}{TABULATION}" );
                        stringBuilder.AppendLine( contactAsStr );
                    }
                }
            }

            return stringBuilder.ToString();
        }

        public static String ToString<T>( this IEnumerable<T> enumerable, Boolean showAllPropsOfItems, String initialTabulation, String nameOfEnumerable = "", String nameOfEachItem = "" )
        {
            nameOfEnumerable = CheckedNameOfVariable( nameOfEnumerable, enumerable );

            Int32 elemCount = enumerable.Count();
            String countWithNameObjectAsStr = VariableWithValue( nameProp: $"{initialTabulation}Count of {nameOfEnumerable}", elemCount, useTab: false );
            if(elemCount > 0)
            {
                countWithNameObjectAsStr += ":";
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine( countWithNameObjectAsStr );

            foreach ( T item in enumerable )
            {
                String itemAsStr;

                if ( showAllPropsOfItems)
                {
                    itemAsStr = ToString( item, initialTabulation: $"{initialTabulation}{TABULATION}", nameOfEachItem );
                }
                else
                {
                    nameOfEachItem = CheckedNameOfVariable( nameOfEachItem, item );
                    itemAsStr = $"{initialTabulation}{VariableWithValue( nameOfEachItem, item, useTab: true )}";
                }

                stringBuilder.AppendLine( itemAsStr );
            }

            return stringBuilder.ToString();
        }

        public static String ToString( this Object objectToConvert, String memberName = "" ) =>
            objectToConvert.ToString( initialTabulation: String.Empty, memberName );

        public static String ToString( this Object objectToConvert, String initialTabulation, String memberName = "" )
        {
            if ( objectToConvert != null )
            {
                if ( memberName == "" )
                {
                    memberName = objectToConvert.GetType().Name;
                }

                var stringBuilder = new StringBuilder();
                stringBuilder.Append( $"{initialTabulation}{memberName}:\n" );

                foreach ( PropertyInfo prop in objectToConvert.GetType().
                    GetProperties().
                    OrderBy( c => c.Name ) )
                {
                    if ( ( prop.PropertyType != typeof( String ) ) && typeof( IEnumerable ).IsAssignableFrom( prop.PropertyType ) )
                    {
                        if ( prop.GetValue( objectToConvert ) is IEnumerable enumerable )
                        {
                            String tab = $"{initialTabulation}{TABULATION}";
                            stringBuilder.Append( enumerable.ToString( tab, enumerableName: prop.Name, nameOfEachItem: String.Empty ) );
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

        public static String ToString(this IEnumerable enumerable, String initialTabulation, String enumerableName = "", String nameOfEachItem = "" )
        {
            String checkedEnumarableName = CheckedNameOfVariable( enumerableName, enumerable );

            var stringBuilder = new StringBuilder( value: $"{initialTabulation}{checkedEnumarableName}:\n" );

            foreach ( Object item in enumerable )
            {
                String itemAsStr = item?.ToString();

                String tab = $"{initialTabulation}{TABULATION}";
                itemAsStr = ( itemAsStr != null ) && itemAsStr.Equals( item.GetType().FullName, StringComparison.Ordinal ) ?
                    item.ToString( tab, nameOfEachItem ) :
                    $"{tab}{itemAsStr};";

                stringBuilder.Append( $"{itemAsStr}\n" );
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// With tabulation in start
        /// </summary>
        internal static String VariableWithValue<T>( String nameProp, T value, Boolean useTab = true )
        {
            String tab = useTab ? TABULATION_AS_STR : String.Empty;

            String propertyWithValue = $"{tab}{nameProp} = {value}";
            return propertyWithValue;
        }

        private static String CheckedNameOfVariable( String nameOfVariable, Object variable )
        {
            if ( String.IsNullOrWhiteSpace( nameOfVariable ) )
            {
                nameOfVariable = variable.GetType().Name;
            }

            return nameOfVariable;
        }
    }
}

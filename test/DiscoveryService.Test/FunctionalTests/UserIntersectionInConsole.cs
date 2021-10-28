using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test.FunctionalTests
{
    static class UserIntersectionInConsole
    {
        public const ConsoleKey KEY_TO_CONTINUE_PROGRAM = ConsoleKey.Enter;
        public const String IS_TRUE = "1";
        public const String IS_FALSE = "2";

        public static String ValidValueInputtedByUser( String requestToUser, Predicate<String> tryPredicateUserInput )
        {
            String userInput;
            Boolean isRightInput = false;

            do
            {
                Console.Write( requestToUser );
                userInput = Console.ReadLine();

                try
                {
                    isRightInput = tryPredicateUserInput( userInput );
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            while ( !isRightInput );

            return userInput;
        }

        public static T ValidValueInputtedByUser<T>( String requestToUser, Predicate<T> tryPredicateUserInput, Func<String, T> convert )
            where T: class
        {
            T value = default;

            do
            {
                try
                {
                    Console.Write( requestToUser );
                    String userInput = Console.ReadLine();
                    value = convert( userInput );
                }
                catch
                {
                    ;//do nothing
                }
            }
            while ( ( value.Equals( default( T ) ) ) || ( !tryPredicateUserInput( value ) ) );

            return value;
        }

        public static T ValidValueInputtedByUser<T>( String requestToUser, Func<String, T> convert, Predicate<T> tryPredicateUserInput, Boolean userInputsInNewLine = false )
            where T: struct
        {
            T value = default;

            do
            {
                try
                {
                    if(userInputsInNewLine)
                    {
                        Console.WriteLine(requestToUser);
                    }
                    else
                    {
                        Console.Write( requestToUser );
                    }

                    String userInput = Console.ReadLine();
                    value = convert( userInput );
                }
                catch
                {
                    ;//do nothing
                }
            }
            while ( !tryPredicateUserInput( value ) );

            return value;
        }
    }
}

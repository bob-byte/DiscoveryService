using System;

namespace LUC.DiscoveryServices.Test.FunctionalTests
{
    static class UserIntersectionInConsole
    {
        public const String IS_FALSE = "2";
        public const String IS_TRUE = "1";
        public const ConsoleKey KEY_TO_CONTINUE_PROGRAM = ConsoleKey.Enter;
        static UserIntersectionInConsole()
        {
            Lock = new Object();
        }

        public static Object Lock { get; }

        public static Boolean NormalResposeFromUserAtClosedQuestion( String closedQuestion )
        {
            lock ( Lock )
            {
                Boolean userResponse = false;
                String readLine;

                do
                {
                    Console.WriteLine( $"{closedQuestion}\n" +
                    $"{IS_TRUE} - yes\n" +
                    $"{IS_FALSE} - no" );
                    readLine = Console.ReadLine();

                    if ( !String.IsNullOrEmpty( readLine ) )
                    {
                        readLine = readLine.Trim();
                    }

                    if ( readLine == IS_TRUE )
                    {
                        userResponse = true;
                    }
                    else if ( readLine == IS_FALSE )
                    {
                        userResponse = false;
                    }
                }
                while ( ( readLine != IS_TRUE ) && ( readLine != IS_FALSE ) );

                return userResponse;
            }
        }

        public static String ValidValueInputtedByUser( String requestToUser, Predicate<String> tryPredicateUserInput )
        {
            String userInput;
            Boolean isRightInput = false;

            lock ( Lock )
            {
                do
                {
                    Console.Write( requestToUser );
                    userInput = Console.ReadLine();

                    try
                    {
                        isRightInput = tryPredicateUserInput( userInput );
                    }
                    catch ( Exception ex )
                    {
                        Console.WriteLine( ex.Message );
                    }
                }
                while ( !isRightInput );
            }

            return userInput;
        }
        public static T ValidValueInputtedByUser<T>( String requestToUser, Predicate<T> tryPredicateUserInput, Func<String, T> convert )
            where T : class
        {
            T value = default;

            lock ( Lock )
            {
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
                while ( value.Equals( default( T ) ) || ( !tryPredicateUserInput( value ) ) );
            }

            return value;
        }

        public static T ValidValueInputtedByUser<T>( String requestToUser, Func<String, T> convert, Predicate<T> tryPredicateUserInput, Boolean userInputsInNewLine = false )
            where T : struct
        {
            T value = default;

            lock ( Lock )
            {
                do
                {
                    try
                    {
                        if ( userInputsInNewLine )
                        {
                            Console.WriteLine( requestToUser );
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
            }

            return value;
        }
    }
}

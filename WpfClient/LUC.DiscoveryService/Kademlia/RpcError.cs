using LUC.DiscoveryServices.Common;

using System;
using System.IO;

namespace LUC.DiscoveryServices.Kademlia
{
    public class RpcError
    {
        public Boolean HasError =>
            IDMismatchError || LocalError || RemoteError || OtherError;

        public Boolean IDMismatchError { get; set; }

        public Boolean LocalError { get; set; }

        public Boolean RemoteError { get; set; }

        public Boolean OtherError { get; set; }

        public String ErrorMessage { get; set; }

        public override String ToString()
        {
            using ( var stringWriter = new StringWriter() )
            {
                stringWriter.Write( value: $"{GetType().Name}:\n{Display.VariableWithValue( nameof( HasError ), HasError )}" );

                if ( HasError )
                {
                    stringWriter.WriteLine( $";\n" +
                        $"{Display.VariableWithValue( nameof( IDMismatchError ), IDMismatchError )};\n" +
                        $"{Display.VariableWithValue( nameof( OtherError ), OtherError )};\n" +
                        $"{Display.VariableWithValue( nameof( LocalError ), LocalError )};\n" +
                        $"{Display.VariableWithValue( nameof( RemoteError ), RemoteError )};\n" +
                        $"{Display.VariableWithValue( nameof( ErrorMessage ), ErrorMessage )}" );
                }

                return stringWriter.ToString();
            }
        }
    }
}

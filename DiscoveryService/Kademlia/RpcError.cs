using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Messages;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.Kademlia
{
    class RpcError
    {
        public Boolean HasError =>
            TimeoutError || IDMismatchError || PeerError;

        public Boolean TimeoutError { get; set; }

        public Boolean IDMismatchError { get; set; }

        public Boolean PeerError { get; set; }

        public String PeerErrorMessage { get; set; }

        public override String ToString()
        {
            using ( StringWriter stringWriter = new StringWriter() )
            {
                stringWriter.Write( $"{GetType().Name}:\n" +
                $"{Display.VariableWithValue( nameof( HasError ), HasError )}" );

                if ( HasError )
                {
                    stringWriter.WriteLine( $";\n" +
                        $"{Display.VariableWithValue( nameof( TimeoutError ), TimeoutError )};\n" +
                        $"{Display.VariableWithValue( nameof( IDMismatchError ), IDMismatchError )};\n" +
                        $"{Display.VariableWithValue( nameof( PeerError ), PeerError )};\n" +
                        $"{Display.VariableWithValue( nameof( PeerErrorMessage ), PeerErrorMessage )}" );
                }

                return stringWriter.ToString();
            }
        }
    }
}

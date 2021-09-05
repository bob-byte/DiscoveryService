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

        public bool TimeoutError { get; set; }
        public Boolean IDMismatchError { get; set; }
        public bool PeerError { get; set; }
        public string PeerErrorMessage { get; set; }

        public override String ToString()
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                stringWriter.Write($"{GetType().Name}:\n" +
                $"{Display.PropertyWithValue(nameof(HasError), HasError)}");

                if (HasError)
                {
                    stringWriter.WriteLine($";\n" +
                        $"{Display.PropertyWithValue(nameof(TimeoutError), TimeoutError)};\n" +
                        $"{Display.PropertyWithValue(nameof(IDMismatchError), IDMismatchError)};\n" +
                        $"{Display.PropertyWithValue(nameof(PeerError), PeerError)};\n" +
                        $"{Display.PropertyWithValue(nameof(PeerErrorMessage), PeerErrorMessage)}");
                }

                return stringWriter.ToString();
            }
        }
    }
}

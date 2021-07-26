using LUC.DiscoveryService.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.Kademlia
{
    public class RpcError : Message
    {
        public bool HasError { get { return TimeoutError || IDMismatchError || PeerError; } }

        public bool TimeoutError { get; set; }
        public bool IDMismatchError { get; set; }
        public bool PeerError { get; set; }
        public string PeerErrorMessage { get; set; }

        public override String ToString()
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                stringWriter.Write($"{GetType().Name}:\n" +
                $"{PropertyWithValue(nameof(HasError), HasError)}");

                if (HasError)
                {
                    stringWriter.WriteLine($";\n" +
                        $"{PropertyWithValue(nameof(TimeoutError), TimeoutError)};\n" +
                        $"{PropertyWithValue(nameof(IDMismatchError), IDMismatchError)};\n" +
                        $"{PropertyWithValue(nameof(PeerError), PeerError)};\n" +
                        $"{PropertyWithValue(nameof(PeerErrorMessage), PeerErrorMessage)}");
                }

                return stringWriter.ToString();
            }
        }
    }
}

using LUC.DiscoveryService.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web;

namespace LUC.DiscoveryService.CodingData
{
    class ParsingTcpData : Parsing<TcpMessage>
    {
        public override Byte[] GetDecodedData(TcpMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var writer = new WireWriter(stream))
                    {
                        writer.Write((UInt32)message.VersionOfProtocol);
                        var arrayIpNetwork = message.GroupsSupported.Keys.ToArray();
                        writer.WriteArray(arrayIpNetwork);
                        for (int i = 0; i < arrayIpNetwork.Length; i++)
                        {
                            for (int i = 0; i < length; i++)
                            {
                                //add to array of names of group key of value(name of Group)
                                //add to array of certificates value of value (certificate)
                            }
                            //write names of groups
                            //write certificates
                        }

                        var decodedData = stream.GetBuffer();

                        return decodedData;
                    }
                }
            }
        }

        public override TcpMessage GetEncodedData(Byte[] bytes)
        {
            if(bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    using (var reader = new WireReader(stream))
                    {
                        try
                        {
                            var protocolVersion = reader.ReadUInt32();
                            if (protocolVersion != Message.ProtocolVersion)
                            {
                                throw new ArgumentException("Bad version of protocol");
                            }
                            List<String> arrayIpNetwork = reader.ReadArrayOfString().ToList();
                            for (int i = 0; i < length; i++)
                            {
                                for (int j = 0; j < length; j++)
                                {
                                    //read names of groups
                                    //read ceritificates
                                }
                                
                                //add those to dictionary
                            }

                            return new TcpMessage((Int32)protocolVersion, groupsOfEachPeer);
                        }
                        catch(IOException)
                        {
                            throw;
                        }
                    }
                }
            }
        }
    }
}

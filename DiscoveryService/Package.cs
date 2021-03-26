using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace DiscoveryServices
{
    class Package
    {
        private Dictionary<ProtocolVersion, String> protocolSupported;

        public Package()
        {
            protocolSupported = new Dictionary<ProtocolVersion, String>()
            {
                { ProtocolVersion.Tcp, "TCP/IP" },
                { ProtocolVersion.Kademilia, "Kademilia" }
            };
        }

        public String GetContainedOfPacket(ProtocolVersion usedProtocol, List<IPAddress> groupsSupported, Int32 TcpPort, String id)
        {
            StringBuilder stringBuilder = new StringBuilder();
            protocolSupported.TryGetValue(usedProtocol, out var protocol);
            stringBuilder.Append($"{protocol}&");

            foreach (var item in groupsSupported)
            {
                stringBuilder.Append($"{item}&");
            }

            stringBuilder.Append($"{TcpPort}&");
            stringBuilder.Append(id);

            return stringBuilder.ToString();
        }

        //It is not used
        List<String> GetGroupsSupported(List<String> ipsOfGroups, String nameFile, Int32 indexColumn, Char separator)
        {
            Boolean isFound = false;
            List<String> groupsSupported = new List<String>();

            using (var reader = new StreamReader(nameFile))
            {
                while (reader.Peek() >= 0 && !isFound)
                {
                    var fields = reader.ReadLine().Split(separator);
                    var ipGroupOfLine = fields[indexColumn];

                    if (ipsOfGroups.Contains(ipGroupOfLine))
                    {
                        groupsSupported.Add(ipGroupOfLine);
                    }
                }
            }

            if (groupsSupported.Count == 0)
            {
                throw new InvalidOperationException("This client doesn\'t belong to any group");
            }
            else
            {
                return groupsSupported;
            }
        }
    }
}

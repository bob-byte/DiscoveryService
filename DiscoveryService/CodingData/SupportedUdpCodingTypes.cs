using System;
using System.Collections.Generic;

namespace LUC.DiscoveryService.CodingData
{
    class SupportedUdpCodingTypes
    {
        private static readonly List<PropertyInUdpMessage> types = new List<PropertyInUdpMessage>
        {
            PropertyInUdpMessage.MessageId,
            PropertyInUdpMessage.MachineId,
            PropertyInUdpMessage.VersionOfProtocol,
            PropertyInUdpMessage.TcpPort
        };

        public Int32 Count => types.Count;

        public PropertyInUdpMessage this[Byte i]
        {
            get
            {
                if (0 <= i && i < types.Count)
                {
                    return types[i];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public Byte this[PropertyInUdpMessage type]
        {
            get
            {
                if (PropertyInUdpMessage.First <= type && type <= PropertyInUdpMessage.Last)
                {
                    var index = types.IndexOf(type);
                    if (index != -1)
                    {
                        return (Byte)index;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    throw new ArgumentNullException(nameof(type));
                }
            }
        }
    }
}

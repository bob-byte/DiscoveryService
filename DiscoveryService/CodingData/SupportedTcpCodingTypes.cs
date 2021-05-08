using System;
using System.Collections.Generic;

namespace LUC.DiscoveryService.CodingData
{
    class SupportedTcpCodingTypes
    {
        private static readonly List<PropertyInTcpMessage> types = new List<PropertyInTcpMessage> 
        { 
            PropertyInTcpMessage.MessageId,
            PropertyInTcpMessage.VersionOfProtocol,
            PropertyInTcpMessage.GroupsSupported,
            PropertyInTcpMessage.KnownIps,
        };

        public Int32 Count => types.Count;

        public PropertyInTcpMessage this[Byte i]
        {
            get
            {
                if(0 <= i && i < types.Count)
                {
                    return types[i];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public Byte this[PropertyInTcpMessage type]
        {
            get
            {
                if(PropertyInTcpMessage.First <= type && type <= PropertyInTcpMessage.Last)
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

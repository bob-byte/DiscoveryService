using LUC.DiscoveryService.Kademlia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class Range : ICloneable
    {
        public Range()
        {
            ;//do nothing
        }

        public Range(UInt64 start, UInt64 end, UInt64 total)
        {
            Start = start;
            End = end;
            Total = total;
        }

        public UInt64 Start { get; set; }

        public UInt64 End { get; set; }

        public UInt64 TotalPerContact { get; set; }

        public UInt64 Total { get; set; }

        public Object Clone() =>
            MemberwiseClone();

        public override String ToString() =>
            $"{Start}-{End}/{Total}";

        public override Boolean Equals(Object obj)
        {
            Boolean isEqual;
            if(obj is Range range)
            {
                isEqual = (range.Start == Start) && (range.End == End) && 
                    (range.TotalPerContact == TotalPerContact) && (range.Total == Total);
            }
            else
            {
                isEqual = false;
            }

            return isEqual;
        }

        public override Int32 GetHashCode()
        {
            Int32 hash = 13;

            hash = PartialHash(hash, Start);
            hash = PartialHash(hash, End);
            hash = PartialHash(hash, TotalPerContact);
            hash = PartialHash(hash, Total);

            return hash;
        }

        private Int32 PartialHash<T>(Int32 hash, T property) =>
            (hash * 7) + property.GetHashCode();
    }
}

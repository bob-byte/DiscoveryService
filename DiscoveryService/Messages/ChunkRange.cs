using LUC.DiscoveryService.Kademlia;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages
{
    class ChunkRange : ICloneable
    {
        public ChunkRange( UInt64 start, UInt64 end, UInt64 total )
            : this()
        {
            Start = start;
            End = end;
            Total = total;
        }

        public ChunkRange()
        {
            NumsUndownloadedChunk = new List<Int32>();
        }

        public UInt64 Start { get; set; }

        public UInt64 End { get; set; }

        public UInt64 TotalPerContact { get; set; }

        public UInt64 Total { get; set; }

        /// <summary>
        /// For debugging only
        /// </summary>
        public List<Int32> NumsUndownloadedChunk { get; }

        public Object Clone() =>
            MemberwiseClone();

        public override String ToString() =>
            $"{Start}-{End}/{Total}";

        public override Boolean Equals( Object obj )
        {
            Boolean isEqual;
            if ( obj is ChunkRange range )
            {
                isEqual = ( range.Start == Start ) && ( range.End == End ) &&
                    ( range.TotalPerContact == TotalPerContact ) && ( range.Total == Total );
            }
            else
            {
                isEqual = false;
            }

            return isEqual;
        }

        public override Int32 GetHashCode()
        {
            Int32 hash = HashCode.Combine( Start, End, TotalPerContact, Total );

            return hash;
        }
    }
}

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages
{
    public class ChunkRange : ICloneable
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
        public List<Int32> NumsUndownloadedChunk { get; set; }

        public Object Clone()
        {
            ChunkRange clone = (ChunkRange)MemberwiseClone();

            if(NumsUndownloadedChunk?.Count != 0)
            {
                Int32[] numsUndownloadedChunk = new Int32[ NumsUndownloadedChunk.Count ];
                NumsUndownloadedChunk.CopyTo( numsUndownloadedChunk );
                clone.NumsUndownloadedChunk = numsUndownloadedChunk.ToList();
            }

            return clone;
        }

        public override String ToString() =>
            Display.ObjectToString( this );

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

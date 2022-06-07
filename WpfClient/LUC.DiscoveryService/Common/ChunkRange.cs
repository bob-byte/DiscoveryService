using LUC.DiscoveryServices.Common;

using System;

namespace LUC.DiscoveryServices.Common
{
    public class ChunkRange : ICloneable
    {
        public ChunkRange( UInt64 start, UInt64 end, UInt64 total )
        {
            Start = start;
            End = end;
            Total = total;
        }

        public ChunkRange( UInt64 start, UInt64 end, UInt64 totalPerContact, UInt64 total )
        {
            Start = start;
            End = end;

            TotalPerContact = totalPerContact;
            Total = total;
        }

        public Boolean IsDownloaded { get; set; }

        public UInt64 Start { get; set; }

        public UInt64 End { get; set; }

        public UInt64 TotalPerContact { get; set; }

        public UInt64 Total { get; set; }

        public Object Clone()
        {
            var clone = (ChunkRange)MemberwiseClone();

            return clone;
        }

        public override String ToString() =>
            Display.ToString( this );

        public override Boolean Equals( Object obj )
        {
            Boolean isEqual;
            if ( obj is ChunkRange range )
            {
                isEqual = ( range.Start == Start ) && ( range.End == End ) && ( range.Total == Total );
            }
            else
            {
                isEqual = false;
            }

            return isEqual;
        }

        public override Int32 GetHashCode()
        {
            Int32 hash = HashCode.Combine( Start, End, Total );

            return hash;
        }
    }
}

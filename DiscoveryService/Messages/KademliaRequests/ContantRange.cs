using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class ContantRange
    {
        public ContantRange()
        {
            ;//do nothing
        }

        public ContantRange(Int64 start, Int64 end, Int64 total)
        {
            Start = start;
            End = end;
            Total = total;
        }

        public Int64 Start { get; set; }

        public Int64 End { get; set; }

        public Int64 TotalPerContact { get; set; }

        public Int64 Total { get; set; }

        public override String ToString() =>
            $"{Start}-{End}/{Total}";
    }
}

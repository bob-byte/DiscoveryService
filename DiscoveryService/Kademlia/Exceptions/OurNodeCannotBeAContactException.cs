using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class OurNodeCannotBeAContactException : Exception
	{
		public OurNodeCannotBeAContactException() { }
		public OurNodeCannotBeAContactException(string msg) : base(msg) { }
	}
}

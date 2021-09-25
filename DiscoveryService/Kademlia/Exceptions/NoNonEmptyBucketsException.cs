﻿using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class NoNonEmptyBucketsException : Exception
    {
        public NoNonEmptyBucketsException()
            : base()
        {
            ;//do nothing
        }

        public NoNonEmptyBucketsException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}
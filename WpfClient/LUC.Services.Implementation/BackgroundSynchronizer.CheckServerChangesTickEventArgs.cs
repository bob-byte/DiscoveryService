
using System;

namespace LUC.Services.Implementation
{
    partial class BackgroundSynchronizer
    {
        private class CheckServerChangesEventArgs
        {
            public CheckServerChangesEventArgs( String serverBucketName, String hexPrefix )
            {
                ServerBucketName = serverBucketName;
                HexPrefix = hexPrefix;
            }

            public String ServerBucketName { get; }

            public String HexPrefix { get; }
        }
    }
}

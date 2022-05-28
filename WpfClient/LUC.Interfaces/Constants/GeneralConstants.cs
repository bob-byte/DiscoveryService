using System;

namespace LUC.Interfaces.Constants
{
    public static class GeneralConstants
    {
        public const String NTFS_FILE_SYSTEM = "NTFS";

        public const UInt16 PROTOCOL_VERSION = 1;

        public const Int32 TOO_BIG_SIZE_TO_COMPUTE_MD5 = 100000000;

        public const String SHOW_DELETED_URI = "show-deleted=1";

        public const Int32 BYTES_IN_ONE_KILOBYTE = 1024;

        public const Int32 BYTES_IN_ONE_MEGABYTE = BYTES_IN_ONE_KILOBYTE * BYTES_IN_ONE_KILOBYTE;
    }
}

using System;

namespace LUC.Interfaces.Constants
{
    public static class DownloadConstants
    {
        public const String TEMP_FILE_NAME_EXTENSION = ".part";

        public const String FOLDER_NAME_FOR_DOWNLOADING_FILES = "LUC temp files";

        public const Int32 TOO_SMALL_FILE_SIZE_TO_SAVE_IN_CHANGES_QUEUE = 1000 * 1000 * 30;
    }
}

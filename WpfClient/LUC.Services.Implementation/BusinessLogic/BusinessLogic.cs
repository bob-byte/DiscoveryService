using System;

namespace LUC.Services.Implementation.BusinessLogics
{
    public static class BusinessLogic
    {
        public const Char SEPARATOR_IN_SERVER_BUCKET_NAME = '-';

        public const Int32 PART_COUNT_IN_SERVER_BUCKET_NAME = 4;

        public const Int32 INDEX_OF_PART_OF_GROUP_ID_IN_SERVER_BUCKET_NAME = 2;

        public static String GenerateBucketName( String tenantId, String groupId )
        {
            Char separator = SEPARATOR_IN_SERVER_BUCKET_NAME;
            String result = $"the{separator}{tenantId}{separator}{groupId}{separator}res";

            return result;
        }
    }
}

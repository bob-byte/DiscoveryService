using System;

namespace LUC.Globalization
{
    public static class SentenceTranslator
    {
        public static String ProvideMessageAboutLockedFile( String fileName, String userName, String userContacts )
        {
            String result = System.String.Format( Strings.MessageTemplate_LockedFile, fileName, userName, userContacts );
            return result;
        }

        public static String ProvideMessageAboutRenamedFile( String nameFrom, String nameTo )
        {
            String result = System.String.Format( Strings.MessageTemplate_RenamedFile, nameFrom, nameTo );
            return result;
        }
    }
}

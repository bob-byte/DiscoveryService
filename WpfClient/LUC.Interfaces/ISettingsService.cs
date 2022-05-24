using System;
using System.Collections.Generic;

namespace LUC.Interfaces
{
    public interface ISettingsService
    {
        ICurrentUserProvider CurrentUserProvider { get; set; }

        Boolean IsLogToTxtFile { get; }

        Boolean IsShowConsole { get; }

        String MachineId { get; }

        void ReadSettingsFromFile();

        void WriteUserRootFolderPath( String userRootFolderPath );

        void WriteMachineId( String machineId );

        void WriteBase64EncryptionKey( String base64Key );

        void WriteIsRememberPassword( Boolean isRememberPassword, String base64Password );

        void WriteLastSyncDateTime();

        String ReadRememberedLogin();

        String ReadBase64Password();

        String ReadBase64EncryptionKey();

        String ReadUserRootFolderPath();

        DateTime ReadLastSyncDateTime();

        void WriteLanguageCulture( String culture );

        String ReadLanguageCulture();

        IList<String> ReadFoldersToIgnore();

        void WriteFoldersToIgnore( IList<String> pathes );
    }
}

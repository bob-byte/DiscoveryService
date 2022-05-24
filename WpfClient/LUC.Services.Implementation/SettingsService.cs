using LUC.Interfaces;
using LUC.Interfaces.Models;
using LUC.Services.Implementation.Helpers;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace LUC.Services.Implementation
{
    [Export( typeof( ISettingsService ) )]
    public class SettingsService : ISettingsService
    {        
        private AppSettings m_current;

        public SettingsService()
        {
            LoggingService = new LoggingService
            {
                SettingsService = this
            };

            ReadSettingsFromFile();
        }

        private ILoggingService LoggingService { get; set; }

        public String MachineId => m_current.MachineId;

        [Import(typeof(ICurrentUserProvider))]
        public ICurrentUserProvider CurrentUserProvider { get; set; }

        public static AppSettings AppSettingsFromFile()
        {
            AppSettings appSettings;

            if (File.Exists(AppSettings.FilePath))
            {
                String json = File.ReadAllText( AppSettings.FilePath );

                if ( String.IsNullOrEmpty( json ) )
                {
                    appSettings = new AppSettings();
                }
                else
                {
                    appSettings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if ( appSettings == null )
                    {
                        appSettings = new AppSettings();
                    }
                }
            }
            else
            {
                appSettings = new AppSettings();
            }

            if ( appSettings.IsShowConsole )
            {
                ConsoleHelper.CreateConsole();
                Console.WriteLine("Console is launched.");
            }            

            return appSettings;
        }

        public void ReadSettingsFromFile()
        {
            m_current = AppSettingsFromFile();

            String machineId = Implementation.MachineId.Create();
            WriteMachineId( machineId );
        }

        private void SerializeSettingToFile()
        {
            try
            {
                String directoryWithSettings = Path.GetDirectoryName( AppSettings.FilePath );
                if ( !Directory.Exists( directoryWithSettings ) )
                {
                    _ = Directory.CreateDirectory(directoryWithSettings);
                }

                var serializer = new JsonSerializer();
                serializer.Converters.Add(new IsoDateTimeConverter());
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.DateFormatHandling = DateFormatHandling.IsoDateFormat;

                using ( var sw = new StreamWriter( AppSettings.FilePath ) )
                {
                    using ( JsonWriter writer = new JsonTextWriter( sw ) )
                    {
                        serializer.Serialize(writer, m_current);
                    }
                }
            }
            catch ( IOException ex )
            {
                LoggingService.LogCriticalError("Can't write settings to file, IO Exception", ex);
            }

            catch ( Exception ex )
            {
                LoggingService.LogCriticalError("SerializeSettingToFile error", ex);
            }
        }

        public String ReadLanguageCulture() => m_current.LanguageCulture;

        public String ReadUserRootFolderPath()
        {
            String login = GetUserLogin();

            UserSetting userSettings = m_current.SettingsPerUser.Single(x => x.Login == login);
            return userSettings.RootFolderPath;
        }

        public void WriteLanguageCulture(String culture)
        {
            if ( m_current.LanguageCulture != culture )
            {
                m_current.LanguageCulture = culture;
                SerializeSettingToFile();
            }
        }

        public void WriteMachineId(String machineId)
        {
            if (!m_current.MachineId.Equals(machineId, StringComparison.Ordinal))
            {
                m_current.MachineId = machineId;
                SerializeSettingToFile();
            }
        }

        public void WriteUserRootFolderPath(String userRootFolderPath)
        {
            String login = GetUserLogin();

            UserSetting userSettings = m_current.SettingsPerUser.Single(x => x.Login == login);

            if ( userSettings.RootFolderPath != userRootFolderPath )
            {
                userSettings.RootFolderPath = userRootFolderPath;
                SerializeSettingToFile();
            }
        }

        private String GetUserLogin()
        {
            if (!String.IsNullOrWhiteSpace(CurrentUserProvider.LoggedUser.Login))
            {
                String login = CurrentUserProvider.LoggedUser.Login;

                UserSetting userSettings = m_current.SettingsPerUser.SingleOrDefault(x => x.Login == login);

                if (userSettings == null)
                {
                    m_current.SettingsPerUser.Add(new UserSetting
                    {
                        Login = login
                    });
                }

                return login;
            }
            else
            {
                throw new InvalidOperationException(message: $"{nameof(CurrentUserProvider.LoggedUser.Login)} is null or white space");
            }
        }

        public String ReadRememberedLogin()
        {
            String possibleRemembered = m_current.SettingsPerUser.Single(x => x.IsRememberLogin).Login;
            return possibleRemembered;
        }

        //TODO: optimize it
        public void WriteIsRememberPassword(Boolean isRememberPassword, String base64Password)
        {
            String login = GetUserLogin();

            m_current.SettingsPerUser.ForEach(x => x.IsRememberLogin = false);
            m_current.SettingsPerUser.ForEach(x => x.IsRememberPassword = false);
            m_current.SettingsPerUser.ForEach(x => x.Base64Password = String.Empty);

            m_current.SettingsPerUser.Single(x => x.Login == login).IsRememberLogin = true;
            m_current.SettingsPerUser.Single(x => x.Login == login).IsRememberPassword = isRememberPassword;

            if ( isRememberPassword )
            {
                m_current.SettingsPerUser.Single(x => x.Login == login).Base64Password = base64Password;
            }

            SerializeSettingToFile();
        }

        public void WriteLastSyncDateTime()
        {
            String login = GetUserLogin();
            m_current.SettingsPerUser.Single(x => x.Login == login).LastSyncDateTime = DateTime.UtcNow;

            SerializeSettingToFile();
        }

        public String ReadBase64Password()
        {
            String possibleRemembered = m_current.SettingsPerUser.SingleOrDefault(x => x.IsRememberPassword)?.Base64Password;
            return possibleRemembered;
        }

        public DateTime ReadLastSyncDateTime()
        {
            DateTime? possibleDateTime = m_current.SettingsPerUser.SingleOrDefault(x => x.IsRememberPassword)?.LastSyncDateTime; // TODO RR Why isremembered

            return possibleDateTime.GetValueOrDefault(DateTime.UtcNow);
        }

        public void WriteBase64EncryptionKey(String base64Key) // TODO RR What else per login? Change to MachineID?
        {
            String login = GetUserLogin();

            m_current.SettingsPerUser.Single(x => x.Login == login).Base64EncryptionKey = base64Key;

            SerializeSettingToFile();
        }

        public String ReadBase64EncryptionKey()
        {
            String login = GetUserLogin();
            String possibleKey = m_current.SettingsPerUser.SingleOrDefault(x => x.Login == login)?.Base64EncryptionKey;
            return possibleKey;
        }

        public Boolean IsLogToTxtFile => m_current.IsLogToTxtFile;

        public Boolean IsShowConsole => m_current.IsShowConsole;

        public IList<String> ReadFoldersToIgnore()
        {
            String login = GetUserLogin();
            IList<String> result = m_current.SettingsPerUser.SingleOrDefault(x => x.Login == login)?.FoldersToIgnore;

            return result ?? new List<String>();
        }

        public void WriteFoldersToIgnore(IList<String> pathes)
        {
            String login = GetUserLogin();
            m_current.SettingsPerUser.Single(x => x.Login == login).FoldersToIgnore = pathes;

            SerializeSettingToFile();
        }

        protected void Init()
        {
            LoggingService = new LoggingService();
            ReadSettingsFromFile();
        }
    }
}

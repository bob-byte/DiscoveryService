using System;
using System.Collections.Generic;

namespace LUC.Interfaces.Models
{
    public class UserSetting
    {
        public UserSetting()
        {
            FoldersToIgnore = new List<String>();
        }

        public String Login { get; set; }

        public String RootFolderPath { get; set; }

        public Boolean IsRememberLogin { get; set; }

        public Boolean IsRememberPassword { get; set; }

        public String Base64Password { get; set; }

        public String Base64EncryptionKey { get; set; }

        public DateTime LastSyncDateTime { get; set; }

        public IList<String> FoldersToIgnore { get; set; }
    }
}


using System;

namespace LUC.Interfaces
{
    public interface IAppSettings
    {
        String MachineId { get; }

        String LanguageCulture { get; }

        Boolean IsShowConsole { get; }
    }
}

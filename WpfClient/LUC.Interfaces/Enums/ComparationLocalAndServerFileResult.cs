using System;

namespace LUC.Interfaces.Enums
{
    [Flags]
    public enum ComparationLocalAndServerFileResult
    {
        None = 0,

        DoesntExistOnServer = 1,

        DoesntExistLocally = 2,

        ///<remarks>
        /// In current realization it can't be, because version is updated in local PC only after upload
        ///</remarks>
        OlderOnServer = 4,

        Equal = 8,

        NewerOnServer = 16,
    }
}

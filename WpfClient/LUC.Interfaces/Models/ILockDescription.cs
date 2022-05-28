using LUC.Interfaces.Enums;

using System;

namespace LUC.Interfaces.Models
{
    public interface ILockDescription
    {
        AdsLockState LockState { get; }

        DateTime LockTimeUtc { get; set; }

        String LockUserId { get; set; }

        String LockUserTel { get; set; }

        String LockUserName { get; set; }
    }
}

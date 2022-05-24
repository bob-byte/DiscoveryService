using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using System;

namespace LUC.Interfaces.Models
{
    public class LockDescription : ILockDescription
    {
        public LockDescription( AdsLockState lockState )
        {
            LockState = lockState;
            LockUserId = String.Empty;
            LockUserTel = String.Empty;
            LockUserName = String.Empty;
            LockTimeUtc = DateTime.UtcNow;
        }

        public LockDescription( String str )
        {
            String[] array = str.Split( ':' );

            if ( array.Length != 5 )
            {
                throw new ArgumentException( "FromString does not work." );
            }

            if ( !Enum.TryParse( array[ 0 ], out AdsLockState parsedState ) )
            {
                throw new ArgumentException( "FromString does not work." );
            }

            if ( !Int64.TryParse( array[ 1 ], out Int64 parsedDateTime ) )
            {
                throw new ArgumentException( "FromString does not work." );
            }

            LockTimeUtc = parsedDateTime.FromUnixTimeStampToDateTime();

            LockState = parsedState;

            LockUserId = array[ 2 ];
            LockUserTel = array[ 3 ];
            LockUserName = array[ 4 ];
        }

        public AdsLockState LockState { get; private set; }

        public DateTime LockTimeUtc { get; set; }

        public String LockUserId { get; set; }

        public String LockUserTel { get; set; }

        public String LockUserName { get; set; }

        public override String ToString() => $"{LockState}:{LockTimeUtc.FromDateTimeToUnixTimeStamp()}:{LockUserId}:{LockUserTel}:{LockUserName}";
    }
}

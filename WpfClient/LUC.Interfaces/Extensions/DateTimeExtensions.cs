using System;
using System.IO;

namespace LUC.Interfaces.Extensions
{
    public static class DateTimeExtensions
    {
        public static Boolean AssumeIsTheSame( this DateTime dateTime1, DateTime dateTime2 ) => dateTime1.Year == dateTime2.Year
                && dateTime1.Month == dateTime2.Month
                && dateTime1.Day == dateTime2.Day
                && dateTime1.Hour == dateTime2.Hour
                && dateTime1.Minute == dateTime2.Minute
                && dateTime2.Second - dateTime1.Second < 2;

        public static DateTime LastWriteTimeUtcWithCorrectOffset( String pathFile )
        {
            DateTime now = DateTime.Now;
            TimeSpan localOffset = now - now.ToUniversalTime();
            DateTime lastModified = File.GetLastWriteTimeUtc( pathFile ) + localOffset;

            return lastModified;
        }
    }
}

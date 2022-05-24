using System;

namespace LUC.Interfaces
{
    internal static class ObjectNameValidator
    {
        private static readonly Char[] s_windowsReservedCharachters = new Char[]
        {
            '<', '>', ':', '"', '/', '\\', '|', '?', '*'
        };

        internal static Boolean HasWindowsReservedCharachters( this String name )
        {
            switch ( name.IndexOfAny( s_windowsReservedCharachters ) )
            {
                case -1:
                    return false;
                default:
                    return true;
            }
        }
        internal static Boolean IsSupportableFileName( this String name )
        {
            if ( name.HasWindowsReservedCharachters() )
            {
                return false;
            }
            else
            {
                switch ( name )
                {
                    case ".riak_action_log.xml":
                    case ".riak_index.lock":
                    case ".riak_index.etf":
                    case "desktop.ini":
                    case "thumbs.db":
                    case ".ds_store":
                    case ".dropbox":
                    case ".dropbox.attr":
                        return false;
                    default:
                        return true;
                }
            }
        }
    }
}

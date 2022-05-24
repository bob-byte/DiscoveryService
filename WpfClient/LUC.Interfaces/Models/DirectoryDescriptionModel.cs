using System.Diagnostics;

namespace LUC.Interfaces.Models
{
    [DebuggerDisplay( "PrefixFromHexString = {PrefixFromHexString} StringName = {StringName} IsDeleted = {IsDeleted}" )]
    public class DirectoryDescriptionModel
    {
        public System.String Prefix { get; set; } // Has hex format

        public System.Boolean IsDeleted { get; set; }

        public System.String StringName { get; set; }

        public System.String PrefixFromHexString { get; set; }
    }
}

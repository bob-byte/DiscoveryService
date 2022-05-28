using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneratePathsToFileInDirAndSubDirForInnoSetup
{
    class InnoSetup
    {
        public const String SourceAttr = "Source";
        public const String DestDirAttr = "DestDir";
        public const String FlagsAttr = "Flags";
        public const String IconsAttr = "Icons";

        public static readonly Dictionary<FlagValue, String> ValueFlagsAttr = new Dictionary<FlagValue, String>
        { { FlagValue.IgnoreVersion, "ignoreversion" } };

        public String RowInInnoSetup(String shortPathForInnoSetup, String subFolder, String valueFlagsAttr) =>
            $"{InnoSetup.SourceAttr}: \"{shortPathForInnoSetup}\"; " +
            $"{InnoSetup.DestDirAttr}: \"{{app}}\\{subFolder}\"; " +
            $"{InnoSetup.FlagsAttr}: {valueFlagsAttr}";
    }
}

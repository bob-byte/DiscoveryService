using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LUC.Interfaces.Models
{
    [DebuggerDisplay( "OriginalFullPath = {OriginalFullPath}" )]
    public class ObjectChangeDescription : ICloneable
    {
        private static Int64 s_previousId;

        public ObjectChangeDescription( String path, FileSystemEventArgs eventArgs )
        {
            OriginalFullPath = path;
            Change = eventArgs;
            Id = Interlocked.Increment( ref s_previousId );
        }

        public String OriginalFullPath { get; set; }

        public FileSystemEventArgs Change { get; private set; }

        public Int64 Id { get; }

        public Boolean IsProcessed { get; set; }

        public Object Clone()
        {
            ObjectChangeDescription clone = MemberwiseClone() as ObjectChangeDescription;
            String rootFolder = Change.FullPath.Replace( oldValue: Change.Name, newValue: String.Empty );

            if(Change is RenamedEventArgs renamedEventArgs)
            {
                clone.Change = new RenamedEventArgs( Change.ChangeType, rootFolder, renamedEventArgs.Name, renamedEventArgs.OldName );
            }
            else
            {
                clone.Change = new FileSystemEventArgs( Change.ChangeType, rootFolder, Change.Name );
            }

            return clone;
        }

        public override System.Boolean Equals( Object obj ) =>
            ( obj is ObjectChangeDescription objectChangeDescription ) && ( objectChangeDescription.Id == Id );

        public override Int32 GetHashCode() =>
            Id.GetHashCode();
    }
}

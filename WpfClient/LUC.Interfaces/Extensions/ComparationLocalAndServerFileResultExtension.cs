using LUC.Interfaces.Enums;

using System;

namespace LUC.Interfaces.Extensions
{
    public static class ComparationLocalAndServerFileResultExtension
    {
        public static Boolean ShouldFileBeUploaded( this ComparationLocalAndServerFileResult comparationResult ) =>
            ( ( comparationResult == ComparationLocalAndServerFileResult.DoesntExistOnServer ) ||
              ( comparationResult == ComparationLocalAndServerFileResult.OlderOnServer ) ) &&
            //TODO: use method HasFlag
            ( ( comparationResult != ( ComparationLocalAndServerFileResult.DoesntExistLocally | ComparationLocalAndServerFileResult.DoesntExistOnServer ) )  ||
              ( comparationResult != ComparationLocalAndServerFileResult.DoesntExistLocally ) );

        public static Boolean IsFileNewerOnServer( this ComparationLocalAndServerFileResult comparationResult ) =>
                ( ( comparationResult == ComparationLocalAndServerFileResult.DoesntExistLocally ) &&
                  ( comparationResult != ComparationLocalAndServerFileResult.DoesntExistOnServer ) ) ||
                ( comparationResult == ComparationLocalAndServerFileResult.NewerOnServer );
    }
}

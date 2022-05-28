using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.Interfaces.Enums
{
    public enum Errors
    {
        FileSizeDoNotMatchHeaders = 1,
        NoDataReceived = 2,
        IncorrectUserNameOrPassword = 3,
        InvalidUploadIdOrGUID = 4,
        SomethingsWentWrong = 5,
        BackendIsUnavailableAtTheMoment = 6,
        BucketNotFound = 7,
        ObjectKeyIsRequired = 8,
        IncorrectObjectName = 9,
        DirectoryExistsAlready = 10,
        PrefixDoNotExist = 11,
        DirectoryNameIsRequired = 12,     //Directory name is required. Can't contain any of the following characters: \" < > \\ | / : * ?",
        NoFilesToMove = 13,               //No files to move. The destination directory might be subdirectory of the source directory.",
        IncorrectSourceOrDestinationPrefix = 14,
        IncorrectSrc_object_keys = 15,
        OnlyPOSTrequestsAreSupportedByThisAPIendpoint = 16,
        NotFound = 17,
        MethodNotAllowed = 18,
        UserIsNotActive = 19,
        NoFilesToCopy = 20,                   //No files to copy. The destination directory might be subdirectory of the source directory.",
        JSONparsingError = 21,
        IncorrectVersionVector = 22,
        IncorrectContentRangeHeader = 23,
        FileSizeExceedsTheLimit = 24,
        UploadIDandPartNumberMustBeSpecified = 25,
        DestinationBucketCannotBeModified = 26,
        NoAccessToSourceBucket = 27,
        IncorrectAccessToken = 28,
        ObjectExistsAlready = 29,
        SourceObjectDoNotExist = 30,
        DuplicateObjectKeysWereFoundInSrc_object_keys = 31,
        SourcePseudoDirectoryDoNotExist = 32,
        InfrastructureControllerIsUnableToProcessSoManyRequests = 33,
        EmptyObject_keys = 34,
        FailedToRenameObject = 35,
        IncorrectPrefix = 36,
        YouDoNotHaveAccessToThisBucket = 37,
        TokenExpired = 38,
        AccessDenied = 39,
        IncorrectMd5 = 40,
        UnknownOperation = 41,
        IncorrectGUID = 42,
        Locked = 43,
        UnknownVersion = 44,
        AlreadyUpToDate = 45,
        OnlyOneObjectKeyAllowedWhenDst_nameSpecified = 46,
        FilenameProhibited = 47,
        FilenameTooLong260Characters = 48,
        UnexpectedEtagsField = 49,
        NoBinaryDataIsExpectedWithTheFirstRequest = 50,
        EtagsDoNotMatch = 51,
        ContentRangeHeaderIsRequired = 52
    }
}

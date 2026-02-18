namespace Dataisland.Core.Domain.Enums;

public enum MembersState
{
    All = 0,
    ActiveOnly = 1,
    DeletedOnly = 2
}

public enum LimitActionType
{
    UploadFile = 0,
    CreateChat = 1,
    AskQuestion = 2,
    CreateWorkspace = 3,
    CreateOrganization = 4,
    FileSizeKb = 5
}

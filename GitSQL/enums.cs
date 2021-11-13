namespace GitSQL;
enum ArgState
{
    Unknown = 0,
    DisplayHelp = 1,
    Valid = 2,
    Invalid = 3,
    Edit = 4,
    Creds = 5,
    GetCreds = 6,
    RemoveCreds = 7,
    View = 8
}
enum OffsetType
{
    Unknown = 0,
    Hours = 1,
    Days = 2,
    Minutes = 3,
    ExactDateTime = 4
}

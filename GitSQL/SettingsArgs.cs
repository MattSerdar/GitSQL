namespace GitSQL;
public class SettingsArgs
{
    public DateTimeOffset SearchFromDate { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset SearchToDate { get; set; } = DateTimeOffset.MinValue;
    public List<string> IgnoredFilePattern { get; set; } = new List<string>();
    public List<string> DirectorySortOrder { get; set; } = new List<string>();
    public List<string> FileSearchPattern { get; set; } = new List<string>();
    public string OutputFileName { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public List<string> RawArgs { get; set; } = new List<string>();
    public string RepoPersonalAccessToken { get; set; } = "";
}

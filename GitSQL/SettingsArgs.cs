namespace GitSQL;
public class SettingsArgs : Args
{
    public List<string> RawArgs { get; set; } = new List<string>();
    public string RepoPersonalAccessToken { get; set; } = "";
    public string OutputFileNamePath
    {
        get
        {
            return Path.Combine(base.OutputDirectory, base.OutputFileName);
        }
    }
}
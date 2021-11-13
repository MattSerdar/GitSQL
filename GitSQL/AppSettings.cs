namespace GitSQL;
public class AppSettings
{
    public GitHub GitHub { get; set; } = new GitHub();
    public Args Args { get; set; } = new Args();
    public Output Output { get; set; } = new Output(); 
}
public class GitHub
{
    public string ProductHeader { get; set; } = "";
    public string RepoOwnerName { get; set; } = "";
    public string RepoName { get; set; } = "";
    public string RepoPath { get; set; } = "";
}

public class Args
{
    public DateTimeOffset SearchFromDate { get; set; }
    public DateTimeOffset SearchToDate { get; set; }
    public string OutputFileName { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public List<string> IgnoredFilePattern { get; set; } = new List<string>();
    public List<string> DirectorySortOrder { get; set; } = new List<string>();
    public List<string> FileSearchPattern { get; set; } = new List<string>();
    public List<string> FileExtensions { get; set; } = new List<string>();
    public List<string> SubDirectories { get; set; } = new List<string>();
}
public class Output
{
    public bool LogToConsole { get; set; }
    public bool TruncateOutputFiles { get; set; }
}

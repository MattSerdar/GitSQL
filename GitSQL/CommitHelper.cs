namespace GitSQL;
public class GitResult
{
    public List<ValidCommit> ValidCommits = new List<ValidCommit>();
    public int CountOfCommits { get; set; }
}
public class ValidCommit
{
    public ValidCommit()
    {
        CommitFiles = new List<CommitFile>();
    }
    public string Directory { get; set; } = "";
    public int DirectorySortOrder { get; set; }
    public List<CommitFile> CommitFiles { get; set; }
}
public class CommitFile
{
    public string FileName { get; set; } = "";
    public string Sha { get; set; } = "";
}
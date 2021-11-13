namespace GitSQL;
public class Git
{
    private readonly GitHubClient client;
    private int DirectorySortOrder = 0;
    private readonly SettingsArgs Settings;
    private readonly GitHub GitHubSettings;
    private readonly Output OutputSettings;

    public Git(SettingsArgs args, GitHub gitHub, Output output)
    {
        GitHubSettings = gitHub;
        Settings = args;
        OutputSettings = output;
        client = new GitHubClient(new Octokit.ProductHeaderValue(gitHub.ProductHeader));
        if (!string.IsNullOrWhiteSpace(args.RepoPersonalAccessToken))
        {
            client.Credentials = new Credentials(args.RepoPersonalAccessToken);
        }
        else
        {
            throw new ArgumentException("Missing credentials to view Github");
        }
        Init();
    }
    public static string ValidateClientCredentials(string pat, string productHeader)
    {
        string msg = "";
        try
        {
            var c = new GitHubClient(new Octokit.ProductHeaderValue(productHeader))
            {
                Credentials = new Credentials(pat)
            };
            var currentUser = c.User.Current().GetAwaiter().GetResult();
        }
        catch (Exception exc)
        {
            msg = exc.Message;
        }
        return msg;
    }
    public GitResult GitResults { get; private set; } = new GitResult();

    #region private methods
    private void Init()
    {
        TruncateDir();
    }
    private StringBuilder GetOutputHeader()
    {
        string dateStartSearch = Settings.SearchFromDate != DateTimeOffset.MinValue ? Settings.SearchFromDate.ToString() : Utils.NotSet;
        string dateEndSearch = Settings.SearchToDate != DateTimeOffset.MinValue ? Settings.SearchToDate.ToString() : Utils.NotSet;

        var sb = new StringBuilder();
        sb.AppendLine("/*");

        sb.AppendFormat("There were a total of {0} files found.{1}", GitResults.ValidCommits.Sum(e => e.CommitFiles.Count), Environment.NewLine);
        if (Settings.RawArgs.Count > 0)
        {
            sb.AppendFormat("{0}The command line args used to get the files were the following:{0}", Environment.NewLine);
            foreach (var a in Settings.RawArgs)
            {
                sb.AppendLine(string.Format("  {0}", a));
            }
        }
        sb.AppendLine("");

        sb.AppendLine("The following settings were used:");
        sb.AppendFormat($"  {Utils.AppName}:       {Utils.CurrentVersion}{Environment.NewLine}");
        sb.AppendFormat($"  RepoName:              {GitHubSettings.RepoName}{Environment.NewLine}");
        sb.AppendFormat($"  Search from date:      {dateStartSearch}{Environment.NewLine}");
        sb.AppendFormat($"  Search to date:        {dateEndSearch}{Environment.NewLine}");
        sb.AppendFormat($"  Output file name:      {Path.Combine(Settings.OutputDirectory, Settings.OutputFileName)}{Environment.NewLine}");
        sb.AppendFormat($"  Ignored file pattern:  {String.Join(", ", Settings.IgnoredFilePattern)}{Environment.NewLine}");
        sb.AppendFormat($"  File search pattern:   {String.Join(", ", Settings.FileSearchPattern)}{Environment.NewLine}");
        sb.AppendFormat($"  Directory Sort Order:  {String.Join(", ", Settings.DirectorySortOrder)}{Environment.NewLine}");
        sb.AppendFormat($"  Sub Directories:       {String.Join(", ", Settings.SubDirectories)}{Environment.NewLine}");

        sb.AppendFormat($"  File Extensions:       {String.Join(", ", Settings.FileExtensions)}{Environment.NewLine}");
        sb.AppendFormat($"  Truncate Output Files: {OutputSettings.TruncateOutputFiles}{Environment.NewLine}");
        sb.AppendLine("");


        if (GitResults.ValidCommits.Count > 0)
        {
            sb.AppendFormat("This is the list of files that make up the content{0}", Environment.NewLine);
            foreach (var vc in GitResults.ValidCommits)
            {
                foreach (var f in vc.CommitFiles.OrderBy(e => e.FileName))
                {
                    sb.AppendLine("  " + f.FileName);
                }
            }

        }

        sb.AppendLine("*/");
        return sb;
    }
    private void GetFiles()
    {
        TruncateDir();
        var utf8 = new UTF8Encoding(false);
        if (GitResults.ValidCommits.Count > 0)
        {
            StringBuilder fNamesList = GetOutputHeader();

            fNamesList.AppendLine();
            fNamesList.AppendFormat("IF DB_NAME() = 'master' BEGIN{0}", Environment.NewLine);
            fNamesList.AppendFormat("    RAISERROR('MASTER DB is currently active. If this is correct then delete this block. Otherwise change your target DB.', 20, 1) WITH LOG;{0}", Environment.NewLine);
            fNamesList.AppendFormat("END{0}GO{0}", Environment.NewLine);
            fNamesList.AppendLine();

            using (var sw = new StreamWriter(Settings.OutputFileNamePath, false, utf8))
            {
                sw.Write(fNamesList.ToString());
                foreach (var vc in GitResults.ValidCommits)
                {
                    foreach (var f in vc.CommitFiles.OrderBy(e => e.FileName))
                    {
                        try
                        {
                            var blob = client.Git.Blob.Get(GitHubSettings.RepoOwnerName, GitHubSettings.RepoName, f.Sha);
                            var n = Encoding.UTF8.GetString(Convert.FromBase64String(blob.Result.Content)) + Environment.NewLine;
                            if (n.Length > 0)
                            {
                                n = CleanupText(n);
                            }
                            sw.Write(n);
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.Message);
                        }
                    }
                }
            }
}
else
{
            using (var sw = new StreamWriter(Settings.OutputFileNamePath, false, utf8))
            {
                sw.Write(GetOutputHeader().ToString());
            }
        }
        Utils.OpenFile(Settings.OutputFileNamePath);
    }
    private string CleanupText(string txt)
    {
        txt = CleanupTextCore(txt, (char)65533);
        txt = CleanupTextCore(txt, (char)65279);
        txt = CleanupTextCore(txt, (char)0);
        return txt;
    }
    private string CleanupTextCore(string txt, char badChar)
    {
        int idx = txt.IndexOf(badChar);
        while (idx > -1)
        {
            txt = txt.Remove(idx, 1);
            idx = txt.IndexOf(badChar);
        }
        return txt;
    }
    private string GetFileName(string dir, string name)
    {
        string fName = Path.Combine(dir, name);
        int count = 0;
        int specialCount = GetSpecialCharacterCount(name);
        while (File.Exists(fName))
        {
            FileInfo fi = new FileInfo(fName);
            string ext = fi.Extension;
            string nameOnly = GetCleanFileName(fi.Name.Replace(ext, ""), specialCount);
            string newName = String.Format("{0}_{1}{2}", nameOnly, count, ext);
            fName = Path.Combine(dir, newName);
            count++;
        }

        return fName;
    }
    private void TruncateDir()
    {
        if (!OutputSettings.TruncateOutputFiles)
        {
            return;
        }
        // get the file name and try to match and
        // delete only similar ones
        var fi = new FileInfo(Settings.OutputFileName);
        string[] filesToDelete = Directory.GetFiles(Settings.OutputDirectory, GetWildCardNameToSearch(fi));
        DeleteFiles(filesToDelete);
    }
    private string GetWildCardNameToSearch(FileInfo file)
    {
        string fileName = file.Name.Replace(file.Extension, "*" + file.Extension);
        return fileName;
    }
    private void DeleteFiles(string[] files)
    {
        foreach (var f in files)
        {
            try
            {
                File.Delete(f);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("There was the following error while trying delete file: {0}{1}{2}",
                    f,
                    Environment.NewLine,
                    e.ToString()));
            }
        }
    }
    // this will count the number of characters in the file name
    // in our case the character we're counting is the underscore character "_"
    private int GetSpecialCharacterCount(string name)
    {
        int c = 0;
        if (!String.IsNullOrWhiteSpace(name))
        {
            int start = name.Length;
            int end = name.Replace("_", "").Length;
            c = start - end;
        }
        return c;
    }
    private string GetCleanFileName(string name, int specialCount)
    {
        string clean = name;
        string[] arr = name.Split(new char[] { '_' });
        if (arr.Length > 0)
        {
            clean = String.Join("_", arr, 0, specialCount + 1);
        }
        return clean;
    }
    public async Task GetCommitsAsync()
    {
        InitDirectorySortOrder();
        var x = new CommitRequest()
        {
            Since = Settings.SearchFromDate == DateTimeOffset.MinValue ? null : Settings.SearchFromDate,
            Path = GitHubSettings.RepoPath,
            Until = Settings.SearchToDate == DateTimeOffset.MinValue ? null : Settings.SearchToDate,
        };

        Utils.ConsoleColorCyan();
        if (x.Since.HasValue && x.Until.HasValue)
        {
            WriteLineToConsole(string.Format("Search for commits between {0} and {0}", x.Since, x.Until));
        }
        else if (x.Since.HasValue)
        {
            WriteLineToConsole(string.Format("Search for commits on or after {0}", x.Since));
        }
        else if (x.Until.HasValue)
        {
            WriteLineToConsole(string.Format("Search for commits on or before {0}", x.Until));
        }
        Console.ResetColor();
        Console.WriteLine();
        var commits = await client.Repository.Commit.GetAll(GitHubSettings.RepoOwnerName, GitHubSettings.RepoName, x);

        GitResults.CountOfCommits = commits.Count;

        Utils.ConsoleColorCyan();
        Console.WriteLine($"Found {commits.Count} commit{(commits.Count > 1 ? "s" : "")}");
        Console.ResetColor();
        Console.WriteLine();

        int count = 0;
        foreach (var c in commits)
        {
            Utils.ConsoleColorCyan();
            WriteLineToConsole($"Commit {++count} of {commits.Count}");
            WriteLineToConsole($"Author = {c.Commit.Author.Name}");
            WriteLineToConsole($"Date = {c.Commit.Author.Date.ToLocalTime()}");
            WriteLineToConsole($"SHA = {c.Sha}");
            WriteLineToConsole($"Url = {c.HtmlUrl}");
            Console.ResetColor();
            var sngCommit = await client.Repository.Commit.Get(GitHubSettings.RepoOwnerName, GitHubSettings.RepoName, c.Sha);
            foreach (var a in sngCommit.Files)
            {
                if (IsValidCommitFile(a))
                {
                    WriteLineToConsole(string.Format("  # of changes = {0} - {1}", a.Changes, a.Filename));
                }
                else
                {
                    Utils.ConsoleColorYellow();
                    WriteToConsole($"IGNORING {a.Filename} - ");
                    Utils.ConsoleColorDarkCyan();
                    WriteLineToConsole($"<{a.Status}>");
                    Console.ResetColor();
                }
            }
            WriteLineToConsole("");
        }

        if (GitResults.ValidCommits.Count > 0)
        {
            GitResults.ValidCommits = GitResults.ValidCommits.OrderBy(e => e.DirectorySortOrder).ThenBy(e => e.Directory).ToList();
        }
        GetFiles();
    }
    private void WriteLineToConsole(string txt)
    {
        if (OutputSettings.LogToConsole)
        {
            Console.WriteLine(txt);
        }
    }
    private void WriteToConsole(string txt)
    {
        if (OutputSettings.LogToConsole)
        {
            Console.Write(txt);
        }
    }
    private bool IsValidCommitFile(GitHubCommitFile file)
    {
        var fn = file.Filename.Split(new char[] { '/' });
        string fName = "";
        string dirName = "";
        if (fn.Length > 1)
        {
            fName = fn[fn.Length - 1];
            dirName = file.Filename.Substring(0, file.Filename.LastIndexOf('/'));
        }
        else
        {
            return false;
        }

        bool isValid = !file.Status.Equals("removed", StringComparison.OrdinalIgnoreCase);

        if (isValid)
        {
            isValid = IsValidExtension(fName);
        }
        if (isValid)
        {
            isValid = IsValidSubDirectory(fn);
        }
        if (isValid)
        {
            isValid = !IsIgnoredFile(fName);
        }
        if (isValid)
        {
            isValid = IsPatternMatch(fName);
        }

        if (isValid)
        {
            ValidCommit? existing = GitResults.ValidCommits.SingleOrDefault(e => e.Directory.Equals(dirName, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null)
            {
                if (!existing.CommitFiles.Select(e => e.FileName).Contains(fName, StringComparer.CurrentCultureIgnoreCase))
                {
                    existing.CommitFiles.Add(new CommitFile()
                    {
                        FileName = fName,
                        Sha = file.Sha
                    });
                }
            }
            else
            {
                var vc = new ValidCommit()
                {
                    Directory = dirName,
                    DirectorySortOrder = GetDirectorySortOrder(dirName)
                };
                vc.CommitFiles.Add(new CommitFile()
                {
                    FileName = fName,
                    Sha = file.Sha
                });

                GitResults.ValidCommits.Add(vc);
            }
        }
        return isValid;
    }
    private int GetDirectorySortOrder(string dirName)
    {
        int i = 0;
        if (dirName.Contains("/"))
        {
            var tmpName = dirName.Split(new char[] { '/' });

            foreach (var d in Settings.DirectorySortOrder)
            {
                i++;
                foreach (var t in tmpName)
                {
                    if (d.Equals(t, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
        }
        else
        {
            foreach (var d in Settings.DirectorySortOrder)
            {
                i++;
                if (d.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return DirectorySortOrder++;
    }
    private void InitDirectorySortOrder()
    {
        DirectorySortOrder = Settings.DirectorySortOrder.Count + 1;
    }
    private bool IsValidSubDirectory(string[] fileparts)
    {
        if (Settings.SubDirectories.Count == 0)
        {
            return true;
        }
        bool valid = false;
        foreach (var p in fileparts)
        {
            valid = Settings.SubDirectories.Contains(p, StringComparer.CurrentCultureIgnoreCase);
            if (valid)
            {
                break;
            }
        }
        return valid;
    }
    private bool IsValidExtension(string fileName)
    {
        if (Settings.FileExtensions.Count > 0)
        {
            var fnName = new FileInfo(fileName);
            return Settings.FileExtensions.Contains(fnName.Extension, StringComparer.CurrentCultureIgnoreCase);
        }
        return true;
    }
    private bool IsIgnoredFile(string fileName)
    {
        bool skip = false;
        if (Settings.IgnoredFilePattern.Count > 0)
        {
            var fi = new FileInfo(fileName);
            foreach (var p in Settings.IgnoredFilePattern)
            {
                skip = fi.Name.Like(p);
                if (skip)
                {
                    break;
                }
            }
        }
        return skip;
    }
    private bool IsPatternMatch(string fileName)
    {
        bool isMatch = Settings.FileSearchPattern.Count == 0;
        if (Settings.FileSearchPattern.Count > 0)
        {
            var fi = new FileInfo(fileName);
            foreach (var p in Settings.FileSearchPattern)
            {
                isMatch = fi.Name.Like(p);
                if (isMatch)
                {
                    break;
                }
            }
        }
        return isMatch;
    }
    #endregion
}

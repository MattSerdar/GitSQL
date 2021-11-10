namespace GitSQL;
public class Git
{
    private readonly GitHubClient client;
    //private GitResult Result = new GitResult();
    private int DirectorySortOrder = 0;
    private readonly SettingsArgs args;

    public Git(SettingsArgs args)
    {
        this.args = args;
        client = new GitHubClient(new Octokit.ProductHeaderValue(Settings.ProductHeader));
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
    public string OutputLocation { get; private set; } = "";
    public static string ValidateClientCredentials(string pat)
    {
        string msg = "";
        try
        {
            var c = new GitHubClient(new Octokit.ProductHeaderValue(Settings.ProductHeader))
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
    public static string ValidateExistingClientCredentials()
    {
        return ValidateClientCredentials(Utils.GetRepoPersonalAccessToken());
    }
    public GitResult GitResults { get; private set; } = new GitResult();
    //public void CreateOutputFile()
    //{
    //    GetFiles();
    //}

    #region private methods
    private void Init()
    {
        TruncateDir();
        OutputLocation = GetFileName(args.OutputDirectory, args.OutputFileName);
    }
    private StringBuilder GetOutputHeader()
    {
        string dateStartSearch = args.SearchFromDate != DateTimeOffset.MinValue ? args.SearchFromDate.ToString() : Utils.NotSet;
        string dateEndSearch = args.SearchToDate != DateTimeOffset.MinValue ? args.SearchToDate.ToString() : Utils.NotSet;

        var sb = new StringBuilder();
        sb.AppendLine("/*");

        sb.AppendFormat("There were a total of {0} files found.{1}", GitResults.ValidCommits.Sum(e => e.CommitFiles.Count), Environment.NewLine);
        if (args.RawArgs.Count > 0)
        {
            sb.AppendFormat("{0}The command line args used to get the files were the following:{0}", Environment.NewLine);
            foreach (var a in args.RawArgs)
            {
                sb.AppendLine(string.Format("  {0}", a));
            }
        }
        sb.AppendLine("");

        sb.AppendLine("The following settings were used:");
        sb.AppendFormat($"  {Settings.AppName}:             {Settings.CurrentVersion}{Environment.NewLine}");
        sb.AppendFormat($"  RepoName:              {Settings.RepoName}{Environment.NewLine}");
        sb.AppendFormat($"  Search from date:      {dateStartSearch}{Environment.NewLine}");
        sb.AppendFormat($"  Search to date:        {dateEndSearch}{Environment.NewLine}");
        sb.AppendFormat($"  Output file name:      {Path.Combine(args.OutputDirectory, args.OutputFileName)}{Environment.NewLine}");
        sb.AppendFormat($"  Ignored file pattern:  {String.Join(", ", args.IgnoredFilePattern)}{Environment.NewLine}");
        sb.AppendFormat($"  File search pattern:   {String.Join(", ", args.FileSearchPattern)}{Environment.NewLine}");
        sb.AppendFormat($"  Directory Sort Order:  {String.Join(", ", args.DirectorySortOrder)}{Environment.NewLine}");
        sb.AppendFormat($"  Sub Directories:       {String.Join(", ", Settings.SubDirectories)}{Environment.NewLine}");

        sb.AppendFormat($"  File Extensions:       {String.Join(", ", Settings.FileExtensions)}{Environment.NewLine}");
        sb.AppendFormat($"  Truncate Output Files: {Settings.TruncateOutputFiles}{Environment.NewLine}");
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

            using (var sw = new StreamWriter(OutputLocation, false, utf8))
            {
                sw.Write(fNamesList.ToString());
                foreach (var vc in GitResults.ValidCommits)
                {
                    foreach (var f in vc.CommitFiles.OrderBy(e => e.FileName))
                    {
                        try
                        {
                            var blob = client.Git.Blob.Get(Settings.RepoOwnerName, Settings.RepoName, f.Sha);
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
            using (var sw = new StreamWriter(OutputLocation, false, utf8))
            {
                sw.Write(GetOutputHeader().ToString());
            }
        }
        Utils.OpenFile(OutputLocation);
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
        if (!Settings.TruncateOutputFiles)
        {
            return;
        }
        // get the file name and try to match and
        // delete only similar ones
        var fi = new FileInfo(args.OutputFileName);
        string[] filesToDelete = Directory.GetFiles(args.OutputDirectory, GetWildCardNameToSearch(fi));
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
            Since = args.SearchFromDate == DateTimeOffset.MinValue ? null : args.SearchFromDate,
            Path = Settings.RepoPath,
            Until = args.SearchToDate == DateTimeOffset.MinValue ? null : args.SearchToDate,
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
        var commits = await client.Repository.Commit.GetAll(Settings.RepoOwnerName, Settings.RepoName, x);

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
            var sngCommit = await client.Repository.Commit.Get(Settings.RepoOwnerName, Settings.RepoName, c.Sha);
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
        if (Debugger.IsAttached || Settings.LogToConsole)
        {
            Console.WriteLine(txt);
        }
    }
    private void WriteToConsole(string txt)
    {
        if (Debugger.IsAttached || Settings.LogToConsole)
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
        bool valid = false;
        if (Settings.SubDirectories.Count == 0)
        {
            return true;
        }
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
        var fnName = new FileInfo(fileName);
        if (Settings.FileExtensions.Count > 0)
        {
            return Settings.FileExtensions.Contains(fnName.Extension, StringComparer.CurrentCultureIgnoreCase);
        }
        return true;
    }
    private bool IsIgnoredFile(string fileName)
    {
        bool skip = false;
        if (args.IgnoredFilePattern.Count > 0)
        {
            var fi = new FileInfo(fileName);
            foreach (var p in args.IgnoredFilePattern)
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
        bool isMatch = false;
        if (args.FileSearchPattern.Count > 0)
        {
            var fi = new FileInfo(fileName);
            foreach (var p in args.FileSearchPattern)
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

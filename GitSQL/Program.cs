namespace GitSQL;
class Program
{
    private static DateTimeOffset _searchFromDate = Settings.SearchFromDate;
    private static DateTimeOffset _searchToDate = Settings.SearchToDate;
    private static string _outputFileNameOnly = Settings.OutputFileName;
    private static readonly List<string> _ignoredFilePattern = Settings.IgnoredFilePattern;
    private static readonly List<string> _fileSearchPattern = Settings.FileSearchPattern;
    private static string _outDir = Settings.OutputDirectory;
    private static readonly List<string> _directorySortOrder = Settings.DirectorySortOrder;

    private const string _leftPadding = "    ";
    private static string _outputFileNameWithDir = "";
    private static readonly List<Nmv> _nmvList = new List<Nmv>();
    private static readonly StringBuilder _errMsg = new StringBuilder();
    private static ArgState _argSt = ArgState.Unknown;
    private static TimeSpan _duration;

    private static readonly List<string> _validArgs = new List<string>()
        {
            "/?", "-?",   // display help
            "/e", "-e",   // display config file
            "/c", "-c",   // credentials/PAT
            "/g", "-g",   // Get current PAT
            "/rg", "-rg", // Removes the currently set PAT
            "/st", "-st", // SearchFromDate
            "/en", "-en", // SearchToDate
            "/f", "-f",   // OutputFileName
            "/i", "-i",   // IgnoredFilePattern
            "/s", "-s",   // FileSearchPattern
            "/o", "-o",   // OutputDirectory
            "/so", "-so", // DirectorySortOrder
        };

    enum ArgState
    {
        Unknown = 0,
        DisplayHelp = 1,
        Valid = 2,
        Invalid = 3,
        Edit = 4,
        Creds = 5,
        GetCreds = 6,
        RemoveCreds = 7
    }
    enum OffsetType
    {
        Unknown = 0,
        Hours = 1,
        Days = 2,
        Minutes = 3,
        ExactDateTime = 4
    }
    public static async Task<int> Main(string[] args)
    {
        ParseArgs(args);
        if (_argSt == ArgState.DisplayHelp)
        {
            DisplayHelp();
        }
        else if (_argSt == ArgState.Invalid)
        {
            Environment.ExitCode = 1;
        }
        else if (_argSt == ArgState.Creds)
        {
            SetCredentials();
        }
        else if (_argSt == ArgState.GetCreds)
        {
            GetCredentials();
        }
        else if (_argSt == ArgState.RemoveCreds)
        {
            RemoveCredentials();
        }
        else if (_argSt == ArgState.Edit)
        {
            // try to open up the config file
            var cfg = Path.Combine(Directory.GetCurrentDirectory(), Settings.AppConfigName);
            if (File.Exists(cfg))
            {
                Utils.OpenFile(cfg);
            }
            else
            {
                Console.WriteLine(String.Format("{0} was not found.", cfg));
            }
        }
        else if (_argSt == ArgState.Valid)
        {
            var passMinCriteria = ValidateMinimumRequirements();
            if (passMinCriteria)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                SettingsArgs sa = new SettingsArgs
                {
                    SearchFromDate = _searchFromDate,
                    SearchToDate = _searchToDate,
                    IgnoredFilePattern = _ignoredFilePattern,
                    DirectorySortOrder = _directorySortOrder,
                    FileSearchPattern = _fileSearchPattern,
                    OutputFileName = _outputFileNameOnly,
                    OutputDirectory = _outDir,
                    RepoPersonalAccessToken = Utils.GetRepoPersonalAccessToken(),
                    RawArgs = args.ToList()
                };
                GitResult result = new GitResult();
                try
                {
                    var g = new Git(sa);
                    await g.GetCommitsAsync();
                    result = g.GitResults;
                    _outputFileNameWithDir = g.OutputLocation;
                }
                catch (Exception exc)
                {
                    SetErrorMessage("The following error occurred while attempting to retrieve git commits", exc.Message);
                }

                sw.Stop();
                _duration = sw.Elapsed.Duration();

                PrintResults(result);
                Environment.ExitCode = 0;
                return Final(0);
            }
        }
        return Final(1);
    }
    private static int Final(int arg)
    {
        if (System.Diagnostics.Debugger.IsAttached)
        {
            Console.Read();
        }
        return arg;
    }
    private static void RemoveCredentials()
    {
        Utils.SetRepoPersonalAccessToken("");
    }
    private static void GetCredentials()
    {
        string c = Utils.GetRepoPersonalAccessToken();
        if (!string.IsNullOrWhiteSpace(c))
        {
            SetClipboardText(c);
            Utils.ConsoleColorCyan();
            Console.WriteLine("Credentials copied to clipboard.");
            Console.ResetColor();
        }
        else
        {
            Utils.ConsoleColorRed();
            Console.WriteLine("No credentials found.");
            Console.ResetColor();
        }
    }
    private static void SetClipboardText(string text)
    {
        //Thread STAThread = new Thread(
        //    delegate ()
        //    {
        //        System.Windows.Forms.Clipboard.SetText(text);
        //    });
        //STAThread.SetApartmentState(ApartmentState.STA);
        //STAThread.Start();
        //STAThread.Join();

        // TODO: fix this - need to find out how to add assembly ref to System.Windows;
        Console.WriteLine("Not working at this point...");
    }
    private static void SetCredentials()
    {
        string pwd = "";
        foreach (var nmv in _nmvList)
        {
            if (
                nmv.Name.Equals("/c", StringComparison.InvariantCultureIgnoreCase) ||
                nmv.Name.Equals("-c", StringComparison.InvariantCultureIgnoreCase))
            {
                pwd = nmv.Val;
                break;
            }
        }

        // the only possible option for credentials is a personal access token
        if (!string.IsNullOrWhiteSpace(pwd))
        {
            Utils.SetRepoPersonalAccessToken(pwd);
        }
        var errMsg = Git.ValidateClientCredentials(Utils.GetRepoPersonalAccessToken());
        if (!string.IsNullOrWhiteSpace(errMsg))
        {
            Utils.ConsoleColorRed();
            Console.WriteLine(errMsg);
            Console.ResetColor();
        }
        else
        {
            Utils.ConsoleColorCyan();
            Console.WriteLine();
            Console.WriteLine("Credentials successfully set!");
            Console.WriteLine();
            Console.ResetColor();
        }
    }
    private static void PrintResults(GitResult result)
    {
        List<ValidCommit> results = result.ValidCommits;
        if (results == null)
        {
            return;
        }
        Console.WriteLine();
        Utils.ConsoleColorCyan();
        Console.WriteLine("########################### Summary ###########################");
        Console.ResetColor();
        Console.WriteLine("{0}:                  {1}", Settings.AppName, Settings.CurrentVersion);
        Console.WriteLine("Commits Searched:           {0}", result.CountOfCommits);
        Console.WriteLine("Files found:                {0}", results.Sum(e => e.CommitFiles.Count));
        Console.WriteLine("ProductHeader:              {0}", Settings.ProductHeader);
        Console.WriteLine("RepoOwnerName:              {0}", Settings.RepoOwnerName);
        Console.WriteLine("RepoName:                   {0}", Settings.RepoName);
        Console.WriteLine("RepoPath:                   {0}", Settings.RepoPath);
        Console.WriteLine("Search from date:           {0}", _searchFromDate != DateTimeOffset.MinValue ? _searchFromDate.ToString() : Utils.NotSet);
        Console.WriteLine("Search to date:             {0}", _searchToDate != DateTimeOffset.MinValue ? _searchToDate.ToString() : Utils.NotSet);
        Console.WriteLine("Output file name:           {0}", _outputFileNameWithDir);
        Console.WriteLine("Ignored file pattern:       {0}", String.Join(", ", _ignoredFilePattern.ToArray()));
        Console.WriteLine("File search pattern:        {0}", String.Join(", ", _fileSearchPattern.ToArray()));
        Console.WriteLine("Directory Sort Order:       {0}", _directorySortOrder.Count > 0 ? String.Join(", ", _directorySortOrder.ToArray()) : Utils.NotSet);
        Console.WriteLine("Sub Directories:            {0}", Settings.SubDirectories.Count > 0 ? String.Join(", ", Settings.SubDirectories.ToArray()) : "*");
        Console.WriteLine("File Extensions:            {0}", Settings.FileExtensions.Count > 0 ? String.Join(", ", Settings.FileExtensions.ToArray()) : "*");
        Console.WriteLine("Truncate Output Files:      {0}", Settings.TruncateOutputFiles.ToString());
        Console.WriteLine("LogToConsole:               {0}", Settings.LogToConsole.ToString());
        Console.WriteLine("Total running time:         {0}", _duration);

        Console.WriteLine();

        foreach (var r in results)
        {
            Utils.ConsoleColorCyan();
            Console.WriteLine("{0}", r.Directory);
            Console.ResetColor();
            foreach (var f in r.CommitFiles.OrderBy(e => e.FileName))
            {
                Console.WriteLine("  " + f.FileName);
            }
        }
    }
    private static void DisplayHelpArg(string arg, string argDesc, string argDetail = "")
    {
        Console.Write($"{_leftPadding}[");
        Utils.ConsoleColorCyan();
        Console.Write($"{arg}");
        Console.ResetColor();
        if (!string.IsNullOrWhiteSpace(argDesc))
        {
            Console.Write($"{argDesc}");
        }
        Console.WriteLine($"] {argDetail}");
    }
    private static void DisplayCurrentCmdConfig(string arg, string argValue)
    {
        Utils.ConsoleColorCyan();
        Console.Write($"{_leftPadding} {arg}");
        Console.ResetColor();
        Console.CursorLeft = _leftPadding.Length + Math.Max(arg.Length, 5);
        Console.WriteLine(argValue);
    }
    private static void DisplayCurrentConfig(string arg, string argValue)
    {
        Utils.ConsoleColorCyan();
        Console.Write($"{_leftPadding} key:");
        Console.ResetColor();
        Console.Write($"{arg}");

        Console.CursorLeft = _leftPadding.Length + Math.Max(arg.Length, 26);
        Utils.ConsoleColorCyan();
        Console.Write("value:");
        Console.ResetColor();
        Console.WriteLine(argValue);
    }
    private static void DisplayHelp()
    {
        Utils.ConsoleColorDarkCyan();
        Console.WriteLine($"{Settings.AppName} v{Settings.CurrentVersion} - Create one file for all your sql changes.{Environment.NewLine}");
        Console.ResetColor();

        Utils.ConsoleColorDarkCyan();
        Console.WriteLine($"  Available arg list:");
        Console.ResetColor();

        DisplayHelpArg("/?", "", "displays this usage guide");
        DisplayHelpArg("/e", "", "Open the config file for viewing/editing");

        Console.WriteLine();
        Console.WriteLine($"{_leftPadding}Configuring your GitHub setup:");
        DisplayHelpArg("/c:", "<PAT>", "use this to set your GitHub PAT \"Personal Access Token\"");
        DisplayHelpArg("/g", "", "Gets your current GitHub PAT and copies to clipboard");
        DisplayHelpArg("/rg", "", "Removes your stored PAT from this environment - DOES NOT REMOVE FROM GITHUB");

        Console.WriteLine();
        Console.WriteLine($"{_leftPadding}Search by Date Range options:");
        DisplayHelpArg("/st:", "<search from date>", "DateTimeOffset to start search Commit Date from");
        DisplayHelpArg("/en:", "<search to date>", "DateTimeOffset to start search Commit Date to");

        Console.WriteLine();
        Console.WriteLine($"{_leftPadding}Configuring your search results:");
        DisplayHelpArg("/f:", "<filename>", "Filename for the output of results");
        DisplayHelpArg("/i:", "<Ignore Pattern>", "list of file patterns to ignore when processing files names");
        DisplayHelpArg("/s:", "<Search Pattern>", "Files to include based on a search pattern");
        DisplayHelpArg("/o:", "<Output Directory>", "Directory to where you want the file saved");
        DisplayHelpArg("/so:", "<Sort Order>", "Comma separated list for order of appearance in saved file");

        string dateStartSearch = _searchFromDate != DateTimeOffset.MinValue ? _searchFromDate.ToString() : Utils.NotSet;
        string dateEndSearch = _searchToDate != DateTimeOffset.MinValue ? _searchToDate.ToString() : Utils.NotSet;

        Console.WriteLine();
        Utils.ConsoleColorDarkCyan();
        Console.WriteLine($"  Your current default config settings that can be set via cmd args are:");
        Console.ResetColor();

        DisplayCurrentCmdConfig("/st", dateStartSearch);
        DisplayCurrentCmdConfig("/en", dateEndSearch);
        DisplayCurrentCmdConfig("/f", Settings.OutputFileName);
        DisplayCurrentCmdConfig("/i", _ignoredFilePattern.Count == 0 ? Utils.NotSet : String.Join(", ", _ignoredFilePattern));
        DisplayCurrentCmdConfig("/s", String.Join(", ", _fileSearchPattern));
        DisplayCurrentCmdConfig("/o", Path.Combine(_outDir, _outputFileNameOnly));
        DisplayCurrentCmdConfig("/so", String.Join(", ", _directorySortOrder));

        Console.WriteLine();
        Utils.ConsoleColorDarkCyan();
        Console.WriteLine("  These can only be revised in the config file:");
        Console.ResetColor();

        DisplayCurrentConfig("ProductHeader", Settings.ProductHeader);
        DisplayCurrentConfig("RepoOwnerName", Settings.RepoOwnerName);
        DisplayCurrentConfig("RepoName", Settings.RepoName);
        DisplayCurrentConfig("RepoPath", Settings.RepoPath);
        DisplayCurrentConfig("TruncateOutputFiles", Settings.TruncateOutputFiles.ToString());
        DisplayCurrentConfig("SubDirectories", String.Join(", ", Settings.SubDirectories));
        DisplayCurrentConfig("FileExtensions", String.Join(", ", Settings.FileExtensions));
        DisplayCurrentConfig("LogToConsole", Settings.LogToConsole.ToString());

        Console.WriteLine();
        Utils.ConsoleColorDarkCyan();
        Console.WriteLine("  Example arg usage is as follows:");
        Console.ResetColor();

        Utils.ConsoleColorCyan();
        Console.WriteLine($"{_leftPadding}Sample dates shown work for both start and end dates:");
        Console.ResetColor();
        Console.WriteLine($"{_leftPadding}/st:\"{DateTime.Now.AddDays(-14).ToString("MM/dd/yy H:mm:ss zzz")}\"");
        Console.WriteLine($"{_leftPadding}/st:\"{DateTime.Now.AddDays(-14).ToString("MM/dd/yy H:mm:ss")}\"");
        Console.WriteLine($"{_leftPadding}/en:{DateTime.Now.AddDays(-2).ToString("M/d/yyyy")}");

        Console.WriteLine();
        Utils.ConsoleColorCyan();
        Console.WriteLine($"{_leftPadding}Note that the patterns for search and ignore work the same:");
        Console.ResetColor();
        Console.WriteLine($"{_leftPadding}To match PersonSelByID.prc and PersonUpdByID.sql");
        Console.WriteLine($"{_leftPadding}/s:\"Person???ByID*\"");
        Console.WriteLine();
        Console.WriteLine($"{_leftPadding}To match PersonSelByID.prc and PersonSelByName.sql");
        Console.WriteLine($"{_leftPadding}/s:\"PersonSelBy*");
        Console.WriteLine($"{_leftPadding}**Keep in mind that these can be comma separated as a single arg if ever needed");

        Console.WriteLine();

        bool validMinRequirements = ValidateMinimumRequirements();
        if (validMinRequirements)
        {
            string invalidCredsmsg = Git.ValidateExistingClientCredentials();
            if (!string.IsNullOrWhiteSpace(invalidCredsmsg))
            {
                Utils.ConsoleColorRed();
                Console.WriteLine("  Note that your current GitHub Personal Access Token throw the following error: {0}", invalidCredsmsg);
                Console.ResetColor();
            }
            else
            {
                Utils.ConsoleColorGreen();
                Console.WriteLine("  Note that your current GitHub Personal Access Token is valid!");
                Console.ResetColor();
            }
        }
    }
    private static void CollectArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string tmpArg = args[i];
            string[] tmpArr = args[i].Split(new char[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (tmpArr.Length == 0)
            {
                continue;
            }

            string val = "";
            string name = tmpArr[0];
            if (tmpArr.Length > 1)
            {
                val = tmpArr[1];
            }

            if (_validArgs.Any(e => e.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                if (_nmvList.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    _errMsg.AppendLine(String.Format("The arg {0} was passed in more than once.", name));
                    break;
                }

                _nmvList.Add(new Nmv()
                {
                    Name = name,
                    Val = val
                });
            }
            else
            {
                _errMsg.AppendLine(String.Format("An invalid arg of {0} was passed in.", name));
                break;
            }
        }
    }
    private static void ParseArgs(string[] args)
    {
        _argSt = ArgState.Unknown;
        CollectArgs(args);
        if (_errMsg.Length == 0)
        {
            foreach (var nmv in _nmvList)
            {
                string name = nmv.Name;
                string val = nmv.Val;

                if (
                    name.Equals("/?", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-?", StringComparison.OrdinalIgnoreCase))
                {
                    _argSt = ArgState.DisplayHelp;
                    break;
                }
                else if (
                    name.Equals("/e", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-e", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    // edit the config file
                    _argSt = ArgState.Edit;
                    break;
                }
                else if (
                    name.Equals("/r", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-r", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    _ignoredFilePattern.Clear();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        string[] tmpPre = val.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var i in tmpPre)
                        {
                            _ignoredFilePattern.Add(i.Trim());
                        }
                    }
                }
                else if (
                    name.Equals("/c", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-c", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    _argSt = ArgState.Creds;
                    break;
                }
                else if (
                    name.Equals("/g", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-g", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    _argSt = ArgState.GetCreds;
                    break;
                }
                else if (
                    name.Equals("/rg", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-rg", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    _argSt = ArgState.RemoveCreds;
                    break;
                }
                else if (
                    name.Equals("/s", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-s", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    string[] tmpPat = val.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    _fileSearchPattern.Clear();
                    foreach (var i in tmpPat)
                    {
                        _fileSearchPattern.Add(i.Trim());
                    }
                }
                else if (
                   name.Equals("/f", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("-f", StringComparison.OrdinalIgnoreCase)
                   )
                {
                    if (!ValidFileName(val))
                    {
                        _errMsg.AppendLine(String.Format("The output filename is invalid {0}.", val));
                    }
                    else
                    {
                        _outputFileNameOnly = val;
                    }
                }
                else if (
                   name.Equals("/o", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("-o", StringComparison.OrdinalIgnoreCase)
                   )
                {
                    if (!Directory.Exists(val))
                    {
                        string? drive = Path.GetPathRoot(val);
                        if (!Directory.Exists(drive))
                        {
                            _errMsg.AppendLine($"Drive letter {drive} specified doesn't exist.");
                        }
                        else
                        {
                            var di = Directory.CreateDirectory(val);
                            _outDir = di.FullName;
                        }
                    }
                    else
                    {
                        _outDir = new DirectoryInfo(val).FullName;
                    }
                }
                else if (
                   name.Equals("/so", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("-so", StringComparison.OrdinalIgnoreCase)
                   )
                {
                    string[] tmpDirs = val.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    _directorySortOrder.Clear();
                    foreach (var i in tmpDirs)
                    {
                        _directorySortOrder.Add(i.Trim());
                    }
                    // validation happens after due to a user may pass in the source directory as an arg
                }
                else if (
                    name.Equals("/st", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-st", StringComparison.OrdinalIgnoreCase))
                {
                    _searchFromDate = Utils.GetConfigDateTimeOffset(val);
                    if (_searchFromDate == DateTimeOffset.MinValue)
                    {
                        _errMsg.AppendLine(String.Format("Unable to determine the time value of {0} for arg {1}.", val, name));
                    }
                }
                else if (
                    name.Equals("/en", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-en", StringComparison.OrdinalIgnoreCase))
                {
                    _searchToDate = Utils.GetConfigDateTimeOffset(val);
                    if (_searchToDate == DateTimeOffset.MinValue)
                    {
                        _errMsg.AppendLine(String.Format("Unable to determine the time value of {0} for arg {1}.", val, name));
                    }
                }
            }
        }

        if (_errMsg.Length > 0)
        {
            _argSt = ArgState.Invalid;
        }

        if (_argSt == ArgState.Invalid)
        {
            if (_errMsg.Length > 0)
            {
                SetErrorMessage("The following error occurred during arg validation:", _errMsg.ToString());
            }
            else
            {
                _argSt = ArgState.Valid;
            }
        }
        else if (_argSt == ArgState.Unknown && _errMsg.Length == 0)
        {
            _argSt = ArgState.Valid;
        }
        else if (_argSt == ArgState.Unknown && _nmvList.Count > 0)
        {
            _argSt = ArgState.Valid;
        }
    }
    private static void SetErrorMessage(string title, string errMsg)
    {
        Utils.ConsoleColorRed();
        Console.WriteLine($"{title}");
        Console.WriteLine($"{_leftPadding}{errMsg}");
        Console.ResetColor();
    }
    private static bool ValidateMinimumRequirements()
    {
        string msg = "";

        if (string.IsNullOrWhiteSpace(Utils.GetRepoPersonalAccessToken()))
        {
            msg += $"{_leftPadding}Missing required GitHub Personal Access Token. Use /c:<PAT> to set the value before proceeding.{Environment.NewLine}";
        }
        if (string.IsNullOrWhiteSpace(Settings.ProductHeader))
        {
            msg += $"{_leftPadding}Missing required configuration field: ProductHeader{Environment.NewLine}";
        }
        if (string.IsNullOrWhiteSpace(Settings.RepoOwnerName))
        {
            msg += $"{_leftPadding}Missing required configuration field: RepoOwnerName{Environment.NewLine}";
        }
        if (string.IsNullOrWhiteSpace(Settings.RepoName))
        {
            msg += $"{_leftPadding}Missing required configuration field: RepoName{Environment.NewLine}";
        }
        if (msg.Length > 0)
        {
            Utils.ConsoleColorRed();
            Console.WriteLine($"The minimum criteria to run {Settings.AppName} have not been met.");
            Console.WriteLine(msg);
            Console.ResetColor();
        }
        return msg.Length == 0;
    }
    private static bool ValidFileName(string filename)
    {
        var pattern = Path.GetInvalidFileNameChars();
        return !filename.Any(pattern.Contains);
    }
    private class Nmv
    {
        public string Name { get; set; } = "";
        public string Val { get; set; } = "";
    }
}

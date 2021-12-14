namespace GitSQL;
class Program
{
    private const string _leftPadding = "    ";
    private static readonly List<Nmv> _nmvList = new();
    private static readonly StringBuilder _errMsg = new();
    private static ArgState _argSt = ArgState.Unknown;
    private static TimeSpan _duration;
    private static readonly AppSettings Config = new();
    private static string EnvKey_PersonalAccessToken = "";
    private static readonly List<string> _validArgs = new()
    {
        "?",// display help
        "c",// credentials/PAT
        "e",// edit config file
        "en",// SearchToDate
        "f",// OutputFileName
        "fe",// FileExtensions
        "g",// Get current PAT
        "i",// IgnoredFilePattern
        "o",// OutputDirectory
        "r",// Removes the currently set PAT
        "s",// FileSearchPattern
        "sd",// SubDirectories
        "so",// DirectorySortOrder
        "st",// SearchFromDate
        "v",// display config file
    };
    public static async Task<int> Main(string[] args)
    {
        try
        {
            ConfigurationBinder.Bind(new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appsettings.json", false, true)
               .Build()
               .GetSection("App"), Config);
        }
        catch (Exception exc)
        {
            SetErrorMessage("Json Error", exc.ToString());
            return Final(1);
        }

        EnvKey_PersonalAccessToken = $"{Utils.AppName}_{Config.GitHub.RepoName}_PAT";

        ParseArgs(args);

        if (_argSt == ArgState.Valid)
        {
            ValidateArgs();
        }

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
            var cfg = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (File.Exists(cfg))
            {
                Utils.OpenFile(cfg);
            }
            else
            {
                Console.WriteLine(String.Format("{0} was not found.", cfg));
            }
        }
        else if (_argSt == ArgState.View)
        {
            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            var js = JsonSerializer.Serialize(Config, options);
            Console.WriteLine(js);
        }
        else if (_argSt == ArgState.Valid)
        {
            var passMinCriteria = ValidateMinimumRequirements();
            if (passMinCriteria)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                SettingsArgs sa = new()
                {
                    SearchFromDate = Config.Args.SearchFromDate,
                    SearchToDate = Config.Args.SearchToDate,
                    IgnoredFilePattern = Config.Args.IgnoredFilePattern,
                    DirectorySortOrder = Config.Args.DirectorySortOrder,
                    FileSearchPattern = Config.Args.FileSearchPattern,
                    OutputFileName = Config.Args.OutputFileName,
                    OutputDirectory = Config.Args.OutputDirectory,
                    RepoPersonalAccessToken = Utils.GetRepoPersonalAccessToken(EnvKey_PersonalAccessToken),
                    FileExtensions = Config.Args.FileExtensions,
                    RawArgs = args.ToList()
                };
                GitResult result = new();
                try
                {
                    var g = new Git(sa, Config.GitHub, Config.Output);
                    await g.GetCommitsAsync();
                    result = g.GitResults;
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
        Utils.SetRepoPersonalAccessToken(EnvKey_PersonalAccessToken, "");
    }
    private static void GetCredentials()
    {
        string c = Utils.GetRepoPersonalAccessToken(EnvKey_PersonalAccessToken);
        if (!string.IsNullOrWhiteSpace(c))
        {
            Console.WriteLine(c);
        }
        else
        {
            Utils.ConsoleColorRed();
            Console.WriteLine("No credentials found.");
            Console.ResetColor();
        }
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
            Utils.SetRepoPersonalAccessToken(EnvKey_PersonalAccessToken, pwd);
        }
        var errMsg = Git.ValidateClientCredentials(Utils.GetRepoPersonalAccessToken(EnvKey_PersonalAccessToken), Config.GitHub.ProductHeader);
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
        Console.WriteLine("{0}:                  {1}", Utils.AppName, Utils.CurrentVersion);
        Console.WriteLine("Commits Searched:           {0}", result.CountOfCommits);
        Console.WriteLine("Files found:                {0}", results.Sum(e => e.CommitFiles.Count));
        Console.WriteLine("ProductHeader:              {0}", Config.GitHub.ProductHeader);
        Console.WriteLine("RepoOwnerName:              {0}", Config.GitHub.RepoOwnerName);
        Console.WriteLine("RepoName:                   {0}", Config.GitHub.RepoName);
        Console.WriteLine("RepoPath:                   {0}", Config.GitHub.RepoPath);
        Console.WriteLine("Search from date:           {0}", Config.Args.SearchFromDate != DateTimeOffset.MinValue ? Config.Args.SearchFromDate.ToString() : Utils.NotSet);
        Console.WriteLine("Search to date:             {0}", Config.Args.SearchToDate != DateTimeOffset.MinValue ? Config.Args.SearchToDate.ToString() : Utils.NotSet);
        Console.WriteLine("Output file name:           {0}", Path.Combine(Config.Args.OutputDirectory, Config.Args.OutputFileName));
        Console.WriteLine("Ignored file pattern:       {0}", String.Join(", ", Config.Args.IgnoredFilePattern));
        Console.WriteLine("File search pattern:        {0}", String.Join(", ", Config.Args.FileSearchPattern));
        Console.WriteLine("Directory Sort Order:       {0}", Config.Args.DirectorySortOrder.Count > 0 ? String.Join(", ", Config.Args.DirectorySortOrder) : Utils.NotSet);
        Console.WriteLine("Sub Directories:            {0}", Config.Args.SubDirectories.Count > 0 ? String.Join(", ", Config.Args.SubDirectories) : "*");
        Console.WriteLine("File Extensions:            {0}", Config.Args.FileExtensions.Count > 0 ? String.Join(", ", Config.Args.FileExtensions) : "*");
        Console.WriteLine("Truncate Output Files:      {0}", Config.Output.TruncateOutputFiles.ToString());
        Console.WriteLine("LogToConsole:               {0}", Config.Output.LogToConsole.ToString());
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
        Console.WriteLine($"{Utils.AppName} v{Utils.CurrentVersion} - Create one file for all your sql changes.{Environment.NewLine}");
        Console.ResetColor();

        Utils.ConsoleColorDarkCyan();
        Console.WriteLine($"  Available arg list:");
        Console.ResetColor();

        DisplayHelpArg("/?", "", "displays this usage guide");
        DisplayHelpArg("/e", "", "Open the config file for editing");
        DisplayHelpArg("/v", "", "View the config file");

        Console.WriteLine();
        Console.WriteLine($"{_leftPadding}Configuring your GitHub setup:");
        DisplayHelpArg("/c:", "<PAT>", "use this to set your GitHub PAT \"Personal Access Token\"");
        DisplayHelpArg("/g", "", "Gets your current GitHub PAT and prints to the console - BEWARE SHOULDER SURFERS!");
        DisplayHelpArg("/r", "", "Removes your stored PAT from this environment - DOES NOT REMOVE FROM GITHUB");

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

        string dateStartSearch = Config.Args.SearchFromDate != DateTimeOffset.MinValue ? Config.Args.SearchFromDate.ToString() : Utils.NotSet;
        string dateEndSearch = Config.Args.SearchToDate != DateTimeOffset.MinValue ? Config.Args.SearchToDate.ToString() : Utils.NotSet;

        Console.WriteLine();
        Utils.ConsoleColorDarkCyan();
        Console.WriteLine($"  Your current default config settings that can be overwritten via cmd args are:");
        Console.ResetColor();

        DisplayCurrentCmdConfig("/st", dateStartSearch);
        DisplayCurrentCmdConfig("/en", dateEndSearch);
        DisplayCurrentCmdConfig("/f", Config.Args.OutputFileName);
        DisplayCurrentCmdConfig("/i", Config.Args.IgnoredFilePattern.Count == 0 ? Utils.NotSet : String.Join(", ", Config.Args.IgnoredFilePattern));
        DisplayCurrentCmdConfig("/s", String.Join(", ", Config.Args.FileSearchPattern));
        DisplayCurrentCmdConfig("/o", Path.Combine(Config.Args.OutputDirectory, Config.Args.OutputFileName));
        DisplayCurrentCmdConfig("/so", Config.Args.DirectorySortOrder.Count == 0 ? Utils.NotSet : String.Join(", ", Config.Args.DirectorySortOrder));
        DisplayCurrentCmdConfig("/fe", Config.Args.FileExtensions.Count == 0 ? Utils.NotSet : String.Join(", ", Config.Args.FileExtensions));
        DisplayCurrentCmdConfig("/sd", Config.Args.SubDirectories.Count == 0 ? Utils.NotSet : String.Join(", ", Config.Args.SubDirectories));

        Console.WriteLine();
        Utils.ConsoleColorDarkCyan();
        Console.WriteLine("  These can only be revised in the config file:");
        Console.ResetColor();

        DisplayCurrentConfig("ProductHeader", Config.GitHub.ProductHeader);
        DisplayCurrentConfig("RepoOwnerName", Config.GitHub.RepoOwnerName);
        DisplayCurrentConfig("RepoName", Config.GitHub.RepoName);
        DisplayCurrentConfig("RepoPath", Config.GitHub.RepoPath);
        DisplayCurrentConfig("TruncateOutputFiles", Config.Output.TruncateOutputFiles.ToString());
        DisplayCurrentConfig("LogToConsole", Config.Output.LogToConsole.ToString());

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
            string invalidCredsmsg = Git.ValidateClientCredentials(Utils.GetRepoPersonalAccessToken(EnvKey_PersonalAccessToken), Config.GitHub.ProductHeader);
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

            if (_validArgs.Any(e =>
                ("/" + e).Equals(name, StringComparison.OrdinalIgnoreCase) ||
                ("-" + e).Equals(name, StringComparison.OrdinalIgnoreCase)))
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
    private static void ValidateArgs()
    {
        _errMsg.Clear();
        try
        {
            Config.Args.OutputDirectory = Utils.ValidateAndCreateOutputDirectory(Config.Args.OutputDirectory);
            if (string.IsNullOrWhiteSpace(Config.Args.OutputFileName))
            {
                Config.Args.OutputFileName = $"{Utils.AppName}_{Config.GitHub.RepoName}.txt";
            }
            if (!ValidFileName(Config.Args.OutputFileName))
            {
                _errMsg.AppendLine($"The output filename is invalid {Config.Args.OutputFileName}.");
                _argSt = ArgState.Invalid;
            }
        }
        catch (Exception exc)
        {
            _argSt = ArgState.Invalid;
            _errMsg.AppendLine(exc.Message);
        }

        if (_argSt == ArgState.Invalid && _errMsg.Length > 0)
        {
            SetErrorMessage("The following error occurred during arg validation:", _errMsg.ToString());
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
                    _argSt = ArgState.Edit;
                    break;
                }
                else if (
                    name.Equals("/v", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-v", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    _argSt = ArgState.View;
                    break;
                }
                else if (
                    name.Equals("/i", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-i", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    Config.Args.IgnoredFilePattern = Utils.ConvertStringToList(val);
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
                    name.Equals("/r", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-r", StringComparison.OrdinalIgnoreCase)
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
                    Config.Args.FileSearchPattern = Utils.ConvertStringToList(val);
                }
                else if (
                   name.Equals("/f", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("-f", StringComparison.OrdinalIgnoreCase)
                   )
                {
                    Config.Args.OutputFileName = val;
                }
                else if (
                   name.Equals("/o", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("-o", StringComparison.OrdinalIgnoreCase)
                   )
                {
                    Config.Args.OutputDirectory = val;
                }
                else if (
                   name.Equals("/so", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("-so", StringComparison.OrdinalIgnoreCase)
                   )
                {
                    Config.Args.DirectorySortOrder = Utils.ConvertStringToList(val);
                }
                else if (
                    name.Equals("/sd", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-sd", StringComparison.OrdinalIgnoreCase))
                {
                    Config.Args.SubDirectories = Utils.ConvertStringToList(val);
                }
                else if (
                    name.Equals("/fe", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-fe", StringComparison.OrdinalIgnoreCase))
                {
                    Config.Args.FileExtensions.Clear();
                    Config.Args.FileExtensions = Utils.ConvertStringToList(val);
                    Utils.ValidateFileExtensions(Config.Args.FileExtensions);
                }
                else if (
                    name.Equals("/st", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-st", StringComparison.OrdinalIgnoreCase))
                {
                    Config.Args.SearchFromDate = Utils.GetConfigDateTimeOffset(val);
                    if (Config.Args.SearchFromDate == DateTimeOffset.MinValue)
                    {
                        _errMsg.AppendLine(String.Format("Unable to determine the time value of {0} for arg {1}.", val, name));
                    }
                }
                else if (
                    name.Equals("/en", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("-en", StringComparison.OrdinalIgnoreCase))
                {
                    Config.Args.SearchToDate = Utils.GetConfigDateTimeOffset(val);
                    if (Config.Args.SearchToDate == DateTimeOffset.MinValue)
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

        if (string.IsNullOrWhiteSpace(Utils.GetRepoPersonalAccessToken(EnvKey_PersonalAccessToken)))
        {
            msg += $"{_leftPadding}Missing required GitHub Personal Access Token. Use /c:<PAT> to set the value before proceeding.{Environment.NewLine}";
        }
        if (string.IsNullOrWhiteSpace(Config.GitHub.ProductHeader))
        {
            msg += $"{_leftPadding}Missing required configuration field: ProductHeader{Environment.NewLine}";
        }
        if (string.IsNullOrWhiteSpace(Config.GitHub.RepoOwnerName))
        {
            msg += $"{_leftPadding}Missing required configuration field: RepoOwnerName{Environment.NewLine}";
        }
        if (string.IsNullOrWhiteSpace(Config.GitHub.RepoName))
        {
            msg += $"{_leftPadding}Missing required configuration field: RepoName{Environment.NewLine}";
        }
        if (msg.Length > 0)
        {
            Utils.ConsoleColorRed();
            Console.WriteLine($"The minimum criteria to run {Utils.AppName} have not been met.");
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

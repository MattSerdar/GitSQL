namespace GitSQL;
public static class Settings
{
    static Settings()
    {
        ProductHeader = GetConfigString("ProductHeader");
        RepoOwnerName = GetConfigString("RepoOwnerName");
        RepoName = GetConfigString("RepoName");
        RepoPath = GetConfigString("RepoPath");

        SubDirectories = GetConfigStringList("SubDirectories");
        DirectorySortOrder = GetConfigStringList("DirectorySortOrder");
        IgnoredFilePattern = GetConfigStringList("IgnoredFilePattern");
        LogToConsole = GetConfigBool("LogToConsole", false);
        SearchFromDate = GetConfigDate("SearchFromDate");
        SearchToDate = GetConfigDate("SearchToDate");

        FileSearchPattern = GetConfigStringList("FileSearchPattern");
        if (FileSearchPattern.Count == 0)
        {
            FileSearchPattern.Add("*");
        }

        FileExtensions = GetConfigStringList("FileExtensions");
        if (FileExtensions.Count == 0)
        {
            FileExtensions.Add("*");
        }
        else
        {
            ValidateFileExtensions();
        }

        TruncateOutputFiles = GetConfigBool("TruncateOutputFiles");
        OutputFileName = ConfigurationManager.AppSettings["OutputFileName"] ?? "mergedSql.txt";
        OutputDirectory = ConfigurationManager.AppSettings["OutputDirectory"] ?? "";

        AppName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        AppConfigName = AppName + ".dll.config";
        CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        bool valid = false;
        if (String.IsNullOrWhiteSpace(OutputDirectory))
        {
            valid = false;
            OutputDirectory = System.IO.Directory.GetCurrentDirectory();
        }
        else if (OutputDirectory.Equals("d", StringComparison.OrdinalIgnoreCase))
        {
            OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
        else if (OutputDirectory.Equals("md", StringComparison.OrdinalIgnoreCase))
        {
            OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        // just to validate the user specified path
        if (!System.IO.Directory.Exists(OutputDirectory))
        {
            try
            {
                System.IO.Directory.CreateDirectory(OutputDirectory);
                valid = true;
            }
            catch { }
            // fall back to the current directory
            if (!valid)
            {
                OutputDirectory = System.IO.Directory.GetCurrentDirectory();
            }
        }
        // suffix the output with the app name to keep the directory organized
        if (!string.IsNullOrWhiteSpace(AppName) && !OutputDirectory.EndsWith("\\" + AppName, StringComparison.InvariantCultureIgnoreCase))
        {
            OutputDirectory = System.IO.Path.Combine(OutputDirectory, AppName);
        }

        if (!System.IO.Directory.Exists(OutputDirectory))
        {
            System.IO.Directory.CreateDirectory(OutputDirectory);
        }
    }
    public static string? AppName { get; set; }
    public static string AppConfigName { get; set; }
    public static Version? CurrentVersion { get; set; }
    public static DateTimeOffset SearchFromDate { get; set; }
    public static DateTimeOffset SearchToDate { get; set; }
    public static bool LogToConsole { get; set; }
    public static bool TruncateOutputFiles { get; set; }
    public static List<string> IgnoredFilePattern { get; set; }
    public static List<string> DirectorySortOrder { get; set; }
    public static List<string> SubDirectories { get; set; }
    public static List<string> FileExtensions { get; set; }
    public static List<string> FileSearchPattern { get; set; }
    public static string OutputFileName { get; set; }
    public static string OutputDirectory { get; set; }
    public static string RepoName { get; private set; }
    public static string RepoPath { get; private set; }
    public static string ProductHeader { get; private set; }
    public static string RepoOwnerName { get; private set; }
    private static void ValidateFileExtensions()
    {
        for (var i = 0; i < FileExtensions.Count; i++)
        {
            var e = FileExtensions[i];
            if (!e.StartsWith("."))
            {
                FileExtensions[i] = "." + e;
            }
        }
    }
    private static string GetConfigString(string key)
    {
        return ConfigurationManager.AppSettings[key] ?? "";
    }
    private static List<string> GetConfigStringList(string key)
    {
        var l = new List<string>();
        var k = ConfigurationManager.AppSettings[key] ?? "";
        if (!String.IsNullOrWhiteSpace(k))
        {
            var items = k.Split(new string[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var i in items)
            {
                l.Add(i.Trim());
            }
        }
        return l;
    }
    private static bool GetConfigBool(string key, bool defaultValue = true)
    {
        bool b = defaultValue;
        var k = ConfigurationManager.AppSettings[key] ?? "";
        if (!String.IsNullOrWhiteSpace(k))
        {
            bool.TryParse(k, out b);
        }
        return b;
    }
    private static DateTimeOffset GetConfigDate(string key)
    {
        var k = ConfigurationManager.AppSettings[key] ?? "";
        return Utils.GetConfigDateTimeOffset(k);
    }
}

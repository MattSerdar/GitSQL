namespace GitSQL;
public static class Utils
{
    private static readonly string EncryptionKey = "248D1292-92C3-4300-BAF6-26163FE3015D";
    public static readonly string NotSet = "<not set>";
    public static string? AppName { get; set; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
    public static Version? CurrentVersion { get; set; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    
    public static DateTimeOffset GetConfigDateTimeOffset(string dtm)
    {
        DateTimeOffset resultDtm = DateTimeOffset.MinValue;
        if (DateTimeOffset.TryParse(dtm, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out DateTimeOffset tmpDt))
        {
            resultDtm = tmpDt;
        }
        return resultDtm;
    }
    public static void ValidateFileExtensions(List<string> fileExtensions)
    {
        for (var i = 0; i < fileExtensions.Count; i++)
        {
            var e = fileExtensions[i];
            if (!e.StartsWith("."))
            {
                fileExtensions[i] = "." + e;
            }
        }
    }
    public static string ValidateAndCreateOutputDirectory(string outputDir)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Directory.GetCurrentDirectory();
        }
        if (outputDir.Equals("d", StringComparison.OrdinalIgnoreCase))
        {
            outputDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
        else if (outputDir.Equals("md", StringComparison.OrdinalIgnoreCase))
        {
            outputDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        else if (!Directory.Exists(outputDir))
        {
            string? drive = Path.GetPathRoot(outputDir);
            if (!Directory.Exists(drive))
            {
                throw new Exception($"Drive letter {drive} specified doesn't exist in path {outputDir}");
            }
            else
            {
                var di = Directory.CreateDirectory(outputDir);
                outputDir = di.FullName;
            }
        }

        // suffix the output with the app name to keep the directory organized
        if (!string.IsNullOrWhiteSpace(AppName) && !outputDir.EndsWith("\\" + AppName, StringComparison.InvariantCultureIgnoreCase))
        {
            outputDir = Path.Combine(outputDir, AppName);
        }

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        return outputDir;
    }
    public static List<string> ConvertStringToList(string input)
    {
        List<string> list = new List<string>();
        if (!String.IsNullOrWhiteSpace(input))
        {
            var items = input.Split(new string[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var i in items)
            {
                list.Add(i.Trim());
            }
        }
        return list;
    }
    public static void OpenFile(string fileName)
    {
        using (Process p = new Process())
        {
            p.StartInfo = new ProcessStartInfo()
            {
                UseShellExecute = true,
                Verb = "open",
                FileName = fileName
            };
            p.Start();
        }
    }
    /// <summary>
    /// Console.ForegroundColor = ConsoleColor.DarkCyan;
    /// Console.BackgroundColor = ConsoleColor.Black;
    /// </summary>
    public static void ConsoleColorDarkCyan()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.BackgroundColor = ConsoleColor.Black;
    }
    /// <summary>
    /// Console.ForegroundColor = ConsoleColor.Cyan;
    /// Console.BackgroundColor = ConsoleColor.Black;
    /// </summary>
    public static void ConsoleColorCyan()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.BackgroundColor = ConsoleColor.Black;
    }
    /// <summary>
    /// Console.ForegroundColor = ConsoleColor.Yellow;
    /// Console.BackgroundColor = ConsoleColor.Black;
    /// </summary>
    public static void ConsoleColorDarkYellow()
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.BackgroundColor = ConsoleColor.Black;
    }
    /// <summary>
    /// Console.ForegroundColor = ConsoleColor.Yellow;
    /// Console.BackgroundColor = ConsoleColor.Black;
    /// </summary>
    public static void ConsoleColorYellow()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.Black;
    }
    /// <summary>
    /// Console.ForegroundColor = ConsoleColor.Red;
    /// Console.BackgroundColor = ConsoleColor.Black;
    /// </summary>
    public static void ConsoleColorRed()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.BackgroundColor = ConsoleColor.Black;
    }
    /// <summary>
    /// Console.ForegroundColor = ConsoleColor.Green;
    /// Console.BackgroundColor = ConsoleColor.Black;
    /// </summary>
    public static void ConsoleColorGreen()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.BackgroundColor = ConsoleColor.Black;
    }
    public static string GetRepoPersonalAccessToken(string environmentVariableName)
    {
        var tmp = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(tmp))
        {
            tmp = Decrypt(tmp);
        }
        else
        {
            tmp = "";
        }
        return tmp;
    }
    public static void SetRepoPersonalAccessToken(string environmentVariableName, string pat)
    {
        Environment.SetEnvironmentVariable(environmentVariableName, Encrypt(pat), EnvironmentVariableTarget.User);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="clearText"></param>
    /// ref https://www.aspsnippets.com/Articles/Encrypt-and-Decrypt-Username-or-Password-stored-in-database-in-Windows-Application-using-C-and-VBNet.aspx
    /// <returns></returns>
    public static string Encrypt(string clearText)
    {
        byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
        using (Aes encryptor = Aes.Create())
        {
            using (Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }))
            {
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }

        }
        return clearText;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cipherText"></param>
    /// ref https://www.aspsnippets.com/Articles/Encrypt-and-Decrypt-Username-or-Password-stored-in-database-in-Windows-Application-using-C-and-VBNet.aspx
    /// <returns></returns>
    public static string Decrypt(string cipherText)
    {
        byte[] cipherBytes = Convert.FromBase64String(cipherText);
        using (Aes encryptor = Aes.Create())
        {
            using (Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }))
            {
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
        }
        return cipherText;
    }
}

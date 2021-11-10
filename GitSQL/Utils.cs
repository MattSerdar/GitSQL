namespace GitSQL;
public static class Utils
{
    private static readonly string EncryptionKey = "248D1292-92C3-4300-BAF6-26163FE3015D";
    private static readonly string EnvKey_PersonalAccessToken;
    public static readonly string NotSet = "<not set>";

    static Utils()
    {
        EnvKey_PersonalAccessToken = Settings.AppName + "_" + Settings.RepoName + "_PAT";
    }

    public static DateTimeOffset GetConfigDateTimeOffset(string dtm)
    {
        DateTimeOffset resultDtm = DateTimeOffset.MinValue;
        if (DateTimeOffset.TryParse(dtm, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out DateTimeOffset tmpDt))
        {
            resultDtm = tmpDt;
        }
        return resultDtm;
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
    public static string GetRepoPersonalAccessToken()
    {
        var tmp = Environment.GetEnvironmentVariable(EnvKey_PersonalAccessToken, EnvironmentVariableTarget.User);
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
    public static void SetRepoPersonalAccessToken(string pat)
    {
        Environment.SetEnvironmentVariable(EnvKey_PersonalAccessToken, Encrypt(pat), EnvironmentVariableTarget.User);
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

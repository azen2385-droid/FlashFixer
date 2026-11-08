using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class Loc
{
    static Dictionary<string, string> t = new Dictionary<string, string>();
    static Dictionary<string, string> fb = new Dictionary<string, string>();
    public static bool Rtl;

    public static void Load(string code)
    {
        fb = Read("en");
        code = Normalize(code);
        t = code == "en" ? fb : Read(code);
        if (t.Count == 0) t = fb;
        Rtl = code == "ar" || code == "fa" || code == "he";
    }

    static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "en";
        code = code.Trim().ToLowerInvariant().Replace('_', '-');
        if (code == "zh" || code.StartsWith("zh-")) return "zh-Hans";
        int dash = code.IndexOf('-');
        if (dash > 0) code = code.Substring(0, dash);
        return code;
    }

    static Dictionary<string, string> Read(string code)
    {
        var d = new Dictionary<string, string>();
        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang", code + ".lang");
        if (!File.Exists(p)) return d;
        foreach (var line in File.ReadAllLines(p, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
            int i = line.IndexOf('=');
            if (i < 1) continue;
            d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim().Replace("\\n", "\n");
        }
        return d;
    }

    public static string T(string key, params object[] args)
    {
        string s;
        if (!t.TryGetValue(key, out s) && !fb.TryGetValue(key, out s)) s = "[" + key + "]";
        return args.Length == 0 ? s : string.Format(s, args);
    }
}

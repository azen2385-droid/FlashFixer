using System;
using System.IO;
using System.Text;

public static class Log
{
    public static void Write(string path, string key, params object[] args)
    {
        try
        {
            var sb = new StringBuilder(DateTime.UtcNow.ToString("o")).Append('\t').Append(key);
            foreach (var a in args)
            {
                string text = a == null ? "" : a.ToString();
                if (text.Length > 500) text = text.Substring(0, 500);
                sb.Append('\t').Append(text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " "));
            }
            File.AppendAllText(path, sb.ToString() + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }
}

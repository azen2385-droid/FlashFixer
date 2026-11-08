using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

public static class Elevation
{
    public static bool IsAdmin()
    {
        using (var id = WindowsIdentity.GetCurrent())
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int RunWorker(string op, DiskCandidate d, string logPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Process.GetCurrentProcess().MainModule.FileName,
            Arguments = string.Format(
                "--worker --op {0} --disk {1} --fp {2} --log \"{3}\"",
                op, d.Number, d.Fingerprint, logPath),
            UseShellExecute = true,
            Verb = "runas"
        };
        try
        {
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return 1223;
        }
    }
}

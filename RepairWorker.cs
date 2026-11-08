using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

public static class RepairWorker
{
    const ulong ExFatAt = 32UL * 1024 * 1024 * 1024;
    const ulong GptAt = 2UL * 1024 * 1024 * 1024 * 1024;
    const ulong SizeSlop = 1024UL * 1024;

    public static int Run(string op, DiskCandidate d, string log)
    {
        if (op == "verify")
        {
            Log.Write(log, "worker.ok");
            return 0;
        }
        if (op == "format") return DoFormat(d, log);
        Log.Write(log, "worker.op_unknown", op);
        return 21;
    }

    static int DoFormat(DiskCandidate requested, string log)
    {
        var all = DiskScanner.Scan();
        var matches = all.Where(x => x.Fingerprint == requested.Fingerprint).ToList();
        if (matches.Count == 0)
        {
            Log.Write(log, "worker.fp_mismatch");
            return 12;
        }
        if (matches.Count > 1)
        {
            Log.Write(log, "worker.fp_ambiguous");
            return 14;
        }

        var d = matches[0];
        if (d.Number != requested.Number)
            Log.Write(log, "worker.disk_moved", requested.Number, d.Number);

        string key;
        if (Safety.Check(d, out key) == Verdict.Refused)
        {
            Log.Write(log, key ?? "refuse.unverified");
            return 13;
        }

        if (d.Size >= ExFatAt && IsXpOrOlder())
        {
            Log.Write(log, "worker.xp_exfat_unsupported");
            return 22;
        }

        ulong measured;
        if (!DiskScanner.TryGetLength(d.Number, out measured))
        {
            Log.Write(log, "worker.size_unreadable");
            return 16;
        }
        ulong diff = measured > d.Size ? measured - d.Size : d.Size - measured;
        if (diff > SizeSlop)
        {
            Log.Write(log, "worker.size_mismatch", d.Size, measured);
            return 15;
        }

        string fs = d.Size >= ExFatAt ? "exfat" : "fat32";
        bool gpt = d.Size >= GptAt;
        Log.Write(log, "worker.format_start", d.Number, fs);

        // Separate script: a failure here must not skip the real format,
        // and a failure of format must not be hidden by a later command.
        RunDiskpart(log, false,
            "select disk " + d.Number,
            "attributes disk clear readonly",
            "online disk",
            "exit");

        string[] script = gpt
            ? new[]
            {
                "select disk " + d.Number,
                "clean",
                "convert gpt",
                "create partition primary",
                "format fs=" + fs + " quick label=FLASH",
                "assign",
                "exit"
            }
            : new[]
            {
                "select disk " + d.Number,
                "clean",
                "create partition primary",
                "format fs=" + fs + " quick label=FLASH",
                "assign",
                "exit"
            };

        int exit = RunDiskpart(log, true, script);
        if (exit == 23) return 23;

        if (!WaitForFilesystem(d.Number))
        {
            Log.Write(log, exit == 0 ? "worker.verify_failed" : "worker.format_fail", exit);
            return exit == 0 ? 24 : 20;
        }

        Log.Write(log, "worker.format_ok");
        return 0;
    }

    static int RunDiskpart(string log, bool required, params string[] lines)
    {
        string dp = Path.Combine(Environment.SystemDirectory, "diskpart.exe");
        if (!File.Exists(dp))
        {
            Log.Write(log, "worker.no_diskpart");
            return 23;
        }

        string tmp = Path.Combine(Path.GetTempPath(), "ff-dp-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(tmp, string.Join("\r\n", lines) + "\r\n", Encoding.ASCII);
            var psi = new ProcessStartInfo
            {
                FileName = dp,
                Arguments = "/s \"" + tmp + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.Append(e.Data).Append(" | "); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.Append(e.Data).Append(" | "); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(180000))
                {
                    try { p.Kill(); } catch { }
                    p.WaitForExit();
                    Log.Write(log, "worker.format_fail", -1);
                    if (required) return 20;
                }
                p.WaitForExit();
                if (sbOut.Length > 0) Log.Write(log, "worker.dp_out", sbOut.ToString());
                if (sbErr.Length > 0) Log.Write(log, "worker.dp_err", sbErr.ToString());
                return p.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Log.Write(log, "worker.format_ex", ex.Message);
            return 23;
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    static bool WaitForFilesystem(uint diskNum)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (attempt > 0) Thread.Sleep(800);
            try
            {
                if (DiskHasFilesystem(diskNum)) return true;
            }
            catch { }
        }
        return false;
    }

    static bool DiskHasFilesystem(uint diskNum)
    {
        string diskId = null;
        using (var s = new ManagementObjectSearcher(
            "SELECT DeviceID FROM Win32_DiskDrive WHERE Index=" + diskNum))
        {
            foreach (ManagementObject o in s.Get())
            {
                diskId = o["DeviceID"] as string;
                break;
            }
        }
        if (string.IsNullOrEmpty(diskId)) return false;

        using (var s = new ManagementObjectSearcher(
            "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + Wql(diskId) + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
        {
            foreach (ManagementObject p in s.Get())
            {
                string part = p["DeviceID"] as string;
                if (string.IsNullOrEmpty(part)) continue;
                using (var s2 = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + Wql(part) + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                {
                    foreach (ManagementObject ld in s2.Get())
                    {
                        string fs = ld["FileSystem"] as string;
                        if (!string.IsNullOrEmpty(fs)) return true;
                    }
                }
            }
        }
        return false;
    }

    static string Wql(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    static bool IsXpOrOlder()
    {
        return Environment.OSVersion.Version.Major < 6;
    }
}

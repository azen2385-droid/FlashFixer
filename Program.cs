using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

static class Program
{
    static string Arg(string[] a, string n)
    {
        int i = Array.IndexOf(a, n);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    [STAThread]
    static int Main(string[] a)
    {
        string lang = Arg(a, "--lang");
        if (lang == null)
            lang = CultureInfo.CurrentUICulture.Name;
        Loc.Load(lang);

        if (a.Contains("--list"))
        {
            var all = DiskScanner.Scan();
            var disks = all.Where(d => d.BusType == DiskScanner.BusUsb).ToList();
            Console.WriteLine("Source: " + (all.Count > 0 ? all[0].Source : "none"));
            Console.WriteLine(disks.Count == 0 ? Loc.T("list.none") : Loc.T("list.header"));
            foreach (var d in disks)
            {
                string k;
                Safety.Check(d, out k);
                Console.WriteLine(Loc.T("list.row", d.Number, d.Name, d.Size / 1048576, d.Vid ?? "????", d.Pid ?? "????")
                    + " | FP:" + d.Fingerprint
                    + " | Serial:" + (string.IsNullOrEmpty(d.Serial) ? "-" : d.Serial)
                    + (k != null ? "   <- " + Loc.T(k) : ""));
            }
            Console.WriteLine("--- All disks (for debug) ---");
            foreach (var d in all)
                Console.WriteLine("PhysicalDrive" + d.Number + " BusType=" + d.BusType
                    + " Size=" + (d.Size / 1048576) + "MB Sys=" + d.IsSystem
                    + " Name=" + d.Name + " Source=" + d.Source);
            return 0;
        }

        if (a.Contains("--worker"))
            return RunWorker(a);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }

    static int RunWorker(string[] a)
    {
        string log = Arg(a, "--log"), op = Arg(a, "--op"), fp = Arg(a, "--fp"), num = Arg(a, "--disk");
        uint n;
        if (log == null || op == null || fp == null || !uint.TryParse(num, out n)) return 10;
        if (op != "format" && op != "verify")
        {
            Log.Write(log, "worker.op_unknown", op);
            return 21;
        }
        if (!Elevation.IsAdmin())
        {
            Log.Write(log, "worker.not_admin");
            return 11;
        }

        var all = DiskScanner.Scan();
        var d = all.FirstOrDefault(x => x.Number == n && x.Fingerprint == fp);
        if (d == null)
        {
            var byFp = all.Where(x => x.Fingerprint == fp).ToList();
            if (byFp.Count == 0) { Log.Write(log, "worker.fp_mismatch"); return 12; }
            if (byFp.Count > 1) { Log.Write(log, "worker.fp_ambiguous"); return 14; }
            d = byFp[0];
            Log.Write(log, "worker.disk_moved", n, d.Number);
        }

        var twins = all.Where(x => x.Number != d.Number && x.Fingerprint == fp).ToList();
        if (twins.Count > 0) { Log.Write(log, "worker.fp_ambiguous"); return 14; }

        string key;
        if (Safety.Check(d, out key) == Verdict.Refused)
        {
            Log.Write(log, key ?? "refuse.unverified");
            return 13;
        }

        return RepairWorker.Run(op, d, log);
    }
}

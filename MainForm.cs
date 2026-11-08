using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public class MainForm : Form
{
    DataGridView grid;
    Button btnRefresh, btnRepair, btnExit, btnNight, btnDay;
    Label lblStatus, lblWarning;
    FlowLayoutPanel lookPanel;
    List<Button> skinButtons = new List<Button>();
    List<DiskCandidate> disks = new List<DiskCandidate>();

    public MainForm()
    {
        Text = Loc.T("app.title");
        Width = 1000;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = Loc.Rtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = Loc.Rtl;
        Font = new Font("Segoe UI", 10f);

        lblWarning = new Label
        {
            Text = Loc.T("warning.usb_all"),
            ForeColor = Color.White,
            BackColor = Color.DarkRed,
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(12, 8, 12, 6),
            Font = new Font(Font, FontStyle.Bold),
            Tag = "banner"
        };

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(12, 8, 12, 8)
        };
        btnRefresh = new Button { Text = Loc.T("list.refresh"), Width = 120, Height = 32 };
        btnRepair = new Button
        {
            Text = Loc.T("btn.repair"),
            Width = 180,
            Height = 32,
            Enabled = false,
            BackColor = Color.DarkRed,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Tag = "danger"
        };
        btnExit = new Button { Text = Loc.T("btn.exit"), Width = 100, Height = 32 };
        btnRefresh.Click += (s, e) => RefreshDisks();
        btnRepair.Click += (s, e) => OnRepair();
        btnExit.Click += (s, e) => Close();
        topPanel.Controls.Add(btnRefresh);
        topPanel.Controls.Add(btnRepair);
        topPanel.Controls.Add(btnExit);

        lookPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8, 6, 8, 0)
        };
        foreach (Skin skin in new[] { Skin.Basic, Skin.Fire, Skin.Water, Skin.Sky, Skin.Dynamic })
        {
            var b = new Button
            {
                Text = Loc.T(Theme.SkinKey(skin)),
                Tag = skin,
                Height = 30,
                Width = 96,
                FlatStyle = FlatStyle.Flat
            };
            var picked = skin;
            b.Click += (s, e) => Theme.Set(picked, Theme.Mode);
            skinButtons.Add(b);
            lookPanel.Controls.Add(b);
        }
        btnNight = new Button { Text = Loc.T("mode.night"), Height = 30, Width = 72, FlatStyle = FlatStyle.Flat };
        btnDay = new Button { Text = Loc.T("mode.day"), Height = 30, Width = 72, FlatStyle = FlatStyle.Flat };
        btnNight.Click += (s, e) => Theme.Set(Theme.Current, UiMode.Night);
        btnDay.Click += (s, e) => Theme.Set(Theme.Current, UiMode.Day);
        lookPanel.Controls.Add(btnNight);
        lookPanel.Controls.Add(btnDay);

        var atmos = new Atmosphere();

        lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            Padding = new Padding(12, 6, 12, 4),
            Text = Loc.T("status.ready"),
            BorderStyle = BorderStyle.FixedSingle
        };

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("col.num"), FillWeight = 12 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("col.name"), FillWeight = 36 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("col.size"), FillWeight = 14 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("col.vidpid"), FillWeight = 16 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("col.status"), FillWeight = 22 });
        grid.SelectionChanged += (s, e) => UpdateRepairButton();

        Controls.Add(grid);
        Controls.Add(lblStatus);
        Controls.Add(topPanel);
        Controls.Add(lookPanel);
        Controls.Add(atmos);
        Controls.Add(lblWarning);

        Theme.Changed += ApplyChrome;
        Theme.Attach(this);
        ApplyChrome();
        FormClosed += (s, e) => { Theme.Changed -= ApplyChrome; };

        Shown += (s, e) => RefreshDisks();
    }

    void RefreshDisks()
    {
        lblStatus.Text = Loc.T("status.scanning");
        btnRefresh.Enabled = false;
        grid.Rows.Clear();
        disks.Clear();
        try
        {
            var all = DiskScanner.Scan();
            disks = all.Where(d => d.BusType == DiskScanner.BusUsb).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { btnRefresh.Enabled = true; }

        foreach (var d in disks)
        {
            string key;
            var v = Safety.Check(d, out key);
            string st = v == Verdict.Refused ? Loc.T(key ?? "refuse.unverified")
                     : v == Verdict.NeedsExtraConfirm ? Loc.T("warn.large")
                     : Loc.T("status.ready");
            int idx = grid.Rows.Add(
                "PhysicalDrive" + d.Number,
                d.Name,
                (d.Size / 1048576) + " MB",
                (d.Vid ?? "????") + ":" + (d.Pid ?? "????"),
                st);
            grid.Rows[idx].Tag = d;
            if (v == Verdict.Refused) grid.Rows[idx].DefaultCellStyle.ForeColor = Theme.Muted;
        }

        lblStatus.Text = disks.Count == 0
            ? Loc.T("list.none")
            : Loc.T("list.count", disks.Count);
        UpdateRepairButton();
    }

    void ApplyChrome()
    {
        foreach (var b in skinButtons)
        {
            bool on = b.Tag is Skin && (Skin)b.Tag == Theme.Current;
            b.FlatAppearance.BorderSize = on ? 2 : 1;
            b.FlatAppearance.BorderColor = on ? Theme.Accent : Theme.Line;
        }
        btnNight.FlatAppearance.BorderSize = Theme.Mode == UiMode.Night ? 2 : 1;
        btnDay.FlatAppearance.BorderSize = Theme.Mode == UiMode.Day ? 2 : 1;
        btnNight.FlatAppearance.BorderColor = Theme.Mode == UiMode.Night ? Theme.Accent : Theme.Line;
        btnDay.FlatAppearance.BorderColor = Theme.Mode == UiMode.Day ? Theme.Accent : Theme.Line;
        foreach (DataGridViewRow row in grid.Rows)
        {
            var d = row.Tag as DiskCandidate;
            if (d == null) continue;
            string key;
            var v = Safety.Check(d, out key);
            row.DefaultCellStyle.ForeColor = v == Verdict.Refused ? Theme.Muted : Theme.Fg;
            row.DefaultCellStyle.BackColor = Theme.Surface;
            row.DefaultCellStyle.SelectionBackColor = Theme.Surface2;
            row.DefaultCellStyle.SelectionForeColor = Theme.Fg;
        }
    }

    void UpdateRepairButton()
    {
        var d = SelectedDisk();
        string ignored;
        btnRepair.Enabled = d != null && Safety.Check(d, out ignored) != Verdict.Refused;
    }

    DiskCandidate SelectedDisk()
    {
        if (grid.SelectedRows.Count == 0) return null;
        return grid.SelectedRows[0].Tag as DiskCandidate;
    }

    void OnRepair()
    {
        var d = SelectedDisk();
        if (d == null) return;

        using (var confirm = new ConfirmForm(d))
            if (confirm.ShowDialog(this) != DialogResult.OK) return;

        lblStatus.Text = Loc.T("status.working");
        btnRepair.Enabled = false;
        try
        {
            string log = Path.Combine(Path.GetTempPath(), "flashfixer-" + Guid.NewGuid().ToString("N") + ".log");
            int code = Elevation.RunWorker("format", d, log);
            if (code == 0)
            {
                MessageBox.Show(this, Loc.T("worker.done"), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshDisks();
            }
            else if (code == 1223)
            {
                MessageBox.Show(this, Loc.T("uac.cancelled"), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, ReadReason(log, code), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (code == 12) RefreshDisks();
            }
        }
        finally
        {
            lblStatus.Text = Loc.T("status.ready");
            UpdateRepairButton();
        }
    }

    static string ReadReason(string log, int code)
    {
        try
        {
            var lines = File.ReadAllLines(log);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var parts = lines[i].Split('\t');
                if (parts.Length < 2) continue;
                string key = parts[1];
                if (key == "worker.dp_out" || key == "worker.dp_err") continue;
                if (!key.StartsWith("worker.") && !key.StartsWith("refuse.") && !key.StartsWith("warn."))
                    continue;
                object[] args = parts.Skip(2).Cast<object>().ToArray();
                return Loc.T("worker.fail") + "\n" + Loc.T(key, args);
            }
        }
        catch { }

        string fallback = FallbackKey(code);
        string detail = fallback == null ? ("Worker exit code: " + code) : Loc.T(fallback);
        return Loc.T("worker.fail") + "\n" + detail;
    }

    static string FallbackKey(int code)
    {
        switch (code)
        {
            case 11: return "worker.not_admin";
            case 12: return "worker.fp_mismatch";
            case 14: return "worker.fp_ambiguous";
            case 15: return "worker.size_mismatch";
            case 16: return "worker.size_unreadable";
            case 20: return "worker.format_fail";
            case 21: return "worker.op_unknown";
            case 22: return "worker.xp_exfat_unsupported";
            case 23: return "worker.format_ex";
            case 24: return "worker.verify_failed";
            default: return null;
        }
    }
}

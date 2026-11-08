using System;
using System.Drawing;
using System.Windows.Forms;

public class ConfirmForm : Form
{
    public ConfirmForm(DiskCandidate d)
    {
        Text = Loc.T("confirm.title");
        Width = 560;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        RightToLeft = Loc.Rtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = Loc.Rtl;
        Font = new Font("Segoe UI", 10f);

        string diskLabel = "PhysicalDrive" + d.Number;
        string key;
        var verdict = Safety.Check(d, out key);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(0),
            AutoSize = false
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblWarn = new Label
        {
            Text = Loc.T("confirm.warning"),
            ForeColor = Color.White,
            BackColor = Color.DarkRed,
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 8),
            Tag = "banner"
        };
        var lblWarn2 = new Label
        {
            Text = Loc.T("warning.usb_all"),
            ForeColor = Color.DarkRed,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 4),
            Tag = "accent"
        };
        var details = new Label
        {
            Text = Loc.T("confirm.disk", diskLabel) + "\n"
                 + Loc.T("confirm.name", d.Name) + "\n"
                 + Loc.T("confirm.size", d.Size / 1048576) + "\n"
                 + Loc.T("confirm.vidpid", (d.Vid ?? "????") + ":" + (d.Pid ?? "????")) + "\n"
                 + Loc.T("confirm.fp", d.Fingerprint),
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 4, 12, 4)
        };
        var lblLarge = new Label
        {
            Text = verdict == Verdict.NeedsExtraConfirm ? Loc.T("warn.large") : "",
            ForeColor = Color.DarkOrange,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 12, 0),
            Tag = "warn"
        };
        var chk = new CheckBox
        {
            Text = Loc.T("confirm.checkbox"),
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 4, 12, 4)
        };
        var lblType = new Label
        {
            Text = Loc.T("confirm.typetext", diskLabel),
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 0)
        };
        var txt = new TextBox { Dock = DockStyle.Fill };
        var txtHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 8) };
        txtHost.Controls.Add(txt);

        var btnCancel = new Button
        {
            Text = Loc.T("confirm.no"),
            DialogResult = DialogResult.Cancel,
            Width = 120,
            Height = 36
        };
        var btnOk = new Button
        {
            Text = Loc.T("confirm.proceed"),
            Width = 200,
            Height = 36,
            Enabled = false,
            BackColor = Color.DarkRed,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Tag = "danger"
        };
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 8)
        };
        btnPanel.Controls.Add(btnOk);
        btnPanel.Controls.Add(btnCancel);

        AddRow(root, lblWarn, 56);
        AddRow(root, lblWarn2, 72);
        AddRow(root, details, 124);
        AddRow(root, lblLarge, verdict == Verdict.NeedsExtraConfirm ? 52 : 8);
        AddRow(root, chk, 64);
        AddRow(root, lblType, 28);
        AddRow(root, txtHost, 36);
        AddRow(root, btnPanel, 56);

        Action update = () => btnOk.Enabled = chk.Checked && txt.Text.Trim() == diskLabel;
        chk.CheckedChanged += (s, e) => update();
        txt.TextChanged += (s, e) => update();
        btnOk.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
        CancelButton = btnCancel;
        AcceptButton = btnOk;

        Controls.Add(root);
        Theme.Attach(this);
    }

    static void AddRow(TableLayoutPanel root, Control c, int height)
    {
        int row = root.RowCount;
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        c.Margin = new Padding(0);
        root.Controls.Add(c, 0, row);
    }
}

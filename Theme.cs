using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

public enum Skin { Basic, Fire, Water, Sky, Dynamic }
public enum UiMode { Night, Day }

public static class Theme
{
    public static Skin Current = Skin.Basic;
    public static UiMode Mode = UiMode.Night;
    public static event Action Changed;

    public static Color Bg, Surface, Surface2, Fg, Muted, Accent, AccentInk, Warn, Line;

    static Theme()
    {
        Load();
        Recalc();
    }

    public static void Set(Skin skin, UiMode mode)
    {
        Current = skin;
        Mode = mode;
        Recalc();
        Save();
        var handler = Changed;
        if (handler != null) handler();
    }

    public static string SkinKey(Skin skin)
    {
        switch (skin)
        {
            case Skin.Fire: return "skin.fire";
            case Skin.Water: return "skin.water";
            case Skin.Sky: return "skin.sky";
            case Skin.Dynamic: return "skin.dynamic";
            default: return "skin.basic";
        }
    }

    public static void Attach(Form form)
    {
        Action paint = () =>
        {
            form.BackColor = Bg;
            form.ForeColor = Fg;
            foreach (Control child in form.Controls) PaintOne(child);
        };
        Changed += paint;
        form.FormClosed += (s, e) => { Changed -= paint; };
        paint();
    }

    static void PaintOne(Control c)
    {
        if (c is Atmosphere)
        {
            c.Invalidate();
            return;
        }

        string tag = c.Tag as string;
        if (c is DataGridView)
        {
            StyleGrid((DataGridView)c);
            return;
        }
        if (c is Button)
        {
            StyleButton((Button)c, tag == "danger");
            return;
        }
        if (c is TextBox)
        {
            c.BackColor = Surface;
            c.ForeColor = Fg;
            return;
        }
        if (tag == "banner")
        {
            c.BackColor = Accent;
            c.ForeColor = AccentInk;
            return;
        }
        if (tag == "accent")
        {
            c.BackColor = Color.Transparent;
            c.ForeColor = Accent;
            return;
        }
        if (tag == "warn")
        {
            c.BackColor = Color.Transparent;
            c.ForeColor = Warn;
            return;
        }

        if (c is Panel || c is FlowLayoutPanel || c is TableLayoutPanel || c is Label || c is CheckBox)
        {
            c.BackColor = (c is Label || c is CheckBox) ? Color.Transparent : Bg;
            c.ForeColor = Fg;
        }

        foreach (Control child in c.Controls) PaintOne(child);
    }

    public static void StyleButton(Button b, bool danger)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        if (danger)
        {
            b.BackColor = Accent;
            b.ForeColor = AccentInk;
            b.FlatAppearance.BorderColor = Accent;
        }
        else
        {
            b.BackColor = Surface2;
            b.ForeColor = Fg;
            b.FlatAppearance.BorderColor = Line;
        }
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Surface;
        grid.GridColor = Line;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Fg;
        grid.DefaultCellStyle.SelectionBackColor = Surface2;
        grid.DefaultCellStyle.SelectionForeColor = Fg;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface2;
    }

    static void Recalc()
    {
        bool night = Mode == UiMode.Night;
        switch (Current)
        {
            case Skin.Fire:
                if (night) Set("#140806", "#24110c", "#3a1a10", "#ffe8d2", "#c49a84", "#ff4d1c", "#1a0602", "#ffb020", "#5a2a18");
                else Set("#fff4ec", "#fffaf6", "#ffe4d4", "#2a120c", "#8a5344", "#d63a12", "#fff8f4", "#9a5a00", "#f0d2c2");
                break;
            case Skin.Water:
                if (night) Set("#06131a", "#0c2430", "#123646", "#e5f7fb", "#8eb4c2", "#1aa6c7", "#041016", "#e0b15a", "#1d4454");
                else Set("#f3fbfd", "#ffffff", "#e5f4f8", "#0c2833", "#4e7380", "#0c7c96", "#f4fcfe", "#8a5a10", "#d3e7ee");
                break;
            case Skin.Sky:
                if (night) Set("#070b18", "#12182c", "#1c2644", "#eef2ff", "#a3b0d4", "#7aa2ff", "#07101f", "#f0c36a", "#2a3658");
                else Set("#f5f8ff", "#ffffff", "#e7eeff", "#15203a", "#5c6b8a", "#2f62d6", "#f7f9ff", "#9a6b12", "#d9e2f5");
                break;
            case Skin.Dynamic:
                if (night) Set("#0c0e12", "#161a22", "#222833", "#f4f7fb", "#9aa3b2", "#d6ff3c", "#12160a", "#ff5d8f", "#313846");
                else Set("#f6f7f4", "#ffffff", "#eef1e6", "#161a14", "#5e665c", "#4f6d00", "#f7ffe4", "#c43b62", "#e2e6da");
                break;
            default:
                if (night) Set("#121410", "#1c1f1a", "#262a22", "#f3f0e6", "#a39e92", "#c4362c", "#fff6f4", "#e0a045", "#34382f");
                else Set("#f4f1ea", "#fffcf7", "#efeae0", "#1c1a16", "#6d675e", "#b42318", "#fff8f6", "#9a6408", "#e4dfd4");
                break;
        }
    }

    static void Set(string bg, string surface, string surface2, string fg, string muted, string accent, string ink, string warn, string line)
    {
        Bg = Hex(bg);
        Surface = Hex(surface);
        Surface2 = Hex(surface2);
        Fg = Hex(fg);
        Muted = Hex(muted);
        Accent = Hex(accent);
        AccentInk = Hex(ink);
        Warn = Hex(warn);
        Line = Hex(line);
    }

    static Color Hex(string hex)
    {
        return Color.FromArgb(
            Convert.ToInt32(hex.Substring(1, 2), 16),
            Convert.ToInt32(hex.Substring(3, 2), 16),
            Convert.ToInt32(hex.Substring(5, 2), 16));
    }

    static string FilePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlashFixer");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ui.cfg");
    }

    static void Load()
    {
        try
        {
            string p = FilePath();
            if (!File.Exists(p)) return;
            foreach (string line in File.ReadAllLines(p))
            {
                int i = line.IndexOf('=');
                if (i < 1) continue;
                string k = line.Substring(0, i).Trim();
                string v = line.Substring(i + 1).Trim();
                if (k == "skin")
                {
                    Skin parsed;
                    if (Enum.TryParse(v, true, out parsed)) Current = parsed;
                }
                else if (k == "mode")
                {
                    UiMode parsed;
                    if (Enum.TryParse(v, true, out parsed)) Mode = parsed;
                }
            }
        }
        catch { }
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(FilePath(), "skin=" + Current + Environment.NewLine + "mode=" + Mode + Environment.NewLine);
        }
        catch { }
    }
}

public class Atmosphere : Control
{
    readonly Timer timer;
    float phase;

    public Atmosphere()
    {
        Height = 78;
        Dock = DockStyle.Top;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        UpdateStyles();
        timer = new Timer { Interval = 33 };
        timer.Tick += (s, e) =>
        {
            phase += 0.035f;
            if (phase > 1000f) phase = 0f;
            Invalidate();
        };
        timer.Start();
        Theme.Changed += () => Invalidate();
        Disposed += (s, e) => timer.Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = ClientRectangle;
        if (rect.Width < 2 || rect.Height < 2) return;

        using (var brush = new LinearGradientBrush(rect, Theme.Bg, Blend(Theme.Bg, Theme.Accent, 0.45f),
            Theme.Current == Skin.Water ? 0f : 90f))
        {
            g.FillRectangle(brush, rect);
        }

        int count = Theme.Current == Skin.Basic ? 3 : 16;
        float speed = Theme.Current == Skin.Fire ? 1.4f : Theme.Current == Skin.Dynamic ? 1.1f : 0.55f;
        for (int i = 0; i < count; i++)
        {
            float t = (phase * speed * 0.08f + i * 0.17f) % 1f;
            float x;
            float y;
            if (Theme.Current == Skin.Water)
            {
                x = (t * 1.2f - 0.1f) * rect.Width;
                y = (float)(rect.Height * (0.25 + 0.5 * Math.Sin(i + phase)));
            }
            else if (Theme.Current == Skin.Sky)
            {
                x = (float)((0.5 + 0.5 * Math.Sin(i * 1.7)) * rect.Width);
                y = (float)((0.5 + 0.5 * Math.Cos(i * 1.3 + phase * 0.2)) * rect.Height);
            }
            else
            {
                x = (float)((0.5 + 0.5 * Math.Sin(i * 1.9 + phase)) * rect.Width);
                y = rect.Height * (1f - t);
            }

            int alpha = Theme.Mode == UiMode.Day ? 90 : 170;
            alpha = (int)(alpha * Math.Sin(Math.Max(0.15, t) * Math.PI));
            int radius = Theme.Current == Skin.Sky ? 2 + (i % 2) : 3 + (i % 4);
            using (var b = new SolidBrush(Color.FromArgb(Math.Max(20, Math.Min(220, alpha)), Theme.Accent)))
                g.FillEllipse(b, x, y, radius, radius);
        }

        if (Theme.Current == Skin.Dynamic)
        {
            float sweep = (phase * 40f) % (rect.Width + 40) - 20;
            using (var b = new SolidBrush(Color.FromArgb(Theme.Mode == UiMode.Day ? 50 : 90, Theme.Accent)))
                g.FillRectangle(b, sweep, 0, 18, rect.Height);
        }
    }

    static Color Blend(Color a, Color b, float t)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}

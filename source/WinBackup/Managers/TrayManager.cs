using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinBackup.Managers;

public enum TrayState
{
    Idle,
    Running,
    Paused,
    Error
}

public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu = new();
    private readonly Dictionary<TrayState, Icon> _icons = new();
    private readonly List<(ToolStripItem Item, Func<bool> Enabled)> _conditions = new();

    public event Action? OpenRequested;

    public TrayManager()
    {
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/WinBackup.ico"))!.Stream;
        using var baseIcon = new Icon(stream, SystemInformation.SmallIconSize);

        _icons[TrayState.Idle] = (Icon)baseIcon.Clone();
        _icons[TrayState.Running] = WithBadge(baseIcon, Color.LimeGreen);
        _icons[TrayState.Paused] = WithBadge(baseIcon, Color.Orange);
        _icons[TrayState.Error] = WithBadge(baseIcon, Color.Red);

        _menu.Opening += (_, _) =>
        {
            foreach (var (item, enabled) in _conditions)
                item.Enabled = enabled();
        };

        _icon = new NotifyIcon
        {
            Icon = _icons[TrayState.Idle],
            Text = "WinBackup",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void AddCommand(string text, Action action, Func<bool>? enabled = null)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        _menu.Items.Add(item);
        if (enabled != null) _conditions.Add((item, enabled));
    }

    public void AddSeparator() => _menu.Items.Add(new ToolStripSeparator());

    public void SetState(TrayState state, string text)
    {
        _icon.Icon = _icons[state];
        _icon.Text = text.Length > 120 ? text[..120] : text;
    }

    public void Notify(string title, string message, bool error) =>
        _icon.ShowBalloonTip(4000, title, message, error ? ToolTipIcon.Warning : ToolTipIcon.Info);

    private static Icon WithBadge(Icon source, Color color)
    {
        using var bitmap = source.ToBitmap();
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var size = bitmap.Width * 0.55f;
            var area = new RectangleF(bitmap.Width - size, bitmap.Height - size, size - 1, size - 1);
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, area);
            graphics.DrawEllipse(Pens.White, area);
        }
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        foreach (var icon in _icons.Values) icon.Dispose();
    }
}

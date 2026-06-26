using System.Drawing;
using System.Windows.Forms;
using SelectCast.Core;

namespace SelectCast.App;

/// <summary>
/// System-tray presence (WinForms <see cref="NotifyIcon"/>) with a context menu. Lets the app live
/// resident: closing the window hides to the tray, real exit is the "Выход" item.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private ToolStripMenuItem? _autostartItem;

    public TrayIcon(Action onOpen, Action onSettings, Action onExit)
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Открыть", null, (_, _) => onOpen());
        _menu.Items.Add("Настройки", null, (_, _) => onSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Выход", null, (_, _) => onExit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = SelectCastInfo.ProductName,
            ContextMenuStrip = _menu,
        };
        _icon.DoubleClick += (_, _) => onOpen();
    }

    /// <summary>Adds a checkable "Автозапуск" item (wired in stage 6c). Reflects and toggles state.</summary>
    public void AddAutostartToggle(Func<bool> isEnabled, Action<bool> setEnabled)
    {
        _autostartItem = new ToolStripMenuItem("Автозапуск") { CheckOnClick = true, Checked = isEnabled() };
        _autostartItem.CheckedChanged += (_, _) => setEnabled(_autostartItem.Checked);
        _menu.Items.Insert(2, _autostartItem); // after Открыть/Настройки, before the separator
    }

    private static Icon LoadIcon()
    {
        // The .ico is embedded as a WPF resource (see SelectCast.App.csproj <Resource>).
        var uri = new Uri("pack://application:,,,/Assets/selectcast.ico");
        System.Windows.Resources.StreamResourceInfo? info = System.Windows.Application.GetResourceStream(uri);
        return info is not null ? new Icon(info.Stream) : SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}

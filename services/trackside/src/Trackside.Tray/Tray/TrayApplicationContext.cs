using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trackside.Tray.Tray;

/// <summary>
/// Windows Forms application context that owns the Trackside notification-area icon.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly TracksideTrayOptions _options;
    private readonly ILogger<TrayApplicationContext> _logger;
    private readonly NotifyIcon _notifyIcon;

    /// <summary>
    /// Creates the tray icon and context menu from configuration.
    /// </summary>
    /// <param name="options">Trackside options containing tray menu definitions.</param>
    /// <param name="logger">Logger for tray actions.</param>
    public TrayApplicationContext(
        IOptions<TracksideTrayOptions> options,
        ILogger<TrayApplicationContext> logger)
    {
        _options = options.Value;
        _logger = logger;
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = BuildMenu(),
            Icon = SystemIcons.Application,
            Text = TrimTooltip(_options.Tooltip),
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRoute("/");

        if (!string.IsNullOrWhiteSpace(_options.BalloonMessage))
        {
            _notifyIcon.ShowBalloonTip(2500, _options.BalloonTitle, _options.BalloonMessage, ToolTipIcon.Info);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (var item in _options.MenuItems)
        {
            menu.Items.Add(CreateMenuItem(item));
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(CreateMenuItem(new TracksideTrayMenuItemOptions
            {
                Text = "Open Kiosk",
                Action = TracksideTrayMenuAction.OpenUrl,
                Route = "/",
            }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateMenuItem(new TracksideTrayMenuItemOptions
            {
                Text = "Exit Trackside",
                Action = TracksideTrayMenuAction.Exit,
            }));
        }

        return menu;
    }

    private ToolStripItem CreateMenuItem(TracksideTrayMenuItemOptions item)
    {
        if (item.Action == TracksideTrayMenuAction.Separator)
        {
            return new ToolStripSeparator();
        }

        var menuItem = new ToolStripMenuItem(item.Text);
        menuItem.Click += (_, _) => ExecuteMenuAction(item);
        return menuItem;
    }

    private void ExecuteMenuAction(TracksideTrayMenuItemOptions item)
    {
        switch (item.Action)
        {
            case TracksideTrayMenuAction.OpenUrl:
                OpenUrl(ResolveUrl(item));
                break;
            case TracksideTrayMenuAction.Exit:
                _logger.LogInformation("Tray companion exit requested.");
                ExitThread();
                break;
        }
    }

    private void OpenRoute(string route) => OpenUrl(CombineBaseUrl(_options.HostBaseUrl, route));

    private void OpenUrl(string url)
    {
        _logger.LogInformation("Opening tray URL {Url}", url);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private string ResolveUrl(TracksideTrayMenuItemOptions item)
    {
        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            return item.Url;
        }

        return CombineBaseUrl(_options.HostBaseUrl, item.Route ?? "/");
    }

    private static string CombineBaseUrl(string baseUrl, string route)
    {
        var normalizedRoute = route.StartsWith('/') ? route : "/" + route;
        return baseUrl.TrimEnd('/') + normalizedRoute;
    }

    private static string TrimTooltip(string value)
    {
        const int maximumNotifyIconTextLength = 63;
        return value.Length <= maximumNotifyIconTextLength ? value : value[..maximumNotifyIconTextLength];
    }
}
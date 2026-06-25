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
    private readonly ITrayStatusClient _statusClient;
    private readonly ILogger<TrayApplicationContext> _logger;
    private readonly Icon _trayIcon;
    private readonly System.Windows.Forms.Timer _statusTimer;
    private readonly NotifyIcon _notifyIcon;
    private Icon? _statusIcon;
    private TrayConnectionStatus _currentStatus = TrayConnectionStatus.Disconnected;
    private bool _statusRefreshInProgress;

    /// <summary>
    /// Creates the tray icon and context menu from configuration.
    /// </summary>
    /// <param name="options">Trackside options containing tray menu definitions.</param>
    /// <param name="statusClient">Client that reads service status for the tray icon dot.</param>
    /// <param name="logger">Logger for tray actions.</param>
    public TrayApplicationContext(
        IOptions<TracksideTrayOptions> options,
        ITrayStatusClient statusClient,
        ILogger<TrayApplicationContext> logger)
    {
        _options = options.Value;
        _statusClient = statusClient;
        _logger = logger;
        _trayIcon = LoadTrayIcon();
        _statusIcon = TrayIconRenderer.Render(_trayIcon, _currentStatus);
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = BuildMenu(),
            Icon = _statusIcon,
            Text = TrimTooltip(BuildTooltip(_currentStatus)),
            Visible = true,
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
        _notifyIcon.DoubleClick += (_, _) => OpenRoute("/");

        _statusTimer = new System.Windows.Forms.Timer
        {
            Interval = StatusRefreshIntervalMilliseconds(_options.StatusRefreshSeconds),
        };
        _statusTimer.Tick += async (_, _) => await RefreshTrayStatusAsync();
        _statusTimer.Start();
        _ = RefreshTrayStatusAsync();

        if (_options.ShowStartupBalloon && !string.IsNullOrWhiteSpace(_options.BalloonMessage))
        {
            _notifyIcon.ShowBalloonTip(2500, _options.BalloonTitle, _options.BalloonMessage, ToolTipIcon.Info);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _statusIcon?.Dispose();
            _trayIcon.Dispose();
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

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs args)
    {
        if (args.Button != MouseButtons.Left)
        {
            return;
        }

        _notifyIcon.ContextMenuStrip?.Show(Cursor.Position);
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

    private async Task RefreshTrayStatusAsync()
    {
        if (_statusRefreshInProgress)
        {
            return;
        }

        _statusRefreshInProgress = true;
        try
        {
            var status = await _statusClient.GetStatusAsync(CancellationToken.None);
            if (status != _currentStatus)
            {
                UpdateTrayStatus(status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh tray status icon.");
            UpdateTrayStatus(TrayConnectionStatus.Disconnected);
        }
        finally
        {
            _statusRefreshInProgress = false;
        }
    }

    private void UpdateTrayStatus(TrayConnectionStatus status)
    {
        _currentStatus = status;
        var previousIcon = _statusIcon;
        _statusIcon = TrayIconRenderer.Render(_trayIcon, status);
        _notifyIcon.Icon = _statusIcon;
        _notifyIcon.Text = TrimTooltip(BuildTooltip(status));
        previousIcon?.Dispose();
    }

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

    private string BuildTooltip(TrayConnectionStatus status) => $"{_options.Tooltip}: {TrayIconRenderer.TextForStatus(status)}";

    private static int StatusRefreshIntervalMilliseconds(double seconds) =>
        checked((int)Math.Round(Math.Clamp(seconds, 0.5, 60.0) * 1000.0));

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : (Icon)SystemIcons.Application.Clone();
    }
}
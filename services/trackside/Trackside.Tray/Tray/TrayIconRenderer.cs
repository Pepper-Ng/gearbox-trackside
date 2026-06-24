using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Trackside.Tray.Tray;

internal static class TrayIconRenderer
{
    public static Icon Render(Icon baseIcon, TrayConnectionStatus status)
    {
        using var bitmap = baseIcon.ToBitmap();
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var size = Math.Min(bitmap.Width, bitmap.Height);
        var diameter = Math.Max(7, size / 3);
        var margin = Math.Max(1, size / 12);
        var rectangle = new Rectangle(bitmap.Width - diameter - margin, bitmap.Height - diameter - margin, diameter, diameter);

        using var borderBrush = new SolidBrush(Color.White);
        graphics.FillEllipse(borderBrush, rectangle.InflateBy(2));

        using var statusBrush = new SolidBrush(ColorForStatus(status));
        graphics.FillEllipse(statusBrush, rectangle);

        using var borderPen = new Pen(Color.FromArgb(160, Color.Black), 1);
        graphics.DrawEllipse(borderPen, rectangle);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static string TextForStatus(TrayConnectionStatus status) => status switch
    {
        TrayConnectionStatus.ActiveSession => "Memory map connected; active session",
        TrayConnectionStatus.MemoryMapConnected => "Memory map connected",
        _ => "No memory map connection",
    };

    private static Color ColorForStatus(TrayConnectionStatus status) => status switch
    {
        TrayConnectionStatus.ActiveSession => Color.LimeGreen,
        TrayConnectionStatus.MemoryMapConnected => Color.DodgerBlue,
        _ => Color.Red,
    };

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}

internal static class RectangleExtensions
{
    public static Rectangle InflateBy(this Rectangle rectangle, int amount)
    {
        rectangle.Inflate(amount, amount);
        return rectangle;
    }
}
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace ParentalControl.Overlay;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length >= 2 && e.Args[0].Equals("--screenshot-pipe", StringComparison.OrdinalIgnoreCase))
        {
            var exitCode = CaptureScreenshotToPipe(e.Args[1]) ? 0 : 1;
            Shutdown(exitCode);
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0].Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
        {
            var exitCode = CaptureScreenshot(e.Args[1]) ? 0 : 1;
            Shutdown(exitCode);
            return;
        }

        if (e.Args.Length >= 1 && e.Args[0].Equals("--dashboard", StringComparison.OrdinalIgnoreCase))
        {
            var dash = new ChildDashboardWindow();
            MainWindow = dash;
            dash.Show();
            return;
        }

        _instanceMutex = new Mutex(true, @"Local\ParentalControl_Overlay_Mutex", out var createdNew);
        if (!createdNew)
        {
            Shutdown(0);
            return;
        }

        var window = new CountdownWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static byte[]? CapturePngBytes()
    {
        SetProcessDPIAware();
        var bounds = SystemInformation.VirtualScreen;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static bool CaptureScreenshotToPipe(string pipeName)
    {
        try
        {
            var bytes = CapturePngBytes();
            if (bytes is null) return false;

            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipe.Connect(5000);
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CaptureScreenshot(string outputPath)
    {
        try
        {
            var bytes = CapturePngBytes();
            if (bytes is null) return false;

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(outputPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using System.IO;
using System.Windows;
using ParentalControl.Core.Data;

namespace ParentalControl.Admin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrash(args.ExceptionObject as Exception);
        };
        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = false;
        };

        base.OnStartup(e);
        DatabaseManager.Initialize();
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ParentalControl", "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "AdminCrash.log"), $"{DateTime.Now:O}\n{ex}\n\n");
        }
        catch { }
    }
}

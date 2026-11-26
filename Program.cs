using Avalonia;
using Serilog;

namespace LightCrm;

internal static class Program {
    [STAThread]
    public static int Main(string[] args) {
        ConfigureLogging();

        AppDomain.CurrentDomain.UnhandledException += (_, e) => {
            Exception? exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "Необроблений виняток");
        };

        TaskScheduler.UnobservedTaskException += (_, e) => {
            Log.Error(e.Exception, "Необроблений виняток у фоновій задачі");
            e.SetObserved();
        };

        try {
            Log.Information("Запуск застосунку «Легкий CRM»");
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Критична помилка застосунку");
            return 1;
        }
        finally {
            Log.Information("Завершення роботи");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() {
        var builder = AppBuilder.Configure<App>();
        builder = builder.UsePlatformDetect();
        builder = builder.WithInterFont();
        return builder;
    }

    private static void ConfigureLogging() {
        string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "crm-.log");

        var configuration = new LoggerConfiguration();
        configuration.MinimumLevel.Debug();
        configuration.WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        Log.Logger = configuration.CreateLogger();
    }
}

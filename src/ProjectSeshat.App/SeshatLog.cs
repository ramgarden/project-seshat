using System;
using System.IO;
using System.Threading;

namespace ProjectSeshat.App;

/// <summary>
/// Minimal file logger that writes Timestamped diagnostic lines to a log file next to the
/// database (under the user's AppData). Also installs global crash handlers so that any
/// unhandled exception is captured to disk instead of leaving only an opaque dialog.
/// </summary>
public static class SeshatLog
{
    private static readonly object Sync = new();
    private static string? _logDirectory;

    public static string LogDirectory
    {
        get
        {
            lock (Sync)
            {
                _logDirectory ??= ResolveDirectory();
                return _logDirectory;
            }
        }
    }

    public static string LogFilePath => Path.Combine(LogDirectory, "seshat.log");

    /// <summary>Installs AppDomain/TaskScheduler crash handlers that write to the log file.</summary>
    public static void InstallGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var message = e.IsTerminating ? "FATAL (terminating)" : "Unhandled exception";
            Write("CRASH", message, e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Write("WARN", "Unobserved task exception", e.Exception);
        };
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message) => Write("ERROR", message, null);

    public static void LogError(Exception? exception, string message)
    {
        if (exception is null)
        {
            Write("ERROR", message, null);
            return;
        }

        Write("ERROR", $"{message}: {exception.Message}", exception);
    }

    private static string ResolveDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDirectory = Path.Combine(appData, "ProjectSeshat");
        var logDirectory = Path.Combine(baseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        return logDirectory;
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = exception is null
                ? $"{DateTimeOffset.Now:HH:mm:ss.fff} [{Thread.CurrentThread.ManagedThreadId}] {level} {message}{Environment.NewLine}"
                : $"{DateTimeOffset.Now:HH:mm:ss.fff} [{Thread.CurrentThread.ManagedThreadId}] {level} {message}{Environment.NewLine}{exception}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Logging must never take down the app.
        }
    }
}
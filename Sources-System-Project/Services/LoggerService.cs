using System;
using System.IO;

namespace Sources.Services;

public static class LoggerService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources", "Logs");

    public static void LogInfo(string message) => WriteLog("INFO", message);
    public static void LogWarning(string message) => WriteLog("WARN", message);
    public static void LogError(string message, Exception? ex = null) =>
        WriteLog("ERROR", $"{message}{(ex != null ? $"\n{ex}" : "")}");

    private static void WriteLog(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var logFile = Path.Combine(LogDir, $"sources_{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}\n";
            File.AppendAllText(logFile, line);
        }
        catch { }
    }
}

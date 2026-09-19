using System.Reflection;
using System.Text;
using Microsoft.Maui.Storage;

namespace Bodh.ai;

public interface ILoggerService
{
    void LogException(Exception exception, string context = "");
    void LogEvent(string message, string category = "General");
    void LogUserEvent(string message);
    void LogAiEvent(string message);
    string LogFilePath { get; }
}

public sealed class LoggerService : ILoggerService
{
    private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
    private readonly object _syncRoot = new();
    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public static LoggerService Instance => _instance.Value;

    private LoggerService()
    {
        // Developer code: packaged MacCatalyst app bundles are read-only, so logs must use
        // MAUI's writable per-user application-data directory at runtime.
        var baseDirectory = FileSystem.AppDataDirectory;
        _logDirectory = Path.Combine(baseDirectory, "logger");
        _logFilePath = Path.Combine(_logDirectory, "bodh-ai-log.txt");

        EnsureLogDirectory();

        try
        {
            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(_logFilePath, $"[{DateTime.UtcNow:O}] Log initialized.{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            WriteFallbackLog($"[{DateTime.UtcNow:O}] [LOGGER] Unable to initialize log file. Details: {ex}");
        }
    }

    public string LogFilePath => _logFilePath;

    public void LogException(Exception exception, string context = "")
    {
        var message = BuildLogMessage("EXCEPTION", context, exception.ToString());
        WriteLog(message);
    }

    public void LogEvent(string message, string category = "General")
    {
        var formattedMessage = BuildLogMessage(category, string.Empty, message);
        WriteLog(formattedMessage);
    }

    public void LogUserEvent(string message)
    {
        LogEvent(message, "USER");
    }

    public void LogAiEvent(string message)
    {
        LogEvent(message, "AI");
    }

    private void EnsureLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch (Exception ex)
        {
            // Agent code: if the folder cannot be created, the logger should still keep a fallback file in the app directory without crashing the app.
            WriteFallbackLog($"[{DateTime.UtcNow:O}] [LOGGER] Unable to create logger directory. Details: {ex}");
        }
    }

    private void WriteLog(string message)
    {
        try
        {
            lock (_syncRoot)
            {
                var content = $"{message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, content, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            WriteFallbackLog($"[{DateTime.UtcNow:O}] [LOGGER] Failed to write log message. Details: {ex}");
        }
    }

    private void WriteFallbackLog(string message)
    {
        try
        {
            var fallbackDirectory = FileSystem.AppDataDirectory;
            Directory.CreateDirectory(fallbackDirectory);
            var fallbackPath = Path.Combine(fallbackDirectory, "bodh-ai-fallback-log.txt");
            File.AppendAllText(fallbackPath, $"{message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // Agent code: final fail-safe prevents app crashes when logging itself fails.
        }
    }

    private static string BuildLogMessage(string category, string context, string details)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        var contextText = string.IsNullOrWhiteSpace(context) ? string.Empty : $" Context: {context}";
        return $"[{timestamp}] [{category}]{contextText} Details: {details}";
    }
}

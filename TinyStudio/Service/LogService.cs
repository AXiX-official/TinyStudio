using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;
using TinyStudio.Models;

namespace TinyStudio.Service;

public static class LogService
{
    public static event Action<LogEntry>? OnLogMessage;

    private static readonly ConcurrentQueue<LogEntry> LogQueue = new();
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static string _currentLogFile = "";
    private static Settings _settings = new();
    private static bool _initialized;
    private static readonly CancellationTokenSource Cts = new();

    public static void Initialize(Settings settings)
    {
        if (_initialized)
        {
            return;
        }

        _settings = settings;
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        CleanupAndRotateLogs();

        _currentLogFile = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        
        Task.Run(() => ProcessLogQueue(Cts.Token));

        _initialized = true;
        Info("LogService Initialized.");
    }

    public static void Verbose(string message) => Log(LogLevel.Verbose, message);
    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            message = $"{message}\n{ex}";
        }
        Log(LogLevel.Error, message);
    }

    public static void Log(LogLevel level, string message)
    {
        var logEntry = new LogEntry(level, message);
        Trace.WriteLine(logEntry.ToString()); // Output to debug console
        LogQueue.Enqueue(logEntry);
        OnLogMessage?.Invoke(logEntry);
    }

    private static async Task ProcessLogQueue(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (LogQueue.TryDequeue(out var logEntry))
            {
                /*try
                {*/
                await File.AppendAllTextAsync(_currentLogFile, logEntry.ToString() + Environment.NewLine, Encoding.UTF8, token);
                //}
                /*catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to write to log file: {ex.Message}");
                }*/
            }
            else
            {
                await Task.Delay(100, token);
            }
        }
    }

    private static void CleanupAndRotateLogs()
    {
        var logFiles = new DirectoryInfo(_logDirectory)
            .GetFiles("log_*.txt")
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        if (logFiles.Any())
        {
            var lastLog = logFiles.First();
            if (_settings.EnableLogCompression)
            {
                CompressFile(lastLog.FullName);
            }
        }

        var compressedFiles = new DirectoryInfo(_logDirectory)
            .GetFiles("log_*.lz4")
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        var filesToDelete = logFiles.Skip(_settings.LogFilesToKeep)
            .Concat(compressedFiles.Skip(_settings.LogFilesToKeep))
            .ToList();

        foreach (var file in filesToDelete)
        {
            file.Delete();
            Info($"Deleted old log file: {file.Name}");
        }
    }

    private static void CompressFile(string filePath)
    {
        var compressedFilePath = Path.ChangeExtension(filePath, ".lz4");
        /*try
        {*/
            using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var targetStream = new FileStream(compressedFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var compressionStream = LZ4Stream.Encode(targetStream);
            sourceStream.CopyTo(compressionStream);
            compressionStream.Close();
            sourceStream.Close();
            File.Delete(filePath);
            Info($"Compressed log file: {Path.GetFileName(filePath)} to {Path.GetFileName(compressedFilePath)}");
        /*}
        catch (Exception ex)
        {
            Error($"Failed to compress log file: {filePath}", ex);
        }*/
    }

    public static void Shutdown()
    {
        Cts.Cancel();
        Info("LogService shutting down.");
    }
}

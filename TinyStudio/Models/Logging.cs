using System;

namespace TinyStudio.Models;

public enum LogLevel
{
    Verbose,
    Debug,
    Info,
    Warning,
    Error
}

public readonly struct LogEntry
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public LogLevel Level { get; }
    public string Message { get; }

    public LogEntry(LogLevel level, string message)
    {
        Level = level;
        Message = message;
    }

    public override string ToString()
    {
        return $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}";
    }
}

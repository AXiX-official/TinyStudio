using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using TinyStudio.Models;
using TinyStudio.Service;

namespace TinyStudio.ViewModels;

public partial class ConsoleLogViewModel : ViewModelBase
{
    private readonly List<LogEntry> _allLogs = new();
    
    [ObservableProperty]
    private TextDocument _logDocument = new();

    [ObservableProperty]
    private LogLevel _minimumLevel = LogLevel.Info;

    public IReadOnlyList<LogLevel> AvailableLevels { get; } = Enum.GetValues<LogLevel>();

    public ConsoleLogViewModel()
    {
        LogService.OnLogMessage += OnLogMessage;
    }

    partial void OnMinimumLevelChanged(LogLevel value)
    {
        RefreshDisplayedLogs();
    }

    private void OnLogMessage(LogEntry logEntry)
    {
        _allLogs.Add(logEntry);
        if (logEntry.Level >= MinimumLevel)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogDocument.Insert(LogDocument.TextLength, logEntry.ToString() + Environment.NewLine);
            });
        }
    }

    private void RefreshDisplayedLogs()
    {
        var filteredLogs = _allLogs.Where(log => log.Level >= MinimumLevel);
        var newText = string.Join(Environment.NewLine, filteredLogs.Select(log => log.ToString())) + Environment.NewLine;
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogDocument.Text = newText;
        });
    }

    public void Cleanup()
    {
        LogService.OnLogMessage -= OnLogMessage;
    }
}

using System.Collections.Generic;

namespace TinyStudio.Models;

public class Settings
{
    public string Theme { get; set; } = "Default";
    public Game GameType { get; set; } = Game.Normal;
    public UnityCNKey? UnityCNKey { get; set; }
    public bool EnableLogCompression { get; set; } = true;
    public int LogFilesToKeep { get; set; } = 2;
    public bool EnableDump { get; set; }
    public bool EnablePreview { get; set; } = true;
    public bool EnableConsole { get; set; } = true;
    public LogLevel ConsoleLogLevel { get; set; } = LogLevel.Info;
}

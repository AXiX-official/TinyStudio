using System.Collections.Generic;

namespace TinyStudio.Models;

public class Settings
{
    public string Theme { get; set; } = "Default";
    public UnityCNKey? UnityCNKey { get; set; }
    public bool EnableLogCompression { get; set; } = true;
    public int LogFilesToKeep { get; set; } = 2;
}

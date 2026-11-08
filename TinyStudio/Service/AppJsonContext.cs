using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using TinyStudio.Models;

namespace TinyStudio.Service;

/// <summary>
/// Source-generated serialization metadata for every JSON payload TinyStudio reads or writes.
/// Native AOT disables reflection-based System.Text.Json, so each root type must be listed here.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(List<UnityCNKey>))]
[JsonSerializable(typeof(ObservableCollection<UnityCNKey>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}

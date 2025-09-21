using System.Text.Json.Serialization;

namespace YukkuDock.Core.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
public partial class SettingsJsonContext : JsonSerializerContext
{
}

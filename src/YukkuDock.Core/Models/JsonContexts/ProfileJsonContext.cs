using System.Text.Json.Serialization;

namespace YukkuDock.Core.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Profile))]
public partial class ProfileJsonContext : JsonSerializerContext
{
}

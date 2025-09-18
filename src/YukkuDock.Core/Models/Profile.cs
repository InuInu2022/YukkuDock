using System.Reflection.PortableExecutable;

namespace YukkuDock.Core.Models;

/// <summary>
/// ユーザープロファイルを表します。
/// </summary>
public record Profile
{
	public Guid Id { get; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// YMM4のバージョン情報を表します。
	/// </summary>
	public Version? AppVersion { get; set; }

	/// <summary>
	/// YMM4のインストールパスを表します。
	/// </summary>
	public string AppPath { get; set; } = string.Empty;

	public ICollection<PluginPack> PluginPacks { get; set; } = [];

	public ICollection<Layout> Layouts { get; set; } = [];

	public ICollection<ItemTemplate> ItemTemplates { get; set; } = [];

	public ICollection<VoiceCharacter> VoiceCharacters { get; set; } = [];
}

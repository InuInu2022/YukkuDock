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

/// <summary>
/// プラグイン、レイアウト、テンプレート、キャラクター等の管理対象のデータの基底クラス
/// </summary>
public abstract record ProfileEntityBase
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>
	/// 所属するプロファイルのIDを表します。
	/// </summary>
	public Guid ProfileId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
}

public interface IActivatable
{
	/// <summary>
	/// 対象が有効かどうかを表します。
	/// 無効の場合、<see cref="MovedPath"/>に移動されます。
	/// </summary>
	bool IsEnabled { get; set; }
	string MovedPath { get; set; }
}

public interface IBackupable
{
	string InstalledPath { get; set; }

	bool IsIgnoredBackup { get; set; }
	string BackupPath { get; set; }
}

public record PluginPack : ProfileEntityBase, IBackupable, IActivatable
{
	public string Version { get; set; } = string.Empty;
	public string Author { get; set; } = string.Empty;
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;
	public bool IsIgnoredBackup { get; set; } = false;
	public string BackupPath { get; set; } = string.Empty;
}

public record Layout : ProfileEntityBase, IBackupable
{
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;

	public bool IsIgnoredBackup { get; set; } = false;
	public string BackupPath { get; set; } = string.Empty;
}

public record ItemTemplate : ProfileEntityBase, IBackupable
{
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;

	public bool IsIgnoredBackup { get; set; } = false;
	public string BackupPath { get; set; } = string.Empty;
}

public record VoiceCharacter : IBackupable
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid ProfileId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;

	public bool IsIgnoredBackup { get; set; } = false;
	public string BackupPath { get; set; } = string.Empty;
}

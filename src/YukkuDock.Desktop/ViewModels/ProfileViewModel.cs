using System.Diagnostics.CodeAnalysis;

using Epoxy;

using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

/// <summary>
/// ユーザープロファイルのViewModelを表します。
/// </summary>
[ViewModel]
public partial class ProfileViewModel(Profile profile)
{
	readonly Profile _profile = profile;

	public string Name { get; set; } = profile.Name;
	public Version? AppVersion { get; set; } = profile.AppVersion;
	public string Description { get; set; } = profile.Description;
	public string AppPath { get; set; } = profile.AppPath;

	public ICollection<PluginPack> PluginPacks { get; set; } = profile.PluginPacks;

	public bool IsAppExists { get; private set; }

	[PropertyChanged(nameof(Name))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask NameChangedAsync(string value)
	{
		if (!string.Equals(_profile.Name, value, StringComparison.Ordinal))
		{
			_profile.Name = value;
		}
		return default;
	}

	[PropertyChanged(nameof(Description))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask DescriptionChangedAsync(string value)
	{
		if (!string.Equals(_profile.Description, value, StringComparison.Ordinal))
		{
			_profile.Description = value;
		}
		return default;
	}

	[PropertyChanged(nameof(AppPath))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AppPathChangedAsync(string value)
	{
		if (!string.Equals(_profile.AppPath, value, StringComparison.Ordinal))
		{
			_profile.AppPath = value;
			IsAppExists = File.Exists(value);
		}
		return default;
	}

	[PropertyChanged(nameof(AppVersion))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AppVersionChangedAsync(Version? value)
	{
		if (!Equals(_profile.AppVersion, value))
		{
			_profile.AppVersion = value;
		}
		return default;
	}
}

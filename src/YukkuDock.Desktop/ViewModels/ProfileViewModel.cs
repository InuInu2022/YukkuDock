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
	readonly Profile profile = profile;

	public string Name { get; set; } = profile.Name;
	public Version? AppVersion { get; set; } = profile.AppVersion;
	public string Description { get; set; } = profile.Description;
	public string AppPath { get; set; } = profile.AppPath;

	public bool IsAppExists { get; private set; }

	[PropertyChanged(nameof(Name))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask NameChangedAsync(string value)
	{
		if (!string.Equals(profile.Name, value, StringComparison.Ordinal))
		{
			profile.Name = value;
		}
		return default;
	}

	[PropertyChanged(nameof(Description))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask DescriptionChangedAsync(string value)
	{
		if (!string.Equals(profile.Description, value, StringComparison.Ordinal))
		{
			profile.Description = value;
		}
		return default;
	}

	[PropertyChanged(nameof(AppPath))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AppPathChangedAsync(string value)
	{
		if (!string.Equals(profile.AppPath, value, StringComparison.Ordinal))
		{
			profile.AppPath = value;
			IsAppExists = File.Exists(value);
		}
		return default;
	}

	[PropertyChanged(nameof(AppVersion))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AppVersionChangedAsync(Version? value)
	{
		if (!Equals(profile.AppVersion, value))
		{
			profile.AppVersion = value;
		}
		return default;
	}
}

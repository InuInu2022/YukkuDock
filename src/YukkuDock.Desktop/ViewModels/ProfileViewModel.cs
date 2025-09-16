using System.Diagnostics.CodeAnalysis;

using Epoxy;

using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

/// <summary>
/// ユーザープロファイルのViewModelを表します。
/// </summary>
[ViewModel]
public partial class ProfileViewModel
{
	readonly Profile profile;

	public ProfileViewModel(Profile profile)
	{
		this.profile = profile;
		Name = profile.Name;
		AppVersion = profile.AppVersion;
	}

	public string Name { get; set; }
	public Version? AppVersion { get; set; }

	[PropertyChanged(nameof(Name))]
	[SuppressMessage("","IDE0051")]
	private ValueTask NameChangedAsync(string value)
	{
		if (!string.Equals(profile.Name, value, StringComparison.Ordinal))
		{
			profile.Name = value;
		}
		return default;
	}
}

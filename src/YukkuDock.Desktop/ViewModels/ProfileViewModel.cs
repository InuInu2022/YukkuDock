using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Epoxy;
using YukkuDock.Core.Models;
using YukkuDock.Core.Services;

namespace YukkuDock.Desktop.ViewModels;

/// <summary>
/// ユーザープロファイルのViewModelを表します。
/// </summary>
[ViewModel]
public partial class ProfileViewModel(Profile profile, IProfileService profileService)
{
	public Profile Profile { get; } = profile;

	public string Name { get; set; } = profile.Name;
	public Version? AppVersion { get; set; } = profile.AppVersion;
	public string Description { get; set; } = profile.Description;
	public string AppPath { get; set; } = profile.AppPath;

	public ICollection<PluginPack> PluginPacks { get; set; } = profile.PluginPacks;

	public bool IsAppExists { get; private set; }

	Timer? saveTimer;
	const int DebounceMilliseconds = 1000;
	readonly IProfileService _profileService = profileService;

	readonly Lock saveTimerLock = new();


	public void UpdateYmmVersion()
	{
		if (!File.Exists(Profile.AppPath)) return;
		var info = FileVersionInfo.GetVersionInfo(Profile.AppPath);
		Debug.WriteLine(info.FileVersion);
		if (Version.TryParse(info.FileVersion, out var version))
		{
			AppVersion = version;
		}
	}

	[SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "<保留中>")]
	[SuppressMessage("Usage", "MA0147:Avoid async void method for delegate", Justification = "<保留中>")]
	[SuppressMessage("Concurrency", "PH_S034:Async Lambda Inferred to Async Void", Justification = "<保留中>")]
	void RequestSave()
	{
		// Timerをリセット（既存TimerがあればDispose）
		saveTimer?.Dispose();
		saveTimer = new Timer(
			async _ =>
			{
				try
				{
					var result = await _profileService
						.TrySaveAsync(Profile)
						.ConfigureAwait(true);
					if (!result.Success)
					{
						Debug.WriteLine("プロファイルの保存に失敗しました。");
					}
				}
				catch (Exception)
				{
					Debug.WriteLine("プロファイルの保存に失敗しました。");
				}
				finally
				{
					lock (saveTimerLock)
					{
						if (saveTimer is not null)
						{
							saveTimer?.Dispose();
							saveTimer = null;
						}
					}
				}
			},
			null,
			DebounceMilliseconds,
			Timeout.Infinite
		);
	}

	[PropertyChanged(nameof(Name))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask NameChangedAsync(string value)
	{
		if (!string.Equals(Profile.Name, value, StringComparison.Ordinal))
		{
			Profile.Name = value;
			RequestSave();
		}
		return default;
	}

	[PropertyChanged(nameof(Description))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask DescriptionChangedAsync(string value)
	{
		if (!string.Equals(Profile.Description, value, StringComparison.Ordinal))
		{
			Profile.Description = value;
			RequestSave();
		}
		return default;
	}

	[PropertyChanged(nameof(AppPath))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AppPathChangedAsync(string value)
	{
		IsAppExists = File.Exists(value);
		if (!string.Equals(Profile.AppPath, value, StringComparison.Ordinal))
		{
			Profile.AppPath = value;
			RequestSave();
		}
		return default;
	}

	[PropertyChanged(nameof(AppVersion))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AppVersionChangedAsync(Version? value)
	{
		if (!Equals(Profile.AppVersion, value))
		{
			Profile.AppVersion = value;
			RequestSave();
		}
		return default;
	}

	[PropertyChanged(nameof(PluginPacks))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask PluginPacksChangedAsync(ICollection<PluginPack> value)
	{
		if (!Equals(Profile.PluginPacks, value))
		{
			Profile.PluginPacks = value;
			RequestSave();
		}
		return default;
	}
}

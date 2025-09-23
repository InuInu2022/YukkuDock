using System.IO.Compression;

using YukkuDock.Core.Models;

using YukkuDock.Core.Services;

namespace YukkuDock.Core;

public static class BackupManager
{


	[System.Diagnostics.CodeAnalysis.SuppressMessage("Correctness", "SS002:DateTime.Now was referenced", Justification = "<保留中>")]
	public static async ValueTask<TryAsyncResult<bool>> TryBackupProfileAsync(
		IProfileService profileService,
		Profile profile,
		CancellationToken cancellationToken = default)
	{
		var profileFolder = profileService.GetProfileFolder(profile.Id);
		var backupFolder = profileService.GetProfileBackupFolder(profile.Id);

		try
		{
			if (!Directory.Exists(backupFolder))
			{
				Directory.CreateDirectory(backupFolder);
			}
		}
		catch (Exception ex)
		{
			return new(false, false, ex);
		}

		var now = DateTime.Now;
		var backupFileName = $"profile_{now:yyyyMMdd_HHmmss}.zip";
		var backupFilePath = Path.Combine(backupFolder, backupFileName);

		if (File.Exists(backupFilePath))
		{
			// 既存ファイルが使用中の場合はエラー通知
			try
			{
				File.Delete(backupFilePath);
			}
			catch (IOException ex)
			{
				// ユーザーに通知
				return new(false, false, ex);
			}
		}

		//現在の最新を保存
		var result = await profileService.TrySaveAsync(profile)
			.ConfigureAwait(false);
		if (!result.Success)
		{
			return result;
		}

		await Task.Run(() =>
		{
			using var archive = ZipFile
				.Open(backupFilePath, ZipArchiveMode.Create);

			// GUID名のフォルダごと圧縮するため、ルートにGUIDフォルダを追加
			void AddDirectory(string sourceDir, string baseDir, string guidFolderName)
			{
				foreach (var file in Directory.EnumerateFiles(sourceDir))
				{
					// entryName: GUID/xxx/yyy
					var entryName = Path.Combine(
						guidFolderName,
						Path.GetRelativePath(baseDir, file)
					);
					archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
				}
				foreach (var dir in Directory.EnumerateDirectories(sourceDir))
				{
					AddDirectory(dir, baseDir, guidFolderName);
				}
			}

			// GUID名のフォルダで圧縮
			AddDirectory(profileFolder, profileFolder, profile.Id.ToString());
		}, cancellationToken)
		.ConfigureAwait(false);


		return new(Success: true, Value: true);
	}


	[System.Diagnostics.CodeAnalysis.SuppressMessage("Correctness", "SS002:DateTime.Now was referenced", Justification = "<保留中>")]
	public static async ValueTask<TryAsyncResult<bool>> TryBackupPluginPacksAsync(
		IProfileService profileService,
		Profile profile,
		IEnumerable<PluginPack>? plugins = null,
		CancellationToken cancellationToken = default
	)
	{
		//var profileFolder = profileService.GetProfileFolder(profile.Id);
		var backupFolder = profileService.GetPluginPacksBackupFolder(profile.Id);

		try
		{
			if (!Directory.Exists(backupFolder))
			{
				Directory.CreateDirectory(backupFolder);
			}
		}
		catch (Exception ex)
		{
			return new(false, false, ex);
		}

		//指定なければすべてが対象
		plugins ??= profile.PluginPacks;

		try
		{
			foreach (var plugin in plugins)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (plugin is null) continue;

				var now = DateTime.Now;
				var backupFileName = $"{plugin.Name}_{now:yyyyMMdd_HHmmss}.zip";
				var backupFilePath = Path.Combine(backupFolder, backupFileName);

				if (File.Exists(backupFilePath))
				{
					// 既存ファイルが使用中の場合はエラー通知
					try
					{
						File.Delete(backupFilePath);
					}
					catch (IOException ex)
					{
						// ユーザーに通知
						return new(false, false, ex);
					}
				}

				var destFile = new FileInfo(backupFilePath);

				string? parent = "";
				try
				{
					parent = Directory.GetParent(plugin.InstalledPath)?.FullName;
				}
				catch (Exception ex)
				{
					return new(false, false, ex);
				}

				await Task.Run(() =>
				{
					ZipFile.CreateFromDirectory(
						parent!,
						destFile.FullName,
						CompressionLevel.Optimal,
						includeBaseDirectory: true
					);
				}, cancellationToken)
				.ConfigureAwait(false);

			}

			return new(true, true);
		}
		catch (Exception ex)
		{
			return new(false, false, ex);
		}
	}

}

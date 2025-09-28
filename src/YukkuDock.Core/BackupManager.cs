using System.IO.Compression;
using System.Text.RegularExpressions;

using YukkuDock.Core.Models;

using YukkuDock.Core.Services;

namespace YukkuDock.Core;

public static partial class BackupManager
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
					// PluginPacks/Backup フォルダ配下は除外
					var relativePath = Path.GetRelativePath(baseDir, file);
					if (relativePath.Contains("PluginPacks" + Path.DirectorySeparatorChar + "Backup" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					var entryName = Path.Combine(guidFolderName, relativePath);
					archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
				}
				foreach (var dir in Directory.EnumerateDirectories(sourceDir))
				{
					// PluginPacks/Backup フォルダ自体も除外
					var relativeDir = Path.GetRelativePath(baseDir, dir);
					if (relativeDir.Contains("PluginPacks" + Path.DirectorySeparatorChar + "Backup", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
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
		var parentFolder = Directory.GetParent(backupFolder);

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
				var reg = GetBackupFileRegex();
				string pName = Path.GetFileNameWithoutExtension(
					reg.Replace(plugin.Name, "", 1));
				var backupFileName = $"{plugin.FolderName}_{pName}_{now:yyyyMMdd_HHmmss}.zip";
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
						CompressionLevel.Fastest,
						includeBaseDirectory: true
					);

					//一つ上の階層にも上書き保存
					File.Copy(
						destFile.FullName,
						Path.Combine(
							parentFolder!.FullName,
							//日付除去
							$"{plugin.FolderName}_{pName}.zip"),
						overwrite: true
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

	[GeneratedRegex(@"\.disabled$", RegexOptions.IgnoreCase)]
	private static partial Regex GetBackupFileRegex();
}

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using YukkuDock.Core.Models;
using YukkuDock.Core.Services;

namespace YukkuDock.Core;

public static class PluginManager
{
	public static readonly IReadOnlySet<string> ExcludeDllPatterns = new HashSet<string>(
		StringComparer.OrdinalIgnoreCase
	)
	{
		"webview2",
		"interactivity",
		"wpfgfx",
		"presentation",
		"presentationframework",
		"presentationcore",
		"windows",
		"windowsbase",
		"epoxy",
		"material.icons",
		"flaui",
		"interop",
		"microsoft.win32",
		"api-ms-win-",
		"windows.",
		"winrt.",
		".winmd",
	}.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// InstalledPath が古い/不正でも、同ディレクトリ内から現在実在する候補（.dll / .dll.disabled）を解決する。
	/// 例）xxx.dll ⇔ xxx.dll.disabled、二重 .disabled の正規化にも対応。
	/// </summary>
	static FileInfo? ResolveExistingVariant(FileInfo candidate)
	{
		// 実在する *.dll / *.dll.disabled を同ディレクトリから特定する
		var dir = candidate.Directory?.FullName;
		if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
		{
			return null;
		}

		// 末尾の .disabled を畳み込んでから "xxx.dll" をベース名に確定
		var name = candidate.Name;
		while (name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
		{
			name = name[..^(".disabled".Length)];
		}
		var idx = name.LastIndexOf(".dll", StringComparison.OrdinalIgnoreCase);
		if (idx < 0)
		{
			return null;
		}
		var baseDllName = name[..(idx + 4)]; // "xxx.dll"

		// 探索候補
		string[] candidates =
		[
			Path.Combine(dir, baseDllName),
			Path.Combine(dir, baseDllName + ".disabled"),
			Path.Combine(dir, baseDllName + ".disabled.disabled"),
		];

		foreach (var p in candidates)
		{
			if (File.Exists(p))
			{
				// 二重 .disabled は即時正規化
				if (p.EndsWith(".dll.disabled.disabled", StringComparison.Ordinal))
				{
					var normalized = p.Replace(".dll.disabled.disabled", ".dll.disabled", StringComparison.Ordinal);
					if (!File.Exists(normalized))
					{
						new FileInfo(p).MoveTo(normalized);
					}
					return new FileInfo(normalized);
				}
				return new FileInfo(p);
			}
		}

		return null;
	}

	/// <summary>
	/// プラグインの有効/無効を切り替える。失敗時はSuccess=false。
	/// </summary>
	public static async ValueTask<TryAsyncResult<string>> TryChangeStatusPluginAsync(
		PluginPack plugin,
		bool enable,
		CancellationToken cancellationToken = default
	)
	{
		try
		{
			var originalPath = plugin.InstalledPath;
			var pluginFile = new FileInfo(originalPath);

			// UI側の InstalledPath が古い/ズレていても実在ファイルへ補正
			var resolved = ResolveExistingVariant(pluginFile);
			if (resolved is not null)
			{
				pluginFile = resolved;
			}

			if (!pluginFile.Exists)
			{
				return new(false, string.Empty, new FileNotFoundException("Plugin file not found.", plugin.InstalledPath));
			}

			// 以降は pluginFile.FullName を基準に .dll ⇔ .dll.disabled のみを移動
			// ".dll.disabled.disabled" は上の ResolveExistingVariant で正規化済み前提
			var fileName = pluginFile.Name;
			var isDll =
				fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".dll.disabled.disabled", StringComparison.OrdinalIgnoreCase);
			if (!isDll)
			{
				return new(false, string.Empty);
			}

			// 現在状態を判定（.dll.disabled.disabled は前段で正規化済みの想定だが二重に見ておく）
			var currentPath = pluginFile.FullName;
			var isCurrentlyDisabled =
				currentPath.EndsWith(".dll.disabled", StringComparison.Ordinal) ||
				currentPath.EndsWith(".dll.disabled.disabled", StringComparison.Ordinal);

			string targetPath;
			if (enable)
			{
				// 有効化：.dll.disabled[.disabled] → .dll
				targetPath = isCurrentlyDisabled
					? currentPath
						.Replace(".dll.disabled.disabled", ".dll", StringComparison.Ordinal)
						.Replace(".dll.disabled", ".dll", StringComparison.Ordinal)
					: currentPath; // 既に有効
			}
			else
			{
				// 無効化：.dll → .dll.disabled（既に無効なら現状維持、二重無効は正規化）
				if (currentPath.EndsWith(".dll", StringComparison.Ordinal) && !isCurrentlyDisabled)
				{
					targetPath = currentPath + ".disabled";
				}
				else if (currentPath.EndsWith(".dll.disabled.disabled", StringComparison.Ordinal))
				{
					targetPath = currentPath.Replace(".dll.disabled.disabled", ".dll.disabled", StringComparison.Ordinal);
				}
				else
				{
					targetPath = currentPath; // 既に無効
				}
			}

			if (string.Equals(currentPath, targetPath, StringComparison.Ordinal))
			{
				// 呼び出し元が追随できるよう、現行パスを返す
				return new(true, currentPath);
			}

			await Task.Run(() =>
			{
				// 競合回避：ターゲットが既にあれば削除してから移動
				if (File.Exists(targetPath))
				{
					File.Delete(targetPath);
				}
				pluginFile.MoveTo(targetPath);
			}, cancellationToken).ConfigureAwait(false);

			return new(true, targetPath);
		}
		catch (Exception ex)
		{
			return new(false, string.Empty, ex);
		}
	}

	[RequiresUnreferencedCode(
		"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
	)]
	[SuppressMessage(
		"Minor Code Smell",
		"S3267:Loops should be simplified with \"LINQ\" expressions",
		Justification = "<保留中>"
	)]
	public static async ValueTask<ICollection<PluginPack>> LoadPluginsFromDirectoryAsync(
		string appPath,
		DirectoryInfo folder
	)
	{
		return await LoadPluginsFromDirectoryAsync(appPath, folder, int.MaxValue)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// 指定ディレクトリからプラグインをロードする（各フォルダ内のDLL最大数制限付き）。
	/// </summary>
	/// <param name="appPath">アプリケーションパス</param>
	/// <param name="folder">プラグインフォルダ</param>
	/// <param name="maxPluginsPerFolder">各フォルダ内のDLL最大ロード数</param>
	/// <returns>ロードされたプラグインのコレクション</returns>
	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.ProcessPluginCandidateAsync(FileInfo, ConcurrentBag<PluginPack>, PluginLoadContext, CancellationToken)"
	)]
	public static async ValueTask<ICollection<PluginPack>> LoadPluginsFromDirectoryAsync(
		string appPath,
		DirectoryInfo folder,
		int maxPluginsPerFolder
	)
	{
		var pluginDirs = folder.GetDirectories().Where(x => x.Exists).ToArray();
		var appDir = Path.GetDirectoryName(appPath)!;
		var pluginPacks = new ConcurrentBag<PluginPack>();

		using var pluginContext = new PluginLoadContext(appDir);

		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
		};

		await Parallel
			.ForEachAsync(
				pluginDirs,
				parallelOptions,
				async (dir, ct) =>
					await ProcessPluginDirectoryAsync(
							dir,
							pluginPacks,
							pluginContext,
							maxPluginsPerFolder,
							ct
						)
						.ConfigureAwait(false)
			)
			.ConfigureAwait(false);

		return [.. pluginPacks];
	}

	/// <summary>
	/// プラグイン情報を段階的にロードする（基本情報→詳細情報）
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.LoadPluginDetailAsync(FileInfo, PluginLoadContext, CancellationToken)"
	)]
	public static async ValueTask<ICollection<PluginPack>> LoadPluginsProgressivelyAsync(
		string appPath,
		DirectoryInfo folder,
		Guid profileId,
		int maxPluginsPerFolder,
		IProgress<PluginPack>? progress = null,
		CancellationToken cancellationToken = default
	)
	{
		var pluginDirs = folder.GetDirectories().Where(x => x.Exists).ToArray();
		var appDir = Path.GetDirectoryName(appPath)!;
		var pluginPacks = new ConcurrentBag<PluginPack>();

		var basicPlugins = new List<PluginPack>();

		foreach (var dir in pluginDirs)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			var candidates = await GetPluginCandidatesAsync(dir).ConfigureAwait(false);
			foreach (var dllFile in candidates.Take(maxPluginsPerFolder))
			{
				if (cancellationToken.IsCancellationRequested)
					break;

				if (!dllFile.Exists)
					continue;

				var basicPlugin = CreateBasicPluginPack(dllFile);
				basicPlugin.ProfileId = profileId;
				pluginPacks.Add(basicPlugin);
				basicPlugins.Add(basicPlugin);

				progress?.Report(basicPlugin);
			}
		}

		_ = Task.Run(
			() =>
			{
				using var detailPluginContext = new PluginLoadContext(appDir);

				foreach (ref var basicPlugin in CollectionsMarshal.AsSpan(basicPlugins))
				{
					if (cancellationToken.IsCancellationRequested)
						break;

					try
					{
						var dllFile = new FileInfo(basicPlugin.InstalledPath);

						var detailedPlugin = LoadPluginDetailSync(
							dllFile,
							detailPluginContext,
							cancellationToken
						);

						if (detailedPlugin is not null)
						{
							detailedPlugin.ProfileId = profileId;
							progress?.Report(detailedPlugin);
						}
						else
						{
							// 読み込み失敗時は何もしない
						}
					}
					catch
					{
						// エラー時は何もしない
					}
				}
			},
			cancellationToken
		);

		return [.. pluginPacks];
	}

	/// <summary>
	/// ファイル情報のみで基本的なPluginPackを作成
	/// </summary>
	static PluginPack CreateBasicPluginPack(FileInfo dllFile)
	{
		// 厳密な有効/無効判定
		var fileName = dllFile.Name;
		var isEnabled =
			fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
			&& !fileName.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase)
			&& !fileName.EndsWith(".dll.disabled.disabled", StringComparison.OrdinalIgnoreCase);

		return new PluginPack
		{
			Name = dllFile.Name,
			Author = string.Empty,
			Version = null,
			InstalledPath = dllFile.FullName,
			FolderName = Path.GetFileName(Path.GetDirectoryName(dllFile.FullName) ?? string.Empty),
			LastWriteTimeUtc = dllFile.LastWriteTimeUtc,
			IsEnabled = isEnabled,
		};
	}

	/// <summary>
	/// プラグインの詳細情報を非同期で取得
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.TryLoadPlugin(String, String, PluginLoadContext)"
	)]
	static async ValueTask<PluginPack?> LoadPluginDetailAsync(
		FileInfo dllFile,
		PluginLoadContext pluginContext,
		CancellationToken ct
	)
	{
		try
		{
			if (!await IsDotNetAssemblyByHeaderAsync(dllFile.FullName).ConfigureAwait(false))
				return null;

			if (IsExcludedDll(dllFile.Name))
				return null;

			var pluginDir = Path.GetDirectoryName(dllFile.FullName);
			if (pluginDir is null)
				return null;

			var plugin = TryLoadPlugin(dllFile.FullName, pluginDir, pluginContext);
			return plugin;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// プラグインの詳細情報を同期で取得（並行処理での安全性確保）
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.TryLoadPlugin(String, String, PluginLoadContext)"
	)]
	static PluginPack? LoadPluginDetailSync(
		FileInfo dllFile,
		PluginLoadContext pluginContext,
		CancellationToken ct
	)
	{
		try
		{
			ct.ThrowIfCancellationRequested();

			if (!IsDotNetAssemblyByHeaderSync(dllFile.FullName))
				return null;

			if (IsExcludedDll(dllFile.Name))
				return null;

			var pluginDir = Path.GetDirectoryName(dllFile.FullName);
			if (pluginDir is null)
				return null;

			var plugin = TryLoadPlugin(dllFile.FullName, pluginDir, pluginContext);
			return plugin;
		}
		catch
		{
			return null;
		}
	}

	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.ProcessPluginCandidateAsync(FileInfo, ConcurrentBag<PluginPack>, PluginLoadContext, CancellationToken)"
	)]
	static async ValueTask ProcessPluginDirectoryAsync(
		DirectoryInfo dir,
		ConcurrentBag<PluginPack> pluginPacks,
		PluginLoadContext pluginContext,
		int maxPluginsPerFolder,
		CancellationToken ct = default
	)
	{
		if (!dir.Exists)
			return;

		var candidates = await GetPluginCandidatesAsync(dir).ConfigureAwait(false);
		if (candidates is [])
			return;

		// DLL最大数制限
		var limitedCandidates = candidates.Take(maxPluginsPerFolder);

		foreach (var dllFile in limitedCandidates)
		{
			if (ct.IsCancellationRequested)
				break;

			if (!dllFile.Exists)
				continue;

			await ProcessPluginCandidateAsync(dllFile, pluginPacks, pluginContext, ct)
				.ConfigureAwait(false);
		}
	}

	[RequiresUnreferencedCode(
		"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
	)]
	static async ValueTask ProcessPluginDirectoryProgressivelyAsync(
		DirectoryInfo dir,
		ConcurrentBag<PluginPack> pluginPacks,
		PluginLoadContext pluginContext,
		int maxPluginsPerFolder,
		IProgress<PluginPack>? progress,
		CancellationToken ct = default
	)
	{
		if (!dir.Exists || ct.IsCancellationRequested)
			return;

		var candidates = await GetPluginCandidatesAsync(dir).ConfigureAwait(false);
		if (candidates is [])
			return;

		var limitedCandidates = candidates.Take(maxPluginsPerFolder);

		foreach (var dllFile in limitedCandidates)
		{
			if (ct.IsCancellationRequested)
				break;

			if (!dllFile.Exists)
				continue;

			// 段階1: 基本情報で即座に表示用オブジェクト作成
			var basicPlugin = CreateBasicPluginPack(dllFile);
			pluginPacks.Add(basicPlugin);
			progress?.Report(basicPlugin);

			// 段階2: 詳細情報を非同期で取得・更新（Task.Runを削除）
			await UpdatePluginDetailAsync(basicPlugin, dllFile, pluginContext, progress, ct)
				.ConfigureAwait(false);
		}
	}

	[RequiresUnreferencedCode(
		"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
	)]
	static async ValueTask ProcessPluginCandidateAsync(
		FileInfo dllFile,
		ConcurrentBag<PluginPack> pluginPacks,
		PluginLoadContext pluginContext,
		CancellationToken ct = default
	)
	{
		var dllName = dllFile.Name;
		var dllFullName = dllFile.FullName;

		if (IsExcludedDll(dllName))
			return;

		if (!await IsDotNetAssemblyByHeaderAsync(dllFullName).ConfigureAwait(false))
			return;

		var pluginDir = Path.GetDirectoryName(dllFullName);
		if (string.IsNullOrEmpty(pluginDir))
			return;

		// CPU-boundな処理のためTask.Runは使わない
		var plugin = TryLoadPlugin(dllFullName, pluginDir, pluginContext);
		if (plugin is not null)
		{
			pluginPacks.Add(plugin);
		}
	}

	static async ValueTask<IReadOnlyList<FileInfo>> GetPluginCandidatesAsync(DirectoryInfo dir)
	{
		var primaryDllPath = Path.Combine(dir.FullName, $"{dir.Name}.dll");
		var primaryDllDisabledPath = Path.Combine(dir.FullName, $"{dir.Name}.dll.disabled");

		var result = new List<FileInfo>();

		if (await FileExistsAsync(primaryDllPath).ConfigureAwait(false))
			result.Add(new FileInfo(primaryDllPath));
		if (await FileExistsAsync(primaryDllDisabledPath).ConfigureAwait(false))
			result.Add(new FileInfo(primaryDllDisabledPath));

		if (result.Count > 0)
			return result;

		var topDlls = await Task.Run(
				() =>
					dir.GetFiles("*.dll", SearchOption.TopDirectoryOnly)
						.Concat(dir.GetFiles("*.dll.disabled", SearchOption.TopDirectoryOnly))
			)
			.ConfigureAwait(false);

		// 除外パターンでフィルタリング
		var filteredTopDlls = topDlls.Where(f => !IsExcludedDll(f.Name)).ToArray();
		if (filteredTopDlls is not [])
			return filteredTopDlls;

		var allDlls = await Task.Run(
				() =>
					dir.GetFiles("*.dll", SearchOption.AllDirectories)
						.Concat(dir.GetFiles("*.dll.disabled", SearchOption.AllDirectories))
			)
			.ConfigureAwait(false);

		return [.. allDlls.Where(f => !IsExcludedDll(f.Name))];
	}

	public static bool IsExcludedDll(string dllName)
	{
		dllName = dllName.ToLowerInvariant();
		// DLL名がパターンに一致する場合は除外
		return ExcludeDllPatterns.Contains(dllName, StringComparer.Ordinal)
			|| ExcludeDllPatterns.Any(pattern =>
				dllName.Contains(pattern, StringComparison.Ordinal)
			)
			// 拡張: "_resources"や"test"なども除外
			|| dllName.Contains("_resources", StringComparison.Ordinal)
			|| dllName.Contains("test", StringComparison.Ordinal);
	}

	/// <summary>
	/// プラグインの詳細情報を非同期で更新
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.TryLoadPlugin(String, String, PluginLoadContext)"
	)]
	static async ValueTask UpdatePluginDetailAsync(
		PluginPack pluginPack,
		FileInfo dllFile,
		PluginLoadContext pluginContext,
		IProgress<PluginPack>? progress,
		CancellationToken ct = default
	)
	{
		try
		{
			// .NETアセンブリかチェック
			if (!await IsDotNetAssemblyByHeaderAsync(dllFile.FullName).ConfigureAwait(false))
			{
				// 新しいインスタンスを作成して報告
				var updatedPlugin = pluginPack with
				{
					Name = "（非.NETアセンブリ）",
				};
				progress?.Report(updatedPlugin);
				return;
			}

			// 除外対象かチェック
			if (IsExcludedDll(dllFile.Name))
			{
				var updatedPlugin = pluginPack with { Name = "（除外対象）" };
				progress?.Report(updatedPlugin);
				return;
			}

			ct.ThrowIfCancellationRequested();

			// アセンブリ詳細情報取得
			var detailedPlugin = TryLoadPlugin(
				dllFile.FullName,
				Path.GetDirectoryName(dllFile.FullName)!,
				pluginContext
			);

			if (detailedPlugin is not null)
			{
				// 新しいインスタンスを作成して報告
				var updatedPlugin = pluginPack with
				{
					Name = detailedPlugin.Name,
					Author = detailedPlugin.Author,
					Version = detailedPlugin.Version,
					LastWriteTimeUtc = detailedPlugin.LastWriteTimeUtc,
					IsEnabled = detailedPlugin.IsEnabled,
					ProfileId = pluginPack.ProfileId,
					FolderName = detailedPlugin.FolderName,
					InstalledPath = detailedPlugin.InstalledPath,
				};
				progress?.Report(updatedPlugin);
			}
			else
			{
				var updatedPlugin = pluginPack with { Name = "（読み込み失敗）" };
				progress?.Report(updatedPlugin);
			}
		}
		catch (OperationCanceledException)
		{
			var updatedPlugin = pluginPack with { Name = "（キャンセル済み）" };
			progress?.Report(updatedPlugin);
		}
		catch (Exception ex)
		{
			var updatedPlugin = pluginPack with { Name = $"（エラー: {ex.GetType().Name}）" };
			progress?.Report(updatedPlugin);
		}
	}

	[RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
	static PluginPack? TryLoadPlugin(
		string dllFullName,
		string pluginDir,
		PluginLoadContext pluginContext
	)
	{
		try
		{
			var assembly = pluginContext.LoadPluginAssembly(dllFullName, pluginDir);
			if (assembly is null)
			{
				return null;
			}

			if (!HasPluginInterface(assembly))
			{
				return null;
			}

			var (name, version) = GetAssemblyInfo(assembly, dllFullName);
			var lastWriteTimeUtc = File.GetLastWriteTimeUtc(dllFullName);

			string? author = null;
			var attr = assembly.GetCustomAttributes();
			var detailsAttr = attr.FirstOrDefault(attr =>
				string.Equals(
					attr.GetType().Name,
					"PluginDetailsAttribute",
					StringComparison.Ordinal
				)
			);
			if (detailsAttr is not null)
			{
				var authorNameProp = detailsAttr.GetType().GetProperty("AuthorName");
				if (
					authorNameProp?.GetValue(detailsAttr) is string authorName
					&& !string.IsNullOrWhiteSpace(authorName)
				)
				{
					author = authorName;
				}
			}

			author ??= assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "?";

			var isEnabled =
				dllFullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
				&& !dllFullName.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase);

			return new PluginPack
			{
				Name = name,
				Author = author,
				Version = Version.TryParse(version, out var ver) ? ver : null,
				InstalledPath = dllFullName,
				FolderName = Path.GetFileName(Path.GetDirectoryName(dllFullName) ?? string.Empty),
				LastWriteTimeUtc = lastWriteTimeUtc,
				IsEnabled = isEnabled,
			};
		}
		catch (BadImageFormatException)
		{
			return null;
		}
		catch (FileLoadException)
		{
			return null;
		}
		catch (ReflectionTypeLoadException ex)
		{
			try
			{
				if (HasPluginInterfaceFromTypes(ex.Types.Where(t => t is not null).OfType<Type>()))
				{
					return CreateBasicPluginPack(new FileInfo(dllFullName));
				}
			}
			catch { }

			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		catch
		{
			return null;
		}
	}

	static (string Name, string? Version) GetAssemblyInfo(Assembly assembly, string fallbackPath)
	{
		try
		{
			var asmName = assembly.GetName();

			//名前
			var name =
				assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
				?? assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
				?? asmName.Name
				?? Path.GetFileNameWithoutExtension(fallbackPath);

			// 製品バージョン（FileVersion）優先
			var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
			if (fileVersionAttr is not null && !string.IsNullOrWhiteSpace(fileVersionAttr.Version))
			{
				return (name, fileVersionAttr.Version);
			}

			// 情報バージョン（InformationalVersion）次点
			var infoVersionAttr =
				assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (
				infoVersionAttr is not null
				&& !string.IsNullOrWhiteSpace(infoVersionAttr.InformationalVersion)
			)
			{
				return (name, infoVersionAttr.InformationalVersion);
			}

			// それ以外はAssemblyVersion
			return (name, asmName.Version?.ToString());
		}
		catch
		{
			return (Path.GetFileNameWithoutExtension(fallbackPath), null);
		}
	}

	[RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
	static bool HasPluginInterface(Assembly assembly)
	{
		try
		{
			var types = SafeGetTypes(assembly);
			return HasPluginInterfaceFromTypes(types);
		}
		catch (Exception ex)
		{
			// 型取得での例外を詳細ログ出力
			Debug.WriteLine(
				$"HasPluginInterface例外: {assembly.GetName().Name} - {ex.GetType().Name}: {ex.Message}"
			);
			return false;
		}
	}

	// 型のコレクションからプラグインインターフェースを検索（再利用可能）
	[RequiresUnreferencedCode(
		"Calls YukkuDock.Core.PluginManager.HasYukkuriMovieMakerInterface(Type)"
	)]
	static bool HasPluginInterfaceFromTypes(IEnumerable<Type> types)
	{
		try
		{
			foreach (var type in types)
			{
				if (!type.IsClass || type.IsAbstract)
					continue;

				// IPluginインターフェースチェック
				if (
					type.GetInterfaces()
						.Any(i => string.Equals(i.Name, "IPlugin", StringComparison.Ordinal))
				)
				{
					return true;
				}

				// YukkuriMovieMakerインターフェースチェック
				if (HasYukkuriMovieMakerInterface(type))
					return true;
			}
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"HasPluginInterfaceFromTypes例外: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	[RequiresUnreferencedCode(
		"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
	)]
	static bool HasYukkuriMovieMakerInterface(Type type)
	{
		try
		{
			var interfaces = type.GetInterfaces();
			return Array.Exists(
				interfaces,
				i => i.FullName?.StartsWith("YukkuriMovieMaker.", StringComparison.Ordinal) == true
			);
		}
		catch
		{
			return false;
		}
	}

	[RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
	static IEnumerable<Type> SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.DefinedTypes;
		}
		catch (ReflectionTypeLoadException ex)
		{
			// 部分的に読み込める型を返す
			return ex.Types.Where(t => t is not null).OfType<Type>();
		}
		catch
		{
			return [];
		}
	}

	[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "<保留中>")]
	static async ValueTask<bool> IsDotNetAssemblyByHeaderAsync(string dllPath)
	{
		try
		{
			// ファイルI/O操作なので非同期化は妥当
			await using var fs = new FileStream(
				dllPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite,
				bufferSize: 4096,
				useAsync: true
			);
			using var br = new BinaryReader(fs);

			// MZヘッダ判定 - 早期リターンを活用
			if (br.ReadUInt16() != 0x5A4D)
				return false;

			fs.Seek(0x3C, SeekOrigin.Begin);
			var peHeaderOffset = br.ReadInt32();

			if (peHeaderOffset <= 0 || peHeaderOffset >= fs.Length)
				return false;

			fs.Seek(peHeaderOffset, SeekOrigin.Begin);
			if (br.ReadUInt32() != 0x00004550)
				return false;

			fs.Seek(20, SeekOrigin.Current);
			var magic = br.ReadUInt16();

			if (magic != 0x10B && magic != 0x20B)
				return false;

			var optionalHeaderSize = magic == 0x20B ? 222 : 206;

			if (fs.Position + optionalHeaderSize >= fs.Length)
				return false;

			fs.Seek(optionalHeaderSize, SeekOrigin.Current);
			var clrHeaderRva = br.ReadUInt32();

			return clrHeaderRva != 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 同期版.NETアセンブリヘッダーチェック（並行処理での安全性確保）
	/// </summary>
	static bool IsDotNetAssemblyByHeaderSync(string dllPath)
	{
		try
		{
			using var fs = new FileStream(
				dllPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite,
				bufferSize: 4096
			);
			using var br = new BinaryReader(fs);

			// MZヘッダ判定
			if (br.ReadUInt16() != 0x5A4D)
				return false;

			fs.Seek(0x3C, SeekOrigin.Begin);
			var peHeaderOffset = br.ReadInt32();

			if (peHeaderOffset <= 0 || peHeaderOffset >= fs.Length)
				return false;

			fs.Seek(peHeaderOffset, SeekOrigin.Begin);
			if (br.ReadUInt32() != 0x00004550)
				return false;

			fs.Seek(20, SeekOrigin.Current);
			var magic = br.ReadUInt16();

			if (magic != 0x10B && magic != 0x20B)
				return false;

			var optionalHeaderSize = magic == 0x20B ? 222 : 206;

			if (fs.Position + optionalHeaderSize >= fs.Length)
				return false;

			fs.Seek(optionalHeaderSize, SeekOrigin.Current);
			var clrHeaderRva = br.ReadUInt32();

			return clrHeaderRva != 0;
		}
		catch
		{
			return false;
		}
	}

	static async ValueTask<bool> FileExistsAsync(string path)
	{
		// ファイルの存在確認は非同期I/O操作として適切
		return await Task.Run(() => File.Exists(path)).ConfigureAwait(false);
	}

	/// <summary>
	/// AssemblyLoadContextの管理を担当するクラス
	/// </summary>
	[RequiresUnreferencedCode(
		"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
	)]
	internal sealed class PluginLoadContext : IDisposable
	{
		readonly AssemblyLoadContext _loadContext;
		readonly string _appDir;
		readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new(
			StringComparer.Ordinal
		);
		readonly SemaphoreSlim _loadSemaphore = new(1, 1);
		readonly object _syncLock = new();
		volatile bool _disposed;

		public PluginLoadContext(string appDir)
		{
			_appDir = appDir;
			_loadContext = new AssemblyLoadContext("SharedPluginInspect", isCollectible: true);
			_loadContext.Resolving += OnResolving;
		}

		Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
		{
			if (
				_disposed
				|| assemblyName.Name is null
				|| PluginManager.IsExcludedDll(assemblyName.Name)
			)
				return null;

			if (_loadedAssemblies.TryGetValue(assemblyName.Name, out var loadedAssembly))
				return loadedAssembly;

			lock (_syncLock)
			{
				if (
					_disposed
					|| _loadedAssemblies.TryGetValue(assemblyName.Name, out loadedAssembly)
				)
					return loadedAssembly;

				var assemblyPath = Path.Combine(_appDir, $"{assemblyName.Name}.dll");
				if (File.Exists(assemblyPath) && IsDotNetAssemblyByHeaderSync(assemblyPath))
				{
					try
					{
						var assembly = _loadContext.LoadFromAssemblyPath(assemblyPath);
						_loadedAssemblies.TryAdd(assemblyName.Name, assembly);
						return assembly;
					}
					catch
					{
						//
					}
				}

				return null;
			}
		}

		public Assembly? LoadPluginAssembly(string dllPath, string pluginDir)
		{
			if (_disposed)
			{
				return null;
			}

			try
			{
				var assemblyName = Path.GetFileNameWithoutExtension(dllPath);

				if (_loadedAssemblies.TryGetValue(assemblyName, out var existingAssembly))
				{
					return existingAssembly;
				}

				_loadSemaphore.Wait();
				try
				{
					if (_disposed)
					{
						return null;
					}

					if (_loadedAssemblies.TryGetValue(assemblyName, out existingAssembly))
					{
						return existingAssembly;
					}

					var assembly = _loadContext.LoadFromAssemblyPath(dllPath);
					_loadedAssemblies.TryAdd(assemblyName, assembly);

					return assembly;
				}
				finally
				{
					if (!_disposed)
						_loadSemaphore.Release();
				}
			}
			catch (ObjectDisposedException)
			{
				return null;
			}
			catch
			{
				return null;
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			lock (_syncLock)
			{
				if (_disposed)
					return;

				_disposed = true;
			}

			_loadContext.Resolving -= OnResolving;
			_loadedAssemblies.Clear();
			_loadSemaphore.Dispose();

			if (_loadContext.IsCollectible)
			{
				_loadContext.Unload();
			}
		}
	}
}

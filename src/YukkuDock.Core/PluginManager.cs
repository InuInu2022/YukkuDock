using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using YukkuDock.Core.Models;

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
	public static async ValueTask<ICollection<PluginPack>> LoadPluginsProgressivelyAsync(
		string appPath,
		DirectoryInfo folder,
		int maxPluginsPerFolder,
		IProgress<PluginPack>? progress = null,
		CancellationToken cancellationToken = default
	)
	{
		var pluginDirs = folder.GetDirectories().Where(x => x.Exists).ToArray();
		var appDir = Path.GetDirectoryName(appPath)!;
		var pluginPacks = new ConcurrentBag<PluginPack>();

		using var pluginContext = new PluginLoadContext(appDir);

		foreach (var dir in pluginDirs)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			var candidates = await GetPluginCandidatesAsync(dir).ConfigureAwait(false);
			var limitedCandidates = candidates.Take(maxPluginsPerFolder);

			foreach (var dllFile in limitedCandidates)
			{
				if (cancellationToken.IsCancellationRequested)
					break;

				if (!dllFile.Exists)
					continue;

				// 段階1: 基本情報のみで即座に表示
				var basicPlugin = CreateBasicPluginPack(dllFile);
				pluginPacks.Add(basicPlugin);
				progress?.Report(basicPlugin);

				// 段階2: 詳細情報も逐次awaitで取得
				var detailedPlugin = await LoadPluginDetailAsync(
						dllFile,
						pluginContext,
						cancellationToken
					)
					.ConfigureAwait(false);

				if (detailedPlugin is not null)
				{
					progress?.Report(detailedPlugin);
				}
			}
		}

		return [.. pluginPacks];
	}

	/// <summary>
	/// ファイル情報のみで基本的なPluginPackを作成
	/// </summary>
	static PluginPack CreateBasicPluginPack(FileInfo dllFile)
	{
		return new PluginPack
		{
			Name = dllFile.Name,
			Author = string.Empty,
			Version = null,
			InstalledPath = dllFile.FullName,
			FolderName = Path.GetFileName(Path.GetDirectoryName(dllFile.FullName) ?? string.Empty),
			LastWriteTimeUtc = dllFile.LastWriteTimeUtc,
			IsEnabled = true,
		};
	}

	/// <summary>
	/// プラグインの詳細情報を非同期で取得
	/// </summary>
	[RequiresUnreferencedCode("Calls YukkuDock.Core.PluginManager.TryLoadPlugin(String, String, PluginLoadContext)")]

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

		if (await FileExistsAsync(primaryDllPath).ConfigureAwait(false))
			return [new FileInfo(primaryDllPath)];

		var topDlls = await Task.Run(() => dir.GetFiles("*.dll", SearchOption.TopDirectoryOnly))
			.ConfigureAwait(false);

		// 除外パターンでフィルタリング
		var filteredTopDlls = topDlls.Where(f => !IsExcludedDll(f.Name)).ToArray();
		if (filteredTopDlls is not [])
			return filteredTopDlls;

		var allDlls = await Task.Run(() => dir.GetFiles("*.dll", SearchOption.AllDirectories))
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
				return null;

			if (!HasPluginInterface(assembly))
				return null;

			var (name, version) = GetAssemblyInfo(assembly, dllFullName);

			var lastWriteTimeUtc = File.GetLastWriteTimeUtc(dllFullName);

			// PluginDetailsAttributeのAuthorNameを優先して取得
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

			// フォールバック: 標準属性
			author ??= assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "?";

			return new PluginPack
			{
				Name = name,
				Author = author,
				Version = Version.TryParse(version, out var ver) ? ver : null,
				InstalledPath = dllFullName,
				FolderName = Path.GetFileName(Path.GetDirectoryName(dllFullName) ?? string.Empty),
				LastWriteTimeUtc = lastWriteTimeUtc,
			};
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
			foreach (var type in types)
			{
				if (!type.IsClass || type.IsAbstract)
					continue;

				if (
					type.GetInterfaces()
						.Any(i => string.Equals(i.Name, "IPlugin", StringComparison.Ordinal))
				)
				{
					return true;
				}

				if (HasYukkuriMovieMakerInterface(type))
					return true;
			}
			return false;
		}
		catch
		{
			// すべての例外を無視
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
		readonly object _syncLock = new();

		public PluginLoadContext(string appDir)
		{
			_appDir = appDir;
			_loadContext = new AssemblyLoadContext("SharedPluginInspect", isCollectible: true);
			_loadContext.Resolving += OnResolving;
		}

		[RequiresUnreferencedCode(
			"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
		)]
		Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
		{
			if (assemblyName.Name is null || PluginManager.IsExcludedDll(assemblyName.Name))
				return null;

			// すでにロード済みのアセンブリを返す
			if (_loadedAssemblies.TryGetValue(assemblyName.Name, out var loadedAssembly))
				return loadedAssembly;

			// アプリディレクトリから読み込み試行
			var assemblyPath = Path.Combine(_appDir, $"{assemblyName.Name}.dll");
			if (File.Exists(assemblyPath) && IsDotNetAssemblyByHeader(assemblyPath))
			{
				try
				{
					var assembly = _loadContext.LoadFromAssemblyPath(assemblyPath);
					_loadedAssemblies.TryAdd(assemblyName.Name, assembly);
					return assembly;
				}
				catch
				{
					// 読み込み失敗時は無視
				}
			}

			return null;
		}

		[RequiresUnreferencedCode(
			"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
		)]
		public Assembly? LoadPluginAssembly(string dllPath, string pluginDir)
		{
			try
			{
				var assemblyName = Path.GetFileNameWithoutExtension(dllPath);

				// 同じアセンブリが既にロードされていたら再利用
				if (_loadedAssemblies.TryGetValue(assemblyName, out var existingAssembly))
					return existingAssembly;

				// 並列アクセスによる重複ロードを防止
				lock (_syncLock)
				{
					if (_loadedAssemblies.TryGetValue(assemblyName, out existingAssembly))
						return existingAssembly;

					// 新規アセンブリをロード
					var assembly = _loadContext.LoadFromAssemblyPath(dllPath);
					_loadedAssemblies.TryAdd(assemblyName, assembly);
					return assembly;
				}
			}
			catch
			{
				return null;
			}
		}

		// 同期版のIsDotNetAssemblyByHeaderを追加（コールバックでの利用に適切）
		static bool IsDotNetAssemblyByHeader(string dllPath)
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

		public void Dispose()
		{
			_loadContext.Resolving -= OnResolving;
			_loadedAssemblies.Clear();

			if (_loadContext.IsCollectible)
			{
				_loadContext.Unload();
			}
		}
	}
}

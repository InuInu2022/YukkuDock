using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using YukkuDock.Core.Models;

namespace YukkuDock.Core;

public static class PluginManager
{
	// ImmutableHashSetからFrozenSetへ変更
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
		var pluginDirs = folder.GetDirectories().Where(x => x.Exists).ToArray();
		var appDir = Path.GetDirectoryName(appPath)!;
		var pluginPacks = new ConcurrentBag<PluginPack>();

		using var pluginContext = new PluginLoadContext(appDir);

		// 並列度をI/O主体ならコア数×2に増やす
		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount * 2
		};

		await Parallel
			.ForEachAsync(
				pluginDirs,
				parallelOptions,
				async (dir, ct) =>
					await ProcessPluginDirectoryAsync(dir, pluginPacks, pluginContext, ct)
						.ConfigureAwait(false)
			)
			.ConfigureAwait(false);

		return [.. pluginPacks];
	}

	[RequiresUnreferencedCode(
		"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
	)]
	static async ValueTask ProcessPluginDirectoryAsync(
		DirectoryInfo dir,
		ConcurrentBag<PluginPack> pluginPacks,
		PluginLoadContext pluginContext,
		CancellationToken ct = default
	)
	{
		if (!dir.Exists)
			return;

		var candidates = await GetPluginCandidatesAsync(dir).ConfigureAwait(false);
		if (candidates is [])
			return;

		// Spanを使用せず、直接コレクションを反復処理
		foreach (var dllFile in candidates)
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

		// ファイルの存在確認は非同期I/O操作として適切
		if (await FileExistsAsync(primaryDllPath).ConfigureAwait(false))
			return [new FileInfo(primaryDllPath)];

		// ディレクトリ走査はI/O-boundなのでTask.Runは適切
		var topDlls = await Task.Run(() => dir.GetFiles("*.dll", SearchOption.TopDirectoryOnly))
			.ConfigureAwait(false);

		if (topDlls is not [])
			return topDlls;

		// サブディレクトリも含めて検索（I/O-boundなのでTask.Runは適切）
		var allDlls = await Task.Run(() => dir.GetFiles("*.dll", SearchOption.AllDirectories))
			.ConfigureAwait(false);
		return allDlls;
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

	[RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
	static PluginPack? TryLoadPlugin(
		string dllFullName,
		string pluginDir,
		PluginLoadContext pluginContext
	)
	{
		try
		{
			// CPU-boundな処理なのでTask.Runは不要
			var assembly = pluginContext.LoadPluginAssembly(dllFullName, pluginDir);
			if (assembly is null)
				return null;

			// プラグインでない場合は早期リターン
			if (!HasPluginInterface(assembly))
				return null;

			// 成功パスでのみアセンブリ情報を取得
			var (name, version) = GetAssemblyInfo(assembly, dllFullName);

			return new PluginPack
			{
				Name = name,
				Version = version,
				InstalledPath = dllFullName,
				FolderName = Path.GetFileName(Path.GetDirectoryName(dllFullName) ?? string.Empty),
			};
		}
		catch
		{
			return null;
		}
	}

	static (string Name, Version? Version) GetAssemblyInfo(Assembly assembly, string fallbackPath)
	{
		try
		{
			var asmName = assembly.GetName();
			return (
				asmName.Name ?? Path.GetFileNameWithoutExtension(fallbackPath),
				asmName.Version
			);
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
			// リフレクション処理はCPU-boundだがTask.Runは不要
			var types = SafeGetTypes(assembly);
			foreach (var type in types)
			{
				if (!type.IsClass || type.IsAbstract)
					continue;

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
			return assembly.GetTypes();
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
}

// AssemblyLoadContextの管理を担当するクラス
[RequiresUnreferencedCode(
	"Plugin loading uses reflection and AssemblyLoadContext, which is unsafe for trimming."
)]
internal sealed class PluginLoadContext : IDisposable
{
	readonly AssemblyLoadContext _loadContext;
	readonly string _appDir;
	readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new(StringComparer.Ordinal);
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

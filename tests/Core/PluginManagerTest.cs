using YukkuDock.Core;
using YukkuDock.Core.Models;

namespace Core;

/// <summary>
/// PluginManagerのプラグイン有効/無効切り替えロジックのテスト。
/// 一時ファイルのクリーンアップはDisposeで一括管理する。
/// </summary>
public class PluginManagerTest : IDisposable
{
	// テストで生成した一時ファイルのパスを記録
	readonly List<string> tempFiles = [];

	static string CreateTempPluginFile(string fileName, List<string> tempFiles)
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(tempDir);
		var filePath = Path.Combine(tempDir, fileName);
		File.WriteAllText(filePath, "dummy");
		tempFiles.Add(filePath);
		return filePath;
	}

	void CleanupTempFiles()
	{
		foreach (var filePath in tempFiles)
		{
			try
			{
				var dir = Path.GetDirectoryName(filePath);
				if (File.Exists(filePath))
					File.Delete(filePath);
				if (Directory.Exists(dir))
					Directory.Delete(dir, true);
			}
			catch { }
		}
		tempFiles.Clear();
	}

	public void Dispose()
	{
		CleanupTempFiles();
	}

	[Fact]
	// 存在しないファイルの場合、falseを返すことを検証するテスト
	public async Task ReturnsFalseIfFileNotFound()
	{
		var plugin = new PluginPack { InstalledPath = "nonexistent.dll" };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, true);
		Assert.False(result.Success);
	}

	[Theory]
	// `*.dll`ではないパスが渡された場合にfalseを返すことを検証
	[InlineData("test.txt")]
	[InlineData("test.doc")]
	[InlineData("test.pdf")]
	public async Task ReturnsFalseIfNotDll(string fileName)
	{
		var filePath = CreateTempPluginFile(fileName, tempFiles);
		var plugin = new PluginPack { InstalledPath = filePath };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, true);
		Assert.False(result.Success);
	}

	[Fact]
	// プラグインを有効化するテスト（無効化されたDLL）
	public async Task EnablesPlugin_DisabledDll()
	{
		var filePath = CreateTempPluginFile("test.dll.disabled", tempFiles);
		var plugin = new PluginPack { InstalledPath = filePath };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, true);
		Assert.True(result.Success);
		Assert.True(File.Exists(filePath.Replace(".dll.disabled", ".dll")));
		tempFiles.Add(filePath.Replace(".dll.disabled", ".dll"));
	}

	[Fact]
	// プラグインを無効化するテスト（有効化されたDLL）
	public async Task DisablesPlugin_EnabledDll()
	{
		var filePath = CreateTempPluginFile("test.dll", tempFiles);
		var plugin = new PluginPack { InstalledPath = filePath };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, false);
		Assert.True(result.Success);
		Assert.True(File.Exists(filePath + ".disabled"));
		tempFiles.Add(filePath + ".disabled");
	}

	[Fact]
	// 既に有効な場合、何も行わないことを検証するテスト
	public async Task NoActionIfAlreadyEnabled()
	{
		var filePath = CreateTempPluginFile("test.dll", tempFiles);
		var plugin = new PluginPack { InstalledPath = filePath };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, true);
		Assert.True(result.Success);
		Assert.True(File.Exists(filePath));
	}

	[Fact]
	// 既に無効な場合、何も行わないことを検証するテスト
	public async Task NoActionIfAlreadyDisabled()
	{
		var filePath = CreateTempPluginFile("test.dll.disabled", tempFiles);
		var plugin = new PluginPack { InstalledPath = filePath };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, false);
		Assert.True(result.Success);
		Assert.True(File.Exists(filePath));
	}

	[Fact]
	// 二重に無効化された拡張子を修正するテスト
	public async Task CorrectsDoubleDisabledExtension()
	{
		var filePath = CreateTempPluginFile("test.dll.disabled.disabled", tempFiles);
		var plugin = new PluginPack { InstalledPath = filePath };
		var result = await PluginManager.TryChangeStatusPluginAsync(plugin, false);
		var correctedPath = filePath.Replace(".dll.disabled.disabled", ".dll.disabled");
		Assert.True(result.Success);
		Assert.True(File.Exists(correctedPath));
		tempFiles.Add(correctedPath);
	}
}

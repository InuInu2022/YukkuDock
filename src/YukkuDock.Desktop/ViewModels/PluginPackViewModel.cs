using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading; // 追加
using Epoxy;
using YukkuDock.Core;
using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public class PluginPackViewModel : IDisposable
{
	private readonly PluginPack _pack;

	public PluginPack PluginPack => _pack;

	public string Name { get; set; }
	public string Author { get; set; }
	public Version? Version { get; set; }
	public string InstalledPath { get; set; }
	public string FolderName { get; set; }
	public DateTime LastWriteTimeUtc { get; set; }
	public bool IsIgnoredBackup { get; set; }
	public bool IsEnabled { get; set; }
	public string LastWriteTimeText
		=> LastWriteTimeUtc.ToLocalTime().ToString("u");


	readonly SemaphoreSlim isEnabledSemaphore = new(1, 1);
	bool suppressIsEnabledChanged; // 再入・無限ループ防止
	bool _disposedValue;

	public PluginPackViewModel(PluginPack pack)
	{
		_pack = pack;
		Name = pack.Name;
		Author = pack.Author;
		Version = pack.Version;
		InstalledPath = pack.InstalledPath;
		FolderName = pack.FolderName;
		LastWriteTimeUtc = pack.LastWriteTimeUtc;
		IsEnabled = pack.IsEnabled;
		IsIgnoredBackup = pack.IsIgnoredBackup;
	}

	// 詳細情報でプロパティを更新
	public void UpdateFromPluginPack(PluginPack pack)
	{
		Name = pack.Name;
		Author = pack.Author;
		Version = pack.Version;
		InstalledPath = pack.InstalledPath;
		LastWriteTimeUtc = pack.LastWriteTimeUtc;
		IsEnabled = pack.IsEnabled;
		IsIgnoredBackup = pack.IsIgnoredBackup;
	}




	[PropertyChanged(nameof(IsEnabled))]
	[SuppressMessage("", "IDE0051")]
	async ValueTask IsEnabledChangedAsync(bool value)
	{
		// プログラム補正時はスキップ
		if (suppressIsEnabledChanged)
		{
			return;
		}

		await isEnabledSemaphore.WaitAsync().ConfigureAwait(true);
		try
		{
			// 実ファイル切替（PluginManager 側で実在ファイルを解決するのでUIのパスが古くても通る）
			var result = await PluginManager
				.TryChangeStatusPluginAsync(_pack, value)
				.ConfigureAwait(true);

			if (result.Success && result.Value is string newPath && !string.IsNullOrWhiteSpace(newPath))
			{
				// 成功時：モデルとUIのパス・状態を同期（再入抑止）
				suppressIsEnabledChanged = true;
				try
				{
					_pack.InstalledPath = newPath;   // モデルのパスを最新化
					InstalledPath = newPath;         // VMの表示用も更新
					IsEnabled = value;               // UIは結果で確定
				}
				finally
				{
					suppressIsEnabledChanged = false;
				}
			}
			else
			{
				// 失敗時：UIを元に戻す（再入抑止）
				suppressIsEnabledChanged = true;
				try
				{
					IsEnabled = !value;
				}
				finally
				{
					suppressIsEnabledChanged = false;
				}
				Debug.WriteLine(result.Exception?.Message);
			}
		}
		finally
		{
			isEnabledSemaphore.Release();
		}

		return;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				// マネージド状態を破棄します (マネージド オブジェクト)
				isEnabledSemaphore?.Dispose();
			}

			// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
			// TODO: 大きなフィールドを null に設定します
			_disposedValue = true;
		}
	}

	// // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
	// ~PluginPackViewModel()
	// {
	//     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

}

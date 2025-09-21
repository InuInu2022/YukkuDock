using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using YukkuDock.Core.Models;

namespace YukkuDock.Core.Services;

/// <summary>
/// アプリ全体の設定管理サービスのインターフェース
/// </summary>
public interface ISettingsService
{
	/// <summary>
	/// 設定ファイルの安全な読込。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<Settings>> TryLoadAsync();

	/// <summary>
	/// 設定ファイルの安全な保存。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<bool>> TrySaveAsync(Settings settings);
}

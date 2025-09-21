using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using YukkuDock.Core.Models;

namespace YukkuDock.Core.Services;

/// <summary>
/// プロファイル管理サービスのインターフェース
/// </summary>
public interface IProfileService
{
	string GetProfileFolder(Guid id);
	/// <summary>
	/// プロファイルの安全な読込。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<Profile>> TryLoadAsync(Guid id);

	/// <summary>
	/// 全プロファイルの安全な読込。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<IReadOnlyList<Profile>>> TryLoadAllAsync();

	/// <summary>
	/// プロファイルの安全な保存。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<bool>> TrySaveAsync(Profile profile);

	/// <summary>
	/// プロファイルの安全な削除。ゴミ箱に移動します。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<bool>> TryDeleteAsync(Profile profile);
}

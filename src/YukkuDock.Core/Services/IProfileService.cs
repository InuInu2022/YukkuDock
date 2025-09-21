using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using YukkuDock.Core.Models;

namespace YukkuDock.Core.Services;

/// <summary>
/// プロファイル管理サービスのインターフェース
/// </summary>
public interface IProfileService
{
	/// <summary>
	/// プロファイルの安全な読込。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<Profile>> TryLoadAsync(string profileFolderPath);

	/// <summary>
	/// プロファイルの安全な保存。失敗時はSuccess=false。
	/// </summary>
	Task<TryAsyncResult<bool>> TrySaveAsync(Profile profile, string profileFolderPath);
}

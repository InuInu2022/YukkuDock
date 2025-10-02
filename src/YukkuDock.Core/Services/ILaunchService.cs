namespace YukkuDock.Core.Services;

public interface ILaunchService
{
	/// <summary>
	/// アプリケーションを起動する
	/// </summary>
	/// <param name="appPath"></param>
	/// <returns>成功失敗　</returns>/
	public bool TryLaunch(
		string appPath
	);

	/// <summary>
	/// アプリケーションを起動し、アイドル状態になるまで待つ
	/// </summary>
	/// <param name="appPath"></param>
	/// <param name="millisecondsDelay"></param>
	/// <returns></returns>
	ValueTask<bool> TryLaunchWaitAsync(
		string appPath,
		int millisecondsDelay = 5000
	);
}

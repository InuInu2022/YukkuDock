using System.Diagnostics;

using YukkuDock.Core.Services;

using YukkuDock.Desktop.ViewModels;

namespace YukkuDock.Desktop.Services;

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
}

public class LaunchService : ILaunchService
{
	public bool TryLaunch(
		string appPath
	)
	{
		if (!File.Exists(appPath))
		{
			return false;
		}

		try
		{
			using var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = appPath,
					UseShellExecute = true
				}
			);
		}
		catch (Exception ex)
		{
			Debug.WriteLine(ex.Message);
			return false;
		}
		return true;
	}
}

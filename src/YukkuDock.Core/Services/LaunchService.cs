using System.Diagnostics;


namespace YukkuDock.Core.Services;

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
					UseShellExecute = true,
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

	public async ValueTask<bool> TryLaunchWaitAsync(
		string appPath,
		int millisecondsDelay = 5000
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
					UseShellExecute = true,
				}
			);
			if (process is null)
			{
				return false;
			}
			int count = millisecondsDelay;
			while (process.MainWindowHandle == 0)
			{
				await Task.Delay(100)
					.ConfigureAwait(false);

				//もしmillisecondsDelayを超えたら抜ける
				if (count <= 0)
				{
					break;
				}
				count -= 100;
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine(ex.Message);
			return false;
		}
		return true;
	}
}

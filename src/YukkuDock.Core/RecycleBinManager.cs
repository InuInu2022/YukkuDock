using YukkuDock.Core.Services;

namespace YukkuDock.Core;

public static class RecycleBinManager
{

	public static async Task<TryAsyncResult<bool>> TryMoveAsync(string path, CancellationToken cancellationToken = default)
	{
		var result = await Emik.Rubbish
			.MoveAsync(path, cancellationToken)
			.ConfigureAwait(false);
		return new(result, result);
	}
}

using Epoxy;

namespace YukkuDock.Desktop.Extensions;

public static class EasyCommandFactoryExtension
{
	/// <summary>
	/// <see cref="Epoxy"/> の<see cref="Command.Factory.Create()"/>をラップし、実行中はボタン等が無効になる便利コマンド生成
	/// </summary>
	public static Command CreateEasy(
		this CommandFactoryInstance factory,
		Func<CancellationToken, ValueTask> execute,
		Action<Exception>? onError = null,
		CancellationToken cancellationToken = default
	)
	{
		Command? cmd = null;
		var isRunning = false;

		cmd = factory.Create(
			async () =>
			{
				if (isRunning)
					return;

				isRunning = true;
				cmd?.ChangeCanExecute();

				try
				{
					await execute(cancellationToken).ConfigureAwait(true);
				}
				catch (Exception ex)
				{
					onError?.Invoke(ex);
				}
				finally
				{
					isRunning = false;
					cmd?.ChangeCanExecute();
				}
			},
			() => !isRunning
		);

		return cmd;
	}

	public static Command CreateBasic<T>(
		this CommandFactoryInstance factory,
		Func<T, CancellationToken, ValueTask> execute,
		Action<Exception>? onError = null,
		CancellationToken cancellationToken = default
	)
	{
		Command? cmd = null;
		var isRunning = false;

		cmd = factory.Create<T>(
			async param =>
			{
				if (isRunning)
					return;

				isRunning = true;
				cmd?.ChangeCanExecute();

				try
				{
					await execute(param, cancellationToken).ConfigureAwait(true);
				}
				catch (Exception ex)
				{
					onError?.Invoke(ex);
				}
				finally
				{
					isRunning = false;
					cmd?.ChangeCanExecute();
				}
			},
			_ => !isRunning
		);

		return cmd;
	}
}

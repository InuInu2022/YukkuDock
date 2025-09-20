using Epoxy;

namespace YukkuDock.Desktop.Extensions;

public static class EasyCommandFactoryExtension
{
	/// <summary>
	/// <see cref="Epoxy"/> の<see cref="Command.Factory.Create()"/>をラップし、実行中はボタン等が無効になる便利コマンド生成
	/// </summary>
	/// <param name="factory">コマンドファクトリ</param>
	/// <param name="execute">実行する非同期処理</param>
	/// <param name="onError">例外発生時に呼び出される処理</param>
	/// <param name="cancellationToken">キャンセルトークン</param>
	/// <returns>生成されたコマンド</returns>
	public static Command CreateEasy(
		this CommandFactoryInstance factory,
		Func<CancellationToken?, ValueTask> execute,
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

	/// <inheritdoc cref="CreateEasy(CommandFactoryInstance, Func{CancellationToken?, ValueTask}, Action{Exception}?, CancellationToken)"/>
	public static Command CreateEasy(
		this CommandFactoryInstance factory,
		Func<ValueTask> execute,
		Action<Exception>? onError = null,
		CancellationToken cancellationToken = default
	)
	{
		return factory.CreateEasy(
			_ => execute(),
			onError,
			cancellationToken
		);
	}

	/// <inheritdoc cref="CreateEasy(CommandFactoryInstance, Func{CancellationToken?, ValueTask}, Action{Exception}?, CancellationToken)"/>
	/// <typeparam name="T">実行する非同期処理の引数の型</typeparam>
	public static Command CreateEasy<T>(
		this CommandFactoryInstance factory,
		Func<T, CancellationToken?, ValueTask> execute,
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

	public static Command CreateEasy<T>(
		this CommandFactoryInstance factory,
		Func<T, ValueTask> execute,
		Action<Exception>? onError = null,
		CancellationToken cancellationToken = default
	)
	{
		return factory.CreateEasy<T>(
			(param, _) => execute(param),
			onError,
			cancellationToken
		);
	}
}

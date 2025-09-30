using Avalonia.Controls;
using Avalonia.Interactivity;

using Epoxy;

using FluentAvalonia.Core;

using FluentAvalonia.UI.Controls;

namespace YukkuDock.Desktop.Services;

public class DialogService : IDialogService
{
	public async ValueTask ShowErrorAsync<T>(
		Pile<T> ownerPile,
		string title,
		string header,
		object? content = default,
		string subHeader = ""
	)
		where T : Interactive
	{
		var td = new TaskDialog
		{
			Title = title,
			IconSource = new SymbolIconSource { Symbol = Symbol.Important },
			Header = header,
			SubHeader = subHeader,
			Content = content,
			ShowProgressBar = false,
			Buttons = { TaskDialogButton.OKButton },
		};

		await ownerPile
			.RentAsync(owner =>
			{
				td.XamlRoot = TopLevel.GetTopLevel(owner);
				return default;
			})
			.ConfigureAwait(true);

		await td.ShowAsync(true)
			.ConfigureAwait(true);
	}

	public async ValueTask ShowDeferralAsync<T>(
		Pile<T> ownerPile,
		string title,
		string header,
		TypedEventHandler<TaskDialog, TaskDialogClosingEventArgs> deferralEvent,
		object? content = default,
		string subHeader = ""
	)
		where T : Interactive
	{
		var td = new TaskDialog
		{
			Title = title,
			IconSource = new SymbolIconSource { Symbol = Symbol.Important },
			Header = header,
			SubHeader = subHeader,
			Content = content,
			ShowProgressBar = false,
			Buttons = {
				TaskDialogButton.YesButton, TaskDialogButton.NoButton
			},
		};

		td.Closing += deferralEvent;

		await ownerPile
			.RentAsync(owner =>
			{
				td.XamlRoot = TopLevel.GetTopLevel(owner);
				return default;
			})
			.ConfigureAwait(true);

		await td.ShowAsync(true)
			.ConfigureAwait(true);
	}
}

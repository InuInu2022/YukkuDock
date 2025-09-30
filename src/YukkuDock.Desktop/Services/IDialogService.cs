using Avalonia.Controls;
using Avalonia.Interactivity;

using Epoxy;

using FluentAvalonia.Core;

using FluentAvalonia.UI.Controls;

namespace YukkuDock.Desktop.Services;

public interface IDialogService
{
	/// <summary>
	/// エラーダイアログを表示する
	/// </summary>
	/// <typeparam name="T">ダイアログのオーナーになるUIの型。<see cref="Window"/>など。</typeparam>
	/// <param name="ownerPile">オーナーの<see cref="Pile"/> </param>
	/// <param name="title"></param>
	/// <param name="header"></param>
	/// <param name="content"></param>
	/// <param name="subHeader"></param>
	/// <returns></returns>
	ValueTask ShowErrorAsync<T>(
		Pile<T> ownerPile,
		string title,
		string header,
		object? content = default,
		string subHeader = ""
	) where T : Interactive;

	/// <summary>
	/// 保留可能なダイアログを表示する
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="ownerPile"></param>
	/// <param name="title"></param>
	/// <param name="header"></param>
	/// <param name="content"></param>
	/// <param name="deferralEvent">ダイアログの保留イベント</param>
	/// <param name="subHeader"></param>
	/// <returns></returns>
	ValueTask ShowDeferralAsync<T>(
		Pile<T> ownerPile,
		string title,
		string header,
		TypedEventHandler<TaskDialog, TaskDialogClosingEventArgs> deferralEvent,
		object? content = default,
		string subHeader = ""
	) where T : Interactive;
}

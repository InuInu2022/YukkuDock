using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace YukkuDock.Desktop.ViewModels;

public record PageItem(
	string Title,
	Symbol Icon,
	Control Content,
	bool IsEnabled = true
);

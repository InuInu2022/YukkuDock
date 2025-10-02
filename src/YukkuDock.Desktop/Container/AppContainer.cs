using Jab;
using YukkuDock.Core.Services;
using YukkuDock.Desktop.Services;
using YukkuDock.Desktop.ViewModels;

namespace YukkuDock.Desktop.Container;

[ServiceProvider]
[Singleton<ISettingsService, SettingsService>]
[Singleton<IProfileService, ProfileService>]
[Singleton<IDialogService, DialogService>]
[Singleton<ILaunchService, LaunchService>]
[Transient<MainWindowViewModel>]
[Transient<YukkuDock.Core.Models.Profile>]
[Transient<ProfileViewModel>]
[Transient<ProfileWindowViewModel>]
[Transient<PluginPageViewModel>]
public partial class AppContainer
{
    // Jabが自動生成
}

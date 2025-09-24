using YukkuDock.Core;
using YukkuDock.Core.Models;
using YukkuDock.Core.Services;

namespace Core;

public class BackupManagerTest
{

	[Fact]
	public async Task TestBackupProfileAsync()
	{
		// Arrange
		var profileService = new Mock<IProfileService>();
		var profile = new Profile { Id = Guid.NewGuid() };
		profileService.Setup(s => s.GetProfileFolder(profile.Id)).Returns("profile_folder");
		profileService.Setup(s => s.GetProfileBackupFolder(profile.Id)).Returns("backup_folder");

		// Act
		var result = await BackupManager.TryBackupProfileAsync(profileService.Object, profile);

		// Assert
		Assert.True(result.Success);
	}

	[Fact]
	public async Task TestBackupPluginPacksAsync()
	{
		// Arrange
		var profileService = new Mock<IProfileService>();
		var profile = new Profile { Id = Guid.NewGuid() };
		profileService.Setup(s => s.GetPluginPacksBackupFolder(profile.Id)).Returns("plugin_backup_folder");

		// Act
		var result = await BackupManager.TryBackupPluginPacksAsync(profileService.Object, profile);

		// Assert
		Assert.True(result.Success);
	}
}

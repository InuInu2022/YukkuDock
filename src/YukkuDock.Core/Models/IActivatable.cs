namespace YukkuDock.Core.Models;

public interface IActivatable
{
	/// <summary>
	/// 対象が有効かどうかを表します。
	/// 無効の場合、<see cref="MovedPath"/>に移動されます。
	/// </summary>
	bool IsEnabled { get; set; }
	string MovedPath { get; set; }
}

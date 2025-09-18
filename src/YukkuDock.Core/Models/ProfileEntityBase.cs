namespace YukkuDock.Core.Models;

/// <summary>
/// プラグイン、レイアウト、テンプレート、キャラクター等の管理対象のデータの基底クラス
/// </summary>
public abstract record ProfileEntityBase
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>
	/// 所属するプロファイルのIDを表します。
	/// </summary>
	public Guid ProfileId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
}

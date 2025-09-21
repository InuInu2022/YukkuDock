

namespace YukkuDock.Core.Models;

/// <summary>
/// アプリ全体の設定情報を保持するモデル
/// </summary>
public class Settings
{
	/// <summary>
	/// テーマ（例: "Light", "Dark"）
	/// </summary>
	public string Theme { get; set; } = "Light";

	/// <summary>
	/// 最後に開いたプロファイルのID
	/// </summary>
	public Guid? LastOpenedProfileId { get; set; }

	/// <summary>
	/// その他の設定項目（必要に応じて追加）
	/// </summary>
	public string Language { get; set; } = "ja";
}

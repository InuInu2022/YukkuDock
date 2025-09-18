using Epoxy;

namespace YukkuDock.Desktop.Converters;

public sealed class EmptyVersionIfNullConverter : ValueConverter<Version?, string>
{
	public override bool TryConvert(Version? from, out string result)
	{
		// 変換した結果は、out引数で返します。
		result = from?.ToString() ?? string.Empty;
		// 変換に失敗する場合はfalseを返します。
		return true;
	}
}

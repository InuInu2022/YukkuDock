using Epoxy;

namespace YukkuDock.Desktop.Converters;

public sealed class EmptyIfNullConverter :
ValueConverter<string?, string>
{
	public override bool TryConvert(string? from, out string result)
	{
		// 変換した結果は、out引数で返します。
		result = from ??= string.Empty;
		// 変換に失敗する場合はfalseを返します。
		return true;
	}
}

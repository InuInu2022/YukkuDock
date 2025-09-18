using System;
using System.Globalization;
using Avalonia.Data.Converters;

using Epoxy;

namespace YukkuDock.Desktop.Converters;

public sealed class FalseIfNullConverter : ValueConverter<bool?, bool>
{
	public override bool TryConvert(bool? from, out bool result)
	{
		// 変換した結果は、out引数で返します。
		result = from ??= false;
		// 変換に失敗する場合はfalseを返します。
		return true;
	}
}

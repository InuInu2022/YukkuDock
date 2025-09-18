using Avalonia.Controls;

using Epoxy;

namespace YukkuDock.Desktop.Converters;

public sealed class ControlToTypeConverter
	: ValueConverter<Control, Type>
{
	public override bool TryConvert(Control from, out Type result)
	{
		// 変換した結果は、out引数で返します。
		result = from.GetType();
		// 変換に失敗する場合はfalseを返します。
		return true;
	}
}
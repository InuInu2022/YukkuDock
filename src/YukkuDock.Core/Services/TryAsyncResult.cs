using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace YukkuDock.Core.Services;

[StructLayout(LayoutKind.Auto)]
public readonly record struct TryAsyncResult<T>(bool Success, T? Value)
{
	[MemberNotNullWhen(true, nameof(Value))]
	public bool Success { get; } = Success;

	public T? Value { get; } = Value;
}

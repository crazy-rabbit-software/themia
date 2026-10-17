namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6902] A widget module: what the host creates from <c>[assembly: ThemiaWidget(id, typeof(M))]</c>.
/// Its <see cref="Descriptor"/> is the pure half; what it adds is the UI the host asks of it.
/// </summary>
public interface IWidgetModule
{
    WidgetDescriptor Descriptor { get; }
}

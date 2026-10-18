using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services;

/// <summary>[S-6913] The Recycle Bin a widget asks for (<see cref="IWidgetHost.RecycleBin"/>): the host's one <see cref="RecycleBinStatus"/>.</summary>
internal sealed class HostRecycleBin : IRecycleBin
{
    public static HostRecycleBin Instance { get; } = new();

    private HostRecycleBin()
    {
    }

    public RecycleBinState? Current => RecycleBinStatus.Current;

    public bool Emptying => RecycleBinStatus.Emptying;

    public event Action? Changed
    {
        add => RecycleBinStatus.Changed += value;
        remove => RecycleBinStatus.Changed -= value;
    }

    public IDisposable Watch() => RecycleBinStatus.Watch();

    public Task EmptyAsync() => RecycleBinStatus.EmptyAsync();
}

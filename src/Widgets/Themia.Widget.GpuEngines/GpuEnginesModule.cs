using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.GpuEngines;

[assembly: ThemiaWidget("gpuEngines", typeof(GpuEnginesModule))]

namespace Themia.Widget.GpuEngines;

/// <summary>[S-11015] One graphics adapter's engines: a row per engine type with its load. SPEC.md beside this file.</summary>
public sealed class GpuEnginesModule : IWidgetModule
{
    /// <summary>[S-11015] Four engine types, a row each.</summary>
    internal const int Rows = 4;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpuEngines"),
        ConfigType = typeof(GpuEnginesConfig),
        CreateDefault = () => new GpuEnginesConfig(),
        Json = GpuEnginesJsonContext.Default,
        Keywords = ["graphics", "gpu", "3d", "video encode", "video decode", "copy", "engines"],
        Label = GpuEnginesStr.GpuEngines_Label.Local(),
        Description = GpuEnginesStr.GpuEngines_Desc.Local(),
        Glyph = Glyphs.Gpu,
        Sizing = _ => MeterSizing.ItemRows(Rows),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Graphics, 0, 2),
    };

    /// <summary>[S-11015] The adapter (offered with two or more, [S-433]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var engines = (GpuEnginesConfig)config;
        if (GpuAdapterLine.Line(menu, engines.Adapter, value => engines.Adapter = value, GpuAdapters.List()) is { } adapter)
        {
            menu.Lines.Add(adapter);
        }
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new GpuEnginesMeter());
}

/// <summary>[S-11015] What one GPU engines widget remembers: its adapter.</summary>
public sealed class GpuEnginesConfig : ReadingConfig, IReadsSystem, IGpuAdapter
{
    /// <summary>[S-433] The adapter, as its position in DXGI's list (as the GPU reading's).</summary>
    public int Adapter { get; set; }

    /// <summary>[S-6573] Every engine's row carries a bar, an item.</summary>
    protected override bool StyleDrawsItems => true;

    /// <summary>[S-2513] Every adapter's sample (its engines) and the adapter's series.</summary>
    public SystemInterests SystemWants(string systemRoot) => GpuInterests.Of(Adapter);

    public override WidgetConfig Clone() => new GpuEnginesConfig { Adapter = Adapter };

    public override IReadOnlyList<string> Repair() => GpuInterests.Repair(Kind, Adapter, a => Adapter = a);
}

/// <summary>[S-11015] The engines as rows under the header, the adapter's name as its label.</summary>
internal sealed class GpuEnginesMeter : GpuPart
{
    public override WidgetId Kind => new("gpuEngines");

    public override bool Sized => true;

    public override MeterAtom Atom => MeterAtom.Items;

    public override MeterLook Look(WidgetConfig config) => MeterLook.Rows;
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GpuEnginesConfig))]
internal sealed partial class GpuEnginesJsonContext : JsonSerializerContext;

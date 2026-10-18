using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2430] One adapter's memory and sensors.</summary>
public sealed class GpuDetailsConfig : DetailConfig, IReadsSystem
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpuDetails"),
        ConfigType = typeof(GpuDetailsConfig),
        CreateDefault = () => new GpuDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["graphics", "vram", "video memory", "temperature", "fan", "nvidia", "amd"],
        Label = Str.Details_Catalog_Gpu_Label.Local(),
        Description = Str.Details_Catalog_Gpu_Desc.Local(),
        Glyph = Glyphs.Gpu,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Graphics, 0, 1),
    };

    /// <summary>[S-2431] The adapter, as its position in DXGI's list (as <see cref="GpuConfig.Adapter"/>: the primary display's first).</summary>
    public int Adapter { get; set; }

    /// <summary>[S-2513] Every adapter's sample, with its memory and sensors.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Gpu);

    public override WidgetConfig Clone()
    {
        var copy = new GpuDetailsConfig { Adapter = Adapter };
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        if (Adapter < 0)
        {
            notes.Add($"{Kind}: adapter {Adapter} is not a position; using the first");
            Adapter = 0;
        }

        return notes;
    }
}

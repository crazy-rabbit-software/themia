using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Widget.Drive;

namespace Themia.Core.Model;

/// <summary>[S-6274] The drive card's looks: the capacity bar, or a ring of the used share.</summary>
[JsonConverter(typeof(StyleJsonConverter<DriveStyle>))]
public enum DriveStyle
{
    /// <summary>[S-4653] The picture and name, the capacity bar and the space line.</summary>
    Bar = 0,

    /// <summary>[S-6274] A ring of the used share with the share inside; the name and the space line beside or under it.</summary>
    Ring = 1,
}

/// <summary>
/// [S-4670] [S-16646] One drive's widget, whatever its kind (a Drive, a USB stick, a network drive): which drive and its
/// look. Each kind is its own config type (a config's type is how it knows its id); everything else is this one's.
/// </summary>
public abstract class OneDriveConfig : WidgetConfig, IStyled, IDriveChoice, Themia.Sdk.Items.IItemParts
{
    private static readonly StyleOption[] DriveStyles =
    [
        StyleOption.Of(DriveStyle.Bar, DriveStr.Styles_Io_Bar.Local(), 0xEDA2),
        StyleOption.Of(DriveStyle.Ring, Str.Styles_Io_Ring.Local(), 0xEA3A),
    ];

    /// <summary>[S-6274] The card's look.</summary>
    public DriveStyle Style { get; set; } = DriveStyle.Bar;

    IReadOnlyList<StyleOption> IStyled.Styles => DriveStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (DriveStyle)value;
    }

    /// <summary>The drive's root (<c>C:\</c>); empty for the kind's default (the system drive, the first stick or share there).</summary>
    public string Drive { get; set; } = string.Empty;

    /// <summary>[S-16672] The parts its face leaves out.</summary>
    public List<Themia.Sdk.Items.ItemPart> HiddenParts { get; set; } = [];

    /// <summary>[S-16950] The parts' order (a face has no columns).</summary>
    public List<Themia.Sdk.Items.ItemPart> PartOrder { get; set; } = [];

    /// <summary>Anything that is not a drive letter (a folder, a typo) is the kind's default again, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        HiddenParts = Themia.Sdk.Items.ItemOrder.Repaired(HiddenParts, Kind.Value, notes);
        PartOrder = Themia.Sdk.Items.ItemOrder.Repaired(PartOrder, Kind.Value, notes);
        if (string.IsNullOrEmpty(Drive))
        {
            return notes;
        }

        if (DiskRules.Root(Drive) is not { } root)
        {
            notes.Add($"{Kind} widget named '{Drive}', which is not a drive; showing its default");
            Drive = string.Empty;
            return notes;
        }

        Drive = root;
        return notes;
    }

    /// <summary>A copy of the kind's own type with this one's settings.</summary>
    protected T CopyTo<T>(T copy)
        where T : OneDriveConfig
    {
        copy.Drive = Drive;
        copy.Style = Style;
        copy.HiddenParts = [.. HiddenParts];
        copy.PartOrder = [.. PartOrder];
        return copy;
    }
}

/// <summary>[S-4670] One drive (any: the system drive by default).</summary>
public sealed class DriveConfig : OneDriveConfig
{
    public override WidgetConfig Clone() => CopyTo(new DriveConfig());
}

/// <summary>[S-16646] One USB stick or external disk (the first plugged in by default).</summary>
public sealed class UsbStickConfig : OneDriveConfig
{
    public override WidgetConfig Clone() => CopyTo(new UsbStickConfig());
}

/// <summary>[S-16646] One mapped network drive (the first there by default).</summary>
public sealed class NetworkDriveConfig : OneDriveConfig
{
    public override WidgetConfig Clone() => CopyTo(new NetworkDriveConfig());
}

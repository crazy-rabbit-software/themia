using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Elements;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("text", typeof(TextElementModule))]
[assembly: ThemiaWidget("divider", typeof(DividerElementModule))]

namespace Themia.Widget.Elements;

/// <summary>
/// [S-10100] The design elements (owner, 2026-11-21: "there should also be additional design elements that you can add too.
/// like icons or own text or dividers ... if there is nothing add this widget type to the widgets too"): widgets that show no
/// data, only what the person puts there: a text of their own and a divider here, the Icon widget ([S-15010]) beside them. Each is a widget like any other, placed
/// alone or in a composition (a forecast's day, [S-15528]). SPEC.md beside this file.
/// </summary>
internal static class ElementKinds
{
    /// <summary>[S-10101] Own text: one line of the person's words on the one text face, no lead (it is what it says).</summary>
    public static TextKind Text { get; } = new("text", "text", Glyphs.Font,
        ElementsStr.Elements_Text_Label.Local(), ElementsStr.Elements_Text_Desc.Local(),
        ["text", "label", "words", "caption", "note", "title", "design"], () => new TextElementConfig())
    {
        Lead = false,
    };
}

/// <summary>[S-10101] What an own text remembers: its words.</summary>
public sealed class TextElementConfig : WidgetConfig, IDesignElement, IContentAligned
{
    /// <summary>The words it shows; empty shows its name, unavailable, until some are typed.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Its words as the text face shows them: one line, kept as typed but its line breaks.</summary>
    internal static string Clean(string? typed) => (typed ?? string.Empty).ReplaceLineEndings(" ").Trim();

    public override IReadOnlyList<string> Repair()
    {
        var clean = Clean(Text);
        if (clean == Text)
        {
            return [];
        }

        Text = clean;
        return [$"{nameof(TextElementConfig)}: its words held a line break; one line"];
    }

    public override WidgetConfig Clone() => new TextElementConfig { Text = Text };
}

/// <summary>[S-10103] What a divider remembers: nothing; its line runs along its box's long side.</summary>
public sealed class DividerElementConfig : WidgetConfig, IDesignElement
{
    public override WidgetConfig Clone() => new DividerElementConfig();
}

/// <summary>[S-10101] Own text as a module: the text face's sizing and view; its words typed in place on its page.</summary>
public sealed class TextElementModule() : TextModule(ElementKinds.Text, ElementsJsonContext.Default, AddFolderId.Notes, 10)
{
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
        if (config is TextElementConfig text)
        {
            menu.Lines.Add(menu.Text(Glyphs.Rename, ElementsStr.Elements_Text_Words.Text(), text.Text, typed => text.Text = TextElementConfig.Clean(typed)));
        }
    }

    public override FrameworkElement CreateView(IWidgetHost host) => new TextElementView(host);
}

/// <summary>[S-10103] A divider as a module: a hairline along its box's long side, any length.</summary>
public sealed class DividerElementModule : IWidgetModule
{
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("divider"),
        ConfigType = typeof(DividerElementConfig),
        CreateDefault = () => new DividerElementConfig(),
        Json = ElementsJsonContext.Default,
        Label = ElementsStr.Elements_Divider_Label.Local(),
        Description = ElementsStr.Elements_Divider_Desc.Local(),
        Glyph = Glyphs.Minus,
        Keywords = ["divider", "line", "separator", "rule", "design"],
        Sizing = _ => WidgetSizing.Range(new(DividerElementView.Least, DividerElementView.Least), new(DividerElementView.Run, DividerElementView.Least), new(SizeStretch.Ceiling, SizeStretch.Ceiling)),
        Traits = WidgetTraits.None,
        Add = new(AddFolderId.Notes, 2, 12),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new DividerElementView();

    /// <summary>A divider has nothing to set: where it runs is its box.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TextElementConfig))]
[JsonSerializable(typeof(DividerElementConfig))]
internal sealed partial class ElementsJsonContext : JsonSerializerContext;

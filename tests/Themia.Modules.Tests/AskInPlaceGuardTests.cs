using System.Reflection;
using Themia.App.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2351] No widget line opens a page of its own for one input (owner, 2026-11-20: "fence name setting opens a new
/// subpage where i can enter the name, this should be a section not an own page. this type of settings component should
/// not exist anywhere in themia settings. implement and replace everywhere."): what a line asks it carries
/// (<see cref="ItemMenuEntry.Ask"/>, <see cref="ItemMenuEntry.Confirm"/>) and the page asks it in place in the line's card;
/// nothing in the SDK can ask on a page of its own, and no level is opened only to hold one asking line.
/// </summary>
public sealed class AskInPlaceGuardTests
{
    /// <summary>The kinds whose lines read a list only the running host installs (as <c>SettingOwnershipTests</c>).</summary>
    private static readonly string[] NeedsTheHost = ["brightness", "desktopItem", "disk", "diskHistory", "drive", "driveCapacity", "driveFileSystem", "driveFree", "driveType", "driveUsed", "gpu", "gpuClock", "gpuDedicatedMemory", "gpuEngines", "gpuFan", "gpuHistory", "gpuLoad", "gpuMemoryClock", "gpuSharedMemory", "gpuTemperature"];

    /// <summary>What is wrong with <paramref name="lines"/> (at any depth) for [S-2351], each with its path.</summary>
    private static IEnumerable<string> Offences(string path, IEnumerable<ItemMenuEntry> lines)
    {
        foreach (var line in lines)
        {
            var at = path + " › " + line.Text;

            // What asks does nothing on a click but open its section in place: no action of its own to run first.
            if (line.Asks && line.Action is not null)
            {
                yield return at + ": asks and runs an action of its own";
            }

            if (line.Ask is not null && line.Confirm is not null)
            {
                yield return at + ": asks a text and a question at once";
            }

            if (line.Children is not { } children)
            {
                continue;
            }

            // A level opened only to hold one asking line (nothing beside it; an empty list's Add… stands under its empty
            // sentence) is a page for one input under another name.
            var shown = children.Where(c => !c.IsSeparator).ToList();
            if (shown is [{ Asks: true }])
            {
                yield return at + ": a level that holds nothing but one line that asks";
            }

            foreach (var below in Offences(at, children))
            {
                yield return below;
            }
        }
    }

    [Fact]
    public void S2351_no_widget_line_opens_a_page_of_its_own_for_one_input()
    {
        var offences = new List<string>();
        var asking = 0;
        foreach (var module in ModuleRegistry.Widgets)
        {
            var kind = module.Descriptor.Id.Value;
            var menu = new WidgetMenuLines(() => { }) { OpenSetting = _ => { } };
            try
            {
                module.MenuLines(menu, module.Descriptor.CreateDefault());
            }
            catch (InvalidOperationException) when (NeedsTheHost.Contains(kind))
            {
                continue;
            }

            var lines = menu.StyleLines.Concat(menu.Lines).ToList();
            asking += Flat(lines).Count(l => l.Asks);
            offences.AddRange(Offences(kind, lines));
        }

        Assert.True(offences.Count == 0, string.Join("\n", offences));

        // The walk met lines that ask (the fence's name among them).
        Assert.True(asking > 0);
    }

    [Fact]
    public void S2351_nothing_in_the_sdk_can_ask_on_a_page_of_its_own()
    {
        // The one way a line asks is carrying what it asks: no member of the SDK takes a callback that would ask elsewhere.
        Type[] asking = [typeof(Action<WidgetTextAsk>), typeof(Action<WidgetConfirm>)];
        var found = typeof(WidgetMenuLines).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m switch
            {
                MethodBase method => method.GetParameters().Any(p => asking.Contains(p.ParameterType)),
                PropertyInfo property => asking.Contains(property.PropertyType),
                _ => false,
            })
            .Select(m => m.DeclaringType?.Name + "." + m.Name)
            .ToList();

        Assert.True(found.Count == 0, string.Join("\n", found));
    }

    [Fact]
    public void S2351_the_fence_name_is_asked_in_place_in_its_own_card()
    {
        var module = ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == "fence");
        var config = module.Descriptor.CreateDefault();
        var menu = new WidgetMenuLines(() => { });
        module.MenuLines(menu, config);

        // The owner's report: Fence name opened a page of its own. Its line asks in place, saying the name it holds.
        var name = menu.Lines[0];
        Assert.NotNull(name.Ask);
        Assert.Null(name.Action);
        Assert.Null(name.Children);
        Assert.True(name.Ask!.AllowEmpty);
    }

    private static IEnumerable<ItemMenuEntry> Flat(IEnumerable<ItemMenuEntry> lines)
        => lines.SelectMany(l => l.Children is { } children ? [l, .. Flat(children)] : (IEnumerable<ItemMenuEntry>)[l]);
}

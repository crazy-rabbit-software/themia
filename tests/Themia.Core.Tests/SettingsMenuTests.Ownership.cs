using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.App.Shell;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3592] The ownership guard, after Karpo's <c>ConfigCoverageTests.NoSettingIsSetAtTwoLevels</c>
/// (karpo-app): every line of every bar menu (⚙, Display ▾, View ▾) and the bar's own Snap is run on
/// a state of its own, and what each changed is read back from the state file's shape. Every field of
/// the app's look and behaviour is set from exactly one level, and the bar's menus set nothing twice:
/// a setting moved, copied or added in a second place fails here.
/// </summary>
public sealed partial class SettingsMenuTests
{
    /// <summary>The bar's menus over one state: ⚙'s host, and Display ▾'s and View ▾'s over the same state.</summary>
    private sealed class World
    {
        public World()
        {
            // Ids of the test's own: every world finds the same lines by the same keys.
            Settings = new FakeHost();
            foreach (var name in new[] { "Work", "Evening" })
            {
                Settings.State.Views.Add(ViewState.Create(name));
            }

            for (var i = 0; i < Settings.State.Views.Count; i++)
            {
                Settings.State.Views[i].Id = "view" + i;
            }

            Settings.State.ActiveViewId = "view0";
            Settings.State.MonitorViews.Clear();

            // This display's view bar as stored once it has been touched: a line writes its field, not the whole entry.
            ViewBarRules.Update(Settings.State, "a", _ => { });

            Display = new OwnDisplayHost(Settings.State);
            Views = new OwnViewsHost(Settings.State);
        }

        public FakeHost Settings { get; }

        public OwnDisplayHost Display { get; }

        public OwnViewsHost Views { get; }

        /// <summary>The menus' roots, by the menu, as the state holds them now.</summary>
        public IEnumerable<(string Menu, IReadOnlyList<SettingsLine> Lines)> Menus()
        {
            yield return ("settings", SettingsMenu.Root(Settings));
            yield return ("display", DisplayMenu.Root(Display));
            yield return ("view", ViewsMenu.Root(Views));
        }

        /// <summary>What the state holds, every leaf by its path ("design.cornerRadiusPx"), with what Windows was asked as paths of their own.</summary>
        public Dictionary<string, string> Shape()
        {
            var leaves = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(JsonNode.Parse(JsonSerializer.Serialize(Settings.State, StateJson.AppState)), string.Empty, leaves);

            // [S-933] [S-930] Windows owns these two; what the menu asked of it is its write.
            leaves["windows.startup"] = string.Join(",", Settings.StartupAsked);
            leaves["windows.desktopIcons"] = string.Join(",", Settings.DesktopIconsAsked);
            return leaves;
        }

        private static void Flatten(JsonNode? node, string path, Dictionary<string, string> into)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj)
                    {
                        Flatten(value, path.Length == 0 ? key : path + "." + key, into);
                    }

                    if (obj.Count == 0)
                    {
                        into[path] = "{}";
                    }

                    break;
                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                    {
                        Flatten(array[i], path + "[" + i + "]", into);
                    }

                    into[path + ".count"] = array.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                default:
                    into[path] = node?.ToJsonString() ?? "null";
                    break;
            }
        }
    }

    /// <summary>Display ▾'s host as the app's: every write a change of the state.</summary>
    private sealed class OwnDisplayHost(AppState state) : IDisplayMenuHost
    {
        public AppState State { get; } = state;

        public string DisplayId => "a";

        public IReadOnlyList<MenuDisplay> Displays { get; } =
        [
            new("a", "Screen a", "Screen a — 1920 × 1080", new DisplayInfo("a", true, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true)),
            new("b", "Screen b", "Screen b — 1920 × 1080", new DisplayInfo("b", true, new PixelRect(1920, 0, 1920, 1080), new PixelRect(1920, 0, 1920, 1040), false)),
        ];

        public void Mutate(Action<AppState> change, bool relayout = true) => change(State);

        public bool SetEnabled(string displayId, bool on)
        {
            if (on)
            {
                State.DisabledMonitors.Remove(displayId);
            }
            else
            {
                State.DisabledMonitors.Add(displayId);
            }

            return true;
        }

        public void SetScale(double scale) => State.MonitorScales[DisplayId] = scale;

        public void Show(string viewId) => State.ShowViewOn(DisplayId, viewId);
    }

    /// <summary>View ▾'s host as the app's for what sets something; its commands (new, copy, delete, move) are not settings.</summary>
    private sealed class OwnViewsHost(AppState state) : IViewsMenuHost
    {
        public AppState State { get; } = state;

        public string DisplayId => "a";

        public void Mutate(Action<AppState> change, bool relayout = true) => change(State);

        public void Add()
        {
            // ignore: a command, not a setting.
        }

        public void Duplicate(string viewId)
        {
            // ignore: a command, not a setting.
        }

        public void Delete(string viewId)
        {
            // ignore: a command, not a setting.
        }

        public void Reorder(string viewId, string targetId)
        {
            // ignore: a command, not a setting.
        }

        public void Confirm(SettingsConfirm confirm)
        {
            // ignore: only commands ask, and commands are not run here.
        }
    }

    /// <summary>
    /// A line that sets something, where it stands: its menu, the level it is on (the choice or the
    /// submenu holding it, or the menu itself) and the keys down to it.
    /// </summary>
    private sealed record Setter(string Menu, string Level, string[] Path);

    /// <summary>
    /// [S-3592] Lines that are not one setting of one level, each for a reason: a preset is a bundle of
    /// the look's own settings written at once ([S-834]); a form's field holds a draft until its form
    /// adds the account ([S-3554]).
    /// </summary>
    private static readonly string[] NotSettings = [SettingsMenu.Keys.Appearance + "/style", SettingsMenu.Keys.Accounts];

    private static IEnumerable<Setter> Setters(string menu, IReadOnlyList<SettingsLine> lines, string level, string[] path)
    {
        foreach (var line in lines)
        {
            string[] at = [.. path, line.Key];
            if (NotSettings.Any(k => line.Key == k || line.Key.StartsWith(k + "/", StringComparison.Ordinal)))
            {
                continue;
            }

            switch (line)
            {
                case SettingsSubmenu submenu:
                    foreach (var below in Setters(menu, submenu.Lines, menu + ":" + submenu.Key, at))
                    {
                        yield return below;
                    }

                    break;
                case SettingsChoice choice:
                    foreach (var option in Setters(menu, choice.Options, menu + ":" + choice.Key, at))
                    {
                        yield return option;
                    }

                    break;
                case SettingsToggle or SettingsRadio or SettingsSlider or SettingsColour or SettingsFill or SettingsFont or SettingsAsk or SettingsIcon or SettingsItem:
                    yield return new Setter(menu, level, at);
                    break;
            }
        }
    }

    /// <summary>Do what a pick of the line would, so it writes something other than what it holds. False for one that holds already (a radio).</summary>
    private static bool Set(SettingsLine line)
    {
        switch (line)
        {
            case SettingsToggle toggle:
                toggle.Set(!toggle.On);
                return true;
            case SettingsRadio radio when !radio.Holds:
                radio.Pick();
                return true;
            case SettingsSlider slider:
                slider.Set(slider.Value >= slider.Max ? slider.Min : slider.Max);
                return true;
            case SettingsColour colour:
                colour.Set("#123456");
                return true;
            case SettingsFill fill:
                fill.Set(FillChoice.Of("#123456", 0.5));
                return true;
            case SettingsFont font:
                font.Set("Consolas");
                return true;
            case SettingsAsk ask:
                ask.Done("Owned");
                return true;
            case SettingsIcon icon:
                icon.Set(Glyphs.Home);
                return true;
            case SettingsItem item:
                foreach (var tool in item.Tools)
                {
                    tool.Ask?.Done("Owned");
                }

                return item.Tools.Any(t => t.Ask is not null);
            default:
                return false;
        }
    }

    /// <summary>Every path each level's lines write, run one line at a time on a world of its own.</summary>
    private static Dictionary<string, HashSet<string>> WritersByPath()
    {
        var writers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Record(string owner, World world, Dictionary<string, string> before)
        {
            var after = world.Shape();
            foreach (var path in before.Keys.Union(after.Keys).Where(p => !before.TryGetValue(p, out var was) || !after.TryGetValue(p, out var now) || was != now))
            {
                // A view's own fields are the view's whichever it is: one path for them all.
                var field = System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", "[]");
                if (!writers.TryGetValue(field, out var owners))
                {
                    writers[field] = owners = [];
                }

                owners.Add(owner);
            }
        }

        foreach (var (menu, root) in new World().Menus())
        {
            foreach (var setter in Setters(menu, root, menu, []))
            {
                var world = new World();
                var line = SettingsMenu.Find(world.Menus().Single(m => m.Menu == setter.Menu).Lines, setter.Path)
                    ?? throw new InvalidOperationException($"{setter.Menu} has no {string.Join('/', setter.Path)}");
                var before = world.Shape();
                if (Set(line))
                {
                    Record(setter.Level, world, before);
                }
            }
        }

        // [S-3516] The bar's Snap: its one setting of its own.
        var bar = new World();
        var shape = bar.Shape();
        EditBarSnap.Set(bar.Settings.State, !bar.Settings.State.Design.GridSnap);
        Record("bar:snap", bar, shape);
        return writers;
    }

    /// <summary>
    /// [S-3592] One field of one level reached from two rows of it, by design: a view's name is View ▾'s,
    /// on the shown view's Name row and on its row of Manage views (Karpo's level of things, [S-3549]).
    /// </summary>
    private static readonly (string Path, string[] Levels)[] SameLevelTwice = [("views[].name", ["view", "view:manage"])];

    [Fact]
    public void S3592_no_setting_is_set_at_two_levels()
    {
        var twice = WritersByPath()
            .Where(w => w.Value.Count > 1 && !SameLevelTwice.Any(s => s.Path == w.Key && w.Value.SetEquals(s.Levels)))
            .Select(w => $"{w.Key} ← {string.Join(", ", w.Value.Order(StringComparer.Ordinal))}").ToList();

        Assert.True(twice.Count == 0, "set at more than one level:\n" + string.Join("\n", twice));
    }

    [Fact]
    public void S3592_every_field_of_the_look_and_the_behaviour_has_its_one_level()
    {
        var written = WritersByPath().Keys.ToList();

        // RunAtStartup is what Windows was last asked, kept for when it cannot be read ([S-935]): ⚙ asks Windows.
        string[] fields =
        [
            .. typeof(DesignState).GetProperties().Where(p => p.CanWrite && p.Name != nameof(DesignState.RunAtStartup)).Select(p => "design." + JsonNamingPolicy.CamelCase.ConvertName(p.Name)),
            .. typeof(AppPreferences).GetProperties().Where(p => p.CanWrite && p.Name is not nameof(AppPreferences.DesktopIconsVisible)).Select(p => "preferences." + JsonNamingPolicy.CamelCase.ConvertName(p.Name)),
            "windows.startup", "windows.desktopIcons", "monitorScales", "monitorViews", "disabledMonitors", "defaultViewForNewDisplays",
        ];

        var unowned = fields.Where(f => !written.Any(w => w == f || w.StartsWith(f + ".", StringComparison.Ordinal) || w.StartsWith(f + "[", StringComparison.Ordinal))).ToList();
        Assert.True(unowned.Count == 0, "no bar menu sets: " + string.Join(", ", unowned));
    }

    /// <summary>
    /// [S-3592] What each menu calls its settings: a line's words, an option by its choice's words. A
    /// radio standing on a level of its own (a view to show) is named by its item, not as a setting.
    /// </summary>
    internal static Dictionary<string, HashSet<string>> SettingWords(IEnumerable<(string Menu, IReadOnlyList<SettingsLine> Lines)> menus)
    {
        var words = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (menu, root) in menus)
        {
            foreach (var setter in Setters(menu, root, menu, []))
            {
                var line = SettingsMenu.Find(root, setter.Path)!;
                var named = line is SettingsRadio && setter.Path.Length > 1 && SettingsMenu.Find(root, setter.Path[..^1]) is SettingsChoice choice
                    ? choice.Text
                    : line is SettingsRadio or SettingsItem ? null : WordsOf(line);
                if (named is { Length: > 0 })
                {
                    (words.TryGetValue(named, out var at) ? at : words[named] = []).Add(menu);
                }
            }
        }

        return words;
    }

    private static string? WordsOf(SettingsLine line) => line switch
    {
        SettingsToggle t => t.Text,
        SettingsSlider s => s.Text,
        SettingsColour c => c.Text,
        SettingsFill f => f.Text,
        SettingsFont f => f.Text,
        SettingsAsk a => a.Text,
        SettingsIcon i => i.Text,
        _ => null,
    };

    [Fact]
    public void S3592_no_two_menus_offer_a_setting_under_the_same_words()
    {
        // Regression: ⚙ › Appearance and Display ▾ both had "Widget scale" (an app-wide and a display's
        // factor multiplied together), so nobody could tell which one they were setting.
        var twice = SettingWords(new World().Menus()).Where(w => w.Value.Count > 1).Select(w => $"{w.Key} ← {string.Join(", ", w.Value)}").ToList();

        Assert.True(twice.Count == 0, "the same words in more than one menu:" + string.Join("; ", twice));
    }

    [Fact]
    public void S3516_snap_is_the_bars_alone()
    {
        var snap = WritersByPath()["design.gridSnap"];

        Assert.Equal(["bar:snap"], snap);
    }
}

using System.Reflection;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-3592] The ownership guard over every widget's menu (Karpo's
/// <c>ConfigCoverageTests.NoSettingIsSetAtTwoLevels</c>, the widgets' half; the bar menus' half is
/// Core's <c>SettingsMenuTests.S3592_</c>): no widget's lines offer a setting under the words of one
/// the bar's menus (⚙, Display ▾, View ▾, Snap) or the card's own rows (Style, Header, the Appearance
/// foot) set, unless the line is a link to it; and no widget's config carries a field of a type only
/// a higher level owns (a temperature scale, a clock, a week start, a cadence, a theme, a language).
/// </summary>

public sealed class SettingOwnershipTests
{
    /// <summary>A settings host that reaches nothing: enough to build ⚙'s lines and read their words.</summary>
    private sealed class QuietSettingsHost : ISettingsMenuHost, ILicenceMenuHost, IBackupMenuHost, IAboutMenuHost
    {
        public AppState State { get; } = AppState.CreateDefault();

        public void Mutate(Action<AppState> change, bool relayout = true) => change(State);

        public AutostartStatus? Startup => null;

        public void SetStartup(bool on)
        {
            // ignore: nothing is asked of Windows here.
        }

        public bool DesktopIconsShown => true;

        public void SetDesktopIcons(bool shown)
        {
            // ignore: nothing is asked of Windows here.
        }

        public bool Suspended => false;

        public void SetGameModeSuspend(bool on) => State.Preferences.GameModeSuspend = on;

        public bool AnimationsEnabled => true;

        public void ReplayIntro(IntroMode mode)
        {
            // ignore: nothing plays here.
        }

        public Locale WindowsLanguage => Locale.En;

        public void SetLanguage(AppLanguage language) => State.Preferences.Language = language;

        public IAccountsMenuHost? Accounts => null;

        public ILicenceMenuHost Licence => this;

        public IBackupMenuHost Backup => this;

        public IAboutMenuHost About => this;

        public SettingsOutcomes Outcomes { get; } = new();

        public void Confirm(SettingsConfirm confirm)
        {
            // ignore: nothing is run here.
        }

        public void Quit()
        {
            // ignore: nothing quits here.
        }

        public LicenseStatus Status { get; } = new(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild);

        public bool CanPurchase => false;

        public string? Price => null;

        public string? Refusal => null;

        public void Purchase()
        {
            // ignore: nothing is bought here.
        }

        public void Restore()
        {
            // ignore: nothing is restored here.
        }

        public string StatePath => "state.json";

        public void Export()
        {
            // ignore: nothing is written here.
        }

        public void Import()
        {
            // ignore: nothing is read here.
        }

        public void OpenFile()
        {
            // ignore: nothing is opened here.
        }

        public void Reset()
        {
            // ignore: nothing is reset here.
        }

        public string Version => "1.0.0";

        public string Runtime => ".NET";

        public void Open(Uri page)
        {
            // ignore: nothing is opened here.
        }

        public UninstallPlan Uninstall => UninstallPlan.Create(false, deleteData: false, "C:\\Roaming", "C:\\Local", "Themia", Version);

        public bool DeleteData => false;

        public void SetDeleteData(bool on)
        {
            // ignore: a draft of nobody's.
        }

        public void UninstallNow(bool deleteData)
        {
            // ignore: nothing is uninstalled here.
        }
    }

    private sealed class QuietDisplayHost(AppState state) : IDisplayMenuHost
    {
        public AppState State { get; } = state;

        public string DisplayId => "a";

        public IReadOnlyList<MenuDisplay> Displays { get; } = [];

        public void Mutate(Action<AppState> change, bool relayout = true) => change(State);

        public bool SetEnabled(string displayId, bool on) => false;

        public void SetScale(double scale) => State.MonitorScales[DisplayId] = scale;

        public void Show(string viewId) => State.ShowViewOn(DisplayId, viewId);
    }

    private sealed class QuietViewsHost(AppState state) : IViewsMenuHost
    {
        public AppState State { get; } = state;

        public string DisplayId => "a";

        public void Mutate(Action<AppState> change, bool relayout = true) => change(State);

        public void Add()
        {
            // ignore: a command.
        }

        public void Duplicate(string viewId)
        {
            // ignore: a command.
        }

        public void Delete(string viewId)
        {
            // ignore: a command.
        }

        public void Reorder(string viewId, string targetId)
        {
            // ignore: a command.
        }

        public void Confirm(SettingsConfirm confirm)
        {
            // ignore: a command's question.
        }
    }

    /// <summary>The words of every setting the bar's menus and the card's own rows set: what a widget's line may not be called.</summary>
    private static HashSet<string> OwnedWords()
    {
        var settings = new QuietSettingsHost();
        var words = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        void Walk(IReadOnlyList<SettingsLine> lines)
        {
            foreach (var line in lines)
            {
                switch (line)
                {
                    case SettingsSubmenu submenu:
                        Walk(submenu.Lines);
                        break;
                    case SettingsChoice choice:
                        words.Add(choice.Text);
                        break;
                    case SettingsToggle t:
                        words.Add(t.Text);
                        break;
                    case SettingsSlider s:
                        words.Add(s.Text);
                        break;
                    case SettingsColour c:
                        words.Add(c.Text);
                        break;
                    case SettingsFill f:
                        words.Add(f.Text);
                        break;
                    case SettingsFont f:
                        words.Add(f.Text);
                        break;
                }
            }
        }

        Walk(SettingsMenu.Root(settings));
        Walk(DisplayMenu.Root(new QuietDisplayHost(settings.State)));
        Walk(ViewsMenu.Root(new QuietViewsHost(settings.State)));

        // The bar's Snap, and the card's own rows ([S-3735], [S-3739]): Style, Header and the Appearance foot.
        words.UnionWith(
        [
            Str.Shell_Toolbar_SnapName.Text(), Str.Styles_Label.Text(), Str.App_ConfigHeaderLabel.Text(), Str.Group_Title_Label.Text(),
            Str.Widget_Align_Label.Text(), Str.Shell_Items_Background.Text(), Str.Shell_Items_TextShort.Text(), Str.Shell_Fill_Image.Text(),
            Str.Widget_Scale_Label.Text(), Str.Settings_Section_Appearance.Text(),
        ]);
        return words;
    }

    /// <summary>What a widget's line sets under: a switch, a choice, a colour, a slider or a line of text by its words; links, notes and commands set nothing.</summary>
    private static IEnumerable<(string Words, string Where)> SettingLines(IEnumerable<ItemMenuEntry> lines, string where)
    {
        foreach (var line in lines)
        {
            if (line.IsSeparator || line.Note || line.LinkTo is not null)
            {
                continue;
            }

            var choice = line.Children is { Count: > 0 } options && options.All(o => o.Group == line.Text);
            if (choice || (line.Checked is not null && line.Group is null) || line.Colour is not null || line.Slider is not null || (line.Asks && line.Children is null))
            {
                yield return (line.Text, where + " › " + line.Text);
            }

            if (!choice && line.Children is { } children)
            {
                foreach (var below in SettingLines(children, where + " › " + line.Text))
                {
                    yield return below;
                }
            }
        }
    }

    /// <summary>
    /// [S-3592] A widget's own setting under words a higher level uses too, each for its reason: it is
    /// the widget's own and only shares a word (a focus session's Length is the session's, never an
    /// animation's, ⚙ › Appearance › Motion's).
    /// </summary>
    private static readonly (string Kind, string Words)[] OwnWords = [("focusSession", "Length")];

    /// <summary>
    /// The kinds whose menus read a list only the running host installs (the displays with a brightness,
    /// the drives, the graphics adapters, the launcher): built with stand-ins by their own menu tests.
    /// </summary>
    private static readonly string[] NeedsTheHost = ["brightness", "disk", "drive", "gpu", "gpuDetails", "shortcuts"];

    [Fact]
    public void S3592_no_widget_sets_what_a_higher_level_owns_except_through_a_link()
    {
        var owned = OwnedWords();
        var found = new List<string>();
        var unbuilt = new List<string>();
        var seen = 0;
        foreach (var module in ModuleRegistry.Widgets)
        {
            var kind = module.Descriptor.Id.Value;
            var menu = new WidgetMenuLines(() => { }, _ => { }) { OpenSetting = _ => { } };
            try
            {
                module.MenuLines(menu, module.Descriptor.CreateDefault());
            }
            catch (InvalidOperationException) when (NeedsTheHost.Contains(kind))
            {
                unbuilt.Add(kind);
                continue;
            }

            foreach (var (words, where) in SettingLines(menu.StyleLines.Concat(menu.Lines), kind))
            {
                seen++;
                if (owned.Contains(words) && !OwnWords.Contains((kind, words)))
                {
                    found.Add(where);
                }
            }
        }

        Assert.Equal(NeedsTheHost, unbuilt.Order(StringComparer.Ordinal));

        // The walk reads the widgets' settings at all: a vocabulary change that hid them would pass empty.
        Assert.True(seen > 150, $"only {seen} settings read off the widgets' menus");
        Assert.True(found.Count == 0, "a widget sets what a higher level owns (link to it instead, [S-3749]): " + string.Join("; ", found));
    }

    /// <summary>[S-3590] The types only a higher level holds: a widget follows them, never keeps its own.</summary>
    private static readonly Type[] OwnedTypes =
    [
        typeof(TemperatureUnit), typeof(TemperatureScale), typeof(ClockHours), typeof(FirstWeekday), typeof(WeekStartDay),
        typeof(AppLanguage), typeof(AppTheme), typeof(RefreshPreferences), typeof(RegionPreferences),
    ];

    [Fact]
    public void S3592_no_widget_config_keeps_its_own_copy_of_a_format_a_cadence_a_theme_or_a_language()
    {
        var copies = ModuleRegistry.Widgets
            .Select(m => m.Descriptor.ConfigType)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && OwnedTypes.Contains(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
                .Select(p => $"{t.Name}.{p.Name}"))
            .Distinct()
            .ToList();

        Assert.True(copies.Count == 0, "a widget keeps its own copy of what ⚙ owns: " + string.Join(", ", copies));
    }
}

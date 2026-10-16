using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The picker's search, ported case for case from the original's <c>widgetSearch.test.ts</c>. The
/// stand-in registry keeps the original's shapes: a multi-word label, a translated label with an
/// English twin, camel-case type keys, and widgets that share words in their descriptions.
/// </summary>
public sealed class WidgetSearchTests
{
    private static readonly WidgetSearchDoc[] Docs =
    [
        new("clock", ["Clock"], WidgetKeywords.For(WidgetKind.Clock),
            ["Clock for your timezone or any city in the world"], ["Others"]),
        new("calendar", ["Calendar"], WidgetKeywords.For(WidgetKind.Calendar),
            ["See your upcoming events and meetings"], ["Office"], ["Microsoft"]),
        new("weatherCurrent", ["Wetter", "Weather"], WidgetKeywords.For(WidgetKind.WeatherNow),
            ["Aktuelle Bedingungen für jeden Ort", "Current conditions for any location"], ["Online"]),
        new("weatherForecast", ["Weather Forecast"], Original(WidgetKind.WeatherForecast),
            ["Multi-day weather outlook for any location"], ["Online"]),
        new("net", ["Network Monitor"], WidgetKeywords.For(WidgetKind.Network),
            ["Live upload and download speeds"], ["System"]),
        new("cpu", ["CPU Monitor"], WidgetKeywords.For(WidgetKind.Cpu),
            ["Live CPU usage and per-core load"], ["System"]),
        // Not a widget any more: a document with a camel-case key and a provider, for the matcher alone.
        new("githubNotifications", ["GitHub Notifications"], ["git", "repo", "inbox", "mentions", "alerts"],
            ["Your GitHub notifications across all selected accounts"], ["Online"], ["GitHub"]),
        new("pomodoro", ["Pomodoro"], WidgetKeywords.For(WidgetKind.Pomodoro),
            ["Focus timer with work and break cycles"], ["Others"]),
        new("countdown", ["Countdown"], WidgetKeywords.For(WidgetKind.Countdown),
            ["Count down to an important date"], ["Others"]),
    ];

    private static readonly (IndexedWidget Index, string Value)[] Indexed =
        [.. Docs.Select(d => (WidgetSearch.Index(d), d.Type))];

    private static List<string> Search(string query) => WidgetSearch.Rank(Indexed, query);

    private static string? Top(string query) => Search(query).FirstOrDefault();

    // The WinUI forecast keywords add "weather" (its label here is "Forecast"); the stand-in keeps
    // the original's list so "weather forecast" still exercises the two-field AND.
    private static string[] Original(WidgetKind kind) => [.. WidgetKeywords.For(kind).Where(k => k != "weather")];

    [Fact]
    public void S941_LowercasesStripsDiacriticsAndFoldsSharpS()
    {
        Assert.Equal("wetter", WidgetSearch.Normalize("Wetter"));
        Assert.Equal("grusse", WidgetSearch.Normalize("Grüße"));
        Assert.Equal("cafe", WidgetSearch.Normalize("  Café "));
    }

    [Fact]
    public void S941_SplitsCamelCaseAndPunctuationIntoWords()
    {
        Assert.Equal(["github", "notifications"], WidgetSearch.Tokenize("githubNotifications"));
        Assert.Equal(["sun", "moon"], WidgetSearch.Tokenize("Sun & Moon"));
        Assert.Equal(["to", "do"], WidgetSearch.Tokenize("to-do"));
    }

    [Fact]
    public void S941_KeepsUnsegmentedScriptsAsOneToken()
    {
        Assert.Equal(["天气"], WidgetSearch.Tokenize("天气"));
    }

    [Fact]
    public void S944_TypoBudgetScalesWithLengthAndShortTokensGetNone()
    {
        Assert.Equal(0, WidgetSearch.MaxTypos("cpu"));
        Assert.Equal(1, WidgetSearch.MaxTypos("clok"));
        Assert.Equal(1, WidgetSearch.MaxTypos("clock"));
        Assert.Equal(2, WidgetSearch.MaxTypos("calendar"));
    }

    [Fact]
    public void S944_TranspositionIsOneEdit()
    {
        Assert.Equal(1, WidgetSearch.EditDistance("clcok", "clock", 2));
        Assert.Equal(1, WidgetSearch.EditDistance("teh", "the", 2));
    }

    [Fact]
    public void S944_BailsOutBeyondTheBudget()
    {
        Assert.Equal(2, WidgetSearch.EditDistance("clock", "calendar", 1));
    }

    [Fact]
    public void S947_FindsTheExactLabel()
    {
        Assert.Equal("clock", Top("Clock"));
        Assert.Equal("net", Top("Network Monitor"));
    }

    [Fact]
    public void S947_LabelPrefixBeatsADescriptionMention()
    {
        Assert.Equal("countdown", Top("cou"));
        Assert.Equal("clock", Top("cl"));
    }

    [Fact]
    public void S940_MatchesTheTypeKeySplitOnCamelCase()
    {
        Assert.Contains("githubNotifications", Search("notifications"));
    }

    [Fact]
    public void S940_MatchesCategoryAndProviderNames()
    {
        var system = Search("system");
        Assert.Contains("cpu", system);
        Assert.Contains("net", system);
        Assert.Contains("calendar", Search("microsoft"));
    }

    [Fact]
    public void S943_FindsAWidgetByWhatItShows()
    {
        Assert.Contains("net", Search("wifi"));
        var temperature = Search("temperature");
        Assert.Contains("weatherCurrent", temperature);
        Assert.Contains("weatherForecast", temperature);
        Assert.DoesNotContain("clock", Search("memory"));
    }

    [Fact]
    public void S943_FindsAWidgetByTheJobItDoes()
    {
        var timer = Search("timer");
        Assert.Contains("pomodoro", timer);
        Assert.Contains("countdown", timer);
        Assert.Contains("calendar", Search("appointments"));
    }

    [Fact]
    public void S943_LabelHitRanksAboveKeywordOnlyHit()
    {
        Assert.Equal("countdown", Top("countdown"));
    }

    [Fact]
    public void S944_SurvivesOneWrongMissingOrExtraLetter()
    {
        Assert.Contains("calendar", Search("calender"));
        Assert.Contains("clock", Search("clok"));
        Assert.Contains("weatherCurrent", Search("wether"));
        Assert.Contains("pomodoro", Search("pomodorro"));
    }

    [Fact]
    public void S944_SurvivesATransposition()
    {
        Assert.Contains("clock", Search("clcok"));
        Assert.Contains("calendar", Search("calnedar"));
    }

    [Fact]
    public void S944_MatchesAMistypedPrefixOfALongerWord()
    {
        Assert.Contains("calendar", Search("kalend"));
    }

    [Fact]
    public void S944_DoesNotFuzzyMatchThreeLetterTokens()
    {
        Assert.Empty(Search("cpx"));
    }

    [Fact]
    public void S947_LiteralMatchOutranksTypoMatch()
    {
        Assert.Equal("clock", Top("clock"));
    }

    [Fact]
    public void S944_ShortTokenWithWrongFirstLetterIsNotATypo()
    {
        Assert.Empty(Search("dount"));
        Assert.Contains("countdown", Search("dountdown"));
        Assert.Contains("countdown", Search("cound"));
    }

    [Fact]
    public void S945_NeverFuzzyMatchesInsideADescription()
    {
        Assert.Empty(Search("citiy"));
        Assert.Contains("clock", Search("city"));
    }

    [Fact]
    public void S942_EveryTokenHasToLandSomewhere()
    {
        Assert.Empty(Search("clock zzzz"));
        Assert.Empty(Search("calendar wifi"));
        Assert.Equal("weatherForecast", Search("weather forecast")[0]);
    }

    [Fact]
    public void S942_TokensMayMatchDifferentFields()
    {
        Assert.Equal(["githubNotifications"], Search("github inbox"));
    }

    [Fact]
    public void S940_FindsATranslatedWidgetByEitherLabel()
    {
        Assert.Contains("weatherCurrent", Search("wetter"));
        Assert.Contains("weatherCurrent", Search("weather"));
        Assert.Contains("weatherCurrent", Search("wettr"));
    }

    [Fact]
    public void S946_EmptyQueryMatchesEverything()
    {
        Assert.Equal(Docs.Length, Search("").Count);
        Assert.Equal(Docs.Length, Search("   ").Count);
    }

    [Fact]
    public void S946_NonMatchesScoreZero()
    {
        var clock = WidgetSearch.Index(Docs[0]);
        Assert.Equal(0, WidgetSearch.Score(clock, "spreadsheet"));
        Assert.True(WidgetSearch.Score(clock, "clock") > 0);
    }

    [Fact]
    public void S946_EqualScoresBreakTiesByLabel()
    {
        var monitors = Search("monitor");
        Assert.Equal(monitors, Search("monitor"));
        Assert.True(monitors.IndexOf("cpu") < monitors.IndexOf("net"));
    }

    [Fact]
    public void S943_EveryKindHasLowerCaseKeywordsWithoutRepeats()
    {
        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var words = WidgetKeywords.For(kind);
            Assert.True(words.Count > 0, $"{kind} has no search keywords");
            Assert.All(words, w => Assert.Equal(w.ToLowerInvariant(), w));
            Assert.Equal(words.Count, words.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void S943_ProvidersFindAccountWidgets()
    {
        var docs = Enum.GetValues<WidgetKind>()
            .Select(k => (WidgetSearch.Index(new WidgetSearchDoc(k.ToString(), [k.ToString()], WidgetKeywords.For(k), Providers: WidgetKeywords.Providers(k))), k))
            .ToArray();

        var google = WidgetSearch.Rank(docs, "google");
        Assert.Contains(WidgetKind.Email, google);
        Assert.Contains(WidgetKind.Calendar, google);
        Assert.Contains(WidgetKind.Contacts, google);
        Assert.Contains(WidgetKind.Tasks, google);
    }

    [Theory]
    [InlineData("agenda", WidgetKind.Calendar)]
    [InlineData("month calendar", WidgetKind.Calendar)]
    [InlineData("year calendar", WidgetKind.Calendar)]
    [InlineData("timeline", WidgetKind.Calendar)]
    [InlineData("waveform", WidgetKind.AudioVisualizer)]
    [InlineData("oscilloscope", WidgetKind.AudioVisualizer)]
    [InlineData("led", WidgetKind.AudioVisualizer)]
    [InlineData("radial", WidgetKind.AudioVisualizer)]
    [InlineData("folder grid", WidgetKind.Folder)]
    [InlineData("folder icons", WidgetKind.Folder)]
    [InlineData("thumbnails", WidgetKind.Folder)]
    public void S943_the_merged_kinds_are_found_by_their_former_names(string query, WidgetKind kind)
    {
        // [S-3640] [S-3216] [S-4123] What were widgets of their own are styles now: their names and
        // words find the one widget. The label here is the kind's name, as in the catalogue.
        var docs = Enum.GetValues<WidgetKind>()
            .Select(k => (WidgetSearch.Index(new WidgetSearchDoc(k.ToString(), [k.ToString()], WidgetKeywords.For(k), Providers: WidgetKeywords.Providers(k))), k))
            .ToArray();

        Assert.Contains(kind, WidgetSearch.Rank(docs, query));
    }
}

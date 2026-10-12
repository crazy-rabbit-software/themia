using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The city search behind the shared location row ([S-2841]..[S-2845]): what is worth asking the
/// geocoder, what of its answer is offered, and how same-named places are told apart. Fixtures
/// only; nothing here touches the network.
/// </summary>
public class PlaceLookupTests
{
    private static PlaceCandidate Hit(
        string? name,
        string? admin1 = null,
        string? country = null,
        double? lat = 47.37,
        double? lon = 8.54,
        string? admin2 = null)
        => new(name, admin1, admin2, country, lat, lon);

    // ---- [S-2841] the query ----------------------------------------------------------------

    [Theory]
    [InlineData("  Zurich ", "Zurich")]
    [InlineData("New    York", "New York")]
    [InlineData("San\tFrancisco", "San Francisco")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void S2841_a_query_is_trimmed_and_its_inner_whitespace_collapsed(string? typed, string expected)
        => Assert.Equal(expected, PlaceQuery.Normalise(typed));

    [Fact]
    public void S2841_a_query_is_cut_at_the_place_length_limit()
        => Assert.Equal(WebInput.MaxPlaceLength, PlaceQuery.Normalise(new string('a', 200)).Length);

    [Fact]
    public void S2841_an_empty_box_asks_nothing()
        => Assert.Equal(PlaceQueryStep.Idle, PlaceQuery.Classify(string.Empty, submitted: false));

    [Fact]
    public void S2841_typing_waits_for_two_characters()
    {
        Assert.Equal(PlaceQueryStep.TooShort, PlaceQuery.Classify("Z", submitted: false));
        Assert.Equal(PlaceQueryStep.Search, PlaceQuery.Classify("Zu", submitted: false));
    }

    [Fact]
    public void S2841_enter_searches_a_single_character_because_some_cities_are_one()
        => Assert.Equal(PlaceQueryStep.Search, PlaceQuery.Classify("津", submitted: true));

    [Theory]
    [InlineData("12345")]
    [InlineData("!!")]
    public void S2841_text_with_no_letter_in_it_is_not_a_place_name(string typed)
        => Assert.Equal(PlaceQueryStep.NotAName, PlaceQuery.Classify(typed, submitted: false));

    [Fact]
    public void S2841_the_debounce_is_shorter_than_a_pause_and_longer_than_a_keystroke()
        => Assert.InRange(PlaceQuery.Debounce.TotalMilliseconds, 200, 400);

    // ---- [S-2842] results ------------------------------------------------------------------

    [Fact]
    public void S2842_a_hit_shows_the_city_over_its_region_and_country()
    {
        var match = Assert.Single(PlaceQuery.Results([Hit("Zürich", "Zurich", "Switzerland")], "zurich"));

        Assert.Equal("Zürich", match.Name);
        Assert.Equal("Zurich, Switzerland", match.Detail);
        Assert.Equal("Zürich, Zurich, Switzerland", match.Label);
    }

    [Fact]
    public void S2842_a_region_named_like_the_city_is_not_repeated()
    {
        var match = Assert.Single(PlaceQuery.Results([Hit("Berlin", "Berlin", "Germany")], "berlin"));

        Assert.Equal("Germany", match.Detail);
        Assert.Equal("Berlin, Germany", match.Label);
    }

    [Fact]
    public void S2842_a_hit_with_no_details_is_its_name_alone()
    {
        var match = Assert.Single(PlaceQuery.Results([Hit("Atlantis")], "atlantis"));

        Assert.Equal(string.Empty, match.Detail);
        Assert.Equal("Atlantis", match.Label);
    }

    [Theory]
    [InlineData(null, 47.0, 8.0)]
    [InlineData("  ", 47.0, 8.0)]
    [InlineData("Zurich\u0007", 47.0, 8.0)]
    [InlineData("Zurich", 91.0, 8.0)]
    [InlineData("Zurich", 47.0, double.NaN)]
    [InlineData("Zurich", null, 8.0)]
    public void S2842_a_malformed_hit_is_dropped(string? name, double? lat, double lon)
        => Assert.Empty(PlaceQuery.Results([Hit(name, lat: lat, lon: lon)], "zurich"));

    [Fact]
    public void S2842_a_region_with_a_control_character_is_left_out_not_shown()
    {
        var match = Assert.Single(PlaceQuery.Results([Hit("Zurich", "Can\u0000ton", "Switzerland")], "zurich"));

        Assert.Equal("Switzerland", match.Detail);
    }

    [Fact]
    public void S2842_an_overlong_name_is_dropped()
        => Assert.Empty(PlaceQuery.Results([Hit(new string('a', 300))], "a"));

    [Fact]
    public void S2842_the_same_place_twice_is_offered_once()
    {
        var results = PlaceQuery.Results(
            [Hit("Zurich", "Zurich", "Switzerland", 47.37, 8.54), Hit("zurich", "zurich", "switzerland", 47.40, 8.50)],
            "zurich");

        Assert.Single(results);
    }

    [Fact]
    public void S2842_exact_names_come_first_then_prefixes_then_the_services_order()
    {
        var results = PlaceQuery.Results(
            [
                Hit("Parish", "New York", "United States", 43.4, -76.1),
                Hit("Le Paris", "Somewhere", "France", 45.0, 2.0),
                Hit("Paris", "Île-de-France", "France", 48.85, 2.35),
                Hit("Paris", "Texas", "United States", 33.66, -95.55),
            ],
            "paris");

        Assert.Equal(["Paris", "Paris", "Parish", "Le Paris"], results.Select(r => r.Name));
        Assert.Equal("Île-de-France, France", results[0].Detail);
    }

    [Fact]
    public void S2842_at_most_six_places_are_offered()
    {
        var hits = Enumerable.Range(0, 10).Select(i => Hit($"Town {i}", lat: i, lon: i));

        Assert.Equal(PlaceQuery.MaxResults, PlaceQuery.Results(hits, "town").Count);
    }

    [Fact]
    public void S2842_same_named_places_in_one_region_are_told_apart_by_county()
    {
        var results = PlaceQuery.Results(
            [
                Hit("Springfield", "Ohio", "United States", 39.92, -83.81, admin2: "Clark County"),
                Hit("Springfield", "Ohio", "United States", 41.0, -81.0, admin2: "Summit County"),
                Hit("Springfield", "Illinois", "United States", 39.8, -89.64, admin2: "Sangamon County"),
            ],
            "springfield");

        Assert.Equal("Clark County, Ohio, United States", results[0].Detail);
        Assert.Equal("Summit County, Ohio, United States", results[1].Detail);
        Assert.Equal("Illinois, United States", results[2].Detail);
        Assert.Equal("Springfield, Clark County, Ohio, United States", results[0].Label);
    }

    [Fact]
    public void S2842_same_named_places_with_nothing_else_to_tell_them_apart_show_coordinates()
    {
        var results = PlaceQuery.Results(
            [
                Hit("Newport", "Wales", "United Kingdom", 51.59, -2.99),
                Hit("Newport", "Wales", "United Kingdom", 52.02, -4.84),
            ],
            "newport");

        Assert.Equal("Wales, United Kingdom · 51.59, -2.99", results[0].Detail);
        Assert.Equal("Wales, United Kingdom · 52.02, -4.84", results[1].Detail);
    }

    [Fact]
    public void S2842_the_coordinates_are_kept_exactly()
    {
        var match = Assert.Single(PlaceQuery.Results([Hit("Accra", "Greater Accra", "Ghana", 5.556, -0.1969)], "accra"));

        Assert.Equal(5.556, match.Latitude);
        Assert.Equal(-0.1969, match.Longitude);
    }

    // ---- [S-2843] outcomes -----------------------------------------------------------------

    [Theory]
    [InlineData(FetchStatus.Ok, PlaceOutcome.Matches)]
    [InlineData(FetchStatus.Empty, PlaceOutcome.NoMatch)]
    [InlineData(FetchStatus.Offline, PlaceOutcome.Offline)]
    [InlineData(FetchStatus.Unreachable, PlaceOutcome.Failed)]
    [InlineData(FetchStatus.TimedOut, PlaceOutcome.Failed)]
    [InlineData(FetchStatus.RateLimited, PlaceOutcome.Failed)]
    [InlineData(FetchStatus.BadResponse, PlaceOutcome.Failed)]
    [InlineData(FetchStatus.Refused, PlaceOutcome.NotAName)]
    public void S2843_each_fetch_status_reads_as_one_search_outcome(FetchStatus status, PlaceOutcome expected)
        => Assert.Equal(expected, PlaceQuery.Outcome(status));

    // ---- [S-2845] cache and language -------------------------------------------------------

    [Fact]
    public void S2845_the_cache_key_ignores_case_and_carries_the_language()
    {
        Assert.Equal(PlaceQuery.CacheKey("Zurich", Locale.De), PlaceQuery.CacheKey("zURICH", Locale.De));
        Assert.NotEqual(PlaceQuery.CacheKey("Zurich", Locale.De), PlaceQuery.CacheKey("Zurich", Locale.En));
    }

    [Theory]
    [InlineData(Locale.En, "en")]
    [InlineData(Locale.De, "de")]
    [InlineData(Locale.Zh, "zh")]
    public void S2845_the_geocoder_is_asked_in_the_app_language(Locale locale, string code)
        => Assert.Equal(code, PlaceQuery.LanguageCode(locale));

    [Fact]
    public void S2845_an_answer_is_served_from_the_cache_the_second_time()
    {
        var cache = new PlaceLookupCache(4);
        IReadOnlyList<PlaceMatch> answer = PlaceQuery.Results([Hit("Zurich")], "zurich");

        cache.Put("k", answer);

        Assert.True(cache.TryGet("k", out var again));
        Assert.Same(answer, again);
    }

    [Fact]
    public void S2845_the_least_recently_used_answer_is_evicted_first()
    {
        var cache = new PlaceLookupCache(2);
        cache.Put("a", []);
        cache.Put("b", []);
        Assert.True(cache.TryGet("a", out _));

        cache.Put("c", []);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Theory]
    [InlineData(PlaceOutcome.Matches, true)]
    [InlineData(PlaceOutcome.NoMatch, true)]
    [InlineData(PlaceOutcome.Offline, false)]
    [InlineData(PlaceOutcome.Failed, false)]
    [InlineData(PlaceOutcome.NotAName, false)]
    public void S2845_only_answers_are_cached_never_failures(PlaceOutcome outcome, bool cached)
        => Assert.Equal(cached, PlaceQuery.IsCacheable(outcome));

    [Fact]
    public void S2842_issue_135_st_louis_with_its_period_is_searched_and_offered_in_chinese()
    {
        // Classic issue #135 (weather for St. Louis not found, app in Chinese). The period is not a
        // reason to refuse the query, and the geocoder's Chinese answer (names translated, one null
        // admin) is offered whole, the US city first as the service ranked it.
        var query = PlaceQuery.Normalise(" St.  Louis ");
        Assert.Equal("St. Louis", query);
        Assert.Equal(PlaceQueryStep.Search, PlaceQuery.Classify(query, submitted: false));

        var offered = PlaceQuery.Results(
            [
                Hit("圣路易斯", "密苏里州", "美国", 38.63, -90.20),
                Hit("St. Louis", "愛德華王子島", "加拿大", 46.88, -64.14),
                Hit("St. Louis, Mount", null, null, 12.1, -61.7),
            ],
            query);

        Assert.Equal(3, offered.Count);
        Assert.Contains(offered, p => p.Name == "圣路易斯" && p.Latitude == 38.63 && p.Longitude == -90.20);
    }
}

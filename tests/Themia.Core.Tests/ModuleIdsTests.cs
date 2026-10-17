using System.Text.Json;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6900] Module ids: camelCase, validated wherever one is made or read.</summary>
public sealed class ModuleIdsTests
{
    [Theory]
    [InlineData("clock")]
    [InlineData("weatherNow")]
    [InlineData("airplaneModeToggle")]
    [InlineData("rss2")]
    public void S6900_accepts_a_camel_case_id(string text)
    {
        Assert.True(ModuleIds.IsValid(text));
        Assert.True(WidgetId.TryParse(text, out var id));
        Assert.Equal(text, id.Value);
        Assert.Equal(text, new ProviderId(text).Value);
        Assert.Equal(text, new FeatureId(text).Value);
        Assert.Equal(text, new CadenceId(text).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Clock")]
    [InlineData("2clock")]
    [InlineData("weather-now")]
    [InlineData("weather_now")]
    [InlineData("weather now")]
    [InlineData("wétter")]
    public void S6900_refuses_anything_else(string? text)
    {
        Assert.False(ModuleIds.IsValid(text));
        Assert.False(WidgetId.TryParse(text, out var id));
        Assert.Null(id.Value);
        if (text is not null)
        {
            Assert.Throws<ArgumentException>(() => new WidgetId(text));
        }
    }

    [Fact]
    public void S6900_refuses_an_id_longer_than_the_limit()
    {
        Assert.True(ModuleIds.IsValid(new string('a', ModuleIds.MaxLength)));
        Assert.False(ModuleIds.IsValid(new string('a', ModuleIds.MaxLength + 1)));
    }

    [Fact]
    public void S6900_ids_compare_by_value()
    {
        Assert.Equal(new WidgetId("clock"), new WidgetId("clock"));
        Assert.NotEqual(new WidgetId("clock"), new WidgetId("cpu"));
        Assert.Equal("clock", new WidgetId("clock").ToString());
        Assert.Equal(string.Empty, default(WidgetId).ToString());
    }

    [Fact]
    public void S6900_an_id_is_written_as_its_string_and_a_malformed_one_is_refused_on_read()
    {
        var converter = new WidgetIdJsonConverter();
        var options = new JsonSerializerOptions { Converters = { converter } };

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            converter.Write(writer, new WidgetId("weatherNow"), options);
        }

        Assert.Equal("\"weatherNow\"", System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
        Assert.Equal(new WidgetId("weatherNow"), Read("\"weatherNow\""));
        Assert.Throws<JsonException>(() => Read("\"Weather Now\""));
        Assert.Throws<JsonException>(() => Read("12"));

        WidgetId Read(string json)
        {
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
            reader.Read();
            return converter.Read(ref reader, typeof(WidgetId), options);
        }
    }
}

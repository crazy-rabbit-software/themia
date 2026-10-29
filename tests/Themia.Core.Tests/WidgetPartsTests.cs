using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-667] What is left of the shared widget parts' metrics: a temperature's number and unit.</summary>
public sealed class WidgetPartsTests
{
    [Fact]
    public void S667_a_temperature_splits_into_its_number_and_unit()
    {
        Assert.Equal(("14", "°C"), WeatherReport.TemperatureParts(14.2, TemperatureUnit.Celsius));
        Assert.Equal(("-3", "°F"), WeatherReport.TemperatureParts(-2.6, TemperatureUnit.Fahrenheit));
        Assert.Equal(("0", "°C"), WeatherReport.TemperatureParts(-0.4, TemperatureUnit.Celsius));
        Assert.Equal(("--", string.Empty), WeatherReport.TemperatureParts(null, TemperatureUnit.Celsius));
        Assert.Equal(("--", string.Empty), WeatherReport.TemperatureParts(double.NaN, TemperatureUnit.Celsius));
    }
}

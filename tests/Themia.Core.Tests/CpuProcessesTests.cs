using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The processor meter's extras (S-1140..S-1150).</summary>
public sealed class CpuProcessesTests
{
    // ---- S-1142 per-core load ----------------------------------------------------------------

    // ---- S-1141 clock -------------------------------------------------------------------------

    // ---- S-1143 uptime ------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, 0, "0m")]
    [InlineData(0, 0, 59, "59m")]
    [InlineData(0, 1, 0, "1h 0m")]
    [InlineData(0, 23, 59, "23h 59m")]
    [InlineData(1, 0, 0, "1d 0h")]
    [InlineData(3, 4, 59, "3d 4h")]
    [InlineData(400, 2, 0, "400d 2h")]
    public void S1143_uptime_in_its_two_largest_units(int days, int hours, int minutes, string expected)
        => Assert.Equal(expected, CpuRules.Uptime(new TimeSpan(days, hours, minutes, 30)));

    [Fact]
    public void S1143_a_negative_span_reads_zero()
        => Assert.Equal("0m", CpuRules.Uptime(TimeSpan.FromMinutes(-5)));

    // ---- S-1146 / S-1147 sensors --------------------------------------------------------------

    private const string MonitorTree = """
        {"id":0,"Text":"Sensor","Value":"","Children":[
          {"id":1,"Text":"DESKTOP","Value":"","ImageURL":"images_icon/computer.png","Children":[
            {"id":2,"Text":"ASUS PRIME","Value":"","ImageURL":"images_icon/mainboard.png","Children":[
              {"id":3,"Text":"Nuvoton","Value":"","ImageURL":"images_icon/chip.png","Children":[
                {"id":4,"Text":"Fans","Value":"","ImageURL":"images_icon/fan.png","Children":[
                  {"id":5,"Text":"CPU Fan","Value":"1180 RPM","ImageURL":"images/transparent.png","Children":[]},
                  {"id":6,"Text":"Chassis Fan #1","Value":"2400 RPM","ImageURL":"images/transparent.png","Children":[]}]},
                {"id":7,"Text":"Temperatures","Value":"","Children":[
                  {"id":8,"Text":"System","Value":"88,0 °C","Children":[]}]}]}]},
            {"id":9,"Text":"AMD Ryzen 7 5800X","Value":"","ImageURL":"images_icon/cpu.png","Children":[
              {"id":10,"Text":"Temperatures","Value":"","ImageURL":"images_icon/temperature.png","Children":[
                {"id":11,"Text":"Core (Tctl/Tdie)","Value":"61,4 °C","Children":[]},
                {"id":12,"Text":"CCD1 (Tdie)","Value":"58,0 °C","Children":[]},
                {"id":13,"Text":"Core #1","Value":"72,0 °C","Children":[]}]}]}]}]}
        """;

    // ---- S-1149 config ------------------------------------------------------------------------

    // ---- S-1150 per-core strip ----------------------------------------------------------------
}

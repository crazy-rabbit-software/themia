using System.Buffers.Binary;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2434]..[S-2436] Core Temp's and HWiNFO's shared memory, parsed from byte fixtures laid out as their headers say.</summary>
public sealed class CpuSensorMemoryTests
{
    // ---- [S-2435] Core Temp --------------------------------------------------------------------

    /// <summary>A Core Temp struct (CoreTempSharedDataEx's prefix): cores per CPU, temps CPU-major, TjMax per CPU.</summary>
    private static byte[] CoreTemp(uint cores, uint cpus, float[] temps, uint[]? tjMax = null, bool fahrenheit = false, bool delta = false, int length = 2688)
    {
        var bytes = new byte[Math.Max(length, 2688)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1536), cores);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1540), cpus);

        for (var i = 0; i < temps.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(1544 + (4 * i)), temps[i]);
        }

        for (var i = 0; i < (tjMax?.Length ?? 0); i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1024 + (4 * i)), tjMax![i]);
        }

        Encoding.ASCII.GetBytes("Intel Core i7-8700K").CopyTo(bytes.AsSpan(2584));
        bytes[2684] = fahrenheit ? (byte)1 : (byte)0;
        bytes[2685] = delta ? (byte)1 : (byte)0;
        return bytes[..length];
    }

    [Fact]
    public void S2435_core_temp_is_the_hottest_core_of_every_cpu()
    {
        var read = CpuSensorMemory.ParseCoreTemp(CoreTemp(4, 2, [41, 47, 44, 39, 52, 40, 0, float.NaN]));

        Assert.Null(read.Fault);
        Assert.Equal(52, read.Celsius!.Value, 3);
    }

    [Fact]
    public void S2435_core_temp_in_fahrenheit_is_converted()
        => Assert.Equal(50, CpuSensorMemory.ParseCoreTemp(CoreTemp(2, 1, [113, 122], fahrenheit: true)).Celsius!.Value, 3);

    [Fact]
    public void S2435_core_temp_distance_to_tjmax_is_subtracted_from_its_cpus_tjmax()
    {
        // CPU 0 (TjMax 100): 40 and 30 below; CPU 1 (TjMax 90): 50 below. Hottest is 70 °C.
        var read = CpuSensorMemory.ParseCoreTemp(CoreTemp(2, 2, [40, 30, 50, 55], tjMax: [100, 90], delta: true));

        Assert.Equal(70, read.Celsius!.Value, 3);
    }

    [Fact]
    public void S2435_core_temp_distance_in_fahrenheit_is_refused_not_guessed()
    {
        var read = CpuSensorMemory.ParseCoreTemp(CoreTemp(2, 1, [40, 30], tjMax: [100], fahrenheit: true, delta: true));

        Assert.Null(read.Celsius);
        Assert.NotNull(read.Fault);
    }

    [Fact]
    public void S2435_a_truncated_core_temp_struct_is_corrupt()
    {
        var read = CpuSensorMemory.ParseCoreTemp(CoreTemp(2, 1, [40, 30], length: CpuSensorMemory.CoreTempLength - 1));

        Assert.Null(read.Celsius);
        Assert.Contains("bytes", read.Fault, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(257u, 1u)]
    [InlineData(4u, 129u)]
    [InlineData(32u, 16u)] // 512 cores, past fTemp[256]
    [InlineData(uint.MaxValue, uint.MaxValue)]
    public void S2435_absurd_core_temp_counts_are_corrupt(uint cores, uint cpus)
    {
        var read = CpuSensorMemory.ParseCoreTemp(CoreTemp(cores, cpus, []));

        Assert.Null(read.Celsius);
        Assert.NotNull(read.Fault);
    }

    [Fact]
    public void S2435_core_temp_before_its_first_read_is_absent_not_corrupt()
    {
        var read = CpuSensorMemory.ParseCoreTemp(new byte[2688]);

        Assert.Equal(SensorMemoryRead.Nothing, read);
    }

    [Fact]
    public void S2435_implausible_core_temp_readings_are_left_out()
        => Assert.Null(CpuSensorMemory.ParseCoreTemp(CoreTemp(2, 1, [0, 400])).Celsius);

    // ---- [S-2436] HWiNFO -----------------------------------------------------------------------

    private const int ReadingV1 = 316;
    private const int ReadingV2 = 460;
    private const int SensorV2 = 392;

    private sealed record Reading(uint Type, string Label, double Value, string Unit = "°C", uint Sensor = 0);

    /// <summary>An HWiNFO memory: header, the sensor array, then the reading array.</summary>
    private static byte[] HwInfo(Reading[] readings, int readingSize = ReadingV2, uint sensors = 2, string signature = "HWiS", int? truncateTo = null)
    {
        const int header = 48;
        var sensorOffset = header;
        var readingOffset = sensorOffset + (SensorV2 * (int)sensors);
        var bytes = new byte[readingOffset + (readingSize * readings.Length)];

        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), readingSize == ReadingV2 ? 2u : 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), (uint)sensorOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), SensorV2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), sensors);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), (uint)readingOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), (uint)readingSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), (uint)readings.Length);

        for (var s = 0; s < sensors; s++)
        {
            Encoding.ASCII.GetBytes(s == 0 ? "CPU [#0]: Intel Core i9-12900K: DTS" : "ASUS ROG STRIX Z690-A (Nuvoton NCT6798D)").CopyTo(bytes, sensorOffset + (s * SensorV2) + 8);
        }

        for (var i = 0; i < readings.Length; i++)
        {
            var r = readings[i];
            var at = readingOffset + (i * readingSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), r.Type);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 4), r.Sensor);
            Encoding.Latin1.GetBytes(r.Label).CopyTo(bytes, at + 12);
            Encoding.Latin1.GetBytes("renamed by the user").CopyTo(bytes, at + 140);
            Encoding.Latin1.GetBytes(r.Unit).CopyTo(bytes, at + 268);
            BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(at + 284), r.Value);

            if (readingSize >= ReadingV2)
            {
                Encoding.UTF8.GetBytes(r.Unit).CopyTo(bytes, at + 444);
            }
        }

        return truncateTo is { } t ? bytes[..t] : bytes;
    }

    private static readonly Reading[] Intel =
    [
        new(1, "Core Max", 71),
        new(1, "CPU Package", 64),
        new(5, "CPU Package Power", 88, "W"),
        new(1, "CPU", 45, Sensor: 1), // the board's zone
    ];

    [Fact]
    public void S2436_hwinfo_prefers_the_package_over_the_hottest_core_and_the_board()
    {
        var read = CpuSensorMemory.ParseHwInfo(HwInfo(Intel));

        Assert.Null(read.Fault);
        Assert.Equal(64, read.Celsius!.Value, 3);
    }

    [Fact]
    public void S2436_hwinfo_reads_amds_tctl_tdie_and_falls_back_to_core_max()
    {
        Assert.Equal(58, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU (Tctl/Tdie)", 58), new(1, "Core Max", 61)])).Celsius!.Value, 3);
        Assert.Equal(61, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "Core Max", 61), new(1, "CPU", 40, Sensor: 1)])).Celsius!.Value, 3);
    }

    [Fact]
    public void S2436_hwinfo_without_a_cpu_reading_is_absent()
        => Assert.Equal(SensorMemoryRead.Nothing, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU", 40, Sensor: 1), new(1, "GPU Temperature", 50)])));

    [Fact]
    public void S2436_two_sockets_give_the_hotter_package()
        => Assert.Equal(66, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU Package", 60), new(1, "CPU Package", 66, Sensor: 1)])).Celsius!.Value, 3);

    [Fact]
    public void S2436_hwinfo_in_fahrenheit_is_converted_in_both_layouts()
    {
        Assert.Equal(60, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU Package", 140, "°F")])).Celsius!.Value, 3);
        Assert.Equal(60, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU Package", 140, "°F")], readingSize: ReadingV1)).Celsius!.Value, 3);
        Assert.Equal(60, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU Package", 60)], readingSize: ReadingV1)).Celsius!.Value, 3);
    }

    [Fact]
    public void S2436_hwinfo_switched_off_is_absent_not_corrupt()
    {
        var dead = HwInfo(Intel, signature: "DEAD");

        Assert.Equal(SensorMemoryRead.Nothing, CpuSensorMemory.ParseHwInfo(dead));
        // The host copies only the header when it claims no length: that alone still reads as absent.
        Assert.Null(CpuSensorMemory.HwInfoClaimedLength(dead));
        Assert.Equal(SensorMemoryRead.Nothing, CpuSensorMemory.ParseHwInfo(dead[..CpuSensorMemory.HwInfoHeaderLength]));
    }

    [Fact]
    public void S2436_a_wrong_signature_is_corrupt()
    {
        var read = CpuSensorMemory.ParseHwInfo(HwInfo(Intel, signature: "NOPE"));

        Assert.Null(read.Celsius);
        Assert.Contains("signature", read.Fault, StringComparison.Ordinal);
        Assert.Null(CpuSensorMemory.HwInfoClaimedLength(HwInfo(Intel, signature: "NOPE")));
    }

    [Fact]
    public void S2436_memory_shorter_than_the_header_claims_is_corrupt()
    {
        var full = HwInfo(Intel);
        var read = CpuSensorMemory.ParseHwInfo(full[..^1]);

        Assert.Null(read.Celsius);
        Assert.NotNull(read.Fault);
        Assert.Equal(full.Length, CpuSensorMemory.HwInfoClaimedLength(full));
        Assert.NotNull(CpuSensorMemory.ParseHwInfo(full[..20]).Fault);
        Assert.NotNull(CpuSensorMemory.ParseHwInfo([]).Fault);
    }

    [Theory]
    [InlineData(40, 70_000u)]       // dwNumReadingElements
    [InlineData(28, 70_000u)]       // dwNumSensorElements
    [InlineData(36, 100u)]          // dwSizeOfReadingElement under Value's end
    [InlineData(32, 8u)]            // the readings inside the header
    [InlineData(32, uint.MaxValue)] // past any view
    public void S2436_absurd_hwinfo_headers_are_corrupt(int field, uint value)
    {
        var bytes = HwInfo(Intel);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field), value);

        var read = CpuSensorMemory.ParseHwInfo(bytes);

        Assert.Null(read.Celsius);
        Assert.NotNull(read.Fault);
    }

    [Fact]
    public void S2436_a_reading_of_a_sensor_past_the_array_is_skipped()
        => Assert.Equal(45, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU Package", 64, Sensor: 9), new(1, "Core Max", 45)])).Celsius!.Value, 3);

    [Fact]
    public void S2436_implausible_and_unitless_readings_are_left_out()
        => Assert.Equal(SensorMemoryRead.Nothing, CpuSensorMemory.ParseHwInfo(HwInfo([new(1, "CPU Package", 255), new(1, "Core Max", 50, "%")])));

    // ---- [S-2434] the label --------------------------------------------------------------------

    [Fact]
    public void S2434_the_fact_says_package_or_thermal_zone_by_its_source()
    {
        string Label(CpuTemperatureSource source)
            => Assert.Single(CpuDetailRules.Sheet(Locale.En, new CpuFigures(Mhz: 3000, Temperature: new CpuTemperature(55, source))).Facts, f => f.Key == "temperature").Label;

        Assert.Equal("CPU package", Label(CpuTemperatureSource.CoreTemp));
        Assert.Equal("CPU package", Label(CpuTemperatureSource.HwInfo));
        Assert.Equal("Thermal zone", Label(CpuTemperatureSource.ThermalZone));
        Assert.Equal("CPU-Package", Assert.Single(CpuDetailRules.Sheet(Locale.De, new CpuFigures(Mhz: 3000, Temperature: new CpuTemperature(55, CpuTemperatureSource.HwInfo))).Facts, f => f.Key == "temperature").Label);
    }
}

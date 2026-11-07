using System.Buffers.Binary;
using System.Text;

namespace Themia.Core.Widgets;

/// <summary>[S-2434] Where the CPU details' temperature came from: it decides the fact's label.</summary>
public enum CpuTemperatureSource
{
    /// <summary>The hottest ACPI thermal zone ([S-2407]): the board's zone, not the package.</summary>
    ThermalZone,

    /// <summary>Core Temp's shared memory ([S-2435]): the hottest core's sensor.</summary>
    CoreTemp,

    /// <summary>HWiNFO's shared memory ([S-2436]): the package (or Tctl/Tdie) reading.</summary>
    HwInfo,
}

/// <summary>[S-2434] One temperature for the CPU details, in °C, and where it came from.</summary>
public readonly record struct CpuTemperature(double Celsius, CpuTemperatureSource Source)
{
    /// <summary>True when the reading is the processor's own sensor, not a board zone.</summary>
    public bool IsPackage => Source is not CpuTemperatureSource.ThermalZone;
}

/// <summary>
/// One parse of a tool's shared memory. <see cref="Celsius"/> is null when the tool gives nothing
/// usable; <see cref="Fault"/> says why when that is because the bytes are wrong (truncated, a bad
/// signature, absurd counts), and is null when the tool simply has no reading right now ("absent").
/// </summary>
public readonly record struct SensorMemoryRead(double? Celsius, string? Fault = null)
{
    public static SensorMemoryRead Nothing => default;

    public static SensorMemoryRead Corrupt(string fault) => new(null, fault);
}

/// <summary>
/// [S-2434]..[S-2436] The bytes Core Temp and HWiNFO publish in their named shared memory, parsed.
/// Pure: the host maps the view read-only, copies at most what the header claims (and never past
/// the view) and hands the copy here; every offset is checked against the copy's length.
/// </summary>
public static class CpuSensorMemory
{
    // ---- Core Temp -----------------------------------------------------------------------------

    /// <summary>Core Temp 1.x's mapping (struct version 2, <c>CoreTempSharedDataEx</c>).</summary>
    public const string CoreTempMapping = "CoreTempMappingObjectEx";

    /// <summary>Older Core Temp releases publish only the original struct under this name.</summary>
    public const string CoreTempLegacyMapping = "CoreTempMappingObject";

    // The layout, from alcpu.com/CoreTemp/developers.html ("CoreTempSharedDataEx", 4-byte member
    // alignment) and Rainmeter's Library/CoreTemp/SharedMem.h (#pragma pack(4)). Every field read
    // here is in the original struct, which is the Ex struct's prefix, so both mappings parse alike:
    //   unsigned int uiLoad[256];    0
    //   unsigned int uiTjMax[128];   1024   per CPU (package)
    //   unsigned int uiCoreCnt;      1536   cores per CPU
    //   unsigned int uiCPUCnt;       1540
    //   float fTemp[256];            1544   per core, CPU-major: [cpu * uiCoreCnt + core]
    //   float fVID, fCPUSpeed, fFSBSpeed, fMultiplier;   2568..2584
    //   char sCPUName[100];          2584
    //   unsigned char ucFahrenheit;  2684   fTemp is in °F
    //   unsigned char ucDeltaToTjMax;2685   fTemp is the distance below TjMax
    private const int CoreTempTjMax = 1024;
    private const int CoreTempCoreCount = 1536;
    private const int CoreTempCpuCount = 1540;
    private const int CoreTempTemps = 1544;
    private const int CoreTempFahrenheit = 2684;
    private const int CoreTempDelta = 2685;
    private const int CoreTempMaxCores = 256;
    private const int CoreTempMaxCpus = 128;

    /// <summary>The bytes the Core Temp parse reads: through <c>ucDeltaToTjMax</c>.</summary>
    public const int CoreTempLength = CoreTempDelta + 1;

    /// <summary>
    /// [S-2435] The hottest core Core Temp reports, in °C. Fahrenheit is converted; "distance to
    /// TjMax" is subtracted from its CPU's TjMax. Both at once is refused: Core Temp's documentation
    /// does not say whether TjMax is then in °F, and a guess would be a wrong figure, not a missing one.
    /// </summary>
    public static SensorMemoryRead ParseCoreTemp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < CoreTempLength)
        {
            return SensorMemoryRead.Corrupt($"Core Temp's memory is {bytes.Length} bytes, under the {CoreTempLength} its layout needs");
        }

        var cores = BinaryPrimitives.ReadUInt32LittleEndian(bytes[CoreTempCoreCount..]);
        var cpus = BinaryPrimitives.ReadUInt32LittleEndian(bytes[CoreTempCpuCount..]);

        if (cores == 0 || cpus == 0)
        {
            // Core Temp has mapped its memory but not read the processor yet.
            return SensorMemoryRead.Nothing;
        }

        if (cores > CoreTempMaxCores || cpus > CoreTempMaxCpus || (ulong)cores * cpus > CoreTempMaxCores)
        {
            return SensorMemoryRead.Corrupt($"Core Temp reports {cpus} processors of {cores} cores, past its arrays");
        }

        var fahrenheit = bytes[CoreTempFahrenheit] != 0;
        var delta = bytes[CoreTempDelta] != 0;

        if (fahrenheit && delta)
        {
            return SensorMemoryRead.Corrupt("Core Temp shows the distance to TjMax in Fahrenheit, which has no documented TjMax unit");
        }

        double? hottest = null;

        for (var cpu = 0; cpu < cpus; cpu++)
        {
            var tjMax = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(CoreTempTjMax + (4 * cpu))..]);

            for (var core = 0; core < cores; core++)
            {
                double value = BinaryPrimitives.ReadSingleLittleEndian(bytes[(CoreTempTemps + (4 * ((cpu * (int)cores) + core)))..]);

                if (!double.IsFinite(value))
                {
                    continue;
                }

                if (delta)
                {
                    if (tjMax is 0 or > (uint)CpuDetailRules.MaxCelsius)
                    {
                        continue;
                    }

                    value = tjMax - value;
                }
                else if (fahrenheit)
                {
                    value = (value - 32) * 5 / 9;
                }

                if (value is >= CpuDetailRules.MinCelsius and <= CpuDetailRules.MaxCelsius && (hottest is null || value > hottest))
                {
                    hottest = value;
                }
            }
        }

        return new SensorMemoryRead(hottest);
    }

    // ---- HWiNFO --------------------------------------------------------------------------------

    /// <summary>HWiNFO's sensor mapping. It exists only while "Shared Memory Support" is on.</summary>
    public const string HwInfoMapping = @"Global\HWiNFO_SENS_SM2";

    // The layout, from HWiNFO's hwisenssm2.h (the shared-memory SDK HWiNFO publishes; #pragma pack(1)):
    //   HWiNFO_SENSORS_SHARED_MEM2
    //     DWORD dwSignature;   0   "HWiS" active, "DEAD" when shared memory was switched off
    //     DWORD dwVersion;     4   2 = UTF-8 strings appended (v7.33+)
    //     DWORD dwRevision;    8
    //     __time64_t poll_time;12
    //     DWORD dwOffsetOfSensorSection, dwSizeOfSensorElement, dwNumSensorElements;   20, 24, 28
    //     DWORD dwOffsetOfReadingSection, dwSizeOfReadingElement, dwNumReadingElements; 32, 36, 40
    //     DWORD dwPollingPeriod;  44 (revision 1+)
    //   HWiNFO_SENSORS_READING_ELEMENT (the stride is dwSizeOfReadingElement; fields grow at its end)
    //     SENSOR_READING_TYPE tReading;  0   1 = SENSOR_TYPE_TEMP
    //     DWORD dwSensorIndex;           4   into the sensor array
    //     DWORD dwReadingID;             8
    //     char szLabelOrig[128];         12  the English label, whatever the user renamed it to
    //     char szLabelUser[128];         140
    //     char szUnit[16];               268 ANSI ("°C" / "°F")
    //     double Value, ValueMin, ValueMax, ValueAvg;   284, 292, 300, 308
    //     BYTE utfLabelUser[128];        316 (version 2+)
    //     BYTE utfUnit[16];              444 (version 2+), UTF-8
    private const uint HwInfoActive = 0x53695748; // "HWiS"
    private const uint HwInfoDead = 0x44414544;   // "DEAD"
    private const int HwInfoSensorOffset = 20;
    private const int HwInfoSensorSize = 24;
    private const int HwInfoSensorCount = 28;
    private const int HwInfoReadingOffset = 32;
    private const int HwInfoReadingSize = 36;
    private const int HwInfoReadingCount = 40;
    private const int ReadingType = 0;
    private const int ReadingSensor = 4;
    private const int ReadingLabel = 12;
    private const int ReadingUnit = 268;
    private const int ReadingValue = 284;
    private const int ReadingUtfUnit = 444;
    private const int LabelLength = 128;
    private const int UnitLength = 16;
    private const uint TypeTemperature = 1;

    /// <summary>The header's bytes through <c>dwNumReadingElements</c>: what <see cref="HwInfoClaimedLength"/> needs.</summary>
    public const int HwInfoHeaderLength = 44;

    /// <summary>The smallest reading element that carries <c>Value</c> (version 1).</summary>
    public const int HwInfoReadingMinimum = ReadingValue + 8;

    /// <summary>A sensor element's smallest size: ids and the two names (version 1).</summary>
    public const int HwInfoSensorMinimum = 8 + (2 * LabelLength);

    /// <summary>More elements than any machine has: a header claiming more is corrupt.</summary>
    public const int HwInfoMaxElements = 65_536;

    /// <summary>The most any parse copies: a header claiming more is corrupt.</summary>
    public const long HwInfoMaxLength = 64L << 20;

    /// <summary>
    /// [S-2436] The readings HWiNFO names the processor's own temperature, best first, matched on
    /// the English label (<c>szLabelOrig</c>) so a user's rename or HWiNFO's language do not matter.
    /// Intel's package sensor; AMD's control temperature (Zen 2+ "Tctl/Tdie", older "Tdie" or "Tctl");
    /// AMD's die average; and the hottest core as a last resort. A board's "CPU" zone is not one.
    /// </summary>
    public static readonly IReadOnlyList<string> HwInfoCpuLabels =
    [
        "CPU Package",
        "CPU (Tctl/Tdie)",
        "CPU (Tdie)",
        "CPU Die (average)",
        "CPU (Tctl)",
        "Core Max",
    ];

    /// <summary>
    /// The bytes HWiNFO's header says its memory spans (both arrays' ends), so the host copies that
    /// and no more; null when the header itself is short, inactive, foreign or absurd (the parse
    /// then says which).
    /// </summary>
    public static long? HwInfoClaimedLength(ReadOnlySpan<byte> header)
        => HwInfoLayout(header, out _) is { } layout ? layout.End : null;

    /// <summary>[S-2436] HWiNFO's processor temperature in °C: the best-ranked label's hottest reading.</summary>
    public static SensorMemoryRead ParseHwInfo(ReadOnlySpan<byte> bytes)
    {
        if (HwInfoLayout(bytes, out var fault) is not { } layout)
        {
            return fault is null ? SensorMemoryRead.Nothing : SensorMemoryRead.Corrupt(fault);
        }

        if (layout.End > bytes.Length)
        {
            return SensorMemoryRead.Corrupt($"HWiNFO's header claims {layout.End} bytes and only {bytes.Length} were readable");
        }

        var bestRank = int.MaxValue;
        double? best = null;

        for (var i = 0; i < layout.Readings; i++)
        {
            var reading = bytes.Slice((int)(layout.ReadingOffset + ((long)i * layout.ReadingSize)), layout.ReadingSize);

            if (BinaryPrimitives.ReadUInt32LittleEndian(reading[ReadingType..]) != TypeTemperature
                || BinaryPrimitives.ReadUInt32LittleEndian(reading[ReadingSensor..]) >= layout.Sensors)
            {
                continue;
            }

            var rank = Rank(reading.Slice(ReadingLabel, LabelLength));

            if (rank > bestRank || rank < 0)
            {
                continue;
            }

            if (Celsius(reading, layout.ReadingSize) is not { } celsius)
            {
                continue;
            }

            if (rank < bestRank || best is null || celsius > best)
            {
                bestRank = rank;
                best = celsius;
            }
        }

        return new SensorMemoryRead(best);
    }

    private readonly record struct Layout(long SensorOffset, int SensorSize, int Sensors, long ReadingOffset, int ReadingSize, int Readings)
    {
        public long End => Math.Max(SensorOffset + ((long)SensorSize * Sensors), ReadingOffset + ((long)ReadingSize * Readings));
    }

    /// <summary>The header's two arrays, checked; null with a fault when corrupt, null without one when inactive.</summary>
    private static Layout? HwInfoLayout(ReadOnlySpan<byte> bytes, out string? fault)
    {
        fault = null;

        if (bytes.Length < 4)
        {
            fault = $"HWiNFO's memory is {bytes.Length} bytes, under its header";
            return null;
        }

        var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes);

        if (signature == HwInfoDead)
        {
            // HWiNFO switched shared memory off (the free version does after 12 hours) and left the mapping.
            return null;
        }

        if (signature != HwInfoActive)
        {
            fault = $"HWiNFO's memory has the signature 0x{signature:X8}, not \"HWiS\"";
            return null;
        }

        if (bytes.Length < HwInfoHeaderLength)
        {
            fault = $"HWiNFO's memory is {bytes.Length} bytes, under its {HwInfoHeaderLength}-byte header";
            return null;
        }

        var sensorOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HwInfoSensorOffset..]);
        var sensorSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HwInfoSensorSize..]);
        var sensors = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HwInfoSensorCount..]);
        var readingOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HwInfoReadingOffset..]);
        var readingSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HwInfoReadingSize..]);
        var readings = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HwInfoReadingCount..]);

        if (sensors > HwInfoMaxElements || readings > HwInfoMaxElements)
        {
            fault = $"HWiNFO claims {sensors} sensors and {readings} readings";
            return null;
        }

        if (readingSize < HwInfoReadingMinimum || readingSize > 1 << 16 || sensorSize < HwInfoSensorMinimum || sensorSize > 1 << 16)
        {
            fault = $"HWiNFO's elements are {sensorSize} and {readingSize} bytes, not the layout this reads";
            return null;
        }

        if (sensorOffset < HwInfoHeaderLength || readingOffset < HwInfoHeaderLength)
        {
            fault = "HWiNFO's arrays start inside its header";
            return null;
        }

        var layout = new Layout(sensorOffset, (int)sensorSize, (int)sensors, readingOffset, (int)readingSize, (int)readings);

        if (layout.End > HwInfoMaxLength)
        {
            fault = $"HWiNFO claims {layout.End} bytes of memory";
            return null;
        }

        return layout;
    }

    /// <summary>The label's place in <see cref="HwInfoCpuLabels"/>, or -1.</summary>
    private static int Rank(ReadOnlySpan<byte> label)
    {
        var text = Text(label, Encoding.Latin1);

        for (var i = 0; i < HwInfoCpuLabels.Count; i++)
        {
            if (string.Equals(text, HwInfoCpuLabels[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The reading in °C, by its unit (UTF-8 when the element has it, else ANSI); null for an implausible one.</summary>
    private static double? Celsius(ReadOnlySpan<byte> reading, int size)
    {
        var value = BinaryPrimitives.ReadDoubleLittleEndian(reading[ReadingValue..]);

        if (!double.IsFinite(value))
        {
            return null;
        }

        var unit = size >= ReadingUtfUnit + UnitLength
            ? Text(reading.Slice(ReadingUtfUnit, UnitLength), Encoding.UTF8)
            : Text(reading.Slice(ReadingUnit, UnitLength), Encoding.Latin1);

        // The degree sign differs by code page; the letter at the end does not.
        double? celsius = unit.EndsWith('C') ? value : unit.EndsWith('F') ? (value - 32) * 5 / 9 : null;
        return celsius is >= CpuDetailRules.MinCelsius and <= CpuDetailRules.MaxCelsius ? celsius : null;
    }

    /// <summary>A NUL-terminated fixed field as text, trimmed.</summary>
    private static string Text(ReadOnlySpan<byte> field, Encoding encoding)
    {
        var end = field.IndexOf((byte)0);
        return encoding.GetString(end < 0 ? field : field[..end]).Trim();
    }
}

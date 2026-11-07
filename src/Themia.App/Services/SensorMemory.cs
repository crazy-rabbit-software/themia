using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2434]..[S-2436] The processor temperature Core Temp or HWiNFO publish in named shared memory,
/// read for the CPU details. Each read opens the mapping read-only, maps it, copies what the
/// layout (Core Temp) or the header (HWiNFO) says it spans and never more than the view holds, and
/// closes both handles before parsing the copy (<see cref="CpuSensorMemory"/>, pure and tested).
/// <para>
/// Nothing is held between reads: a mapping kept open would outlive the tool and serve its last
/// figures forever. Opening a name that does not exist is one cheap kernel call, made only on the
/// processor source's own due tick ([S-2501]), never retried faster. The tools' mutexes are not
/// taken: waiting on another program's lock is timing, and a copy torn mid-update gives at worst
/// one old reading beside new ones, every offset still checked against the copy.
/// </para>
/// </summary>
internal static unsafe partial class SensorMemory
{
    private const string Scope = "cpu";

    private const uint FileMapRead = 0x0004;
    private const int ErrorFileNotFound = 2;

    private static byte[] _buffer = [];
    private static readonly Dictionary<string, string> _faults = new(StringComparer.Ordinal);
    private static CpuTemperatureSource? _lastSource;

    /// <summary>Core Temp, else HWiNFO; null when neither has a reading.</summary>
    public static CpuTemperature? Read()
    {
        var temperature = ReadCoreTemp() is { } coreTemp
            ? new CpuTemperature(coreTemp, CpuTemperatureSource.CoreTemp)
            : ReadHwInfo() is { } hwInfo
                ? new CpuTemperature(hwInfo, CpuTemperatureSource.HwInfo)
                : (CpuTemperature?)null;

        Report(temperature?.Source);
        return temperature;
    }

    /// <summary>Called when the last CPU details widget goes: the copy buffer and the logged state go with it.</summary>
    public static void Release()
    {
        _buffer = [];
        _faults.Clear();
        _lastSource = null;
    }

    private static double? ReadCoreTemp()
    {
        // The Ex mapping when Core Temp publishes it; an older Core Temp has only the original one.
        foreach (var name in (ReadOnlySpan<string>)[CpuSensorMemory.CoreTempMapping, CpuSensorMemory.CoreTempLegacyMapping])
        {
            if (Copy(name, static _ => CpuSensorMemory.CoreTempLength) is { } length)
            {
                return Parsed(name, CpuSensorMemory.ParseCoreTemp(_buffer.AsSpan(0, length)));
            }
        }

        return null;
    }

    private static double? ReadHwInfo()
    {
        var name = CpuSensorMemory.HwInfoMapping;
        var length = Copy(name, static header => CpuSensorMemory.HwInfoClaimedLength(header) ?? Math.Min(header.Length, CpuSensorMemory.HwInfoHeaderLength));
        return length is { } l ? Parsed(name, CpuSensorMemory.ParseHwInfo(_buffer.AsSpan(0, l))) : null;
    }

    private static double? Parsed(string name, SensorMemoryRead read)
    {
        if (read.Fault is { } fault)
        {
            // Once per distinct fault: the same corrupt memory on every tick is one problem.
            if (!_faults.TryGetValue(name, out var last) || last != fault)
            {
                _faults[name] = fault;
                Log.Warn(Scope, $"{name}: {fault}; not used for the temperature");
            }
        }
        else
        {
            _faults.Remove(name);
        }

        return read.Celsius;
    }

    private static void Report(CpuTemperatureSource? source)
    {
        if (source != _lastSource)
        {
            _lastSource = source;
            Log.Info(Scope, source is { } s ? $"the CPU temperature is read from {s}" : "no temperature tool's shared memory has a reading");
        }
    }

    /// <summary>
    /// Copies the mapping named <paramref name="name"/> into <see cref="_buffer"/>: as many bytes as
    /// <paramref name="lengthOf"/> says (given what the view holds), never past the view. Null when
    /// the mapping does not exist or cannot be read (the latter logged once).
    /// </summary>
    private static int? Copy(string name, Func<ReadOnlySpan<byte>, long> lengthOf)
    {
        using var mapping = OpenFileMappingW(FileMapRead, false, name);

        if (mapping.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();

            if (error != ErrorFileNotFound)
            {
                Parsed(name, SensorMemoryRead.Corrupt($"OpenFileMapping failed ({error})"));
            }

            return null;
        }

        using var view = MapViewOfFile(mapping, FileMapRead, 0, 0, 0);

        if (view.IsInvalid)
        {
            Parsed(name, SensorMemoryRead.Corrupt($"MapViewOfFile failed ({Marshal.GetLastPInvokeError()})"));
            return null;
        }

        // The view handle is held by the using above for as long as the pointer is read.
        var start = (byte*)view.DangerousGetHandle();
        MemoryBasicInformation info;

        if (VirtualQuery(start, &info, (nuint)sizeof(MemoryBasicInformation)) == 0)
        {
            Parsed(name, SensorMemoryRead.Corrupt($"VirtualQuery failed ({Marshal.GetLastPInvokeError()})"));
            return null;
        }

        // The view spans the whole section, rounded up to a page: what is readable.
        var viewLength = (long)Math.Min((ulong)info.RegionSize, (ulong)CpuSensorMemory.HwInfoMaxLength);
        var wanted = lengthOf(new ReadOnlySpan<byte>(start, (int)viewLength));
        var length = (int)Math.Clamp(wanted, 0, viewLength);

        if (_buffer.Length < length)
        {
            _buffer = new byte[length];
        }

        new ReadOnlySpan<byte>(start, length).CopyTo(_buffer);
        return length;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "OpenFileMappingW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeMemoryMappedFileHandle OpenFileMappingW(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeMemoryMappedViewHandle MapViewOfFile(SafeMemoryMappedFileHandle mapping, uint access, uint offsetHigh, uint offsetLow, nuint bytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nuint VirtualQuery(void* address, MemoryBasicInformation* buffer, nuint length);

    /// <summary>MEMORY_BASIC_INFORMATION (64-bit): only the region's size is read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }
}

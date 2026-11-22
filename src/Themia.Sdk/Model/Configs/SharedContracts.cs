using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-433] [S-11752] A GPU atom's config: which adapter it reads, as its position in DXGI's list (software adapters left out).</summary>
public interface IGpuAdapter
{
    int Adapter { get; set; }
}

/// <summary>
/// [S-14960] [S-11752] A network kind's config that reads one adapter's figures (owner, 2026-11-21: "local address you should
/// be able to select the network adapter, for the similar widgets the same"): the adapter's id (Windows' GUID, a rename is
/// still the same adapter), null for Automatic (the busiest adapter's address and link, every counted adapter's traffic).
/// </summary>
public interface IHoldsAdapter
{
    /// <summary>The adapter's id, or null for Automatic.</summary>
    string? Adapter { get; set; }

    /// <summary>The adapter's name as last seen ("Wi-Fi"), for an adapter that is not connected now.</summary>
    string? AdapterName { get; set; }
}

/// <summary>[S-11752] A drive atom's config (the space on one drive, its facts): the drive's root, empty for the system drive.</summary>
public interface IDriveChoice
{
    string Drive { get; set; }
}

/// <summary>
/// [S-15513] A widget about one day: which day from today it shows (0 today, 1 tomorrow). Only a kind
/// whose reading means something for a day ahead says so (<see cref="TakesDay"/>): the present's readings
/// (feels like, humidity) hold no day.
/// </summary>
public interface IDayChoice
{
    /// <summary>The day from today, 0 or more.</summary>
    int Day { get; set; }

    /// <summary>Whether this kind reads a day at all.</summary>
    bool TakesDay { get; }
}

/// <summary>[S-3321] [S-11752] A screen-time widget's config: the day, or the rolling window ending today, it covers.</summary>
public interface IScreenTimePeriod
{
    ScreenTimePeriod Period { get; set; }
}

/// <summary>[S-1464] [S-11752] A music atom's config (the track, the cover, the progress): when it paints nothing.</summary>
public interface IHidesWhenIdle
{
    MusicHideWhenIdle HideWhenIdle { get; set; }
}

/// <summary>[S-11752] A sound atom's config (the volume, the mixer): the output device it follows, null for Windows' default.</summary>
public interface IAudioOutput
{
    /// <summary>The device's id, or null for whatever Windows plays to.</summary>
    string? DeviceId { get; set; }

    /// <summary>The device's name as last seen, for a device that is gone.</summary>
    string? DeviceName { get; set; }
}

/// <summary>
/// [S-7063] [S-11752] An account widget's config that reads a choice of the signed-in accounts offering
/// one feature (mail, contacts): empty means every such account.
/// </summary>
public interface IChoosesAccounts
{
    /// <summary>What the accounts it chooses offer.</summary>
    FeatureId AccountFeature { get; }

    /// <summary>The accounts chosen, by id; empty for every one.</summary>
    IReadOnlyList<string> ChosenAccounts { get; set; }
}

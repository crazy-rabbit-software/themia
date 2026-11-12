using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-433] [S-11752] A GPU atom's config: which adapter it reads, as its position in DXGI's list (software adapters left out).</summary>
public interface IGpuAdapter
{
    int Adapter { get; set; }
}

/// <summary>[S-11752] A drive atom's config (the space on one drive, its facts): the drive's root, empty for the system drive.</summary>
public interface IDriveChoice
{
    string Drive { get; set; }
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

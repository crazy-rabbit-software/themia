using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3913] Windows' own do not disturb, as Karpo switches it (<c>DoNotDisturb.cs</c>): through the one COM
/// object every part of Windows that switches it goes through, in the Windows Push Notifications user
/// service — CLSID_QuietHoursSettings {F53321FA-34F8-4B7F-B9A3-361877CB94CF}, IQuietHoursSettings
/// {6BFF4732-81EC-4FFB-AE67-B6C1BC29631F} (undocumented; slots 3 get_UserSelectedProfile,
/// 4 put_UserSelectedProfile, 8 get_QuietMomentsManager, 9 get_OffProfileId, 10 get_ActiveQuietMomentProfile,
/// 12 get_ActiveProfile; IQuietMomentsManager slot 5 TurnOffCurrentlyActiveQuietMoment). On sets the
/// person's choice to priority only, as Windows' toggle does; off sets it back to the off profile and
/// turns off a rule that holds it on for now (the schedule comes round again).
/// <para>
/// Heard through the account's CloudStore value Windows writes when the person's choice changes,
/// watched. A rule's own on and off (a game, the schedule) writes nothing there, so it shows when the
/// pointer next comes by. (Karpo hears <c>ToastNotificationManagerForUser.NotificationModeChanged</c>,
/// which needs a Windows 11 SDK projection Themia does not build against.)
/// </para>
/// </summary>
internal sealed unsafe class DoNotDisturbToggleSource : ToggleSource
{
    private static readonly Guid SettingsClass = new("F53321FA-34F8-4B7F-B9A3-361877CB94CF");
    private static readonly Guid SettingsInterface = new("6BFF4732-81EC-4FFB-AE67-B6C1BC29631F");

    private const string StateKey =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\"
        + @"default$windows.data.donotdisturb.quiethourssettings\"
        + @"windows.data.donotdisturb.quiethourssettings";

    private const int SlotUserSelected = 3;
    private const int SlotPutUserSelected = 4;
    private const int SlotMoments = 8;
    private const int SlotOffProfile = 9;
    private const int SlotActiveMoment = 10;
    private const int SlotActive = 12;
    private const int SlotTurnOffMoment = 5;

    private readonly Lock _gate = new();
    private RegistryWatch? _watch;

    public override void Start() => OffThread("following do not disturb", () =>
    {
        var watch = RegistryWatch.Open(RegistryHive.CurrentUser, StateKey, Read);
        lock (_gate)
        {
            if (Disposed)
            {
                watch?.Dispose();
                return;
            }

            _watch = watch;
        }

        Read();
    });

    public override void Refresh() => OffThread("reading do not disturb", Read);

    public override Task<bool> SetAsync(bool on) => OffThread("switching do not disturb", () =>
    {
        var settings = Make();
        if (settings == 0)
        {
            return false;
        }

        try
        {
            var off = Text(settings, SlotOffProfile) ?? QuickToggleRules.QuietOffProfile;
            if (on)
            {
                Check(Put(settings, QuickToggleRules.QuietPriorityProfile), "put_UserSelectedProfile");
            }
            else
            {
                if (!string.Equals(Text(settings, SlotUserSelected) ?? off, off, StringComparison.OrdinalIgnoreCase))
                {
                    Check(Put(settings, off), "put_UserSelectedProfile");
                }

                if (Text(settings, SlotActiveMoment) is { Length: > 0 })
                {
                    TurnOffMoment(settings);
                }
            }
        }
        finally
        {
            ToggleNative.Release(settings);
        }

        Read();
        return Reading.On == on;
    }, false);

    private void Read()
    {
        if (Disposed)
        {
            return;
        }

        var settings = Make();
        if (settings == 0)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
            return;
        }

        try
        {
            var off = Text(settings, SlotOffProfile);
            var active = Text(settings, SlotActive) ?? off;
            Publish(ToggleReading.Of(QuickToggleRules.DoNotDisturbOn(active, off)));
        }
        finally
        {
            ToggleNative.Release(settings);
        }
    }

    /// <summary>Windows' own QuietHoursSettings, in WpnUserService; 0 where there is none (Windows 10 before 1903).</summary>
    private static nint Make()
    {
        var made = ToggleNative.Create(SettingsClass, SettingsInterface, ToggleNative.ClsCtxLocalServer, out var result);
        if (made == 0)
        {
            Log.Info("toggles", $"QuietHoursSettings is not there (0x{result:X8})");
        }

        return made;
    }

    /// <summary>A profile id from one of the getters (task-allocated, freed here); null where it said nothing.</summary>
    private static string? Text(nint settings, int slot)
    {
        char* text = null;
        var result = ((delegate* unmanaged[Stdcall]<nint, char**, int>)ToggleNative.Slot(settings, slot))(settings, &text);
        try
        {
            return result < 0 || text is null ? null : new string(text);
        }
        finally
        {
            if (text is not null)
            {
                ToggleNative.CoTaskMemFree(text);
            }
        }
    }

    private static int Put(nint settings, string profile)
    {
        fixed (char* id = profile)
        {
            return ((delegate* unmanaged[Stdcall]<nint, char*, int>)ToggleNative.Slot(settings, SlotPutUserSelected))(settings, id);
        }
    }

    private static void TurnOffMoment(nint settings)
    {
        nint moments = 0;
        var asked = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)ToggleNative.Slot(settings, SlotMoments))(settings, &moments);
        if (asked < 0 || moments == 0)
        {
            Check(asked, "get_QuietMomentsManager");
            return;
        }

        try
        {
            Check(((delegate* unmanaged[Stdcall]<nint, int>)ToggleNative.Slot(moments, SlotTurnOffMoment))(moments), "TurnOffCurrentlyActiveQuietMoment");
        }
        finally
        {
            ToggleNative.Release(moments);
        }
    }

    private static void Check(int result, string what)
    {
        if (result < 0)
        {
            Log.Warn("toggles", $"IQuietHoursSettings {what} answered 0x{result:X8}");
        }
    }

    protected override void Stop()
    {
        RegistryWatch? watch;
        lock (_gate)
        {
            watch = _watch;
            _watch = null;
        }

        watch?.Dispose();
    }
}

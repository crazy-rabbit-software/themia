using Themia.App.Services.Drives;

namespace Themia.App.Services;

/// <summary>
/// [S-4623] The alarm service for the app's lifetime, and the system signals the drive and alarm
/// widgets wait on: a volume arriving or leaving ([S-4655]), the Recycle Bin changing ([S-1389]), the clock or zone changing and the PC
/// waking ([S-4623]). Only the app starts these — never a debug render.
/// </summary>
internal sealed partial class AppHost
{
    private AlarmService? _alarms;

    private void StartAlarms()
    {
        _alarms = new AlarmService(_dispatcher, () => State, SaveGeometryOnly);
        AppServices.Alarms = _alarms;

        // Settings, an undo, a paste or a deleted widget: the alarms there are now.
        StateChanged += _alarms.Sync;
        _command.TimeChanged += _alarms.TimeChanged;
        _command.Resumed += _alarms.Resumed;
        _command.VolumesChanged += DriveService.DevicesChanged;
        _command.RecycleBinChanged += RecycleBinStatus.BinChanged;

        _alarms.Start();
    }

    private void StopAlarms()
    {
        if (_alarms is not { } alarms)
        {
            return;
        }

        StateChanged -= alarms.Sync;
        _command.TimeChanged -= alarms.TimeChanged;
        _command.Resumed -= alarms.Resumed;
        _command.VolumesChanged -= DriveService.DevicesChanged;
        _command.RecycleBinChanged -= RecycleBinStatus.BinChanged;
        AppServices.Alarms = null;
        alarms.Dispose();
        _alarms = null;
    }
}

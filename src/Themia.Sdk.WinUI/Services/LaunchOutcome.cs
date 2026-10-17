namespace Themia.App.Services;

/// <summary>What happened when something was asked to open.</summary>
public enum LaunchOutcome
{
    /// <summary>Handed to the shell.</summary>
    Opened = 0,

    /// <summary><see cref="Themia.Core.Widgets.LaunchTargets"/> would not have it.</summary>
    Refused = 1,

    /// <summary>A well-formed local path that is not there any more.</summary>
    Missing = 2,

    /// <summary>The shell was asked and said no: no association, a cancelled elevation prompt, a dead share.</summary>
    Failed = 3,
}

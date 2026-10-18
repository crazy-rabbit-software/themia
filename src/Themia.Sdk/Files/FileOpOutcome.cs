namespace Themia.App.Services.Files;

/// <summary>How a file operation ended.</summary>
public enum FileOpOutcome
{
    Done,

    /// <summary>The user cancelled in the shell's progress or conflict dialog. Not a failure.</summary>
    Cancelled,

    Failed,
}

namespace Themia.Core.Model;

/// <summary>[S-1822] Whose throughput the disk meter shows.</summary>
public enum DiskScope
{
    /// <summary>One drive: <c>DiskConfig.Drive</c> (empty = the system drive).</summary>
    OneDrive = 0,

    /// <summary>Every drive together.</summary>
    AllDrives = 1,
}

namespace Themia.Core.Files;

/// <summary>An OLE drop effect. The values are the Win32 <c>DROPEFFECT_*</c> bits, so they cross the COM boundary as they are.</summary>
[Flags]
public enum DropEffect
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
}

/// <summary>What a dragged data object carries, as far as a drop cares.</summary>
[Flags]
public enum DropContent
{
    None = 0,

    /// <summary>File-system paths (<c>CF_HDROP</c>, or <c>file:///</c> lines a Chromium app offers instead).</summary>
    Files = 1,

    /// <summary>Files with no path yet (<c>FileGroupDescriptorW</c>: Outlook attachments, a browser's image).</summary>
    VirtualFiles = 2,

    /// <summary>Web addresses (a browser's link or tab).</summary>
    Urls = 4,

    /// <summary>Shell items (<c>Shell IDList Array</c>): a Start-menu app has nothing else.</summary>
    ShellItems = 8,
}

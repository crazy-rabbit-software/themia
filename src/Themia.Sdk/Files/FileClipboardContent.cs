using Themia.Core.Files;

namespace Themia.App.Services.Files;

/// <summary>What a paste would bring in: the files' paths, whether they were cut, and any virtual files.</summary>
public sealed record FileClipboardContent(IReadOnlyList<string> Paths, DropEffect Effect, int VirtualFiles);

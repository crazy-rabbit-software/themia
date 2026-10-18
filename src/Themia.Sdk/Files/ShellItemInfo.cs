namespace Themia.App.Services.Files;

/// <summary>One shell item of a drag: its parsing name, its display name, and its file-system path when it has one.</summary>
public readonly record struct ShellItemInfo(string ParsingName, string DisplayName, string? FileSystemPath);

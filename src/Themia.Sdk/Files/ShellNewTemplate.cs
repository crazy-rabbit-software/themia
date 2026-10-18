namespace Themia.App.Services.Files;

/// <summary>How a ShellNew template makes its file.</summary>
public enum ShellNewKind
{
    /// <summary><c>NullFile</c>: an empty file.</summary>
    Empty,

    /// <summary><c>FileName</c>: a copy of a template file.</summary>
    Copy,

    /// <summary><c>Data</c>: these bytes.</summary>
    Data,
}

/// <summary>One entry of Explorer's New menu ("Text Document", "Microsoft Word Document").</summary>
public sealed record ShellNewTemplate(string Extension, string Label, ShellNewKind Kind, string? Source, byte[]? Data);

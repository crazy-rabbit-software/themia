using System.Globalization;
using System.Text;
using System.Text.Json;
using Themia.Core.Diagnostics;

namespace Themia.Core.Notes;

/// <summary>A note as a picker sees it.</summary>
/// <param name="Id">32 lowercase hex digits (<see cref="NoteStore.IsValidId"/>); also the body's file name.</param>
/// <param name="Title">Already cleaned (<see cref="NoteStore.CleanTitle"/>): one line, never blank.</param>
/// <param name="UpdatedAt">Unix seconds of the last body write. "Recently changed", never the list order.</param>
public sealed record NoteMeta(string Id, string Title, long UpdatedAt);

/// <summary>Whether a body was there to read.</summary>
public enum NoteReadStatus
{
    /// <summary>Read; <see cref="NoteRead.Text"/> is the body (possibly empty).</summary>
    Found,

    /// <summary>No such note: deleted, or an id this store never minted. Not the same as "empty".</summary>
    Missing,

    /// <summary>The note exists but its file could not be read this time (locked, permissions).</summary>
    Failed,
}

/// <summary>The outcome of reading one body. <see cref="Text"/> is empty unless <see cref="Status"/> is Found.</summary>
public readonly record struct NoteRead(NoteReadStatus Status, string Text)
{
    public static NoteRead Missing { get; } = new(NoteReadStatus.Missing, string.Empty);

    public static NoteRead Failed { get; } = new(NoteReadStatus.Failed, string.Empty);
}

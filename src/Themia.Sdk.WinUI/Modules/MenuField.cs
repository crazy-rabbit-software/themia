namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-2332] A widget page's line of text typed in place (a title, a caption, an address, a coordinate): the 240-wide
/// text box at its line's end, as a setting's Name is on the Views page, never a page of its own (owner, 2026-11-19:
/// "the title and caption for example should show inline not under the additional menu"). <see cref="Current"/> is
/// what it holds; Enter or leaving the box keeps what is typed where <see cref="Refuse"/> takes it (its reason shows
/// under the line's name), Escape puts back what holds. <see cref="Keep"/> stores and saves.
/// </summary>
/// <param name="Current">What the line holds now.</param>
/// <param name="Placeholder">The box's hint while it is empty.</param>
/// <param name="Refuse">Why a typed text cannot be kept, or null when it can.</param>
/// <param name="Keep">Stores a text that can be kept, and saves.</param>
/// <param name="AllowEmpty">An empty box may be kept (it clears the line).</param>
public sealed record MenuField(string Current, string Placeholder, Func<string, string?> Refuse, Action<string> Keep, bool AllowEmpty = true);

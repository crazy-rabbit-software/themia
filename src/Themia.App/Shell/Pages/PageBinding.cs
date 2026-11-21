using System.Globalization;
using System.Text;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-2361] A page's rows across models: a change of the state builds the page's rows again, and every card whose rows
/// read the same as before stays as it was drawn. Two things make that sound. A row's <see cref="Signature"/> is what
/// it draws (its words, its kind, what it holds, its state, its icon's and its picker's keys), never what it does; and
/// what a row does is bound late (<see cref="Bind"/>): a kept card's controls do what the newest model's row of the same
/// key does, never what the model they were drawn from said.
/// </summary>
internal sealed class PageBinding
{
    /// <summary>The newest model's rows by their keys (at every depth, and the buttons beside them).</summary>
    private Dictionary<string, PageRow> _live = new(StringComparer.Ordinal);

    private PageHeader? _header;

    /// <summary>
    /// <paramref name="raw"/> made the newest, and its rows with every handler bound late: a control drawn from a bound row
    /// runs the handler of the row of its key in whichever model is the newest when it runs (nothing where none is).
    /// </summary>
    public PageModel Bind(PageModel raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var live = new Dictionary<string, PageRow>(StringComparer.Ordinal);
        void Index(PageRow row)
        {
            live[row.Key] = row;
            foreach (var inner in row.Rows ?? [])
            {
                Index(inner);
            }

            if (row.Beside is { } beside)
            {
                Index(beside);
            }
        }

        foreach (var row in raw.Sections.SelectMany(s => s.Rows))
        {
            Index(row);
        }

        _live = live;
        _header = raw.Header;
        return raw with
        {
            Sections = [.. raw.Sections.Select(s => s with { Rows = [.. s.Rows.Select(Bound)] })],
            Header = raw.Header is { } header ? header with { Tools = [.. header.Tools.Select(t => Bound(t, () => _header?.Tools))] } : null,
        };
    }

    private PageRow? Live(string key) => _live.GetValueOrDefault(key);

    private PageRow Bound(PageRow row)
    {
        var key = row.Key;
        return row with
        {
            Toggle = row.Toggle is null ? null : on => Live(key)?.Toggle?.Invoke(on),
            Run = row.Run is null ? null : () => Live(key)?.Run?.Invoke(),
            Rows = row.Rows is { } rows ? [.. rows.Select(Bound)] : null,
            Number = row.Number?.Rebound(() => Live(key)?.Number),
            Colour = row.Colour?.Rebound(() => Live(key)?.Colour),
            Edit = row.Edit is { } edit ? edit with
            {
                Commit = typed => Live(key)?.Edit?.Commit(typed),
                Refuse = edit.Refuse is null ? null : typed => (Live(key)?.Edit?.Refuse ?? edit.Refuse)(typed),
                Live = edit.Live is null ? null : typed => Live(key)?.Edit?.Live?.Invoke(typed),
                Fill = edit.Fill is null ? null : () => (Live(key)?.Edit?.Fill ?? edit.Fill)(),
                Wipe = edit.Wipe is null ? null : () => Live(key)?.Edit?.Wipe?.Invoke(),
            } : null,
            Question = row.Question is { } question ? question with { Done = () => Live(key)?.Question?.Done() } : null,
            Search = row.Search is { } search ? search with { Find = (typed, cancel) => (Live(key)?.Search ?? search).Find(typed, cancel) } : null,
            Tools = row.Tools is { } tools ? [.. tools.Select(t => Bound(t, () => Live(key)?.Tools))] : null,
            Order = row.Order is { } order ? order with { Move = (from, to) => Live(key)?.Order?.Move(from, to) } : null,
            Beside = row.Beside is { } beside ? Bound(beside) : null,
        };
    }

    private static PageTool Bound(PageTool tool, Func<IReadOnlyList<PageTool>?> tools)
    {
        PageTool? Live() => tools()?.FirstOrDefault(t => t.Key == tool.Key);
        return tool with
        {
            Run = () => Live()?.Run(),
            Question = tool.Question is { } question ? question with { Done = () => Live()?.Question?.Done() } : null,
        };
    }

    /// <summary>
    /// [S-2361] What <paramref name="row"/> draws, at every depth: equal signatures draw the same card. A row whose icon,
    /// whose head's picture or whose picker has no key of what it draws never reads the same as another.
    /// </summary>
    public static string Signature(PageRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var text = new StringBuilder();
        Write(text, row);
        return text.ToString();
    }

    /// <summary>[S-2361] What a header card draws.</summary>
    public static string Signature(PageHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var text = new StringBuilder().Append(header.Glyph).Append('\u001f').Append(header.Name).Append('\u001f').Append(header.Caption);
        foreach (var tool in header.Tools)
        {
            Write(text, tool);
        }

        return text.ToString();
    }

    private static void Write(StringBuilder text, PageRow row)
    {
        void Field(object? value) => text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\u001f');
        static string Unique() => "\u0001" + Guid.NewGuid().ToString("N");

        text.Append('{');
        Field(row.Key);
        Field(row.Kind);
        Field(row.Text);
        Field(row.Caption);
        Field(row.Value);
        Field(row.Icon is null ? null : row.IconKey ?? Unique());
        Field(row.Shows is null ? null : row.ShowsKey ?? Unique());
        Field(row.Picker is null ? null : row.PickerKey ?? Unique());
        Field(row.Marked);
        Field(row.On);
        Field(row.Toggle is not null);
        Field(row.Holds);
        Field(row.Run is not null);
        Field(row.Disabled);
        Field(row.Danger);
        Field(row.Busy);
        Field(row.Filter);
        Field(row.Keywords);
        Field(row.Owner);
        Field(row.SetsUp);
        if (row.Number is { } number)
        {
            Field($"n{number.Value}|{number.Min}|{number.Max}|{number.Step}|{(number.Value is { } held ? number.Format(held) : null)}");
        }

        if (row.Colour is { } colour)
        {
            var options = colour.Options;
            Field($"c{colour.IsFill}|{colour.Hex}|{colour.Fill?.Kind}|{colour.Fill?.Colour}|{colour.Fill?.Opacity}|{options.DefaultLabel}|{options.AllowCustom}|{options.DefaultColor?.Invoke()}");
            foreach (var preset in options.Presets)
            {
                Field(preset.Hex + preset.Label.Text());
            }
        }

        if (row.Edit is { } edit)
        {
            Field($"e{edit.Placeholder}|{edit.Initial}|{edit.AllowEmpty}|{edit.Masked}|{edit.Numeric}|{edit.Accept}|{edit.Live is not null}|{edit.Refuse is not null}");
        }

        if (row.Question is { } question)
        {
            Field($"q{question.Question}|{question.Accept}|{question.Danger}|{question.Detail}");
        }

        if (row.Search is { } search)
        {
            Field($"s{search.Placeholder}|{search.Value}|{search.Settle.Ticks}");
        }

        if (row.Order is { } order)
        {
            Field($"o{order.Index}|{order.Count}");
        }

        foreach (var tool in row.Tools ?? [])
        {
            Write(text, tool);
        }

        if (row.Beside is { } beside)
        {
            text.Append('b');
            Write(text, beside);
        }

        foreach (var inner in row.Rows ?? [])
        {
            Write(text, inner);
        }

        text.Append('}');
    }

    private static void Write(StringBuilder text, PageTool tool)
        => text.Append("t{").Append(tool.Key).Append('\u001f').Append(tool.Glyph).Append('\u001f').Append(tool.Name).Append('\u001f')
            .Append(tool.Shortcut).Append('\u001f').Append(tool.Disabled).Append('\u001f')
            .Append(tool.Question is { } q ? $"{q.Question}|{q.Accept}|{q.Danger}|{q.Detail}" : null).Append('}');
}

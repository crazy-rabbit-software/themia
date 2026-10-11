using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    public static void Ping(List<FrameworkElement> rows, PingConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Widget_Ping_Label.Text()));

        AddHostRow(rows, () => config.Host, value => config.Host = value, save);
        AddLabelRow(rows, () => config.HostLabel, value => config.HostLabel = value, save);

        MeterRows(rows, config, save, Str.WidgetsB_Settings_Ping_Refresh.Text());
    }

    /// <summary>
    /// [S-1173] A name for the host. Stored as typed (a label may have spaces in it) and trimmed where
    /// it is shown; empty means the header shows the address.
    /// </summary>
    private static void AddLabelRow(List<FrameworkElement> rows, Func<string> get, Action<string> set, Action save)
    {
        var box = new TextBox
        {
            Text = get(),
            PlaceholderText = Str.WidgetsB_Settings_Ping_Name_Placeholder.Text(),
            MaxLength = PingConfig.MaxLabelLength,
            MinWidth = 200,
        };

        box.TextChanged += (_, _) =>
        {
            if (get() != box.Text)
            {
                set(box.Text);
                save();
            }
        };

        rows.Add(FluentLayout.Row(Glyphs.Tag, Str.WidgetsB_Settings_Ping_Name_Label.Text(),
            Str.WidgetsB_Settings_Ping_Name_Desc.Text(), box, "label"));
    }

    /// <summary>
    /// The host field, with its own verdict underneath it.
    /// <para>
    /// This is the trust boundary: whatever is typed here is persisted and later handed to a name
    /// resolver, so it is checked before it is stored rather than after it is read back. A value
    /// that is not a plausible host is <b>not saved at all</b> — the widget keeps measuring the last
    /// host that was — because silently storing it would leave the box looking accepted while the
    /// tile said "unreachable", which reads as a network fault rather than as a typo.
    /// </para>
    /// <para>
    /// An empty box is not a rejection and it is not "probe nothing" either: it means the default
    /// resolver, and the line under the box says which one, so the widget's behaviour is never
    /// something the user has to infer from a blank field.
    /// </para>
    /// </summary>
    private static void AddHostRow(List<FrameworkElement> rows, Func<string> get, Action<string> set, Action save)
    {
        var box = new TextBox
        {
            Text = get(),
            PlaceholderText = PingConfig.DefaultHost,
            MinWidth = 200,
        };

        var verdict = FluentLayout.Verdict();

        void Judge(bool writeThrough)
        {
            var typed = box.Text.Trim();

            if (typed.Length == 0)
            {
                FluentLayout.Confirm(verdict, Str.WidgetsB_Settings_Ping_Measuring.Format(("host", PingConfig.DefaultHost)));

                if (writeThrough && get().Length != 0)
                {
                    set(string.Empty);
                    save();
                }

                return;
            }

            if (!HostAddress.IsPlausible(typed))
            {
                FluentLayout.Refuse(verdict, Str.WidgetsB_Settings_Ping_NotAHost.Text());
                return;
            }

            FluentLayout.Confirm(verdict, string.Empty);

            if (writeThrough && get() != typed)
            {
                set(typed);
                save();
            }
        }

        // Judged on every keystroke so a typo is flagged while it is still being made, and written
        // through on the same keystroke so the widget behind the window follows along. A rejected
        // value simply never reaches the config.
        box.TextChanged += (_, _) => Judge(writeThrough: true);

        // State the verdict for the value that is already stored, without saving it back — an
        // untouched field must not be able to rewrite the config just by being shown.
        Judge(writeThrough: false);

        rows.Add(FluentLayout.FieldRow(Glyphs.Link, Str.WidgetsB_Settings_Ping_Host_Label.Text(),
            Str.WidgetsB_Settings_Ping_Host_Desc.Text(), box, verdict));
    }
}

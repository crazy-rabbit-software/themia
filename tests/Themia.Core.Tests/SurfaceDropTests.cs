using System.Buffers.Binary;
using System.Text;
using Themia.Core.Files;
using Themia.Core.Localization;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>WP10-SURFACE: the desktop surface as a drop target (S-1020..S-1026).</summary>
public sealed class SurfaceDropTests
{
    // ---- routing ([S-1020]) ---------------------------------------------------------------------

    [Theory]
    [InlineData(DropContent.Files)]
    [InlineData(DropContent.VirtualFiles)]
    [InlineData(DropContent.Urls)]
    [InlineData(DropContent.ShellItems)]
    public void S1020_a_widget_that_takes_drops_gets_them_outside_edit_mode(DropContent content)
        => Assert.Equal(DropRoute.Widget, SurfaceDrops.Route(SurfaceHit.Widget, editMode: false, widgetTakesDrops: true, content));

    [Fact]
    public void S1020_a_widget_takes_no_drop_while_editing()
        => Assert.Equal(DropRoute.Refuse, SurfaceDrops.Route(SurfaceHit.Widget, editMode: true, widgetTakesDrops: true, DropContent.Files));

    [Fact]
    public void S1020_a_widget_without_drops_refuses_rather_than_passing_to_the_desktop_behind_it()
        => Assert.Equal(DropRoute.Refuse, SurfaceDrops.Route(SurfaceHit.Widget, editMode: false, widgetTakesDrops: false, DropContent.Files));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S1020_bare_surface_is_the_desktop(bool editMode)
        => Assert.Equal(DropRoute.Desktop, SurfaceDrops.Route(SurfaceHit.Empty, editMode, widgetTakesDrops: false, DropContent.Files | DropContent.Urls));

    [Theory]
    [InlineData(false, DropContent.Files)]
    [InlineData(true, DropContent.Files)]
    [InlineData(false, DropContent.VirtualFiles)]
    [InlineData(true, DropContent.VirtualFiles)]
    [InlineData(false, DropContent.Urls)]
    [InlineData(true, DropContent.Urls)]
    [InlineData(false, DropContent.ShellItems)]
    [InlineData(true, DropContent.ShellItems)]
    public void S1021_a_drop_on_bare_desktop_goes_to_the_desktop_folder_in_edit_mode_and_outside_it(bool editMode, DropContent content)
    {
        Assert.Equal(DropRoute.Desktop, SurfaceDrops.Route(SurfaceHit.Empty, editMode, widgetTakesDrops: false, content));

        // Whether a widget elsewhere takes drops changes nothing about bare desktop.
        Assert.Equal(DropRoute.Desktop, SurfaceDrops.Route(SurfaceHit.Empty, editMode, widgetTakesDrops: true, content));
    }

    [Fact]
    public void S1020_the_surface_controls_refuse()
        => Assert.Equal(DropRoute.Refuse, SurfaceDrops.Route(SurfaceHit.Chrome, editMode: true, widgetTakesDrops: false, DropContent.Files));

    [Theory]
    [InlineData(SurfaceHit.Empty)]
    [InlineData(SurfaceHit.Widget)]
    public void S1020_nothing_droppable_is_refused_everywhere(SurfaceHit hit)
        => Assert.Equal(DropRoute.Refuse, SurfaceDrops.Route(hit, editMode: false, widgetTakesDrops: true, DropContent.None));

    // ---- forced effect as keys ([S-1023]) -------------------------------------------------------

    [Fact]
    public void S1023_move_is_shift_copy_is_ctrl_link_is_both()
    {
        const uint left = SurfaceDrops.KeyLeftButton;

        Assert.Equal(left | SurfaceDrops.KeyShift, SurfaceDrops.KeysFor(DropEffect.Move, left));
        Assert.Equal(left | SurfaceDrops.KeyControl, SurfaceDrops.KeysFor(DropEffect.Copy, left));
        Assert.Equal(left | SurfaceDrops.KeyControl | SurfaceDrops.KeyShift, SurfaceDrops.KeysFor(DropEffect.Link, left));
    }

    [Fact]
    public void S1023_forcing_replaces_the_users_modifiers_but_keeps_the_buttons()
    {
        var keys = SurfaceDrops.KeyRightButton | SurfaceDrops.KeyControl | SurfaceDrops.KeyAlt;

        Assert.Equal(SurfaceDrops.KeyRightButton | SurfaceDrops.KeyShift, SurfaceDrops.KeysFor(DropEffect.Move, keys));
    }

    [Fact]
    public void S1023_no_forced_effect_passes_the_keys_through()
    {
        var keys = SurfaceDrops.KeyLeftButton | SurfaceDrops.KeyControl;

        Assert.Equal(keys, SurfaceDrops.KeysFor(DropEffect.None, keys));
    }

    // ---- feedback ([S-1024]) --------------------------------------------------------------------

    [Fact]
    public void S1024_captions_use_the_shells_words_with_the_place_inserted()
    {
        Assert.Equal(new DropFeedback(DropEffect.Move, "Move to %1", "Desktop"), SurfaceDrops.Feedback(Locale.En, DropEffect.Move, "Desktop"));
        Assert.Equal(new DropFeedback(DropEffect.Copy, "Copy to %1", "Docs"), SurfaceDrops.Feedback(Locale.En, DropEffect.Copy, "Docs"));
        Assert.Equal("Create link in %1", SurfaceDrops.Feedback(Locale.En, DropEffect.Link, "Docs").Message);
        Assert.Equal(DropFeedback.Refused, SurfaceDrops.Feedback(Locale.En, DropEffect.None, "Docs"));
    }

    [Fact]
    public void S1024_a_refused_drag_clears_the_description()
    {
        Assert.Equal(DropImage.Invalid, DropFeedback.Refused.Image);
        Assert.Equal(DropImage.Move, SurfaceDrops.Feedback(Locale.En, DropEffect.Move, "x").Image);
    }

    [Theory]
    [InlineData(DropEffect.Move)]
    [InlineData(DropEffect.Copy)]
    [InlineData(DropEffect.Link)]
    [InlineData(DropEffect.None)]
    public void S5546_plain_feedback_takes_the_drop_with_no_caption_and_no_mark(DropEffect effect)
    {
        var plain = DropFeedback.Plain(effect);

        Assert.Equal(DropImage.NoImage, plain.Image);
        Assert.Equal(effect, plain.Effect);
        Assert.True(plain.Bare);
        Assert.Equal((string.Empty, string.Empty), (plain.Message, plain.Insert));

        // Refused stays the cleared description, and a captioned effect its own mark.
        Assert.False(DropFeedback.Refused.Bare);
        Assert.Equal(DropImage.Invalid, DropFeedback.Refused.Image);
        Assert.NotEqual(DropFeedback.Refused, DropFeedback.Plain(DropEffect.None));
    }

    [Theory]
    [InlineData(DropEffect.Copy | DropEffect.Move, DropEffect.Move)]
    [InlineData(DropEffect.Copy | DropEffect.Link, DropEffect.Copy)]
    [InlineData(DropEffect.Link, DropEffect.Link)]
    [InlineData(DropEffect.None, DropEffect.None)]
    public void S1024_a_set_of_effects_shows_the_strongest(DropEffect answered, DropEffect shown)
        => Assert.Equal(shown, SurfaceDrops.Single(answered));

    [Fact]
    public void S1024_the_description_is_the_win32_layout()
    {
        var bytes = SurfaceDrops.EncodeDescription(DropImage.Copy, "Copy to %1", "Desktop");

        Assert.Equal(1044, bytes.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal("Copy to %1", SurfaceDrops.DecodeText(bytes.AsSpan(4, 520)));
        Assert.Equal("Desktop", SurfaceDrops.DecodeText(bytes.AsSpan(524, 520)));
    }

    [Fact]
    public void S1024_an_overlong_insert_is_cut_to_fit_and_stays_terminated()
    {
        var bytes = SurfaceDrops.EncodeDescription(DropImage.Move, "Move to %1", new string('a', 500));

        var insert = SurfaceDrops.DecodeText(bytes.AsSpan(524, 520));
        Assert.Equal(259, insert.Length);
        Assert.Equal(0, bytes[^1]);
        Assert.Equal(0, bytes[^2]);
    }

    [Fact]
    public void S1024_a_cut_never_splits_a_surrogate_pair()
    {
        var text = new string('a', 258) + "\U0001F600";
        var bytes = SurfaceDrops.EncodeDescription(DropImage.Move, text, string.Empty);

        var message = SurfaceDrops.DecodeText(bytes.AsSpan(4, 520));
        Assert.Equal(new string('a', 258), message);
    }

    [Fact]
    public void S1024_the_refused_description_has_the_invalid_type()
        => Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(SurfaceDrops.EncodeDescription(DropImage.Invalid, "", "")));

    // ---- text payloads ([S-1025]) ---------------------------------------------------------------

    [Fact]
    public void S1025_a_percent_encoded_utf8_path_decodes_to_its_real_characters()
        => Assert.Equal([@"C:\Müller.txt"], SurfaceDrops.FileUrls("file:///C:/M%C3%BCller.txt"));

    [Fact]
    public void S1025_ascii_escapes_decode_and_plain_text_passes()
    {
        Assert.Equal(@"C:\My Docs\a b.txt", SurfaceDrops.FileUrls("file:///C:/My%20Docs/a%20b.txt")[0]);
        Assert.Equal("plain", SurfaceDrops.PercentDecode("plain"));
    }

    [Fact]
    public void S1025_a_malformed_escape_is_kept_as_written()
        => Assert.Equal("50%-off %zz %4", SurfaceDrops.PercentDecode("50%-off %zz %4"));

    [Fact]
    public void S1025_several_lines_unc_paths_and_duplicates()
    {
        var paths = SurfaceDrops.FileUrls("file:///D:/a.txt\r\nfile://server/share/b.txt\r\nfile:///D:/a.txt\n");

        Assert.Equal([@"D:\a.txt", @"\\server\share\b.txt"], paths);
    }

    [Theory]
    [InlineData("https://example.com/x")]
    [InlineData("file:///relative/x")]
    [InlineData("file://")]
    [InlineData("file:///C:/a%00b")]
    [InlineData("")]
    [InlineData(null)]
    public void S1025_anything_but_an_absolute_local_or_unc_path_is_skipped(string? text)
        => Assert.Empty(SurfaceDrops.FileUrls(text));

    [Fact]
    public void S1025_wide_text_stops_at_its_nul_and_survives_odd_lengths()
    {
        var bytes = Encoding.Unicode.GetBytes("https://a.example\0garbage");

        Assert.Equal("https://a.example", SurfaceDrops.DecodeText(bytes));
        Assert.Equal("ab", SurfaceDrops.DecodeText([.. Encoding.Unicode.GetBytes("ab"), 0x41]));
        Assert.Equal(string.Empty, SurfaceDrops.DecodeText([]));
    }
}

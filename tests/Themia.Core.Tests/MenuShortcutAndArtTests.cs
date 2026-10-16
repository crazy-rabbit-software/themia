using System.Text;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1753] shortcut text as Windows writes it; [S-1754]..[S-1756] File Explorer's two-tone menu pictures.</summary>
public sealed class MenuShortcutAndArtTests
{
    // ---- [S-1753] shortcut text ----------------------------------------------------------------------

    [Theory]
    [InlineData(ChordModifiers.Ctrl, ChordKey.C, "Ctrl+C")]
    [InlineData(ChordModifiers.Ctrl | ChordModifiers.Shift, ChordKey.C, "Ctrl+Shift+C")]
    [InlineData(ChordModifiers.Alt, ChordKey.Enter, "Alt+Enter")]
    [InlineData(ChordModifiers.None, ChordKey.Enter, "Enter")]
    [InlineData(ChordModifiers.None, ChordKey.Delete, "Delete")]
    [InlineData(ChordModifiers.None, ChordKey.F2, "F2")]
    [InlineData(ChordModifiers.Shift, ChordKey.F10, "Shift+F10")]
    [InlineData(ChordModifiers.None, ChordKey.F12, "F12")]
    [InlineData(ChordModifiers.Ctrl, ChordKey.Z, "Ctrl+Z")]
    public void S1753_chords_read_as_windows_writes_them(ChordModifiers modifiers, ChordKey key, string expected)
        => Assert.Equal(expected, KeyChordText.Format(new KeyChord(modifiers, key), Locale.En));

    [Fact]
    public void S1753_modifiers_go_ctrl_alt_shift_whatever_order_they_were_given()
        => Assert.Equal("Ctrl+Alt+Shift+W", KeyChordText.Format(new KeyChord(ChordModifiers.Shift | ChordModifiers.Alt | ChordModifiers.Ctrl, ChordKey.W), Locale.En));

    [Fact]
    public void S1753_key_words_follow_the_language_letters_do_not()
    {
        Assert.Equal("Strg+Umschalt+C", KeyChordText.Format(KeyChord.CtrlShift(ChordKey.C), Locale.De));
        Assert.Equal("Alt+Eingabe", KeyChordText.Format(KeyChord.Alt(ChordKey.Enter), Locale.De));
        Assert.Equal("Entf", KeyChordText.Format(KeyChord.Plain(ChordKey.Delete), Locale.De));
        Assert.Equal("Maj+F10", KeyChordText.Format(KeyChord.Shift(ChordKey.F10), Locale.Fr));
    }

    [Fact]
    public void S1753_every_language_names_every_key()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            var text = KeyChordText.Format(new KeyChord(ChordModifiers.Ctrl | ChordModifiers.Alt | ChordModifiers.Shift, ChordKey.Enter), locale);
            Assert.Equal(4, text.Split('+').Length);
            Assert.DoesNotContain(text.Split('+'), part => part.Length == 0 || part.Contains("shell.key", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void S1753_a_chord_no_menu_shows_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyChordText.Format(new KeyChord((ChordModifiers)8, ChordKey.A), Locale.En));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyChordText.Format(new KeyChord(ChordModifiers.None, (ChordKey)999), Locale.En));
    }

    // ---- [S-1754] which glyph has Explorer's picture -----------------------------------------------------

    [Theory]
    [InlineData(0xE8C6, MenuArt.Cut)]
    [InlineData(0xE8C8, MenuArt.Copy)]
    [InlineData(0xE77F, MenuArt.Paste)]
    [InlineData(0xE8AC, MenuArt.Rename)]
    [InlineData(0xE72D, MenuArt.Share)]
    [InlineData(0xE74D, MenuArt.Delete)]
    [InlineData(0xE62F, MenuArt.CopyAsPath)]
    [InlineData(0xE90F, MenuArt.Properties)]
    public void S1754_explorers_commands_have_explorers_pictures(int codePoint, MenuArt expected)
        => Assert.Equal(expected, MenuArtRules.ForGlyph(codePoint));

    [Theory]
    [InlineData(0xE74A)] // Move up: Explorer has no picture of it
    [InlineData(0xE73E)] // a check mark
    [InlineData(0x0041)]
    public void S1754_a_glyph_explorer_has_no_picture_of_keeps_its_glyph(int codePoint)
        => Assert.Null(MenuArtRules.ForGlyph(codePoint));

    [Fact]
    public void S1755_every_picture_has_its_own_file_in_each_theme_folder()
    {
        var names = Enum.GetValues<MenuArt>().Select(MenuArtRules.FileName).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => Assert.EndsWith(".svg", n, StringComparison.Ordinal));

        var light = MenuArtRules.Folder(@"C:\Windows", dark: false);
        var dark = MenuArtRules.Folder(@"C:\Windows", dark: true);
        Assert.StartsWith(@"C:\Windows\SystemApps\", light, StringComparison.Ordinal);
        Assert.EndsWith("theme-light", light, StringComparison.Ordinal);
        Assert.EndsWith("theme-dark", dark, StringComparison.Ordinal);
    }

    // ---- [S-1756] a file is a picture only when it is a small, self-contained SVG ------------------------

    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void S1756_explorers_shape_of_svg_is_a_picture()
        => Assert.True(MenuArtRules.IsPlausibleSvg(U("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><path fill=\"#555\" d=\"M0 0h16v16H0z\"/></svg>\n")));

    [Theory]
    [InlineData("")]
    [InlineData("<html></html>")]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<svg><image href=\"https://example.com/x.png\"/></svg>")]
    [InlineData("<svg><foreignObject/></svg>")]
    [InlineData("<svg><path d=\"M0 0\"/>")]
    public void S1756_anything_else_is_not(string text)
        => Assert.False(MenuArtRules.IsPlausibleSvg(U(text)));

    [Fact]
    public void S1756_too_large_or_not_utf8_is_not()
    {
        var huge = U("<svg>" + new string(' ', MenuArtRules.MaxSvgBytes) + "</svg>");
        Assert.False(MenuArtRules.IsPlausibleSvg(huge));
        Assert.False(MenuArtRules.IsPlausibleSvg([0x3C, 0x73, 0x76, 0x67, 0xFF, 0xFE, 0x3C, 0x2F, 0x73, 0x76, 0x67, 0x3E]));
    }
}

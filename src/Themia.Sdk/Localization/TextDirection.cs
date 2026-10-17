namespace Themia.Core.Localization;

/// <summary>
/// [S-1113] Which way a language's UI runs, and which icons turn with it.
/// <para>
/// Arabic, Urdu and Persian read right to left, and so does their UI: settings, card content, menus, the
/// edit bar and the view bar mirror. What does not mirror is decided here and in the views, after
/// Windows' own guidance: where a widget sits on the desktop (that is the user's geometry, not
/// text), numbers and clock faces, media transport (play, previous, next, seek), and charts whose
/// horizontal axis is time.
/// </para>
/// </summary>
public static class TextDirection
{
    /// <summary>Whether <paramref name="locale"/>'s UI runs right to left.</summary>
    public static bool IsRightToLeft(Locale locale) => locale is Locale.Ar or Locale.Ur or Locale.Fa;

    /// <summary>
    /// Segoe Fluent Icons glyphs that point a way along the line of text and so turn round in a
    /// right-to-left UI: back and forward, the chevrons, send, reply, undo and redo, the return
    /// arrow, "open outside", sign out and the bulleted and checked lists (their bullets lead).
    /// Media transport (E768 play, E892 previous, E893 next) is deliberately not here: playback
    /// controls keep their direction in every language.
    /// </summary>
    private static readonly HashSet<char> Directional =
    [
        '', // Back
        '', // Forward
        '', // ChevronLeft
        '', // ChevronRight
        '', // Send
        '', // MailReply
        '', // Undo
        '', // Redo
        '', // Return arrow (wrap)
        '', // Open in a new window
        '', // Sign out
        '', // Bulleted list
        '', // Checklist
    ];

    /// <summary>[S-1113] Whether an icon glyph is drawn mirrored when the UI runs right to left.</summary>
    public static bool MirrorsGlyph(string? glyph)
        => glyph is { Length: 1 } && Directional.Contains(glyph[0]);

    /// <summary>
    /// [S-1114] The language tag text is laid out with. Besides hyphenation it is what DirectWrite
    /// picks fallback fonts by, so each is the language with its main region: Devanagari (hi, mr)
    /// and Bengali come out in Nirmala UI, Arabic, Urdu, Cyrillic and Vietnamese in Segoe UI,
    /// Japanese in Yu Gothic UI, Simplified Chinese in Microsoft YaHei UI, Korean (ko-KR) in Malgun
    /// Gothic, Traditional Chinese (zh-TW) in Microsoft JhengHei UI; Dutch and Turkish stay in
    /// Segoe UI. Telugu, Tamil, Gujarati and Gurmukhi (te-IN, ta-IN, gu-IN, pa-IN) come out in
    /// Nirmala UI, Thai (th-TH) in Leelawadee UI, Persian (fa-IR), Hausa, Swahili and Filipino in
    /// Segoe UI (which has Hausa's hooked letters). Urdu is not asked for a Nastaliq face: Windows installs "Urdu Typesetting" only with the Urdu language pack, and its
    /// tall lines would overflow every height the widgets budget in line heights.
    /// <para>
    /// The region does not decide the digits ([S-1115]): DirectWrite's contextual substitution
    /// follows the neighbouring script, so a time beside a Latin word ("09:00–09:15 · Teams") came
    /// out Latin in Arabic. <see cref="LocaleFormat"/> writes Arabic's own digits into the text
    /// instead. Bengali is laid out as India (bn-IN), not Bangladesh, and keeps Latin digits:
    /// Windows' bn-BD default is Bengali digits, which run about a quarter wider than the Latin
    /// advance every widget's type ladder is measured on and clipped the tall clock. Urdu keeps
    /// Latin digits, as Windows' ur-PK default and CLDR do. Persian writes Extended Arabic-Indic
    /// digits, as Windows' fa-IR does. Thai, Telugu, Tamil, Gujarati and Punjabi keep Latin
    /// digits, Windows' default for th-TH, te-IN, ta-IN, gu-IN and pa-IN (their own digits are an
    /// opt-in there); so does every other language here.
    /// </para>
    /// </summary>
    public static string LayoutTag(Locale locale) => locale switch
    {
        Locale.Zh => "zh-Hans",
        Locale.Pt => "pt-BR",
        Locale.Hi => "hi-IN",
        Locale.Ar => "ar-SA",
        Locale.Bn => "bn-IN",
        Locale.Ru => "ru-RU",
        Locale.Id => "id-ID",
        Locale.Ur => "ur-PK",
        Locale.Mr => "mr-IN",
        Locale.Vi => "vi-VN",
        Locale.Ko => "ko-KR",
        Locale.Nl => "nl-NL",
        Locale.Tr => "tr-TR",
        Locale.ZhHant => "zh-TW",
        Locale.Te => "te-IN",
        Locale.Ha => "ha-NG",
        Locale.Pa => "pa-IN",
        Locale.Sw => "sw-KE",
        Locale.Fil => "fil-PH",
        Locale.Ta => "ta-IN",
        Locale.Fa => "fa-IR",
        Locale.Th => "th-TH",
        Locale.Gu => "gu-IN",
        _ => LanguageResolver.Code(locale),
    };
}

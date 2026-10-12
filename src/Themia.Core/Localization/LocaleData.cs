namespace Themia.Core.Localization;

/// <summary>
/// [S-1106] Per-language formatting data, written out because the app is built with
/// <c>InvariantGlobalization</c> (no ICU, no <c>CultureInfo</c> data — S-54). Transcribed from CLDR
/// (the data Windows, ICU and browsers use) for each language's main region: en = US,
/// pt = Brazil, zh = Simplified/China, es = Spain, ar = Saudi Arabia (with the Gregorian month
/// names Egypt and the Levant use), bn = Bangladesh, ur = Pakistan, hi and mr = India, ko = Korea, nl = the Netherlands, tr = Turkey,
/// zh-Hant = Taiwan, the rest their home country.
/// <para>
/// [S-1115] Every pattern is written with Latin digits (0–9); a language that writes its own
/// (<see cref="DigitZero"/>: Arabic) has them swapped in as the last step of formatting, so the
/// patterns stay comparable and every language shares one formatter. The swap is in the text, not
/// left to DirectWrite's contextual substitution, which follows the neighbouring script and drew
/// "09:00–09:15 · Teams" with Latin digits.
/// </para>
/// <para>
/// Patterns use CLDR letters: <c>d dd</c> day, <c>M MM</c> month number, <c>MMM MMMM</c> month name
/// as used inside a date, <c>LLLL</c> month name on its own (Polish declines it), <c>y</c> year,
/// <c>EEE EEEE</c> weekday, <c>H HH</c> 0–23, <c>h hh</c> 1–12, <c>K</c> 0–11, <c>mm ss</c>,
/// <c>a</c> AM/PM marker; text in single quotes is literal.
/// </para>
/// </summary>
internal sealed class LocaleData
{
    public required string[] Months { get; init; }

    /// <summary>Month names inside a date ("4 października"); same as <see cref="Months"/> unless the language declines them.</summary>
    public string[]? MonthsInDate { get; init; }

    public required string[] MonthsShort { get; init; }

    /// <summary>Short month names inside a date ("4 мая" beside "май"); same as <see cref="MonthsShort"/> unless the language declines them.</summary>
    public string[]? MonthsShortInDate { get; init; }

    /// <summary>Sunday first, as <see cref="DayOfWeek"/> counts.</summary>
    public required string[] Weekdays { get; init; }

    public required string[] WeekdaysShort { get; init; }

    public required string[] WeekdaysNarrow { get; init; }

    /// <summary>By <see cref="DateStyle"/>.</summary>
    public required string[] DatePatterns { get; init; }

    public required string Time24 { get; init; }

    public required string Time12 { get; init; }

    public required bool Uses24Hour { get; init; }

    public required string Am { get; init; }

    public required string Pm { get; init; }

    public required char Decimal { get; init; }

    public required string Group { get; init; }

    /// <summary>CLDR minimumGroupingDigits: 2 means 1234 stays ungrouped but 12 345 is grouped (es, pl).</summary>
    public int MinimumGrouping { get; init; } = 1;

    /// <summary>The size of every group after the first three digits: 2 for the Indian lakh/crore grouping (12,34,567), else 3.</summary>
    public int SecondaryGrouping { get; init; } = 3;

    /// <summary>The percent pattern around the number, "{0}%" or "{0} %".</summary>
    public required string Percent { get; init; }

    public required DayOfWeek FirstDayOfWeek { get; init; }

    /// <summary>
    /// [S-1115] The language's own zero when it writes its own digits (Arabic-Indic U+0660 for
    /// Arabic); null for Latin digits. The ten digits follow it in Unicode.
    /// </summary>
    public char? DigitZero { get; init; }

    public required string Now { get; init; }

    /// <summary>Relative times, by <see cref="RelativeUnit"/>: long ("3 minutes ago") and short ("3 min. ago").</summary>
    public required RelativeForms[] Relative { get; init; }

    public required RelativeForms[] RelativeShort { get; init; }

    public static LocaleData For(Locale locale) => locale switch
    {
        Locale.De => De,
        Locale.Es => Es,
        Locale.Fr => Fr,
        Locale.It => It,
        Locale.Ja => Ja,
        Locale.Pl => Pl,
        Locale.Pt => Pt,
        Locale.Zh => Zh,
        Locale.Hi => Hi,
        Locale.Ar => Ar,
        Locale.Bn => Bn,
        Locale.Ru => Ru,
        Locale.Id => Id,
        Locale.Ur => Ur,
        Locale.Mr => Mr,
        Locale.Vi => Vi,
        Locale.Ko => Ko,
        Locale.Nl => Nl,
        Locale.Tr => Tr,
        Locale.ZhHant => ZhHant,
        _ => En,
    };

    // Relative-time helpers: P(one, other) for the two-form languages, Pl3 for Polish and Russian
    // (one, few, many; a fraction takes few), Ar6 for Arabic, P1 for none.
    private static PluralText P(string one, string other) => new(One: one, Other: other);

    private static PluralText P1(string other) => new(Other: other);

    private static PluralText Pl3(string one, string few, string many) => new(One: one, Few: few, Many: many, Other: few);

    /// <summary>Arabic: one and two are words of their own (no number), few takes the plural, many and other the singular.</summary>
    private static PluralText Ar6(string one, string two, string few, string many, string other)
        => new(Other: other, One: one, Few: few, Many: many, Zero: other, Two: two);

    private static RelativeForms R(PluralText past, PluralText future) => new(past, future);

    // -----------------------------------------------------------------------------------------

    private static readonly LocaleData En = new()
    {
        Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
        MonthsShort = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
        Weekdays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
        WeekdaysShort = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"],
        WeekdaysNarrow = ["S", "M", "T", "W", "T", "F", "S"],
        DatePatterns = ["M/d/y", "MMM d, y", "MMMM d, y", "EEEE, MMMM d, y", "MMMM d", "MMM d", "MMMM y", "EEE, MMM d"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = false,
        Am = "AM",
        Pm = "PM",
        Decimal = '.',
        Group = ",",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "now",
        Relative =
        [
            R(P("{0} minute ago", "{0} minutes ago"), P("in {0} minute", "in {0} minutes")),
            R(P("{0} hour ago", "{0} hours ago"), P("in {0} hour", "in {0} hours")),
            R(P("{0} day ago", "{0} days ago"), P("in {0} day", "in {0} days")),
            R(P("{0} week ago", "{0} weeks ago"), P("in {0} week", "in {0} weeks")),
            R(P("{0} month ago", "{0} months ago"), P("in {0} month", "in {0} months")),
            R(P("{0} year ago", "{0} years ago"), P("in {0} year", "in {0} years")),
        ],
        RelativeShort =
        [
            R(P1("{0} min. ago"), P1("in {0} min.")),
            R(P1("{0} hr. ago"), P1("in {0} hr.")),
            R(P("{0} day ago", "{0} days ago"), P("in {0} day", "in {0} days")),
            R(P1("{0} wk. ago"), P1("in {0} wk.")),
            R(P1("{0} mo. ago"), P1("in {0} mo.")),
            R(P1("{0} yr. ago"), P1("in {0} yr.")),
        ],
    };

    private static readonly LocaleData De = new()
    {
        Months = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"],
        MonthsShort = ["Jan.", "Feb.", "März", "Apr.", "Mai", "Juni", "Juli", "Aug.", "Sept.", "Okt.", "Nov.", "Dez."],
        Weekdays = ["Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag"],
        WeekdaysShort = ["So.", "Mo.", "Di.", "Mi.", "Do.", "Fr.", "Sa."],
        WeekdaysNarrow = ["S", "M", "D", "M", "D", "F", "S"],
        DatePatterns = ["dd.MM.y", "dd.MM.y", "d. MMMM y", "EEEE, d. MMMM y", "d. MMMM", "d. MMM", "MMMM y", "EEE, d. MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = ".",
        Percent = "{0}\u00A0%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "jetzt",
        Relative =
        [
            R(P("vor {0} Minute", "vor {0} Minuten"), P("in {0} Minute", "in {0} Minuten")),
            R(P("vor {0} Stunde", "vor {0} Stunden"), P("in {0} Stunde", "in {0} Stunden")),
            R(P("vor {0} Tag", "vor {0} Tagen"), P("in {0} Tag", "in {0} Tagen")),
            R(P("vor {0} Woche", "vor {0} Wochen"), P("in {0} Woche", "in {0} Wochen")),
            R(P("vor {0} Monat", "vor {0} Monaten"), P("in {0} Monat", "in {0} Monaten")),
            R(P("vor {0} Jahr", "vor {0} Jahren"), P("in {0} Jahr", "in {0} Jahren")),
        ],
        RelativeShort =
        [
            R(P1("vor {0} Min."), P1("in {0} Min.")),
            R(P1("vor {0} Std."), P1("in {0} Std.")),
            R(P("vor {0} Tag", "vor {0} Tagen"), P("in {0} Tag", "in {0} Tagen")),
            R(P1("vor {0} Wo."), P1("in {0} Wo.")),
            R(P1("vor {0} Mon."), P1("in {0} Mon.")),
            R(P1("vor {0} J."), P1("in {0} J.")),
        ],
    };

    private static readonly LocaleData Es = new()
    {
        Months = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"],
        MonthsShort = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic"],
        Weekdays = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"],
        WeekdaysShort = ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"],
        WeekdaysNarrow = ["D", "L", "M", "X", "J", "V", "S"],
        DatePatterns = ["d/M/y", "d MMM y", "d 'de' MMMM 'de' y", "EEEE, d 'de' MMMM 'de' y", "d 'de' MMMM", "d MMM", "MMMM 'de' y", "EEE, d MMM"],
        Time24 = "H:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "a. m.",
        Pm = "p. m.",
        Decimal = ',',
        Group = ".",
        MinimumGrouping = 2,
        Percent = "{0}\u00A0%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "ahora",
        Relative =
        [
            R(P("hace {0} minuto", "hace {0} minutos"), P("dentro de {0} minuto", "dentro de {0} minutos")),
            R(P("hace {0} hora", "hace {0} horas"), P("dentro de {0} hora", "dentro de {0} horas")),
            R(P("hace {0} día", "hace {0} días"), P("dentro de {0} día", "dentro de {0} días")),
            R(P("hace {0} semana", "hace {0} semanas"), P("dentro de {0} semana", "dentro de {0} semanas")),
            R(P("hace {0} mes", "hace {0} meses"), P("dentro de {0} mes", "dentro de {0} meses")),
            R(P("hace {0} año", "hace {0} años"), P("dentro de {0} año", "dentro de {0} años")),
        ],
        RelativeShort =
        [
            R(P1("hace {0} min"), P1("dentro de {0} min")),
            R(P1("hace {0} h"), P1("dentro de {0} h")),
            R(P("hace {0} día", "hace {0} días"), P("dentro de {0} día", "dentro de {0} días")),
            R(P1("hace {0} sem."), P1("dentro de {0} sem.")),
            R(P1("hace {0} m."), P1("dentro de {0} m.")),
            R(P1("hace {0} a"), P1("dentro de {0} a")),
        ],
    };

    private static readonly LocaleData Fr = new()
    {
        Months = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"],
        MonthsShort = ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."],
        Weekdays = ["dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi"],
        WeekdaysShort = ["dim.", "lun.", "mar.", "mer.", "jeu.", "ven.", "sam."],
        WeekdaysNarrow = ["D", "L", "M", "M", "J", "V", "S"],
        DatePatterns = ["dd/MM/y", "d MMM y", "d MMMM y", "EEEE d MMMM y", "d MMMM", "d MMM", "MMMM y", "EEE d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = "\u202F",
        Percent = "{0}\u202F%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "maintenant",
        Relative =
        [
            R(P("il y a {0} minute", "il y a {0} minutes"), P("dans {0} minute", "dans {0} minutes")),
            R(P("il y a {0} heure", "il y a {0} heures"), P("dans {0} heure", "dans {0} heures")),
            R(P("il y a {0} jour", "il y a {0} jours"), P("dans {0} jour", "dans {0} jours")),
            R(P("il y a {0} semaine", "il y a {0} semaines"), P("dans {0} semaine", "dans {0} semaines")),
            R(P1("il y a {0} mois"), P1("dans {0} mois")),
            R(P("il y a {0} an", "il y a {0} ans"), P("dans {0} an", "dans {0} ans")),
        ],
        RelativeShort =
        [
            R(P1("il y a {0} min"), P1("dans {0} min")),
            R(P1("il y a {0} h"), P1("dans {0} h")),
            R(P1("il y a {0} j"), P1("dans {0} j")),
            R(P1("il y a {0} sem."), P1("dans {0} sem.")),
            R(P1("il y a {0} m."), P1("dans {0} m.")),
            R(P1("il y a {0} a"), P1("dans {0} a")),
        ],
    };

    private static readonly LocaleData It = new()
    {
        Months = ["gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre"],
        MonthsShort = ["gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic"],
        Weekdays = ["domenica", "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato"],
        WeekdaysShort = ["dom", "lun", "mar", "mer", "gio", "ven", "sab"],
        WeekdaysNarrow = ["D", "L", "M", "M", "G", "V", "S"],
        DatePatterns = ["dd/MM/y", "d MMM y", "d MMMM y", "EEEE d MMMM y", "d MMMM", "d MMM", "MMMM y", "EEE d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = ".",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "ora",
        Relative =
        [
            R(P("{0} minuto fa", "{0} minuti fa"), P("tra {0} minuto", "tra {0} minuti")),
            R(P("{0} ora fa", "{0} ore fa"), P("tra {0} ora", "tra {0} ore")),
            R(P("{0} giorno fa", "{0} giorni fa"), P("tra {0} giorno", "tra {0} giorni")),
            R(P("{0} settimana fa", "{0} settimane fa"), P("tra {0} settimana", "tra {0} settimane")),
            R(P("{0} mese fa", "{0} mesi fa"), P("tra {0} mese", "tra {0} mesi")),
            R(P("{0} anno fa", "{0} anni fa"), P("tra {0} anno", "tra {0} anni")),
        ],
        RelativeShort =
        [
            R(P1("{0} min fa"), P1("tra {0} min")),
            R(P1("{0} h fa"), P1("tra {0} h")),
            R(P1("{0} g fa"), P1("tra {0} g")),
            R(P1("{0} sett. fa"), P1("tra {0} sett.")),
            R(P("{0} mese fa", "{0} mesi fa"), P("tra {0} mese", "tra {0} mesi")),
            R(P("{0} anno fa", "{0} anni fa"), P("tra {0} anno", "tra {0} anni")),
        ],
    };

    private static readonly LocaleData Ja = new()
    {
        Months = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"],
        MonthsShort = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"],
        Weekdays = ["日曜日", "月曜日", "火曜日", "水曜日", "木曜日", "金曜日", "土曜日"],
        WeekdaysShort = ["日", "月", "火", "水", "木", "金", "土"],
        WeekdaysNarrow = ["日", "月", "火", "水", "木", "金", "土"],
        DatePatterns = ["y/MM/dd", "y/MM/dd", "y年M月d日", "y年M月d日EEEE", "M月d日", "M月d日", "y年M月", "M月d日(EEE)"],
        Time24 = "H:mm",
        Time12 = "aK:mm",
        Uses24Hour = true,
        Am = "午前",
        Pm = "午後",
        Decimal = '.',
        Group = ",",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "今",
        Relative =
        [
            R(P1("{0} 分前"), P1("{0} 分後")),
            R(P1("{0} 時間前"), P1("{0} 時間後")),
            R(P1("{0} 日前"), P1("{0} 日後")),
            R(P1("{0} 週間前"), P1("{0} 週間後")),
            R(P1("{0} か月前"), P1("{0} か月後")),
            R(P1("{0} 年前"), P1("{0} 年後")),
        ],
        RelativeShort =
        [
            R(P1("{0} 分前"), P1("{0} 分後")),
            R(P1("{0} 時間前"), P1("{0} 時間後")),
            R(P1("{0} 日前"), P1("{0} 日後")),
            R(P1("{0} 週間前"), P1("{0} 週間後")),
            R(P1("{0} か月前"), P1("{0} か月後")),
            R(P1("{0} 年前"), P1("{0} 年後")),
        ],
    };

    private static readonly LocaleData Pl = new()
    {
        Months = ["styczeń", "luty", "marzec", "kwiecień", "maj", "czerwiec", "lipiec", "sierpień", "wrzesień", "październik", "listopad", "grudzień"],
        MonthsInDate = ["stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca", "lipca", "sierpnia", "września", "października", "listopada", "grudnia"],
        MonthsShort = ["sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru"],
        Weekdays = ["niedziela", "poniedziałek", "wtorek", "środa", "czwartek", "piątek", "sobota"],
        WeekdaysShort = ["niedz.", "pon.", "wt.", "śr.", "czw.", "pt.", "sob."],
        WeekdaysNarrow = ["N", "P", "W", "Ś", "C", "P", "S"],
        DatePatterns = ["dd.MM.y", "d MMM y", "d MMMM y", "EEEE, d MMMM y", "d MMMM", "d MMM", "LLLL y", "EEE, d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = "\u00A0",
        MinimumGrouping = 2,
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "teraz",
        Relative =
        [
            R(Pl3("{0} minutę temu", "{0} minuty temu", "{0} minut temu"), Pl3("za {0} minutę", "za {0} minuty", "za {0} minut")),
            R(Pl3("{0} godzinę temu", "{0} godziny temu", "{0} godzin temu"), Pl3("za {0} godzinę", "za {0} godziny", "za {0} godzin")),
            R(Pl3("{0} dzień temu", "{0} dni temu", "{0} dni temu"), Pl3("za {0} dzień", "za {0} dni", "za {0} dni")),
            R(Pl3("{0} tydzień temu", "{0} tygodnie temu", "{0} tygodni temu"), Pl3("za {0} tydzień", "za {0} tygodnie", "za {0} tygodni")),
            R(Pl3("{0} miesiąc temu", "{0} miesiące temu", "{0} miesięcy temu"), Pl3("za {0} miesiąc", "za {0} miesiące", "za {0} miesięcy")),
            R(Pl3("{0} rok temu", "{0} lata temu", "{0} lat temu"), Pl3("za {0} rok", "za {0} lata", "za {0} lat")),
        ],
        RelativeShort =
        [
            R(P1("{0} min temu"), P1("za {0} min")),
            R(P1("{0} godz. temu"), P1("za {0} godz.")),
            R(Pl3("{0} dzień temu", "{0} dni temu", "{0} dni temu"), Pl3("za {0} dzień", "za {0} dni", "za {0} dni")),
            R(P1("{0} tydz. temu"), P1("za {0} tydz.")),
            R(P1("{0} mies. temu"), P1("za {0} mies.")),
            R(Pl3("{0} rok temu", "{0} lata temu", "{0} lat temu"), Pl3("za {0} rok", "za {0} lata", "za {0} lat")),
        ],
    };

    private static readonly LocaleData Pt = new()
    {
        Months = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"],
        MonthsShort = ["jan.", "fev.", "mar.", "abr.", "mai.", "jun.", "jul.", "ago.", "set.", "out.", "nov.", "dez."],
        Weekdays = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"],
        WeekdaysShort = ["dom.", "seg.", "ter.", "qua.", "qui.", "sex.", "sáb."],
        WeekdaysNarrow = ["D", "S", "T", "Q", "Q", "S", "S"],
        DatePatterns = ["dd/MM/y", "d 'de' MMM 'de' y", "d 'de' MMMM 'de' y", "EEEE, d 'de' MMMM 'de' y", "d 'de' MMMM", "d 'de' MMM", "MMMM 'de' y", "EEE, d 'de' MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = ".",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "agora",
        Relative =
        [
            R(P("há {0} minuto", "há {0} minutos"), P("em {0} minuto", "em {0} minutos")),
            R(P("há {0} hora", "há {0} horas"), P("em {0} hora", "em {0} horas")),
            R(P("há {0} dia", "há {0} dias"), P("em {0} dia", "em {0} dias")),
            R(P("há {0} semana", "há {0} semanas"), P("em {0} semana", "em {0} semanas")),
            R(P("há {0} mês", "há {0} meses"), P("em {0} mês", "em {0} meses")),
            R(P("há {0} ano", "há {0} anos"), P("em {0} ano", "em {0} anos")),
        ],
        RelativeShort =
        [
            R(P1("há {0} min."), P1("em {0} min.")),
            R(P1("há {0} h"), P1("em {0} h")),
            R(P("há {0} dia", "há {0} dias"), P("em {0} dia", "em {0} dias")),
            R(P1("há {0} sem."), P1("em {0} sem.")),
            R(P("há {0} mês", "há {0} meses"), P("em {0} mês", "em {0} meses")),
            R(P("há {0} ano", "há {0} anos"), P("em {0} ano", "em {0} anos")),
        ],
    };

    private static readonly LocaleData Zh = new()
    {
        Months = ["一月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "十一月", "十二月"],
        MonthsShort = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"],
        Weekdays = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"],
        WeekdaysShort = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"],
        WeekdaysNarrow = ["日", "一", "二", "三", "四", "五", "六"],
        DatePatterns = ["y/M/d", "y年M月d日", "y年M月d日", "y年M月d日EEEE", "M月d日", "M月d日", "y年M月", "M月d日EEE"],
        Time24 = "HH:mm",
        Time12 = "ah:mm",
        Uses24Hour = true,
        Am = "上午",
        Pm = "下午",
        Decimal = '.',
        Group = ",",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "现在",
        Relative =
        [
            R(P1("{0}分钟前"), P1("{0}分钟后")),
            R(P1("{0}小时前"), P1("{0}小时后")),
            R(P1("{0}天前"), P1("{0}天后")),
            R(P1("{0}周前"), P1("{0}周后")),
            R(P1("{0}个月前"), P1("{0}个月后")),
            R(P1("{0}年前"), P1("{0}年后")),
        ],
        RelativeShort =
        [
            R(P1("{0}分钟前"), P1("{0}分钟后")),
            R(P1("{0}小时前"), P1("{0}小时后")),
            R(P1("{0}天前"), P1("{0}天后")),
            R(P1("{0}周前"), P1("{0}周后")),
            R(P1("{0}个月前"), P1("{0}个月后")),
            R(P1("{0}年前"), P1("{0}年后")),
        ],
    };

    // -----------------------------------------------------------------------------------------
    // [S-1112] The eight added later.
    // -----------------------------------------------------------------------------------------

    private static readonly LocaleData Hi = new()
    {
        Months = ["जनवरी", "फ़रवरी", "मार्च", "अप्रैल", "मई", "जून", "जुलाई", "अगस्त", "सितंबर", "अक्तूबर", "नवंबर", "दिसंबर"],
        MonthsShort = ["जन॰", "फ़र॰", "मार्च", "अप्रैल", "मई", "जून", "जुल॰", "अग॰", "सित॰", "अक्तू॰", "नव॰", "दिस॰"],
        Weekdays = ["रविवार", "सोमवार", "मंगलवार", "बुधवार", "गुरुवार", "शुक्रवार", "शनिवार"],
        WeekdaysShort = ["रवि", "सोम", "मंगल", "बुध", "गुरु", "शुक्र", "शनि"],
        WeekdaysNarrow = ["र", "सो", "मं", "बु", "गु", "शु", "श"],
        DatePatterns = ["d/M/y", "d MMM y", "d MMMM y", "EEEE, d MMMM y", "d MMMM", "d MMM", "MMMM y", "EEE, d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = false,
        Am = "am",
        Pm = "pm",
        Decimal = '.',
        Group = ",",
        SecondaryGrouping = 2,
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "अभी",
        Relative =
        [
            R(P1("{0} मिनट पहले"), P1("{0} मिनट में")),
            R(P("{0} घंटा पहले", "{0} घंटे पहले"), P1("{0} घंटे में")),
            R(P1("{0} दिन पहले"), P1("{0} दिन में")),
            R(P1("{0} सप्ताह पहले"), P1("{0} सप्ताह में")),
            R(P1("{0} माह पहले"), P1("{0} माह में")),
            R(P1("{0} वर्ष पहले"), P1("{0} वर्ष में")),
        ],
        RelativeShort =
        [
            R(P1("{0} मि॰ पहले"), P1("{0} मि॰ में")),
            R(P1("{0} घं॰ पहले"), P1("{0} घं॰ में")),
            R(P1("{0} दिन पहले"), P1("{0} दिन में")),
            R(P1("{0} सप्ताह पहले"), P1("{0} सप्ताह में")),
            R(P1("{0} माह पहले"), P1("{0} माह में")),
            R(P1("{0} वर्ष पहले"), P1("{0} वर्ष में")),
        ],
    };

    private static readonly RelativeForms[] ArabicRelative =
    [
        R(Ar6("قبل دقيقة واحدة", "قبل دقيقتين", "قبل {0} دقائق", "قبل {0} دقيقة", "قبل {0} دقيقة"), Ar6("خلال دقيقة واحدة", "خلال دقيقتين", "خلال {0} دقائق", "خلال {0} دقيقة", "خلال {0} دقيقة")),
        R(Ar6("قبل ساعة واحدة", "قبل ساعتين", "قبل {0} ساعات", "قبل {0} ساعة", "قبل {0} ساعة"), Ar6("خلال ساعة واحدة", "خلال ساعتين", "خلال {0} ساعات", "خلال {0} ساعة", "خلال {0} ساعة")),
        R(Ar6("قبل يوم واحد", "قبل يومين", "قبل {0} أيام", "قبل {0} يومًا", "قبل {0} يوم"), Ar6("خلال يوم واحد", "خلال يومين", "خلال {0} أيام", "خلال {0} يومًا", "خلال {0} يوم")),
        R(Ar6("قبل أسبوع واحد", "قبل أسبوعين", "قبل {0} أسابيع", "قبل {0} أسبوعًا", "قبل {0} أسبوع"), Ar6("خلال أسبوع واحد", "خلال أسبوعين", "خلال {0} أسابيع", "خلال {0} أسبوعًا", "خلال {0} أسبوع")),
        R(Ar6("قبل شهر واحد", "قبل شهرين", "قبل {0} أشهر", "قبل {0} شهرًا", "قبل {0} شهر"), Ar6("خلال شهر واحد", "خلال شهرين", "خلال {0} أشهر", "خلال {0} شهرًا", "خلال {0} شهر")),
        R(Ar6("قبل سنة واحدة", "قبل سنتين", "قبل {0} سنوات", "قبل {0} سنة", "قبل {0} سنة"), Ar6("خلال سنة واحدة", "خلال سنتين", "خلال {0} سنوات", "خلال {0} سنة", "خلال {0} سنة")),
    ];

    private static readonly LocaleData Ar = new()
    {
        Months = ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"],
        MonthsShort = ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"],
        Weekdays = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"],
        WeekdaysShort = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"],
        WeekdaysNarrow = ["ح", "ن", "ث", "ر", "خ", "ج", "س"],

        // CLDR puts RLMs between the numeric parts; the surfaces lay text out right-to-left
        // themselves ([S-1113]), and Latin digits joined by '/' already stay one left-to-right run.
        DatePatterns = ["d/M/y", "dd/MM/y", "d MMMM y", "EEEE، d MMMM y", "d MMMM", "d MMM", "MMMM y", "EEE، d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = false,
        Am = "ص",
        Pm = "م",

        // [S-1115] Arabic-Indic digits with their own separators (CLDR's "arab" numbering), written
        // into the text rather than left to the layout's substitution.
        Decimal = '٫',
        Group = "٬",
        Percent = "{0}٪",
        DigitZero = '٠',
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "الآن",
        Relative = ArabicRelative,

        // Arabic has no customary abbreviations for these units: the short style is the long one.
        RelativeShort = ArabicRelative,
    };

    private static readonly RelativeForms[] BengaliRelative =
    [
        R(P1("{0} মিনিট আগে"), P1("{0} মিনিটে")),
        R(P1("{0} ঘন্টা আগে"), P1("{0} ঘন্টায়")),
        R(P1("{0} দিন আগে"), P1("{0} দিনের মধ্যে")),
        R(P1("{0} সপ্তাহ আগে"), P1("{0} সপ্তাহে")),
        R(P1("{0} মাস আগে"), P1("{0} মাসে")),
        R(P1("{0} বছর আগে"), P1("{0} বছরে")),
    ];

    private static readonly LocaleData Bn = new()
    {
        Months = ["জানুয়ারী", "ফেব্রুয়ারী", "মার্চ", "এপ্রিল", "মে", "জুন", "জুলাই", "আগস্ট", "সেপ্টেম্বর", "অক্টোবর", "নভেম্বর", "ডিসেম্বর"],
        MonthsShort = ["জানু", "ফেব", "মার্চ", "এপ্রি", "মে", "জুন", "জুল", "আগ", "সেপ", "অক্টো", "নভে", "ডিসে"],
        Weekdays = ["রবিবার", "সোমবার", "মঙ্গলবার", "বুধবার", "বৃহস্পতিবার", "শুক্রবার", "শনিবার"],
        WeekdaysShort = ["রবি", "সোম", "মঙ্গল", "বুধ", "বৃহস্পতি", "শুক্র", "শনি"],
        WeekdaysNarrow = ["র", "সো", "ম", "বু", "বৃ", "শু", "শ"],
        DatePatterns = ["d/M/y", "d MMM, y", "d MMMM, y", "EEEE, d MMMM, y", "d MMMM", "d MMM", "MMMM y", "EEE, d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = false,
        Am = "AM",
        Pm = "PM",
        Decimal = '.',
        Group = ",",
        SecondaryGrouping = 2,
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "এখন",
        Relative = BengaliRelative,
        RelativeShort = BengaliRelative,
    };

    private static readonly LocaleData Ru = new()
    {
        Months = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"],
        MonthsInDate = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"],
        MonthsShort = ["янв.", "февр.", "март", "апр.", "май", "июнь", "июль", "авг.", "сент.", "окт.", "нояб.", "дек."],
        MonthsShortInDate = ["янв.", "февр.", "мар.", "апр.", "мая", "июн.", "июл.", "авг.", "сент.", "окт.", "нояб.", "дек."],
        Weekdays = ["воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота"],
        WeekdaysShort = ["вс", "пн", "вт", "ср", "чт", "пт", "сб"],
        WeekdaysNarrow = ["В", "П", "В", "С", "Ч", "П", "С"],
        DatePatterns = ["dd.MM.y", "d MMM y 'г'.", "d MMMM y 'г'.", "EEEE, d MMMM y 'г'.", "d MMMM", "d MMM", "LLLL y 'г'.", "EEE, d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = " ",
        Percent = "{0} %",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "сейчас",
        Relative =
        [
            R(Pl3("{0} минуту назад", "{0} минуты назад", "{0} минут назад"), Pl3("через {0} минуту", "через {0} минуты", "через {0} минут")),
            R(Pl3("{0} час назад", "{0} часа назад", "{0} часов назад"), Pl3("через {0} час", "через {0} часа", "через {0} часов")),
            R(Pl3("{0} день назад", "{0} дня назад", "{0} дней назад"), Pl3("через {0} день", "через {0} дня", "через {0} дней")),
            R(Pl3("{0} неделю назад", "{0} недели назад", "{0} недель назад"), Pl3("через {0} неделю", "через {0} недели", "через {0} недель")),
            R(Pl3("{0} месяц назад", "{0} месяца назад", "{0} месяцев назад"), Pl3("через {0} месяц", "через {0} месяца", "через {0} месяцев")),
            R(Pl3("{0} год назад", "{0} года назад", "{0} лет назад"), Pl3("через {0} год", "через {0} года", "через {0} лет")),
        ],
        RelativeShort =
        [
            R(P1("{0} мин. назад"), P1("через {0} мин.")),
            R(P1("{0} ч назад"), P1("через {0} ч")),
            R(P1("{0} дн. назад"), P1("через {0} дн.")),
            R(P1("{0} нед. назад"), P1("через {0} нед.")),
            R(P1("{0} мес. назад"), P1("через {0} мес.")),
            R(Pl3("{0} г. назад", "{0} г. назад", "{0} л. назад"), Pl3("через {0} г.", "через {0} г.", "через {0} л.")),
        ],
    };

    private static readonly LocaleData Id = new()
    {
        Months = ["Januari", "Februari", "Maret", "April", "Mei", "Juni", "Juli", "Agustus", "September", "Oktober", "November", "Desember"],
        MonthsShort = ["Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"],
        Weekdays = ["Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"],
        WeekdaysShort = ["Min", "Sen", "Sel", "Rab", "Kam", "Jum", "Sab"],
        WeekdaysNarrow = ["M", "S", "S", "R", "K", "J", "S"],
        DatePatterns = ["dd/MM/y", "d MMM y", "d MMMM y", "EEEE, dd MMMM y", "d MMMM", "d MMM", "MMMM y", "EEE, d MMM"],

        // Indonesian separates hours and minutes with a dot (and seconds too: 15.05.09).
        Time24 = "HH.mm",
        Time12 = "h.mm a",
        Uses24Hour = true,
        Am = "AM",
        Pm = "PM",
        Decimal = ',',
        Group = ".",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "sekarang",
        Relative =
        [
            R(P1("{0} menit yang lalu"), P1("dalam {0} menit")),
            R(P1("{0} jam yang lalu"), P1("dalam {0} jam")),
            R(P1("{0} hari yang lalu"), P1("dalam {0} hari")),
            R(P1("{0} minggu yang lalu"), P1("dalam {0} minggu")),
            R(P1("{0} bulan yang lalu"), P1("dalam {0} bulan")),
            R(P1("{0} tahun yang lalu"), P1("dalam {0} tahun")),
        ],
        RelativeShort =
        [
            R(P1("{0} mnt lalu"), P1("dalam {0} mnt")),
            R(P1("{0} jam lalu"), P1("dalam {0} jam")),
            R(P1("{0} h lalu"), P1("dalam {0} h")),
            R(P1("{0} mgg lalu"), P1("dalam {0} mgg")),
            R(P1("{0} bln lalu"), P1("dalam {0} bln")),
            R(P1("{0} thn lalu"), P1("dalam {0} thn")),
        ],
    };

    private static readonly RelativeForms[] UrduRelative =
    [
        R(P1("{0} منٹ پہلے"), P1("{0} منٹ میں")),
        R(P("{0} گھنٹہ پہلے", "{0} گھنٹے پہلے"), P1("{0} گھنٹے میں")),
        R(P1("{0} دن پہلے"), P1("{0} دن میں")),
        R(P("{0} ہفتہ پہلے", "{0} ہفتے پہلے"), P1("{0} ہفتے میں")),
        R(P1("{0} ماہ پہلے"), P1("{0} ماہ میں")),
        R(P1("{0} سال پہلے"), P1("{0} سال میں")),
    ];

    private static readonly LocaleData Ur = new()
    {
        Months = ["جنوری", "فروری", "مارچ", "اپریل", "مئی", "جون", "جولائی", "اگست", "ستمبر", "اکتوبر", "نومبر", "دسمبر"],
        MonthsShort = ["جنوری", "فروری", "مارچ", "اپریل", "مئی", "جون", "جولائی", "اگست", "ستمبر", "اکتوبر", "نومبر", "دسمبر"],
        Weekdays = ["اتوار", "پیر", "منگل", "بدھ", "جمعرات", "جمعہ", "ہفتہ"],
        WeekdaysShort = ["اتوار", "پیر", "منگل", "بدھ", "جمعرات", "جمعہ", "ہفتہ"],

        // CLDR's narrow Urdu weekdays are Latin (S M T …); a calendar header wants the script's own initials.
        WeekdaysNarrow = ["ا", "پ", "م", "ب", "ج", "ج", "ہ"],
        DatePatterns = ["d/M/y", "d MMM، y", "d MMMM، y", "EEEE، d MMMM، y", "d MMMM", "d MMM", "MMMM y", "EEE، d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = false,
        Am = "AM",
        Pm = "PM",
        Decimal = '.',
        Group = ",",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "ابھی",
        Relative = UrduRelative,
        RelativeShort = UrduRelative,
    };

    private static readonly LocaleData Mr = new()
    {
        Months = ["जानेवारी", "फेब्रुवारी", "मार्च", "एप्रिल", "मे", "जून", "जुलै", "ऑगस्ट", "सप्टेंबर", "ऑक्टोबर", "नोव्हेंबर", "डिसेंबर"],
        MonthsShort = ["जाने", "फेब्रु", "मार्च", "एप्रि", "मे", "जून", "जुलै", "ऑग", "सप्टें", "ऑक्टो", "नोव्हें", "डिसें"],
        Weekdays = ["रविवार", "सोमवार", "मंगळवार", "बुधवार", "गुरुवार", "शुक्रवार", "शनिवार"],
        WeekdaysShort = ["रवि", "सोम", "मंगळ", "बुध", "गुरु", "शुक्र", "शनि"],
        WeekdaysNarrow = ["र", "सो", "मं", "बु", "गु", "शु", "श"],
        DatePatterns = ["d/M/y", "d MMM, y", "d MMMM, y", "EEEE, d MMMM, y", "d MMMM", "d MMM", "MMMM y", "EEE, d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = false,
        Am = "AM",
        Pm = "PM",
        Decimal = '.',
        Group = ",",
        SecondaryGrouping = 2,
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "आत्ता",
        Relative =
        [
            R(P("{0} मिनिटापूर्वी", "{0} मिनिटांपूर्वी"), P("{0} मिनिटामध्ये", "{0} मिनिटांमध्ये")),
            R(P("{0} तासापूर्वी", "{0} तासांपूर्वी"), P("{0} तासामध्ये", "{0} तासांमध्ये")),
            R(P("{0} दिवसापूर्वी", "{0} दिवसांपूर्वी"), P("{0} दिवसामध्ये", "{0} दिवसांमध्ये")),
            R(P("{0} आठवड्यापूर्वी", "{0} आठवड्यांपूर्वी"), P("{0} आठवड्यामध्ये", "{0} आठवड्यांमध्ये")),
            R(P("{0} महिन्यापूर्वी", "{0} महिन्यांपूर्वी"), P("{0} महिन्यामध्ये", "{0} महिन्यांमध्ये")),
            R(P("{0} वर्षापूर्वी", "{0} वर्षांपूर्वी"), P("{0} वर्षामध्ये", "{0} वर्षांमध्ये")),
        ],
        RelativeShort =
        [
            R(P1("{0} मि. पूर्वी"), P1("{0} मि. मध्ये")),
            R(P1("{0} ता. पूर्वी"), P1("{0} ता. मध्ये")),
            R(P("{0} दिवसापूर्वी", "{0} दिवसांपूर्वी"), P("{0} दिवसामध्ये", "{0} दिवसांमध्ये")),
            R(P("{0} आठवड्यापूर्वी", "{0} आठवड्यांपूर्वी"), P("{0} आठवड्यामध्ये", "{0} आठवड्यांमध्ये")),
            R(P("{0} महिन्यापूर्वी", "{0} महिन्यांपूर्वी"), P("{0} महिन्यामध्ये", "{0} महिन्यांमध्ये")),
            R(P("{0} वर्षापूर्वी", "{0} वर्षांपूर्वी"), P("{0} वर्षामध्ये", "{0} वर्षांमध्ये")),
        ],
    };

    private static readonly RelativeForms[] VietnameseRelative =
    [
        R(P1("{0} phút trước"), P1("sau {0} phút")),
        R(P1("{0} giờ trước"), P1("sau {0} giờ")),
        R(P1("{0} ngày trước"), P1("sau {0} ngày")),
        R(P1("{0} tuần trước"), P1("sau {0} tuần")),
        R(P1("{0} tháng trước"), P1("sau {0} tháng")),
        R(P1("{0} năm trước"), P1("sau {0} năm")),
    ];

    private static readonly LocaleData Vi = new()
    {
        Months = ["Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6", "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"],
        MonthsInDate = ["tháng 1", "tháng 2", "tháng 3", "tháng 4", "tháng 5", "tháng 6", "tháng 7", "tháng 8", "tháng 9", "tháng 10", "tháng 11", "tháng 12"],
        MonthsShort = ["Thg 1", "Thg 2", "Thg 3", "Thg 4", "Thg 5", "Thg 6", "Thg 7", "Thg 8", "Thg 9", "Thg 10", "Thg 11", "Thg 12"],
        MonthsShortInDate = ["thg 1", "thg 2", "thg 3", "thg 4", "thg 5", "thg 6", "thg 7", "thg 8", "thg 9", "thg 10", "thg 11", "thg 12"],
        Weekdays = ["Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"],
        WeekdaysShort = ["CN", "Th 2", "Th 3", "Th 4", "Th 5", "Th 6", "Th 7"],
        WeekdaysNarrow = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"],
        DatePatterns = ["dd/MM/y", "d MMM, y", "d MMMM, y", "EEEE, d MMMM, y", "d MMMM", "d MMM", "LLLL 'năm' y", "EEE, d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "SA",
        Pm = "CH",
        Decimal = ',',
        Group = ".",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "bây giờ",
        Relative = VietnameseRelative,
        RelativeShort = VietnameseRelative,
    };

    // ko-KR. Korean writes the month as a number ("10월") and has no plural.
    private static readonly RelativeForms[] KoreanRelative =
    [
        R(P1("{0}분 전"), P1("{0}분 후")),
        R(P1("{0}시간 전"), P1("{0}시간 후")),
        R(P1("{0}일 전"), P1("{0}일 후")),
        R(P1("{0}주 전"), P1("{0}주 후")),
        R(P1("{0}개월 전"), P1("{0}개월 후")),
        R(P1("{0}년 전"), P1("{0}년 후")),
    ];

    private static readonly LocaleData Ko = new()
    {
        Months = ["1월", "2월", "3월", "4월", "5월", "6월", "7월", "8월", "9월", "10월", "11월", "12월"],
        MonthsShort = ["1월", "2월", "3월", "4월", "5월", "6월", "7월", "8월", "9월", "10월", "11월", "12월"],
        Weekdays = ["일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일"],
        WeekdaysShort = ["일", "월", "화", "수", "목", "금", "토"],
        WeekdaysNarrow = ["일", "월", "화", "수", "목", "금", "토"],
        DatePatterns = ["y. M. d.", "y. M. d.", "y년 M월 d일", "y년 M월 d일 EEEE", "MMMM d일", "MMM d일", "y년 MMMM", "MMM d일 (EEE)"],
        Time24 = "H:mm",
        Time12 = "a h:mm",
        Uses24Hour = false,
        Am = "오전",
        Pm = "오후",
        Decimal = '.',
        Group = ",",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "지금",
        Relative = KoreanRelative,
        RelativeShort = KoreanRelative,
    };

    private static readonly LocaleData Nl = new()
    {
        Months = ["januari", "februari", "maart", "april", "mei", "juni", "juli", "augustus", "september", "oktober", "november", "december"],
        MonthsShort = ["jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec"],
        Weekdays = ["zondag", "maandag", "dinsdag", "woensdag", "donderdag", "vrijdag", "zaterdag"],
        WeekdaysShort = ["zo", "ma", "di", "wo", "do", "vr", "za"],
        WeekdaysNarrow = ["Z", "M", "D", "W", "D", "V", "Z"],
        DatePatterns = ["dd-MM-y", "d MMM y", "d MMMM y", "EEEE d MMMM y", "d MMMM", "d MMM", "MMMM y", "EEE d MMM"],
        Time24 = "HH:mm",
        Time12 = "h:mm a",
        Uses24Hour = true,
        Am = "a.m.",
        Pm = "p.m.",
        Decimal = ',',
        Group = ".",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "nu",
        Relative =
        [
            R(P("{0} minuut geleden", "{0} minuten geleden"), P("over {0} minuut", "over {0} minuten")),
            R(P1("{0} uur geleden"), P1("over {0} uur")),
            R(P("{0} dag geleden", "{0} dagen geleden"), P("over {0} dag", "over {0} dagen")),
            R(P("{0} week geleden", "{0} weken geleden"), P("over {0} week", "over {0} weken")),
            R(P("{0} maand geleden", "{0} maanden geleden"), P("over {0} maand", "over {0} maanden")),
            R(P1("{0} jaar geleden"), P1("over {0} jaar")),
        ],
        RelativeShort =
        [
            R(P1("{0} min geleden"), P1("over {0} min")),
            R(P1("{0} uur geleden"), P1("over {0} uur")),
            R(P("{0} dag geleden", "{0} dagen geleden"), P("over {0} dag", "over {0} dagen")),
            R(P1("{0} wk geleden"), P1("over {0} wk")),
            R(P1("{0} mnd geleden"), P1("over {0} mnd")),
            R(P1("{0} jr geleden"), P1("over {0} jr")),
        ],
    };

    // tr-TR. A Turkish noun stays singular after a number ("3 dakika"), so one and other read alike.
    private static readonly LocaleData Tr = new()
    {
        Months = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"],
        MonthsShort = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"],
        Weekdays = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"],
        WeekdaysShort = ["Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"],
        WeekdaysNarrow = ["P", "P", "S", "Ç", "P", "C", "C"],
        DatePatterns = ["d.MM.y", "d MMM y", "d MMMM y", "d MMMM y EEEE", "d MMMM", "d MMM", "MMMM y", "d MMM EEE"],
        Time24 = "HH:mm",
        Time12 = "a h:mm",
        Uses24Hour = true,
        Am = "ÖÖ",
        Pm = "ÖS",
        Decimal = ',',
        Group = ".",
        Percent = "%{0}",
        FirstDayOfWeek = DayOfWeek.Monday,
        Now = "şimdi",
        Relative =
        [
            R(P1("{0} dakika önce"), P1("{0} dakika sonra")),
            R(P1("{0} saat önce"), P1("{0} saat sonra")),
            R(P1("{0} gün önce"), P1("{0} gün sonra")),
            R(P1("{0} hafta önce"), P1("{0} hafta sonra")),
            R(P1("{0} ay önce"), P1("{0} ay sonra")),
            R(P1("{0} yıl önce"), P1("{0} yıl sonra")),
        ],
        RelativeShort =
        [
            R(P1("{0} dk. önce"), P1("{0} dk. sonra")),
            R(P1("{0} sa. önce"), P1("{0} sa. sonra")),
            R(P1("{0} gün önce"), P1("{0} gün sonra")),
            R(P1("{0} hf. önce"), P1("{0} hf. sonra")),
            R(P1("{0} ay önce"), P1("{0} ay sonra")),
            R(P1("{0} yıl önce"), P1("{0} yıl sonra")),
        ],
    };

    // zh-Hant as written in Taiwan (zh-TW): the 12-hour clock, Sunday first, 週 for the weekday.
    private static readonly RelativeForms[] TraditionalChineseRelative =
    [
        R(P1("{0} 分鐘前"), P1("{0} 分鐘後")),
        R(P1("{0} 小時前"), P1("{0} 小時後")),
        R(P1("{0} 天前"), P1("{0} 天後")),
        R(P1("{0} 週前"), P1("{0} 週後")),
        R(P1("{0} 個月前"), P1("{0} 個月後")),
        R(P1("{0} 年前"), P1("{0} 年後")),
    ];

    private static readonly LocaleData ZhHant = new()
    {
        Months = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"],
        MonthsShort = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"],
        Weekdays = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"],
        WeekdaysShort = ["週日", "週一", "週二", "週三", "週四", "週五", "週六"],
        WeekdaysNarrow = ["日", "一", "二", "三", "四", "五", "六"],
        DatePatterns = ["y/M/d", "y年M月d日", "y年M月d日", "y年M月d日 EEEE", "M月d日", "M月d日", "y年M月", "M月d日 EEE"],
        Time24 = "HH:mm",
        Time12 = "ah:mm",
        Uses24Hour = false,
        Am = "上午",
        Pm = "下午",
        Decimal = '.',
        Group = ",",
        Percent = "{0}%",
        FirstDayOfWeek = DayOfWeek.Sunday,
        Now = "現在",
        Relative = TraditionalChineseRelative,
        RelativeShort = TraditionalChineseRelative,
    };
}

/// <summary>One text per plural category; a missing category uses <see cref="Other"/>.</summary>
internal readonly record struct PluralText(string Other, string? One = null, string? Few = null, string? Many = null, string? Zero = null, string? Two = null)
{
    public string For(PluralCategory category) => category switch
    {
        PluralCategory.Zero => Zero ?? Other,
        PluralCategory.One => One ?? Other,
        PluralCategory.Two => Two ?? Other,
        PluralCategory.Few => Few ?? Other,
        PluralCategory.Many => Many ?? Other,
        _ => Other,
    };
}

/// <summary>"… ago" and "in …" for one unit.</summary>
internal readonly record struct RelativeForms(PluralText Past, PluralText Future);

# Widget design rules (the owner's, verbatim)

The one checklist every widget is judged against. Each rule is the owner's own words with its date. Nothing here
is a helper's reading of them, and **no exemption exists unless the owner granted it in words** (quote it under the
rule). A check script, a SPEC entry or a render option that allows less than a rule here is wrong, not the rule.

The widget audit (`.claude/workflows/widget-audit.js`) renders every kind in every state at many sizes, runs the
machine checks, then has independent reviewers judge every kind against **every** rule below and records a
pass/fail per kind × rule. A widget change is done only when that audit passes.

## Layout

- **WR-1 No space at the top.** 2026-11-19: "no this is fine if it happens, but there should not be a space at the
  top"; "the drive widget still has a fucking space at the top, maybe also at the bottom. make sure that this does
  not happen anywhere."
- **WR-2 Items touch: no spacing between them.** 2026-11-19: "in terms of design i want the widgets all to have the
  same design philosopy, the items have no spacing between them and they try to take up the whole space from top to
  bottom and left to right."
- **WR-3 Items reach from edge to edge.** Same quote as WR-2, and 2026-11-19: "and the items dont reach from edge to
  edge. how did you miss that? you might have missed this for others too." An item's box (its row band, cell, hover
  area) runs from the card's left edge to its right edge, the first from the card's top; the inset is padding inside
  the item, never a margin round the items.
- **WR-4 Items keep their fixed size; the last one is cut.** 2026-11-20: "the widget items should actually have a fixed
  size. then when you change the height of a widget the items should not grow or shrink to fit all the way from the top
  to the bottom, they should just keep their size and the last item will be cutoff to some extent then." A repeated item
  never grows or shrinks with its card: the items stand from the content's top (WR-1), touching (WR-2), and the last one
  the card reaches is cut by its foot (or, for items side by side, its right edge).
  Replaced 2026-11-20, the earlier WR-4 "Items stretch to fill", 2026-11-19: "the weather forecast widget and others too
  the items still dont reach to the top and bottom. they still have a little / small gap. they should stretch if they
  dont reach all the way."
- **WR-5 Empty room only at the foot.** 2026-11-19, of a list with fewer items than its stated rows: "no this is fine if
  it happens, but there should not be a space at the top". Never a space above the first item or between items. Under
  the last item: a list with more items than its card holds has its last item cut by the card's foot (WR-4, 2026-11-20:
  "the last item will be cutoff to some extent then"); one with fewer items than room leaves the room under its last
  item empty.
  Replaced 2026-11-20, the earlier WR-5's "never a part row or leftover room under the last item: the items stretch to
  fill the card to its foot", from 2026-11-20: "now the weather forecast widget always has one block space at the
  bottom. this should obviously not be"; "same for the task widget, there is also a one block space at the bottom now.
  why is that?"; "right now all of the widgets have a space on the bottom, this means their size steps are not in sync
  with the item heights. fix this if you are not already fixing it."; "the calendar does not always stretch the contents
  to the top and bottom although it would be easily possible."
- **WR-6 Items have one fixed size; widgets step by one cell.** 2026-11-20: "the widget items should actually have a
  fixed size. then when you change the height of a widget the items should not grow or shrink to fit all the way from
  the top to the bottom, they should just keep their size and the last item will be cutoff to some extent then." And, of
  the widget's size, 2026-11-20: "the widgets should not have the size increments anymore but should be incrementable in
  steps of one." Every repeated item has one size, its optimal: its natural length in whole desktop grid cells along the
  axis it repeats; it is that size at every size of its card; all items of a widget are the same size. A widget resizes
  by one grid cell on each axis, whatever its items.
  Replaced 2026-11-20, the WR-6 "Items have an optimal size and stretch a little", 2026-11-20: "actually, the multiple
  of its children size should be relaxed, also that the size of the items can only be a defined block size, in general
  they should try to have a fixed block size but dont have to. for you to understand why i am changing this back again,
  if the widgets only have steps in increments of four or six for example, you cannot properly align them. lets not
  limit the user in the alignment. the items should have an optimal size and then they may stretch a little to fill up
  the remaining space. the widgets should not have the size increments anymore but should be incrementable in steps of
  one. does this make sense?"
  Replaced 2026-11-20, the earlier WR-6 "Items are whole grid cells", 2026-11-19: "items inside widgets should be
  designed in a way that they match the size of the grid on the desktop, for example they should be designed so that
  they are 3 blocks high or wide, that means that a widget that is 10 blocks hight should not show 4 items because then
  the items would have an uneven size which i dont want. do this for all the widgets."
- **WR-7 A reading and its caption stay together and scale up.** 2026-11-19, answers: Readouts "All together,
  scaled"; "Keep them together". The reading, its caption and its controls are one block that grows to fill.
- **WR-8 Clock without top and bottom space.** 2026-11-19: "the clock widget has too big of a space at the top and
  bottom."
- **WR-9 Calendar cells and numbers fill the card.** 2026-11-19: "the calendar does not follow this design philosophy
  for example. fix that"; answer "Bigger numbers too"; "the calendar does not match the fucking design rules i told
  you." 2026-11-20: "the calendar shows the individual events too early, the items should stay small for a little
  longer." A day cell keeps its small marks until it clearly holds a readable chip line. The month is a grid of the
  card, not a list: its day cells share the card (WR-4 and WR-6 are about a widget's repeated list items).

- **WR-14 A face that scales scales down too.** 2026-11-19: "you should be able to make the clock even smaller than it
  is right now. for the widgets that can scale there should basically be no lower limit except for one that makes
  sense so you can still see the widget." A face that scales as one (a clock, a date, a reading) is sized down to where
  its smallest text is still read (9 DIP; a glyph 12) and never under the grid's least widget (32 DIP); rows and list
  items keep their one fixed size (WR-6; a list's least is one item).
- **WR-15 A running reading never moves.** 2026-11-20: "the clock right now changes its layout depending on the time
  that it shows because not all of the chars have the same width." (SPEC [S-14319]; the gap check judges a held reading
  by its template, `holds`.)

- **WR-16** *(Retired 2026-11-22.)* Atoms on one universal spacing, asked for the now playing group alone ("for now
  just for the now playing group"). The owner reset it: "actually they should not all have a shared base. i think it
  makes more sense for them to keep their individuality. basically reset them to what they were a few days ago";
  asked which point, "before 19th. but the single metric widgets stay". The now playing widgets keep their own layout
  again, judged by WR-1..WR-15. Still in force from that day: "all the now playing widgets should have the option to be
  hidden when noting plays. in edit mode they should appear normally though".

## Content

- **WR-10 Defaults, never hidden.** 2026-11-19: "even if there is no rain in the next two hours or even if there is no
  cpu data available it should show the default values in the graph and not just not show the graph. for both cases.
  also for other widgets." No data, no account, not set up, offline: the widget keeps its face (its graph, its rows,
  its month, its reading as "--") and says why in a compact line; it never swaps its face for a status line alone.
- **WR-11 One base, little variance.** 2026-11-19: "they dont need to have an icon neccsarily, but they should reuse
  the same base. generally i feel like the widgets have to much variance in themselves. they should reuse more of the
  same components." Rows look like rows everywhere (same heights, type, padding, hover band); facts lists, item lists
  and section headings are the shared components.
- **WR-12 Nothing changes size on hover or press.** 2026-11-19: "some widgets like the now playing scrubber changes
  text sizes when hovering over it with the mouse" … "this should not be". Text, items and the scale of a block are
  the same at rest, under the pointer and pressed. 2026-11-19: "there are layout shifts when i click add task in the
  task widget. layout shifts for these kind of inputs should never happen. fix for all the widgets". An inline input
  stands in exactly the box of what it replaces: opening, typing, committing or cancelling it moves nothing else.
- **WR-13 Hover lights the item under the pointer, edge to edge.** 2026-11-19: "the hover areas of the disk widgets
  are fucked up and wrong. when hovering over a widget it also does not show the hover effect, it just shows it for
  all the other same kind disk widgets."; 2026-11-19: "when hovering over an email it highlights multiple emails. it
  should only highglight exactly the same item, not all from the sender for example as it does right now." Hover
  lights exactly the item under the pointer; a link to another widget lights only that same item there, never
  several items that share a sender, organiser or kind.

## Exemptions granted by the owner

- **Pictures and video.** A kind whose face is a picture or a video filling the card edge to edge (gallery, video,
  cover art) meets WR-1..WR-4 by that picture.
- **Tiles stand centred.** 2026-11-19, asked whether a quick toggle's or power button's icon and name may stay
  centred in the tile: "Centred is fine". The bands over and under a tile's face are alike; nothing else of WR-1..WR-4
  is relaxed.
- **A one-line face may stand centred.** 2026-11-19, asked about clock, date and the Header widget, whose one line grows
  until the width stops it: "Centred is fine". The leftover height is shared alike over and under the line.
- **A reading that fills one side may stand centred on the other.** 2026-11-20, asked whether a reading and its caption
  that fill the card's height but not its width (or the reverse) may stand centred like the clock, or the widget should
  rearrange to fill (the caption beside the reading): "Centred is fine". The block grows until one axis stops it, and
  WR-1..WR-4 hold on that axis; the leftover on the other axis is shared alike on both sides. Lists, graphs and items
  are not relaxed.
- **A wrapped grid keeps fixed cells, a gap at the right.** 2026-11-23, asked "When the weather forecast's Grid style or
  the calendar's year view wraps onto a second line, how should the day/month cells behave?", the owner chose "Fixed size,
  gap at right": "Every cell keeps its own size (80 DIP) at every card width; whole columns only, so up to one cell of
  empty room stays at the right edge. Matches 'one fixed size'." In such a grid the room past the last whole column,
  less than one cell, is not a WR-3 or WR-5 failure; only the last line is cut, by the foot (WR-4).
- **The Metric widget's head: name top left, value top right.** 2026-11-23: "the metric should have the name and an icon for
  the metric on the topleft and then the current value of the metric on the topright".
- **Quick toggles go under the least widget.** 2026-11-23, of the quick settings, whose least then stood at the 32 of WR-14:
  "the toggle buttons should be possible to be even smaller". A quick setting's (and a power button's) tile goes down to its
  glyph at 12 in a 4 inset (20); its words keep the 9 floor of WR-14 and give way before the glyph.
- **A device list's block gap.** 2026-11-23, of the device widgets: "optionally it should be possible to add a block gap
  into the widget so that they are dymanic but it looks like different widgets." With a device list's "Gap between items"
  on (off by default), one grid cell stands between two items and the card paints a ground under each item alone, so each
  looks like a widget of its own; that cell is not a WR-2 or WR-5 failure. Every item keeps WR-1, WR-3, WR-4 and WR-6.
- **Now playing's transport stands right-aligned.** 2026-11-23: "for the now playing widget move the action buttons on
  the right side of the text and right align them". The previous, play/pause and next buttons stand at the right inset,
  beside the texts or alone; the room at their left when they stand alone is not a WR-3 failure.
- **Desktop icons keep Windows' look.** 2026-11-19, asked whether the widget rules apply to desktop icons: "Keep
  Windows' look". A desktop icon follows Windows' icon cell (and the icon layouts, [S-13970]), not WR-1..WR-6.

Nothing else is exempt. Graphs are not: 2026-11-19, asked whether a graph's plot should reach the card's edges: "Plot
edge to edge" (the plot and its fill reach the card's edges, the labels stand inside it, over the plot).

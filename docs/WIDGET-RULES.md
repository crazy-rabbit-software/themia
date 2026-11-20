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
- **WR-4 Items stretch to fill.** 2026-11-19: "the weather forecast widget and others too the items still dont reach
  to the top and bottom. they still have a little / small gap. they should stretch if they dont reach all the way."
- **WR-5 Empty room only at the foot, and only as empty rows.** 2026-11-19, of a list with fewer items than its stated
  rows: "no this is fine if it happens, but there should not be a space at the top". 2026-11-20: "now the weather
  forecast widget always has one block space at the bottom. this should obviously not be"; "same for the task widget,
  there is also a one block space at the bottom now. why is that?"; "right now all of the widgets have a space on the
  bottom, this means their size steps are not in sync with the item heights. fix this if you are not already fixing
  it."; "the calendar does not always stretch the contents to the top and bottom although it would be easily
  possible." Empty stated rows may stand at the foot, as rows; never above the first item, never between items, and
  never a part row or leftover room under the last item: the items stretch to fill the card to its foot (WR-6).
- **WR-6 Items have an optimal size and stretch a little; widgets step by one cell.** 2026-11-20: "actually, the
  multiple of its children size should be relaxed, also that the size of the items can only be a defined block size,
  in general they should try to have a fixed block size but dont have to. for you to understand why i am changing this
  back again, if the widgets only have steps in increments of four or six for example, you cannot properly align them.
  lets not limit the user in the alignment. the items should have an optimal size and then they may stretch a little to
  fill up the remaining space. the widgets should not have the size increments anymore but should be incrementable in
  steps of one. does this make sense?" Every repeated item has an optimal size, a whole number of desktop grid cells
  along the axis it repeats; a card shows as many items as hold that size and they stretch evenly to fill it, never
  smaller than their optimal size; all items of a widget are the same size. A widget resizes by one grid cell on each
  axis, whatever its items.
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
  longer." A day cell keeps its small marks until it clearly holds a readable chip line.

- **WR-14 A face that scales scales down too.** 2026-11-19: "you should be able to make the clock even smaller than it
  is right now. for the widgets that can scale there should basically be no lower limit except for one that makes
  sense so you can still see the widget." A face that scales as one (a clock, a date, a reading) is sized down to where
  its smallest text is still read (9 DIP; a glyph 12) and never under the grid's least widget (32 DIP); rows and list
  items keep their optimal size (WR-6; a list's least is one item).

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
- **Desktop icons keep Windows' look.** 2026-11-19, asked whether the widget rules apply to desktop icons: "Keep
  Windows' look". A desktop icon follows Windows' icon cell (and the icon layouts, [S-13970]), not WR-1..WR-6.

Nothing else is exempt. Graphs are not: 2026-11-19, asked whether a graph's plot should reach the card's edges: "Plot
edge to edge" (the plot and its fill reach the card's edges, the labels stand inside it, over the plot).

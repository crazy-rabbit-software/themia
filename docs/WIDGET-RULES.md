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
- **WR-5 Empty room only at the foot.** 2026-11-19, of a list with fewer items than its stated rows: "no this is fine
  if it happens, but there should not be a space at the top". Empty stated rows / leftover whole cells may stand at
  the foot; never above the first item, never between items.
- **WR-6 Items are whole grid cells.** 2026-11-19: "items inside widgets should be designed in a way that they match
  the size of the grid on the desktop, for example they should be designed so that they are 3 blocks high or wide,
  that means that a widget that is 10 blocks hight should not show 4 items because then the items would have an
  uneven size which i dont want. do this for all the widgets." Every repeated item is a whole number of desktop grid
  cells along the axis it repeats; all items of a widget are the same size.
- **WR-7 A reading and its caption stay together and scale up.** 2026-11-19, answers: Readouts "All together,
  scaled"; "Keep them together". The reading, its caption and its controls are one block that grows to fill.
- **WR-8 Clock without top and bottom space.** 2026-11-19: "the clock widget has too big of a space at the top and
  bottom."
- **WR-9 Calendar cells and numbers fill the card.** 2026-11-19: "the calendar does not follow this design philosophy
  for example. fix that"; answer "Bigger numbers too"; "the calendar does not match the fucking design rules i told
  you."

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
  the same at rest, under the pointer and pressed.
- **WR-13 Hover lights the item under the pointer, edge to edge.** 2026-11-19: "the hover areas of the disk widgets
  are fucked up and wrong. when hovering over a widget it also does not show the hover effect, it just shows it for
  all the other same kind disk widgets."; 2026-11-19: "when hovering over an email it highlights multiple emails. it
  should only highglight exactly the same item, not all from the sender for example as it does right now." Hover
  lights exactly the item under the pointer; a link to another widget lights only that same item there, never
  several items that share a sender, organiser or kind.

## Exemptions granted by the owner

None yet. A kind whose face is a picture or a video filling the card edge to edge (gallery, video, cover art) meets
WR-1..WR-4 by that picture; nothing else is exempt.

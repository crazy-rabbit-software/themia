# Elements (text, divider)

The design elements family ([S-6911]): widgets that show no data, only what the person puts there (owner, 2026-11-21:
"there should also be additional design elements that you can add too. like icons or own text or dividers ... if there is
nothing add this widget type to the widgets too"). Its tags are [S-10100]..[S-10199], this file's alone; the root
`SPEC.md` holds the host's sections.

Owning files: everything in this folder.

- **[S-10100]** A design element is a widget like any other, placed alone (2026-11-22: the forecast it could also be part
  of is one widget again, [S-6163]; the owner's 21 Nov request for own text and dividers stands). All three are on the add
  page under Notes & tools.
- **[S-10101]** **Text** (`text`): one line of the person's own words on the one text face (`TextView`, [S-14930]), no
  lead, aligned as any text ([S-6583]); typed in place on its page (Words). With none yet it shows its name, unavailable.
- **[S-16430]** **Own text stands centred until an alignment is chosen** (docs/WIDGET-RULES.md, owner 2026-11-20, of a
  reading that fills one axis: "Centred is fine"; "the leftover on the other axis is shared alike on both sides"): its
  default alignment is Center (`TextElementConfig.DefaultAlign`), so a line the height stops shares the width left over
  alike (it stood 12 from the left and 34 from the right of a 240 × 100 card); Left and Right stay the person's choice
  ([S-6580]). Test `ElementsTests.S16430_`.
- **[S-16431]** **Own text without words says why** (WR-10: "not set up ... says why in a compact line"): its name stays,
  unavailable, and the text face's compact line says "No words yet: type them under Words" at the card's foot, or beside
  the name in a card one line tall, its tooltip where neither holds it (`ElementKinds.Shown`, [S-15710], [S-15716]). Test
  `ElementsTests.S16431_`.
- **[S-10102]** **The Icon widget** (`icon`, its own module, [S-15010]) is a design element too: there is one icon widget, on the icon base ([S-15003]), never a second here.
- **[S-10103]** **Divider** (`divider`): a hairline in the track role through its box's middle along its long side, edge
  to edge: across a box wider than tall, down one taller than wide; nothing to set; at least one grid cell.

Needs a manual pass: each at its min, natural and max on the live desktop (showcase views, [S-1810]).

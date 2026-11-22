# Elements (text, divider)

The design elements family ([S-6911]): widgets that show no data, only what the person puts there (owner, 2026-11-21:
"there should also be additional design elements that you can add too. like icons or own text or dividers ... if there is
nothing add this widget type to the widgets too"). Its tags are [S-10100]..[S-10199], this file's alone; the root
`SPEC.md` holds the host's sections.

Owning files: everything in this folder.

- **[S-10100]** A design element's config is an `IDesignElement`: it belongs in any composition, a weather forecast's
  day included ([S-15528]). All three are on the add page under Notes & tools.
- **[S-10101]** **Text** (`text`): one line of the person's own words on the one text face (`TextView`, [S-14930]), no
  lead, aligned as any text ([S-6583]); typed in place on its page (Words). With none yet it shows its name, unavailable.
- **[S-10102]** **The Icon widget** (`icon`, its own module, [S-15010]) is a design element too (`IconWidgetConfig` is an
  `IDesignElement`): there is one icon widget, on the icon base ([S-15003]), never a second here.
- **[S-10103]** **Divider** (`divider`): a hairline in the track role through its box's middle along its long side, edge
  to edge: across a box wider than tall, down one taller than wide; nothing to set; at least one grid cell.

Needs a manual pass: each at its min, natural and max on the live desktop (showcase views, [S-1810]).

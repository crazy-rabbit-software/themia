# Third-party notices

Themia bundles the following third-party material. Each notice below applies only to the files
it names.

## Fluent UI System Icons

- Source: https://github.com/microsoft/fluentui-system-icons ("Color" variants, unmodified)
- Used for: the icons of the settings window's navigation pane
- Files: `src/Themia.App/Assets/NavIcons/`
  - `ic_fluent_apps_20_color.svg`
  - `ic_fluent_content_view_20_color.svg`
  - `ic_fluent_document_text_20_color.svg`
  - `ic_fluent_globe_20_color.svg`
  - `ic_fluent_laptop_20_color.svg`
  - `ic_fluent_paint_brush_20_color.svg`
  - `ic_fluent_person_20_color.svg`
  - `ic_fluent_premium_20_color.svg`
  - `ic_fluent_question_circle_20_color.svg`
  - `ic_fluent_settings_20_color.svg`

```
MIT License

Copyright (c) 2020 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## WinUI Gallery icon list

- Source: https://github.com/microsoft/WinUI-Gallery, `WinUIGallery/Samples/Iconography/IconsData.json`
  (commit 14a4a1a2b8ddc527dc4a7d5f7e743d7c2bc97db7): the names, code points and search tags of the
  Segoe Fluent Icons glyphs. The glyphs themselves come from the font that ships with Windows; no
  font file is bundled.
- Used for: the icon picker of views and groups, and its search
- Files: `src/Themia.Core/Icons/FluentIconData.g.cs` (converted by `scripts/fluent-icons.py`:
  tags lower-cased and de-duplicated, the `symbol-icon` marker dropped)

```
MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE
```

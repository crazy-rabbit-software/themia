using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The About page. [S-493]
/// <para>
/// The same page as karpo's, section for section, so the two apps from the studio read as one
/// family: the app's own name card (the mark over the name, the version and what it runs on), who
/// made it, the one thing you can do to the running app — quit it — and the settings file, which
/// can be exported, imported back with a preview of what changes, and opened.
/// </para>
/// </summary>
internal static partial class ConfigPages
{
    /// <summary>The name card's mark, in DIPs. Decoded logically, so it is sharp at any display scale.</summary>
    private const int AboutLogoSize = 64;

    /// <summary>How many lines of an import preview the dialog lists before it says "and N more".</summary>
    private const int PreviewLines = 12;

    private static FrameworkElement BuildAbout(AppHost host)
    {
        var rows = new List<FrameworkElement> { NameCard() };

        rows.Add(FluentLayout.Header(Str.Shell_About_MadeBy.Text()));

        var link = new HyperlinkButton
        {
            Content = "lometics.com",
            NavigateUri = new Uri("https://lometics.com/"),
        };

        rows.Add(FluentLayout.Row(CompanyMark(), "Lometics",
            Str.Shell_About_Studio.Text(), link));

        rows.Add(FluentLayout.Header("Themia"));

        var quit = new Button { Content = Str.Settings_Quit_Action.Text() };
        quit.Click += (_, _) => host.RequestExit();
        rows.Add(FluentLayout.Row(Glyphs.Power, Str.Settings_Quit_Label.Text(), Str.Shell_About_QuitDesc.Text(), quit));

        rows.Add(FluentLayout.Header(Str.Shell_About_SettingsFile.Text()));

        var export = new Button { Content = Str.Shell_About_ExportEllipsis.Text() };
        export.Click += (_, _) => _ = ExportAsync(host, export);
        rows.Add(FluentLayout.Row(Glyphs.Export, Str.Settings_ExportSettings_Label.Text(),
            Str.Shell_About_ExportDesc.Text(), export));

        var stateName = Path.GetFileName(host.StatePath);
        var import = new Button { Content = Str.Shell_About_ImportEllipsis.Text() };
        import.Click += (_, _) => _ = ImportAsync(host, import);
        rows.Add(FluentLayout.Row(Glyphs.Import, Str.Settings_ImportSettings_Label.Text(),
            Str.Shell_About_ImportDesc.Format(("file", stateName)),
            import));

        rows.Add(FluentLayout.ClickRow(Glyphs.Code, Str.Shell_About_OpenConfig.Text(), host.StatePath, () => OpenConfigFile(host)));

        // [S-892] Back to a fresh profile, with what is there now kept beside the settings file.
        rows.Add(FluentLayout.ConfirmRow(
            Glyphs.Reset,
            Str.Shell_About_ResetAll.Text(),
            Str.Shell_About_ResetAllDesc.Text(),
            Str.Shell_About_ResetEllipsis.Text(),
            Str.Shell_About_ResetConfirm.Text(),
            () => Str.Shell_About_ResetBody.Format(("file", stateName)),
            () =>
            {
                if (host.ResetAll() is { } problem)
                {
                    Notices.Error(Str.Shell_About_ResetFailed.Text(), problem);
                }
            }));

        // [S-1552] The very last card on the page, under its own heading.
        rows.Add(FluentLayout.Header(Str.Shell_Uninstall_Action.Text()));
        rows.Add(UninstallRow(host));

        return Page(Str.Settings_Section_About.Text(), rows);
    }

    /// <summary>The app's own name card, as Windows Settings starts its About page: the mark over the name and version.</summary>
    private static StackPanel NameCard()
    {
        var logo = new Image
        {
            Width = AboutLogoSize,
            Height = AboutLogoSize,
            Margin = new Thickness(0, 0, 0, Fluent.Space2),
            HorizontalAlignment = HorizontalAlignment.Left,

            // Logical decoding: 64 DIPs is 128 pixels at 200%, and WIC is asked for exactly that
            // rather than for a 256px bitmap scaled down on every frame.
            Source = new BitmapImage
            {
                DecodePixelType = DecodePixelType.Logical,
                DecodePixelWidth = AboutLogoSize,
                UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "themia-logo.png")),
            },
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(logo, Str.Shell_About_ThemiaLogo.Text());

        var name = new TextBlock { Text = "Themia" };
        if (Application.Current.Resources.TryGetValue("SubtitleTextBlockStyle", out var subtitle) && subtitle is Style style)
        {
            name.Style = style;
        }

        var version = new TextBlock
        {
            Text = Str.Shell_About_Version.Format(("version", AppInfo.Version)),
            Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
        };

        var runtime = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        runtime.Text = RuntimeLine();

        return new StackPanel
        {
            Spacing = Fluent.Space1,
            Margin = new Thickness(0, 0, 0, Fluent.Space4),
            Children = { logo, name, version, runtime },
        };
    }

    /// <summary>".NET 10.0.0 · Windows App SDK 1.8.x": what the build actually runs on.</summary>
    private static string RuntimeLine()
    {
        var runtime = $".NET {Environment.Version.ToString(3)}";

        try
        {
            var sdk = Microsoft.Windows.ApplicationModel.WindowsAppRuntime.ReleaseInfo.AsString;
            return string.IsNullOrWhiteSpace(sdk) ? runtime : $"{runtime} · Windows App SDK {sdk}";
        }
        catch (Exception ex) when (ex is COMException or TypeLoadException or DllNotFoundException)
        {
            // The release info lives in the runtime DLL; a build that cannot reach it still has a
            // .NET version worth showing.
            Log.Warn(Scope, "could not read the Windows App SDK release", ex);
            return runtime;
        }
    }

    /// <summary>The studio's mark, drawn from SVG at the size the row shows it, so it stays sharp at every scale.</summary>
    private static Image CompanyMark()
    {
        const int size = 20;

        var image = new Image
        {
            Width = size,
            Height = size,
            Source = new SvgImageSource(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "lometics.svg")))
            {
                RasterizePixelWidth = 64,
                RasterizePixelHeight = 64,
            },
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(image, Str.Shell_About_LometicsLogo.Text());
        return image;
    }

    // -----------------------------------------------------------------------------------------
    // The settings file
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The window the pickers belong to. <c>Microsoft.Windows.Storage.Pickers</c> takes a window id
    /// rather than an HWND threaded through an initialiser, and works unpackaged.
    /// </summary>
    private static WindowId? Owner(FrameworkElement source)
        => source.XamlRoot?.ContentIslandEnvironment?.AppWindowId;

    /// <summary>[S-494] Save the whole state to a file the user picks.</summary>
    private static async Task ExportAsync(AppHost host, Button source)
    {
        if (Owner(source) is not { } owner)
        {
            Log.Warn(Scope, "no window to own the export picker; nothing was exported");
            return;
        }

        source.IsEnabled = false;
        try
        {
            var picker = new FileSavePicker(owner)
            {
                SuggestedFileName = Str.Shell_About_ExportFileName.Format(("date", DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))),
                DefaultFileExtension = ".json",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };

            picker.FileTypeChoices.Add(Str.Shell_About_FileType.Text(), [".json"]);

            if (await picker.PickSaveFileAsync() is not { } picked)
            {
                return; // ignore: the user cancelled the picker
            }

            if (SettingsTransfer.WriteFile(host.State, picked.Path) is { } problem)
            {
                await TellAsync(source, Str.Shell_About_ExportFailed.Text(), problem).ConfigureAwait(true);
                return;
            }

            Log.Info(Scope, $"exported settings to {picked.Path}");
        }
        catch (Exception ex)
        {
            // An escaping exception here would be lost on a discarded task. COMException is the
            // usual one: the picker could not be shown.
            Log.Error(Scope, "export failed", ex);
        }
        finally
        {
            source.IsEnabled = true;
        }
    }

    /// <summary>
    /// [S-495..S-497] Pick a file, read it through the store's own validation, show what it would
    /// change, and only on a yes keep the current settings beside the state file and apply it.
    /// </summary>
    private static async Task ImportAsync(AppHost host, Button source)
    {
        if (Owner(source) is not { } owner)
        {
            Log.Warn(Scope, "no window to own the import picker; nothing was imported");
            return;
        }

        source.IsEnabled = false;
        try
        {
            var picker = new FileOpenPicker(owner) { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json");

            if (await picker.PickSingleFileAsync() is not { } picked)
            {
                return; // ignore: the user cancelled the picker
            }

            var read = SettingsTransfer.ReadFile(picked.Path);
            if (read.State is not { } incoming)
            {
                await TellAsync(source, Str.Shell_About_ImportFailed.Text(), read.Problem ?? Str.Shell_Transfer_Unreadable.Text())
                    .ConfigureAwait(true);
                return;
            }

            var displays = host.Displays.Select(d => d.Id).ToList();
            var preview = SettingsTransfer.Compare(host.State, incoming, read.Repairs, displays);
            var backupName = Path.GetFileName(SettingsTransfer.BackupPath(host.StatePath, DateTime.Now));

            if (!await AskAsync(source, preview, Path.GetFileName(host.StatePath), backupName).ConfigureAwait(true))
            {
                return;
            }

            if (host.ImportState(incoming) is { } problem)
            {
                await TellAsync(source, Str.Shell_About_ImportFailed.Text(), problem).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "import failed", ex);
        }
        finally
        {
            source.IsEnabled = true;
        }
    }

    private static async Task<bool> AskAsync(FrameworkElement source, ImportPreview preview, string stateName, string backupName)
    {
        if (source.XamlRoot is not { } root)
        {
            Log.Warn(Scope, "no XamlRoot for the import dialog; nothing was imported");
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            RequestedTheme = source.ActualTheme,
            Title = Str.Shell_About_ImportConfirm.Text(),
            Content = DescribeImport(preview, stateName, backupName),
            PrimaryButtonText = Str.Common_Import.Text(),
            CloseButtonText = Str.Common_Cancel.Text(),
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static async Task TellAsync(FrameworkElement source, string title, string message)
    {
        if (source.XamlRoot is not { } root)
        {
            Log.Warn(Scope, $"{title}: {message}");
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            RequestedTheme = source.ActualTheme,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Str.Common_Ok.Text(),
        };

        await dialog.ShowAsync();
    }

    /// <summary>[S-496] The import dialog's body: what changes, then what is good to know.</summary>
    private static ScrollViewer DescribeImport(ImportPreview preview, string stateName, string backupName)
    {
        var body = new StackPanel { Spacing = Fluent.Space3, MaxWidth = 480 };

        void Section(string title, IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            var section = new StackPanel { Spacing = Fluent.Space1 / 2 };
            var heading = Fluent.Text(FluentType.BodyStrong, "TextFillColorPrimaryBrush");
            heading.Text = title;
            section.Children.Add(heading);

            foreach (var line in lines.Take(PreviewLines))
            {
                section.Children.Add(new TextBlock { Text = "• " + line, TextWrapping = TextWrapping.Wrap });
            }

            if (lines.Count > PreviewLines)
            {
                var more = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
                more.Text = Str.Shell_About_AndMore.Plural(lines.Count - PreviewLines);
                section.Children.Add(more);
            }

            body.Children.Add(section);
        }

        Section(Str.Shell_About_WhatChanges.Text(), preview.Changes.Count > 0 ? preview.Changes : [Str.Shell_About_NoChanges.Text()]);
        Section(Str.Shell_About_GoodToKnow.Text(), [.. preview.Notes, Str.Shell_About_KeptAs.Format(("file", stateName), ("backup", backupName))]);

        return new ScrollViewer
        {
            Content = body,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    /// <summary>Open the state file in whatever edits JSON here; its folder when it has not been written yet.</summary>
    private static void OpenConfigFile(AppHost host)
    {
        var path = host.StatePath;
        var target = File.Exists(path) ? path : Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not open {target}", ex);
        }
    }
}

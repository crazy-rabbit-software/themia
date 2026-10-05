# Microsoft Store release

Themia ships through the Microsoft Store only: the Store installs it, updates it and sells the Pro
add-on. Every other build (F5, `dotnet build`, `scripts/publish.ps1`) is unpackaged, runs as a
**Developer build** and is unrestricted. Spec: `SPEC.md`, section *Microsoft Store package and
licensing* ([S-1060]..[S-1071]).

## One-time setup in Partner Center

1. **Reserve the name** "Themia" under the Lometics GmbH developer account.
2. **Product identity** (Product management › Product identity): copy the three values into
   `src/Themia.App/Package.appxmanifest`, replacing the placeholders:
   - `Package/Identity/@Name` ← *Package/Identity/Name* (placeholder `LometicsGmbH.Themia.RESERVE`)
   - `Package/Identity/@Publisher` ← *Package/Identity/Publisher* (placeholder `CN=00000000-0000-0000-0000-000000000000`)
   - `Package/Properties/PublisherDisplayName` must equal *Package/Properties/PublisherDisplayName* (already `Lometics GmbH`)
3. **Pro add-on** (Add-ons › Create a new add-on): type **Durable**, product lifetime **Forever**,
   product id e.g. `themia-pro`, price (the original sold Pro for USD 19, one time). After it is
   created, copy its **Store ID** (12 characters, `9…`) into `StoreIds.ProAddOn` in
   `src/Themia.App/Services/Licensing/StoreIds.cs`. Until then the Upgrade button says Pro is not on
   sale, and nothing can unlock Pro.
4. **Restricted capability `runFullTrust`**: say in the submission notes that Themia is a Win32
   desktop app (desktop windows, input hooks, audio and Wi-Fi APIs). `radios` and `bluetooth` are
   for the Bluetooth widget.
5. The Store listing, age rating, a privacy policy URL (required, since Themia goes online) and screenshots.

## Building a submission

```powershell
# 1. Set the version: Package.appxmanifest Identity/@Version, Major.Minor.Build.0
# 2. Build (Native AOT, unsigned):
.\scripts\package.ps1
#    -> artifacts\store\Themia_<version>_x64.msixupload  (upload this)
#    -> artifacts\store\Themia_<version>_x64.msix        (the same package)
```

The package is **not signed**. The Store signs it on ingestion with its own certificate for the
reserved publisher. No certificate is created, imported or referenced anywhere in this repository.
The script warns while the identity placeholders are still in the manifest. Partner Center refuses
such an upload.

## Local sideload test (manual)

Windows only installs signed packages, so a local test needs a self-signed certificate whose subject
equals the manifest's `Publisher`. This is a deliberate manual step for the developer. The scripts
never do it, because it means trusting a certificate on the machine.

1. In an elevated PowerShell, create a test certificate whose subject is the manifest's publisher
   (for example `CN=00000000-0000-0000-0000-000000000000` while the placeholder is in place), export
   it as `.pfx`, and import the public part into *Local Machine › Trusted People*.
2. Sign a **copy** of the `.msix` with it: `signtool sign /fd SHA256 /f test.pfx /p <pwd> Themia_<v>_x64.msix`.
   Never upload a self-signed package.
3. Install it by double-clicking it, or with `Add-AppxPackage .\Themia_<v>_x64.msix`. Quit any
   unpackaged Themia first.
4. Check:
   - Start menu tile and taskbar icon use the Themia logo; About shows the manifest version.
   - Settings › General › Start with Windows toggles the `ThemiaStartup` task (also visible in
     Settings › Apps › Startup), and a sign-in starts Themia.
   - Settings › License says **Themia Free** (a sideloaded package has no Store licence), shows the
     usage of 10 widgets and 2 views, and the 11th widget (place, paste, duplicate)
     or the 3rd view is refused and opens the License page. Nothing already there is removed.
   - The Bluetooth widget lists devices (radios/bluetooth capabilities).
   - Volume/Mixer device picks switch the Windows default output.
   - Errors still reach Event Viewer › Application (source *Themia*, with the "description cannot
     be found" preamble: the package registers no event message file).
   - About › Uninstall: the dialog has no "keep data" choice. Themia closes, the package is removed,
     and `%LOCALAPPDATA%\Packages\<family name>` is gone.
5. Remove the package (`Get-AppxPackage *Themia* | Remove-AppxPackage`) and delete the test
   certificate from *Trusted People*.

A real purchase can only be tested once the app is published (it may be hidden): install it from the
Store and buy Pro in the Store's sandbox or with a promo code.

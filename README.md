<p align="center">
  <strong>-------></strong>
  <a href="/README.md">Russian</a> |
  <a href="/README.en.md">English</a> |
  <a href="/README.es.md">Spanish</a> |
  <a href="/README.zh.md">Chinese</a> |
  <strong><-------</strong>
</p>

# ZCode Desktop Russian Localization

Unofficial Russian (ru-RU) localization for ZCode Desktop, a GUI client for an
AI coding agent. The official app ships with English and Chinese only; this
project adds a full Russian UI: settings, menus, dialogs, tray, About window
(~4,900 strings).

Works with **any version** of the app: the installer extracts `app.asar`,
locates the needed files by code signatures, and applies patches. Untranslated
strings from newer versions simply show in English — nothing breaks.

## Installation (automatic)

1. Fully quit ZCode (including the tray icon).
2. Download the archive from the [Releases](../../releases) page (file
   `zcode-ru-localization-vX.X.X.zip`), unpack it, and run
   **`РусификаторZCode.exe`**.
3. Pick your ZCode folder → click "Install Russian language".
4. Start ZCode — the UI is now in Russian.

A GUI-free alternative is PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File install-ru.ps1 [-InstallDir "C:\path\to\ZCode"]
```

Requirements: [Node.js](https://nodejs.org) in PATH.

## Rollback

Click "Rollback" in the program, or manually: delete `resources\app` and
rename `resources\app.asar.orig` back to `resources\app.asar`.

## After updating ZCode

An app update restores English. Just run the installer again — it re-applies
the patch to the new version. Strings that are not yet translated show in
English.

## How it works

- Electron loads a `resources/app` directory **instead of** `app.asar` when it
  exists. The installer does not repack the archive; it places the patched
  folder next to it (the original is kept as `app.asar.orig`).
- `patch.mjs` locates the message catalog by its signature
  `g={"zh-CN":…,"en-US":…}` (file names change between versions), injects the
  Russian catalog, adds `ru-RU` to validation schemas, language pickers, and
  the tray/About catalogs.
- Before installing, the patcher syntax-checks the patched files; on any error
  the installation is aborted and the app remains untouched.

## Contributing translations

All strings live in [`ru.json`](ru.json) (key → text). Found an untranslated
string? Add the pair and rerun the installer. Never alter placeholder tokens
(`{version}`, `{count}`).

## Building the installer from source

The exe compiles with the .NET Framework compiler that ships with Windows —
nothing to install. Run `Build.bat` or:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -out:"РусификаторZCode.exe" InstallRu.cs -r:System.Windows.Forms.dll -r:System.Drawing.dll
```

The exe expects `patch.mjs`, `ru.json`, and `node_modules`
(`npm install @electron/asar`) next to it.

## Disclaimer

Unofficial project, not affiliated with the ZCode developers. Only
localization files on your machine are patched. The author is not liable for
any issues — you can always roll back (see above).

## License

[MIT](LICENSE)

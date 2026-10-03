# Item catalog regression checks

Windows / .NET Framework 4.0 or later is required. The harness exercises the compiled UI's actual loading and filtering methods without showing a window or sending game commands.

Build into a **new, empty output directory** and run the executable there:

```powershell
dotnet build tools/ItemCatalogTests -c Release -o <fresh-output-directory>
& <fresh-output-directory>/ItemCatalogTests.exe
```

Coverage: 1,500 matching items (unfiltered and searched), oracle exclusion, multiline CSV fields with blank lines, commas and escaped quotes, searches in continuation lines, empty results, reloads, missing/empty files, and legacy five-column exports.

An optional local game export can be checked without committing game data:

```powershell
& <fresh-output-directory>/ItemCatalogTests.exe <item_ids.csv> <expected-loaded-count> <expected-displayed-count>
```

This additional check is intended for the build 24329824 catalog, including IDs 30121 and 43001–43009. The harness creates a private `BepInEx` fixture beside its executable and refuses to run if that directory already exists; use a fresh output directory for each run.

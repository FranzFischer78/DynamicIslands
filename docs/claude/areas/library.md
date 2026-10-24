# Library

Sharing islands and world plans. Export writes a pack (.zip), Import installs one, and the ISLAND LIBRARY window downloads entries from the online library. Also covers the CustomIslands-Library repo, which holds entries and builds `index.json`.

## Files
| File | Role |
|---|---|
| `LibraryOnline.cs` | `LibraryClient` (`library.txt`, `Address`, `LoadIndex`, `Parse`, `StateOf`, `Picture`, `Download`), `LibraryEntry`, `LibraryFileRef` |
| `LibraryDrive.cs` | `LibraryDrive`: a Google Drive folder library (`IsDrive`, `List`, `Find` path -> file id, `DownloadUrl`) |
| `LibraryWindow.cs` | `LibraryWindow`: the ISLAND LIBRARY window (tabs, search, `ShowDetail`, `OnMain`, `OnRemove`, `OpenSubmit` = Discord steps) |
| `LibraryPack.cs` | `LibraryInfo` (info.json), `LibraryInstalled`/`LibraryInstalledFile` (installed.json), `LibraryPackContents`, `LibraryPack` (`Export`, `ExportWith`, `Read`, `FromFiles`, `Validate`, `Install`, `Remove`, `ChangedFiles`, `WorldsUsing`), `LibrarySource` |
| `LibraryJson.cs` | `LibraryJson`: `Parse`, `Write`, readers `Str`, `Int`, `Bool`, `Strings`, `Objects` |
| `LibraryWindows.cs` | `LibraryExportWindow` (Export, Share -> `SubmitUrl`), `LibraryImportWindow` (Import, installed list, Tidy up), `WindowKeys` |

## Main flow
1. `LibraryWindow.Open(tab, newGame)`: from the main menu (`DynamicIslands.cs`), "Get more..." in the New Game box (`Open(0, true)`), "Library..." in `IslandFilesWindow` (`Open(1)`). Refused inside a world.
2. `LibraryWindow.Load` -> `LibraryClient.LoadIndex`: error when `Online` is false; cached `CacheMinutes`. Drive `Address`: `LibraryDrive.Forget`, then `LibraryDrive.Find(root, "index.json")`. Web address: `index.json?t=<ticks>`.
3. `LibraryClient.Parse` refuses `format` > `LibraryClient.Format`. Files come from `download` only when both it and the address are http; else from the address. Entries without id or path, or with `..` in the path, are skipped.
4. Rows show `LibraryClient.StateOf` (`NotInstalled`, `Installed`, `Update`; `NotInstalled` also when an installed file is missing) and `LibraryClient.Picture` (cached).
5. `LibraryWindow.OnMain`: on an update with `LibraryPack.ChangedFiles`, the first click only warns. It then calls `LibraryClient.Download(e, appearWhileSailing, true, ...)`.
6. `Download` fetches `LibraryEntry.InstallFiles` (islands, plan, map types; not pictures) via `FileUrl`, checks `IsSafeFileName`, size and `LibraryPack.Sha256`. On a mismatch it reloads the list once and retries. Then `LibraryPack.FromFiles` and `LibraryPack.Install(..., LibraryPack.SourceLibrary)`.
7. `Install` -> `InstallFiles`: names (`FreeName` = "Name (Title)", `FreePlanName`), `StoryIdsFor` (T8: a story item id another installed entry uses becomes `<id>-<pack id>`; `Rewritten` -> `StoryItems.RenameIds` renames the definition, `collect` steps and every `story:<id>`), `Rewritten` rules, `KeepForWorlds`, map type files, `SaveInstalled`, then `SetPoolWeight` (island entry with appear-while-sailing: 1, else 0). On any failure `InstallUndo.RollBack` runs and `Install` throws.
8. `LibraryPack.Remove(id)` skips `shared` files; `RemoveFile` keeps files another entry lists or a saved world uses (`WorldsUsing`), and moves the rest to `deleted\library` (`PiecesFiles.MoveToDeleted`).

Export: `LibraryExportWindow.OpenIsland`/`OpenPlan` -> `LibraryPack.LastInfo` (from `exports.json`: same id, version + 1) -> `LibraryPack.Export` -> `ExportWith` -> `exports\<id>.zip`. Import: `LibraryImportWindow.Pick` -> `LibraryPack.Read` -> `LibraryImportWindow.Install` -> `LibraryPack.Install(..., SourceImport)`.

## Data and keys
- `library.txt` in `DynamicIslands.assetpath`, written with defaults when missing: `online = on|off`, `address =` (Drive folder link, `gdrive:<id>`, or web folder), `key =` (Google API key; without it the public folder listing is read). Empty `address` or `OldDefaultAddress` means `DefaultAddress` (a Drive folder).
- `index.json`: `format`, `library`, `download`, `commit`, `entries[]`. Entry: `id`, `path`, every info.json field, `islands`, `size`, `updated`, `files[]` (`name`, `size`, `sha256`; info.json not listed).
- `info.json` (`LibraryInfo.ToJson`/`From`): `id`, `kind` (`island`/`plan`), `title`, `author`, `version`, `summary`, `description`, `tags` (the tag `quest`, any case, makes an island's library page say "Quest: yes"; without it "No quest - an island to explore"), `players`, `length`, `icon`, `pictures`, `plan` (plans), `remix`, `featured`, `minModVersion`, `created`, `basedOn`. No game settings.
- `library\installed.json`: `entries[]` (`id`, `source`, `title`, `author`, `kind`, `version`, `remix`, `date`, `plan`, `storyIds[]` (`from`, `to`: the T8 renames, kept by updates), `files[]` with `name`, `original`, `kind` (`island`/`plan`/`maptype`), `sha256`, `shared`). `LibraryInstalled.Source` = `library:<id>@<version>` or `import:<id>@<version>`.
- Folders: `library\` (`cache\<id>-<sha12>.img`), `exports\` (zips, `exports.json`), `import\`.
- A pack is one `<id>/` folder: `info.json`, `icon.jpg`, `picture1.jpg`..`picture4.jpg`, `.island` files, the `.plan`, map type files. Limits: `MaxFiles`, `MaxTotalBytes`, `MaxIslandBytes`, `AllowedExtensions`.

## Library repo
- `index.json` (built, never hand-edited); `islands/<id>/` (info.json, icon, pictures, one `.island`); `plans/<id>/` (same, plus the `.plan` and every island it names); `tools/build-index.ps1`; `.github/workflows/build-index.yml`, `.github/ISSUE_TEMPLATE/submit.yml`. A Drive library uses the same layout.
- `build-index.ps1 [-commit <sha>] [-check] [-root <folder>] [-drive]` leaves an entry out when: folder name not `^[a-z0-9][a-z0-9-]*$`; no/invalid info.json; missing `kind`, `title`, `author`, `version`, `summary`, `icon` (plans: `plan`); `kind` not matching the folder; a named file missing; icon > 200 KB, picture > 500 KB, entry > 50 MB; no `.island`, a `.island` not starting `CISL`, or an island entry with more than one; a plan's `island:`/`oneof:` island missing; a file name with `# % ? : * " < > |`. Exit 1 when any was left out.
- An island entry also gets `monsters` (the function `Monsters` reads the .island's objects and props; counted as `IslandDifficulty.MonstersOf` - keep both alike): `LibraryEntry.Monsters` (-1 when missing) gives the row's "Difficulty:" label in `LibraryWindow.AddRow`. The live index has it only after build-index runs again.
- Sort: featured, then `updated` (git date, else `created`) newest first, then title. `download` = raw GitHub at the commit (or `main`); empty with `-drive`. `format` is written as 1.
- Workflow: push to main touching `islands/**`, `plans/**`, the script or the workflow runs `-commit $GITHUB_SHA`, commits `index.json`, fails the run if entries were left out; pull requests (islands/plans) run `-check`; also `workflow_dispatch`.

## Where to change X
| Task | Edit |
|---|---|
| A new info.json field | `LibraryInfo` (field, `ToJson`, `From`) and the repo README's info.json table |
| Download checks and retry | `LibraryClient.Download` |
| Reading the list | `LibraryClient.Parse` |
| Drive access | `LibraryDrive.List`, `Find` |
| Pack checks | `LibraryPack.Validate` (Import and download); `Read` (zip rules) |
| Install naming and keep rules | `LibraryPack.InstallFiles`, `FreeName`, `KeepForWorlds` |
| Removal | `LibraryPack.RemoveFile` |
| Window buttons and texts | `LibraryWindow.ShowDetail`, `OnMain`, `OnRemove` |

## Traps
- A `format` above `LibraryClient.Format` makes older clients refuse the whole list. Raise the script's `format` only together with `Format`.
- Web library: files come from the commit in `download`, so list and files match. Drive: `download` is ignored, files are found by path (case-insensitive, last duplicate wins) at download time; build that list with `-drive`.
- The script copies info.json fields over `id` and `path`, so an info.json `id` replaces the folder name as the entry id.
- The script needs a `.island` in every entry, but `LibraryPack.Validate` accepts a plan with none (it may bring only map-type islands). The script leaves such a plan out.
- Unity's `JsonUtility` writes `{}` for runtime-compiled classes: use `LibraryJson` (`MaxDepth` limits nesting).
- Installs never overwrite the player's own files (clash -> new name or kept). `OnMain` always passes `replaceChanged` = true; its warning click is the only guard. Import has its own toggle. Remove never deletes for good.
- Paths from index data go through `LibraryPack.IdFrom` or `IsSafeFileName`.
- The library goes online only while its window is open and sends only download requests.
- `LibraryWindow.ShowPicture` clears the picture before loading (a slow download left the entry before's picture on every entry clicked after it; CILibraryBrowse checks it).
- C# 7.3 only.
- Docs: README Features bullets on packs and the island library, the files table, "Source overview"; `docs/GUIDE.md` section 4.7 and chapter 10; the library repo README when layout or fields change.

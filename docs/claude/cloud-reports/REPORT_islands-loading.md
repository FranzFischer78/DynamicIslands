# Report: "+ Add a saved island" fast, with a loading sign (branch cloud/islands-loading)

Based on origin/content 09161a8. **Not compiled or run** (no compiler or Raft here): run `compile.ps1` and
`compile.ps1 -Release`, then `CIChoiceTiming` and the tests listed below before merging.

## Cause (confirmed)
`ChoiceWindow.Islands()` asked `IslandCache.Props(n)` for every saved island, and each one was a full
`IslandFile.Load` (heightmap, objects, paint), all on the main thread before the window appeared.
`WorldPlanWindow.QuestNote` (the "- quest, 3 steps" in the rule pickers) did the same through `IslandCache.QuestOf`.

## What changed
| Where | What |
|---|---|
| `DynamicIslands/IslandSummaries.cs` (new) | The summary cache. `Mods\DynamicIslands\summarycache.txt`: a header line, then one line per island: name, size, last-write time (UTC ticks), title, author (base64), quest step count. A file is read again only when its size or time changed. `Of`/`TitleOf`/`QuestStepsOf` (line 63+) answer from memory, comparing size and time at most every 2 s, and read the file there only if it changed. `Prepare` (l. 95) starts a look: one `Task.Run` worker (`Work`, l. 120) compares the files and reads only the changed ones with `IslandFile.LoadOffThread`. It makes no Unity calls and doesn't touch the cache, only the job's own arrays. `Finish` (l. 148) takes the results in on the main thread, either from a coroutine on `DynamicIslands.instance` or when the window asks. A missing or broken cache file, a wrong header or a bad line means it is made again (`Load`, l. 206). An unreadable island is logged once per change of the file, is not written to the cache, and is read again once it changes. |
| `DynamicIslands/ChoiceWindow.cs:115` | `OpenIslands(title, Func<choices>, pick, empty, Button from)`: the window opens at once with "Loading islands... (n of N)" (updated each frame in `Update`, l. 179), the search field off, and `from` greyed. Once the look is done, `EndLoading` builds `choices()` and fills the list. While it loads, a second call does nothing. `Close`/`Open`/Esc leave the loading state and un-grey the button (`StopLoading`). The look itself goes on and is kept for the next list. `Islands()` (l. 251) now takes titles from `IslandSummaries.TitleOf`. The test accessors `Values`/`Shown`/`EmptyText`/`PickValue`/`DeleteValue` finish a list still loading first (`Settle`), so existing tests that click and read the list at once keep working. New test accessors: `Loading`, `LoadingText`. |
| `DynamicIslands/MainStoryHelper.cs:89-92` | "+ Add a saved island" uses `OpenIslands` and passes itself to be greyed. |
| `DynamicIslands/WorldPlanWindow.cs:540` | The "▾" island picker of a rule's "what" (island / one of) uses `OpenIslands`. |
| `DynamicIslands/WorldPlanWindow.cs:785-791` (`Pick`) | The waits-for and near-which-island pickers (rules plus saved islands with their quest notes) use `OpenIslands` and grey their ▾ button. Note: `Pick` is also used for lists without islands (zones, signals, steps, rules). These now show the loading line for a frame or so too, which is harmless and fast once cached. |
| `DynamicIslands/WorldPlanWindow.cs:818` (`QuestNote`) | The quest step count comes from the summary cache. |
| `DynamicIslands/QuestEditorWindow.cs:406` | "Bring which island" uses `OpenIslands`. |
| `DynamicIslands/WorldDirector.cs:646` (`IslandCache.ForgetFile`, called by every `IslandFile.Save`) | Also `IslandSummaries.Forget(name)`, so a saved island is read again. `IslandCache.Forget()` (l. 512, after pack installs, tidy-ups, deletes in My islands) calls `IslandSummaries.Recheck()`, so every size and time is compared again. |
| `DynamicIslands/IslandRename.cs:230` (`MoveFile`) | Forgets both names: a renamed file keeps its size and time, so it has to be forgotten explicitly. |
| `DynamicIslands/IslandFilesWindow.cs:305` (`MoveToDeleted`, used by the ISLANDS window and My islands) | Forgets the deleted island. |
| `DynamicIslands/DevTestsChoiceTiming.cs` (new), `CIChoiceTiming` | See "Test" below. |
| `DynamicIslands/DynamicIslands.csproj` | `IslandSummaries.cs` and `DevTestsChoiceTiming.cs` added. |

### The other lists (task point 3)
I looked at each one. None reads every island's file, so nothing there needed the cache:
- **Templates...** lists `WorldPlanTemplates` (built-in rule sets) and opens no island files.
- **Import...** (`LibraryImportWindow`) lists the packs' contents, not the saved islands' files.
- **My islands...** (`MyIslandsWindow.Refresh`) uses names, the spawn pool's weights and the worlds' lists, not the island files.
- **ISLANDS window** (`IslandFilesWindow.Refresh`) uses only `FileInfo` (date and size) per row.

The lists that did read every file all go through `ChoiceWindow.Islands()` or `QuestNote`: the main story helper, the rule pickers and the quest editor's "Bring which island". All of them are changed.
`PlanPickerWindow.IslandMap` still loads one whole island, for one plan's picture. That is a single file, not a list, so I left it.

## Test: `CIChoiceTiming` (Dev, editor)
It makes 50 islands `citiming-00..49`, each with a 513 x 513 heightmap (a real island's size) and a title. Then:
1. **Before:** it times the old way, `IslandCache.Props` of every saved island, and logs it.
2. **New islands:** World plans > New main story > it clicks + Add a saved island and checks that:
   - the window opens within 250 ms, saying "Loading islands... (n of N)"
   - the button is greyed, and a second click does nothing
   - afterwards the button is back, and all 50 islands are listed with their titles
3. **From the cache:** it unloads the cache from memory (as in a new session) and opens the list again. No island file may be read, it must be faster than the old way, and summarycache.txt must have the 50 lines.
4. **Saved, renamed, deleted:** it saves one again with a new title, renames one (`IslandRename.Rename`) and deletes one (`MoveToDeleted`). The new title must show, the new name must be listed, the deleted one must be gone, and exactly 2 files may be read.
5. **Broken cache:** it writes garbage into summarycache.txt. The list must still have the titles, and the file must be made again.

At the end it removes the citiming islands, including from `deleted\`. Its PASS line holds the before and after milliseconds.

Also run, because they click island lists and read them at once (now through `Settle`): `CIPlanPicks`, `CIPlanHelp`,
`CIQuestBookEditor`, `CIAutosave` (the unsaved-work list, plain `Open`) and `CIButtons`.

## What to test in game
- With many islands (hundreds), in World plans > New main story..., + Add a saved island:
  - the window comes at once with "Loading islands... (n of N)" counting up, and the button is grey
  - the list comes after with titles
  - the second time it is near instant, also after restarting Raft
- Esc while it is loading closes the window and the button works again. Opening another list straight after works.
- Save an island with a new title, rename one, delete one: each list shows the change.
- Delete `Mods\DynamicIslands\summarycache.txt`, or put rubbish in it: the lists still work and the file comes back.
- A broken `.island` file: the list still opens. The island is listed without a title, with one warning in the log, not one every time.

## Unsure
- `Task.Run` plus `IslandFile.LoadOffThread` on a worker follows the existing precedent in `DynamicIslands.SpawnIslandFile`.
  `IslandFile.Read` only logs (thread-safe) and does no other Unity calls, and `StableIds.ReadTail` runs there today as well.
- The worker doesn't call `SafeFile.Recover`. A half-saved island (`.island.tmp` without its `.island`) isn't in the
  saved list anyway, and start-up's `RecoverAll` and `IslandFile.Load` still handle it.
- `IslandSummaries.Of` reads a changed file on the main thread when a list hasn't prepared, such as `FieldChoices` used by
  tests, or a quest note of a rule's island that isn't in the saved list. That is one file, as before.
- Uncompiled spots to watch: the method group `ChoiceWindow.Islands` passed as `Func<IEnumerable<Choice>>`, and `Button b = null;`
  assigned in the same statement that builds the lambda that uses it (C# allows this because it is assigned before the click runs).

## Doc lines to add (not edited here)
- **CHANGELOG** (next version, editor):
  "+ Add a saved island and the other island lists in World plans open at once with a 'Loading islands... (n of N)' line,
  then list your islands. Their titles are kept in `summarycache.txt`, so the next time takes no time (it read every
  island's whole file first)."
- **README** source overview: add the row
  "| `IslandSummaries.cs` | What the island lists show of each saved island (title, author, quest steps), kept in `summarycache.txt` and read again only when a file changes |".
  Also add to the `ChoiceWindow.cs` row's text: "(a list with islands opens at once and loads them: `OpenIslands`)".
- **README** files list / `docs/claude/MAP.md` "Player files" (and `docs/claude/areas/editor.md` Data and keys):
  "`summarycache.txt`: the island lists' titles and quest steps (safe to delete: it is made again)".
- **GUIDE.md** (World plans, New main story): "The list of your islands opens at once. With many islands it says
  'Loading islands...' for a moment the first time."
- `docs/claude/areas/testing.md` command families: add `CIChoiceTiming` under Editor tests.

## Merge notes (2026-10-16)
- Merged into content by the merge agent. Compile fix: DevTestsChoiceTiming.cs lacked `using HMLLibrary; using RaftModLoader;` for `[ConsoleCommand]`.
- Review: the worker (IslandSummaries.Work, IslandFile.LoadOffThread) makes no Unity calls and only fills the job's arrays; Finish takes them in on the main thread. No changes needed.
- Doc lines added to CHANGELOG, README (file table, ChoiceWindow + IslandSummaries rows), GUIDE (New main story), MAP.md, areas/editor.md, areas/testing.md; PDF regenerated.
- Tests (Raft, merged build): CIChoiceTiming PASS (opens in 21 ms, was 11575 ms), CIPlanPicks 18/18, CIPlanHelp 15/15, CIQuestBookEditor 12/12, CIAutosave 16/16.

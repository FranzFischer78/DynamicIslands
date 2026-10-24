# Report: gen-errors (Raft's scene scripts kept quiet, clickable error notes)

Branch `cloud/gen-errors`, from `origin/content` 09161a8. Not compiled or run (no compiler or Raft here): written by
reading the code around it. First thing to do: `compile.ps1` and `compile.ps1 -Release`, then the two tests below.

## What changed

### 1. Raft's scripts waking while the mod loads their scenes (`PlaceableCatalog.cs`)
- `OpenScene` (~802): every scene copy the mod loads itself is now **quiet** from the moment it starts loading
  until it is unloaded (`CloseScene`, ~892, in a `finally`). Before, only scenes loaded switched off (`hide`) were covered,
  and only while they were loading. That missed the **core catalogue's scenes** (`Sources`, `Hide = false`: the big and small
  tropical islands, Temperance/Caravan/Balboa small, Vasagatan). They are loaded switched on, so Raft's scripts also run
  `Start` a frame later.
- `quietScenes` (~824) replaces `hiddenLoads`. It maps each scene name to the handles of Raft's own copies of that scene,
  which existed before the load. Raft's own copy is never kept quiet, even when it has the same name (the old check went by
  name only).
- `Patch` (~849): the finalizer now also goes on `Start` where the script has one. It **logs once at start** whether it
  applied: `Raft's story scripts kept quiet in the scenes the mod loads for objects: CoolingChallenge.Awake, ...`, or a
  warning naming what couldn't be patched.
- `QuietLoadFinalizer` (~869): counts what it swallows (`Quieted`) and logs the first one of each method (as a plain log
  line, not an error).
- Test helper `ReopenForTest` (~918): loads the core scenes as the catalogue does, plus the scenes given objects come from,
  each for a frame, then unloads them.
- I did **not** switch the core loads to "inactive". Its clones, harvestables and terrain textures come from active
  objects, and switching off would change what the generator copies, which can't be checked here.

### 2. The mod's own clickable error note (new `ErrorNotes.cs`)
- `Install` hooks `Application.logMessageReceived` as the first start step (`DynamicIslands.cs:191`). `Uninstall` runs on
  RML's Unload (`DynamicIslands.cs:468`).
- `WhoseFor` (~89) decides what gets noted:
  - Exceptions whose stack has the mod's code (`DynamicIslands.*`), labelled "Custom Islands", or e.g. "Raft (called by
    Custom Islands)" when Raft's code threw under the mod's.
  - The mod's own `[CUSTOM ISLANDS] ... Exception` error lines (e.g. `Guarded`'s "... failed: <exception>").
  - Raft's exceptions while one of the mod's object scenes is loaded, labelled "Raft (a script in <scene>, which Custom
    Islands loaded ...)".
  - "Screen position out of view frustum" is never noted.
  - Raft's own errors at other times, and other mods' errors that don't go through the mod, aren't noted: RML's toast covers
    those.
- Whose code it is, from the stack: frames from Unity, .NET and Harmony are skipped. `DynamicIslands.` frames are the mod's;
  `RaftModLoader.`/`HMLLibrary.` frames are RML's. A type with no namespace counts as Raft's; any other namespace counts as
  "another mod (<namespace>)".
- The note: a small bar at the bottom left on its own canvas (order 885, under the info box), reading "Custom Islands: an
  error happened - click for details", with "(n)" when there are several. It shows the frame after the error (never from
  inside Unity's log call) and hides after 30 s.
  - Its "x" button, or opening the details box, mutes the note for errors already seen. A new error shows it again, so an
    error that repeats every frame doesn't keep bringing it back.
- Clicking opens `InfoWindow` (`OpenDetails`, ~261). The box has whose code it is, the time and how many times it happened,
  the versions, the message, the first 8 stack lines, and where to report (GitHub issues and Discord, from `HelpLinks`).
  - It also names the path to errors.txt.
  - Buttons: **Copy** (versions, time, whose code, message, 40 stack lines to the clipboard), **Open the log folder**
    (`HelpLinks.OpenLogFolder`), **Report on GitHub** (`HelpLinks.NewIssueUrl`), **Post on Discord**, **Close**.
- `Mods\DynamicIslands\errors.txt` (`Append`, ~208): `=== <time> | Custom Islands <v>, Raft <v> | <whose>`, then the
  message, the stack and a blank line.
  - The same error (same message and first stack line) is written at most 3 times per session.
  - Past 256 KB the file is cut to its newer half, starting at an entry.
- `DynamicIslands.csproj`: `<Compile Include>` lines for `ErrorNotes.cs` and `DevTestsErrors.cs`.

### Tests (new `DevTestsErrors.cs`)
- `CIGenBuildingsQuiet` (anywhere):
  - Logs whether the quiet patch applied, and checks that it did.
  - Loads every core scene and every scene the cases need again, as the mod does (`ReopenForTest`).
  - Generates with `CreateFile`: Buildings on (Mixed, 8) and Caves on, across 5 styles, radius 60/110/160, Levels off and
    on, a 25-landmark case, and one case per story remake (`Remakes.All`, which pulls in Pilot, the boat, Temperance,
    Caravan, Varuna, Utopia...).
  - Counts exceptions and errors in the style of the `DevTestsContent.cs` counter (frustum message and `[CITEST]` excluded);
    it must be 0. Also logs how many times the scripts were kept quiet.
- `CIErrorNote` (anywhere):
  - Throws a real `InvalidOperationException` in the mod's code and hands it to the note as Unity does.
    `CIErrorNote log` sends it through `Debug.LogException` instead, which tests the whole path but leaves an exception line
    in Player.log.
  - Checks the record (whose code: Custom Islands), that the note shows with its text, the box text (message, whose code,
    stack, Discord and Issues links), the buttons, Copy's clipboard text, and that Open the log folder goes to `LogFolder`
    (in `HelpLinks.TestMode`).
  - Checks the errors.txt entry (time, versions, whose code) and that the file stays under the size cap.
  - Checks that the frustum message and a Raft-only exception outside the mod aren't noted, and the whose-code labels for
    "another mod" and "Raft (called by Custom Islands)".

## To test in game
1. `ci.ps1 -Command "CIGenBuildingsQuiet"`: PASS. Player.log should have the "kept quiet" start line naming
   CoolingChallenge and OlofFinalRoom.
2. `ci.ps1 -Command "CIErrorNote"`, then `CIErrorNote log`: PASS. Then look at the note (bottom left), click it, and try
   Copy, Open the log folder and the "x".
3. By hand: Generate in the editor with Buildings and Caves on (snowy, then a Utopia/Temperance remake). RML's "AN ERROR
   OCCURED!" should no longer appear. If a Raft script still throws, the mod's note says which one, and errors.txt has it.
4. Check the note is clickable in the main menu, the editor and a world, and that the info box opens over it.

## Unsure
- **Scripts other than CoolingChallenge and OlofFinalRoom.** The finalizer only covers those two. If the core or story
  scenes have other scripts that throw in Awake/OnEnable/Start, `CIGenBuildingsQuiet` will name them in its first-problems
  list. Add their names to `PlaceableCatalog.QuietMethods`.
- **`__originalMethod` injection** in a Harmony finalizer: supported in Harmony 2. If RML's Harmony refuses it, remove the
  parameter (it is only used for the log line).
- **Raft's scene names:** `Scene.name` is assumed to equal the name the mod loads it by (the old code assumed the same).
- **Errors on other threads** aren't noted: `logMessageReceived` is main thread only.
- **"Raft code while the mod is doing something"** is detected only while the mod has object scenes loaded. Other Raft
  errors are noted only when the mod's code is on their stack.
- Whose code it is goes by namespace: a mod without a namespace would be reported as Raft.

## Doc lines to add (not edited here: other branches change these files)
- README, Source overview: `| ErrorNotes.cs | The mod's own error note: an exception from the mod's code (or from Raft's
  while it loads Raft's scenes) shows a clickable note; its box says whose code it was, with Copy, the log folder and where
  to report; errors.txt keeps them |`
- README, Source overview, PlaceableCatalog.cs line: add "; Raft's story scripts are kept quiet in the scenes it loads for
  objects (`QuietMethods`)".
- README, Console commands (dev): `CIGenBuildingsQuiet`, `CIErrorNote [log]`.
- GUIDE, section 15 (Reporting a problem): "When something goes wrong in Custom Islands, a note **Custom Islands: an error
  happened - click for details** appears at the bottom left. Click it to see what happened and whose code it was; **Copy**
  puts it on the clipboard for a GitHub issue or a Discord post, and **Open the log folder** finds Player.log. Every such
  error is also kept in `Mods\DynamicIslands\errors.txt`."
- CHANGELOG, Fixed: "Raft's story scripts no longer throw while the generator loads Raft's islands for buildings and caves
  (also the core catalogue's scenes), so RML's 'AN ERROR OCCURED!' no longer shows then." CHANGELOG, New: "A clickable error
  note of the mod's own, with the details, Copy, the log folder and where to report; errors.txt."
- `docs/claude/areas/testing.md`, test table: `CIGenBuildingsQuiet`, `CIErrorNote` in `DevTestsErrors.cs`.

## Merge notes (2026-10-16)
- Merged into content by the merge agent. Compile fix: DevTestsErrors.cs lacked `using HMLLibrary; using RaftModLoader;`.
- Review: OnLog runs on the main thread (logMessageReceived), guards against re-entry and never throws; the note shows a frame later. No code changes needed.
- Doc lines added (CHANGELOG New + Fixed, README source rows + errors.txt file row, GUIDE 15, MAP.md, testing.md); README has no dev-command list, so the CI commands went to testing.md only. PDF regenerated.
- Tests (Raft, merged build): CIGenBuildingsQuiet 3/3 (0 errors over 24 islands), CIErrorNote 10/10, CIErrorNote log 10/10, CIGenTest 7/7.

# Testing (dev builds)

Automated tests and content tools, run as `CI*` console commands. Players never see them: release builds leave them out.

## Files

- `DevTests.cs` -> `Log`, `Fail`, `Check`, `Init`, the command file (`PollCommandFile`, `RunCommand`), editor and world drivers.
- `DevTests*.cs` (113 files) -> one `public static partial class DevTests`; each file adds one area's commands.
- `DevTestsRecipe.cs` -> `CIRecipe` and recipe-writing helpers (see `RECIPE_FORMAT.md`).
- `DevTestsT8cb.cs` -> `CIT8Items` (story take-out), `CICB1Recipe` (decorative ready pieces), `CITrust` (two players: host trust, a player's take-out). CI world commands set `PlayerTrust.ForTests`; `CITrust setup` clears it, `CITrust on` sets it again.
- `DevTestsPlay.cs` -> `CIPlay` play tests (see `PLAY_FORMAT.md`).
- `DevTestsScenarios.cs` -> `Sc*` helpers to bring, use and remove islands in a world; many `CISc*` scenario tests.
- `DevTestsEditorStart.cs` -> the start window, plan on the main menu, Use in a plan..., Edit island, plan tabs (`CICreateWindow`, `CIPlanFromMenu`, `CIUseInPlan`, `CIEditIslandFromMenu`, `CIPlanTabs`, `CIStoryNewIsland`).
- `DevTestsMultiplayer.cs`, `DevTestsMultiplayerFull.cs` -> two-player helpers (`PutPlayerNear`, `OnRaftCommand`, `AnimalsOf`).
- `DynamicIslands.cs` -> calls `DevTests.Init` through reflection at start-up, if the type exists.
- `compile.ps1`, `pack.ps1` -> `-Release` leaves `DevTests*.cs` out.

## Main flow

1. Start-up in `DynamicIslands.cs` finds `DynamicIslands.DevTests.Init` by reflection and calls it.
2. `DevTests.Init` logs `[CITEST] Command file: <path>`, sets `Application.runInBackground`, starts `PollCommandFile`.
3. `DevTests.PollCommandFile` checks `CommandFile` every 0.2 s (real time; a file written in the last 0.1 s waits a look). It reads all lines, deletes the file, and runs
   each non-empty line not starting with `#`, logging `[CITEST] > <line>`.
4. `DevTests.RunCommand` finds the method with a `ConsoleCommand` attribute of that name (any case) in the mod's assembly
   and calls it with the rest of the line split at spaces (no quoting). Any of the mod's console commands works.
5. The command usually starts a coroutine (`DynamicIslands.instance.StartCoroutine`), logs with `Log`/`Check`, and ends
   with `Log("PASS: ...")` or `Fail(...)`.

## Data and keys

- `Mods\DynamicIslands\dev_commands.txt`: commands for this Raft. `dev_commands_2.txt`: for a second Raft running in
  Sandboxie (`DevTests.Sandboxed`: `SbieDll.dll` is loaded); the box must open that file to the real folder.
- Log lines: `[CITEST] <text>` (`Log`), `[CITEST] PASS: <what>` / `[CITEST] FAIL: <what>` (`Check`, Debug.Log),
  `[CITEST] FAIL: <msg>` (`Fail`, Debug.LogError). They show in the F10 console and in Raft's `Player.log`.
- Command file errors: `[CITEST] FAIL: no console command called <x>`, `[CITEST] FAIL: command '<line>': <exception>`.
- `Mods\DynamicIslands\recipes\` (`DevTests.RecipeFolder`): `.recipe` and `.play` files, and outputs `view_*.jpg`,
  `play_*.jpg`, `objects_<category>.txt`, `questicons.txt`, `sounds.txt`. `CIShot` writes `shot_<name>.png` and `CIItems`
  writes `items.txt` in `Mods\DynamicIslands\`.
- Repo `content\recipes\` and `content\tests\` hold the sources; `content\objects\` holds object and item lists. No mod
  code or repo script copies them; both kinds go into the one `recipes\` folder.
- Test worlds: name starts with `CI ` (`SaveAndLoad.CurrentGameFileName`). Test island: `citest` (`DevTests.TestIsland`);
  scenario tests save islands named `cisc...` and delete them with `ScRemove`.

## Command families

| Family | Examples | Where |
|---|---|---|
| Drivers | `CIEditor`, `CITab`, `CIClick`, `CINewWorld`, `CILoadWorld`, `CISave`, `CIShot`, `CIBackground`, `CIMute`, `CIQuit`, `CIAlive`, `CIScene` | `DevTests.cs`, `DevTestsMultiplayer.cs`, `DevTestsRecipe.cs` |
| Editor tests | `CITest`, `CIUndo`, `CIGenTest`, `CIButtons`, `CIButtonsInside` (also the Camera window, "camera"), `CICameraSettings` (the settings, and the controls list: every row, one line each, fits 1280x720), `CIIslandFilesHelp`, `CIChoiceTiming` (`DevTestsChoiceTiming.cs`), `CITestButton` (`DevTestsPolish.cs`), `CIGenBuildingsQuiet`, `CIErrorNote` (`DevTestsErrors.cs`), `CIQuestTest`, `CITerrainBrushes`, `CIGenSweep` (`quick` / `<chunk>` / `all` / `list`: Generate island through its window over ~370 fixed cases), `CIGenCase <n>` | `DevTests.cs`, `DevTestsAll.cs`, `DevTestsUiFit.cs`, `DevTestsContent.cs`, `DevTestsTerrainBrushes.cs`, `DevTestsGenSweep.cs` |
| World tests (host) | `CITestWorld`, `CIQuestWorld`, `CILootWorld`, `CIZoneWorld`, `CIBehaviourWorld` | `DevTests.cs`, `DevTestsContent.cs`, `DevTestsBehaviour.cs` |
| `...Unit` (rules, mostly anywhere), `...Check` (state, mostly in game) | `CIMonsterUnit`, `CILibraryUnit`, `CISafetyUnit`, `CIRulesCheck`, `CIPlanCheck`, `CIStoryChainCheck` | per area file |
| Scenarios | `CIScRam`, `CIScDeath`, `CIScCatch` | `DevTestsScenario*.cs` and others |
| Two players | `CIMPFull`, `CIMPState`, `CIJoinHost`, `CIGoto`, `CIUse`, `CIOpenChest`, `CIReadNote` | `DevTestsMultiplayer*.cs`, `DevTestsUI.cs` |
| Library content | `CIRecipe`, `CIPlay`, `CIView`, `CIViewAt`, `CIFloating`, `CIFloatingSweep`, `CIRemoveBuried`, `CISettle`, `CIFeatureCheck` | `DevTestsRecipe.cs`, `DevTestsPlay.cs`, `DevTestsFeatureCheck.cs` |
| On the ground, in a world (`tools\groundall.ps1`) | `CIGroundWorld`, `CIGroundIslands`, `CIGroundTypes`, `CIShackDish` (a generated shack with its dish forced: dish on its pole's axis, 6.0 m up, pole on the ground) | `DevTestsGround.cs` (support chains: a floor, pillar or prop must rest on something that reaches the ground; each banner rope end needs a post or wall; small things on a steep wall fail; a lamp or projector under a ceiling, and roof pieces joined edge to edge, count as held; `CIRenderers <object>` lists an object's meshes and colliders) |
| Stale islands (`tools\stale.ps1`, GAP17) | none (git + file times) | `STALE: <island>` for every built island older than its recipe or an included lib_ recipe (author time, HEAD + origin/content, 60 min grace); `BEHIND:` when the worktree lacks recipe commits of origin/content; `-rebuild` builds up to 12 with `contentall.ps1 -parts build -only`. pack.ps1 -Install doesn't rebuild islands, so groundall (editor sweep, plans/lib/all parts) and alltests' content phase run it first; `-player 2` also flags box copies older than Raft 1's (`SHADOWED:`, the box reads its own) |
| Build check (`tools\buildcheck.ps1`, GAP20) | none (reads the installed .rmod) | `REPO:` the worktree the content tools build from (CI_MOD_DIR, else DynamicIslands-tests) and its commit, `INSTALLED:` the .rmod and its pack stamp (`DynamicIslands.build.txt` beside it, written by pack.ps1, copied by `-Install`; never inside the .rmod: IslandNetwork.Build hashes its files); PASS when every file of the .rmod is the repo's, else `MATCH:` the worktrees that are + FAIL; alltests' content phase skips stale/contentall on a FAIL (`CI_BUILD_CHECK=warn` runs them anyway) |

Prefix families (`CIGen*`, `CIRandomizer*`, `CIStory*`, `CIPlan*`, `CIQuestBook*`, `CINoteBook*`, `CIProbe*`, `CIMeasure*`,
`CIDump*`) are spread over several files. Each command's `docs:` text starts with `Dev`, mostly followed by where it runs
(`Dev, editor`, `Dev, in game (host)`, `Dev, world`, `Dev, main menu`, `Dev, anywhere`). Grep `ConsoleCommand(name: "CI<name>"`.

## Where to change X

- New test command -> a `[ConsoleCommand(name: "CI...", docs: "Dev, <where>: ...")]` public static method in a
  `DevTests*.cs` file; long work in a coroutine; report with `Check`, finish with `Log("PASS: ...")` or `Fail`.
- New recipe step -> the switch in `DevTests.RecipeRoutine`; preprocessor keywords -> `DevTests.ExpandLines`.
- New play step -> the switch in `DevTests.PlayRoutine`; new expectation -> `DevTests.PlayExpect`.
- Islands in a world for a test -> `ScBring`, `ScSpot`, `ScOpenChest`, `ScReadNote`, `ScEnterZone`, `ScUse`,
  `ScAnimals`, `ScKill`, `ScRemove` in `DevTestsScenarios.cs`.
- Command file behaviour -> `DevTests.PollCommandFile`, `DevTests.RunCommand`, `DevTests.CommandFile`.

## Traps

- Code outside `DevTests*.cs` must never use `DevTests`: release builds don't have it. Check with `compile.ps1 -Release`
  (it compiles a copy of the csproj without the `Compile Include="DevTests` lines).
- A new test file must be named `DevTests*.cs` (so `-Release` drops it) and get a `<Compile Include>` line in
  `DynamicIslands.csproj`, or `compile.ps1` never compiles it (three test files missed it until
  10-21; Raft compiles them anyway). `pack.ps1` packs every file in `DynamicIslands\` anyway.
- All `DevTests*.cs` files are one class: helper names (`Log`, `Fail`, `Check`, `F`, `Tokens`, `Options`, `Rest`, `Num`)
  are shared, and a second one with the same signature breaks the build.
- C# 7.3 only (`LangVersion` in the csproj; RML compiles with it).
- The lines of one command file run one after another: each waits until the tests the earlier lines started (through
  `StartTest`) have ended. A command that starts a coroutine some other way returns at once, and the next line starts.
- A test that stops with `Fail` and `yield break` prints no PASS line and may leave islands or state behind.
- A command run from the file sets `HelpLinks.Automated` (no browser or Explorer windows).

## Island checks: ground, overlap, one-sided (`DevTestsGround.cs`)

- `CI<Check>World` measures the loaded islands, `CI<Check>Islands <which>` saved islands one by one, `CI<Check>Types [types] [n]` brand-new generated islands (ground and overlap only; host, a `CI ...` test world). Each `IslandCheck` writes `Mods\DynamicIslands\<file>_<world|islands|types>.txt`.
- `CIOverlap*` (`OverlapProblems`): building pieces through furniture/props (`PieceOverlaps`), then `SceneOverlaps`: a themed scene's props, chest and note (`RandomizerIslands.SceneProp` = `set.scene`, the theme's name) against each other by their collider boxes as turned (big mesh colliders too, meshes when none; 3D separating axes), fail over 6 cm, lines `scene <theme>: A ... goes d m into B`. Islands made before 10-21 have no `set.scene`: their set pieces named in `RandomizerIslands.Themes` are measured instead. Dish + pole pairs skipped.
- The generator keeps the same rule (`RandomizerIslands.FootOf`/`Into`, 2D turned boxes, 5 cm gap) in `Dress`, `Sprinkle`, `DressWorld`; `CIIslandsSound` checks it on the files (no Raft objects needed).

## Running dev commands: ci.ps1

Use `ci.ps1` in the repo root to run this mod's console commands (the CI* test commands and the others) in a running Raft. It prints only that run's `[CITEST]` lines, so do not read `Player.log` yourself.

- Needs Raft running with a dev build of the mod (`pack.ps1 -Install`, not `-Release`; `-Release` leaves out `DevTests*.cs`). At startup the mod calls `DevTests.Init` through reflection. It logs `[CITEST] Command file: <path>` and starts `PollCommandFile`.
- `PollCommandFile` reads `dev_commands.txt` in `DynamicIslands.assetpath` (`Mods\DynamicIslands\` beside `Raft_Data`) every 0.2 s, deletes it and runs each line. Blank lines and `#` lines are skipped. Each line is logged as `[CITEST] > <line>`, then `RunCommand` runs the `ConsoleCommand` method with that name. Words after the name are its arguments. An unknown name logs `FAIL: no console command called <name>`.
- Run: `powershell -ExecutionPolicy Bypass -File ci.ps1 -Command "CIEditor" -Until '^Editor ready:'`. Separate several commands with `;`.
- The commands of one run go one after another (see below), so `CIEditor;CIDemo` works in one run.
- Every test command runs its coroutine through `DevTests.StartTest`, which logs `[CITEST] IDLE (all tests done)` when the last running test ends (also after an exception, logged as `FAIL: exception: ...`). Without `-Until` a run ends there, so most runs need no `-Until`. `CINewWorld` and `CILoadWorld` end only once the world is in (`World ready`), so the next run can be a test. Commands that start no coroutine (`CIMainMenu`, `CIQuit`) still end on `-Quiet`. A new test command must start its routine with `StartTest(...)`, not `DynamicIslands.instance.StartCoroutine`. Several commands in one run (`-Command "CIA;CIB"`) run one after another: a line waits until the tests of the earlier lines have ended, and IDLE comes once, after the last (until 10-16 IDLE came after CIA, the run stopped there and the next run's test overlapped CIB - the cause of the "flaky" CIGenRaftFeatures).
- Readiness, not fixed waits (10-20; the menu line is in `DevTestsContent.cs`, `World ready` and spawn now in `DevTests.cs`,
  `Took`, `WaitIslandSettled` and `RemoveIslandsAndWait` in `DevTestsReady.cs`): `CIMainMenu` logs `At the main menu (after n s)` and IDLE once the menu's New Game box is there; `CINewWorld` waits up to 30 s for that box; `World ready` comes once the player is in and `CustomIslandSpawner.StreamTicks > 0` with no island loading (10 s cap); `WaitIslandSettled` (no `CaveMountain.Pending`, rigidbodies asleep, object count still; capped at the old fixed time) and `RemoveIslandsAndWait` replace the ground checks' and `CIPlay`'s sleeps; `CISpawnNow` ends with `PASS: spawn now - ... built in n s`. Scripts wait for these lines instead of `Start-Sleep`.
- Usual loop: `CIMainMenu`, then `CINewWorld CI <name>` (a fresh world: some rewards come once per world), then the test; `CIQuit -Until Quitting` at the end. Only name test worlds "CI ...".
- Within one test there is no common end line: `Check` logs `PASS:`/`FAIL:` for every sub-check too. `-Until` is a regex matched against the text after `[CITEST] `, on lines after the last command's echo. Use the test's final verdict text, e.g. `'editor save/load round trip'` for `CITest`. Without `-Until`, the run ends after `-Quiet` (20) s with no new `[CITEST]` line. With `-Until`, that quiet rule applies only once a `FAIL:` line has come. Long tests: raise `-Timeout` (300).
- The script writes to the command file path from the log's last `Command file:` line (else `<RaftDir>\Mods\DynamicIslands\`). It stops at once (exit 2) when the log shows the mod loaded without that line, which means a release build. A file the mod does not read in 60 s is deleted again (exit 3).
- Output: `FAIL:` lines, then at most 40 more lines: mod exceptions (`EXC`) first, then the last other `[CITEST]` lines. Lines are cut at 300 characters. `-Full` shows everything. The last line is `PASS n, FAIL n, <seconds> s`.
- Exit 0 no FAIL, 1 FAIL, 2 setup problem (one line says which), 3 timeout.
- Second Raft under Sandboxie: `-Player 2 -LogPath <its Player.log>` uses `dev_commands_2.txt`. The box must open that file to the real folder (`OpenFilePath`).

## Low graphics for test runs: CIGfx (`DevTestsGfx.cs`)

- `CIGfx low [fps]`: windowed 1280x720, quality level 0, shadows off, lowest water, AO/AA/volumetric light off, vsync off, frame cap (default 30, `0` = none). `CIGfx high`: the user's values back (taken when low was first set). `CIGfx status`.
- `tools\raft.ps1 launch` sends `CIGfx low` to every Raft it starts (both players; `rebuild.ps1`, `mp.ps1` go through it). `$env:RAFT_GFX = "high"` skips it (`docshots.ps1` does, and sends `CIGfx high`).
- Pictures stay at the user's quality: `RenderCam` (play, recipe, library pictures) and `PlayPicture` switch for the shot (`GfxShotBegin`/`GfxShotEnd`). `CIShot` captures the screen as it is: send `CIGfx high` first for full-size shots, `CIGfx low` after.
- Nothing low is kept: `GfxSavePostfix` makes Raft's Settings.rgd save the user's values; quitting restores them; `tools\gfx.ps1 watch <pid>` (started by `raft.ps1`) puts Unity's registry values (`Screenmanager*`, `UnityGraphicsQuality*`) back from the backup once the first player's Raft closes. `gfx.ps1 check` compares with the backup.

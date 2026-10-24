# Generator sweep (branch cloud/gen-sweep)

Not compiled or run: no Raft, RML or compiler here. Every API was checked against the source by reading.
The first `compile.ps1` and the first `CIGenSweep quick` will show what reading missed.

## What was written

- `DynamicIslands/DevTestsGenSweep.cs` (new, LF like every other `.cs` in the repo: `git ls-files --eol` shows all `i/lf w/lf`)
- `DynamicIslands/DynamicIslands.csproj:255`: `<Compile Include="DevTestsGenSweep.cs" />`

| Where | What |
|---|---|
| `DevTestsGenSweep.cs:24` | `GenSweepCase`: tab, settings, Raft island / variation / Rebuild it choice, ready-made kind, quick flag; `Describe()` prints everything needed to make the case again (Normal: every field that differs from a new `IslandGenSettings`, as `Field=value`) |
| `:54`, `:56` | `GenSweepPerChunk = 30` (chunk size when not given), `GenSweepTimeout = 180` s per case |
| `:79` | `GenSweepCases()`: the fixed list, always in the same order, numbered from 0 |
| `:279` | `CIGenSweep quick` (default) / `<chunk> [chunks]` / `all` / `list` |
| `:304` | `CIGenCase <number> [number ...]` |
| `:323` | `GenSweepRoutine`: the error counter, one FAIL line per failing case, the summary line |
| `:383` | `GenSweepOne`: one case through the window itself |
| `:489` | `GenSweepCheck`: the checks on the made island |
| `:583` | `GenSweepTidy`: between cases |

### The cases (about 374; `CIGenSweep list` prints them all with numbers)

| # | Group | Cases |
|---|---|---|
| 1 | every style x layout (5 x 8), radius 70, height 30, density 0.4 | 40 |
| 2 | sizes (Raft's small, large, Balboa radius from `raft_islands.txt`) x heights (`MinHeight` 3, `MaxHeight` 160) x the 8 layouts | 48 |
| 3 | peaks 1 and 5 x the 8 layouts | 16 |
| 4 | coast 0/1 x bays 0/1 x Round, Crescent, Plateau, Archipelago | 16 |
| 5 | hostile creatures 0 / 2 / 24 x the 5 styles (24 also with 24 friendly and 24 sea life) | 15 |
| 6 | buildings off; a cave alone; buildings on at every count 1..25 (kinds in turn, a cave with the odd counts); every building kind (12) at 3 with a cave | 39 |
| 7 | every other setting at its extremes, one at a time (stretch, peak shape, roughness, terraces, lakes, valleys, erosion, all shaping, cliffs, beach, shallow sea floor, rocky and reef seabed, shelf and drop-off, density 0 and 1, every object slider full / none, gathering deepest and shallowest, most loot, 8 quest steps, level up, boss creatures, smallest, biggest) and Raft's features 1..10 | 39 |
| 8 | seeds 0, 1, 42, 999999, 123456789, -7 with the window's default settings | 6 |
| 9 | seeded random mixes of every setting (`System.Random(20261016 + i)`, buildings and caves 1 in 4) | 60 |
| 10 | Randomize existing: each of Raft's 33 offered islands as a variation (with the Rebuild it choice picking it makes); every 4th also as "something new like it" (9); the 11 islands with designs also with Rebuild it = none | 53 |
| 11 | Ready-made: each of the 21 built-in kinds (`FromFile == null`), seeds 1 and 2 | 42 |

Raft's islands and the map types come last, so a change to `raft_islands.txt` or the map types leaves the numbers of
groups 1-9 as they are. Group 6 and 9 use `GenBuildings.Kinds` and group 1 `TerrainPainter.Styles` / `IslandShapes.Names`:
a new style, layout or theme renumbers what follows (as intended: they are swept too).

Quick run (`CIGenSweep quick`, 22 cases): the style x layout diagonal and the last pair, small + high and Balboa + low,
coast and bays at 1 on a crescent, 24 creatures volcanic, 25 buildings, all shaping, 10 features, seed 42, two random mixes,
the first Raft island and the biggest one, ready-made random, wreck, sky and sunken.

### Per case

1. The window is closed and opened again, the tab shown (`ShowTab`); Normal: `GeneratorWindow.Use(settings)`;
   Randomize existing: `GeneratorWindow.Pick(island, variation)`, then the seed (and the Rebuild it choice) through `Use`;
   Ready-made: the private `mapType` set by reflection and the seed through `Use`.
2. The reach line as the window shows it (`GeneratorWindow.ReachNow`, `ReachText`): it must start with its level's words
   ("Easy:", "Reachable:", "Possible but tricky:" or "By diving:", "Possible but unlikely:", "Not from the raft") and
   hold no NaN / Infinity / int.MinValue.
3. The private `OnGenerate` invoked as the button does (Ready-made: twice, the first click only asks to save first). It
   waits while the window's `loadingBuildings` is set (Raft's scenes loading), up to 180 s.
4. Made: Normal / Randomize: the window closed and `LastResult` starts "Island "; Ready-made: the window closed, the
   island file `MapTypes.FreeFileName(type, Roll(...))` exists and the editor has loaded it (`LoadedObjects` under
   `PlacedObjects`, waited for up to 180 s).
5. Checks on the editor's island:
   - terrain size finite and at most 5000 m; no NaN or infinite height;
   - land above `EditorWaterLevel` (ready-made flying / sunken: the ground isn't flat; ready-made without land: skipped);
   - Normal: the top at most 1.1 x the height asked for + 2 m (the doc says the tallest peak is exactly that height);
   - `IslandGenerator.LastReport` (Normal / Randomize): land length, width, top, area, seconds finite and not negative,
     not bigger than the terrain (top at most `MaxHeight` + 20), `Wanted` not negative;
   - objects (active `EditorGameObject`s under `PlacedObjects`): at most `MaxObjects` + 3000; Normal with trees, bushes or
     rocks asked for on land of radius 20+: at least one; every object's position and scale finite, within the terrain
     area +-100 m and height +-300 m, scale under 1000;
   - the reach line from step 2 against `IslandReach.Assess` of the made terrain: more than one level apart fails; one
     level apart is a note (counted in the summary, printed with the case).
6. Every `LogType.Exception`, `Error` and `Assert` while the case runs (one frame after it too) counts, Raft's own
   included; only `[CITEST]` lines and "Screen position out of view frustum" are left out (as DevTestsContent.cs's counter).
7. Between cases: the made ready-made file deleted, the window closed, the hidden objects Generate keeps for undo
   destroyed and the undo history cleared; every 20 cases `Resources.UnloadUnusedAssets` and `GC.Collect`.

### Output

- A failing case: one line, `[CITEST] FAIL: gen case #<n> [<group>] <tab>: <settings> (<s> s): <problems> [<notes>]`.
- A passing case says nothing, unless it took over 30 s or has a note (`  gen case #<n> ok in <s> s [...]`).
- The last line: `[CITEST] PASS: generator sweep <what>: <n> cases, 0 failed[, k with the reach line one level off the made ground], <s> s`
  or `[CITEST] FAIL: generator sweep <what>: <n> cases, <f> failed (#3 #17), <s> s`. `<what>` is `quick`, `all`,
  `chunk 3/13` or `case #17`.
- `CIGenSweep list`: one line per case, then `PASS: generator sweep list: 374 cases, 22 in the quick run, 13 chunks of about 30`.

## How to run

In the editor (the command opens it if needed and waits for it):

```
powershell -ExecutionPolicy Bypass -File ci.ps1 -Command "CIGenSweep list" -Full
powershell -ExecutionPolicy Bypass -File ci.ps1 -Command "CIGenSweep quick" -Until '^(PASS|FAIL): generator sweep' -Timeout 900
powershell -ExecutionPolicy Bypass -File ci.ps1 -Command "CIGenSweep 1" -Until '^(PASS|FAIL): generator sweep' -Timeout 900
...  CIGenSweep 2 ... CIGenSweep 13
powershell -ExecutionPolicy Bypass -File ci.ps1 -Command "CIGenCase 117" -Until '^(PASS|FAIL): generator sweep' -Timeout 300
```

Use `-Until` on the summary: a failing case logs a FAIL line first, and without `-Until` the quiet rule would end the
run 20 s after it. Or wait for `IDLE` (no `-Until`, the run ends at `[CITEST] IDLE (all tests done)`).

### Expected run time (a guess: nothing was timed)

- A plain Normal case: Generate 1-5 s (radius 280 with density 1 longer), the reach line up to about 1.5 s, the made
  ground's reach on the full heightmap (513 x 513, bigger for Temperance) about as long, plus about 0.3 s of frames.
  Say 4-8 s.
- The first case that needs Raft's scenes (buildings, caves, Raft's features past six, every Randomize existing case)
  loads them: tens of seconds once, then they stay loaded.
- Quick: about 3-5 minutes. One chunk of about 29 cases: about 3-5 minutes (the buildings, random-mix and Randomize
  existing chunks the slowest). All 13 chunks: about 45-70 minutes.
- If chunks run long, pass more chunks (`CIGenSweep 1 20`) or lower `GenSweepPerChunk`; the numbers of the cases stay.

## Unsure

- Reflection on `GeneratorWindow`'s private `OnGenerate`, `mapType` and `loadingBuildings` (`CIGenQuest` already invokes
  `OnGenerate` the same way). Renamed, the sweep stops at once with a FAIL saying so.
- The Normal top check (1.1 x height + 2 m) and the 3000 objects allowed past `MaxObjects` are my guesses at the limits;
  a failure on them might be the check, not the generator.
- The reach comparison: the window works on a coarser grid around the land (`Assess(settings)`) and on the window's
  settings, not `Effective()` (a cave can let the ground down). A level apart is only a note; two apart fails. If that
  is too strict, CIReachTest compares one island exactly.
- An error logged after a case ends (from a coroutine Generate started) counts for the next case: one extra frame is
  waited, not more.
- Ready-made `LoadIsland` calls `KeepUnsaved()`: after a generated island, it writes an autosave of it before loading
  the made one (`EditorAutosave.WriteNow`). The sweep leaves those autosaves (and the last made island) in the editor.
- Seed -7: the window's seed field is set from the settings (`ShowAll`), so a negative seed is used as is; a player can
  type one too (IntegerNumber field). If the generator doesn't take negative seeds, that case shows it.
- The Randomize existing "like it" cases use what picking the island fills in (`Pick(island, false)`); the Rebuild it
  choice is the one picking sets (`<scene>:any`) unless the case says none.

## Generator issues spotted by reading (not fixed here)

1. `Remakes.For(scene)` (`IslandGenerator.cs:306`) matches a design's scene as a substring of the island's scene:
   `Landmark_TemperanceSmall#1-3` contain `Landmark_Temperance`, so picking one of Temperance's small islets on Randomize
   existing offers (and by default chooses, `GeneratorWindow.Choose`) the Rebuild it designs of Temperance itself. The
   sweep's "randomize existing" cases of those islets will show what that builds on an islet.
2. `IslandGenSettings.Clamp` (`IslandGenerator.cs:127`) clamps Height to 2..160 although `MinHeight` is 3, and doesn't
   clamp `BuildingCount`, `QuestSteps` or `Features` (they are clamped where used: `GenBuildings` `:874`, `GenQuest` `:666`,
   `GenFeatures.Make`), so a preset or a map type's `set` line with e.g. `BuildingCount=40` keeps 40 in the settings and
   the window's slider shows it past its end.
3. The window's reach line uses the window's settings (`UpdateReach`: `ps = s`), while the preview and Generate use
   `Effective()` on Randomize existing (no buildings, no caves, Raft's kinds under water). Only a cave changes the
   ground, so the line can differ from the island made with Caves on in the Normal settings carried over.

## For TEST_CATALOGUE and the docs (not edited here)

- TEST_CATALOGUE, generator section: "GS1 generator sweep: every style x layout, size, height, peaks, coast and bays,
  creatures, buildings and caves 0-25, every other setting at its extremes, seeds, 60 random mixes, every Raft island on
  Randomize existing, every ready-made kind - no exception or error, land, no NaN or huge sizes, objects in range and
  area, reach line agrees with the ground. `CIGenSweep <chunk>` (13 chunks), quick run `CIGenSweep quick`, one case
  `CIGenCase <n>`."
- `docs/claude/areas/testing.md`, Command families, Editor tests row: add `CIGenSweep`, `CIGenCase` (`DevTestsGenSweep.cs`).
- `docs/claude/areas/generator.md`, Traps: "A new setting, layout, style or map type: the sweep picks it up only where its
  case list reads the lists (`GenSweepCases`); a new setting needs its extremes added to group 7."
- README "Source overview" table: `DevTestsGenSweep.cs` - the generator sweep (`CIGenSweep`, `CIGenCase`).

# Generator

Makes a whole island from settings and a seed: land, ground paint, nature on land and under water, creatures, loot boxes, buildings, Raft's features and a quest. Used by the editor's Generate window, map types, and islands made while sailing.

## Files
| File | Role |
|---|---|
| `IslandGenerator.cs` | `IslandGenSettings`, `IslandShapes`, `GenReport`, `GenQuest`, `GenBuildings`, the Radio Tower part of `Remakes`, and `IslandGenerator` (heights, object planning, `GenerateInEditor`, `CreateFile`, `Preview`) |
| `GeneratorWindow.cs` | The Generate window: tabs Normal / Randomize existing / Ready-made, preview map, reach line, seed, presets |
| `RaftIslands.cs` | `RaftIsland`, `HeightField`; reads `raft_islands.txt`, `island_thumbs\`, `island_heights\`; `LikeIt`, `VariationOf`; `ModFile` |
| `RaftLand.cs` / `RaftUnderwater.cs` | `raft_land.txt` (things on land per habitat bin, `BinOf`) / `raft_underwater.txt` (objects per depth band, `Bands`, `DropOffOf`) |
| `IslandReach.cs` | `Assess`: can a player get from the sea onto the land; `MoveContentWithinReach` for chests and notes |
| `GenFeatures.cs` / `GenGather.cs` | `GenFeatures.Make` (`Kinds`: vines, treasure, zipline, code, hives, dirt; `MoreKinds` past six: power, engine, cage, lift) / `GenGather.Apply` (things to gather: settings `Gather`, `Shallows`, `ShallowsDepth`, kind keys in `GatherOff`) |
| `StoryRemakes.cs` | `Remakes.StoryDesigns`: the story islands' designs, each with its `Kit` |
| `IslandLooks.cs` | `Remakes.Looks`: the story islands' looks (Caravan mesas, Tangaroa dome, Utopia towers, ...) for Randomize existing; `LookLike` sets `s.Look`, `BuildLook` runs in `GenBuildings.Apply` before the designs |
| `MapTypes.cs` | `MapType`, `MapKit` (placing content on a file), `RaftRoof.Hip`, `MapTypes.All`, `Roll`, `Create` |
| `MapTypeFiles.cs` | `.maptype` files: `MapTypeRanges`, `LoadAll`, `Parse`, `ToText`, `Export`, `ReRoll`, the console commands |
| `ScatterTool.cs` | `ScatterTool.Scatter`: many copies of one object round a point (the object browser's Scatter row), one undo step |

## Main flow (Generate in the editor)
1. `GeneratorWindow.Open` copies `IslandGenerator.Last` (at first `IslandGenSettings.RaftLike()`, also the Defaults button; plain `new IslandGenSettings()` is unchanged for presets and map types). After a Randomize existing / Ready-made generate (`randomizeMade`), the Normal tab swaps in its own last settings (`lastNormal`, `OwnSettings`, also on `SetTab`). The Layout stepper follows `IslandShapes.Order`. Controls are made with `Slider`, `Stepper`, `Choice`, `PresetButton`; a change calls `Changed`, which sets timers so `Update` runs `UpdatePreview` (`IslandGenerator.HeightsMetres` at 160 px, `Preview`, `Estimate`) and `UpdateReach` (`IslandReach.Assess`) a moment later.
2. `OnGenerate` reads the seed. Ready-made tab: `OnMakeType` -> `MakeType` -> `MapTypes.Roll` + `MapTypes.Create` -> file saved -> `DynamicIslands.LoadIsland`.
3. Other tabs: `Effective()` (Randomize existing: no buildings or caves, `RaftSeaKinds` on) -> `NeededNames` -> `LoadThenGenerate` (`PlaceableCatalog.EnsureLoaded`) when scenes are needed -> `Generate` (then `FrameCamera`, `EditorUI.RefreshIsland`, window closes).
4. `IslandGenerator.GenerateInEditor`: `DynamicIslands.ResetBuildArea` / `AreaFor` (a resize clears undo) -> before-snapshots -> `SetEditorStyle`, `SetEditorWaterLevel` -> `HeightsMetres` -> if `PlaceableCatalog.IsBuilt`: `PlanAll` -> `GenBuildings.Apply` -> `GenGather.Apply` -> `SetHeights` -> `TerrainPainter.Setup` -> old objects hidden, new ones spawned under `GeneratedObjects` (`IslandSpawner.SpawnObjects`) -> quest and Levels via `IslandSettingsUndo.Record` -> one `CommandGroup` into `UndoRedoManager.Insert`.
5. `HeightsMetres`: `ShapeOf` (a layout, or `FromRaftIsland` when `Source` is set; the Raft small / large layouts: `RaftLayoutIsland` picks a measured island by seed, `FromRaftLayout` turns, mirrors, wobbles it and adds bays, and after the normalising `RaftBeach` caps the land to a beach plus a walkable slope and a wading shelf, then `Normalise` again) -> stretch -> `ShapePeaks`, `CarveValleys`, `Normalise`, `Terrace`, `Erode`, `CarveLakes`, `ShapeSeabed`, `FlattenEdges`.
6. `PlanAll`: `Survey` / `SurveySea` -> `LandTargets` + `SeaTargets` (scaled down past `MaxObjects`) -> `PlanNature` -> `PlanSea` (`PlanReefs`, `SeaCap`) -> `PlanContent` (creature spots, loot boxes).
7. `GenBuildings.Apply` on a `MapKit`: `Remakes.Build` -> cave (`RandomizerIslands.EmbeddedCave`) -> huts, cabins, themes, `Landmark` -> `GenFeatures.Make` -> `GenQuest.Make`.

Without the editor, `IslandGenerator.CreateFile(settings, name)` runs steps 5-7 at `AreaFor` size and `BuildResolution` (513). `MapTypes.Create` uses the type's `Build` or `CreateFile`, then its `Content`, then `IslandReach.MoveContentWithinReach`. While sailing, `CustomIslandSpawner` uses `RandomSettings` or a map type; `GenerateAndSpawn` saves the file and calls `IslandNetwork.BroadcastAdded`. Console: `GenerateIsland [seed] [size] [height] [roughness] [peaks] [objects] [style]`.

## Data and keys
- Presets: `Mods\DynamicIslands\generator_presets\<name>.txt` (`GeneratorWindow.PresetFolder`), `Field=value` lines from `IslandGenSettings.ToText` (every public instance field, by reflection). `FromText` skips unknown names and calls `Clamp`.
- Map types: `Mods\DynamicIslands\maptypes\<name>.maptype` (key = value; full help in `MapTypeFiles.Help`). `set = <Field> <value>` uses the same field names. `LoadAll` replaces the file types in `MapTypes.All`. Commands `ExportMapType`, `ReloadMapTypes`, `ReRollMapType`.
- Generated files: `gen-<type>-<seed>` (`MapTypes.FileName`; `FreeFileName` adds -2, -3). `CustomIslandSpawner.RemakeOf` makes a missing one again from its name: same kind and seed, not always the very same island.
- `raft_islands.txt`: one island per line, tab-separated `key=value` (`scene`, `label`, `kind`, `style`, sizes, then the `RaftIslands.Kinds` counts). `island_thumbs\<SafeName(scene)>.jpg` (or `.png`). `island_heights\<SafeName(scene)>.bin`: magic `CSIH`, deflated int16 decimetres above the sea (`WriteHeights` / `ReadHeights`).
- `raft_land.txt` rows `land` / `lobj`; `raft_underwater.txt` rows `area` / `profile` / `obj` / `tex`. Each file's `#` header names the columns.
- The data files sit in the repo's `DynamicIslands\` folder and ship in the .rmod. `RaftIslands.ModFile` reads the .rmod copy; `Mods\DynamicIslands\<file>` wins only when the .rmod has none or the file `dev_overrides` exists. Dev commands `CIMeasureIslands`, `CIMeasureLand`, `CIMeasureUnderwater` write new ones; `Reload()` rereads.

## Where to change X
| Task | Edit |
|---|---|
| A new setting | a field in `IslandGenSettings` with a neutral default, `Clamp`; a control in `GeneratorWindow.BuildNormal` / `BuildContent` |
| A new layout | `IslandShapes` constants, `Names`, `Hints` (same index), `Order` (the Layout list's order); `IslandGenerator.ShapeOf` (`Marsh` and `TwinPeaks` go through `Round`); the shape list in `MapTypeFiles.Help` |
| What a style scatters | `IslandGenerator.Pools` (a `StylePools` of regexes per style) |
| How dense / Like Raft | `LandTargets`, `LikeRaftAmount`, `TypicalDensity`, `NatureLikeRaft`; under water `SeaTargets`, `SeaCap`, `PlanReefs` |
| Where things stand | `PlanNature`, `HabitatOf`, `WithinHabit`; under water `PlanSea`, `BaseDrop`, `LowestShowing` |
| Creatures, loot boxes | `PlanContent`, `HostileKindsOf`, `TierLoot` |
| A story island's look (Randomize existing) | a `Look` in `IslandLooks.cs` `MakeLooks` (`Scenes`, `Kit`, `Shape`, `Build`); test `CILookAlike` (DevTestsLooks.cs) |
| Buildings, caves, wrecks | `GenBuildings.Apply`, `Hut`, `Landmark`, `LandmarkNames` |
| A built-in map type | an entry in `MapTypes.All` (`Ranges` or `Settings`, `Content(MapKit, s)` or `Build`) |
| Reach rules | `IslandReach.WalkSlope`, `StepUp`, `JumpUp`, `SwimLedge`, `ClimbLedge`, `Assess` |

## Traps
- The same settings and seed must give the same island (presets, map types rolled from a seed, `CIGenParity`). Each step seeds its own `System.Random` from `s.Seed`; give a new step its own stream, and add new draws only after the old ones (as `GenFeatures.Make` does past six). Hash names with `StableHash`, not `string.GetHashCode`. A new setting must default to the old behaviour.
- `GenerateInEditor` and `CreateFile` must stay in step (`CIGenParity` in `DevTestsMore.cs` compares them).
- `IslandFile` / `MapKit` positions are terrain-local; heights are metres above the terrain's base. The sea is at `WaterLevel`: `IslandFile.DefaultWaterLevel` (20, shallow) or `IslandFile.DeepWaterLevel` (160, deep). Use `s.WaterLevel` or `MapKit.Sea`, never a fixed number.
- Nature is planned from `PlaceableCatalog.GenNames` only (CA19): Raft's own scene objects and things to gather that a catalog without `placeables.txt` makes core, no buildables (other mods add those), ordinal order - the same list on every PC (`CIGenNames`). Never sort a list a seed picks from with the culture's order. Objects from Raft's other scenes must be listed in `GenBuildings.NeededNames`, `Remakes.NeededNames` (a design's `Kit`), `GenFeatures.NamesFor` or `SeaNamesNeeded`, so they load before generating. `PlanAll` returns nothing until `PlaceableCatalog.IsBuilt`.
- `MapKit.Add` and `MapKit.Clear` remove objects without `Props` near new content. Content that must stay needs `Props`.
- Kept in step with `TerrainPainter.Styles`: `IslandGenerator.Pools` (same index), `GenGather.Kinds`, `GeneratorWindow.StyleHints`, the switch in `Preview`. `RaftLand.For` and `RaftUnderwater.For` give volcanic the desert's data.
- `RaftSeaKinds` is a property, so it is not in presets or `set` lines.
- Islands made while sailing are generated on the host only (`CustomIslandSpawner.Tick` returns on clients); clients get them through `IslandNetwork`.
- The generator sweep (`CIGenSweep`, `DevTestsGenSweep.cs`) reads the style, layout, building-kind, Raft-island and map-type lists, so new ones are swept (and renumber the cases after them); a new setting needs its extremes added to group 7 of `GenSweepCases`.
- `pack.ps1 -Release` leaves out `DevTests*.cs`: game code must not call into them.
- C# 7.3. Docs: README `The island generator`, `Map types`, `Console commands (F10)` and the source overview; `docs/GUIDE.md` 4.5 (The island generator) and 4.6 (Ready-made islands); then the PDF is made again from `GUIDE.md`.

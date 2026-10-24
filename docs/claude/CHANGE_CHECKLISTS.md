# Change checklists

Every place that changes together for one kind of new feature, so the change can be made in one pass.
Do not read this whole file. Run `grep -n '^## ' docs/claude/CHANGE_CHECKLISTS.md`, read only your section, then read "Every feature" at the end.
Paths are under `DynamicIslands/` unless they say otherwise. Every name here can be found with grep. MAP.md says where things live.
RECIPE_FORMAT.md and PLAY_FORMAT.md describe the content test files.

## New .cs file
**Must change**
- `DynamicIslands.csproj`: add a `<Compile Include="Name.cs" />` line. `compile.ps1` builds from this file. `pack.ps1` packs the files of `DynamicIslands\` whether they are in the csproj or not, so a file left out still ships but is never compile-checked.
- Use `namespace DynamicIslands.Editor` and C# 7.3 (`LangVersion` in the csproj). Only `DynamicIslands.cs`, `terraineditor.cs`, `BlueprintSteps.cs`, `LaserBeam.cs`, `RaftStore.cs`, `RaftVending.cs` and `DevTests*.cs` use `namespace DynamicIslands`.
**Usually also**
- Work done every frame: add `try { X.Tick(); } catch (Exception e) { TickError("...", e); }` to `DynamicIslands.Update`. Work done at start: add a `StartStep(...)` to `DynamicIslands.Start`. If you hook a Raft or Unity event, take it off in `DynamicIslands.OnModUnload`.
- Harmony patch classes: `PatchHealth.PatchAll` applies every `[HarmonyPatch]` class on its own. Add a word to `PatchHealth.Feature` so a patch that fails names the feature.
- Dev tests go in a file named `DevTests<Area>.cs` holding `public static partial class DevTests`, with commands named `CI...`. The `DevTests` prefix is what `-Release` leaves out.
**Test**: `compile.ps1`, then `compile.ps1 -Release`.
**Docs**: add a row for the file to the README `### Source overview` table.

## New console command
**Must change**
- Put `[ConsoleCommand(name: "Name", docs: "...")]` on a `public static void ...Command(string[] args)`. It goes in the feature's own class (as `MonsterDifficulty.MonstersCommand`, `BuildCost.BuildCostCommand`, `WorldIslands.WorldIslandsCommand`), or in `DynamicIslands.cs` next to `LevelsCommand`.
- Follow the order the existing commands use:
  - Check the word first.
  - `LoadSceneManager.IsGameSceneLoaded`: at the main menu, a setting command sets the next new world and remembers it.
  - `Raft_Network.IsHost`: refuse with a text that contains "Only the host".
  - Make the change. Call `IslandWorldState.Save()` if world state changed. Then call `DynamicIslands.Notify(...)`.
**Usually also**
- With no arguments, show the current value (as `Levels`, `RegrowDays`, `BuildCost` do).
- Send the change to players through the feature's message (see "New network message kind").
- For a host-only command, add it to `tries` in `DevTests.HostOnlyRoutine` (`DevTestsHostSettings.cs`). That test counts "Only the host" log lines.
**Test**: write a CI test that calls `...Command(new[] { ... })` directly. Run `CIHostOnlyCheck` as a player who joined.
**Docs**: add the command to the README `## Console commands (F10)` table and the GUIDE `## 11. Console commands` table. Name it in the feature's own README and GUIDE sections.

## New island setting (saved in the .island)
**Must change**
- `IslandInfo.cs`: add a key constant to `IslandProps` (`group.name`, as `rules.levels`).
- `EditorUI.cs`: add a control to `EditorUI.BuildInfoTools` (groups "Shown to players", "Rules", "Quest"). It writes through `EditorUI.SetInfo` and is shown in `EditorUI.RefreshInfo`.
- The effect in a world reads one of these:
  - `IslandSettings.Props`, which `IslandInfo.Tag` puts on each spawned island root.
  - `IslandCache.PropsOf(entry)`, which also works while the island is unloaded.
  - Per-island overrides of a world value go in `IslandRules`.
**Usually also**
- The file format does not change: `IslandFile.Props` (format 4) holds any key. Binary data goes in a tag of `IslandFile.Tail` (`DynamicIslands.currentIslandTail` in the editor). `CIFormatTail` tests the tail.
- `EditorUI.SetInfo` goes through `IslandSettingsUndo.Change`, which makes the undo step and the unsaved mark the autosave sees. That snapshot holds only `currentIslandProps`, `currentStyle` and `currentElevation`.
- If the generator sets it: in `IslandGenerator.GenerateInEditor` (inside the generation's undo step, with `IslandSettingsUndo.Record`), in `IslandGenerator.CreateFile`, and in `GeneratorWindow.MakeType` (map types).
- Players who join get the island file itself. A world-wide effect starts from `IslandInfo.Tag` (the level up system used to; it is a world option only since 2026-10-16).
- Recipes already set any key with `island key=value`. If Check should warn about it, change `PlanChecker`.
**Test**: `CIPropsTest` (`DevTestsContent.cs`) saves and loads island settings. Add a `.play` step if players see the setting.
**Docs**: GUIDE `### 4.4 The Island tab`; README `## The editor`.

## New per-object setting
**Must change**
- Add the key to `ObjectProps` (as `tint.color`, `loot.items`) or to the feature's own class (`CodeLock.Code` = `lock.code`, `ZiplineEnds.ZipTo` = `zip.to`, `BehaviourProps`).
- `ObjectInspector.cs`: add a control in `ObjectInspector.Groups`, chosen by object kind (as `ZiplineGroup`) or by the object's settings (as the keypad code field). It writes through `ObjectInspector.Set` or `PropsCommand.Change(target, ObjectProps.With(...))`. That is one undo step and counts as unsaved.
- The effect in a world goes where the object comes alive or is used:
  - `IslandSpawner.SpawnBody` (with `editable` false), as `ZiplineEnds.Apply`, `CustomNote.Attach`.
  - Or the use handler `UseInteract` (`Behaviours.cs`), as `CodeLock.Use`.
**Usually also**
- In the editor's view, `ObjectProps.ApplyInEditor` shows it. `ObjectProps.Defaults` gives a value when the object is placed from the list.
- State that stays after use (opened, unlocked):
  - Store it as a key in `IslandWorldState.Entry.State`, in a range no other feature uses (see the `*KeyBase` / `*Base` constants, e.g. `CodeLock.LockKeyBase`).
  - Set it with `ContentState.MarkUsed` or `IslandNetwork.SendUsed`.
  - If only one player may have it, use `Claims`.
- If it gives or needs story items, zones or pages, change `PlanChecker.FromFile` and `PlanChecker.FindTraps`.
- An object players need one of Raft's tools for (a feature): add it to `RaftFeatures.Features` (and the tool to `RaftFeatures.Tools`; a variant item goes in the tool's `Also`, a quest-item tool sets `Story`; a feature that only counts with some props sets `When`). The plan Check and the generator's explorer's chest follow it.
- Recipes set any key with `prop <alias> key=value`. Add a verb to `DevTests.RecipeRoutine` (as `zipto`) only when the value must be worked out, and describe it in RECIPE_FORMAT.md.
**Test**: a `CIPropsTest`-style editor test (inspector, undo, save/load). In a world, `CIFeatureCheck` or a `.play` (`use name`, `expect shown name`).
**Docs**: GUIDE `### 4.3 Placing objects (Objects tab)` or the matching `## 5. Making islands come alive` subsection; README `## The editor`.

## New state saved with the world
**Must change**
- In the feature's class, add:
  - `internal static void Reset()`
  - `internal static bool ReadLine(string key, string value)`: reads its own `@key=` lines and returns true for them.
  - `internal static IEnumerable<string> WriteLines()`
  - `internal static bool HasState`
- `IslandWorldState.OnWorldLoaded`: call `Reset()`, and add `ReadLine` to the `||` chain. Some callers pass the key lower-cased.
- `IslandWorldState.Save`: add `!X.HasState` to the "nothing to save" test, and `lines.AddRange(X.WriteLines())`.
- When the state changes in a running world, call `IslandWorldState.Save()` right after (as `WorldOptions.Set` does).
**Usually also**
- Players who join need the state:
  - Add `JoinPart("...", () => X.Message(), to)` in `IslandNetwork.OnMessage` (the `SyncRequest` case).
  - The client handles it in `X.OnMessage`.
  - The client clears it in `IslandNetwork.OnWorldReceived`.
  - State that must not carry into the next world: forget it where `WorldDirector.Tick` sees that the world was left.
- Older versions keep `@` lines they don't know (`keptLines`), and warn when `@modversion=` is newer. To add a value to an existing comma list, append it and read it only when present (as `@rndsailed=`).
- If a line names an island, change `Housekeeping.NamesIn` (Tidy up) and the world-file loop of `IslandRename.Rename`.
**Test**: `DevTestsX5.cs` (`CIOldWorldLines`, `CINewerWorldFile`, `CIEverythingOn`). Add shared values to `DevTests.ServerSig`.
**Docs**: the README `## Installing` row for `worlds\<world id>.txt`; the feature's GUIDE section.

## New network message kind
**Must change**
- `IslandNetwork.cs`: add a `public const int` to `IslandNetMessage`, using the next free number: 1-29 and 33-37 are taken (the highest is `Kraken` 37), 30-32 are unused, so the next is 38. Give it a summary: who sends it, and which fields it uses (`Ids`, `Index`, `Count`, `Name`, `Data`, `Hash`, `Offsets`).
- Send it with `IslandNetwork.SendToEveryone`, or a helper like `SendLevels` / `SendWorldRules`. Host-only sends check `Raft_Network.IsHost`.
- Add `case IslandNetMessage.X:` to `IslandNetwork.OnMessage`, calling `X.OnMessage(msg ...)`. The host applies a player's message before it passes it on with `SendToClients`.
**Usually also**
- Players who join: add a `JoinPart` in the `SyncRequest` case. Each part is sent on its own, and order matters: parts that name islands go after the island list.
- Older versions ignore a `Kind` they don't know (`OnMessage` has no default case).
- If a player must know whether the host handles the new kind:
  - Add a word to `HostCapabilities`.
  - Read it in the client branch of `SyncRequest` (as `HostAddsCounts`).
  - Reset it with the others in `IslandNetwork.Tick`.
- The host decides values such as the day: it does not trust a player's value (as `ObjectUsed` does).
- Single-machine tests use `IslandNetwork.Loopback`.
**Test**: `CINetTest` (`DevTests.cs`: messages survive RML's serializer). With two players: `CIMPState`, `CIServerSig`.
**Docs**: GUIDE `## 8. Playing together` (`### Who needs what: every case`); README `### Source overview` (it gives kind numbers, as "kind 17").

## New world option (World settings > Extra options)
**Must change**
- `WorldOptions.cs`: add a name constant at the same place in `WorldOptions.All`, `WorldOptions.Labels` and `WorldOptions.Hints`. The three lists line up by index. These follow `All` without changes: the New Game window (`WorldSettingsWindow.Build`), Esc > Custom Islands (`WorldWindow.Build`), the message and `@options=`.
- The feature checks `WorldOptions.On(WorldOptions.X)` or listens to `WorldOptions.Changed` (raised on every machine). Host-only work also checks `Raft_Network.IsHost` (as `GhostRafts`).
- The option's own saved state is called from `WorldOptions.ReadLine` / `WriteLines` / `HasState` / `Reset` / `OnWorldReceived` (as `PrivateStorage`; `GhostRafts` only from `ReadLine`, `WriteLines` and `Reset`). Shared data goes through `WorldOptions.Message` / `OnMessage`.
**Usually also**
- The docs text of `WorldOptions.WorldOptionsCommand` lists the options.
- Shuffles use `WorldOptions.Seed`, which is the same on every machine.
- For a Harmony patch, add a word to `PatchHealth.Feature`.
- Older versions: `WorldOptions.Parse` drops option names it doesn't know.
**Test**: `DevTestsOptions.cs`:
- `CIWorldOptionsUnit` checks the exact `Encode` text of `All`.
- `CIOptionsMatrix` loops `mask < (1 << groups.Length)`: each option alone, the sea options as one group and the retired ones as one (512 combinations, ~22 min). A new sea option goes in its `sea` list, or the run doubles.
- Also run `CIWorldSettingsBox`, `CIWorldWindow` (`DevTestsPolish.cs`) and `CIEverythingOn`.
**Docs**: GUIDE `### 9.4 Extra options` and `### 9.1 The World settings window`; README `## World settings: more ways to play Raft again` and the `WorldOptions` row of the console table.

## New world setting (New Game choice or rule, or a spawnpool.txt value)
**Must change** for a choice kept with the world (as monster difficulty, build cost or the level up system):
- Hold the choice in a `Pending` value (as `PlayerLevels.Pending`). Keep the last choice in `world_rules.txt` with `WorldRules.SaveDefault` / `WorldRules.ReadDefault`. In `Reset`, a new world (`GameManager.IsInNewGame`) takes the choice.
- Save it as in "New state saved with the world". Small rules can go in `WorldRules.ReadLine` / `WriteLines` / `HasState` and travel in `WorldRules.Message` (kind 15).
- New Game window:
  - Add a control in `WorldSettingsWindow.Build` or `NewWorldRulesBox.BuildInto`.
  - Count it in `WorldSettingsWindow.Changed`.
  - Reset it in `WorldSettingsWindow.RaftsOwn`.
  - Show it in `WorldSettingsWindow.Show`.
  - Save it when Create is pressed: `WorldSettingsChoice.Prefix` (options, levels) or `NewWorldRulesChoice.Prefix` (the rules box).
  - `WorldOptions.Reset` drops unsaved choices when a saved world loads.
- Running world: add a control to `WorldWindow.Build` (the host changes it; players see it read-only) and a console command.
**Must change** for a spawnpool.txt value:
- `CustomIslandSpawner.SetValue`, `Clamped`, `ValueOf`, `NumberKeys` and the `DefaultPool` text.
- A `Setting` row in `DefaultsWindow` (`Sailing`, `Shared` or `Generated`).
- If every player must share it, also `WorldRules.HostSettingsData` / `HostSettingsFrom`.
**Test**: `CIMonsterUnit`, `CIWorldRulesBox` (`DevTestsDifficulty.cs`), `CIWorldWindow`, `CISettingsWindow`, `CIEverythingOn`, `DevTests.ServerSig`.
**Docs**: GUIDE `## 9. World settings: rules and extra systems` and `## 10. Settings files`; README `## World rules: monster difficulty and build cost` or `## World settings: more ways to play Raft again`, and the `## Installing` rows.

## New world-plan rule part (WHAT, WHEN, WHERE or "done when")
**Must change**
- `WorldDirector.cs`:
  - The kind lists: `IntroRule.WhatKinds` / `WhenKinds` / `WhereKinds` / `DoneKinds`.
  - `IntroRule.ToLine` and `IntroRule.Parse` (`IntroRule.NormalDone` for done kinds).
  - `IntroRule.DescribeWhat` / `DescribeWhen` / `DescribeWhere` / `DescribeDone`.
- The host's checks:
  - `WorldDirector.Met` and `WorldDirector.Happened` (WHEN), `WorldDirector.Place` (WHERE), `WorldDirector.Bring` (WHAT).
  - `ReturningIslands.WaitsFor` lists the WHEN kinds that wait for an island.
  - Done kinds: `StoryChain.IsDone`.
- `WorldPlanWindow.cs`: `WhenOptions` / `WhatOptions` / `WhereOptions` / `DoneOptions`, and the card fields in `WorldPlanWindow.RuleCard` (`needsRef`, `needsArg`) or `StoryRow`.
- `PlanChecker.cs`: `NeedsIsland`, `CheckWhen` / `CheckIslandFor`, `CheckWhere`, `CheckBring`, `CheckStory`.
**Usually also**
- If the rule needs a fact about an island: `IslandCache.Info` / `IslandCache.Get` (in `WorldDirector.cs`) and `PlanChecker.Facts` / `PlanChecker.FromFile`.
- If a part names an island: `LibraryPack.IslandsOf` / `LibraryPack.Rename`, `Housekeeping.RuleIslands`, `IslandRename.RenameIn`, `CustomIslandSpawner.AllPlansIslandNames`.
- WHERE kinds that are not brought at once (`receiver`, `sailing`) are `IntroRule.Special` and handled in `StoryChain`.
- Older versions:
  - `IntroRule.Parse` returns null for a kind it doesn't know.
  - Plans keep that line (`WorldPlan.Kept`). The world file keeps the `@planrule=` line. An island's `bring.rules` skips it (`WorldDirector.RulesFromProps`).
  - An unknown done kind reads as "".
- A plan-level setting instead:
  - Add a `WorldPlan` field and handle it in `WorldPlan.Parse` / `WorldPlan.ToText`.
  - Store the world's copy in `WorldDirector.WriteLines` / `ReadLine` (`@plan...`).
  - Add a `WorldPlanWindow.Recipe...` hook and a `plan` verb in `DevTests.RecipeRoutine`.
**Test**: `CIRuleTest`, `CIPlanTest`, `CIPlanWorld` (`DevTestsDirector.cs`), `CIStoryChainUnit`, `CIScCheckRuntime`. A plan `.play` can use `tune`, `arrive`, `sail` and `expect chain`.
**Docs**: GUIDE `### 7.3 A rule card, part by part`, `### 7.4 Everything a rule can do`, `### 7.5 Check: finding and fixing problems` and `### 7.7 The plan file`; README `## World plans`.

## New quest step kind
**Must change**
- `Quest.cs`:
  - Add the kind to `IslandQuest.Types`.
  - Add its text to `IslandQuest.Step.Describe`.
  - If the host counts it, add it to `IslandQuest.Counted` (as `collect`, `pages`: `QuestTracker.Found`, checked from `QuestTracker.Tick`).
  - Otherwise call `QuestTracker.Event(entry, "type", target, amount)` where it happens (as in `TriggerZone`, `CustomNote`, `LootCrate`, `CreatureSpawner`). `QuestTracker.Matches` compares the type and the target.
- `QuestEditorWindow.cs`: `TypeLabels`, `TypeHints`, `TargetHints`, and `NamesFor` (the target list). The count field shows for `kill`, `catch` and counted kinds.
- `PlanChecker.cs`: `PlanChecker.CheckQuest` (can the step be done on this island?), using `PlanChecker.Facts`.
**Usually also**
- Older versions: `IslandQuest.From` skips a step kind it doesn't know, so the quest is shorter there.
- Quest traps: `PlanChecker.FindTraps`. Generated quests: the pool in `GenQuest.Make` (`IslandGenerator.cs`).
- Recipes accept any `IslandQuest.Types` kind in `step type|target|count|text`. A new player action in a `.play` needs a verb in `DevTests.PlayRoutine` and a line in PLAY_FORMAT.md.
**Test**: `CIQuestTest`, `CIQuestWorld` (`DevTestsContent.cs`), `CIMoreQuests`, `CICheckQuestTraps`, `CIGenQuest`. A `.play` can check it with `expect step n`.
**Docs**: GUIDE `### 6.1 Quests` and `### 6.2 Behaviour and events` (quest traps); README `## Features`.

## New behaviour action or condition
**Must change** for an action:
- `Behaviours.cs`:
  - `BehaviourProps.Verbs`, and `SharedVerbs` if the host runs it once for everyone.
  - `ObjAction.HasTarget` / `ObjAction.HasArg`, and `ObjAction.Describe`.
  - Run it in `Behaviours.RunShared` (shared verbs) or `Behaviours.RunPersonal` (each player).
- `BehaviourWindow.cs`: `VerbLabels`, `VerbHints`, and the fields in `BehaviourWindow.ActionRow`.
**Must change** for a condition ("only if"):
- `BehaviourProps.CheckKinds`, `ObjCheck.What`, `Behaviours.Holds`. The host checks story-item conditions again in `Behaviours.StoryChecksHold`.
- `BehaviourWindow`: `CheckLabels`, `CheckHints`, and the fields in `BehaviourWindow.CheckRow`.
**Usually also**
- Older versions: `ObjAction.Parse` / `ObjCheck.Parse` drop lines with a verb or kind they don't know.
- If rules or Check should see what it sends or gives (signals, pages): `IslandCache.Get` (in `WorldDirector.cs`), `PlanChecker.FromFile`, `PlanChecker.FindTraps`.
- A new event (`BehaviourProps.ObjectEvents` / `IslandEvents`): call `Behaviours.Fire(entry, index, "event", localPlayer)` where it happens.
- Recipes already set any action line with `beh <alias> on.<event>=...`.
**Test**: `CIBehaviourTest` (editor), `CIBehaviourWorld` (world), `CIStoryTest` (checks). With two players: `CIMPIsland` / `CIMPFull`.
**Docs**: GUIDE `### 6.2 Behaviour and events`; README `## Features` and the `Behaviours.cs` row of `### Source overview`.

## New editor window or tab
**Must change** for a window:
- Give the class `public static bool IsOpen`, `Open()` and `Close()`.
- Create it with `X.Create(EditorUI.Canvas.transform)` in the `try` list of `DynamicIslands.OpenEditor`. Build the UI from `UIKit` (`UIKit.Group`, `UIKit.Button`, `UIKit.Field`).
- If it dims the editor, add `X.IsOpen` to `EditorInput.WindowOpen` (`EditorTools.cs`) so editor shortcuts wait.
- Set `EditorInput.IsTyping` while a text field has focus, and set it false on close.
- Esc and Enter:
  - A window opened over another sets `EditorInput.SubWindowClosedFrame = Time.frameCount` in `Close()`.
  - The window under it skips that frame (`EditorInput.SubWindowJustClosed`, `DropList.Busy`), as `QuestEditorWindow` does.
**Usually also**
- If the window is a static class rather than a MonoBehaviour, add it to `DevTests.StaticWindows` (`DevTestsAll.cs`) so `CIButtons` closes it.
- Make its changes as undo steps (`IslandSettingsUndo.Change`, `PropsCommand.Change`) so the autosave sees them. Guide links use `HelpLinks.OpenGuideSection("<anchor>")`.
- For a tab: `TAB` in `UnityScripts/TabSelector.cs`, a `Build...Tools` panel in `EditorUI.Setup`, `EditorUI.OnTabChanged`, and `tabButtons` in `EditorUI.BuildTopBar`.
**Test**: `CIButtons` presses every button of every window. Also run `CIEditorState`, and add a test that clicks through the window as a player does.
**Docs**: GUIDE `## 4. Building your own island: the editor` (`### 4.1 The screen` for the top bar and tabs); README `## The editor`; a picture in `docs/images` where it helps.

## New generator setting or feature
**Must change**
- `IslandGenerator.cs`: add a public field to `IslandGenSettings` whose default changes nothing (0 or off). These follow public fields without changes: presets (`ToText` / `FromText`; a missing name keeps its default), map type `set <field>` (`MapTypeFiles.ApplySets`) and recipe `gen Name=value`.
- `GeneratorWindow.cs`: add a `Slider(...)` or button in `BuildNormal` / `BuildContent` / `BuildRandomize` / `BuildReady`.
- Make both paths give the same result for the same seed: `IslandGenerator.GenerateInEditor` (editor) and `IslandGenerator.CreateFile` (worlds, map types through `MapTypes.Create`). A content step put in `GenBuildings.Apply` (as `GenFeatures.Make`, the quest) or `GenGather.Apply` reaches both, since both call them.
**Usually also**
- Clamp the field in `IslandGenSettings.Clamp`.
- If it places objects from Raft's island scenes, list them in `GenBuildings.NeededNames`. They are loaded with `PlaceableCatalog.EnsureLoaded` before generating in the editor.
- Islands made while sailing: `IslandGenerator.RandomSettings`, `CustomIslandSpawner.GatherFor` (spawnpool.txt `generated...` values), `WorldRandomizer.GatherFor`.
- Generated quests: `GenQuest.Make`. Check them with `PlanChecker.QuestFindings`. Island settings set during a generate go in its undo step with `IslandSettingsUndo.Record`.
**Test**: `CIGenParity` (editor and `CreateFile` give the same island), `CIGenMatrix`, `CIPresetTest` (`DevTestsMore.cs`), plus the feature's own (`CIGenGather`, `CIGenRaftFeatures`, `CIGenQuest`).
**Docs**: GUIDE `### 4.5 The island generator`; README `## The island generator`.

## New randomizer part
**Must change**
- `WorldRandomizer.cs` → `RandomizerSettings`: add a name constant at the same place in `Features`, `FeatureLabels` and `FeatureHints`. The hint says how often at each level and what stays as Raft has it. These follow `Features` without changes: `NewWorldOptions`, `WorldWindow`, `Encode` / `Decode` and the `Randomizer` command.
- The work is gated by `Current.Has(RandomizerSettings.X)` and scaled with `Pick(light, normal, wild)`. Places the existing parts use:
  - Extras on Raft's islands: `RandomizerContent.Extras`, plus the gate in `WorldRandomizer.GiveExtras`.
  - Raft's animals: `WorldRandomizer.VariantOf`.
  - Islands while sailing: `WorldRandomizer.OnSailed` (`Bring`, `OwnTypes`, and a map type in `MapTypes`).
**Usually also**
- A sailing part: when it is switched off, clear what is due in `WorldRandomizer.Set` and `WorldRandomizer.OnMessage`.
- A part is on unless it is listed in `Disabled`. So a new part is on in every existing world that has the randomizer on.
- Sailing counters: append them to `@rndsailed=` / `@rndcount=` in `WorldRandomizer.WriteLines`, and in `ReadLine` read them only when present.
- Update `WorldRandomizer.Describe` and the docs text of `DynamicIslands.RandomizerCommand`.
**Test**: `CIRandomizerHints` (`DevTestsHostSettings.cs`: one hint per part), `CIRandomizerUnit`, `CIRandomizerWorld`, `CIOddities` (`DevTestsRandomizer.cs`), `CIEverythingOn`.
**Docs**: GUIDE `### 9.3 The world randomizer`; README `## World randomizer` and the `Randomizer` row of the console table.

## Something a library pack must carry
Use this when a feature makes an island or plan depend on another file. Settings inside an `.island` or `.plan` already travel with it.
**Must change**
- `LibraryPack.cs`:
  - Gather it on export: `LibraryPack.Collect` follows the islands that rules bring, and `MapTypesUsed` does the same for map type files.
  - Add its extension to `AllowedExtensions`.
  - Give it a kind like `KindMapType`, handled in `PathOf` / `ExtensionOf`.
  - Install it in `InstallFiles` (undone by `InstallUndo` if the install fails), and remove it in `RemoveFile`.
- If a name can change on install, rewrite the references in `LibraryPack.Rewritten` / `LibraryPack.Rename`.
- Online downloads: `LibraryEntry.InstallFiles` (`LibraryOnline.cs`) picks files by extension.
**Usually also**
- Rename in the editor: `IslandRename.Rename` / `IslandRename.RenameIn`.
- Tidy up must count the file as used: `Housekeeping`.
- Reading a pack: `LibraryPack.Validate`.
**Test**: `CILibraryUnit`, `CILibraryRoundTrip`, `CILibraryWindows` (`DevTestsLibrary.cs`), `CILibraryDownload`, `CIRenameIsland`.
**Docs**: GUIDE `### 4.7 Saving and sharing`; the README bullet **The island library.** under `## Features`, and `## Installing`.

## New library island or plan (content only)
This is the most common change in history. It does not touch any `.cs` file.
**Must change**
- `content/recipes/<name>.recipe`: `CIRecipe <name>` builds it through the editor (RECIPE_FORMAT.md).
- `content/tests/<name>.play`: `CIPlay <name>` plays it in a test world `CI ...` (PLAY_FORMAT.md).
- `content/tests/<name>_pics.play`: the guide pictures.
- `docs/images/library/<name>.jpg`.
- GUIDE `## 15. Learn from the library: example islands and plans`: add its row to the matching table (`### 15.2` to `### 15.8`).
- GUIDE `## 16. Learn from the islands`, `### 16.4`: if the island is the best one to learn a feature from, name it in that feature's row (every editor feature has one island there).
- README `## Features`, bullet **The island library.**: update the island count and its list.
- `docs/Custom-Islands-Guide.pdf`, made again from GUIDE.md.
**Usually also**
- Both commands read the files from `Mods\DynamicIslands\recipes\` (`DevTests.RecipeFolder`), not from `content\`.
- Every object a recipe names must be in the editor, or the recipe stops. `content/objects/objects_<category>.txt` holds sizes measured by `CIMeasureObjects`.
- A plan recipe uses `plan new` ... `plan save`. If Check finds a problem, the recipe stops.
- **Everything stands on the ground** (the user, 2026-10-11). Every structure, cave piece, prop and plant rests on the land,
  the sea floor or another object: nothing hovering, no house with its legs in the air on a slope, nothing buried whole,
  cave rims flush with the ground. Roofs rest *on* pillars or walls; foundations are never used as roofs. On a slope,
  flatten the ground under a building first (`ramp`/brush) or give it legs that reach down. Think about this while
  placing, not only at the end: set a height only when the object must sit on something else.
  What hangs is held too (2026-10-22: a zipline end 0.4 m over a short post on The Lodestone Array, a ballast stone
  under a crane that a flying island left out under its water line on Altitude Market): both ends of a zipline, rope,
  cable or wire within 0.3 m of the top of a post, pillar or the land; a lantern, bucket, hook, net or ballast in the
  air with a holder right above it. On a flying island nothing may reach under the island's own sea level (h 0), or it
  is left out when it spawns: keep cranes and posts on the flat, back from the edge.
- **Nothing goes through anything** (the user, 2026-10-18: a grid of posts through The Isobar Club's pianos and pool
  table). A post, pillar, wall, floor or roof piece never passes through furniture, a prop, a chest or a tree's trunk.
  Place furniture between columns; under a big flat roof of floor plates give it deliberate columns clear of the
  furniture, or let `save`/`posts` stand its posts (they now move off things, or are left out).
**Test**: `CIRecipe <name>`, `CIFloating` in the editor (0 problems), then `CIPlay <name>`, and in a test world
`CIGroundIslands <name>`, `CIOverlapIslands <name>` and `CIOneSidedIslands <name>` (TEST_CATALOGUE §G, IG7/IG8/IG9 - the ground check also reports zipline and wire ends in the air and props that hang from nothing; a one-sided facade piece used as a wall needs a twin turned round, its face to the room). A new plan: both with `plans`.

## Every feature
- Update the docs in the same commit (README `## Documentation`):
  - The GUIDE section.
  - README: the `## Features` list, the feature's section, `## Installing`, `## Console commands (F10)`.
  - CHANGELOG: under the newest `## ` heading, in `### New` / `### Changed` / `### Fixed`, for what players or island makers see.
- Then rebuild `docs/Custom-Islands-Guide.pdf` from GUIDE.md. README names `guidepdf.ps1` in the project's tools; it is not in this repository.
- Renaming a GUIDE heading breaks its anchor. Fix the `## Contents` links and the `HelpLinks.OpenGuideSection` callers (`LibraryWindow.cs`, `WorldPlanWindow.cs`, `CreateWindow.cs`). `DevTestsHelp.cs` compares two of those anchors as literal text.
- Every new `.cs` file needs a `<Compile Include>` line in `DynamicIslands.csproj`.
- Run `powershell -ExecutionPolicy Bypass -File compile.ps1`. It must print BUILD OK.
- Run `compile.ps1 -Release`. It must also print BUILD OK, which means no mod code uses `DevTests`. Only `DynamicIslands.Start` reaches `DevTests.Init`, and only by reflection.
- Write the mod's files with `SafeFile.WriteAllText` / `WriteAllLines` / `WriteAllBytes`. A new settings file also needs a row in README `## Installing` and a mention in GUIDE `## 10. Settings files`.

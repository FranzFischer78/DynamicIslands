# World plans and Raft's story

Which custom islands a world gets, when and where (world plans and island rules), how plan islands join Raft's Receiver story, and the main story tabs in Raft's notebook.

## Files
| File | Role |
|---|---|
| `WorldDirector.cs` | `IntroRule` (one rule line), `WorldPlan` (a .plan file), `IslandCache` (settings of unspawned island files; `rulescache.txt`, `notbeforecache.txt`), `WorldDirector` (host: checks rules, brings and announces islands), `ChunkPointFitPatch`, `ReturningIslands` (left-behind islands come back) |
| `StoryChain.cs` | Plan islands in Raft's Receiver chain, mod frequencies (types from `ModTypeBase` = 100), chain state sent to players |
| `StoryOrder.cs` | Raft's story islands in order (`StoryOrder.Chain`, `Key`, `Parse`); world option `WorldOptions.StoryOrder` (story islands in a new order) |
| `QuestBook.cs` | Main story tabs and pages in Raft's notebook, built on every machine |
| `QuestBookPreview.cs` | World Plans' Preview notebook: a plan's story as `Moment`s, stepped with Back / Next |
| `MainStoryHelper.cs` | New main story helper; `Finish` turns its islands into rule cards |
| `WorldPlanWindow.cs` | Plan window (title WORLD PLAN; an island's own rules: `OpenIsland`, no tabs); `WorldPlanTemplates`. Tabs `TabStory` / `TabOther` / `TabSettings` (`SelectTab`, `ShowTabParts`, `StoryTab`); `MoveInStory` (the card's ▲/▼) rewrites the rule's `StoryPlace` (`first` / `after:<prev>`) and checks the result against `StoryChain.BuildSteps`; `CardFor` / `ShowTab` are for tests |
| `MenuWindows.cs` | Canvas `CustomIslandsMenuWindows` (sort 872) that builds `CreateWindow`, `WorldPlanWindow`, `MainStoryHelper`, `PlanCheckWindow`, `PlanProgressWindow`, `LibraryExportWindow`, `LibraryImportWindow`, `TextPromptWindow`, `ChoiceWindow` on the main menu, so a plan is made with no island loaded |
| `CreateWindow.cs`, `EditorEntry.cs` | Start window (EDITOR) and the ways between plan and island editor: plan card Edit island -> `EditorEntry.EditFromPlan`; `+ New island` -> `NewIslandFromPlan`; island editor Use in a plan... -> `UseInPlan` -> `AddToPlan` (Next in the Main story line = `StoryEnd` place, or Another island = start / ahead rule) |
| `ChoiceWindow.cs` | Modal pick-one list with search (islands, map types, plans) |
| `PlanChecker.cs`, `PlanCheckWindow.cs` | Check: `Finding`s (`Level.Problem` / `Warning` / `Tip`) and the report window. `PlayOrder` / `Stop` / `StopName`: the plan in play order (`StoryChain.BuildSteps`, then WHEN; km/day placed by `KmPerStoryIsland` / `DayKm`); `CheckTools`, `CheckStoryOrder`, `CheckStoryClash` walk it (CIPlanProgressOrder, CIStoryIdClash) |
| `PlanProgressWindow.cs` | Progression panel: `PlanChecker.Progression(plan)` in play order - Raft's story islands kept (`Step.Rule` -1, `Step.Raft`) at their places, each rule's blueprints, story items, tools (`Step.Tools`, `Step.ToolNeeds`), never given; per step `NeedsFrom` (a lock's story item kept from an earlier stop) and `Early` (a blueprint before its bases, `RaftFeatures.BuildsOn`; Check: `CheckBlueprintOrder`) (CIPlanBlueprints, CIPlanProgressOrder, CIPlanBlueprintOrder; the library plans: CIPlanLibraryProgression) |
| `RaftFeatures.cs` | Feature -> tools table (vines, sea vines, buried treasure, dirt, ziplines, cages for the bolt cutters; `When`-gated advisory zones: air -> oxygen/flippers, dark -> headlight), tools with `Also` variants, extra `Blueprints` and `Story` items (bolt cutters), `Researched`; `GivenIn`; `Has`/`LabelOf` for "a/b" needs; `BuildsOn` (blueprints that build on others) |
| `WorldIslands.cs`, `IslandPickerWindow.cs`, `IslandThumbs.cs` | Spawn-pool entries left out of a world, gap between random islands; the list in the New Game box (rows: tick box, picture from `IslandThumbs` / library / `RaftIslands.Thumbnail`, QUEST badge, description and the small print's `IslandDifficulty.Label` from `IslandSummaries`; filled by two coroutines after the rows are built) |

## Main flow
1. New Game box (`NewWorldOptions.cs`) sets `WorldDirector.PendingPlan`; `WorldIslands.Chosen` holds the entries left out (unticked).
2. Load: `IslandWorldState.OnWorldLoaded` calls the state classes' `Reset`. Host only: it reads the world file (`WorldCopy.Choose`), passes each `@key=value` line along a chain of `ReadLine`s, then calls `StoryChain.OnWorldRead`, `WorldRules.OnWorldRead`, `WorldDirector.OnWorldLoaded`. With no lines (clients) only the last two run. A brand-new world is caught by `WorldDirector.CheckNewWorld` (Raft raises `SaveAndLoad.LoadComplete` only for saved worlds).
3. `WorldDirector.OnWorldLoaded` (returns on clients): a new world gets `SetPlan(PendingPlan ?? DefaultPlan, true)` and `StoryChain.FromPlan`; a saved world plays its stored copy (`@planrule=` lines) unless `PlanFileToPlay` returns the .plan file.
4. `WorldDirector.Tick` (each second, host): `UpdateVisits`, `Evaluate`, `StoryChain.Tick`, `ReturningIslands.Tick`, `QuestMilestones.Tick`.
5. `Evaluate` -> `TryRule` -> `Met` / `Happened` -> `Bring` (`FileFor`, `Place`, `IslandWorldState.Add`, spawn) -> `Announce` (`IslandNetwork.SendAnnounce`, `Show`). A plan rule then goes into `Done`; an island rule sets state key `RuleKeyBase + i` on its island.
6. `IntroRule.Special` rules (`InStory` = place `first` / `after:` / `instead:`, or WHERE `receiver` / `sailing`) are skipped by `Evaluate`. `StoryChain.Tick` waits until an `InStory` step is unlocked, then `Fire`s it when `Met`, brings it (`TryBring`; a `receiver` rule only from `OnTuned`), and calls `MarkDone` when `IsDone`; `MarkDone` unlocks the next step (`Unlock`).
7. `StoryChain.Changed` -> `Rebuild`, `Broadcast`, `ChainChanged`. `QuestBook.Tick` (every 0.5 s, every machine) runs `Apply` when `Signature()` changed.
8. After Raft's `SaveAndLoad.SaveWorld`, `IslandWorldState.Save` (host) writes `WorldDirector.WriteLines`, `WorldIslands.WriteLines`, `StoryChain.WriteLines` with the rest.

## Data and keys
- Plan file `Mods\DynamicIslands\plans\<name>.plan` (`WorldPlan.Folder`, `PathFor`, `Load`, `Parse`, `ToText`, `Save`). Keys: `description`, `random`, `story`, `storyleaveout`, `storyending`, `endingtitle`, `credits` (the ending's credits, RF2: `StoryCredits.cs`), `rule`, `modversion`; `#` = comment; unknown lines and unreadable rules go to `Kept` and are written back. Built-ins `WorldPlan.RandomName`, `NoneName` have no file. No sample plans since 2026-10-16 (`EnsureSamples` and the 4 sample templates removed); tests write their own `CI ...` plans (`DevTests.WriteCIPlan` / `RemoveCIPlan` / `CIPlanOf` in `DevTestsLeftovers.cs`).
- Rule line (`IntroRule.ToLine` / `Parse`): `id | what | when | where | message | label | story place | done when | tab title | tab colour | tab intro`. `WhatKinds` island, type, pool, oneof; `WhenKinds` start, km, day (`km:N:again` / `day:N:again` = `IntroRule.Again`: `ReturningIslands.Why` "again" until `Finished`, `TickAgain` every N days/km, no cap), quest, step, zone, visit, rule, signal; `WhereKinds` ahead, near, receiver, sailing; `DoneKinds` quest, visit, step, zone, signal, note. Story place `first`, `after:X`, `instead:X`, `beside`. An island's own rules: setting `bring.rules` (`WorldDirector.IslandRulesKey`).
- World file `Mods\DynamicIslands\worlds\<WorldGuid>.txt`. Director: `@plan=`, `@sailed=`, `@done=`, `@planfrom=`, `@planowner=`, `@planid=` (the plan's `id =`; a file with another id never replaces the world's copy), `@planhash=`, `@planrandom=`, `@plandesc=`, `@planrule=` (`@savedby=` is read through `WorldCopy`). Chain: `@storychain=`, `@storyrule=`, `@storyending=`, `@storyendtitle=`, `@storycredits=`, `@storycreditsshown=` (credits shown once, `StoryChain.MarkCreditsShown`), `@storybeside=`, `@storyfreq=`, `@storyunlocked=`, `@storydone=`, `@storyfired=`, `@storybrought=`, `@storydue=`. Islands: `@islandsoff=` (`|`-separated), `@raftgap=`, `@raftgapcount=`, `@playtime=`. Island line `name|x|y|z|state|rule|label|hash` (`rule` = `Entry.Rule`).
- Island state keys: `WorldDirector.VisitKey` 0x50000, `RuleKeyBase` 0x50001 + rule index, `ReturningIslands.ReturnsKey` 0x50F00, `ReturningIslands.AgainKey` 0x50F01 (an again island's clock: Day / Yield = metres sailed when last near).
- Network (`IslandNetMessage`): `Announce` 8, `StoryChain` 19 (also carries banners in `Name`), `QuestCount` 20; clients read the host's plan from the host's world file (`WorldCopy` 18, `WorldCopy.HostLines`).
- `spawnpool.txt`: `defaultPlan`, `returnMinutes`. `world_rules.txt` keeps the last New Game choice: `islandsoff=`, `raftgap=`.
- Commands: `WorldPlan [name]` (`DynamicIslands.WorldPlanCommand`), `StoryChain`, `WorldIslands`, `WorldIslandsGap`.

## Where to change X
| Task | Edit |
|---|---|
| New WHEN kind | `IntroRule.WhenKinds`, `Parse`, `ToLine`, `DescribeWhen`; `WorldDirector.Met` / `Happened`; `PlanChecker.CheckWhen`; `WorldPlan.Help` |
| New WHERE | `IntroRule.WhereKinds`, `Parse`, `ToLine`; `WorldDirector.Place`; `PlanChecker.CheckWhere` |
| Chain done when | `IntroRule.DoneKinds`, `NormalDone`; `StoryChain.IsDone` |
| Chain order | `StoryChain.BuildSteps` |
| Play order (Check, Progression) | `PlanChecker.PlayOrder` (`KmPerStoryIsland`, `DayKm`) |
| Which tool a feature wants | `RaftFeatures.Features` / `RaftFeatures.Tools` |
| Notebook tabs and pages | `QuestBook.Describe`, `BuildIsland`, `MakeTab`, `Apply`, and `Signature` |
| Check findings | `PlanChecker.Check` and its `Check*` methods |
| New plan file key | `WorldPlan.Parse`, `ToText`, `Help`; the world's copy in `WorldDirector.ReadLine` / `WriteLines` |
| Random-island pool | `WorldIslands.TakesPart`; `CustomIslandSpawner.PlanIslandNames` |

## Traps
- `Progress.Steps` holds Raft's story islands too (`Rule` -1): filter `Rule >= 0` before indexing `plan.Rules`.
- Story items found on Raft's own story islands never count for the plan's locks (`CheckStoryOrder` skips Raft stops).
- Host only: `WorldDirector.Tick` returns on clients after `CheckNewWorld`; `StoryChain.FromPlan`, `Tick`, `OnTuned` act only on the host. Clients get the chain in `StoryChain.OnMessage` (main story rules in `clientBook`, read through `BookRules`).
- `WorldDirector.SetPlan` does not touch the chain: call `StoryChain.FromPlan` too (as `WorldPlanCommand` and `OnWorldLoaded` do).
- `PlanFileToPlay` returns the .plan file only when it differs from the stored copy and is the same plan: same library/import source (`PlanFrom`), or this PC is `PlanOwner`, and the file's `Id` matches `PlanId` when both have one (`Save()` gives own plans an id; `Copy...` makes a new one). Plan islands are found by content hash (`PlanHashes`, `FileFor`), not only by name.
- `Done`, `StoryChain.Fired` and `Brought` remember rule ids. `ForgetChangedRules` re-arms a done id that now brings another island; `WorldPlanWindow.NewRuleId` makes fresh ids; `IntroRule.CleanId` turns `| : , ;` into `-`.
- `WorldDirector.Done` also holds `QuestMilestones.NearMark` / `AllMark` (`milestone:90`, `milestone:100`).
- An island rule's fired flag is `RuleKeyBase` + its index in `bring.rules`: inserting or reordering rules shifts it (`QuestEditorWindow.SetQuestBringRule` inserts at 0).
- World file reading: the first `ReadLine` that returns true takes the key; an `@` line nobody takes is kept and written back. `WorldRandomizer`, `WorldOptions`, `WorldIslands`, `StoryChain`, `WorldDirector` get the key lowercased, the others as written. A new state class needs `Reset`, `ReadLine`, `WriteLines`, and its `HasState` in the nothing-to-save check of `IslandWorldState.Save`.
- Harmony: `NoteBook.UnlockFrequency` has two prefixes, `StoryChainUnlock` (skips Raft's call while `StoryChain.Active`; the chain's own calls set `StoryChain.Bypass`) and `StoryOrderUnlock` (`StoryOrder.Active` is false while the chain is active). `NoteBookUI.UpdateAllNotesVisibility` has postfixes from `StoryOrder` and `QuestBook`. Others: `ChunkManager.AddChunkPointForcibly` (`StoryChainReceiver`), `RecieverFrequency.InitializeAllFrequencies`, `RGD_RecieverFrequencies.RestoreFrequencies`, `NoteBookUI.FlipToPageLocally`, `FrequencyTextMeshPro.Start`, `FrequencyTextMeshProUI.Start`, `ChunkManager.DoesPointFit`.
- `QuestBook` redraws only when `Signature()` changes. Its notes use indexes from 20000 and never enter Raft's unlock list. Page numbers must match on every machine so Raft's page flips land on the same page.
- C# 7.3: no switch expressions, `??=`, ranges or `using var`.
- The plan window's tabs: Main story line = rules with a story place, in `StoryChain.BuildSteps` order (Raft's islands as grey lines); Other islands = the rest; Settings = switches and description. A card's STORY row moves it between tabs.
- Docs: a format change goes into `WorldPlan.Help` (written at the top of each .plan) and docs/GUIDE.md `7.7 The plan file`; island rules are GUIDE `6.4`, the Receiver story `6.5`; a new file gets a line in README.md `### Source overview`.

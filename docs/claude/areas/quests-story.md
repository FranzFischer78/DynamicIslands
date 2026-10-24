# Quests and story content

What players do on an island: quests, behaviours and events, trigger zones, notes, chests, story items and the journal, Raft-style pieces (doors, levers, code locks, quest item pickups, buried treasure, ziplines), and atmosphere and sound zones.

## Files
| File | Role |
|---|---|
| `Quest.cs` | `IslandQuest` (a quest in island settings), `QuestTracker` (progress, quest panel, rewards) |
| `QuestEditorWindow.cs` | Quest editor (Island tab), also the island's when-the-quest-is-done bring rule |
| `StoryItems.cs` | `StoryItemDef`, `StoryItems`, `StoryBook` (crew's items and journal pages), `QuestCount`, `NoteCount`, `WorldProgress`, `QuestMilestones`, `JournalWindow` |
| `StoryItemsWindow.cs` | Story items editor; `StorySets` (`LockedDoor`, `TreasureMap`, `LockedChest`, `NoteTrail`) |
| `Behaviours.cs` | `BehaviourProps` (keys), `ObjAction`, `ObjCheck`, `IslandBehaviour` (movers), `SharedClock`, `UseInteract` (interact key; `CodeLock.Use` when `lock.code` is set), `Behaviours` (events at run time) |
| `BehaviourWindow.cs` | Behaviour & events (object) and Island events editor |
| `TriggerZone.cs` | Zone: message, items, quest `reach`, `enter` event, air pocket, hazard tick |
| `GapFeatures.cs` | Hazards (zone.hazard/damage/safe), DarknessOverlay, GapChecks (wears/holds), zap, repel, `hit` action, BossShieldPatch; UI in `ObjectInspectorGaps.cs` |
| `LootCrate.cs` | `ContentState` (used state: shared, saved, refilled) and `LootCrate` (chests) |
| `CustomNote.cs` | `CustomNote` (readable note) and `NoteReader` (the note on screen) |
| `IslandInfo.cs` | `IslandProps`, `IslandSettings`, `IslandRules.RegrowDays`, `IslandInfo` (arrival banner, zone messages) |
| `LaserBeam.cs` | `beh.laser=beam` on an object adds it (Behaviours.Attach): traces every 0.1 s, mirrors (`beh.laser=mirror`) turn it along their flattened forward; on the host the newly reached object gets event `laser` via `FireFromHost`. Action `character` (Behaviours.RunPersonal: `CharacterOf` by index or displayName, then `CharacterManager.UnlockCharacterByIndex` - changes the player's profile, tests never run it). Boss arena: MapTypes.Boss (`ArenaStake`, `ArenaGate`, `BossSignal`) |
| `TriggerSounds.cs` | Shared verbs `playsound`/`stopsound` (RF1): `HostRun` -> Moment "sound"/"stop"; FMOD instances per machine while `Behaviours.Near`; loops kept in `e.State` key `KeyBase` 0xD0000 + index (the same base as `Survivors.KeyBase`: an overlap in the code) (Yield = `Hash(arg)`, found again by `FindArg`); `Tick` from `DynamicIslands.Update` |
| `CameraMoment.cs` | Shared verb `camera` (RF2): Target = marker, Arg = `look|seconds|range`; Moment "camera"; each machine in range: glide 0.8 s, hold, glide back; MouseLook + PersonController disabled; Esc = `Skip` (closes the pause menu) |
| `StoryCredits.cs` | A plan's ending credits (RF2): host `Tick` sees `QuestBook.StoryOver` go false -> true, `HostShow` (Moment "ending", `StoryChain.CreditsShown`); UI canvas sort 520 |
| `ReadyPieces.cs` | Default settings for Raft's doors, hatches, levers, lifts, cages, cameras, generators, radios, engine, and the puzzle pieces (mirrors, water wheels, pipes turn: `Turns(kind)`; wire connectors: `state||open` or take the cable, then open; claw, scales); a kind's signal is its name |
| `CodeLock.cs`, `QuestItemPickups.cs` | Keypad lock (`lock.code`); Raft's quest item pickups give story items `raft-<type>` |
| `QuestRewards.cs` | Each player's share of a quest's Raft-item reward |
| `BuriedTreasure.cs`, `ZiplineEnds.cs` | Raft treasure points (extra loot `treasure.loot` given on pickup by a Pickup patch, once; story loot kept dug via ContentState key `KeepBase` 0xF0000+n; `OldKeepBase` 0xD0000+n still read for older saves); zipline far end (`zip.to`); the tools each needs: `RaftFeatures.cs` (also the vines, dirt) |
| `KeycardDoor.cs` | Key/keycard door settings: the story check in `if.use`, used up = a `take` check instead of `has`; inspector card list; Check warns when no story item opens one |
| `AmbienceZones.cs` | `AtmosphereZone`, `SoundZone`, `SoundLibrary` |

## Quests: how steps are defined
- Settings `quest.title`, `quest.intro`, `quest.steps`, `quest.reward`, `quest.done`; further quests `quest2.*` to `quest9.*` (`IslandQuest.Key(key, n)`, `MaxQuests` = 9, n = 0 is the main quest). Read/write with `IslandQuest.From` / `To`.
- `quest.steps`: one line per step, `type|target|count|text`. `IslandQuest.Types`: `reach` (zone id), `read` (note title), `open` (the chest's note title; empty = any), `kill` / `catch` (creature `Kind.Label`), `collect` (story item, `story:<id>` or id), `pages` (empty = this island, `all` = any). `collect` and `pages` are `Counted`. Empty target matches anything.

## Main flow
1. On the acting machine: `TriggerZone.Enter` -> `QuestTracker.Event(e, "reach", Id)`; `NoteReader.Show` -> `read`; `LootCrate.Open` -> `open`. The host's `CreatureSpawner` -> `kill` / `catch`.
2. `QuestTracker.Event` tries every quest of the island: a match on the current step adds progress; a match on a later step is kept (`Remember`, credited by `CreditEarly`).
3. `QuestTracker.Set(e, n, step, progress, send)` stores it and sends `QuestStep` (n = 0) or `QuestStepMore` (n > 0). A client with `IslandNetwork.HostAddsCounts` sets its own view and sends only its amount (`Name` = `add`); the host adds it in `AddFromPlayer` and tells everyone, who call `QuestTracker.Apply`.
4. `collect` / `pages` steps advance in the host's `CheckCounted` (from `QuestTracker.Tick`), only while a player is near. Per island copy: page keys use `StoryBook.PageIsland(e)`; story items record their source copy (`Held.From`, saved as the 8th `story.item` field) and `Found` subtracts `FoundOnOtherCopies`.
5. Last step: `Completed` -> story items once, on the host (`StoryBook.Give`); Raft items per player via `QuestRewards.OnCompleted` (or later `Collect` in `Tick`). Main quest only: `QuestTracker.Advanced` -> `Behaviours.OnQuestAdvanced` -> on the host, when done, `FireFromHost(e, -1, "quest")`.
6. World plans read it: `WorldDirector.Happened` (`quest`, `step`) and `StoryChain.IsDone`.
7. Behaviours: `Behaviours.Fire` -> checks (`Passes`; failing, `Otherwise` runs the `else.` actions as event `<ev>!`) -> `Run`: personal part on the acting machine (`Schedule` -> `RunPersonal`), shared part on the host (`RunShared`; a client sends `EventFired`). `wait` splits actions into parts.

## Data and keys
- Object settings: `obj.name`, `beh.*`, `col.mode`, `on.<event>` (actions `verb|target|argument`), `if.<event>` (checks `kind|target|argument`, `!kind` = NOT, a line `any`), `else.<event>`. Events `use`, `enter`, `read`, `open`, `defeat`; island events `arrive`, `quest`. Lists in `BehaviourProps` (`ObjectEvents`, `IslandEvents`, `Verbs`, `SharedVerbs`, `CheckKinds`).
- Other settings: `story.items` (`id|name|icon|description`, and `|raft=<Raft item>` last for T8 take-out), `note.title` / `note.text`, `loot.items` / `loot.refill`, `zone.id` / `zone.radius` / `zone.message` / `zone.repeat` (`never` = once ever) / `zone.air`, `creature.zone`, `lock.code`, `zip.to`, `info.title` / `info.author` / `info.description`, `rules.regrow`, `rules.levels`, `atmo.*`, `sound.event` / `sound.volume` / `sound.mode`.
- Island state (field 5 of the world file's island line, sent to joining players): `ContentState.LootKeyBase` 0x20000 + chest ordinal; `TriggerZone.KeyBase` 0x30000 + zone ordinal; `QuestTracker.StepKey` 0x40000, `ProgressKey` 0x40001, `EarlyKeyBase` 0x40100 + step (quest n > 0: step 0x40200 + 2n, progress 0x40201 + 2n, early 0x40300 + 0x40 n + step); `Behaviours.StateBase` 0x60000 + object (shown / open), `DoneBase` 0x70000 + object (`IslandIndex` 0xFFFF = the island), `SharedOnceBase` 0x80000, `PendingBase` 0x900000, `SignalBase` 0x1000000; `CodeLock.LockKeyBase` 0xA0000; `QuestItemPickups.KeyBase` 0xB0000; `TraderRaft.KeyBase` 0xC0000; `Survivors.KeyBase` 0xD0000 + cage object (opened), and `TriggerSounds.KeyBase` 0xD0000 + object too (looped sounds: the same range, an overlap in the code); `BossPhases.KeyBase` 0xE0000 + creature spot object (fight in its second phase); `BuriedTreasure.KeepBase` 0xF0000 + treasure (dug for good). Below `CreatureSpawner.StateKeyBase` 0x10000: trees and pickups.
- World file: `@story.item=` (id|count|name|icon|description|found|raft) and `@story.page=` (key|day|title|island|text), fields URI-escaped. Page keys: `note:<island>:<object>`, `act:<island>:<object>:<title>`, `storyfreq:<step>`; `<island>` = `StoryBook.PageIsland(e)`.
- `Mods\DynamicIslands\worlds\<WorldGuid>-<steam id>.rewards`: this machine's `rewarded` / `owed` lines (`QuestRewards`), never sent.
- Network: `ObjectUsed` 6, `QuestStep` 7, `ObjectSet` 9, `EventFired` 10, `Story` 11, `Claim` 16, `QuestCount` 20, `QuestStepMore` 25.

## Where to change X
| Task | Edit |
|---|---|
| New quest step type | `IslandQuest.Types`, `Step.Describe`; call `QuestTracker.Event` where it happens (or `Counted` + `QuestTracker.Found`); `QuestEditorWindow.TypeLabels`, `TypeHints`, `TargetHints`, `NamesFor`; `PlanChecker.CheckQuest` |
| New action verb | `BehaviourProps.Verbs` (`SharedVerbs` if shared), `ObjAction.HasTarget` / `HasArg` / `Describe`; `Behaviours.RunShared` or `RunPersonal`; `BehaviourWindow.VerbLabels`, `VerbHints` |
| New check kind | `BehaviourProps.CheckKinds`; `Behaviours.Holds`; `ObjCheck.Describe`; `BehaviourWindow.CheckLabels`, `CheckHints` |
| Chest, zone, note use | `LootCrate.Open`, `TriggerZone.Enter`, `NoteReader.Show`; refill and re-arm in `ContentState.OnIslandReady` |
| Story items, journal | `StoryBook.Give`, `Take`, `AddPage`, `Apply`; `JournalWindow` |
| Take out as a Raft item (T8) | `StoryItemDef.RaftItem`, `StoryBook.CanTakeOut` / `TakeOut` (client: Story `out` -> host `outok`/`outno` via `IslandNetwork.SendStoryTo`); `JournalWindow.RefreshTakeOut` |

## Traps
- Host/client: the acting machine does the personal part (message, Raft items, sound, teleport); the host does shared actions, gives story items once, keeps quest totals, notices kills and catches, and runs `FireFromHost` (`quest`, `defeat`). A chest, a once-zone, or a `read` / `arrive` with a `take` check or `give` / `teleport` asks `Claims.May` first. `StoryBook` changes go to the host, which sends the whole state (`Name` = `all`).
- `read` and `arrive` fire once per world (`DoneBase` key); later reads run messages and sounds only. On the host a shared part runs once per `read` / `arrive` (`SharedOnce`) and once per second for other events.
- `FireFromHost` drops `has` / `take` checks of Raft items (no player did it); use story items there.
- `QuestTracker.Advanced`, `on.quest`, the `quest` check and the WHEN `step:` use the main quest only; `quest:<ref>:<n>` rules use `QuestTracker.IsDone(e, n)`.
- `QuestTracker.Set` never moves back; a stale message returns false and the host does not pass it on.
- Steps and checks match by name (zone id, note title, creature `Kind.Label`; case-insensitive): renaming an object breaks them. Zone and chest keys use their ordinal in file order.
- A chest holding story items never refills (`LootCrate.Attach`).
- The quest editor reads `IslandQuest.From(props, n, true)` (raw); without `raw` texts are made plain for showing.
- Editor tables (`TypeLabels`, `VerbLabels`, `CheckLabels` and their hints) are indexed directly: a new type, verb or kind without an entry throws.
- `PendingKey` packs the event number in 4 bits (`ObjectEvents` + `IslandEvents`, the `!` variants after them): new events renumber saved pending keys, and more than 8 events overflow.
- Atmosphere and sound zones act per machine only (no state, no network).
- C# 7.3: no switch expressions, `??=`, ranges or `using var`.
- Docs: docs/GUIDE.md `5.2` to `5.5`, `6.1 Quests`, `6.2 Behaviour and events`, `6.3 Story items and story sets`; README.md `### Source overview`.

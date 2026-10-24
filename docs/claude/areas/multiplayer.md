# Multiplayer
The host owns the world: its island list, world file and settings. Clients copy them, fetch island files they lack, and send what their player does to the host, which decides and passes it on.

## Files
| File | Role |
|---|---|
| `IslandNetwork.cs` | `IslandNetMessage` (one `[Serializable]` class for every kind) and `IslandNetwork`: send helpers, `OnMessage`, join reply, island list, file transfer, `Resync`, `HashOf` |
| `DynamicIslands.cs` | `OnNetworkMessage` (RML) -> `IslandNetwork.OnMessage`; `HookRaftEvents` re-hooks `Raft_Network.OnWorldReceivedLate`; `Update` -> `IslandNetwork.Tick`; console command `Resync` (`ResyncCommand`) |
| `Claims.cs` | Things only one player can have (a chest's loot, a once-zone) |
| `PlayerTrust.cs` | CB2: host takes a player's used/harvest/quest/event report only within `Allowed(e)` (unload distance + land radius + 150 m) of the island (`MayMark`); `ForTests` (set by CI world commands) skips it |
| `WorldCopy.cs` | The world file sent to every player, so any of them can host later |
| `WorldSaveShare.cs` | Raft's newest save folder (`<date>-Latest`) packed+deflated, sent after each Raft save (`WorldCopy.AfterSave` -> `AfterRaftSave`, a frame later) and at a join; the player files it under `SaveAndLoad.WorldPath` (marker `CustomIslands-world.txt` = world guid; another world of that name -> `<name> (<guid8>)`; `.part` then move; old `-Latest` renamed; received folders pruned to 3) |
| `WorldRules.cs` | Kind 15, and the host's spawnpool.txt settings every player shares |
| `VersionMismatch.cs` | `MismatchText` for a version difference (no online update check: removed 2026-10-17, raftmodding.com is the only source) |

## Main flow
1. Client: `Raft_Network.OnWorldReceivedLate` -> `IslandNetwork.OnWorldReceived`: resets `WorldRules`, `WorldOptions`, `StoryChain`, `WorldCopy`; empties the island list.
2. `IslandNetwork.Tick` sends `SyncRequest` (`Name` = `"version:"` + `LibraryPack.ModVersion`) every `SyncRetrySeconds` (5 s); after `SyncMaxTries` (12) every 30 s, with a notice.
3. Host, `OnMessage` case `SyncRequest`: `CompareVersions`; replies `SyncRequest` with its version and `HostCapabilities` (`"counts,spots,events"`); then `JoinPart` each, in order: `WorldRules.Message`, `IslandsMessage(..., true)`, `StoryBook.StateMessage`, `WorldOptions.Message`, `DailyQuest.Message`, `UpgradeTable.Message`, `RogueShark.Message("state")`, `StoryChain.Message`, `QuestCount.Message`, `CreatureSpawner.SpotsMessage(null)`, `WorldRandomizer.Message`, `PlayerPlaces.PlaceMessage`; then `PlayerLevels.StateFor` -> `SendLevels`, `WorldCopy.Send(to)`, `WorldSaveShare.Send(to)`, `AskPlayers(to)` when files are wanted.
4. Client `ReceiveIslands`: each island at `FromHost` (x,z from its own raft, the host's y); a known island more than 5 m off is moved. A `FullList` drops islands not in it and sets `synced`. New entries -> `ResolveFile`.
5. `ResolveFile`: the same-name file if its hash matches; else `<name>_<hash>.island`; else a copy of any `*_<hash>.island`; else `FileRequest` and `Entry.WaitingForFile`.
6. Host `SendFile` -> `SendChunks`: `FileChunk`s of `ChunkBytes` (3000) in base64, `ChunksPerFrame` (6) a frame. `ReceiveChunk`: only hashes in `requested`; checks `Hash`; saves `<name>_<hash>.island`; clears `WaitingForFile`, so streaming spawns it.
7. Afterwards: `BroadcastAdded`/`BroadcastRemoved`; `WorldRules.Broadcast` (also from `WorldRules.OnPoolChanged`), `WorldOptions.Broadcast`; `WorldCopy.Send(null)` after each save.
8. Claims: `LootCrate`, `TriggerZone`, `Behaviours` call `Claims.May(entry, key, then)`. Host: `HostGrant` (not `ContentState.IsUsed`, not held by another) holds it `HoldSeconds` (6 s). Client: `SendClaim`, host answers `Count` 1/0. No answer in 3 s: with `HostAnswersClaims` it asks again every 5 s up to 30 s, else (an older host) it goes ahead. A grant older than `FreshAnswerSeconds` is asked again.
9. Retries: `RetryLateFiles` asks again for a file with no new chunk in `FileRetrySeconds` (30 s), keeping parts that came. `Resync` clears `synced`, `requested`, `incoming`; the next `Tick` asks for the whole join reply.
10. Host lacks a file (a world from another host): `WorldCopy.LocalFileFor` -> `WantFromPlayers`; `FileWanted` now, to each joiner and every 30 s; a player who has that content answers with `FileChunk`s (`SendFile(..., quiet)`).
11. Versions: `CompareVersions` on both sides, text from `VersionMismatch.MismatchText`; a `FullList` with no version reply first means an older host (`VersionMismatch.Older`). The mod never asks the internet for a newer version (CIUpdateCheck guards that).

## Data and keys
Fields: `Ids`, `Names`, `Hashes`, `Offsets`, `States`, `Labels`, `Rules`, `FullList`, `Name`, `Hash`, `Index`, `Count`, `Data`. "Via host": a client sends to the host, the host applies it and sends it to everyone.
| Kind | Direction | Carries |
|---|---|---|
| 1 `Islands` | host -> clients | per island `Ids`, `Names`, `Hashes`, `Offsets` (3 floats), `States`, `Labels`, `Rules`; `FullList` |
| 2 `Remove` | host -> clients | `Ids` |
| 3 `SyncRequest` | both | `Name` version; host's reply `Data` = capabilities |
| 4 `FileRequest`, 5 `FileChunk`, 23 `FileWanted` | see steps 5, 6, 10 | `Name`, `Hash`; chunks `Index`/`Count`/`Data` |
| 6 `ObjectUsed` | via host | `Ids[0]` island, `Index` state key, `Count` day (host sets its own day; below 0 = available again, host only) |
| 7 `QuestStep`, 25 `QuestStepMore` | via host | `Ids` island (+ quest number), `Index` step, `Count`; `Name` "add" = a player's amount |
| 8 `Announce`, 9 `ObjectSet` | host -> clients | banner `Ids[0]`/`Name`/`Data`/`Offsets`; object `Ids[0]`, `Index`, `Count` bits |
| 10 `EventFired` | both | `Ids[0]`, `Index` object, `Name` event, `FullList` = from host, `Count` 1 = waits / answer |
| 11 `Story`, 14 `Levels` | both | `Name` = sub-kind (`StoryBook.OnMessage`, `PlayerLevels.OnMessage`), `Data` |
| 12 `PlayerPlace` | host -> joiner | `Ids[0]`, `Offsets` from the island |
| 13 `Randomizer`, 17 `WorldOptions`, 19 `StoryChain`, 20 `QuestCount` | host -> clients | `Data` (and `Name`) from that class's `Message` |
| 15 `WorldRules` | host -> clients | `Index` monsters, `Count` build cost %, `Data` `receiver=;unload=;regrow=;rdist=`, `Name` refunds, `Names` the rescued survivors (`Survivors.FromHost`) |
| 16 `Claim` | both | `Ids` island and question number, `Index` key, `Count` 1/0 |
| 18 `WorldCopy`, 24 `WorldCopyNewer` | host -> clients; client -> host | `Name` world guid, `Hash` stamp, parts `Index`/`Count`/`Data`; newer: `Data` `savecount;savedat` |
| 26 `DailyQuest` | both | `Name` "state" (host's task, `Data`), "add" (a player's part: `Index` day, `Count`), "got" (reward taken) |
| 27 `RogueShark` | host -> clients | `Name` "spawn"/"dead"/"state", `Index` the rogue's object index, `Count` no new one before this day |
| 28 `WorldSave` | host -> clients | `Name` world guid, `Hash` save folder name, parts `Index`/`Count`/`Data` (base64 of the pack, 3000 chars) |
| 29 `UpgradeTable` | host -> everyone; client -> host | `Name` "state"/"learned" from the host, "research"/"learn" to it |
| 33 `Moment` | host -> clients | when/then moments: `Name` "sound"/"stop" (`TriggerSounds`), "camera" (`CameraMoment`), "ending" (`StoryCredits`), "ufo" (`UfoSightings`, Data = the saucer's path, no Ids); `Ids[0]` island, `Index` object, `Data` |
| 34 `VendingHatch` | host -> clients | a vending hatch paid or emptied: `Ids` island, state key, button + 1 (0 = empty); `Count` day |
| 35 `StormSeas` | host -> everyone | `Name` "broke", `Index` the storm, `Count` floors broken |
| 36 `WhaleBreach` | host -> clients | a whale breach (option `whalebreach`): `Index` its number, `Offsets` { dx, dz from the raft, heading, roll side }; `WhaleBreach.OnMessage` plays it by the client's raft |
| 37 `Kraken` | host <-> clients | the kraken arm (option `kraken`): `Name` rise/hits/let/tear/away (host -> clients) or hit (client -> host, `SendToTheHost`); rise `Data` `block;side;seed;need;stay`, `Count` hits, `Offsets[0]` elapsed; tear `Count` floors torn; also a join-reply part |
| 38 `AlphaHealth` | host -> clients | an animal turned alpha (Big Bruce too), sent once by `CreatureSpawner.SendHostHealth` after `ApplyStats`: `Index` object index, `Offsets` { max, health }; `CreatureSpawner.HostHealth` sets it exactly (or keeps it 60 s for `MatchHealth` when the animal isn't there yet) |
| 21 `CreatureSpots`, 22 `ObjectHarvest` | host -> clients; via host | `Data` `objectIndex:islandId:spot;...`; `Ids` island, active, yield, `Index` ordinal, `Count` day (host sets its own) |

## Where to change X
- New kind: a const in `IslandNetMessage` (1-29 and 33-38 are taken, 30-32 unused; the next free one is 39), a send helper in `IslandNetwork`, a `case` in `IslandNetwork.OnMessage`.
- State a joining player needs: the `JoinPart` list in `OnMessage` case `SyncRequest`, plus a broadcast on change.
- Island list fields: `IslandsMessage` and `ReceiveIslands`. Files: `ResolveFile`, `SendFile`, `ReceiveChunk`.
- One-player-only things: `Claims.May`, `Claims.HostGrant`. Shared settings: `WorldRules.HostSettingsData`, `HostSettingsFrom`.

## Traps
- The host decides. Copy the pattern `if (Raft_Network.IsHost) SendToClients(msg); else if (InMultiplayerGame || Loopback != null) SendToHost(msg);`; the host checks, applies, then passes on. A client's day or "available again" is not trusted (`ObjectUsed`, `ObjectHarvest`). A new player report about an island goes through `PlayerTrust.MayMark` on the host. Levels: only the host turns them on; a client's "mine" record over what the host credited (hits, shared kills, `CreditXp`) is clamped and sent back as "state" Count 2.
- Older versions ignore an unknown `Kind`. Don't change what an existing kind means: add a kind (as `QuestStepMore` did). A host behaviour clients rely on goes in `HostCapabilities`; clients reset those flags at the first `SyncRequest`.
- The version reply uses only fields every version has, so an older player can read it; it goes before every other join part.
- Island lists and banners carry x,z from the sender's raft (`CustomIslandSpawner.RaftPosition`) plus absolute y, never world coordinates. Clients drop `Islands` before `worldReceived`.
- `Entry.Id` is the host's. A client may have the island unloaded (`Root == null`): keep state in `Entry.State`, apply on load.
- A new island is announced only once its file exists: `IslandsMessage` skips failed entries and ones with no hash; `TrySpawn` adds generated islands with broadcast off and `GenerateAndSpawn` calls `BroadcastAdded` after the file's `Save`.
- Shared settings: read `WorldRules.UnloadDistance`, `RegrowDays`, `ShowOnReceiver`, `ReceiverDistance`, not the `CustomIslandSpawner` fields.
- Client copies of host state must be reset in `IslandNetwork.OnWorldReceived` or the world-load `Reset`: the next host differs.
- Names and hashes from another PC become file paths: check `IslandNetwork.ReceivedProblem` / `IsHash` (`FileNames.ReceivedProblem`).
- Mod messages and Raft's RPCs (`NetworkChannel.Channel_Game`) keep no order between them: `CreatureSpots` goes before the animal's `Message_CreateAINetworkBehaviour`.
- Big data goes in parts (3000 a part), a few a frame. Each join part is sent on its own (`JoinPart`) so one failure doesn't drop the rest.
- Tests: `IslandNetwork.Loopback` catches sent messages in single-PC dev tests (`DevTests*.cs`); the two-player test helpers (`CIMPState`...) are in `DevTestsMultiplayer.cs`.

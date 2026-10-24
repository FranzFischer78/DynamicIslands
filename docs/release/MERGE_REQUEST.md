# Custom Islands 3.0 (alpha): rebuilt for Raft 1.1

## Summary

This brings Custom Islands from **v1.1.1** (the last release on raftmodding.com, 2022) to **version 3.0**, a rebuild
for **Raft 1.1 (Unity 2021.3)** done in September-October 2026 by SwedenJohansson on top of FranzFischer78's code and
MegaMatrixs' design. v1.1.1 no longer compiles on current Raft; 3.0 compiles and runs again, and grows the mod from
"load islands built in Unity" into an in-game island editor with quests, world plans, multiplayer and a shared
island library.

3.0 is released as an **experimental alpha**: the main menu says so in a box, and the docs ask players to back up
their worlds. The full, dated list of changes is in [`CHANGELOG.md`](../../CHANGELOG.md); the player-facing
documentation is the illustrated [`docs/GUIDE.md`](../GUIDE.md) (and its PDF) and [`README.md`](../../README.md).

## What changed since v1.1.1

### Runs on Raft 1.1 again
- The mod compiles under RML (C# 7.3) against Raft 1.1 / Unity 2021.3, with save and load fixed.
- Islands are stored in a new `.island` format (compressed binary: heights, paint, objects, settings).
- **Breaking:** islands built as Unity `.assets` files (version 2) are no longer supported; they have to be rebuilt in
  the editor.
- The editor scene and gizmo shaders still come from the [Custom-Islands-UI](https://github.com/FranzFischer78/Custom-Islands-UI)
  Unity project's bundles; the editor's panels are now built in code in Raft's own look (the bundle's old toolbar is
  switched off), and the 2021 RTS camera is replaced by a Unity-style scene camera.

### Island editor
- Terrain brushes (Raise, Lower, Flatten, Smooth, Noise, Erode, brush falloff), terrain stamps, texture painting with
  Raft's ground textures, five island styles and a second mixed-in style.
- Every object of Raft (~1,900) placeable from a picture browser with search, including Raft's building blocks and the
  objects of every story island; objects settle on the ground by their base.
- Selection tools, copy and paste between islands, Place exactly, object groups, undo/redo, autosave and crash recovery,
  a time-of-day preview and **Test in a world**.
- My islands / My groups and stamps windows; renaming an island updates the worlds, plans and rules that use it.

### Island generator and map types
- A seeded generator with layouts, coasts, peaks, lakes, reefs and underwater slopes, with densities measured from
  Raft's own islands; buildings, caves, animals, loot and an optional generated quest.
- *Randomize existing* (variations of Raft's 33 islands), Raft's other story islands rebuilt from their own pieces.
- Map types (ready-made islands with content), now also as `.maptype` data files.

### Islands that come alive
- Creatures (20 of Raft's animals with custom stats and tint), notes and signs, chests and loot (locked chests,
  keypad codes, buried treasure), trigger zones and ambushes, air pockets, atmosphere and sound zones.
- Behaviour and events without code: doors, gates, lifts, levers, "when ... then" actions, "only if" checks, signals.
- Quests (several per island), story items, story sets, and a journal (J) with a progress panel.
- Raft's own features as placeables: dirt spots, wild beehives, machete vines, ziplines, quest item pickups.

### In a world
- Custom and freshly generated islands appear while sailing, unload when far away, and show on the Receiver; flying and
  underwater islands; harvesting, loot and kills remembered and regrown.
- **World plans:** rule cards (when / bring / where / tell / story) with a Check that walks the plan like a world plays
  it; chosen in Raft's New Game box or changed in a running world.
- **Story chain:** plan islands in Raft's Receiver story (first, after or instead of a story island), Raft's story on or
  off, and a main story with its own tabs and pages in Raft's notebook.
- **Optional World settings** (off in a plain world): monster difficulty, build cost, a world randomizer, nineteen
  extra options (ghost rafts, private storages, storm seas, a trader raft, extra upgrades, a ghost ship, fog banks and
  more), a head start raft and a level up system. All of it can be changed in a running world under Esc > Custom
  Islands.

### Multiplayer
- Up to eight players: the host sends islands, missing island files, harvest state and World settings to everyone who
  joins; chests, zones, doors, quests and the journal stay in sync and are decided by the host.
- **Host swap:** any player can host a saved world later; each world keeps its own copy of its plan.
- Version handshake between players and version notices on plans and worlds; newer worlds' unknown lines are kept.

### Sharing
- Packs (Export / Import) for islands and plans, never overwriting the player's own files.
- The in-game **island library** ([SwedenJohansson/CustomIslands-Library](https://github.com/SwedenJohansson/CustomIslands-Library))
  with ~100 example quest islands and five world plans (The Long Voyage, Raft Remade, Raft 2: The Drowned Frontier,
  The Abyss Expedition, Silver Screen Seas).

### Safety and reliability
- Saves replace files in one step and recover half-finished saves; deleted files go to a `deleted` folder.
- An audit of about 70 items across saves, files and multiplayer (crash recovery between Raft's saves, unloaded
  islands, library installs that check files and roll back, host file names checked before use).
- A start-up check on the main menu for common install problems (unzipped `.rmod`, unwritable or synced folders).

### Player-facing additions
- EXPERIMENTAL ALPHA RELEASE box on the main menu with Discord, Guide (PDF) and **Report a problem** (a report form
  with versions filled in, the log folder, a new GitHub issue).
- The illustrated guide (`docs/GUIDE.md`, 85 pictures, also as a PDF shipped in the `.rmod`).

## Compatibility notes

- **Every player needs the mod**, and all players should run the same version (`requiredByAllPlayers: true`).
- Don't run the original Dynamic Islands v1.1.1 next to 3.0: both use `Mods\DynamicIslands` and patch the same parts of
  Raft.
- Version 2 `.assets` islands must be rebuilt (see above).
- `modinfo.json`: version `3.0`, license GNU AGPLv3, update URL unchanged
  (`https://www.raftmodding.com/api/v1/mods/custom-islands/version.txt`).

## Known issues

Documented in README's *Known issues*, with the advice in the guide's section for each topic. The main ones:
playing together over several days needs care (host from the folder of whoever hosted last, same mod version, correct
PC clocks); editing an island that saved worlds use keeps those worlds on the version they started with; the mod's
own text is English only.

## Testing

- Two players on one PC (a second Steam account in Sandboxie) for every in-world feature; save/load, leave/rejoin and
  restarts tested automatically, alone and with two players; many-player cases simulated (`CIManyPlayers`). Not yet
  tested between two PCs over the internet.
- Automatic test runs (`alltests.ps1`): a story plan built in World Plans and played through, every editor window at
  eight screen sizes, every Raft graphics setting and language, broken settings and island files.
- Scenario tests where Raft's gameplay meets the mod, and a voyage played to its end by two players over three
  sessions with two host swaps. Every library island and plan was played through by an automatic tester.
- Release builds (`pack.ps1 -Release`) leave out the `CI*` development test commands.

## Building

RML compiles the `.cs` files when Raft starts; `compile.ps1` does a compile check with MSBuild and `pack.ps1` builds
`DynamicIslands.rmod`. See README's *Building* section.

# Project map

"Custom Islands", a Raft 1.1 mod for RML. Harmony patches into Raft. The
editor scene and gizmo shaders are Unity 2021.3 asset bundles from
FranzFischer78/Custom-Islands-UI; the panels are built in code.

## Read one doc for your task

| Task touches | Read |
|---|---|
| Writing or fixing a `.recipe` | `RECIPE_FORMAT.md` |
| Writing or fixing a `.play` test | `PLAY_FORMAT.md` |
| Running tests (`ci.ps1`), CI commands, test logs | `areas/testing.md` |
| Island generator, map types, Raft's measured islands | `areas/generator.md` |
| The editor: tools, windows, objects, undo, Test | `areas/editor.md` |
| World plans, Raft's story, islands while sailing, the notebook | `areas/world-plans.md` |
| Quests, story items, behaviours, notes, chests, zones | `areas/quests-story.md` |
| `.island` files, spawning, what a world saves | `areas/islands-in-world.md` |
| Host and clients, network messages | `areas/multiplayer.md` |
| World settings: rules, randomizer, options, levels | `areas/world-settings.md` |
| Packs and the online library | `areas/library.md` |
| Updating GUIDE, README, CHANGELOG for a feature | `areas/docs.md` |
| Adding a feature: every place it touches | `CHANGE_CHECKLISTS.md` (your kind's section only) |

Anything else: grep README.md's "### Source overview" table, which has one
line per source file.

## Layout

| Path | Holds |
|---|---|
| `DynamicIslands/` | The mod folder that becomes the `.rmod`: about 200 `.cs` files (and 113 `DevTests*.cs`), `modinfo.json` (version), asset bundles, measured Raft data (`raft_*.txt`, `island_thumbs/`, `island_heights/`) |
| `content/recipes/` | `.recipe` scripts that build an island or world plan |
| `content/tests/` | `.play` play-tests |
| `content/objects/` | Object catalogue lists by category |
| `docs/` | `GUIDE.md` (players' guide), the guide PDF, `images/`, `release/` (mod page and merge-request text), `claude/` (these docs; `claude/cloud-reports/` holds the reports of merged cloud branches) |
| root | `compile.ps1`, `ci.ps1` (run dev tests), `pack.ps1`, `build.bat`, README, CHANGELOG, `.sln` |

## Build and pack

- `compile.ps1 [-Full] [-Release]`: compile check. `-Full` prints every error (otherwise the first 40);
  `-Release` compiles without `DevTests*.cs`.
- `pack.ps1 [-Install] [-Release]`: makes `DynamicIslands.rmod` (a zip of
  `DynamicIslands/` plus the guide PDF). `-Install` copies it to Raft's
  `mods` folder (`-RaftDir`, default
  `D:\Games\SteamLibrary\steamapps\common\Raft`).
- Player files live in `Mods\DynamicIslands\`: `<name>.island`, `plans\`,
  `worlds\`, `maptypes\`, `recipes\`, `spawnpool.txt`, `library.txt`, `summarycache.txt`, `errors.txt` (`ErrorNotes`).
- Raft's log: `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\Player.log`.

## The library repo (SwedenJohansson/CustomIslands-Library)

A separate repo; see `areas/library.md`. Never edit its `index.json` by
hand; a GitHub workflow rebuilds it on every push to `main`.

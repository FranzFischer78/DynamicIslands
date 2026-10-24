# Area: player docs
`docs/GUIDE.md` (illustrated guide), `README.md` (full reference), `CHANGELOG.md`, `docs/images/`, `docs/release/`, `docs/Custom-Islands-Guide.pdf`.
README's `## Documentation` sets the rule: every change a player or island builder sees updates the docs in the same commit. Guide and README share one order: island creation, quests and stories first; the optional world systems after.

## Reading these files cheaply
- `docs/GUIDE.md` is ~440 KB; `README.md` is ~200 KB with lines over 3,000 characters. Never read either whole.
- Outline: `grep -n '^#' docs/GUIDE.md` (same for `README.md`). A term: `grep -n '<term>' docs/GUIDE.md README.md`.
- Then read ~40-line ranges; for README, cut long lines (`sed -n 'A,Bp' README.md | cut -c1-300`).

## Where a feature's docs go
Must change: the feature's rows in the table below. A new `.cs` file gets a README `### Source overview` row (the `DevTests*.cs` files get none). A new guide chapter 9 system gets a sub-bullet under README `- **Optional world systems**`.
Usually also: a `CHANGELOG.md` bullet; a picture in `docs/images`; a new word players must know in `### Words used in this guide`; a line in `## Contents` for a new subsection; the PDF made again.

| Kind of feature | GUIDE.md | README.md |
|---|---|---|
| Editor tool, tab, window, saving/sharing | `### 4.1`-`### 4.4`, `### 4.7`, `### 4.9` | `- **Island editor**` sub-bullet; `## The editor` (Where/Control/What it does table) |
| Generator option / map type | `### 4.5 The island generator` / `### 4.6 Ready-made islands (map types)` | `## The island generator` (Group/Settings table) / `## Map types` (Type/What table) |
| Creature, note, chest, zone, atmosphere, sound | `## 5.` (5.1-5.5) | `- **Island editor**` sub-bullet |
| Quest, behaviour/event, story item, Receiver, notebook | `## 6.` (6.1-6.5) | `- **Island editor**` sub-bullet |
| What players meet while sailing or on an island, the journal (J) | `## 3.` | `- **In your worlds**` sub-bullet |
| World plan, rule part | `## 7.` (7.3 rule card, 7.4 every rule, 7.5 Check) | `## World plans` (Part/Choices table), `### Raft's story and the Receiver` |
| Multiplayer behaviour | `## 8.`, `### Who needs what: every case`, `### Hosting on another day` | `- **Multiplayer` sub-bullet |
| World settings window, extra option, islands while sailing | `### 9.1`, `### 9.4` (one `####` per option), `### 9.5` | `## World settings: more ways to play Raft again` |
| World rule / randomizer part / level up | `### 9.2` / `### 9.3` / `### 9.6` | `## World rules: monster difficulty and build cost` / `## World randomizer` (Part/What changes table) / `## The level up system` |
| File or setting in `Mods\DynamicIslands\` | `## 10. Settings files` (`spawnpool.txt` table, "Other files:" table) | `## Installing` (File/What it is table) |
| Console command | `## 11. Console commands` | `## Console commands (F10)` |
| Good-to-know advice / can't-change limit | the topic's own section (no known-issues chapter) / `## 12.` | `## Known issues (being fixed)` / `## Limitations (can't be changed)` |
| Main menu box: alpha box / PC check | `## 1. Installing` / `**The PC check.**` in `## 1. Installing` | `- **EXPERIMENTAL ALPHA RELEASE box**` / `- **PC check:**` |

## GUIDE.md
Top: intro, `## Quick start` (I want to.../Do this/Read), `### Words used in this guide` (Word/Meaning), `## Contents` (linked list of chapters, numbered subsections and a few unnumbered ones: add a line for a new subsection).
Then `## N. Title` chapters, `### N.M Title` subsections, and unnumbered `####` topics inside a subsection; nothing deeper. Chapters 3 and 8, the end of 6 and the start of 7 use unnumbered `###`.
- 1 Installing, the alpha box. 2 Starting a new world: Raft's New Game box, the plan, **WORLD SETTINGS...**.
- 3 Sailing: islands appearing, arriving, notes/chests/zones/creatures, quests, the journal (J).
- 4 The editor: screen, Terrain, Objects, Island tab, generator, map types, saving and sharing, first island, Test.
- 5 Making islands come alive: creatures, notes, chests, zones and ambushes, atmosphere and sound.
- 6 Stories: quests, behaviour and events, story items, islands that bring islands, Raft's story and notebook.
- 7 World plans: the three kinds of islands, the shipped plans, first plan, rule card, every rule, Check, sharing, the plan file.
- 8 Playing together. 9 World settings: window, rules, randomizer, extra options, islands while sailing, level up.
- 10 Settings files. 11 Console commands (Command/Where/What it does). 12 Limitations. 14 Reporting a problem. 15 Learn from the library (15.1-15.13: example islands, plans, the storylines). 16 Learn from the islands (open any island in the editor, every feature and an island that shows it).
- No known-issues chapter (removed 2026-10-23): advice on how to avoid a problem goes in the topic's own section, written as plain advice; can't-change limits go to 12.
- 13 Questions and problems: a paragraph that starts with the problem in bold, ending in a full stop
  (`**No islands appear while I sail.**`), then the answer.
- Anchors are GitHub style: `### 4.7 Saving and sharing` is `#47-saving-and-sharing`. Before renaming a heading, grep `OpenGuideSection(` (code), `GUIDE.md#` (`README.md`, `docs/release/MOD_PAGE.md`) and `](#` (the guide).
- House style (README `## Documentation`): a feature gets its place in the matching section, in the same detail as the rest: what it does, where to find it, what players see, multiplayer, the console command; with a new picture in `docs/images` where one helps. Plain words, buttons in **bold**, files and commands in backticks.

## README.md sections
- Intro: credits, the alpha notice, links to `CHANGELOG.md`, the guide and its PDF.
- `## Features`: top bullets `- **Island editor**`, `- **In your worlds**`, `- **Multiplayer`, `- **Optional world systems**`, `- **PC check:**`, `- **Hover tips:**`, `- **EXPERIMENTAL ALPHA RELEASE box**`; a feature is a sub-bullet, often led by a **bold name**.
- `## Installing`: how to install, then the File/What it is table of `<Raft>\Mods\DynamicIslands\` (a row per new file or folder); `### Keys in the settings files` lists the hand-edit keys of `spawnpool.txt`, `world_rules.txt` and `randomizer.txt`.
- Island creation: `## The editor` (controls), `## The island generator`, `## Map types`, `## World plans`.
- Optional systems: `## World settings: more ways to play Raft again`, `## World rules: monster difficulty and build cost`, `## World randomizer`, `## The level up system`.
- `## Console commands (F10)`: Command/Where/What it does; the `CI*` dev commands (`DevTests*.cs`) are not listed, and `pack.ps1 -Release` leaves those files out.
- `## Reporting a problem` (short form of guide 14). `## Documentation`: the docs rule and table.
- `## Building`: `compile.ps1`, `pack.ps1`. `### Source overview`: File/What it holds, a row per `.cs` file except `DevTests*.cs` (related files share one).
- `## Known issues (being fixed)` and `## Limitations (can't be changed)` (the second points to guide 12). Then `## What has been tested`, `## Contributing`, `## License`.

## CHANGELOG.md
- Newest first, for players and island makers; internal clean-ups and test runs are left out.
- Blocks are `## <date>: <theme>` (the newest starts with the version, `## 3.0 alpha - `), separated by `---`, with `### New`, `### Changed`, `### Fixed` as needed.
- An entry is `- ` plus one or two plain sentences, a new feature often led by its **bold name**. Lines wrap before 120 characters; continuation lines are indented two spaces.

## Images, the guide PDF, docs/release/
- `docs/images/*.jpg`: lowercase with hyphens, prefixed by where the shot is: `editor-`, `generator-`, `generated-`, `world-`, `plan-`, `library-`, `levels-`, `randomizer-`, `notebook-`, `newgame-`, `receiver-`, `level-`, `learn-` (also `main-menu.jpg`, `maptypes.jpg`, `report-box.jpg`).
- Guide: `![Alt text](images/<name>.jpg)` on its own line, then an italic `*caption*` line. README uses `docs/images/<name>.jpg`.
- Library thumbnails: `docs/images/library/<name_with_underscores>.jpg`, inline in chapter 15 tables as `![](images/library/<name>.jpg) **Name**`.
- `docs/Custom-Islands-Guide.pdf` is made again from `GUIDE.md` after a change to it, by `guidepdf.ps1` (Node and Microsoft Edge), which is in the project's tools, not this repo. `pack.ps1` packs it into the `.rmod` as `Custom-Islands-Guide.pdf`; `HelpLinks.OpenGuide` writes it to `Mods\DynamicIslands\` to open it. Without it, `pack.ps1` warns and the Guide button opens the PDF online (`HelpLinks.GuidePdfOnline`).
- `docs/release/MERGE_REQUEST.md`: the merge request text for the 3.0 release (summary, changes since v1.1.1 by area, compatibility, known issues, testing, building); it points to `CHANGELOG.md`.
- `docs/release/MOD_PAGE.md`: the mod page text for 3.0 (what it is, installing, features, known clashes, the island library, guide and help, reporting a problem); it links to the guide and changelog on GitHub.
- Neither release file is per feature: change them for a release, or when a change makes one of their statements wrong.

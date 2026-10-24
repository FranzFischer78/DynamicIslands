# Custom Islands 3.0 (experimental alpha)

**Build your own islands in an in-game editor, then find them in your worlds while sailing.**

By FranzFischer78 (code) and MegaMatrixs (design). Version 3.0 was rebuilt for Raft 1.1 (Unity 2021.3) with
SwedenJohansson.

> **Experimental alpha release.** This is the first release of version 3.0, an early alpha. Things are likely to
> change, some systems might be unstable, and progress is not guaranteed to be saved: **back up the worlds you care
> about** (Raft keeps them in `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\User`).

---

## What it is

Custom Islands adds an **island editor** to Raft's main menu. Sculpt and paint the land, or generate a whole island
from a seed, place any of Raft's ~1,900 objects, and give the island life: Raft's own animals, notes, chests, trigger
zones, doors and levers, quests and story items. Your islands then turn up in your worlds while you sail, show on
Raft's Receiver, and are saved with the world like Raft's own islands.

**World plans** decide which islands a world gets: random islands, a story from island to island, or your islands
woven into Raft's own Receiver story, with their own tabs in Raft's notebook. The **island library** in the main menu
has close to a hundred ready-made quest islands and five world plans to play or to open in the editor and learn from.

---

## Every player needs the mod

Custom Islands is **required by all players**. In multiplayer (up to eight players, Raft's maximum) everyone needs
the mod and RML, and **everyone must run the same version** of Custom Islands: check the version in the alpha box on
the main menu before you play together, and update everyone before a session, not in the middle of an adventure.
The host's islands, missing island files and World settings are sent to everyone who joins, and any player can host
a saved world later.

---

## Requirements and installing

- **Raft 1.1** (tested on 1.1.01).
- **Raft Mod Loader (RML)** from [raftmodding.com](https://www.raftmodding.com/) (tested with v2.8.10).

1. Install RML and start Raft through it once.
2. Put **`DynamicIslands.rmod`** into Raft's `mods` folder (for example `...\steamapps\common\Raft\mods\`), or install
   it from this page. **Don't unzip it.**
3. Start Raft with RML's **Play** button. The first start takes a little longer while RML compiles the mod.

Your islands, plans and settings live in `<Raft>\Mods\DynamicIslands\`. Keep Raft out of `Program Files` and synced
folders (OneDrive, Dropbox), so the mod can save its files there.

---

## Features

### The island editor (EDITOR in the main menu)
- Sculpt with Raise, Lower, Flatten, Smooth, Noise and Erode brushes; terrain stamps; paint with Raft's own ground
  textures or let the island texture itself.
- Five styles: Tropical, Snowy, Desert, Forest and Volcanic.
- Place **every object of Raft** (~1,900) from a browser with pictures and a search, including Raft's building blocks
  (build huts or your own abandoned rafts) and the objects of every story island.
- Box select, copy and paste between islands, object groups, Place exactly, undo/redo, autosave.
- **Light** shows the island at morning, noon, evening, night or overcast; **Test** tries it in a world in one click.

### The island generator
- A whole island from a seed: layouts (atoll, archipelago, sea stacks, plateau, crescent...), size, peaks, coasts,
  beaches, cliffs, lakes, and an underwater slope like Raft's own islands.
- Trees, rocks, resources, things to gather and finds in the shallows placed from measurements of Raft's islands.
- Animals, loot boxes, buildings and caves, and even a quest made with the island.
- **Randomize existing:** something new like one of Raft's 33 islands, or a variation of it.
- **Map types:** ready-made islands with content (wreck, volcano, treasure island, sunken island, sky island...).

### Islands that come alive
- **Creatures:** 20 of Raft's animals with your own health, damage, speed, size and colour; catchable animals; ambushes.
- **Notes and signs, chests and loot** (also locked chests and keypad codes), buried treasure, ziplines, machete vines.
- **Trigger zones, atmosphere zones** (fog, light, particles) and **sound zones** with Raft's 500+ sounds.
- **Behaviour and events, no code needed:** doors, gates, lifts, levers, "when ... then" actions and "only if" checks.
- **Quests** (several per island), **story items** and a **journal** (J) with the crew's progress.

### In your worlds
- Custom and freshly generated islands appear ahead of the raft while you sail and show on the Receiver.
- Flying and underwater islands; harvested trees and looted chests are remembered and grow back.
- **World plans:** pick a plan in Raft's New Game box, or make your own with rule cards and a Check that finds problems.
- **Your islands in Raft's story:** leave out or replace story islands, add your own on their own Receiver frequency,
  with tabs and pages in Raft's notebook.

### Sharing
- **Packs:** Export an island or plan (with every island it brings) as a `.zip`; Import never overwrites your files.
- **The island library** (ISLAND LIBRARY in the main menu): browse, download and update islands and plans in-game.

### Optional world settings (off in a plain world)
- **Monster difficulty** (Timid to Nightmare) and **build cost** (0-100% more materials).
- **World randomizer:** a normal Raft world that plays out differently every time, without touching Raft's story.
- **Extra options**, each on or off: ghost rafts, private storages, long voyage, iron raft, shared EXP, dangerous
  nights, a daily quest, a rogue shark, silver and golden barrels, storm days, storm seas, a trader raft, extra
  upgrades, UFO sightings, a bioluminescent sea, lightning strikes, a ghost ship, whale breaches and fog banks; and a
  head start raft.
- **Level up system:** EXP from monsters and stat points to spend (K).
- Changed any time in a running world with **Esc > Custom Islands** (the host decides for everyone).

---

## Known clashes

- **The original Dynamic Islands mod (FranzFischer78's v1.1.1):** don't keep it in the mods folder next to this one.
  Both use the same `Mods\DynamicIslands` folder and patch the same parts of Raft.
- **An unzipped copy of this mod:** if you ever unzipped the `.rmod` into `Mods\DynamicIslands`, the unzipped files win
  over every future update. Delete `modinfo.json`, `raft_*.txt` and any `.cs` files from that folder (keep your
  islands, plans and settings), then restart Raft.
- **Different Custom Islands versions in one game:** players are warned, but older versions can drop parts of a world
  they don't know when they host and save. Everyone should run the same version.
- **Islands made with version 2** as Unity `.assets` files no longer load: rebuild them in the editor.

Other mods haven't been tested together with Custom Islands. If another mod misbehaves with it, please report it
(below) and name your other mods.

---

## The island library

Download islands and world plans others made, right in the game: **ISLAND LIBRARY** in the main menu (also
**Get more plans...** in the New Game box). Every entry is looked at before it goes in.

- Library on GitHub: [SwedenJohansson/CustomIslands-Library](https://github.com/SwedenJohansson/CustomIslands-Library)
- Share your own: export it in the editor, then post the pack on the
  [Custom Islands Discord](https://discord.gg/U7DfKY9tN), or use the
  [Submit an island or plan](https://github.com/SwedenJohansson/CustomIslands-Library/issues/new?template=submit.yml)
  form.

---

## Guide and help

- [Illustrated guide](https://github.com/SwedenJohansson/DynamicIslands/blob/master/docs/GUIDE.md) (also as a PDF
  from the **Guide (PDF)** button on the main menu): installing, sailing, the editor, quests and stories, world plans,
  playing together and the optional systems, with 85 pictures.
- [Changelog](https://github.com/SwedenJohansson/DynamicIslands/blob/master/CHANGELOG.md): everything new in 3.0.
- Before playing together over several days, read the guide's
  [Hosting on another day](https://github.com/SwedenJohansson/DynamicIslands/blob/master/docs/GUIDE.md#hosting-on-another-day).

---

## How to report a problem

The quickest way: **Report a problem** in the alpha box on Raft's main menu. It copies a report form with your
versions filled in, opens the log folder, and opens a new GitHub issue or the Discord.

Or report it yourself, on either:
- **GitHub:** [github.com/SwedenJohansson/DynamicIslands/issues](https://github.com/SwedenJohansson/DynamicIslands/issues)
- **Discord:** [discord.gg/U7DfKY9tN](https://discord.gg/U7DfKY9tN)

Please include:
1. **What happened**, step by step, and how often (every time, now and then, once).
2. **What you expected** to happen.
3. **Screenshots** or a short video, if it can be seen.
4. **The logs, before you restart Raft:** `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\Player.log` (and
   `Player-prev.log` after a crash); if Raft won't start with the mod, `%APPDATA%\RaftModLoader\logs\hloader.log`.
   Playing together: every player's logs, the host's first.
5. **Versions:** Custom Islands, Raft and RML (all on the main menu) and your other mods; single player or together.
6. **The islands and world plans involved** (names, files or download links).

---

License: GNU AGPLv3

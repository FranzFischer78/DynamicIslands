# Custom Islands: the guide

A picture-by-picture guide to **Custom Islands** (DynamicIslands), a [Raft](https://raft-game.com/) mod for the
[Raft Mod Loader](https://www.raftmodding.com/). It is for players who want new islands in their worlds, and for
builders who want to make islands and whole adventures of their own. You don't need to know anything about modding:
everything is done with buttons in the game. The [README](../README.md) has the full reference (every setting, file
and command); this guide shows you around.

Custom Islands version 3 is made by **SwedenJohansson**, based on DynamicIslands by FranzFischer78 (code) and
MegaMatrixs (design).

The guide follows the heart of the mod first: **meeting custom islands** while you sail (sections 2-3), **building your
own** in the editor (4-5), giving them **stories** - quests, doors and keys, story items, islands in Raft's story (6) -
and **world plans** that decide which islands a world gets, when and where (7). Section 8 is about playing together. The
**optional systems** that change a whole world - world rules, the world randomizer, the extra options, the island list
and the level up system - are gathered in section 9, followed by the files, the console commands, help, and how to
report a problem (section 14). Chapter 12 lists what can't be changed.

![A generated island seen from the sea](images/world-island-from-sea.jpg)
*A custom island met while sailing: made by the mod's island generator, with Raft's own palms, rocks and reef.*

## Quick start

Pick what you want to do; each line leads to the part of the guide you need.

| I want to... | Do this | Read |
|---|---|---|
| **Just play with new islands** | Install the mod, click **NEW WORLD**, leave everything as it is, **Create**, and sail | [1](#1-installing), [2](#2-starting-a-new-world), [3](#3-sailing-custom-islands-in-your-world) |
| **Play an adventure someone made** | **ISLAND LIBRARY** in the main menu → **World plans** → **Download**; then **NEW WORLD** → choose it as the Custom Islands plan | [4.7](#47-saving-and-sharing), [7.6](#76-playing-changing-and-sharing-a-plan) |
| **Build my own island** | **EDITOR** → **Generate** an island → shape it, add objects → **Save** | [4.8](#48-your-first-island-step-by-step) |
| **Give my island a quest or a secret** | Island tab → **Edit quest...**; an object → **Behaviour & events...** | [5](#5-making-islands-come-alive), [6](#6-stories-quests-behaviours-story-items) |
| **Make my own adventure across several islands** | **EDITOR** → **A world plan** → **New world plan** → add your islands → **Save**; choose it in NEW WORLD | [Three things you can make](#three-things-you-can-make), [7.2](#72-your-first-world-plan-step-by-step) |
| **Play with friends** | Everyone installs the mod; the host creates the world; friends join through Steam | [8](#8-playing-together), [Hosting on another day](#hosting-on-another-day) |
| **Change how a world plays** (tougher monsters, levels, a randomized world) | **NEW WORLD** → **WORLD SETTINGS...** | [9](#9-world-settings-rules-and-extra-systems) |
| **Report a bug or ask a question** | Main menu alpha box → **Report a problem** (or **Discord**) | [14](#14-reporting-a-problem) |

### Three things you can make

Click **EDITOR** in Raft's main menu. A window asks **What do you want to make?** and has three cards. The card of what
you made last is lit, and its **Continue** button takes you straight back to it.

| Card | What it is | What you click |
|---|---|---|
| **An island** | One island: its land, objects, creatures, quests. Saved as `<name>.island` | **New island**, **Open an island...**, or **Continue '<last island>'** (shown with its picture). The 3D editor opens ([chapter 4](#4-building-your-own-island-the-editor)) |
| **A world plan** | The list of islands a world gets, and when and where each comes. Saved as `<name>.plan`. You make it right on the main menu - no island is loaded and no 3D editor opens ([chapter 7](#7-world-plans-which-islands-a-world-gets)) | **New world plan**, **Open a world plan...**, or **Continue '<last plan>'** (with its Main story line in a few words) |
| **The guide** | This guide | **Open the guide**; **Islands, plans, story** opens this section online |

The **Island Library** stays on Raft's main menu, as before.

**The Main story line** is the part of a plan that tells a story. It is the islands the Receiver leads players to, one
after the other, in the order they are played. After the last of your islands the Receiver leads to Raft's ending,
Utopia. Islands that are not in the Main story line come on their own - at the start, after some km or days, when a
quest is done, near another island - and are called **Other islands**.

How the three fit together: you build islands, then put them in a plan. In the island editor, **Use in a plan...**
adds the island you are working on to a plan; on a plan's island card, **Edit island** opens that island in the 3D
editor, and **Back to the plan** brings you back ([7.2](#72-your-first-world-plan-step-by-step)). Closing a plan comes
back to this window.

### Words used in this guide

| Word | Meaning |
|---|---|
| **Custom island** | An island of this mod, as opposed to Raft's own islands. You meet them while sailing, like Raft's |
| **Island file** | A saved island, `<name>.island` in `Mods\DynamicIslands\`. Made in the editor, downloaded, or sent by a host |
| **Map type** | A kind of island the mod makes new for each world: a camp, a volcano, a wreck, a sky island... ([4.6](#46-ready-made-islands-map-types)). No file needed |
| **Generated island** | An island made by the island generator from a style and a size ([4.5](#45-the-island-generator)) |
| **Spawn pool** | The islands that may turn up by chance while you sail (`spawnpool.txt`, [10](#10-settings-files)) |
| **Quest** | An island's list of steps (go there, read that, open this...) with a reward at the end ([6.1](#61-quests)) |
| **Story item** | A key, a map, a log... that the crew carries and the journal (J) shows ([6.3](#63-story-items-and-story-sets)) |
| **Journal** | The crew's book of the custom islands (J): their story items and every note read, shared by everyone in the world. Not Raft's notebook (T) ([3](#the-journal-j)) |
| **World plan** | A list of rules for a world: which islands it gets, when and where ([7](#7-world-plans-which-islands-a-world-gets)) |
| **Main story line** | The islands of a plan that the Receiver leads players to, one after the other, in play order; the other islands of the plan come on their own ([Three things you can make](#three-things-you-can-make)) |
| **Rule** | One line of a plan: *when* something happens, bring *this island*, *there*, and tell the players *this* |
| **Raft's story / the Receiver chain** | Raft's own story islands (Radio Tower ... Utopia), found by tuning the Receiver. A plan can change it ([6.5](#65-your-islands-in-rafts-story-the-receiver)) |
| **World settings** | Optional switches for one world: monster difficulty, build cost, randomizer, extra options, levels ([9](#9-world-settings-rules-and-extra-systems)) |
| **Pack** | A `.zip` made with **Export...**: a plan or island with every island it needs, to share ([4.7](#47-saving-and-sharing)) |
| **Island library** | The public collection of plans and islands others made, downloaded in the game ([4.7](#47-saving-and-sharing)) |
| **Host** | The player whose PC runs the world in multiplayer. The host's islands, plan and settings count ([8](#8-playing-together)) |

## Contents

First: [Three things you can make](#three-things-you-can-make) (island, world plan, Main story line).

1. [Installing](#1-installing)
2. [Starting a new world](#2-starting-a-new-world)
3. [Sailing: custom islands in your world](#3-sailing-custom-islands-in-your-world)
4. [Building your own island: the editor](#4-building-your-own-island-the-editor)
   - [4.1 The screen](#41-the-screen) · [4.2 Shaping the land](#42-shaping-the-land-terrain-tab) ·
     [4.3 Placing objects](#43-placing-objects-objects-tab) · [4.4 The Island tab](#44-the-island-tab)
   - [4.5 The island generator](#45-the-island-generator) · [4.6 Ready-made islands (map types)](#46-ready-made-islands-map-types)
   - [4.7 Saving and sharing: autosave, export, import, the island library](#47-saving-and-sharing)
   - [4.8 Your first island, step by step](#48-your-first-island-step-by-step) · [4.9 Trying the island in a world (Test)](#49-trying-the-island-in-a-world-test)
5. [Making islands come alive](#5-making-islands-come-alive): [creatures](#51-creatures), [notes](#52-notes-and-signs),
   [chests](#53-chests-and-loot), [zones and ambushes](#54-trigger-zones-and-ambushes), [atmosphere and sound](#55-atmosphere-and-sound)
6. [Stories: quests, behaviours, story items](#6-stories-quests-behaviours-story-items)
   - [6.1 Quests](#61-quests) · [6.2 Behaviour and events](#62-behaviour-and-events) · [6.3 Story items](#63-story-items-and-story-sets)
   - [6.4 Islands that bring islands](#64-islands-that-bring-islands) · [6.5 Your islands in Raft's story (the Receiver)](#65-your-islands-in-rafts-story-the-receiver) ·
     [The main story in Raft's notebook](#the-main-story-in-rafts-notebook-side-quests-in-the-journal) · [Making a main story](#making-a-main-story-the-helper-and-preview-notebook)
7. [World plans: which islands a world gets](#7-world-plans-which-islands-a-world-gets)
   - [7.1 The plans that come with the mod](#71-the-plans-that-come-with-the-mod)
   - [7.2 Your first world plan, step by step](#72-your-first-world-plan-step-by-step)
   - [7.3 A rule card, part by part](#73-a-rule-card-part-by-part) · [7.4 Everything a rule can do](#74-everything-a-rule-can-do)
   - [7.5 Check: finding and fixing problems](#75-check-finding-and-fixing-problems)
   - [7.6 Playing, changing and sharing a plan](#76-playing-changing-and-sharing-a-plan) · [7.7 The plan file](#77-the-plan-file)
8. [Playing together](#8-playing-together)
   - [Who needs what: every case](#who-needs-what-every-case) · [Hosting on another day](#hosting-on-another-day)
9. [World settings: rules and extra systems](#9-world-settings-rules-and-extra-systems)
   - [9.1 The World settings window](#91-the-world-settings-window)
   - [9.2 World rules: monster difficulty and build cost](#92-world-rules-monster-difficulty-and-build-cost)
   - [9.3 The world randomizer](#93-the-world-randomizer)
   - [9.4 Extra options](#94-extra-options)
   - [9.5 Islands while sailing](#95-islands-while-sailing)
   - [9.6 The level up system](#96-the-level-up-system)
10. [Settings files](#10-settings-files)
11. [Console commands](#11-console-commands)
12. [Limitations: what can't be changed](#12-limitations-what-cant-be-changed)
13. [Questions and problems](#13-questions-and-problems)
14. [Reporting a problem](#14-reporting-a-problem)
15. [Learn from the library: example islands and plans](#15-learn-from-the-library-example-islands-and-plans)
   - [15.1 Opening an example in the editor](#151-opening-an-example-in-the-editor) ·
     [15.2 The small and medium islands](#152-the-small-and-medium-islands) · [15.3 The very large islands](#153-the-very-large-islands) ·
     [15.4 The world plans](#154-the-world-plans) · [15.5 Where to look for...](#155-where-to-look-for) ·
     [15.6 The themed islands](#156-the-themed-islands) · [15.7 The new islands](#157-the-new-islands) ·
     [15.8 Raft's story islands, piece by piece](#158-rafts-story-islands-piece-by-piece) ·
     [15.9 A whole storyline: The Rainmakers](#159-a-whole-storyline-the-rainmakers) ·
     [15.10 A whole storyline: The Last Post](#1510-a-whole-storyline-the-last-post) ·
     [15.11 A whole storyline: Relic of the Seventh Tide](#1511-a-whole-storyline-relic-of-the-seventh-tide) ·
     [15.12 A whole storyline: The Lullaby Frequency](#1512-a-whole-storyline-the-lullaby-frequency) ·
     [15.13 A whole storyline: The Gravity Sea](#1513-a-whole-storyline-the-gravity-sea) ·
     [15.14 A whole storyline: The Last Green World](#1514-a-whole-storyline-the-last-green-world) ·
     [15.15 A whole storyline: The Frozen Horizon](#1515-a-whole-storyline-the-frozen-horizon) ·
     [15.16 A whole storyline: The City That Never Sank](#1516-a-whole-storyline-the-city-that-never-sank) ·
     [15.17 A whole storyline: The Sunken Kingdom](#1517-a-whole-storyline-the-sunken-kingdom) ·
     [15.18 A whole storyline: The End of the Ocean](#1518-a-whole-storyline-the-end-of-the-ocean)
16. [Learn from the islands](#16-learn-from-the-islands)
   - [16.1 Opening any island in the editor](#161-opening-any-island-in-the-editor) · [16.2 Looking around](#162-looking-around) ·
     [16.3 Two small islands with many features](#163-two-small-islands-with-many-features) · [16.4 Every feature, and an island to look at](#164-every-feature-and-an-island-to-look-at)

---

## 1. Installing

> **Experimental Alpha Release:** this is the mod's first release, an early alpha. Things are likely to change, some systems might be
> unstable, and progress is not guaranteed to be saved - back up the worlds you care about (Raft keeps them in
> `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\User`). The main menu shows this in the **EXPERIMENTAL ALPHA
> RELEASE** box right of the middle, with the mod's version and three buttons: **Discord** (the Custom Islands Discord
> server), **Guide (PDF)** (this guide, which comes with the mod) and **Report a problem** ([section 14](#14-reporting-a-problem)).
> Drag the box anywhere you like; it stays where you put it. It steps aside while another window of the main menu is
> open (New Game, Load World, Settings, the island library...) and comes back when you close it. **Got it** folds it into a slim bar with the same buttons (its
> **Show** opens it again); its **X** closes it altogether, and then only a small **Custom Islands: help** button is left in
> the menu's bottom right corner, which opens it again. The mod remembers either for this version only (in `notice.txt`),
> so a new version shows the whole box again.

1. Install the **Raft Mod Loader** (RML) from [raftmodding.com](https://www.raftmodding.com/) and start Raft through it.
2. Put `DynamicIslands.rmod` into Raft's `mods` folder (for example
   `...\steamapps\common\Raft\mods\`), or install the mod from its raftmodding.com page. Put the `.rmod` file itself
   there - don't unzip it: Raft's mod loader needs the file, and the mod reads its own files from it. If you unzipped
   it before, delete `modinfo.json`, `raft_*.txt` and any `.cs` files from `<Raft>\Mods\DynamicIslands` (keep your
   islands, plans and settings files). Keep only this mod, not the original **Dynamic Islands** (Franz's) next to it:
   they share a folder.
3. Start Raft with RML's **Play** button. The first start after installing or updating the mod takes about a minute longer: RML compiles the mod, and the main menu says so once when it is ready. Later starts use RML's kept copy and are quicker ([13](#13-questions-and-problems)). When a newer version is out, the main menu tells you once per start ([13](#13-questions-and-problems)).

The mod keeps its files in `<Raft>\Mods\DynamicIslands\`: your islands (`*.island`), the settings files
(`spawnpool.txt` and others, see [section 10](#10-settings-files)) and the world plans (`plans\`).

**The PC check.** When the mod starts it checks the PC: whether it can save in `Mods\DynamicIslands`, whether Raft sits
in a synced folder (and, when saving failed, under Program Files - Steam's own default folder works), whether the .rmod
was unzipped there, whether the original Dynamic Islands mod is loaded too, and whether every part of the mod started.
If something is wrong, one box on the main menu says what and how to fix it.

**Playing together?** Every player needs the mod. The host's islands, files and settings are sent to everyone who
joins (see [section 8](#8-playing-together)).

## 2. Starting a new world

Click **NEW WORLD** in Raft's main menu. Raft's box looks as it always does, with two things of the mod at the bottom
right: the **Custom Islands plan** and the **WORLD SETTINGS...** button, which says how many settings differ from
plain Raft.

![The New Game box with the mod's parts](images/newgame-box.jpg)
*Raft's New Game box with the Custom Islands plan (Random islands) and the World settings button.*

| Part | What it does |
|---|---|
| **Custom Islands plan** | The chosen plan's name and **Choose plan...**, which opens the plan picker (below). Which custom islands the world gets: **Random islands** (they appear by chance while you sail), **No custom islands**, or a plan with a storyline (from the island library: a story from island to island; see [section 7](#7-world-plans-which-islands-a-world-gets)). A plan can mix random islands, side trips and main quest islands: [the three kinds](#the-three-kinds-of-islands-you-meet-at-sea). The box shows the chosen plan's description under it (hover it for the whole text); under that, **Get more plans...** opens the island library to download plans others made; a plan you download there is chosen here ([4.7](#47-saving-and-sharing)) **View the plan...** shows what the chosen plan does: its islands, when and where each comes, and Raft's story. |
| **World settings...** | Opens the World settings window: the world's rules, the world randomizer, the extra options, the level up system and which islands turn up while sailing ([section 9](#9-world-settings-rules-and-extra-systems)). The button reads `Raft's own` while nothing differs from plain Raft, otherwise how many settings you changed (`3 changed`) |

**Choose plan...** opens the **plan picker**: every plan on the left - its name, its kind (built in, library plan,
your plan; *big plan* when it brings ten islands or more) and how many islands it brings - and the selected one on the
right: a picture (the library's picture, or a map of its first island), whether random islands come while sailing,
whether Raft's story is on, its whole description and the islands it brings. Click a plan to look at it; **Select**,
a double-click or Enter chooses it, **Cancel** or Esc keeps the one before. **Up/Down** move through the list, and the
search box at the top shows only the plans whose name, description or islands hold what you type. **View rules...**
lists every rule of the plan in words.

![The plan picker](images/plan-picker.jpg)
*The plan picker: a plan selected, with a map of its first island and the islands it brings.*

Then click Raft's **Create** as usual. Your World settings are remembered for the next new world, and so is the plan
(in `newworld_plan.txt`; the first time it is **Random islands**, or the `defaultPlan` of `spawnpool.txt`). Both belong to the world:
saved with it, the same for every player, and every new world gets its own.

**Just want new islands?** Leave the plan on **Random islands**, leave World settings as they are (`Raft's own`) and
create the world: islands turn up while you sail ([section 3](#3-sailing-custom-islands-in-your-world)), and the rest of
Raft plays as it always does. The World settings are optional extras for players who know Raft well; they are described
together near the end of this guide.

**What does this button do?** Rest the mouse on it for a moment: in the mod's windows on the main menu (the plan
picker, World settings, the island library, Defaults...) a small tip by the mouse says what it does. In the editor the
status bar at the bottom says it instead, and in a world the Custom Islands window has its own hint line.

![The main menu](images/main-menu.jpg)
*The whole main menu. The EDITOR button opens the island editor ([section 4](#4-building-your-own-island-the-editor)), ISLAND LIBRARY the island library; the EXPERIMENTAL ALPHA RELEASE box (drag it anywhere), here folded with Got it, keeps its Discord, Guide and Report a problem buttons (Show opens it again). It steps aside while New World or any other window is open.*

## 3. Sailing: custom islands in your world

### Islands appear while you sail

*Random islands, side trips and main quest islands - the three kinds of islands you meet, and how each is set up - are
explained side by side in [section 7](#the-three-kinds-of-islands-you-meet-at-sea).*

With the **Random islands** plan, now and then an island appears **250-350 m ahead of the raft**: one after every
**3-6 of Raft's own islands** you meet (a number in that span each time; the world's creator can choose another span,
[9.5](#95-islands-while-sailing)), and **none in a new world's first 10 minutes of play**. However many islands take
part, they never crowd the sea. It can be:

- one of **your own islands** (every saved island takes part unless `spawnpool.txt` says otherwise; islands your world plan brings, and islands generated while sailing, don't come by chance),
- a **brand-new generated island**: a random size and style (tropical, snowy, desert, forest or volcanic), sometimes
  flying; it is saved as `gen-<style>-<seed>.island`, so it stays in that world. In World settings > Islands while sailing it is the row
  **Brand-new generated islands**; untick it and only made or downloaded islands and map types come (see [9.5](#95-islands-while-sailing)),
- a **map type**: a sandbar, a wreck of raft blocks, an atoll or a sunken island (more in [4.6](#46-ready-made-islands-map-types)).

Which of these a world may meet can be chosen when it is created ([9.5](#95-islands-while-sailing)). The optional world
settings add more things to meet at sea: the randomizer's odd islands, large islands and boss lairs
([9.3](#93-the-world-randomizer)) and ghost rafts ([9.4](#94-extra-options)).

Islands keep clear of Raft's own islands and of each other, and Raft won't put its islands on top of them later.
Islands far behind the raft are unloaded (after 800 m) and come back when you return.

**Islands you still need come back.** Raft's current carries the raft one way, so an island can drift out of reach -
and without a Receiver nothing shows the way back. An island the players still need comes back on its own: about
**12 minutes** after the raft left it behind (800 m and more), it turns up ahead of the raft again just as you left
it - its quest where it was, opened chests still open - and a banner says "Back in sight". That is:

- an island whose quest you began and didn't finish: it comes back up to **three times** (an island you left on purpose
  stops coming back);
- an island a world plan still waits for - one that leads to the next island, like The Abyss Expedition's sunken
  island: it comes back every time, until you've done there what the plan waits for;
- an island a plan brought on a set day or distance with **again until done** ([7.4](#74-everything-a-rule-can-do)): it
  comes back every time that many days (or km) after you were last near it - even if you never reached it - until you
  reached it and finished its quest.

An island found with a Receiver frequency stays where it is (the Receiver shows the way), and so do islands nobody
needs. With several players the host decides, and the island moves for everyone. (`returnMinutes` in
`spawnpool.txt`, [10](#10-settings-files); 0 = never.)

**A finished island never comes again by chance.** When the spawn pool picks an island this world already has:

- **finished** - its quest done, or, for an island without a quest, reached by a player, and every note on it found
  - it isn't picked; another island comes instead;
- **notes left** - its quest done but a note on it not found yet (the journal shows "(6/7 notes)") - it may come
  back as you left it, so a missed note isn't lost for good;
- **not finished** - its quest begun and not done, or never reached - it comes back ahead of the raft **as you left
  it** (quest steps, opened chests, harvest), with a banner "Back in sight", instead of a second, fresh copy;
- already near the players (loaded) - not picked.

Each pick is a fresh random draw by the islands' weights, so the same island can come twice - but one this world
already has (left behind, not finished) counts only **a quarter** of its weight, so islands you haven't seen come
first. An island whose builder set **"Comes by chance after ... km sailed"** isn't picked before the world has sailed
that far.

**On the Receiver:** once you have built Raft's Receiver, custom islands show as **green dots** with their distance,
if they are within 2 km - an island the players still need shows however far it is (`receiverDistance` in `spawnpool.txt`). An island a rule brought (a quest reward, a plan) carries its name on its dot.

![Custom islands on the Receiver](images/receiver-dots.jpg)
*Raft's Receiver with two custom islands as green dots, one 494 m away.*

### Arriving

An island **with a quest** and a name shows it as a **banner** when you come near (once per island per session), with its author and
a short welcome if the builder wrote them. An island without a quest shows no banner (its zone and trigger messages still come). Banners and messages (an island's, a zone's, a lever's) come **one after
another**: one that arrives while another is on screen waits until that one has gone. Each stays long enough to read: 5 seconds for a short one, longer for a long description (up to 16).

![Arriving at Crane Yard](images/world-arrival-banner.jpg)
*An island's banner as you arrive: Crane Yard, with its quest already listed on the right.*

**Islands missing.** When a world you load uses island files this PC doesn't have (deleted, renamed, or removed from
the library since), a box names them and the library entry each came with. **Play without them** plays the rest (those
islands stay in the world and come back once their file is here again); **Main menu** leaves without saving, so you
can get them again. Only the host or a single player sees the box ([13](#13-questions-and-problems)).

### What you can do on an island

Custom islands work like Raft's own: you can **walk** on them, the **raft runs aground** on them, and you can
**chop** palms, pines, birches and mango trees and **pick up** rocks, ores, clay, sand, berries, pineapples,
flowers and more. What you took **stays taken** (also after saving, loading and sailing away), and it **grows
back** after 3 in-game days (the island's builder or `spawnpool.txt` can change that).

![On a generated island](images/world-on-a-generated-island.jpg)
*On a generated tropical island: Raft's bamboo, palms and plants, placed where Raft's own islands have them.*

![A generated jungle](images/world-jungle.jpg)
*A generated island with the Nature setting on "Jungle": you have to squeeze between the trees.*

**Diving:** generated islands rise from a deep sea floor like Raft's own: a sandy shelf about 10 m deep with corals
and sea plants, then a drop-off. Giant clams, silver algae, stones, ores, scrap and the seaweed on sea vines can be
picked up.

![Diving around a generated island](images/world-dive.jpg)
*Under water around a generated island: the shelf, the reef, the drop-off and the slope further down.*

**Flying and sunken islands:** some islands float high in the sky (build stairs or pillars up from the raft), others
lie under the surface for divers.

### Notes, chests, zones and creatures

The builder of an island can give it much more than land (see [section 5](#5-making-islands-come-alive)). In a world:

- **Notes:** look at a readable object (a sign, a paper, a bottle...) and press the interact key (**E**) to read it.
  Close it with E, Tab, Esc or the button.
- **Chests:** look at one and press **E**: the items go into your inventory. The chest is then empty for everyone
  until it fills up again (after the regrow days, or never). Some chests are **locked** until you have their key.
- **Trigger zones:** invisible areas that fire when a player walks in: a message, items, or an **ambush** (animals
  that appear the moment you step in). Under water, a zone can be an **air pocket**: inside it your breath fills up
  again, as in Raft's air pockets under Caravan Town.
- **Creatures:** Raft's own animals at the island's creature spots, with the builder's toughness, size and colour.
  Warthogs and bears fight you; chickens, goats and llamas can be caught with Raft's net launcher and kept on the raft.
  Killed and caught animals come back after the regrow days unless the builder said never. A **boss** may have a
  second phase: at part of its health it roars, calls more animals and gets faster.
- **Survivors:** someone locked in a cage. Bring the cage's key (a story item) and press **E**: the cage opens, the
  survivor thanks you in a speech bubble, and from then on stands on your raft.
- **Locked doors:** Raft's card reader doors and other key doors open only with their card or key (a story item): the
  reader beeps it through and the door opens for everyone. Without it the door stays shut and says so.
- **Buried treasure:** nothing shows above the ground. Raft's **metal detector** beeps as you come near, and three
  digs with the **shovel** bring up the chest. Once dug up it is gone for every player until the regrow days.
- **A Raft store or a vending machine:** a trading post on an island is a real store (press **E**: Raft's goods for
  trash cubes and trade coins, and any of Raft's story blueprints you missed). Tangaroa's vending machine sells
  Tangaroa's plants for its tokens. Both keep their stock with the world, the same for every player
  ([5.3](#53-chests-and-loot)).

![Reading a note](images/world-note.jpg)
*A note found on an island ("Warthogs live here. Bring a spear."), its title in Raft's lettering and its text on Raft's light tan, as the journal shows its pages. Press E, Tab or Esc to close it.*

### Quests

An island with a quest shows a **quest panel** on the right when you come within about 70 m of its shore (it stays up
until you are 150 m away, so islands still need looking for), with its steps: go somewhere, read a
note, open a chest, defeat or catch animals, collect story items, find journal pages, find one of Raft's blueprints. Each step done shows what's
next; the last one gives the reward to every player near the island - and a crew member who was elsewhere (or joins
later) gets their share when they come to the island, once; that player sees "Your reward for '<quest>' waits for you at
the island - sail there to get it". Rewards in Raft items go to the players within about 150 m of the island when the last
step is done; story items go to the whole crew. **Collect** and **find journal pages** steps count what the crew has
found (story items also when a lock used them up since) and finish when a player comes to the island, so the reward
reaches that player. The panel shows **five steps at a time**: the one
just done, the one you're on and what comes after. A longer quest scrolls - to your step by itself whenever it changes,
and with the mouse wheel over the panel while the cursor is free (the inventory, the journal, the Esc menu).
Once every quest of the island is done, the panel goes by itself **30 seconds** later; its **×** (top right, while the
cursor is free) closes it at any time - it comes back with the next step, or when you come to the island again.

![The quest panel](images/world-quest-panel.jpg)
*The quest panel of "The lost camp" on the right: the first step is "Go to camp".*

![Quest complete](images/world-quest-done.jpg)
*Quest complete: the banner, every step ticked in the panel, and the reward (planks and rope) in the inventory.*

### New islands from quests and rules

A finished quest (or a plan's rule) can **bring a new island**: every player sees a banner with a message and how far
and which way it is, and the island's dot on the Receiver carries its name.

![A banner for a new island](images/world-new-island-banner.jpg)
*A rule brought a new island: "About 970 m to the south-east (on your Receiver)". The quest panel shows the
finished step.*

### The journal (J)

The **journal** is the crew's book of the custom islands' story: what you found on them and what you carry for them.
Press **J** in a world to open it; **J**, **Esc** or **Close** closes it. (Esc closes only the window: Raft's pause menu doesn't open on the same press. The same goes for a note, the code lock, the stats page, a choice and the info windows. Every window of the mod - in a world, on the main menu and in the editor - closes with Esc, the topmost one first, one per press. A window with changes you haven't saved - World plans, a quest, a note, story items, behaviour and events - asks first: **Save and close**, **Close without saving** or **Keep editing**.) (J and the stats page's K can be changed in **Defaults... > Keys (this PC)**: click one, press the new key.) (J does nothing while you type in the chat
or console, while a note is open, or in the editor.) Its key is also shown at the hotbar: after Raft's notebook tab
(**T**) come the journal's tab (**J**, an open book) and, while the level up system is on in the world, the stats
page's (**K**, a star). They are copies of Raft's own key tabs and only remind you of the key (press the key; the tab is
not a button): they show and hide with Raft's own key tabs, and say the new key at once when you change it in
**Defaults... > Keys (this PC)**.

![The journal](images/world-journal.jpg)
*The journal: the custom quests done at the top, the Progress panel (quests, islands reached, notes, story items, journal pages, overall - hover a line for what it counts), the story items at the top left, the pages under Quest Pages by island with its quest and its notes found ("(1/1 notes)"), and the page "The vault" open, signed with the island, its quest and the day.*

**What's in it**

- **Story items** (top left): keys, map pieces, logs... that the islands' builders made. Each shows its picture and
  name, with **×2**, **×3**... when the crew has more than one. Past eight, the list scrolls (the mouse wheel or its bar). Click one to see its picture and description on the
  paper. They come from chests, trigger zones, quest rewards and island events; a message tells you ("Story item: Old
  key (J: journal)"). An item that is **used up** (say a key a locked door takes) leaves the journal.
  An item its builder linked to one of Raft's own items has a **Take out: <item>** button over its description: it
  takes one out of the journal into your inventory as that Raft item (the crew has one less; the host decides, so two
  players can't take out the last one twice). It can't be put back.
- **Quest Pages** (bottom left), **by island** - each island's quest line: each island's pages under its name and its quest's title (**√ done** once the
  quest is done, the whole line in light green) and how many of its notes you have found, **(5/7 notes)** - so you know if some are still left on it - the island you were at last first, its newest page first - so notes of several islands don't get
  mixed up. They are every note you read on a custom island - the first time anyone reads it, once, with its title -
  pages an island writes when something happens (an island event "write a journal page"), and, under **Other pages**,
  the frequencies a world plan gives out ("Tune the Receiver to #4821"). Click a page to read it on the paper, signed
  with the island, its quest and the day it was found.
- **QUESTS 7 / 20 · 35%** (top, next to the title): how many of this world's **custom islands' quests** are done,
  with a green bar that fills as you go. Raft's own story (Radio Tower ... Utopia) isn't counted - the journal is the
  custom islands' book. It counts:
  - **a world plan's own islands in the story** (the main quest islands, such as Wreckers' Cove after the Radio Tower
    in The Long Voyage): done as the story counts them - by default when their quest is done;
  - **the plan's other islands that have a quest** (side trips, a quest chain...): counted **from the start of the
    world**, even before they come, so the total shows how much there is to do. One that comes as a new island of a
    map type or from a list counts once it has come (until then nobody knows what it is);
  - **every other island with a quest** that has come to the world: by chance while sailing, brought by another
    island's rule, the world randomizer's treasure hunts. These add to the total as you meet them.
  - **the islands ticked for this world that have a quest and haven't come yet** (while random islands are on - the
    "Islands while sailing" list, [9.5](#95-islands-while-sailing)): counted **from the start**, so 47 ticked islands
    with 44 quests make the total at least 44. One that comes moves to the line above; one that is unticked or an
    island without a quest isn't counted.

  An island's quest counts once, however many rules name it; an island with several quests counts each of them. **Click the count** for the whole list on the paper, by kind:
  √ done, – still to do. The count is the same for every player and is worked out again every 2 seconds while the
  journal is open, so a quest another player finishes shows at once.

  **The end in sight.** When **90%** of the world's quests are done, every player gets a banner: *"Nearing the end -
  You are nearing the end: 18 of 20 quests done (90%). The journal (J) lists what is left."* At **100%** it says *"The
  whole quest line is done - You have completed the whole quest line: all 20 quests of this world are done!"*, and the
  count reads **ALL DONE**. Each banner comes once per world (it is remembered with the world, for every host), and a
  world that gets to 100% in one go shows only the second. A world with fewer than 5 quests has no banners: there, one
  island's quest isn't a quest line. Islands with quests that come later (by chance while sailing) add to the count
  again, but the banners don't come twice.

  ![The quests list in the journal](images/world-journal-quests.jpg)
  *A click on the count: the custom quests by kind, done and still to do.*
- **Progress** (top right, over the page): how far the crew has come in this world, each as found / total - hover a
  line to see what it counts:
  - **Quests** - quests done, and the per cent (the same as the bar at the top);
  - **Islands reached** - custom islands someone has set foot on, of those in the world, those its plan will bring and the saved islands ticked for the world (World settings, Islands while sailing) that can still come;
  - **Notes found** - notes read of the notes with a text on those islands (each island's line under Quest Pages has
    its own, "(5/7 notes)");
  - **Story items found** - of those the islands have; one used up since (a key a door took) still counts;
  - **Journal pages** - the pages all the islands can give: their notes and the pages their events write (a side
    quest's are in the journal, a main story island's in Raft's notebook);
  - **Overall** - all of it together, with the per cent.

  What counts is what this world has: the custom islands in it and the saved islands its plan brings, counted from the
  start of the world (also those still to come). An island made new from a map type counts once it has come, and an
  island that comes by chance adds to every line when it comes. Raft's own islands aren't counted (their notes
  are in Raft's notebook, T). An empty journal says where to look.
- **Daily quest** (under the page, while the world option is on, [9.4](#daily-quest)): today's task and how far along,
  with a bar, the reward, whether it's done (and whether you got the reward), and when the next one comes (at
  daybreak). The task is also at the top of the quests list.

**One journal for the whole crew.** Everyone in the world shares it: a note one player reads is in everyone's
journal, and a key one player finds opens the door for all. The host keeps it, a player who joins gets it, it is
saved with the world, and it goes back with an older save ([8](#8-playing-together)).

**The journal and quests.** Besides the count and its list (above), the journal doesn't show a quest's steps: an island's quest is shown in the **quest panel** on the
right while you are **at that island** ([Quests](#quests)), and it leaves when you sail away. But the journal is what
some steps count:

- **Find (a number of) a story item** counts that story item in the journal - so an item found before the quest
  counts too, and so does one used up since (a key a door took).
- **Find pages on this island** counts the journal's pages from that island's notes and events; **on any island**
  counts every page.

So when a quest step waits for items or pages, press J to see what the crew has.

**The journal and Raft's notebook: side quests and the main story.** A world plan's **main story** islands are in
**Raft's own notebook (T)**; everything else - the **side quests** - is in the journal:

| | Raft's notebook (T) | The journal (J) |
|---|---|---|
| **Islands** | Raft's story islands, and the plan's **main story** islands (a tab each, in story order) | **Side quests**: every custom island outside the plan's main story |
| **Pages** | Raft's notes; for a main story island its intro, its quest's steps and the notes read on it | The notes of side quest islands, pages their events write, and their frequencies |
| **Items** | Raft's quest items, and the main story's story items (Found items) | The side quests' story items |
| **The Receiver** | Raft's frequencies, and each main story island's #frequency on its tab | A page with each side quest island's frequency |
| **Quests** | The main story islands' steps (crossed out as they are done) | How far the main story is, and the side quests' list; a side quest's steps show in the quest panel at its island |
| **Without the mod** | Raft's own notebook: the mod's tabs and pages aren't in Raft's save, they are made again when the world loads | Kept in the mod's file of the world: it comes back with the mod |

More: [The main story in Raft's notebook, side quests in the journal](#the-main-story-in-rafts-notebook-side-quests-in-the-journal).

Story items aren't in your inventory: the crew holds them (in the journal, or in Raft's Found items for the main
story), like Raft holds its quest items outside the inventory.

---

## 4. Building your own island: the editor

Click **EDITOR** in the main menu and, in the start window, **An island**: **New island**, **Open an island...** or
**Continue** with the island you made last ([Three things you can make](#three-things-you-can-make)). A loading box shows what it is doing ("Opening the editor", then "Loading Raft's
objects from its islands" with a bar) until the editor is ready: a few seconds the first time after starting Raft,
less after that. The editor opens on the sea with an empty build area.

**New to the editor?** **Camera...** at the right of the status bar lists every mouse button and key of the editor in
one place: the camera, the tabs, undo and save, and the Terrain and Objects keys ([4.1](#41-the-screen)).

![The loading box](images/editor-loading.jpg)
*The box that covers the screen while the editor opens.*

### 4.1 The screen

![The editor: Terrain tab](images/editor-terrain.jpg)
*The editor on the Terrain tab.*

- **Top bar:** New, Open, Save, Save as · Undo, Redo · the three tabs **Terrain**, **Objects**, **Island** (F1, F2, F3)
  · **Generate** (the island generator), **Light** (the time of day, below), **Test** (try the island in a world,
  [4.9](#49-trying-the-island-in-a-world-test)), **Use in a plan...**, **Main menu**. The island's name is at the top left.
  **Use in a plan...** puts this island in a world plan: pick a plan (or **A new world plan...** and name it), then
  **Next in the Main story line** (the Receiver leads players to it after the story's last island, before Raft's ending
  Utopia) or **Another island (comes on its own)** (at the start, ahead of the raft; change WHEN and WHERE on its card).
  The plan opens on it. An island without a name must be saved first. If you came from a plan's **Edit island**, this
  button reads **Back to the plan**: it saves the island and returns to the plan. Unlike **Save as**, which only saves
  the island under a new name, it puts the island into a plan; the **?** after it explains this when you hover it.
  **Main menu** returns to the start window.
- **Tool panel** (left): the tab's tools in bordered groups. The lit button is the one in use.
- **Status bar** (bottom): explains the tool, or the button under the mouse. On the right: where the camera is, and
  **Camera...** (every mouse button and key of the editor, and the camera's settings).
- **The blue plane is the sea.** Everything below it is under water in the game.

**Moving the camera** (like Unity's scene view):

| Do this | To |
|---|---|
| **Right-drag** | look around (dragging up tilts the view up, as in most games; the settings can turn that round); while held, **WASD** flies where you look, **Q/E** go down and up, the **wheel** sets the speed |
| **WASD** or arrows | move over the island at the same height |
| **Space** / **C** | go straight up / straight down, whichever way the camera looks (the Terrain and Objects tabs' tips say so too) |
| **Middle-drag** | pan (the ground follows the cursor) |
| **Alt + left-drag** | orbit around the selection, or the middle of the view |
| **Wheel** | zoom towards what is under the cursor |
| **Alt + right-drag** | zoom in (drag right or up) and out (left or down), towards the middle of the view |
| **F** | frame the selection, or the whole island |
| **Shift** | three times faster |

**The camera's settings and every key.** **Camera...** at the right of the status bar opens **EDITOR CAMERA AND
CONTROLS**, which lists every mouse button and key of the editor in two columns. On the left, **Camera**: the controls
above (right mouse + drag, WASD, Q/E and wheel; WASD or arrows; Space/C; middle drag; the wheel; Alt + left drag;
Alt + right drag; F; Shift), and under them this PC's settings: **Look sensitivity**, **Move speed** (the right mouse + wheel speed, kept between sessions),
**Zoom speed** and **Pan speed** (each x1 = the mod's own, a slider from slower to faster), **Drag up: look up / down**
(click to switch; up, the default, looks up as in most games, down tilts the view as if you dragged the scene) and
**Wheel up: zoom in / out** (the wheel turned round). **Mod's own** puts them all back (dragging up looks up); **Close** or Esc closes the window. They are
saved a moment after a change, in `world_rules.txt` ([section 10](#10-settings-files)), and are also in
**Defaults... > Editor camera (this PC)**. On the right:

- **Anywhere in the editor:** F1 / F2 / F3 the Terrain, Objects and Island tab; Ctrl+Z undo; Ctrl+Y or Ctrl+Shift+Z
  redo; Ctrl+S / Ctrl+O save the island / open one; Esc closes a window or stops placing.
- **Terrain tab:** left drag uses the brush; Shift + drag with **Noise** takes the bumps away; Q/E turn a stamp.
- **Objects tab:** a left click places the picked object or selects; Shift + click keeps placing or adds to the
  selection; Ctrl + click takes one out of it; a left drag on the ground is a box select (Shift adds to it); Q/E and
  [ / ] turn and resize while placing; 1 / 2 / 3 / 4 the move / turn / scale / all handles; X the world's arrows or
  the object's own; P turns around each one or the middle; Ctrl held snaps while dragging (0.25 m, 15°); Ctrl+D /
  Delete duplicate / delete the selection; Ctrl+C / Ctrl+V copy / paste, also to another island; Ctrl+A selects
  everything.

Undo and redo everything with **Ctrl+Z** / **Ctrl+Y** (or Ctrl+Shift+Z); save with **Ctrl+S**, open with **Ctrl+O**.

**Light: the time of day.** The top bar's **Light** list has **Morning, Noon, Evening, Night** and
**Overcast** (click it and pick one): the sun's height, colour and shadows, the sky's light on everything and a haze far out change, so you
can see how the island will look at that time in Raft (a dark cave mouth at night, a beach in the evening sun). It is
only how the editor shows the island - nothing is saved in the island. Your choice is kept for the next time you open
the editor (Noon the first time).

**Raft's sky and sea.** On the Island tab, **View: Raft's sky and sea** shows the island under Raft's own sky and on its
real ocean (with waves) instead of the editor's plain sky and blue plane - the Light list then sets the hour of Raft's
sky. It takes effect the next time you open the editor from the main menu; off again works at once.

![Raft's sky and sea in the editor](images/editor-skysea.jpg)
*An island in the editor under Raft's own sky, on Raft's ocean (Noon).*

![The editor light](images/editor-light.jpg)
*The same island at Morning, Noon, Evening and Night.*

**Two small windows that ask you something** come up in many places and always work the same way.

- **A list to choose from.** Its title is in capitals (MAP TYPE, BRING WHICH ISLAND, WORLD PLAN...), under it a
  **Search...** box (type part of a name; the count at the top right says how many rows are shown) and one row per
  choice with a short line about it. Click a row to pick it - the window closes - or press **Cancel** or Esc to close it
  without choosing. A few lists have an **X** at the end of a row that deletes that one (the row goes, the list stays
  open), and an empty list says why it is empty. You meet it behind the **…** beside a name in **Behaviour &
  events...**, in the quest editor's **When the quest is done, bring a new island**, in the World plan window (the
  island, the map type, **Add rules from a template**, the plans) and the New main story line helper, and when the editor
  offers the unsaved work it kept.
- **A name to type**, for **Save as group...**, **Save stamp...**, **Save generator preset...** and the name of a
  world plan. A line says what will be kept, the field holds the name (up to 48 letters), **OK** or Enter keeps it,
  **Cancel** or Esc doesn't. A name that can't be a file name stays open and says why in red: it can't contain
  `\ / : * ? " < > |`, be longer than 60 letters, begin with a space, end with a space or a dot, or be one of Windows'
  own names such as CON.

**Esc** closes the mod's windows the topmost one first, one per press, so the window under it stays open. Esc in a
text field doesn't lose what you typed. A window with unsaved changes (the World plan window, a quest, a note, story
items, Behaviour & events) asks first: **Save and close**, **Close without saving** or **Keep editing**.

### 4.2 Shaping the land (Terrain tab)

- **Sculpt:** **Raise**, **Lower**, **Flatten**, **Smooth**. Hold the left mouse button on the ground; the white ring
  shows the brush. Objects standing on the ground you sculpt **go up and down with it** (trees, rocks, huts: lowering
  the ground no longer leaves them in the air, raising it no longer buries them); what stands higher, on a deck or a
  roof, stays. **Ctrl+Z** undoes the stroke and the objects' moves together.
  **Flatten to** (a field in the brush settings): type a height in metres above the sea (for example 4) and **Flatten**
  levels the ground to exactly that height, as a recipe's `brush flatten ... h=4` does. Leave it empty to flatten to the
  height where the stroke starts.
  **Noise** roughens the ground under the brush (**Noise size**: small bumps or big swells; hold **Shift** at the start
  of a stroke to take the same noise off again). **Erode** lets steep ground slide down until no slope is steeper than
  **Talus** - cliffs crumble into screes, spikes into hills; the ground is moved, not lost.
- **Paint ground:** the style's four textures (for a tropical island Sand, Grass, Rock, Seabed). **Auto** textures an
  area by its height and slope again. **Mix** adds a second style's four textures (eight in all): a tropical island
  with a snowy peak, a desert with forest ground. Older versions of the mod show the first style's textures there.
- **Brush:** size, strength and **Edge**: **Smooth** (soft all the way out, as before), **Linear** or **Hard** (full
  strength up to a thin rim - for terraces and sharp paint borders).
- **Stamps:** click the ground to put down a **Hill**, **Peak**, **Crater**, **Mesa**, **Lagoon** or **Ridge**, as big
  as the brush (Q/E turn it). **Save stamp...** keeps the land under the brush as a stamp of your own; **Manage...** beside it renames or
  deletes your stamps.

**Sea floor.** On the Island tab, **Sea floor: Deep / Shallow** switches the sea around the island without making it
again: the land keeps its height above the sea, the untouched sea floor goes down (a slope falls away from the shore)
or up, and objects on the ground move with it. One Ctrl+Z undoes it.

**Island styles.** On the Island tab, **Style** ◄ ► steps through Tropical, Snowy, Desert, Forest and Volcanic (or click the name for the list): the
ground takes the style's textures, the paint buttons get its names, and the generator uses its plants and animals.

![The island styles](images/editor-styles.jpg)
*The same kind of island in four styles, as the generator makes them: tropical palms, snowy pines, desert red rock,
forest birches.*

### 4.3 Placing objects (Objects tab)

![The editor: Objects tab](images/editor-objects.jpg)
*The Objects tab: the tools on the left, the object browser on the right (713 of 1,947 objects loaded).*

The **object browser** on the right has **every object of Raft**, about 1,900, each with a picture: nature, harvestable
trees and rocks, Raft's 88 building blocks (to build huts or your own abandoned rafts), everything else you can build
on a raft, and the objects of Raft's story islands (they load the first time you open their category). **Search**
finds objects in every category.

**Things to gather** (in groups by the style of island they suit: tropical, snowy, desert, forest, volcanic, sea finds,
finds on land - a thing that suits several styles is under each) is the category of what a player can pick, cut or
dig: palms (also the small islands' palms), mangoes, banana trees, Tangaroa's trees, pines and birches to cut, a
**snowy pine** to cut (Raft's pine wearing Temperance's snow), Caravan Town's acacias, pineapples, melons, strawberries,
berry bushes, Balboa's mushrooms, flowers, Raft's finds on the sea floor - sand, clay, stone, iron and copper ore,
scrap, giant clams, silver algae, **seaweed** - the finds lying on the story islands' land (stone, scrap, titanium ore,
planks, plastic) and Raft's **dirt spots** (the mounds its big islands have; players dig them with the shovel for dirt) and Utopia's
**market goods** (the sacks, baskets and crates of bananas on its market stalls; picked, they give bananas).
They work for the player as on Raft's own islands: picked, or cut with the axe, they give Raft's items and come back
after the island's regrow days, the same for every player and after a reload. Raft has no date palm: desert islands
get Raft's palms and acacias. Those from Raft's story islands (banana and Tangaroa trees, acacias, strawberries,
mushrooms, the finds on land) load with their island's scene the first time they are wanted. **Honey**: Raft has no wild hives
(its honey comes from the beehives players build), so "Loot & chests" has a **Wild beehive** - Raft's beehive holding
three honeycombs, refilling after the island's regrow days like any chest.

**Raft's story pieces that use the player's quest tools.** Balboa's **ChoppableVines** come ready to cut: a player
with Raft's **machete** cuts them away (without one they are told they need it), and keeps the machete for the next
vines on any later island - the vines stay cut. Raft's **zipline lines** (**ZiplinePath** from Tangaroa,
**ZiplinePath_Landmark** from Caravan Town) keep Raft's own zipline: a player with the **zipline tool** rides them as
on Raft's islands. Search the browser for them. Select a line to set its **far end**: **Far end: where I look**
puts the far end's floor on the ground the camera looks at (keep it lower - a zipline runs downhill); in a world Raft
makes the line from the object to there (the editor still shows Raft's own line). **Raft's own** puts it back.
**Near end: where I look** moves the whole line so its **near end's** floor stands on the spot the camera looks at,
on the ground or on a deck you built, so players can step onto the line from there. A line under the buttons shows
both ends' positions (in the same metres as Place exactly), so you can set the ends to exact spots.

**Raft's vehicles** are in the browser under their island's category. Search for them by name:
- Caravan Town: the vans (**Van_1** to **Van_5**), the caravans, the bare caravan frame, the toy boat (**BoatMini**)
  and the rocket.
- Varuna Point: the excavator, forklift, wagons and the huge harbour crane (**VP_ApexCrane**).
- Temperance: the snowmobiles (**TP_SnowVehicle01/02**).
- Utopia: the two cranes.
- The radio tower's plastic boat (**RT_PlasticBoat**), the crashed **Airplane** and the **BoatStranded**.

Placed, a vehicle rests on its wheels, tracks or hull and is solid: players stand on it and bump into it.

**Buried treasure** (in "Loot & chests"; a red cross on a mound in the editor) is Raft's own treasure for the
**metal detector** and the **shovel**: in a world the detector beeps as players come near it, three digs bring up the
chest, and the chest gives Raft's treasure loot. Once dug up it stays gone (also for the other players, and after
saving and loading) and is buried again after the island's regrow days. Nothing shows above the ground - players
need the detector, so a note or a quest step can hint where to search.

Select a buried treasure for its own settings: **Extra loot...** chooses items its chest gives **besides** Raft's
treasure loot - Raft's items or the island's story items - once, to whoever picks the chest up (**None** takes them
out again). Raft's shovel only digs where the detector has found the treasure, so without a detector players find
nothing. A treasure that holds a **story item** never comes back once dug up: dug for one player, it is dug for the whole
crew, and the story item goes to the crew.

**Keycard doors.** Raft's card reader door (**PlantationDoor**, the Tangaroa card reader) and its other key doors
(Utopia's entrance and hut doors, Varuna's garage, Temperance's reactor door, the padlocked door) are ready pieces:
select one for its **Keycard door** (or **Key door**) settings. **Card** chooses the story item that opens it - one of
the island's own story items or one of Raft's quest items (the keycards first); **None** lets anyone open it.
**Card used up** takes the card when the door opens; not ticked, the crew keeps it for other doors. Without the card the
door stays shut and says so; with it the reader beeps the card through (Raft's own sound) and the door goes, for every
player and after saving and loading. **Check** warns about a key door that no story item opens.

**Scatter** (the row under the browser) spreads many of the **last object you picked** round the point you are
looking at: **how many** (1-200, 8 at first), within a **radius** in metres (1-300, 15), and **keep** (0-30, 3) - not within that many metres of anything
already on the island (its buildings, its quest's objects, its plants). **On land / Under water / Anywhere** (click
to change it) says where they go:
- under water: 0.6 to 40 m down;
- anywhere: land and sea floor alike;
- Raft's sea finds go under water when the button says On land.

**Seed**: type a number and the same seed spreads them on the same spots every time, as a recipe's `scatter ... seed=`
does. Leave it empty for new spots each time; the message says which seed was used. Each is set down on the ground as the placer does; when the area has no
room for all of them it says how many it placed, and **Ctrl+Z** takes the whole scatter back.
**Air pocket** (in the Placing group, under Random / Slope / Grid) puts a ready air pocket in the middle of the view, as one undo step:
- Raft's air-pocket object;
- a trigger zone with **Air** on (radius 2.5 m, "An air pocket - breathe.");
- an atmosphere zone with bubbles.

Put it under water, inside a wreck or a cave.
Objects from Raft's islands are placed **standing straight**: one that leant a little where Raft has it (a ladder
against a wall) stands up straight; a bigger lean - more than 25°, a boulder lying on its side - is its look and stays.

- **Place:** click an object in the browser, then click the ground. **Q/E** turn it, **[** and **]** resize it,
  **Shift+click** keeps placing, **Esc** stops. Hold **Ctrl** to nudge it with the mouse. On a slope an object goes down to the **lowest ground under its base**,
  so a house on legs or a van stands on all of its legs and wheels instead of its high side (with **Slope** on, it leans
  with the ground instead).
- **Select:** click a placed object; **Shift+click** adds more, **Ctrl+click** takes one out again.
- **Transform** (keys 1-4): Move, Turn, Scale or All, with the coloured handles. **X** switches the arrows between the
  world's directions and the object's own turn; **P** turns and scales around each object's own point or the middle of
  the selection; hold **Ctrl** while dragging to snap (0.25 m, 15°). An island holds at most 100 000 objects (the editor
  says so past 12 000: big islands take longer to appear in a world).
- **Selection:** **Ground** puts the selection down on the terrain - by its base: on a slope down to the lowest ground
  under it, so nothing of it stands in the air - **Duplicate** (Ctrl+D), **Deselect**, **Delete** (the Delete key). **Copy** (Ctrl+C)
  and **Paste** (Ctrl+V) move objects between islands: copy, open another island (or New), paste - they come with
  their spacing and settings, where the mouse points (Ctrl+V) or the middle of the screen (the button).
- **Selecting many:** drag on empty ground to select everything in a box (Shift adds to the selection); **All**
  (Ctrl+A) selects every object, **Same kind** every object like the selected ones. **List...** shows the island's
  objects by kind with how many: **Select** a kind, **Hide** it (not drawn, can't be picked) or **Lock** it (drawn, but
  clicks, boxes and All pass it by) while you edit around it - hidden and locked objects are still saved and in the
  game, and opening another island shows and unlocks everything. A search narrows the list; **Show all** and **Unlock all** undo every Hide and Lock.

![Placed objects](images/editor-placed-list.jpg)
*The Placed objects list: the signs hidden, the warthog herds locked.*
- **Place exactly** (the last group when one object is selected): its **Position** (X and Z in metres across the build
  area, Y in metres above the sea), **Turn** in degrees and **Size** (1 = as made, up to 10000), typed. Enter moves it; Ctrl+Z
  undoes. The fields follow the object when you move it with the gizmo.

![Place exactly](images/editor-place-exactly.jpg)
*A pasted sign placed exactly: X 510, 3 m above the sea, Z 520.5, turned 90 degrees, twice the size.*
- **Placing:** **Random** gives each placed object a random turn and size, **Slope** leans it with the ground, **Grid**
  snaps to Raft's 1.5 m building grid (Q/E then turn in 90° steps).
- **Groups:** select several objects and click **Save as group...**. The group appears under **My groups** at the top
  of the browser, to place on any island. **Manage...** beside it opens **My groups and stamps**: pick a group (or, on
  the Stamps tab, a stamp), type a new name and press **Rename**, or press **Delete** twice - it is moved to
  `Mods\DynamicIslands\deleted\groups` (or `...\stamps`), where you can get it back. Islands keep what was placed
  from a group: a placed group is separate objects.

![My groups and stamps](images/editor-groups-stamps.jpg)
*My groups and stamps: the saved groups (or stamps), the name to rename to, Rename and Delete.*

![A creature and a sign in the editor, a saved group in the browser](images/editor-groups.jpg)
*A herd of two warthogs (the pink marker) and a sign. "My groups" in the browser holds two saved groups.*

Selecting a single object shows its **inspector** in the tool panel: creature settings, a note, loot, a zone, a colour,
behaviours (see [section 5](#5-making-islands-come-alive)).

### 4.4 The Island tab

![The editor: Island tab](images/editor-island.jpg)
*The Island tab: style, height in the world, the generator, the name players see, the island's rules and its quest.*

- **Style** and **Height** in the world: **At sea** (0), **Flying** (60 m up) or **Sunken** (30 m under water), or any
  number from -100 to 250. The island stays where it is in the editor; the **blue plane moves to where the sea will be** in a world -
  60 m below a flying island's water line, 30 m above a sunken one's (it can be seen from below too), and back at the
  water line at 0.

| Sunken (-30 m) | Flying (60 m) |
|---|---|
| ![The sea 30 m above the island](images/editor-elevation-sunken.jpg) | ![The sea 60 m below the island](images/editor-elevation-flying.jpg) |

*The same island at two heights: the blue plane is the sea it will meet in a world.*
- **Shown to players:** the island's **name**, your name and a short welcome. Players see them as a banner when they arrive, but only if the island has a quest.
- **Rules:** how many in-game days until chopped trees, picked items, killed animals, looted chests and fired zones
  come back on this island (empty = the world's setting, 0 = never, at most 999).
  **Comes by chance after ... km sailed:** the earliest the spawn pool may bring this island while sailing, in km the
  world has sailed, with the sailing time it means under it ("40 km: about 5 h of sailing", at 8 km/h). Empty = any
  time, at most 192 km (about 24 h). A world plan still brings its islands when its rules say.
  (The level up system is a world option, not an island's: see [section 9.6](#96-the-level-up-system).)
- **Quest**, **Islands it brings**, **Island events**, **Story items**: see [section 6](#6-stories-quests-behaviours-story-items).
- **About this island** (the last group of the tab): how many objects the island has, what the height means **in a
  world** ("normal (at sea level)", "flying 60 m above the sea" or "under water, top of the land 30 m deeper than
  normal"), and how many objects the object list has loaded (and how many more can be loaded from Raft's islands).

### 4.5 The island generator

**Generate** (top bar) makes a whole island for you to start from. The **preview** map on the right follows every
change; the line under it says how big the island is and how many objects it will get. The **seed** picks one island
of all possible ones: the same seed and settings always give the same island, on every PC (whatever its language, its
`placeables.txt` or other mods). Every setting has a **?** to hover.
Generating replaces the island you have; **Ctrl+Z** brings it back. After **Generate** the window steps aside so you
see the new island at once (a message says what it made); **Generate** in the top bar opens it again, with your settings.

![The generator, Normal tab](images/generator-normal.jpg)
*The Normal tab: your presets, the style and layout, the size and height (with buttons for the size and height of
Raft's own small island, large island and Balboa), peaks, hills and the coast.*

- **Island:** the **style** and the **layout**: **Raft small island** (the first one), Raft large island, round, atoll,
  archipelago, sea stacks, plateau, marsh, crescent, twin peaks. The two Raft layouts take the outline and ground of
  one of Raft's own small (or big) islands - the seed picks which, and turns, mirrors and wobbles it - with a beach all
  round you wade onto from the raft (cliffs only where you ask for them). Until you generate something else, the
  generator opens on an island like Raft's small ones: Raft small island, about 40 m across and 12 m high, an uneven
  coast with a bay, no cliffs, objects as dense as on Raft's small islands; **Defaults** gives the same. After a
  **Randomize existing** or **Ready-made** island, the Normal tab still opens on its own last settings.
- **Size and height, coast and outline, land features:** the size (land about 16-560 m across), the highest point
  (3-160 m), 1-5 peaks and their shape, hills, coast, bays, beach, cliffs, stretch (up to 3 times as long as wide) and
  its direction, valleys, lakes, terraces, erosion.
- **Under water:** a **deep sea floor like Raft's** (a shelf about 10 m deep, then a drop-off) or a **shallow** one
  (a flat seabed 20 m down), the width of the shallow water, how steep the drop-off is, and the seabed (sand, rocky,
  or a reef ring).
- **Nature:** trees, bushes, rocks, beach things and harvestables, with quick buttons **None**, **Sparse**,
  **Like Raft**, **Dense** and **Jungle**. "Like Raft" places everything where Raft's own islands have it: a nearly
  bare beach, palms inland on grass, boulders on steep ground - and as thick on the land as there, kind by kind,
  measured on Raft's islands of the style: a big island like Raft's big ones (sparse, few things to pick up on the
  land), a small one like its small ones (bamboo, bushes and flowers close together, green nearly down to the water).
  Each slider's word says how it stands against Raft ("less than Raft", "like Raft", "dense"...). Thicker than Like
  Raft (**Dense**, **Jungle**) thickens the island's higher ground: its low beach and flats - under 4 m above the sea,
  where Raft's islands grow bamboo - stay as thick as Raft's, so a jungle island's beaches don't become fields of
  bamboo. And at Like Raft no kind is ever thicker on the land than on Raft's own islands of the style and size: a
  cluster of little islets, all shore, would otherwise get twice the bushes of Raft's small islands.
  Everything stands with **all of its base on the ground**:
  on a slope a rock, a bush or a log goes down to the lowest ground under it (its high side a little in the slope, as
  Raft's own are), and flat things such as snow drifts lie along gentle slopes and are left off steep ones. They are
  set down on the terrain's own surface (its triangles), so nothing hangs in the air over a steep, uneven slope.
- **Things to gather** (in Nature) and **Finds in the shallows** (in Life under water), from none to much, both off
  unless you move them: more of what players gather on the land, chosen by the style - palms, mango trees, pineapples,
  watermelons, bananas and flowers (tropical), pines, berry bushes and flowers (snowy), palms, pineapples, watermelons
  and flowers (desert), birches, pines, berry bushes and flowers (forest), palms, mango trees, pineapples and black
  flowers (volcanic), with dirt spots and up to three wild beehives on tropical, forest and volcanic islands - on fairly flat land, kept off the island's other objects; and Raft's sea finds where players
  reach them easily, 0.6-6 m down just off the shore: sand, clay, stones, metal and copper ore, scrap, giant clams and
  seaweed. With a slider all the way up, about 10 things per 1000 m² of land and 30 per 1000 m² of shallows.
  **Which kinds:** under each slider the style's kinds can be switched off one by one (**All kinds** puts them back);
  **How far out** (2-20 m deep, 6 by default) sets how deep the shallow finds may lie. The world randomizer's islands
  get both by its level, and **Defaults...** sets them for islands generated while sailing (off unless you turn them up).
- **Life under water:** corals, sea vines, kelp, rocks, stones, ores, giant clams and sunken barrels, placed like
  around Raft's own islands: each kind as close to the shore as there (boulders by the shore, rock formations on the
  drop-off) and as thick. The corals grow in **reefs** as Raft's do: a few tight patches some 15 m across (one per
  250 corals or so), 2-20 m down and 6-45 m out from the land, more on the slopes than on flat sand, with bare sand
  and rock between them - sea vines and kelp round them - never a carpet of coral over the whole sea floor. At Like
  Raft there are never more corals and plants per m² of sea floor than around Raft's own islands, nor more rocks than
  half as many again - an island with a wide shallow shelf gets more sea floor, not more rocks crowded along its
  beaches. Raft's resources stay where Raft has them: metal and copper ore, scrap, giant clams and
  silver algae only under water, never on the land - and every island with sea floor around it gets at least one of
  each that Raft's islands of its style have, so even a small island has its ore. The quick buttons **None**, **Sparse**, **Like Raft**, **Rich** and **Teeming** set all of these sliders at once. **Like Raft** is as dense as Raft's own reefs (about 160 corals and plants per 1000 m²
  2-10 m down, with sand between the reef patches); **Teeming**, the top of the sliders, is twice that. More than
  that carpets a shallow lagoon's floor - nothing like Raft. **Groups** (under Nature) makes the reefs tighter (or
  looser).
- **Animals:** hostile creatures (the style's own, or the kinds you click), how tough (Easy, Normal, Hard, Boss),
  friendly animals to catch, sea creatures (0-24 spots each).
- **Loot:** how many loot boxes (0-40), their lowest and highest **tier** (1: planks and plastic ... 5: titanium, explosive
  goo, batteries), in the open or hidden.
- **Buildings and caves** (the last group):
  - **Buildings: Off / On** - buildings on the island's open, level land, each with a chest (most with a note too, which
    counts in the journal's notes).
  - **Kind** (a ▼ list, or the arrows):
    - **Mixed** - a bit of everything that suits the island's style;
    - **Castaway huts** - Raft's thatch walls and roof on Raft's foundations, a hammock and a chest inside;
    - **Wooden cabins** - the same in Raft's wooden walls and roof, with a bed, a chest and a cabin log;
    - in both, now and then a thing or two to pick up lies on the floor beside the furniture (Raft's plastic or scrap,
      coming back after a few days like Raft's own);
    - **Wrecks and landmarks** - Raft's own set pieces, on the land and in the water: a boat run aground on the beach
      or sunk 5-14 m down off the coast, a plane crashed on the land or lying on the sea floor, a small boat pulled up
      the beach, a van, a caravan, a shack, a statue on high ground, a rocket's debris, and structures of Raft's radio
      tower pieces: a **lighthouse** near the coast and a **lookout mast** on high ground (two decks on four legs, ladders
      up, a chest on top), a **jetty** of Raft's foundations out over the water with a boat at its end, and a **ruin**
      (a room with half its walls broken or gone, a crate inside), and a **skyscraper** (12 x 9 m, 6 to 10 storeys of
      the tower's windowed walls, a ladder up its front to a railed roof with a lamp, the dish and a locker) - with a
      chest by the ones on land (Mixed includes them too);
    - the quest islands' **scenes**, made of their own props, each with a chest and a note: **Castaways' camp**,
      **Caravan outpost**, **Radio outpost**, **Scrapyard**, **Old market**, **Bear country** (with a bear nearby),
      **Frozen camp** and **Hotel garden**.

    A hut or cabin is 3 x 2 or 2 x 2 cells of Raft's building grid, open on one side. It stands level on the highest
    ground under it, with the ground built up under it (blended over 3 m, so nothing hangs in the air on a slope), and
    its roof rests on its walls and corner pillars as Raft's own building puts one.
  - **How many** - 0 to 25, at least 22 m apart (0: none). A small or steep island has room for fewer: the message after
    **Generate** says how many found a spot ("2 building(s) found no level spot").
  - **Caves: Off / On** - one of Raft's own cave pieces (Balboa's) set into the land, its mouth towards open, level
    ground, with a guard inside (a polar bear on a snowy island, a bear in a forest, a hyena in the desert, else a
    warthog or a rat) and a hoard. It needs a hill next to open, level land; if none fits, the message after **Generate** says so.
    In a world (not in the editor) every cave piece on an island - this one, the randomizer's dens and any you place
    yourself - gets a rock hill built over its back and sides, down to the ground and open only at its mouth, made of
    the island's own biggest rock (snowy rock on a snowy island). The hill stops where the cave's roof ends, in a face
    down to the ground, so the ground in front of the mouth and beside it stays free for paths and ledges.
    Balboa's vine cave is a whole rock already and gets none.
    Every cave piece's rock is drawn from outside as well as from inside (Raft draws it from inside only), and the
    island's ground where it runs through a cave's rooms is let down under the room's floor, or cut away where it lies
    wholly inside or runs steeply through (a hole dug down to a tunnel's end): from inside a cave you see only rock - no ground across a room, no sea or sky through a wall. The
    editor does the same as you work, and keeps the heights you sculpted (they are saved as you made them).
    Selene's room pieces (the station's walls, ceilings, floors, door frames and tunnels) are drawn from both sides too,
    in the editor and in a world: Raft draws them from inside only, so a room's outer wall - or its ceiling seen from
    above - would show the room through it.

  The scenes and the cave use objects of Raft's own islands: the first **Generate** with them loads those islands
  (a few seconds; the line says "Loading Raft's islands for the buildings and caves...") and then generates. The same
  settings and seed always give the same buildings in the same places. A preset keeps these settings too.

  ![Castaway huts](images/generator-buildings-huts.jpg)
  *Castaway huts: Raft's foundations, thatch walls and pillars, the roof resting on them, a hammock and a chest inside.*

  ![A lighthouse](images/generator-lighthouse.jpg)
  *Wrecks and landmarks: a lighthouse of Raft's radio tower pieces near the coast, two decks, ladders, a chest on top.*

  ![A jetty](images/generator-jetty.jpg)
  *A jetty of Raft's foundations out over the water from the beach, a small boat floating at its end.*

  ![A sunken plane](images/generator-sunken-plane.jpg)
  *A plane wreck lying on the sea floor off the coast (the editor shows the sea floor without water).*
- **Quest** (the very last group): **Quest steps**, 0 to 8 - a quest made together with the island, and everything it
  needs put on the island:
  - first a **castaway's note** to read where players come ashore;
  - then, chosen by the seed: a **lookout** to climb to (a trigger zone up high), **monsters** to defeat (the style's own:
    warthogs or rats, a bear in a forest, a polar bear on snow, hyenas in the desert), **map pieces** to collect from
    three small chests (a story item, in the journal), a **torn page** to read, a **supply crate** to open, **animals**
    to catch with Raft's net launcher;
  - last the **castaway's hoard** to open at the top of the island. With 1 step there is only a hidden hoard.
  - with **Buildings** (huts or cabins) a step is the **key in the castaway's hut**, and the hoard won't open without
    it; with a **Cave** the hoard is the one in the cave. The quest's title fits the steps it got.

  It replaces the island's quest (Ctrl+Z brings the old one back): the Island tab's **Quest** shows it, and you can
  change it there like any quest. On a small island a step whose place doesn't fit is left out; the message says how
  many steps it got. 0 leaves the island's quest as it is.

  ![A generated quest](images/generator-quest.jpg)
  *A generated island with its quest: the castaway's note by the landing, a lookout, map pieces, the hoard at the top.*

  **Raft's features**, 0 to 10 (same group): things Raft's story islands have the player do, different ones first - a
  **cache behind vines** (it shows once the vines are cut with the machete), **buried treasure** for the metal detector
  and the shovel, a **zipline** from the island's top down to its beach, a **strongbox behind a code panel** (the code is
  on a note elsewhere on the island), a **grove of wild beehives**, **dirt** for the shovel. Past six, Raft's story
  machinery (ready pieces, chapter 15.8): a **generator** to start with Raft's generator part (in a mechanic's toolbox
  elsewhere) and a **radio** beside it that then works and shows a supply drop, an **engine** to start with Raft's gas
  tank (in a fuel crate) that shows a locker, a **chest in a cage** cut open with Raft's bolt cutters, a **lift** at the
  foot of a cliff that carries players up. An **explorer's chest** near the shore holds the tools they need (machete,
  metal detector, shovel, zipline tool, bolt cutters). Six or fewer give the same islands as before.
- **My presets:** **Save these settings...** keeps them under a name, and each saved preset is a button that puts its
  settings back (its **×**, clicked twice, moves it to `deleted\presets`); **Defaults** puts every setting back (the seed and style stay).

![Under water, and a help popup](images/generator-underwater.jpg)
*Further down the Normal tab: the land features, the Under water group (Deep, like Raft) and the Nature quick buttons.
Hovering a ? shows its help (here Stretch).*

![Animals and loot](images/generator-animals-loot.jpg)
*The bottom of the Normal tab: things to collect, sunken barrels, animals (the kinds to click, the toughness) and loot.*

**Can players reach it?** Above the seed, a coloured line says whether a player arriving by raft can get onto the
island: **Easy** (beaches, walk to the top), **Reachable**, **Possible but tricky** (a jump onto a low ledge),
**Possible but unlikely**, or **Not from the raft without building** (cliffs all around: players need stairs, a
ladder or foundations). It is worked out with Raft's own player: how steep it can walk, how high it jumps.

![The reach line says building is needed](images/generator-reach.jpg)
*A cliff island: "Not from the raft without building: cliffs all around (the lowest ledge is 17 m above the water)."*

**Randomize existing:** click one of Raft's 33 islands (each has a picture). **Something new like it** makes a new
island with its size, height and look; **A variation of it** starts from the island's own ground and reshapes it
(stretch, mirror, roughen, coast, valleys...). This tab shows only what remakes the chosen island - and a **Quest**
group - not the Normal tab's other groups (its buildings and caves aren't used here). It remakes the island's
**ground and nature**, for a story island **its look** (below), and for one with designs also **what is built on
it**:

![Rebuild it: an oil rig](images/generator-rebuild-rig.jpg)
*Rebuild it: the Radio Tower rebuilt as an oil rig off the island - the tower's floors on legs, the dish, the windmill.*

- **Rebuild it** (shown for Raft's story islands): **Design** - something like the
  island built anew from **its own pieces** on the new ground, different with every seed (how high, how many storeys
  and decks, which walls have windows, which way it faces):
  - **A radio tower** - a station on the ground (its door, a power box, a locker), legs 6 to 18 m up to the radio room
    (the radio on its table, a chair, the radar screen, a locker), a roof with the dish, the windmill, a floodlight and
    a mast with a lamp; a ladder all the way up;
  - **An oil rig** - over the open sea off the island (water at least 4 m deep under all of it): four of the tower's
    floors on legs down to the sea floor, railings, a control room with the dish on its roof, a lit flare stack, the
    windmill, supplies, and ladders down to a moored boat;
  - **A lighthouse under construction** - on a headland (high ground with the sea close by): 4 to 6 storeys of the
    tower's walls and floors with a ladder up the outside, the top ones still being built - poles round it, the
    lamp's parts waiting by the door - or finished, with a lantern room of windows, lamps facing out and a lit mast;
  - **Random design** (picked by the seed), or **None** (only ground and nature).

  The other story islands have two designs each, built from their own pieces on a levelled pad of the new land (made,
  raised out of the sea, where the island has no flat land), turned by the seed, each with a container of loot:

  | Island | Designs |
  |---|---|
  | Balboa | **A logging camp** (the shack, a plank fence, tents, a table with a lantern, the generator and barrels, bear signs) · **A relay station** (a hut of the relay walls, decks and roof, the radio racks and desk inside, a porch with a railing and an antenna mast) |
  | Caravan Town | **A caravan circle** (caravans round the well and the raft monument, benches, the workbench, the mayor's chest) · **A market on scaffold decks** (caravans on the scaffold decks with steps, a workshop deck, crates and tyres) |
  | Tangaroa | **A seaside café** (sunshades over outdoor tables, a kitchen counter, the burger sign, a vending machine, plants) · **The founder's garden** (the statue among benches and plants, the grand piano under a sunshade, the board room's table set for a sales pitch) |
  | Varuna Point | **A building site** (the break room on its stilts, the excavator and forklift, dumpsters, rubble, beams, pipes, the skylift) · **A half-built tower** (a 22 m lift shaft, garage door frames, beams, the skylift) |
  | Temperance | **An igloo village** (a big dome and two small, the snowmobile shed, snow vehicles, tarp crates, ice pillars) · **A weather outpost** (the electrical building under the telephone antenna, the telescope, mirror housings) |
  | Utopia | **A water station** (water tanks on bamboo stands, pumps, the crane, covered crates) · **A market yard** (baskets and crates round silver tables, a dog cage, the harpoon platform) |
  | Vasagatan | **The lounge on the beach** (the stage, bars, sofas, carpets, lamps, the DJ's table) · **The engine yard** (the engines and control board, lockers, bunk beds) |

  Its furniture stands on the floors and its legs reach the ground or the sea floor: nothing of it floats. The pieces
  come from Raft's own island: the first Generate loads it (a few seconds).

  ![Rebuild it: the story islands' designs](images/generator-rebuild-story.jpg)
  *Rebuild it for the other story islands: two designs each, from their own pieces.*

For Raft's story islands, **Something new like it** and **A variation of it** also give the new island **its look** -
the original's own big pieces, laid out the way the original has them (the line under the picture names it):

| Island | Its look |
|---|---|
| Caravan Town (and its three small islands) | **Red rock mesas**: 2 to 8 flat-topped rocks with steep walls, some in twins joined by the rope bridge; ladders up a notch in the wall, a hut on its pillars (or an acacia or a dead tree) and often a caravan or van on top, round boulders and a caravan or van at the feet; caravans, vans, acacias, dead trees and round rocks lie all over the low sand between the mesas, as thick as on Raft's own. A variation turns the island's own high ground into mesas. |
| Tangaroa | **The dome**: a round island levelled about 9.5 m up, the dome's base, frame and glass, the hexagon roads and the tall tower in the middle. |
| Utopia | **Tall white towers**: Utopia's three towers with the room on top, and its four other towers further out, all rising out of the sea round a low island the way the original has them. |
| Varuna Point | **The motherlode**: two 84 m motherlode floors on four pillars over levelled rocky ground, walls round the sides (one side sometimes left open), the lift shaft and the excavator beside them, the apex crane on top and rubble and bricks on both floors. |
| Balboa (and its small islands) | **Giant firs** on the higher ground and the big rocks on its slopes. |
| Temperance | **Snow rocks**: the big unclimbable rocks, the telephone antenna on the highest point and the large igloo on the flat. |
| Radio Tower | **Low rocks**: a few rocks just out of the sea, the concrete footings of the tower's platforms on them and a plastic boat on the beach; the designs build the tower, and the satellite dish goes on the highest rock the design left free. |
| Vasagatan | **The liner's furniture washed up**: 2 to 5 heaps of its lounge couches, tables, chairs and bar stools on the beach and its safe further up (the designs build the liner's rooms on the land). |

The look uses its own random numbers, so the same seed gives the same ground and nature with or without it.

![Randomize existing](images/generator-randomize.jpg)
*Randomize existing: Raft's islands, measured, with a picture each. Here a variation of "Big Island OG".*

![A variation of one of Raft's islands](images/generated-variation.jpg)
*A variation made from one of Raft's big islands, opened in the editor.*

![A generated island in the editor](images/generated-island.jpg)
*A generated island in the editor, ready to change: its trees and bushes are ordinary objects you can move or delete.*

The editor's view under water shows what a deep sea floor looks like:

![Under water in the editor](images/editor-underwater.jpg)
*A generated island on the deep sea floor: the shelf and drop-off from above, the slope, the reef, the drop-off.*

### 4.6 Ready-made islands (map types)

The generator's **Ready-made (with content)** tab makes whole islands with a story: chests, notes, creatures, zones
and a quest, from a seed. Click a card, then **Make** (click twice): the island is saved as `gen-<type>-<seed>` and
opened for you to change. If that name is taken it gets -2, -3... (rename it with **Save as**: `gen-...` islands are left out of plans and random islands). A chest or note that would stand where players can't get without building (on a sea stack's
top, a cliff ledge) is moved to the nearest place they can reach.

![The Ready-made tab](images/generator-readymade.jpg)
*The Ready-made tab: plain islands of each style, and the map types with their content.*

![Map types](images/maptypes.jpg)
*Some map types: an archipelago (a castaway's caches), an atoll, sea stacks (a chest on the tallest), a boss island,
an old camp, and a treasure island in a world, its quest panel saying "Find the map on the beach".*

| Map type | What it is |
|---|---|
| Random island; Tropical, Snowy, Desert, Forest, Volcanic island | Any style and size (now and then a flying one); a plain island of that style |
| Sandbar | A tiny island with a few palms and a small chest: a rest stop |
| Atoll | A ring of land around a shallow lagoon |
| Archipelago | Several islets; a castaway's note and three caches |
| Sea stacks | Steep rock pillars; a chest on top of the tallest (build your way up) |
| Boss island | A plateau with cliffs and a ramp; the arena wakes a boss bear (a polar bear on a snowy one; at half health it roars, calls 2 more and gets faster: a [boss phase](#51-creatures)). Stepping in, wooden stakes burst up round the arena and shut the crew in (two minutes at most, so a player who dies can come back; stepping in again while the beast lives shuts them once more); the beast defeated, they sink and the spoils chest shows |
| Volcano, swamp, frozen spire | A tall volcano with embers; low land with pools and mist; a snowy peak with a cache on top |
| Treasure island | A map in a bottle on the beach leads to the X and a treasure chest |
| Old camp | An abandoned camp with a notice board and supplies: a good first island of a story |
| Sunken island, sky island | Under water, with corals, sunken barrels and puffer fish; floating 45-90 m up with a cache |
| Wreck, ghost raft | No land: an abandoned raft of Raft's own blocks, floating like a real raft (the deck just above the water). A wreck has barrels to loot; a ghost raft is small, medium or large with a note, and the large ones are guarded by rats and screechers ([9.4](#94-extra-options)) |
| Trader raft | No land: a trader's raft with a hut and three or four stalls that swap basic resources for seeds, a tree seed or a blueprint ([9.4](#trader-raft)) |
| Ghost ship | No land: a glowing wreck of Raft's blocks (a deck with railings, a cabin, a mast and a torn sail) with four chests of good loot and a crew of ghost rats and screechers ([9.4](#ghost-ship)) |
| Oddities, boss lair, large island | The world randomizer's islands ([section 9.3](#93-the-world-randomizer)) |

**Your own map types.** A map type can also be a text file: `Mods\DynamicIslands\maptypes\<name>.maptype`, read at
start (F10 `ReloadMapTypes` reads them again). Its help header explains every line: the land (styles, shape, radius,
height... or `settings = <built-in type>`), the height (flying, sunken), and the content - `content = <built-in type>`
reuses a built-in type's content, then your own lines add chests, notes, creatures, zones, atmosphere, objects and a quest
at spots like `top`, `beach`, `near:20`. F10 `ExportMapType <type>` writes a built-in type as a file to start from.
Your types show in the Ready-made tab and can be named in `spawnpool.txt` (`type:<name>`) and in plans like the built-in
ones, and an island pack can carry them. **`ReRollMapType <type> [seed]`** (F10, in the editor) puts a type's content
onto the island you are editing, without touching the land: one Ctrl+Z takes it off again.

**A map type file, line by line.** An example - a small desert island with a cache at the top, a bottle that points
to it, two rats and a two-step quest (every island this type makes is different, but always has these):

```
label = Lookout rock
description = A small desert rock with a cache at its top
title = Lookout Rock
settings = ranges
styles = desert
shape = round
radius = 60 - 70
height = 20 - 25
roughness = 0.4
density = 0.3
peaks = 2
set = Hostiles 0
chest = Loot_Chest | top:0.5 | Lookout cache | Treasure | Found it.\nWell done.
note = Note_Bottle | dry | Old map | Climb to the top.
creature = Rat | near:30 | 2 | Hard
zone = top:0.5 | lookout-top | 6 | The top!
atmosphere = mid | 40 | #334455 | 0.4 | #FFEEDD | 0.3 | mist
quest = The lookout | Look around. | Done! |
step = read | Old map | 1 | Read the map
step = open | Lookout cache | 1 |
```

| Line | What it says |
|---|---|
| `label`, `description`, `title` | The name in lists, one line about it, and the banner players see on arrival (empty: none; an island without a quest shows no banner) |
| `flying = <chance> \| <min> - <max>`, `sunken = <m>` | How often it floats and how high; or under water, its top that many metres down |
| `settings = ranges` | The land is rolled from the lines below (`random`: as a random island; a built-in type's name: that type's land) |
| `styles`, `shape`, `radius`, `height`, `roughness`, `density`, `peaks`, `likeraft` | The land: one style is picked (name one twice to make it twice as likely); `shape` is round, atoll, archipelago, seastacks, plateau, marsh, crescent, twinpeaks, raftsmallisland or raftlargeisland; `60 - 70` is rolled, one number is fixed; `likeraft = large/small` makes trees and pickups as dense as on Raft's own islands |
| `set = <setting> <value>` | Any generator setting by name after the roll, as in a generator preset (`Hostiles`, `Cliffs`, `SeaFloor`, `Trees`...) |
| `content = <built-in type>` | That type's content first (a no-land type's raft too: `content = wreck`), then your own lines |
| `chest`, `note`, `creature`, `zone`, `atmosphere`, `object`, `clear` | The content, placed in order. Loot is `Basics`, `Metal`, `Food`, `Treasure` or items as in the loot editor (`Plank*12;Rope*4`); `\n` is a new line in a text |
| `quest`, then `step` lines | A quest (title, intro, done text, reward) and its steps as in the quest editor (`read`, `open`, `kill`, `reach`...) |

**Where things go** (`<where>`): `mid` (the middle), `top` (the highest ground; `top:0.5` within half the radius),
`dry` (dry flat ground), `beach`, `water` (a lagoon), `seabed`, `near:30` (dry ground within 30 m of the line before's
spot), `islet:1` / `stack:1` (the biggest islet or sea stack), `at:20,-10` (metres from the middle), and `@2` after any
of them puts the thing that many metres up (`top:0.6@2`). A spot that can't be found on a rolled island leaves that line
out. The file's own help at its top (from `ExportMapType`) lists every value.

### 4.7 Saving and sharing

**Save** (Ctrl+S) saves straight away once the island has its own name (a new island asks for one first: until then it
is only "myisland"); **Save as** and **Open** show the **Islands** window: a name, the height in the world, and your
saved islands (click = pick, double-click = open, Delete asks first and says which saved worlds, plans and other
islands' rules use the island). **Rename** gives the picked island a new name: press Rename, type the new name, press
Rename again. Everything that names it follows: the saved worlds that have it (every copy of their state, their kept
plans and quest rewards), the plans and other islands whose rules bring it, wait for its quest or stand near it, its
`spawnpool.txt` line and the copies kept for saved worlds. A deleted island isn't erased: it is moved to `Mods\DynamicIslands\deleted` -
move the file back into `Mods\DynamicIslands` to get it back. Copies kept for saved worlds (`<name>_<hash>`) aren't
in the list: **Tidy up** (in **My islands** or the Import window) clears the ones nothing uses.

**Give my worlds this version.** A saved world keeps playing the version of an island it was saved with. When you save
an island that worlds play in an older version, the editor offers **Give my worlds this version** (also in **My islands**
for the picked island): those worlds then play the island as you saved it now, with what was used there kept on the
same objects (unless rules or quest steps were removed or reordered: then fired rules and reached steps may land on
other ones), and the old copies go to `deleted\kept versions`. **Keep their version** leaves them as they are.

**My islands...** (in the Islands window) lists every island file at once: where it came from (**Mine**, the island
**Library**, **Generated** while sailing, copies **From hosts**), how many saved worlds use it (hover for their names)
and its **weight** in the random pool - type a number to give it a line of its own in `spawnpool.txt` (2 = twice as
likely, 0 = never by chance; grey = the weight every island not listed gets). Pick one to **Open**, **Rename** or
**Delete** it (Delete names who uses it first). **Tidy up** removes copies (from hosts, or kept for saved worlds) no saved world uses and moves
generated islands nothing uses to the `deleted` folder. The tabs and a search narrow the list.

**An island a world needs is put back.** When a saved world loads and one of its islands has no file any more (Tidy up
moved it, you deleted it in the editor, or a `gen-...` file was deleted by hand), the mod looks in
`Mods\DynamicIslands\deleted` (and its folders, such as `kept versions`) and copies it back: on the host the one with
the hash the world recorded, else the newest copy of that name; on a player's PC only a copy with the host's hash. A note
on screen says "'X' was put back from the deleted folder for this world". The copy in `deleted` stays. With no copy
there, a `gen-...` island is made again from its name (the same kind and seed, not quite the same island) and the host
is told; an island you made yourself can't be made again, so keep it.

![My islands](images/editor-my-islands.jpg)
*My islands: four test islands - one of mine used by a world, a copy from a host, one from the library, a generated one.*

**Nothing unsaved is lost:** opening another island, **New**, or a map type's **Make** while the island has unsaved
changes keeps them as its autosave first (the editor says so, and offers them back the next time it opens).

![The Islands window](images/editor-islands-window.jpg)
*The Islands window: the name and height, the saved islands with their date and size, and Save, Open, Rename, Delete.*

Each part of the Islands window has a **?**: hover it for what **Name**, **Height in the world**, the list and the
sharing buttons do. Height in the world is the same value as the Island tab's Height: 0 is at sea level, above 0 a
flying island (players build up to it), below 0 a sunken one (players dive to it).

Islands are files in `<Raft>\Mods\DynamicIslands\` (`<name>.island`). In multiplayer the host's island files are sent
to players who don't have them, so nobody needs to share files just to play together ([section 8](#8-playing-together)).

**Autosave.** While the island has changes you haven't saved, the editor keeps a copy every 3 minutes, and when you
leave to the main menu or quit Raft (`Mods\DynamicIslands\autosave\<name>.island` - not an island of yours, it never
turns up anywhere). If Raft closes before you saved (a crash, the power going), the next time the editor opens it
offers the unsaved work: **Open** it (then **Save** to keep it), **Throw them away**, or **Not now** (asked again next
time; **Throw them away** moves them to `Mods\DynamicIslands\deleted\autosave`, so they can still be got back; the **X** at the end of an island's row throws away only that island's work, the same way). Saving the island removes its autosave. Every change counts - the land, objects, their settings, and the
island's own settings on the Island tab (name, texts, style, height, rules, quest, events, story items), which Undo and
Redo also take back and forth; save before you close Raft all the same. Selecting objects isn't a change.

**Objects this Raft version doesn't have.** After a Raft update an object an island uses may be gone or renamed. In the
editor it shows as a **red box** in its place (the island says how many when it opens): it keeps its name, place, size
and settings, and saving writes it back unchanged, so a later version of Raft or the mod can show it again. Delete the
box if you don't want it. In a world such an object is left out, and the player is told once.

**Saving an island your saved worlds have.** A saved world remembers what was used on the island - trees chopped,
things picked up, chests looted, zones fired, doors opened. Every object of an island has its own number, kept through
your edits (a copy gets a new one), so the world knows which object each thing belongs to. So:
- changes to the **objects** reach those worlds the next time they load: moving them, changing their settings, the
  ground, the island's settings, **new objects**, and also **deleting objects**, changing their order or making one a
  chest - what was used stays with the same objects, and what belonged to a deleted object goes with it (the editor
  says which worlds got the new version). If you moved its ground, anything built on it there may no longer fit;
- **removing or reordering the island's rules or a quest's steps** (or changing what a step asks for) doesn't reach
  them, because a world keeps those by their place: those worlds keep playing the version they started with (the editor
  says so; a copy `<name>_<code>.island` is kept for them), and new worlds get your new version. The same happens, rarely,
  when the editor can't count an object's pickups yet (an object of Raft's other islands not loaded). To give a world
  the new version anyway, use **Give my worlds this version** (below), or save your changes under a new name
  (**Save as**) and bring that island into the world.

**Sharing an island or a plan: Export.** In the Islands window, pick an island and click **Export...**; in the World
plan window (a plan open) click **Export...**. The **Share** window says what goes along:
- an island takes the islands its rules bring with it ("Palm Cove + 1 island it brings: Treasure Cove"), and theirs, so
  its quest chain still works for whoever gets it;
- a plan takes every island it needs. Rules of the kind "a new island of a map type" need no file (every player's mod
  makes those; your own map types' `.maptype` files go along), and islands from the spawn pool come from each player's own islands;
- if one of those islands isn't saved, the export stops and names it.
- an island taken from someone else's entry that says no to changed versions can't be exported; the title and author must be filled in.

Fill in the **title**, **author** (your Steam name to start with), a one-line **summary**, a **description** (what
players find, how long it takes, what you had in mind, e.g. "best with Fierce monsters" - a pack never sets difficulty
or other World settings; the player's own choices always apply), **tags**, **players** and **length**, and whether
**others may change it and share their version** (they must credit you either way; a changed version of someone else's entry says **Based on <title> by <author>**:
that credit goes along). The **picture** is what the
editor's camera shows: close the window, move the view, open it again, or click **Take picture**; its middle becomes the
icon. **Export** writes a pack, `Mods\DynamicIslands\exports\<title>.zip` (the title in small letters with dashes, e.g. `palm-cove.zip`); **Open folder** shows it; **Share...** opens the
island library's page to send it in. Export the same island or plan again later and the pack is the next version of
the same entry (so whoever has it can update it).

![The Share window](images/library-share.jpg)
*The Share window: what goes along, the entry's info on the left, the picture of the editor's view on the right.*

**Getting islands from a pack: Import.** Put a pack (`.zip`) you got into `Mods\DynamicIslands\import` (**Open import
folder**; **Refresh** looks for new packs there), click **Import...** in the Islands window or the World plan window, and pick it: the window says what it
holds, who made it and its version. **Install** puts it in place:
- **Your own files are never overwritten.** If you have a different island with the same name, the pack's is installed
  as `Name (Pack title)` and the pack's plan and islands are changed to use that name. The same island (the very same
  file) is shared, not copied. A plan whose name you already use gets the author added: `My Voyage (Author)` - and
  your very own plan (the same file) stays yours: removing the pack later leaves it.
- A plan may bring only new islands of map types: such a pack holds no island files at all, and that's fine.
- **Story items with the same id:** a world's crew holds one of each story item id for all its islands. If the pack's
  islands use an id another installed pack uses too (two packs' `key`), Install gives the new pack's item an id of its
  own (`key-<pack id>`), changes its quests, chests and behaviours to match, and says so. In a world with both, each
  key opens only its own door. An update of the pack keeps the same new id.
- An **island** from a pack only turns up by chance while sailing if you tick **Also let it turn up while sailing** -
  then also in worlds you've already started with random islands (you can untick it for a new world in World
  settings, [9.5](#95-islands-while-sailing)). A **plan's** islands never turn up at random: they come when the plan
  brings them.
- A pack made with a **newer version** of the mod says so: "Install anyway" may leave out things this version doesn't
  know, and a quest that needs them may not be finishable. An island file of a newer *format* can't be read at all and
  is refused ("update the mod").
- A broken or harmful pack is refused with the reason, and nothing is written: over 50 MB (packed or unpacked), more
  than 64 files, an island over 64 MB unpacked, a file that would land outside the mod's folder, or any file but
  islands, plans, map types, `.json` and `.jpg` / `.png` pictures (so no programs). Every island in a pack is read in full
  before anything is installed, and a pack that fails half way puts back what it changed. Still, prefer the island
  library (every entry is looked at) to a pack from someone you don't know, and report a pack that fails
  ([chapter 14](#14-reporting-a-problem)).
- Installing a newer version of something you installed **updates** it. Worlds you've already started get the new
  version when its objects keep their order (a fixed quest, new ground, objects added), as with your own saves (4.7);
  when objects were removed or reordered they keep the version they started with (the old file stays for them, and
  the install says which) - **Give my worlds this version** in **My islands** moves them over with what was used kept
  on the same objects. If you changed one of its islands in the editor, yours is
  kept unless you tick **Replace files I changed**.

**Installed from packs** lists everything you installed, with **Remove** (click twice). Remove deletes what the entry
installed but keeps anything a saved world still uses (it says which world), and never touches your own islands and
plans. Under it, **Tidy up** (click twice) says what has piled up and clears it:
- copies of islands (`<name>_<code>`: from multiplayer hosts, or kept for saved worlds) that no saved world on this PC uses - also none of
  Raft's older saves of a world (a host sends them again if needed);
- generated islands (`gen-...`: made while sailing, or with **Make** and never given a name) that no saved world, plan,
  island rule or library entry uses - moved to `Mods\DynamicIslands\deleted`, so they can be got back;
- the mod's files of worlds you deleted in Raft (only worlds you hosted: the copies of worlds you joined are kept, so
  you can host them later) - moved to `Mods\DynamicIslands\worlds\removed`.

An entry whose files you deleted since (in the editor, or by hand) shows as not installed again, so it can be
downloaded again.

![The Import window](images/library-import.jpg)
*The Import window: packs in the import folder, what the picked one holds, and what's installed.*

Import is offered in the island editor (the Islands and World plan windows), never inside a running world.

**The island library: download plans and islands others made.** **ISLAND LIBRARY** in Raft's main menu (also **Get
more...** in the New Game box and **Library...** in the Islands window) opens the library: a public collection on
a shared Google Drive folder, where every
entry is looked at before it goes in.
- **World plans** and **Islands** tabs, a **search** (titles, authors, summaries, tags), featured entries first. Each
  row has the entry's icon, title, author and summary, and says **INSTALLED** or **UPDATE** when that applies (**FEATURED**: a featured entry you don't have).
  **Refresh** reads the library's list again at once; otherwise the window reads it again after 10 minutes.
- An island's row also says how hard it is: **Difficulty: Easy**, **Medium**, **Hard**, **Extreme** or **Impossible** - only a label, from the island's monsters: Easy none, Medium 1-5, Hard 6-20, Extreme 21-49, Impossible 50 or more. Each creature spawn point counts its number of
  creatures (a range such as 2..3 its highest number) plus the animals a boss calls; the animals to catch and the
  harmless ones (chickens, goats, llamas, puffins, the butler bot, turtles, dolphins, stingrays, whales, birds) and a
  creature made harmless (damage 0) don't count. It changes nothing in the game. The list takes it from the library's
  index; an island put in before the label came shows none until the index is made again.
- The picked entry shows its pictures (**<** **>**), description, author, version, date, size, how many islands, players
  and length, and for an island whether it has a quest (**Quest: yes**, or **No quest - an island to explore**, from its `quest` tag; a library maker gives every island with a quest that tag), and one button:
  - **Download** - downloads it and installs it the same way as Import (your own files are never overwritten, a plan's
    islands never turn up at random, an island only if you tick **Also turn up while sailing**);
  - **Update** - the library has a newer version than the one you have, or one of its files changed there since you
    downloaded it (even without a new version number): installs it (worlds you've already started get it when its
    objects keep their order, else keep the version they started with). If you changed some of its files in the editor
    or World plans since, the first click names them (and the saved worlds that use them) and offers three choices:
    - **Update, keep a copy** - the new version replaces them, and yours stay as **"name (yours)"** (an island copy
      never turns up at random; open it in the editor like any island);
    - **Keep my changes** - the rest is updated, your changed files stay as they are;
    - **Cancel** - nothing changes.

    When you load a world that has an island an update replaced, a message says so once;
  - **Installed** - you have the newest version. **Remove** (click twice) takes it away again, keeping what a saved
    world uses.
  - An entry made with a newer version of the mod says **Download anyway**, with the warning.

![The island library](images/library-window.jpg)
*The island library: the islands tab with three installed and Palm Cove picked; its picture, description and Download.*

- A picture on its way says **(loading)** beside its number, and one that can't be fetched says **(can't be loaded)**;
  the space stays empty meanwhile, never showing the entry before's picture.
- Every downloaded file is checked against the library's list (its size and a fingerprint); if one doesn't match,
  nothing is installed. A plan downloaded from the New Game box's **Get more plans...** is chosen there right away.
- The library goes online only while this window is open, and sends nothing but the downloads - no account, no Steam id.
  Without internet it says it can't read or reach the island library; packs someone sent you still install with **Import...**.
  `Mods\DynamicIslands\library.txt` can switch it off (`online = off`).

**The guide:** **Guide** at the top of the island library opens this guide (its PDF) - chapter 15 is about the
library's example islands and plans.

**Sharing yours in the library:** click **Submit yours...** at the top of the island library. It explains the three steps:
1. **Export** your island or plan in the editor (above), with a title, summary, description and picture;
2. **post the pack** (`.zip`) on the [Custom Islands Discord](https://discord.gg/U7DfKY9tN) with a few words about it;
3. it is **approved first**: every entry is looked at before it goes into the library, so it can take a while. Once it
   is in, every player sees it in the library.

Its buttons open the Discord, your exports folder and this section of the guide. By submitting, you say you made it and
share it under CC BY 4.0 (others may use it and must credit you).

![Submit yours](images/library-submit.jpg)
*Submit yours... in the island library: export, post it on the Discord, approved first.*

**Or with GitHub:** after an export, **Share...** in the Share window opens the library's
[Submit an island or plan](https://github.com/SwedenJohansson/CustomIslands-Library/issues/new?template=submit.yml)
form (a free GitHub account is needed) and the folder with your pack: drag the `.zip` in, tick the box (you made it and
share it under CC BY 4.0: others may use it and must credit you), send. To update it later, export it again (it keeps
its entry) and send that the same way.

### 4.8 Your first island, step by step

1. Main menu → **EDITOR**.
2. **Generate** → Normal tab: Style **Tropical**, click **Small island**, Nature **Like Raft** → **Generate**.
3. **Terrain** tab: raise a hill, paint a sandy path, stamp a **Lagoon**.
4. **Objects** tab: search "chest", place a **Chest**, click **Add items...** and pick some loot.
5. Place a **Sign** from "Notes & signs", click **Edit note...**, write a welcome.
6. **Island** tab: give it a name ("Skull Rock") and your name.
7. **Save** (Ctrl+S), type a name, Enter.
8. **Test** in the top bar: the island is tried in a world at once, and **Back to the editor** brings you back
   ([4.9](#49-trying-the-island-in-a-world-test)).
9. Main menu → **NEW WORLD** → create a world. Your island takes part in the Random islands plan, or press F10 and type
   `SpawnIsland <name> 250` to have it appear 250 m ahead right away.

**Making it look right.** Raft's pieces were made for Raft's own places, so a few rules keep an island looking like
one of Raft's:
- **Everything on the ground.** Nothing floats and nothing sinks out of sight: a rock, a tree or a hut stands with all of
  its base on the ground (on a slope the low side touches, the high side goes a little into it). Wrecks and boats lie
  on the sea floor or float at the water line, never in between.
- **Roofs on their pillars or walls,** never on nothing; a floor or foundation is not a roof. A building's walls reach
  the ground or a floor.
- **Caves and overhangs closed:** a cave's walls and roof meet with no gap the sky shows through, and a room built of
  Raft's room pieces has its back walls - look at it from inside and from behind.
- **No missing pieces:** a piece that shows as nothing or pink did not load; take it out or use another.
- **As thick as Raft's:** not more trees and bushes than Raft's own islands of the style (Nature **Like Raft**).
Look at the island from the sea, from the top and from inside its caves before you share it; **Test** shows it as a
world will.

**Next:** give the island a quest ([6.1](#61-quests)), then make a world plan that brings it into a world at the right
moment, with other islands after it ([7.2](#72-your-first-world-plan-step-by-step)).

### 4.9 Trying the island in a world (Test)

**Test** in the editor's top bar tries the island you are making in a real world, without leaving it by hand:

1. The island is saved (it needs a name: Save as first for a new one).
2. The mod goes to the main menu and loads the world **Custom Islands test** - it makes it the first time, as a normal
   world with the plan **No custom islands**, so nothing else turns up.
3. Your island is put beside the raft and you stand on it. Islands tried there before are taken away first. Their journal pages and story items go too, so a quest starts fresh.
   You arrive with the **starter kit** (below), hunger, thirst, health and oxygen full.
4. Walk around, open the chests, read the notes, meet the creatures, try the quest.
5. **Esc → Custom Islands → Back to the editor**: the test world is left **without saving** and the editor opens
   again with your island, where you left it.

While the test starts, a **STARTING THE TEST** box covers the screen (Raft's main menu loads the test world behind
it). **Cancel** takes you back to the editor. If the test can't start, you are back in the editor with the reason in the
status bar - click **Test** again.

**The starter kit.** So you can try everything on the island at once - fight its creatures, chop and dig, explore its
caves and dive at its reef - Test puts these in your inventory when you arrive (what doesn't fit is dropped at your
feet):

| For | Items |
|---|---|
| Fighting | a metal spear, a machete, a bow and 20 metal arrows |
| Gathering and building | a metal axe, a building hammer, a scrap hook, a metal fishing rod, a shovel (for buried treasure) |
| Caves and diving | a head light, flippers, an oxygen bottle |
| Food and water | 10 cooked meat, 3 canteens of fresh water |

Hunger, thirst, health and oxygen are filled up as well. In the test world, **Esc → Custom Islands** has two buttons
for it next to Back to the editor:
- **Kit: On / Off** - switch it off to try your island the way a new player meets it, with empty hands (from the next
  Test; the choice is remembered in `testkit.txt` in `Mods\DynamicIslands`);
- **Give the kit** - the kit again right now (lost your spear, ate the food), with hunger, thirst, health and oxygen
  full.

The test world is made with your last World settings choices (monsters, randomizer, extra options...); change them
there with **Esc → Custom Islands** if you want to try the island another way. It never counts as a saved world that
uses your islands (no kept copies or warnings for it).

Change something, **Test** again. The test world is a world like any other in Raft's Load Game list: you can load it
yourself too, and delete it from there when you no longer want it. (Trying an island in one of your own worlds is still
possible with F10 → `SpawnIsland <name> 250`.)

---

## 5. Making islands come alive

Select an object to see its **inspector** in the tool panel. Everything here can be undone.

### 5.1 Creatures

Open **Animals: hostile**, **Animals: catchable**, **Animals: harmless** or **Sea creatures** in the browser and place an animal: warthog,
pig, bear, mama bear, polar bear, hyena, rats, roach, bee swarm, screecher, Caravan Town's white screecher; chicken, goat, llama; Temperance's puffins and Utopia's butler bot
(harmless: they walk or fly about and never attack); puffer fish, angler fish,
turtle, stingray, dolphin, whale.

![The creature inspector](images/editor-creature.jpg)
*A warthog spot: a herd of 3, health ×2, damage ×1.5, comes back after 3 days, appears at once.*

- **Animals here:** 1 to 8 (a herd).
- **Easy / Normal / Hard / Boss**, or set **health** (up to ×12, for bosses), **damage** (up to ×4), **speed** and **size** yourself.
  **Size** goes from ×0.5 to ×3 of Raft's own animal (the marker in the editor shows it): its body and hit box grow
  with it, its health doesn't - give a big boss more **health** too. Players in multiplayer see the same size.
- **Comes back after** the regrow days, or **Never**.
- **Appears** at once, or **when a zone fires** (an ambush, see [5.4](#54-trigger-zones-and-ambushes)).
- **Colour** (every object): **None** keeps its own colours; the nine swatches tint it (a creature spot: every animal of
  it), and once it is tinted the **Strength** slider (0 to 100 %) says how strongly the colour shows. **Custom colour...**
  adds **Red**, **Green** and **Blue** sliders (0 to 255) for your own mix: the object's own colours are multiplied by
  the tint.
- **Danger** (every creature spot): **Zap** makes its animals shock a player who comes within 2 m - that much damage,
  at most every 2 s (Tangaroa's **butler bots** placed from the list zap 8). **In its phase: Only blasts hurt it**
  turns a boss's weapons-proof once its **Boss phase** starts: the players' blows glance off, and only a **hurt
  creatures** action ([6.2](#62-behaviour-and-events)) does harm - Varuna Point's **explosive barrels** and **pillars**
  are ready for it (used, each takes a third of the health of the creature spot named `boss`, then goes).
- **Boss phase** (hostile animals): click **none** to give the spot a second phase. Once per fight, when one of its
  animals falls to **Starts at health** (10 to 90 %, 50 % at first), it **calls** up to 6 more creatures (of **its own**
  kind or another hostile one) around itself, gets faster (**Speed then**, ×1.3 at first), sends a **signal**
  ("phase" unless you name one, for world plans and **signal** checks, see [6.2](#62-behaviour-and-events)) and runs
  its **phase** event (a message, a sound...). A world saved mid-fight comes back in the second phase, without calling
  again; once all its animals are beaten the next fight (after the regrow days) has its phase again. Under a harder
  monster difficulty the phase comes at the same share of health, after more hits. The **Boss** map type gives its
  beast a phase that calls 2 more and says "The beast roars in fury". Everyone sees it: the host runs it, and hits from
  any player count.

**Angler fish swim rounds** around their spot, through the open water near it (in Raft they follow a route of their
island's; here the mod lays one out in a ring around the spot, clear of the ground and of objects). Give them room:
a spot a few metres from walls and the sea floor, in water deeper than they are long.

In the editor a creature is a coloured marker with its name, or Raft's real model once you have been in a world since
starting Raft:

![Creatures with Raft's models](images/editor-creature-models.jpg)
*Creature spots with Raft's own models: a warthog, a llama, a bear, a chicken.*

### 5.2 Notes and signs

"Notes & signs" has a paper, a bundle of papers, an open book, a sign, a notice board and a message in a bottle, and
**any object can be made readable** (**Readable...**). A sign shows its note's title on its board, and a notice board shows lines of made-up writing on both faces, so players can tell there is something to read (the note itself opens with E). The title can be 60 characters long, the text 4000. **Ctrl+Enter** saves, **Esc** cancels. **Remove** makes the object unreadable again.

![A note on an object](images/editor-note.jpg)
*An object with a note: its title "Hidden treasure" and the start of the text.*

![The note editor](images/editor-note-editor.jpg)
*The note editor: the title, the text (Enter starts a new line), and a preview of the paper players will see.*

### 5.3 Chests and loot

"Loot & chests" has chests, a crate, a wooden box, barrels and a wild beehive (honeycomb), and **any object can hold loot** (**A chest...**).
**Fills up again** sets whether a looted chest is full again after the island's regrow days or never. A chest holds up to 12 kinds of items, up to 999 of each. **Empty** takes everything out; **Not a chest** makes the object plain again. A chest that
holds a **story item** never fills up again, whatever is chosen (the story's key or log comes once - the panel says so).

![The loot inspector](images/editor-loot.jpg)
*A chest's loot: planks, plastic, palm leaves, rope and nails. The Basics, Metal, Food and Treasure sets fill it
quickly; it fills up again after 3 days, or never.*

![The item picker](images/editor-loot-picker.jpg)
*Add items...: every item of Raft with its picture and a search. Click an item to add one, again to add more.*

**The abandoned raft crate.** "Loot & chests" also has Raft's own **Abandoned raft crate**: the box you grab on the
small abandoned rafts that drift by in Raft. In a world it works as there: a player takes it whole (look at it, **E**) and
gets a handful of random items from Raft's own loot for those rafts - now and then a cooking recipe or a mystery package.
Its contents **can't be chosen** (use a chest for chosen loot), and it **doesn't count for a quest's "Open a chest" step** -
the editor says so too, in the object list's hint and on the crate's own panel when you select it, and Check tells
you when a quest needs a chest and the island only has raft crates. Once taken it
stays gone, and comes back after the island's regrow days like harvested things ([4.4](#44-the-island-tab); 0 = never).

**A Raft store (Raft's trading post).** The **Islands** list has **TradingPost**: the trading post of Raft's big
islands - hut, register, lantern and radio - and in a world it **is a store**. Look at it and press **E**: Raft's own
store window opens with Raft's goods at Raft's prices, paid in **trash cubes** and **trade coins**, and players can
sell their trophy fish there as at Raft's posts. Raft's reputation (its three tiers) is Raft's own, one total for all
its posts and the mod's. Each store also sells the **story blueprints** of Raft's islands that the player hasn't found
yet (6 trash cubes each, one each), so a missed blueprint can always be had later - the window doesn't list the ones
the player already has. Each store keeps its own stock: what is sold is saved with the world and is the same for every
player (buying together works: a joined player's purchase goes through the host). Put it on the ground (**Ground**), on
a flat open spot with room in front of the counter. In an island recipe: `store [alias] at x z ground`.

**Raft's vending machine (Tangaroa's).** The **Islands** list also has **VendingMachine**: the vending machine of
Tangaroa's glass domes, and in a world it **works** as there. Its six buttons sell Raft's goods at Raft's prices: two
of one of Tangaroa's six plants for **one Tangaroa token** each. Press a button with a token on you (tokens are the
crew's, like Raft's quest items): the token is taken and the package drops into the hatch; press the hatch to take it.
While a package waits, the other buttons do nothing. A waiting package is saved with the world and is the same for every
player (a joined player's press goes through the host). Tokens come from Raft's **QuestItemPickup_Tangaroa_Token**
pickups: put a few near the machine. Put the machine on the ground (**Ground**) with room in front of it. In an island
recipe: `vending [alias] at x z ground`. Every library storyline has one on a story island (see
[15.4](#154-the-world-plans)). The decor copy **VendingMachine_w_Animations** stays a prop.

### 5.4 Trigger zones and ambushes

A **trigger zone** ("Zones & triggers") is an invisible sphere. When a player walks in it shows your **message**,
**gives items**, and wakes the creatures that wait for it. **Fires:** **Once** (for the first player - and ready again
after the island's regrow days, like loot), **Once ever** (never again in that world: for the story - an ambush, a
bridge shown) or **Every time** (each time a player walks in, at most every half minute). Zones are 1 to 50 m in radius. A trigger zone's name can be 24 characters long, its message 200.

![A trigger zone](images/editor-zone.jpg)
*A trigger zone (the orange sphere): its name, size 10 m, the message "You hear grunting...", fires once, and one
creature spot waits for it.*

![An ambush](images/editor-ambush.jpg)
*The warthog's **Appears** is set to "when zone-158 fires": its name tag says it waits. It appears the moment a player
walks into the zone.*

**Air pockets.** A trigger zone's **Air** row makes it an **air pocket**: a player inside it breathes - their breath is
full again - as in Raft's air pockets under Caravan Town. Raft's own air-pocket objects (the shack, the gas tank and
the container in Caravan Town's list) come without their air, so put an air-pocket zone on each, about as big as the
object. A zone **hidden** by a behaviour gives no air until it is shown: *The Abyss* in the library keeps its three
pockets empty until the air line's valve is opened (the valve's action shows them, with bubbles - [5.5](#55-atmosphere-and-sound)).

**Hazards.** A trigger zone's **Hazard** group makes the zone hurt the players standing in it (each player's own
machine, every second) unless something protects them: **Acid** and **Fumes** (a protective suit protects),
**Live water**, **Spikes** and **Cold** (damage per second, 0-50), **Darkness** (no damage: the screen goes black
unless the player **wears a headlight** - then they see a headlight's cone) and **Deep water** (the breath runs out
fast unless the player **wears an oxygen tank**). **Protected when** takes lines like the behaviour window's checks,
one holding is enough: `suit` (a protective suit is on: the **protective suit** action, or Raft's hazmat suit),
`wears|HeadLight*`, `holds|Torch`, `has|story:charm|1`, `state|Valve|closed`, `signal|power`; empty = the hazard's
own, `none` = nothing protects. A name ending in `*` matches every kind (`Axe*` = any axe), and the mod's upgrades
count as the item they improve. A message says why (at most every 6 s; your own line or the hazard's). A **hidden**
zone does nothing: switch live water off with a lever or a generator whose action **hide**s the zone.

**Invisible walls and ramps** (also in "Zones & triggers") are solid in the world but not seen: block a path, fence
an arena, or make a cliff climbable.

### 5.5 Atmosphere and sound

An **atmosphere zone** changes the fog colour, the light and adds particles (fireflies, mist, snow, embers, bubbles)
around a spot; a **sound zone** plays one of Raft's 500+ sounds while a player is inside (**While inside**, louder
towards the middle) or once on entering (**Once on entering**). **Choose sound...** picks the sound (► listens, ■
stops), the **Volume** slider sets how loud it is in the middle of the zone (5 to 100 %, 80 % to begin with), and the
zone has a size like any zone.
**Bubbles** rise from the zone's middle in a column, about 14 m high - under water, for an air pocket filling or a
vent; give the zone only bubbles (no fog or light) and it changes nothing else.
A **dark light tint** makes the inside really dark: Raft's sun, the sky's light and its reflections all take the tint
while you are in the zone - for a cave, a mine or a buried room (Old Mine Islet and Shelter Atoll in the library).

![An atmosphere zone](images/editor-atmosphere.jpg)
*An atmosphere zone: its size, fog colour and strength, light tint and strength, and particles (fireflies).*

![Inside the atmosphere zone](images/editor-atmosphere-inside.jpg)
*Fly the camera into the zone to see it: purple fog and fireflies.*

![The sound picker](images/editor-sound.jpg)
*Choose sound...: Raft's sounds with a search. ► listens, Use picks it.*

---

## 6. Stories: quests, behaviours, story items

### 6.1 Quests

**Island tab → Edit quest...**: a title, an introduction (shown when players arrive), up to 10 steps in order, a
reward and a closing message. (**+ Add a step** stops at 10. A quest file with more steps - the library's finales have
up to 40 - is read up to 99 steps, more are cut, and the editor shows them all.) Under **Reward and ending**, **Choose reward...** opens the ITEMS window - the island's story items first, then every item of Raft, with a
search; click an item to add one, again to add more, then **Done** - and the line beside the button lists what the
reward is ("Reward: Plank ×4, Rope ×2"). **None** takes the reward away. **When done** is the closing message shown with
"Quest complete". Raft's items go to the players near the island when the last step is done.

**Several quests on one island:** the buttons at the top of the quest editor are the island's quests - the **Main
quest** and **+ Another quest** (up to nine quests in all). Each has its own steps, reward and messages and is done on its own;
what a player does counts for every quest waiting for it. The panel shows the first quest not done yet ("+1 more"
when others are open), the journal lists each, and world plans and the story wait for the main quest - or for another
one by its number (a rule's "quest 2"). **Remove this quest** takes one off; the ones after it move up. **Remove quest**
at the bottom takes all of the island's quests off at once (one Ctrl+Z brings them back).

![The quest editor](images/editor-quest.jpg)
*The quest "The lost camp": go to the camp, read the diary, open the supplies, chase off 2 warthogs. The reward is
planks and rope, and when the quest is done it brings a saved island "Old camp" 700 m north, with a message and a
name on the Receiver.*

Steps: **go to** a trigger zone, **read** a note, **open** a chest (one of the mod's chests - Raft's abandoned raft crate doesn't count), **defeat** or **catch** a number of animals,
**collect** a number of a story item, **find** journal pages, find a **Blueprint** of Raft's. Each step's kind is a **▼ list** (each kind says what it
asks). Steps point at things on the island by name: the small **▾** beside the name lists the names this island has
for that kind - its trigger zones, note titles, chest titles or creatures - so you can pick one instead of typing it
(place the zones, notes, chests and creatures first). A **collect** step picks its story item with **…**. **When the quest is done, bring a new island** (main quest only) chooses from a
list too (nothing, a saved island, a new island of a map type), and so does its direction.

**Steps done in another order count too:** a chest opened or animals defeated before their step has come are
remembered, and count the moment their step comes - the guide's camp quest done backwards (warthogs, supplies, then
the diary) finishes when the diary is read. To keep the story in order anyway, hide a later step's chest or animals until
the step before shows them (**Behaviour & events...** > **At first: Hidden until shown**, for animals **Hidden (ambush)**, and a **show** action).

**Blueprint steps** ask for one of Raft's blueprints (`Blueprint_Fueltank`, ...; the **▾** lists them, Raft's story
blueprints first; empty = any of Raft's story blueprints). A player has it when it is in their inventory or they have
learned it at the research table, and the step counts while that player is at the island. So put the blueprint on the
island (a chest's loot, or lying about) and the step asks the player to find it. A player who missed it - or learned it
somewhere else - is not stuck: learned counts too, and every **Raft store** (5.3) sells the story blueprints a player
hasn't got, one of each, for 6 trash cubes. Check and the plan checker say when a blueprint step names a blueprint
Raft doesn't have, or one no island of the plan gives.

**Collect and find-pages steps** count what the whole crew holds, and finish while a player is at the island - so the
reward reaches someone. Two copies of the same island in one world each count their own: the pages read and story items
found on one copy don't finish the other's step (items found away from both count for both).

**Traps to avoid** when you make a quest (toggles in events that happen again, keys, rewards): see the notes under
6.2; **Check** finds most of them ([7.5](#75-check-finding-and-fixing-problems)). Play your adventure once with
**Test** ([4.9](#49-trying-the-island-in-a-world-test)), doing things in the wrong order on purpose. Steps find
things by name: renaming a note, zone or creature kind that a step points at breaks the step, so change the step with
it.

### 6.2 Behaviour and events

Select any object → **Behaviour & events...**. No code needed:

- **A name** that actions refer to (objects with the same name act together).
- **Movement:** spin, bob, move back and forth, or **open and close** like a door, gate, bridge or lift (with a
  Preview in the editor). A spin turns around the vertical, or with **Own axis** around the object's own up axis:
  tilt it first - a water wheel, a windmill's blades, a fan (Tide Farm's wheel in the library). With **Carries players**, players standing on a moving object ride along (a lift).
- **At first:** there, or **hidden until shown** (a hidden creature spot is an ambush).
- **Players can use it:** Raft's "press E" hint with your own text ("Pull the lever"). With a **keypad code** (the
  **Keypad code** field under **Behaviour & events...** in the object panel; it shows once players can use the object) a keypad opens first, as on Tangaroa:
  the right digits run the object's use (its checks and actions), a wrong code says so, and once opened it stays open
  for everyone. Put the code on a note somewhere on the island.
- **Collision:** Raft's own, walk through, one box, or solid.
- **When ... then:** when a player uses it, walks into a zone, reads a note, opens a chest, all the animals of a
  spot are defeated, a boss spot's second **phase** begins ([5.1](#51-creatures)), or a **laser beam** reaches it → show / hide objects, **show/hide** (hidden ones appear, shown ones
  disappear), open / close doors, **open/close** (open if closed, closed if open - a lever), say a message, give
  items, play a sound, teleport the player, send a signal, write a journal page, **unlock one of Raft's characters**
  for the player (its name or number; it is kept in the player's Raft profile, like Raft's own unlocks), or **wait**
  some seconds first: under each event, **+ Add an action** adds one, **+ Wait** a pause and **+ Only if...** a check.
  Each action is chosen from a **▼ list**
  that says what it does; **…** beside a name lists the names on this island. The actions that work on objects (show,
  hide, show/hide, open, close, open/close, teleport to) name them in the **name** field: leave it empty (or pick
  **(itself)** from the **…** list) and the action works on the object these actions belong to. **show/hide** and
  **open/close** change whatever state the object is in now, so in an event that can happen
  again (a zone that fires more than once, a chest that fills up, animals that come back) use **show**, **open**,
  **journal page** and **say** instead, or set the zone to **Fires: Once ever**. The event window warns with an amber
  line when such an event has a show/hide or open/close, and a zone's enter event has a **Fires once ever** button.
  **Keys and Raft items:** on the island's "when its quest is done" event and a creature spot's **defeat** event no
  player does it, so **has item / uses up item** checks of Raft items are left out (the window warns; the actions run
  without them). Use story items there: **Island tab > Story items...** (a name, a picture, a line of text), hand it
  out with a chest (**Add items...** lists them), a **give items** action or the quest's reward, and check it with
  **+ Only if...** > **uses up item** or **has item**. A note's actions run once, for its first reader: for something
  every player should get, use the quest's reward or a chest per player. A note's "read" and the island's "first
  come" happen **once per world**: whoever reads the note again (anyone) gets its messages and sounds, not its items or
  teleports again. After a **wait**, what changes the island (show, open, a signal, a journal page) happens even when the
  crew sails away or the host saves and quits meanwhile: it happens when the island loads again. What is for the player
  (a message, items, a teleport) is left out for a player who died or left the island meanwhile.
- **Only if ...:** the player **has** an item or story item (or **uses one up**, like a key), an object is open,
  closed, shown or hidden, a signal was sent, the quest reached a step (a ▼ list too). Turn a check round with **not**; ask for
  **all** or **any** of them. **Otherwise** say a message. (An island file written by hand or by a script can give
  **Otherwise** any action, not only a message; the editor shows only the message and keeps the other actions as they are.)
- **Wears, holds, suits and blasts** (2026-10-24): the checks **wears item** (a headlight, flippers, an oxygen tank)
  and **holds item** (in the player's hand: **binoculars** for a lookout - Raft's telescope counts too -, `Hook*` or
  `Axe*` for an opening only a hook or an axe gets through) look at the player who does it, so they are left out
  where no player does it, like **has item**. The action **protective suit** puts a suit on that player for some
  seconds (120 at first): acid and fumes zones don't hurt them. **hurt creatures** (shared) takes a share of the health
  (25 % at first) of the animals of the creature spots with that name. More ready pieces: Temperance's **key socket**
  (an item goes in, once: open = filled, sends `socket`; a puzzle checks the sockets' states), Varuna Point's
  **spotlights** and the radio tower's spotlight **keep creatures back** (the object panel's radius, 8 m: while shown,
  the island's animals and Raft's jellyfish keep out; hide it to switch it off), and Temperance's **snow vehicles**:
  broken down until a player brings 3 bolts, then each use carries the players on it 24 m along and back (they can't
  be driven: Raft's snowmobile is Temperance's own vehicle, made by its shed). A **generator** placed from the list
  wants **3** generator parts (the object panel's **Parts needed**, 1-6); old islands keep theirs.
- **Sounds and camera moments** (actions in the same ▼ list, shared by every player): **play sound/music** plays one of
  Raft's sound events or music cues (pick it with **…**, a list of every one Raft has), with a volume, **once** or on a
  **loop**, **near** the object (or the objects named) or for **everyone on the island**. Every player near the island
  hears it; it stops when they leave the island or it unloads, and a loop starts again when they come back, after a
  reload and for a player who joins later (a once sound is not played again). **stop sound** stops the sound of the
  objects named, or **all** of the island's. (The older **sound** action plays a sound for the one player only.)
  **camera moment** shows the view from a **marker** object (its name; empty = the object itself) toward an object to
  **look at** (empty = the way the marker faces) for some **seconds** (1-30), then glides back to the player; the
  player can't walk or look around meanwhile, and **Esc** skips it. Only players within its **range** of the marker
  (default 50 m) get it. Mark the spot with any small piece (a sign, a stone) and name it. **Check** warns about a sound
  Raft doesn't have and a marker or look-at name no object has.

![A lever opens a door](images/editor-behaviour.jpg)
*A log named "lever" that players can use ("Pull the lever"): it opens or closes the door and says "The door moves".*

![A locked door](images/editor-behaviour-checks.jpg)
*A door that opens only if the player has the story item "rusty key"; otherwise it says "It's locked. Maybe there's a
key somewhere..."*

**Island events** (Island tab): actions for when players first come to the island and when its quest is done.

![Island events](images/editor-island-events.jpg)
*Island events: what happens when players first come to the island, and when its quest is done.*

### 6.3 Story items and story sets

**Island tab → Story items...** (**+ Add a story item** adds one): keys, map pieces, logs... with a name, a description and a picture (Raft's quest
item pictures or any Raft item). Chests, zones, quest rewards and "give" actions hand them out; "only if" checks ask
for them; players find them in the journal (J), or in Raft's notebook (T) for a main story island. **Raft item...**
beside an item links it to one of Raft's own items (**-** clears it): players can then **take it out** of the journal
as that item, into their inventory ([3](#the-journal-j)) - a map piece that becomes a real blueprint, a log that
becomes planks. Story items stay journal items (a saved world never holds an item Raft doesn't know); the link is
only a way out. Raft's own quest item pickups (search "QuestItemPickup") work as story items too: picked up, the crew gets Raft's item (`story:raft-<type>`). The list has the pickups of Vasagatan (the bolt cutters, keys, the keycard...), Temperance and Utopia too.

**What ends up in the players' journal** ([3](#the-journal-j)), so you can plan a story with it:

- Every note with text becomes a page the first time someone reads it, titled with the note's title (give notes
  clear titles: the page list shows them). A note with no text adds no page.
- An event's **write a journal page** adds a page with your title and text (once per world) - a clue, a diary entry,
  what the crew learned.
- Story items show with their picture, name and description; when a check **uses one up**, it leaves the journal.
- A quest step **Collect** or **Find pages** counts what the journal holds, also what was found before the
  quest.

**Story sets** place a ready piece of a story in one step: **a locked door and its key** (in a chest with a note),
**a trail of notes** leading to a hidden chest, **a treasure map** (a bottle gives the map; the X digs up a chest
only for someone who has it), or **a locked chest** whose key is hidden in driftwood nearby.

![Story items and story sets](images/editor-story-items.jpg)
*The Story items window: the island's story items ("Old key") and the four story sets.*

**Survivors to rescue.** **Loot & chests** has **Survivor in a cage** (Raft's shark cage with one of Raft's
characters inside). In its **Survivor** group set the **Name**, who it **Looks like** (a Raft character, or any by its
id), **What the survivor says when freed**, the **Key item** (a story item, used up; empty: not locked), a **Survivor
id** and a **Signal** ("survivor" unless you name one). A player whose crew holds the key presses **E** at the cage:
the cage opens for everyone, the survivor says the line in a speech bubble over the cage, and a few seconds later
stands on the raft (as decoration: no needs, no work). The world remembers its survivors: one per id, also across
islands and after a reload, so two cages with one id free one survivor.

### 6.4 Islands that bring islands

**Island tab → Islands it brings...** gives the island its own rules: "when my quest is done", "when step 2 is done",
"when zone X fires" or "when players first get here" → bring a saved island or a new island of a map type, how far
and which way, with a message and a name on the Receiver. The cards work like a world plan's rules ([7.3](#73-a-rule-card-part-by-part)),
but they travel with the island: a chain of shared island files is a story on its own, in any world where the first
island turns up. A world plan is the better choice when you want to decide the whole world's story in one place. A rule that brings **its own island** (a new copy of it, which has the same rule) stops at 10 copies in a world.

![The island's rules](images/editor-island-rules.jpg)
*An island rule: when this island's quest is done, bring a new island of a random type 600 m north, with the
message "Well done" and the name "Reward" on the Receiver. The map on the right sketches where islands go.*

### 6.5 Your islands in Raft's story (the Receiver)

Raft's own story is a chain. A note on each story island gives the **frequency** of the next one, and players find it
by tuning the **Receiver** to it: Radio Tower, Vasagatan, Balboa, Caravan Town, Tangaroa, Varuna Point, Temperance
and, last, Utopia, the ending. A world plan ([section 7](#7-world-plans-which-islands-a-world-gets)) can change that
chain, in the **World plan** window (its **Settings** and **Main story line** tabs):

- **Raft's story islands: on/off.** On (the default) keeps Raft's story. **Off** leaves all of Raft's story islands
  out: the plan's own islands are the whole adventure. Raft's ordinary islands still turn up as in any world.
- **Leave out one story island:** click its name in the row next to the switch (it goes dark). The note before it then
  leads to the one after it. For example, without Balboa, Vasagatan's note gives Caravan Town's frequency.
- **Main story or side quest:** on a rule's card, the **STORY** list decides. **Side quest (not in the story)** keeps
  the island out of the story: it goes into the **journal (J)**. Any other choice makes it **main story**: it gets a
  tab in **Raft's own notebook** (see below). The places:
  - **Main story: first**: before everything, unlocked from the start of the world;
  - **After** one of Raft's story islands, or after another of your islands in the story;
  - **In place of** a story island: that island is left out and yours takes its place. Its note leads to your island,
    and your island leads on to the one after;
  - **Main story, beside Raft's**: in the notebook, but *not* in Raft's chain of story islands - its own WHEN gives
    its coordinates (for an expedition that runs alongside Raft's story, each island's when the one before is done,
    like The Abyss Expedition).
- **Every main story island has coordinates.** A main story island is always found **on the Receiver**: choosing a
  place in the story sets its WHERE to "On the Receiver" (the only choice then), and **Check** calls a main story island
  that isn't on the Receiver a problem. Its frequency comes on its notebook tab and in a banner when its coordinates are found - the
  first island's at the start of the world.
- **Next coordinates when:** when your island counts as done, so the next island's coordinates are found:
  - by default, when its quest is done, or when players reach it if it has no quest;
  - or when players reach it, when N steps of its quest are done, when one of its zones fires, when it sends a
    signal, or when a player reads one of its notes (pick it with ▾).
- **Where** it appears, for any rule:
  - **on the Receiver**: it gets its own 4-digit frequency, made for each world. When it is unlocked, every player sees
    a banner "Tune the Receiver to #4821", the journal gets a page, and the number on the note before it shows it.
    **Any player** can dial it on the Receiver; the island then comes up ahead of the raft, like Raft's story islands;
  - **by chance while sailing**: it comes up ahead some time after it is unlocked;
  - **ahead of the raft** or **near an island**, as soon as it is unlocked.
![Raft's story in the World plan window](images/editor-story-chain.jpg)
*Check on the template "Balboa replaced" (a new forest island in Balboa's place): every rule can work, and two tips - the
story in order (Radio Tower > Vasagatan > 'forest' > Caravan Town > ... > Utopia) and the blueprints Balboa held.*

- A rule's own **when** still counts. A story island "after Vasagatan" with "after sailing 3 km" is unlocked by
  Vasagatan's note, but comes only once the raft has also sailed 3 km.

When one of your islands unlocks one of Raft's story islands, no note of Raft's told the players, so a banner does:
"The Receiver picks up a new frequency: #1234", and the journal keeps it as a page ("A new signal").

**Check** shows the chain the plan makes (for example "Radio Tower > Vasagatan > 'forest' > Caravan Town > ..."), and
gives tips. They are only recommendations; you can do what you want:
- Raft's story starts at the Radio Tower;
- Utopia is Raft's ending, so without it last the story has no ending;
- a left-out island may hold blueprints the story needs. Without Balboa there is no machete, fuel tank, fuel pipes or
  biofuel extractor, so put them in a chest or a quest reward of your own.

**Templates...** has two for Raft's story:
- **Detour in Raft's story**: one of your islands after Vasagatan;
- **Balboa replaced**: an island in Balboa's place.

In a world, `StoryChain` (F10) shows the chain and where it stands. The chain belongs to the world: it is saved with
it, the same for every player, and it comes along when another player hosts the world. If you edit your plan's story
later (its islands, Raft's story on or off, the islands left out), the changed chain plays the next time the world
loads, like any change to your plan ([section 7](#7-world-plans-which-islands-a-world-gets)): what is unlocked and
done stays, and your Receiver islands keep their frequencies.

### The main story in Raft's notebook, side quests in the journal

**The short answer:** the **main story** is in **Raft's own notebook (T)**, the **side quests** are in the
**journal (J)**. Which is which, the world plan decides (the **STORY** list on each rule card, above).

**Raft's notebook (T): the main story.** Every main story island gets a **tab** on the book's edge, in story order
- between Raft's own tabs (Radio Tower, Vasagatan...) when Raft's story is on, alone after "First page" when it is off.
The tab shows the island's name (or the **tab title** you give it) and, for an island on the Receiver, its
**#frequency**; it appears when the island's coordinates are found. Its pages, in Raft's paper and handwriting:
- **the intro** - the text you write under **NOTEBOOK > first page** (or, empty, "A new frequency: #1234. Tune the
  Receiver to it to find ...", followed by the rule's message);
- **the quest's checklist** - its steps as they are done, crossed out, and the one to do now; a long quest goes on
  over more pages. The steps are no longer listed in the journal;
- **the notes read on the island and the pages its events write** ("journal page" actions), two to a page (a long one
  on a page of its own), in the order the crew found them.

Its **story items** show under Raft's **Found items**, with their pictures (Raft's quest item pictures). When the last
main story island is done, the plan's **ending page** (the card **THE END** under the rules) comes last in the book,
with a banner to every player. Every player also sees the **credits** once: the title (the card's **CREDITS** section: **Title on the
credits**, or the plan's name), the makers' names (**Made by**, one per line) and the closing note; **Back to the sea** (or Esc)
closes them. A world that is loaded again doesn't show them again; the note stays in Raft's notebook. The world goes
on, and Raft's own Utopia ending is not touched.

When there are more tabs than fit on the book's edge, the tabs **scroll** (mouse wheel over them); the open island's
tab is scrolled into view when you turn the pages.

![The main story in Raft's notebook](images/notebook-main-story.jpg)
*Silver Screen Seas (Raft's story on): Raft's tabs and the plan's islands in story order - Radio Tower, Camp Blackwater,
Vasagatan, The Sunken Liner, Balboa... (the strip scrolls for the rest). The Sunken Liner's pages: a long note on Raft's
notepad on a page of its own, two shorter ones on the page beside it.*

Raft's own islands are shown as the plan's story has them: an island **left out** or **replaced** has no tab (its pages
go to the back of the book), and with **Raft's story off** none of Raft's tabs show. Everything else of Raft's notebook
is Raft's: its notes, voice lines, quest items and the page you are on (another player turning the page turns it for
everyone, as in Raft).

**The journal (J): side quests.** Islands outside the story - by chance, ordinary rules, side trips - keep everything
in the journal as before: their notes, story items, pages from events and their Receiver frequencies. The journal's
quest list says only how far the main story is ("Main story: 3 of 10 done - its quests, notes and items are in
Raft's notebook" - a main story island's further quests and a generated main story island's quest count there too);
the per cent at the top still counts every quest.

**The rule for a new story line.** Every island that is part of the story gets a place in it (**STORY**: first,
after, in place of, or **beside** Raft's story), so all its steps are in Raft's notebook; only islands the story never
depends on stay side quests. So, in a plan:
- an island **brought by** a main story island (WHEN *quest*, *step*, *visit*, *zone*, *signal* or *rule* of it) is
  main story too - use **beside** for an expedition that runs alongside Raft's (as The Abyss Expedition does);
- a main story island never **waits on** a side quest island's WHEN;
- a **story item** a main story island needs (a lock, a quest's *collect*, a *has* / *take* check) is found on a main
  story island, not only on a side trip;
- islands chained one after another by WHEN are a story line: give them places in the story.

The mod's own story lines (Raft Remade, Raft 2 - The Drowned Frontier, The Long Voyage, Silver Screen Seas, The Abyss
Expedition) all keep this rule; the source has a check for it (`content/questbook_check.py`, over every plan recipe
and the plans in `Mods\DynamicIslands\plans`).

**Saves.** Nothing of the mod goes into Raft's own notebook save: the tabs and pages are made again from the world's
story each time it loads. A world opened without the mod has Raft's own notebook.

### Making a main story: the helper, and Preview notebook

**New main story line** (World plan window, Main story line tab) makes a main story step by step: Raft's story on or off,
then your saved islands in the order players meet them - for each, where it sits in Raft's story, its tab colour and
what gives the next coordinates. **Done: make the cards** makes the rule cards (every island on the Receiver, each after the one
before; the first one's coordinates given at the start when Raft's story is off or it comes first) and runs Check. Change the cards as you like after. The list of your islands opens at once; with many islands it says "Loading islands..." for a moment the first time.

**Preview notebook** (World plan window, next to Check) shows the plan *as it is in the window* - saved or not - in Raft's
own notebook: the mod goes to the test world (as Test in a world does), opens the book with every tab and page, and a
bar at the bottom steps through the story the way players will meet it: **|◀ Start** (nothing found yet), **◀ Back**,
**Next ▶** ("Saltmarsh Ferry: its coordinates are found", "quest step 3 of 6 done", "'The ferryman's log' is read",
"the next coordinates"...), **All ▶|**. **Back to the plan** returns to the plan with your changes still there. **Open the notebook** opens the book again after Esc.

![Preview notebook: Raft 2's ten tabs](images/notebook-preview-all.jpg)
*Preview notebook on "Raft 2 - The Drowned Frontier" (Raft's story off): First page, then the plan's ten islands in
story order, each with its Receiver number; the plan's story items under Found items; the step-through bar below.*

![Preview notebook, one step](images/notebook-preview-step.jpg)
*Stepping through: Saltmarsh Ferry's coordinates are found - its tab, its intro and its quest's first step.*

**Check** warns about the notebook too: a main story island without notes (its tab shows only its intro and steps), a
note too long for its paper (over 1100 letters; a note over 300 gets a page of its own), a story item without a picture, a tab title over 24 letters, a "next coordinates" note that isn't
on the island.

The world option **Story islands in a new order** ([9.4](#94-extra-options)) works the same way for the notebook:
the numbers on Raft's notes follow the world's order.

## 7. World plans: which islands a world gets

A **world plan** decides which custom islands a world gets, **when** they come and **where**. Without a plan, islands
only turn up by chance while you sail. With a plan, you can tell a story: *"When the world starts, put an old camp
ahead of the raft. When its quest is done, bring my island Skull Rock 800 m to the north-east. When players reach Skull
Rock, bring a treasure island near it."*

A plan is a list of **rules**, and each rule brings **one island**. Every rule answers four questions:

| Question | For example |
|---|---|
| **When** does the island come? | the world starts, after sailing 3 km, on day 5, when an island's quest is done, when players reach an island |
| **What** island? | one of your saved islands, a new island of a map type (a camp, a volcano, a wreck...), one from the spawn pool, one of a list |
| **Where** does it go? | ahead of the raft, near another island (how far and which way), on its own Receiver frequency, by chance while sailing |
| **What are players told?** | a message on screen ("Smoke rises from a small island ahead.") and a name on the Receiver |

You make and change plans in the **World plan** window - on the main menu, **EDITOR** → **A world plan** - and pick one for a world in Raft's **New
Game** box. You don't need to have built any islands yourself: the mod's map types ([4.6](#46-ready-made-islands-map-types))
are enough for a whole plan.

Before you share a plan or change one that running worlds use, see [7.6](#76-playing-changing-and-sharing-a-plan).

### The three kinds of islands you meet at sea

Every custom island that turns up in a world came there in one of **three ways**. When you stand on them they are all
the same - land, objects, maybe a quest - but they are set up in different places, they come at different times, they
behave differently if you sail past them, and only one kind is part of a story. This part explains all three, with
The Long Voyage (a plan of the island library, [15.4](#154-the-world-plans)) as the example, because it has all three
at once.

**The short version:**

- **Random islands** come **by chance** while you sail. Nobody planned them: which island, where and when is rolled
  as you go. A world gets them when its plan has **Random islands while sailing: on** (the plan called *Random islands*,
  the default, has nothing else).
- **Side trips** are islands a **plan** brings at a **set point of the voyage** - "after sailing 4 km, 450 m ahead of
  the raft" - with a message on screen. Every world of that plan gets the same island at the same point. They are
  extras: nothing else in the plan waits for them. In The Long Voyage: **The Stranded Gull** after 2 km, **Signal
  Rock** after 4 km (its quest is called **"Dead Air"**), Ranger's Rest after 7 km, and so on.
- **Main quest islands** are islands a plan puts **into a story chain** - here into Raft's own story, each found with
  the **Receiver** on its own frequency - and the story only goes on when their quest is done. In The Long Voyage:
  **Wreckers' Cove** after Raft's Radio Tower, **Thornwood** after Vasagatan, and five more.

A **quest** belongs to an island, not to the way it came. "Dead Air" is the quest of the island Signal Rock; Signal Rock
would bring its quest along as a random island too. So "a quest island" can be any of the three kinds: what makes an
island a *main* quest island is that a plan's story waits for its quest.

#### The three kinds side by side

| | Random islands | Side trips | Main quest islands |
|---|---|---|---|
| **What it is** | An island picked by chance from the **spawn pool**: one of your saved islands, a brand-new generated island, or a new island of a map type (a sandbar, a wreck, an atoll...) | An island a **plan rule** brings at a set point: after a distance sailed, on a day, when the world starts | An island a **plan rule** puts into a **story chain**: after one of Raft's story islands (or in place of one, or first) |
| **Example in The Long Voyage** | Any island of your pool, a generated jungle island... | The Stranded Gull (2 km), Signal Rock - quest "Dead Air" (4 km), Ranger's Rest (7 km), Stilt Hollow (10 km)... | Wreckers' Cove (after the Radio Tower), Thornwood (after Vasagatan), Scrapyard Haven (after Balboa)... |
| **When it comes** | While the raft sails: one after every 3-6 of Raft's own islands met (the world's span), none in a new world's first 10 minutes, never while the raft is at one of Raft's islands | When the rule's **WHEN** happens. The Long Voyage's side trips: when the raft has sailed that many km in this world (counted from the world's start) | When it is **unlocked** - the story island before it is done (for Raft's islands: when you read the note there that would give the next frequency) - **and** a player tunes the Receiver to its frequency |
| **Where it appears** | 250-350 m ahead of the raft, a little to one side (10-35 degrees), at least 800 m from other custom islands | Where the rule's **WHERE** says. The Long Voyage's side trips: **450 m ahead** of the raft | The rule's metres **ahead of the raft when a player tunes the Receiver** to it (700-1000 m in The Long Voyage), like Raft's own story islands |
| **What tells you** | Nothing when it appears: you see it. Near it, its banner with its name (only if it has a quest) | The rule's **message** on every player's screen as it appears, with how far and which way ("A mast on a rock ahead - its light is dead."), and its name on its Receiver dot | When it is unlocked: a banner with the rule's message and "Tune the Receiver to #4821", a page in the journal (J), and that number on Raft's note. On the Receiver: its name and frequency |
| **The same in every world?** | No: different islands at different places every time | Yes: the same island at the same distance in every world of the plan (only the exact spot ahead depends on where you sail) | Yes: the same island at the same place in the story. Only the 4-digit frequency is made new for each world |
| **Needed to finish the plan?** | No | No: optional extras. Skip them and nothing is missing from the story | **Yes** (in a plan with a story): its quest done unlocks the next step of the story - in The Long Voyage, the next story island's frequency |
| **If you sail past it** | It stays where it appeared. Beyond 800 m it is unloaded and comes back when you return. If you **reached it and its quest isn't done**, it comes back ahead of the raft by itself about 12 minutes later (up to 3 times) | The same as a random island: stays where it appeared; if you reached it and left its quest unfinished, it comes back ahead of the raft about 12 minutes later (up to 3 times). One you never reached doesn't come back | It stays where it came up: its dot on the Receiver shows the way back. It doesn't come back by itself |
| **Where you set it** | The plan's switch **Random islands while sailing** (World plan window, Settings tab); *which* islands: **World settings > Islands while sailing** ([9.5](#95-islands-while-sailing)) and `spawnpool.txt` ([10](#10-settings-files)) | A **rule** in the World plan window (Other islands tab): WHEN **After sailing a distance**, BRING **One of my saved islands**, WHERE **Ahead of the raft**, TELL a message and a Receiver name, STORY **Side quest (not in the story)** | A **rule** in the World plan window (Main story line tab): WHEN **When the world starts**, BRING **One of my saved islands**, WHERE **On the Receiver**, STORY **After** a story island, **next coordinates when** its quest is done |
| **In the plan file** ([7.7](#77-the-plan-file)) | `random = on` | `rule = signal \| island:Signal Rock \| km:4 \| ahead:450 \| A mast on a rock ahead - its light is dead. \| Signal Rock` | `rule = cove \| island:Wreckers' Cove \| start \| receiver:700 \| Under the Radio Tower's signal hides another... \| Wreckers' Cove \| after:RadioTower \| quest` |

#### Single islands and plan islands

Seen from your island files there are only **two groups**:

- **Single islands** are made, or downloaded, to be **found by chance while you sail**. They are the islands of the
  spawn pool and of the list in World settings ([9.5](#95-islands-while-sailing)).
- **Plan islands** come **only in their own world plan**, with their story: the islands a plan names in a rule
  (`island:` or `oneof:` in [the plan file](#77-the-plan-file)) and the islands *those* islands' own rules bring
  ([6.4](#64-islands-that-bring-islands)). A plan island **never turns up by chance**, not even when it is listed by
  name in `spawnpool.txt`: one that came by chance in another world would be out of its story (or twice in the same
  world, with its quest mixed up).

The mod looks at every saved plan to tell them apart, so you do nothing: use an island in a plan and it is a plan
island. Three kinds of files are never in the pool or the list at all: `rnd-...` (the [world randomizer's](#93-the-world-randomizer)
extras), `gen-...` (made up while sailing, kept in their world) and `Name_<hash>` (copies kept for a multiplayer world).
When you install a plan from the island library, its islands also get a `<island> 0` line in `spawnpool.txt` (weight 0
= left out). Islands the plan shares with another library pack keep their line.

**To make an island a single island**, use it in no plan - or save a copy under another name and use the copy in the
plan. **Plans that come with the mod:** Random islands and No custom islands
([7.1](#71-the-plans-that-come-with-the-mod)); and the big plans The Long Voyage, Raft Remade, Raft 2 - The Drowned Frontier, The Abyss Expedition and Silver Screen
Seas ([15.4](#154-the-world-plans)).

#### Random islands, in detail

**How a world gets them.** In Raft's **New Game** box, **Custom Islands plan** → **Choose plan...** ([2](#2-starting-a-new-world)):

- **Random islands** (the default): *only* random islands, no story of the mod's.
- **No custom islands**: none at all.
- **Any other plan**: random islands only if that plan has **Random islands while sailing: on**. The Long Voyage has it
  on, so you meet random islands *between* its side trips and quest islands; The Abyss Expedition has it off. A plan made for
  a tight story usually has it off: then the world has only the plan's islands.

You switch it for a plan in the **World plan** window: the switch **Random islands while sailing** on the **Settings** tab
of the plan (step 4 of [7.2](#72-your-first-world-plan-step-by-step)); in the plan file it is the line `random = on` or
`random = off`. In a running world the host can switch them with `CustomIslandsAuto on` / `off` (F10).

**Which islands can come.** The **spawn pool**: every island you have saved or downloaded (unless `spawnpool.txt`
gives it weight 0), brand-new generated islands (`generated` in `spawnpool.txt`), and the map types listed there. For
each new world you can untick islands in **World settings > Islands while sailing > CHOOSE ISLANDS...** (its row **Brand-new generated islands** is the made-up gen-... islands: untick it for none)
([9.5](#95-islands-while-sailing)). A **plan's own islands never come by chance**, so Signal Rock can't turn up early
as a random island and then again at 4 km ([plan islands](#single-islands-and-plan-islands)).

**How often and where.** One after every **3-6 of Raft's own islands** met (Raft's plain islands - not its story
islands - counted as the raft comes within about a kilometre of them; a new number in the span after each custom
island). The span is the world's: chosen in **World settings > Islands while sailing** ([9.5](#95-islands-while-sailing)),
`WorldIslandsGap` in a world. None in a new world's first **10 minutes** of play (`quietMinutes`; game time, kept with
the world - worlds made before this don't wait). `chancePerKm` only switches them on (above 0) or off (0).
`spawnDistanceMin` / `spawnDistanceMax` (250-350 m ahead), `minSpacing` (800 m between custom islands) - all in
`spawnpool.txt` ([10](#10-settings-files)). A finished island isn't picked again; an unfinished one (or one with notes not found) comes back as it was
([3](#islands-appear-while-you-sail)). None appears while the raft is at one of Raft's own islands, and Raft won't put one of its
islands on top of a custom one later.

#### Side trips, in detail

A side trip is one **rule** of a plan: "bring this island at this point of the voyage, here, and tell the players".
To make one in the **World plan** window, **Other islands** tab ([7.2](#72-your-first-world-plan-step-by-step) shows every click):

1. Open your plan and click **+ Add a rule**. Give it a name, for example `signal`.
2. **WHEN**: choose **After sailing a distance** and type the km, for example `4`. The distance is what the raft has
   sailed in this world since it started (`WorldPlan` in F10 shows it). Other WHENs make side trips too: **On a day**
   (day 5), or **When the world starts** for an island right at the beginning.
3. **BRING**: **One of my saved islands** and pick the island with **▾** (`Signal Rock`). Or **A new island of a map
   type** (`wreck`, `camp`, `sunken`...) for an island made new in every world, or **One island from a list** for a
   different one each time.
4. **WHERE**: **Ahead of the raft** and the metres (`450`). Raft draws about 400 m far, so at 450 m the island is just
   coming into sight. **By chance while sailing** instead makes it come up some time *after* the distance, like a
   random island.
5. **TELL**: the message every player sees when it appears (`A mast on a rock ahead - its light is dead.`) - the mod
   adds how far and which way - and the name on the Receiver (`Signal Rock`). Both are optional, but without a message
   players can't tell it from a random island.
6. **STORY**: leave it on **Side quest (not in the story)**. That is what keeps it a side trip: nothing waits for it,
   and its notes and story items go into the journal (J).
7. **Check**, then **Save**.

As a line of the plan file:

```
rule = signal | island:Signal Rock | km:4 | ahead:450 | A mast on a rock ahead - its light is dead. | Signal Rock
```

Good to know:
- Each rule fires **once per world**. Sail on to 4 km a second time (in the same world) and nothing new comes.
- If the raft is far from where it was when the rule fired, the island is still there: the rule placed it once, ahead
  of the raft at that moment. Its Receiver dot (with the name from TELL) shows it.
- A side trip can have its own quest. If you reach it and leave before the quest is done, it comes back ahead of the
  raft about 12 minutes later, up to three times ([3](#islands-appear-while-you-sail)); `returnMinutes` in
  `spawnpool.txt` changes the minutes (0 = never).
- Another rule can wait for a side trip ("when Signal Rock's quest is done, bring...") - then it is no longer a mere
  extra: the plan waits for it, and it comes back every time until that is done.
- A side trip on a day or a distance with **again until done** ticked comes back every N days (or km) after you were
  last near it, even if you never reached it, until you reached it and finished its quest ([7.4](#74-everything-a-rule-can-do)).

#### Main quest islands, in detail

A main quest island is a rule with a **place in a story chain**. The Long Voyage puts one after each of Raft's story
islands; your own plan can also make a story without Raft's ([6.5](#65-your-islands-in-rafts-story-the-receiver):
**Raft's story islands: off** and your islands **First**, **After** each other). To make one in the **World plan**
window (**Main story line** tab):

1. Make sure the plan has **Raft's story islands: on** (the switch at the top), unless your story replaces Raft's.
2. **+ Add a rule**, name it (`cove`).
3. **WHEN**: **When the world starts**. That only means the rule doesn't wait for anything *else*: the **STORY** place
   below decides when the island is unlocked.
4. **BRING**: **One of my saved islands**, pick it with **▾** (`Wreckers' Cove`). It needs a **quest** ([6.1](#61-quests)).
5. **WHERE**: **On the Receiver**, and the metres it comes up ahead of the raft when a player tunes to it (`700`). The
   island gets its own 4-digit frequency, made new for each world.
6. **TELL**: a message (shown when the frequency is unlocked: `Under the Radio Tower's signal hides another: a lantern
   code, blinking over and over.`) and the name on the Receiver (`Wreckers' Cove`).
7. **STORY**: **After** and the story island (**Radio Tower**) - that makes it **main story** (and WHERE stays **On the
   Receiver**: a main story island is always found by its coordinates). **next coordinates when**: **Its quest is done**
   (the default, **Its quest is done (or reached)**, does the same for an island that has a quest).
8. **NOTEBOOK** (it appears for a main story island): a tab title if the island's name is too long, a tab colour, and
   the intro on its first page in Raft's notebook (optional).
9. **Check** shows the whole chain ("Radio Tower > 'cove' > Vasagatan > ..."); **Preview notebook** shows the tab and
   its pages; then **Save**.

As a line of the plan file:

```
rule = cove | island:Wreckers' Cove | start | receiver:700 | Under the Radio Tower's signal hides another: a lantern code, blinking over and over. | Wreckers' Cove | after:RadioTower | quest
```

**What players see, step by step** (The Long Voyage, first quest island):

1. On Raft's **Radio Tower** they read the note that normally gives Vasagatan's frequency. In this world it gives the
   **Wreckers' Cove** frequency instead: a banner says "Tune the Receiver to #4821" (each world has its own number),
   Raft's notebook (T) gets a **Wreckers' Cove #4821** tab after the Radio Tower's, and the note shows the number.
2. A player tunes Raft's **Receiver** to it. Wreckers' Cove comes up about 700 m ahead of the raft, and shows on the
   Receiver with its name.
3. They sail there and do its **quest**: its steps are on the tab's checklist page (crossed out as they are done) and in
   the quest panel while they are at the island; the notes they read there go onto the tab's pages.
4. The quest done, the story goes on: **Vasagatan's** frequency is unlocked ("The Receiver picks up a new frequency:
   #1234") and its tab shows in the notebook, and Raft's story continues as Raft has it - until Vasagatan's note
   unlocks the next quest island, Thornwood.

If you don't do the quest, the story stops there: the next story island's frequency never comes. `StoryChain` (F10)
shows the whole chain, what is unlocked and done, and every frequency.

#### Other ways an island can come

The three above are what you meet in most worlds. A plan or an island can also bring islands in other ways - they work
like side trips (a rule with a WHEN, a WHERE and a message), only the WHEN is something that happens on an island:

- **Quest chains**: "when Old Camp's quest is done, bring Skull Rock 800 m north-east of it" ([7.2](#72-your-first-world-plan-step-by-step),
  [7.4](#74-everything-a-rule-can-do)). The Abyss Expedition is made of these: each island comes **on the Receiver**
  when the one before's quest is done or players reach it. The plan waits for them, so if one drifts out of reach before you've done
  what the plan waits for, it comes back every time.
- **Islands that bring islands**: an island's own rules, made in the editor ([6.4](#64-islands-that-bring-islands)) -
  for example a quest reward that brings a treasure island.
- **The world randomizer's islands** (World settings, [9.3](#93-the-world-randomizer)): oddity islands, large islands
  and boss lairs while sailing - random islands of their own, not from your spawn pool.

#### How to tell them apart in a world

| You see... | It is... |
|---|---|
| An island comes up with **no message**; its Receiver dot has **no name** | A random island (or one of the randomizer's) |
| A **message** on screen as it appears ("A white hull glints on a sandbar ahead.") and a **named** Receiver dot | A plan rule's island: a side trip, or part of a quest chain |
| A banner "**Tune the Receiver to #....**", then the island comes when you tune to it | A main quest island in the story chain |

In F10 (Raft's console):
- `WorldPlan` - the world's plan, whether random islands are on, the km sailed and the day, and **every rule** with
  **[done]** in front of the ones that have brought their island;
- `StoryChain` - the story in order (Raft's islands and the plan's), what is unlocked and done, and the frequencies;
- `ListSpawned` - every custom island in this world, with how far it is from the raft.

#### The Long Voyage from start to end

| When | What comes | Kind |
|---|---|---|
| From the start, all voyage long | Now and then an island of your spawn pool | Random island |
| 2 km sailed | **The Stranded Gull**, 450 m ahead: "A white hull glints on a sandbar ahead." | Side trip |
| 4 km | **Signal Rock** (quest "Dead Air"): "A mast on a rock ahead - its light is dead." | Side trip |
| After the Radio Tower's note | **Wreckers' Cove** on the Receiver | Main quest island |
| 7 km | **Ranger's Rest** | Side trip |
| After the Cove's quest | Vasagatan's frequency, as Raft has it | Raft's story |
| After Vasagatan's note | **Thornwood** on the Receiver | Main quest island |
| 10, 13, 16 km | **Stilt Hollow**, **Mayor's Wharf**, **Crane Yard** | Side trips |
| ... | the same after Balboa, Caravan Town, Tangaroa, Varuna Point and Temperance | Main quest islands |
| 20, 24, 28, 32 km | **Frost Hollow**, **Tide Farm**, **Old Mine Islet**, **Shelter Atoll** | Side trips |
| After Temperance's quest island | Utopia, Raft's ending | Raft's story |

The side trips come by distance and the quest islands by the story, so where they fall between each other depends on
how fast you sail and play: a quick player may reach Vasagatan before 7 km, a slow one may have met five side trips
by then. [15.4](#154-the-world-plans) lists every island of the plan.

#### Questions

- **"An island appeared - is it random or part of the plan?"** Look for a message when it appeared and a name on its
  Receiver dot (the table above), or type `WorldPlan` in F10: a plan island's rule is marked **[done]**.
- **"Do I have to do the side trips?"** No. They are there to explore; the story never waits for them.
- **"I sailed past a side trip - is it gone?"** It stays where it came up (its Receiver dot shows where). If you had
  reached it and its quest wasn't done, it comes back ahead of the raft by itself after about 12 minutes, up to three
  times. One you never reached stays where it is.
- **"A quest island drifted away before I finished it."** Main quest islands stay where they came up - follow
  their dot on the Receiver, which shows the way and the distance. Islands a plan waits for in other ways come back by themselves until you've done
  what the plan waits for ([3](#islands-appear-while-you-sail)).
- **"Can I have only the plan's islands, no random ones?"** Yes: in your own plan, set **Random islands while sailing**
  to off. For a plan someone else made, make a copy (**Copy...** in the World plan window) and switch it off there, or untick
  every island in **World settings > Islands while sailing** for that world.
- **"Can a random island be one of the plan's islands?"** No: an island any saved plan names never comes by chance
  ([plan islands](#single-islands-and-plan-islands)). To use it by chance too, save a copy under another name.
- **"Where do I change the km, the message, or which island?"** On the main menu: **EDITOR** → **A world plan**, open the plan, change
  the rule's card (**Other islands** or **Main story line** tab), **Save**. A world already playing that plan gets the change the next time it loads; rules that have
  already fired stay done.

### 7.1 The plans that come with the mod

Choose one in the New Game box (**Custom Islands plan**), or open it in the World plan window to see how it's made:

| Plan | What happens |
|---|---|
| **Random islands** (the default) | Islands turn up by chance while you sail ([section 3](#3-sailing-custom-islands-in-your-world)). No story |
| **No custom islands** | Plain Raft: no custom islands at all (the World settings still apply) |

Full storylines come from the island library: **Get more plans...** in the New Game box downloads them
([15.4](#154-the-world-plans) lists them). A downloaded plan is an ordinary plan file: open it in the World plan window
and use **Copy...** to start your own plan from it.

### 7.2 Your first world plan, step by step

This walkthrough makes a plan called **Castaway trail** with four islands. It uses map types, so it works even if you
haven't built an island yet. Step 6 shows where to use one of your own islands instead.

![The World plan window](images/editor-world-plans.jpg)
*The World plan window on its Main story line tab: Raft's own story islands as grey lines, a card for each of
your islands (WHEN, BRING, WHERE, TELL), the map on the right, and Check, Test this plan and Save at the bottom.*

**1. Open the World plan window.** In Raft's main menu click **EDITOR**, then **New world plan** on the **A world plan**
card (or **Open a world plan...**, or **Continue** with the plan you made last). The plan opens right on the main menu:
no island is loaded and the 3D editor does not open. Coming from an island instead? In the island editor click **Use in a
plan...** ([4.1](#41-the-screen)).

**The window.** The title is **WORLD PLAN**. The top row has **Plan: <name> ▼** (pick another plan), **New...**,
**Open...**, **Copy...**, **Delete**, **Templates...** and **?**. **New...** asks what to start from: an empty plan, a
**Main story line step by step**, a template, or a copy of another plan. Below it are three tabs:
- **Main story line (n):** the islands the Receiver leads players to, one after the other, in play order. When Raft's
  story is on, Raft's own story islands show as grey lines between yours. Buttons: **New main story line** (the
  step-by-step helper, [6.5](#65-your-islands-in-rafts-story-the-receiver)), **+ Add a saved island** (adds one of
  your islands at the end of the story, before Utopia; the list opens at once and says "Loading islands... (n of N)"
  while it reads them) and **+ New island** (name it: it is put at the end of the
  story, the plan is saved and the island editor opens on the new empty island, with **Back to the plan**). **▲** and
  **▼** on an island's card move it one step along the story, also past Raft's own islands; Utopia stays last. An
  island set "in place of" one of Raft's islands is moved with its STORY row instead. Islands "beside the story" are
  listed under their own header, and the last card is the ending (last page).
- **Other islands (n):** islands that come on their own - at the start, after some km or days, when a quest is done,
  near another island... Each is a rule card with WHEN, BRING, WHERE and TELL, and **+ Add a rule** adds one. The
  STORY row of a card moves it into the Main story line tab (the window follows the card).
- **Settings:** random islands while sailing, Raft's own story on or off (and which story islands to leave out), and
  the plan's description.

The buttons at the bottom are in two rows: **+ Add a rule** (on the Other islands tab), **Check**, **Progression**,
**Preview notebook** and **Test this plan** above, **Export...**, **Import...**, **Save** and **Close** below. On an island's card, **Edit island** opens that island in the 3D editor (the plan is
saved first); the editor then shows **Back to the plan**, which saves the island and returns to the plan - or to the main
menu with the plan open, if you came from there. Closing the plan comes back to the start window.

**Help while you work:** every part of the window has a small **?** next to it. Hover it (or click it) and a note
explains that part: what each choice of **WHEN**, **BRING** and **WHERE** means, what Check looks for, and so on. The green
**Help** button at the top right shows the steps below in short, with buttons that open this guide (the PDF, or this
section online).

![A ? explains a part](images/plan-help-popup.jpg)
*Hovering the ? after **When**: every choice it has, in a few words.*

The buttons at the bottom are in two rows: **Check**, **Progression**, **Preview notebook** and
**Test this plan** above; **Export...**, **Import...**, **Save** and **Close** below.

![The Help box](images/plan-help.jpg)
*Help: how to make a plan, in eight steps, and the guide's buttons.*

**2. Start a new plan.** Click **New...**, choose **empty**, type a name (`Castaway trail`) and press **Enter** (or **OK**).
The window now shows your empty plan: "No rules yet". The name is also the plan's file name, so it can't be the name of a plan you
already have.

**3. Describe it.** On the **Settings** tab, click the description field and write one line about the plan, for example
`Follow a castaway's trail across four islands`. Players see it when they choose the plan in the New Game box.

**4. Set the two switches** (also on the **Settings** tab).
- **Random islands while sailing: on / off.** On: islands from your spawn pool *also* turn up by chance, between your
  plan's islands. Off: the world has only the plan's islands. For a story, **off** is usually best - click it.
- **Raft's story islands: on / off.** On (the default): Raft's own story (Radio Tower, Vasagatan, ... Utopia) is still in
  the world, next to your islands. Off: your plan is the whole adventure. Leave it **on** for now; [6.5](#65-your-islands-in-rafts-story-the-receiver)
  explains the story row.

**5. The first island: an old camp when the world starts.** Go to the **Other islands** tab and click **+ Add a rule**. A card appears with
one section per question ([7.3](#73-a-rule-card-part-by-part)). Each section has a **▼ list**: click it and pick a choice -
each choice says in a line what it does. Fill the card in:
- **Name** (top of the card, for example `rule1kqx`): type `camp`. The name is how other rules say "the camp".
- **WHEN**: it already says **When the world starts**.
- **BRING**: pick **A new island of a map type**, then click **▾** after the **map type** field and pick **Old camp** (or
  type `camp`).
- **WHERE**: it already says **Ahead of the raft**. Set the metres to `350`.
- **TELL**: the message `Smoke rises from a small island ahead.`, and **on the Receiver** `Old camp`.

The sentence at the top of the card now reads: *When the world starts: bring a new old camp, 350 m ahead of the raft.*
Read it after every change: it says in plain words what the rule will do. Under each section, a line in italics explains
the choice you made.

**6. The second island: your own island, when the camp's quest is done.** Click **+ Add a rule** again. A new rule
already waits for the rule before it: **WHEN** **When a quest is done**, island `camp`; **WHERE** **Near an island**,
600 m. Fill in:
- **Name**: `cove`.
- **BRING**: pick **One of my saved islands**, then **▾** and one of your islands (for example **Skull Rock** from
  [4.8](#48-your-first-island-step-by-step)). No island of your own yet? Leave it on **A new island of a map type** and
  choose **Tropical island**.
- **WHERE**: **Near an island**, `800` metres, and in the direction list **North-east**. The field after **of** can stay
  empty: then it means "the island where the WHEN happened", the camp. (You can also type `camp` there, or pick it with
  the **▾** after the field.)
- **TELL**: `The camp's notes speak of a cove to the north-east.`, on the Receiver `Cove`.

The camp's map type has a quest (the notice board). "When a quest is done" at an island that has no quest never fires -
use **When players reach an island** for those (next step).

**7. The third island: a treasure island when players reach the cove.** **+ Add a rule**, then:
- **Name**: `treasure`.
- **WHEN**: **When players reach an island**, then the **▾** after the **island** field and pick `cove` (or type it).
  The list has the plan's other rules and your saved islands, and says which have a quest.
- **BRING**: **A new island of a map type** → **▾** → **Treasure island**.
- **WHERE**: **Near an island**, `900` metres, **Any way**, of `cove`.
- **TELL**: `From the cliffs you spot another island.`, on the Receiver `Treasure`.

**8. A fourth island on the way: a wreck after 3 km.** Not every rule has to follow another. **+ Add a rule**, then:
- **Name**: `wreck`; **WHEN**: **After sailing a distance**, type `3` (km);
- **BRING**: **A new island of a map type** → **Wreck**;
- **WHERE**: **Ahead of the raft**, `300` metres; **TELL**: `Something floats ahead: a wrecked raft.`

**9. Check the plan.** Click **Check**. The box under the map says **√ Every rule can work.**, or lists what's wrong
(see [7.5](#75-check-finding-and-fixing-problems)). The map on the right sketches where the islands will go.

**10. Save.** Click **Save**. The bottom of the screen says `Saved plan 'Castaway trail' (4 rules)`. Always save before
you click **Close**: Close throws away changes since the last save.

**11. Play it.** Go back to the main menu (**MAIN MENU**, top right) → **NEW WORLD**. At the bottom right, click the
**Choose plan...** under Custom Islands plan, pick **Castaway trail** and **Select**, then click Raft's **Create**. In the world:
- the old camp is 350 m ahead of the raft right away, with the message on screen and "Old camp" on your Receiver;
- finish the camp's quest (the quest panel shows its steps) and the cove comes 800 m to the north-east;
- sail to the cove: when you reach it, the treasure island appears near it;
- after 3 km of sailing, the wreck comes up ahead, whatever else you're doing.

Press **F10** and type `WorldPlan` to see the world's plan and which rules have fired.

**12. Change it later.** On the main menu click **EDITOR** → **A world plan** → **Continue 'Castaway trail'** (or
**Open a world plan...**, or **Plan ▼** in the window). Change
what you want, **Save**. New worlds get the new version. A world that already uses the plan gets it the next time you load
it on your PC; rules that already happened stay done ([7.6](#76-playing-changing-and-sharing-a-plan)).

The finished plan as its file (`Mods\DynamicIslands\plans\Castaway trail.plan`, see [7.7](#77-the-plan-file)):

```
description = Follow a castaway's trail across four islands
random = off
story = on

rule = camp | type:camp | start | ahead:350 | Smoke rises from a small island ahead. | Old camp
rule = cove | island:Skull Rock | quest:camp | near:camp:800:north-east | The camp's notes speak of a cove to the north-east. | Cove
rule = treasure | type:treasure | visit:cove | near:cove:900:any | From the cliffs you spot another island. | Treasure
rule = wreck | type:wreck | km:3 | ahead:300 | Something floats ahead: a wrecked raft.
```

**Short cut: Templates...** (top row) adds a ready-made set of rules to the open plan:
**Detour in Raft's story**, **Balboa replaced**, **Sky chain** (flying islands, each appearing when players reach the one
before), **Story chain** (three of your islands, each quest bringing the next: pick your islands with **▾**) and **Quest
reward island**. Then change what you like. An id that's already in your plan gets a `b` added.

### 7.3 A rule card, part by part

![A rule card](images/plan-rule-card.jpg)
*A rule card: its name and what it does in a sentence, then one section per question. Each section
has a ▼ list of its choices and a line in italics saying what the chosen one does.*

![A ▼ list open](images/plan-dropdown.jpg)
*The **BRING** list open: every choice, each with what it does. The chosen one is lit; click another to change it (Esc
or a click outside closes the list).*

| Part | What it does |
|---|---|
| **RULE 1** and **Name** | The rule's number and name (`camp`, `beast`). Other rules point at the island it brought with this name. Each name once per plan; no `\|`, `:`, `,` or `;` (they are taken out) |
| **▲ ▼ Remove** | Move the rule up or down in the list (the order only matters for reading: each rule waits for its own WHEN), or remove it |
| **The sentence** | Under the name: the rule in plain words. If it doesn't say what you meant, change the card |
| **WHEN** | What the rule waits for - a ▼ list of nine choices ([7.4](#74-everything-a-rule-can-do)). A choice that needs more shows named fields after it: **island**, **zone**, **signal**, **steps**, **day**, **km sailed**, **rule**; a day or a distance also has the **again until done** tick box |
| **BRING** | What kind of island: a new island of a map type, one of your saved islands, one island from a list, or a random one from the spawn pool. The field after it says which (**map type**, **island**, **islands**) |
| **WHERE** | Ahead of the raft, near an island, on the Receiver, or by chance while sailing; then the **metres** (50 to 5000). For "near an island", also the **direction** (a ▼ list: Any way, North, North-east...) and **of** which island (empty = the island where the WHEN happened) |
| **DOT** | When the island's dot shows on the Receiver's radar: **Always** (the default, as Raft's story islands), or only once the player has the **Motor wheel**, **Steering wheel** or **Engine controls** blueprint. Until then the island comes straight ahead of the raft (a player without an engine can still reach it) and has no dot; after it, a "near" island takes its place to the side. Check calls a blueprint Raft doesn't have a problem |
| **TELL** | The message every player sees when the island appears (up to 160 letters) (with how far and which way it is), and the island's name **on the Receiver** (up to 18 letters). Both optional |
| **STORY** | World plans only: **side quest** (the journal) or **main story** (Raft's notebook) - the island's place in the story (a ▼ list: side quest, main story first, after or in place of one of Raft's story islands, or beside Raft's story) and, for the main story, **next coordinates when** ([6.5](#65-your-islands-in-rafts-story-the-receiver)). A main story island's WHERE is always **On the Receiver** |
| **NOTEBOOK** | Main story only: the island's **tab title**, **tab colour** (a ▼ list of Raft's nine: Pink, Sand, Purple, Teal, Olive, Red, Green, Blue, Light blue; **Automatic colour** takes one by the island's place in the story) and the **first page** intro in Raft's notebook |
| **THE END** | Under the rules, when the plan has a main story: the last page of the story in Raft's notebook (optional) |
| **The lines in italics** | Under each section: what the chosen option does, with the numbers and names you gave. For a map type, what kind of island it makes |
| **?** | At the end of each section: hover it for every choice of that section in a few words |

**The ▾ lists.** A field that names something has a **▾** after it: the island a rule waits for, the zone, signal or quest
step on that island, the island to put it near, the map type or saved island to bring, and a story rule's "done when"
zone, signal, step or note. It lists what exists, so there is nothing to misspell: the plan's rules (with what they bring),
your saved islands (with "quest, 4 steps" or "no quest"), and the zones, signals and quest steps saved in that island's
file. Picking one fills the field; typing still works. A new map-type island is only made in the world, so its zones and
signals can't be listed: its list is empty and says so - type the name.

![The ▾ list of a rule](images/plan-picks.jpg)
*The ▾ after the zone of "When a trigger zone fires" lists the zones of the island the rule waits for (here the saved island "shrine").*

### 7.4 Everything a rule can do

**When** the island comes. "Island" below means a rule's id (the island that rule brought) or the name of an island:

| When | Needs | The island comes... |
|---|---|---|
| **When the world starts** | - | as soon as the world starts |
| **After sailing a distance** | km | when the raft has sailed that far in this world |
| **On a day** | a day | on that in-game day |

**Again until done.** **After sailing a distance** and **On a day** have a tick box after the number. Ticked, the island
comes back ahead of the raft every N days (or N km sailed) after the players were last near it, N being the rule's
number, for as long as they haven't reached it or haven't finished its quest (an island without a quest is done once
a player reaches it). It is the same island each time - its quest, chests and harvest as they were, never a second
copy - and a banner says "Back in sight". There is no limit to how often; the host decides with several players. An
island on the Receiver doesn't come back by itself (its frequency shows the way). In the plan file: `day:5:again`.
| **When a quest is done** | an island, and **quest no.** (empty = its main quest) | when that island's quest is done (or its Quest 2, 3 ... when a number is set). The island must have that quest ([6.1](#61-quests)) |
| **When quest steps are done** | an island, steps | when that many steps of its quest are done |
| **When a trigger zone fires** | an island, a zone name | when a player walks into that trigger zone on it ([5.4](#54-trigger-zones-and-ambushes)) |
| **When players reach an island** | an island | when a player first comes to that island |
| **Right after another rule** | a rule's name | right after that rule's island has come |
| **When a signal is sent** | an island, a signal name | when an object on that island sends that signal (a **send a signal** action, [6.2](#62-behaviour-and-events)) |

**What** it brings:

| Bring | Which one | Good to know |
|---|---|---|
| **One of my saved islands** | one of your islands (`.island` files) | Every player gets it from the host; only the host needs the file |
| **A new island of a map type** | a map type: `random`, `sandbar`, `atoll`, `archipelago`, `stacks`, `boss`, `volcano`, `swamp`, `spire`, `treasure`, `camp`, `sunken`, `sky`, `wreck`, `ghostraft`, `traderraft`, `ghostship`, the styles `tropical`, `snowy`, `desert`, `forest`, `volcanic`, and the randomizer's `oddity` (or one kind: `van`, `caravan`, `planecrash`, `boatwreck`, `shack`, `statue`, `rocket`, `hut`), `large`, `lair` | A new island is made for the world, different in every world. No file needed, so it always works when shared |
| **A random island (spawn pool)** | - | A random island of the spawn pool, as random islands are: one of your islands, a new generated island or a map type listed there (`spawnpool.txt`, [10](#10-settings-files)) |
| **One island from a list** | island names, separated by commas | One of them is picked, ones not in the world yet first |

**Where** it goes:

| Where | Metres mean | Good to know |
|---|---|---|
| **Ahead of the raft** | how far ahead | The simplest: players can't miss it |
| **Near an island** | centre to centre from that island | With a direction and an island (**of**). If **WHEN** has no island (the world starts, a distance, a day, another rule), you must name one in **of** |
| **On the Receiver** | how far ahead it comes when tuned | It gets its own 4-digit frequency; it comes when a player tunes Raft's Receiver to it ([6.5](#65-your-islands-in-rafts-story-the-receiver)) |
| **By chance while sailing** | how far ahead | Comes up ahead after 0.3 to 1.8 km more of sailing once the rule fires, like a random island |

**Ideas to start from:**
- **A quest chain:** each island's rule waits for **When a quest is done** at the island before it, **Near an island** of it. Players
  follow the story from island to island. (Every island except the last needs a quest.)
- **Explore to find more:** **When players reach an island** instead of **When a quest is done**, for islands without quests.
- **Timed surprises:** **After sailing a distance** or **On a day** with **Ahead of the raft**, between the story's islands.
- **A secret:** a trigger zone in a cave (**When a trigger zone fires**) or a lever that sends a signal (**When a signal is sent**) brings a
  hidden island.
- **Something different every time:** **One island from a list** with a few of your islands, or a map type `random`.
- **Found by radio:** **On the Receiver**, with the message telling players the island is out there.

### 7.5 Check: finding and fixing problems

Click **Check** any time. It goes through the plan the way a world will play it, and looks **inside the islands** too:
each island's quest step by step (is there a zone, a note, a chest or creatures with the name each step needs?), its
trigger zones and signals, and - for new islands of a map type - a sample island of that type. Then a report opens:

![Check's report](images/plan-check.jpg)
*Check's report: each finding with its level, the rule it is about, why, how to fix it, and **Show rule** to go to its card.*

- **Problem** (red): the rule can't work - it never brings its island. Fix these first.
- **Warning** (yellow): it may not work as you mean (for example it waits for an island no rule of the plan brings).
- **Tip** (blue): good to know (no message, very far away, Raft's story...).

After the first Check, every card shows its own problems and warnings in red and yellow under its sentence, and they
follow as you change the plan. The box under the map sums up. **Save** checks too, and opens the report when there is a
problem. The report's **Check again** checks after you changed something.

Check walks the plan in **play order**, the order a player meets its islands, not the order of the cards. Raft's story
chain comes first, with your islands in their places (first, after, instead of). Every other rule comes where its WHEN
puts it: at the start, right after the island it waits for, or - for a distance or a day - after the story islands a
player passes on the way (about one every 4 km). Raft's story islands give their blueprints (the machete on Balboa, the
metal detector on Caravan Town...) when the story reaches them, not at the start.

**Tools.** Vines and sea vines need the machete, buried treasure the metal detector and a shovel, dirt the shovel,
ziplines (all of Raft's zipline lines) the zipline tool, and Raft's dog cage, shark cage and padlocked door the **bolt
cutters** (Vasagatan's quest item). A check on a tool (`has|Binoculars|1`, `has|Axe|1`...) counts too, and so do the
headlight, binoculars, oxygen bottle, flippers, axe and metal hook. The electric zipline tool, the advanced headlight
and the stone or titanium axe count as the tool. Some needs are only **advice** (a warning starting with "Advice"):
an underwater air zone or a quest target 15 m or more under the sea (the oxygen bottle or flippers), a dark
atmosphere zone round a quest target (the headlight), and a step whose text names binoculars or a headlight. If a step of an island's main quest can't be done because no island before it gives the tool (or its
blueprint), Check warns - in the story chain, the story would stop there. Optional finds that need a tool given later
get no warning, as in Raft: a cache behind vines no step needs, or buried treasure. Players come back with the tool;
the Progression panel lists them quietly.

**Story items on two islands.** If two islands of the plan each give a story item with the same id (two 'keycard's),
the one found on either island opens the other's locks too. Check warns: give each island its own id. Raft's own quest
items (the Vasagatan keycard, the bolt cutters...) found on Raft's story islands open only that island and are never
keys for your locks.

**Progression** (beside Check, world plans only) opens a panel with what the plan gives of Raft's progression, in play
order: Raft's story islands it keeps, each in its place with the blueprints and tools it gives, and each rule whose
islands give one of Raft's story-island blueprints, a tool or a story item (a key, a keycard...), or have a lock or a
feature that wants one. A lock whose item no island before it gives, and a main-quest need of a tool not given yet,
are shown in yellow; an optional find needing a later tool is a muted line. A lock whose item an island met before
gives says where it was **kept from** (the crew keeps a story item that isn't used up, for later islands). A blueprint
given **too early** - before the ones it builds on, like the engine controls before the motor wheel and steering
wheel - is shown in yellow at its rule, with the stop that gives its bases. Last, in yellow, come the blueprints the
plan never gives. **Show rule** goes to a rule's card. Use it to build a plan where the player unlocks more and more
as they sail on.

What Check looks for:

| | Problems (it can't work) | Warnings and tips |
|---|---|---|
| **Names** | a rule with no name, two rules with the same name | a rule named like a rule on one of the islands the plan brings (what waits for that name may take the other's island) |
| **WHEN** | no number for a distance or a day; a quest wait at an island **without a quest**; a quest that **can't be finished** (a step needs a zone, note, chest or creatures the island hasn't, or more pages than it gives - a note gives a page only when it has text); more steps than the quest has; a **zone** or **signal** the island hasn't (Check lists the ones it has); an island or rule name that doesn't exist; a rule that waits for itself; **rules that wait for each other in a circle** | waits for a saved island **no rule of the plan brings** (a problem when random islands are off); waits for a rule that has a problem; needs more creatures than the island has (and they don't come back - by default they do, after the regrow days); creatures Raft leaves out of a game mode (screechers and puffer fish: none in Creative); a story item nothing on the island gives; a distance over 30 km or a day after 20 (slow to test), or 0 (it comes at once); **again until done** below 1 (it comes back every km or day) or with its island on the Receiver (that island doesn't come back by itself) |
| **BRING** | no island chosen, an island that isn't saved, a map type that doesn't exist, a list with no saved island, an empty spawn pool | some islands of a list aren't saved; a very big island (over 12 000 objects); the same island brought twice |
| **WHERE** | near an island nobody has; near "the island where it happened" when the WHEN happens at no island; near its own island | near a saved island no rule brings; near a rule it doesn't wait for (it waits until that island is there); very far away |
| **TELL** | | no message; found by Receiver but no Receiver name |
| **STORY** | its "next coordinates when" can't happen (as WHEN above, also a note number the island hasn't); after Utopia while Utopia ends the story (it never counts as done); a **main story island not on the Receiver** (it has no coordinates) | after an island that isn't in the story; Raft's story order and missing blueprints; **notebook:** a main story island without notes, a note over 1100 letters, a story item without a picture, a long tab title |
| **The plan** | no rules, random islands off and Raft's story off (a world gets nothing); an island's own rules using the story or the Receiver | nothing comes by itself (every rule waits for another island) |
| **Raft's blueprints** | | a blueprint that lies on one of Raft's story islands (the steering wheel, the engine's parts, the machete, the electric purifier, the titanium tools... 23 in all) that the plan **never gives** - its story island left out or replaced, and none of the plan's islands giving it as a reward, in a chest or from a note: the player could never build it. A tip lists which of the plan's islands give which blueprints, in the plan's order - a world plan is an adventure where the player unlocks more and more; a blueprint given **before the ones it builds on** (the engine controls before the motor wheel and steering wheel, the fuel pipes before a fuel tank, the advanced biofuel extractor before the plain one, the water pipes before a water tank): players find it before they can use it - the fix gives Raft's own order |
| **Quest traps** | | a later step's chest or animals there from the start (not "Hidden until shown"); story items given again (a zone that fires again, a chest that fills up, animals that come back); show/hide or open/close in an event that happens again (it flips back); has / uses up Raft items on the quest-done or a defeat event (they fire on the host: left out); "give items" on a note (only the first reader); a story item used (a quest step, a check, a chest, an action) that isn't in the island's Story items list (deleted, or a typo); a keycard/key door that no story item opens (it opens for anyone) - each with its fix ([6.2](#62-behaviour-and-events)) |
| **Story items in order** | | an island with a lock (a keycard door, a locked chest...) that wants a story item - one of Raft's quest items like the Tangaroa keycard, or the island's own - which no island of the plan gives, or only an island met later in play order: players reach the lock without the key. A lock that opens in more than one way and can open by a signal another piece on its island sends (a ready radio on an island with a generator, which runs it without the battery part) doesn't count. The warning names the item and the island that gives it |
| **Codes in order** | | a keypad (`lock.code`) whose code no note or text on its island or an island met before it tells - players reach the keypad without the code. Split ("3-1-2", "3 1 2") or spelled-out digits (FOUR, ONE, NINE in order across the island's texts) count as told. The warning names the island that tells it later, if any |
| **Tools** | | a step of an island's main quest that needs one of Raft's tools (vines and sea vines: the machete; buried treasure: the metal detector and a shovel; a zipline: the zipline tool; a cage or padlocked door: the bolt cutters; a check on a tool) which no island met before it gives, in play order - in the story chain the story stops there. Optional finds needing a later tool are only listed in Progression |
| **Story items on two islands** | | one story item id given on two islands of the plan (two 'keycard's): the one found on either opens the other's locks too; give each island its own id |

Check can't play the quests for you. Test your plan: create a world with it and play it through (F10 → `WorldPlan`
shows which rules have fired). A map type's island is made new in each world, so Check looks at a sample of it: its
names are the same every time, its exact places aren't.

### 7.6 Playing, changing and sharing a plan

- **Test this plan** (World plan window, next to Preview notebook) saves the plan and makes a new world with it, a test world
  "Plan test <time>": its islands come as in any world. **Esc → Custom Islands → Back to the editor** brings you
  back to the plan (the test world stays under Load; delete old ones there).
- **Choose it** in the New Game box: **Choose plan...**, pick your plan and **Select** (**View the plan...** under
  it shows what the plan does: its description, Raft's story, each island and when it comes), then **Create**. The
  next new world starts on **Random islands** again (or the `defaultPlan` of `spawnpool.txt`). A world keeps the plan
  it was made with: **Esc → CUSTOM ISLANDS** shows it, and which random islands come while sailing can still be
  changed there (**Islands while sailing**). F10 → `WorldPlan` shows the plan and which rules have fired.
- **Multiplayer:** only the host needs the plan and its islands. Players who join get every island as it appears
  ([section 8](#8-playing-together)).
- **The plan decides islands only.** Monster difficulty, build cost, the level up system, the randomizer and the extra
  options are chosen in World settings for each world, whatever plan it has ([section 9](#9-world-settings-rules-and-extra-systems)).
- **Copy...** saves the plan under another name. **Delete** moves the plan file to `Mods\DynamicIslands\deleted\plans` (it doesn't ask - move it
  back from there to undo); worlds that use it keep their own copy.
- **Share it:** **Export...** makes a pack (`.zip`) with the plan and every saved island it needs, to send to a friend
  or to put in the island library; **Import...** installs a pack someone sent you ([4.7](#47-saving-and-sharing)).
  Map type islands need no file, so a plan made only of map types always works for everyone.

**A world keeps its own copy of its plan.** When a world starts, the plan's rules are saved with the world:
- **you edit your plan** (in the World plan window, on the PC of the player who made the world): the next time the world loads,
  the changed plan plays - rules that already happened stay done, new ones come - and you're told so;
- deleting the plan file, an **update** of a library or pack plan, or **another player** hosting the world with a
  different plan of the same name never changes the world: it plays its own copy (to give a running world another
  plan on purpose, use the F10 command `WorldPlan <name>` in it);
- a **new plan with an old plan's name** is a different plan: each plan you make or copy gets its own id (`id =` in
  the file), so if you delete a plan and make another under its name, worlds made with the old one keep playing their
  own copy and the new plan's edits don't reach them;
- the world's plan goes along when someone else hosts the world later ([section 8](#8-playing-together)), even if they
  never had the plan;
- worlds saved before this version get their copy the next time they're saved on a PC that has the plan file;
- a world keeps a plan's progress by each rule's **id**: adding and deleting rules is fine, and so is changing a fired
  rule's words, WHEN or WHERE. A fired rule whose id you rename, or whose **brings** you change, is a new rule to the
  world, and its island comes again. (The window gives each new rule an id no rule of the plan had, like `rule4kmx`.)

A plan can only bring islands that are on the host's PC. If one is missing, the host sees which island and where it
came from (the pack or library entry, or "ask the player who made this world"), and the rule waits until the island is
there. If there is no room for a rule's island, it keeps trying, looks further out after a few tries, and the host is told once.

### 7.7 The plan file

Plans are text files in `Mods\DynamicIslands\plans\<name>.plan`. The World plan window writes them for you, but you
can also open one in a text editor (the file starts with a short help). One `rule =` line per rule, its parts
separated by `|`:

```
rule = id | what | when | where | message | Receiver name
```

| Part | Written as |
|---|---|
| what | `island:<saved island>`, `type:<map type>`, `pool`, `oneof:<island>, <island>, ...` |
| when | `start`, `km:<km>`, `day:<day>` (`km:<km>:again`, `day:<day>:again` = again until done, [7.4](#74-everything-a-rule-can-do)), `quest:<island>` (`quest:<island>:2` for its quest 2), `step:<island>:<steps>`, `zone:<island>:<zone>`, `visit:<island>`, `rule:<rule id>`, `signal:<island>:<signal>` |
| where | `ahead:<m>`, `near:<island>:<m>:<direction>`, `receiver:<m>`, `sailing:<m>`. In `near`, `self` = the island where the WHEN happened (the window writes it when **of** is empty); the direction can also be degrees. End with `:needs=<blueprint>` (`engine`, `steering`, `controls` or a Raft item name, e.g. `near:self:700:east:needs=engine`) to hold the Receiver dot and the side placing until the player has that blueprint (the DOT choice) |

Two more parts put an island into the main story: `| first` / `after:<story island or rule id>` / `instead:<story island>` / `beside`
and when it's done: `quest`, `visit`, `step:<n>`, `zone:<zone>`, `signal:<signal>`, `note:<note number>` (empty: its quest, or reaching it). Three more parts style its notebook tab: `| tab title | tab colour | tab intro`; the colour is a number, 1 pink, 2 sand, 4 purple, 5 teal, 6 olive, 7 red, 8 green, 9 blue, 10 light blue (there is no 3; empty or 0 = by the island's place in the story). Other lines: `description = ...`, `storyending = ...` (the last page; `\n` = a new line), `endingtitle = ...` and `credits = ...` (the credits' title and the makers' names, `\n` between names),
`random = on/off` (no line: off), `story = on/off`, `storyleaveout = Balboa, Tangaroa`. The mod writes two more lines itself, which you leave as they are: `id = <12 letters and digits>` (which plan this is, whatever its name: a world remembers it, so a new plan later given an old plan's name isn't taken for an edit of it; plans from before 2026-10-10 and library plans have none) and `modversion = 3.0` (the version of the mod that saved the plan: a plan saved by a newer version keeps the lines this version can't read, and when it has some you are told to update the mod). Lines this version can't read are kept as they are. Save the file; the World plan window and the New Game box read it the
next time they open.

## 8. Playing together

Up to eight players (Raft's maximum). **Every player needs the mod.**

- **The host decides, for everyone:** the islands, the plan, the World settings (world rules, randomizer, extra
  options, which islands turn up while sailing; [section 9](#9-world-settings-rules-and-extra-systems)), and the host's
  `spawnpool.txt` settings that change what players see (Receiver dots, unload distance) and the world's regrow days. A player's own
  files and last New Game choices never change the host's world, and only the host can change its settings.
- **Players who join** (or join again, or after a restart) get all of it: the islands and any island files they don't
  have, what was chopped, picked, looted and fired, doors and levers, quests, the crew's journal, where they stood on
  an island, the tough animals' health and a kraken arm holding the raft.
- **Shared:** harvesting and pickups, chests (when several players open one at the same moment, the first gets it),
  zones, doors, quests and their rewards, story items, the journal, the sounds and music the islands' actions play
  (each player hears them while near the island) and camera moments (each player within range sees them). Things
  that move follow a clock all players share, so everyone sees them in the same place.
- **What grows back** is the host's decision: things come back when the island loads on the host after the regrow
  days; a player who has the island loaded sees it the next time it loads there.
- **Joining:** use Steam's "Join Game" on a friend (Raft's own Join World list is empty in this Raft version). Big
  islands take a while to arrive over the internet, and several players joining at once can make the host's game lag
  for a moment: let players join one at a time and stay near the raft until the islands have arrived. A Raft shark
  near a custom island can look different on a joined player's screen.
- **Notices a joined player may see:**
  - **"The host hasn't sent its custom islands yet (does the host have Custom Islands?). Still asking..."** - after
    you join, your game asks the host for its island list every 5 seconds. If nothing has come after 12 tries (about
    a minute) you get this notice once, and it goes on asking every 30 seconds until the list arrives. Usually the
    host doesn't have the mod, or is still busy loading.
  - **"Waiting for the island '<name>' from the host - still asking"** - an island file hasn't come within 30 seconds
    (a message got lost, the host is busy). It is asked for again every 30 seconds; the notice comes once for each
    island. `Resync` (F10) asks at once.
  - **"The host has Custom Islands 3.x - you have 3.y"** - the versions differ; both should install the same, newest
    one ([Everyone runs the same version of the mod](#everyone-runs-the-same-version-of-the-mod)).
  - **"An island from the host is left out: its name can't be a file name here (see the log, F10)"** - the host's island
    has a name this PC can't use as a file name: a folder in it (`\ / :` or `..`), a character Windows refuses, a
    name over 73 characters, a space or a dot at the end or a space at the start, or a Windows device name such as
    `CON`. That island is left out and the rest of the world plays. The editor never saves such a name, so it comes
    from a file renamed by hand: the host renames it.
- **Someone else hosts next time:** the host sends Raft's newest save of the world to every player after each save, so
  the next host just picks the world in **Load Game** ([Hosting on another day](#hosting-on-another-day)). The custom
  islands, what was used, quests, the journal, everyone's levels, the world's rules and its plan come along: the folder carries
  the mod's copy (`CustomIslands.txt`), and every player who joined keeps one too. Islands you only have from joining are used
  from their downloaded copies. Islands the plan hasn't brought yet are the one thing the new host may not have - see the
  table below.
- **A crew that splits up:** the host keeps an island loaded while any player is at it, so its animals, ambushes, doors
  and puzzles keep working for the players there while the host and the raft sail on, far away. It unloads once
  everyone has left it (beyond the unload distance).
- **Levels** (with the level up system on): each player's own, kept by the host and back when they join again; the
  host works out everyone's EXP, and each player sees the others' levels under their names. Only the host switches
  levels on, and a player's record with more EXP than the host credited them is put back to the host's.
- **What the host takes from players:** a code lock opened, a signal or rule fired, quest progress, a tree or pickup
  harvested is taken only from a player near that island (within the unload distance, where it is loaded on their
  side). A report about an island far from them is left out (the log says so) - a game out of step, or a cheat.

### Who needs what: every case

Only **the host's** islands, plan and settings count in a game. Players who join need **the mod, nothing else**: no
islands, no plans, no packs. What they're sent is kept on their PC (islands as `<name>_<code>`), which is what lets
them host the world later.

| Case | What happens | What anyone has to do |
|---|---|---|
| **A hosts, B joins** (any number of players, joining and leaving any time) | B gets every island that has appeared, with what was chopped, looted and fired, the quests, the journal, levels and settings - also after leaving and joining again, or a crash | Nothing |
| **A adds islands while B plays** (a plan's rule, a random island, a spawned one) | B gets each new island as it appears | Nothing |
| **B hosts A's saved world later**, A joins | B uses the world's own copy (it comes in Raft's world folder, and B kept one while playing): the islands that appeared (from what B was sent), what was used, quests, journal, levels, World settings, and the **plan** | Copy the world folder to B (below). If the plan still has islands to bring that never appeared, B doesn't have them: B sees which ones. A exports the plan (4.7) and B imports it, or B gets it from the island library |
| **A world made from a pack or library plan** hosted by someone who never installed it | As above; the message names the pack or library entry | Install that pack or entry |
| **A world saved before worlds kept their plan** (older versions), hosted by someone without the plan file | The plan's remaining rules can't run: the host is told when the world loads | The first host saves the world once with this version (the plan is then in the world), or gives the plan file |
| **The world's maker edits the plan** while worlds use it | The next time such a world loads on their PC, it plays the changed plan (they're told) | Nothing |
| **The plan is updated (library or pack) or deleted**, or another player hosts with a different plan of the same name | Worlds already started keep their own copy | Nothing (`WorldPlan <name>` in a world to switch it on purpose) |
| **An older save of the world is loaded** (Raft's Load Game box keeps the last 8 saves) | The custom islands, what was chopped, looted and fired, quests, journal and settings go back to that save as well | Nothing |
| **An installed island is updated** while a world uses it | That world gets the new one when its objects keep their order; else it keeps the version it started with (new worlds get the new one) | Nothing |
| **You remove a pack** that a saved world uses | What the world uses is kept (you're told which world) | Nothing |
| **A pack's island has the name of one of yours** | Installed as `Name (Pack title)`; yours is untouched | Nothing |
| **A pack was made with a newer mod version** | A warning; "Install anyway" may leave out what this version doesn't know | Update the mod if something's missing |
| **An island downloaded or imported later**, in a world with random islands | Only turns up if you allowed it (import) - also in worlds already started; a plan's islands never do | Untick it for a new world in World settings (9.5) |
| **Different players have different islands with the same name** | The host's is used; others get the host's as a copy and keep their own | Nothing |
| **B's Raft crashes and B joins again** | B gets the world as it is now, as on any join. Joining again within seconds of the crash can, now and then, leave B unable to open chests or set off zones ("The host didn't answer") | Wait about half a minute after a crash before joining again; if the message keeps coming, leave and join again |

### Hosting on another day

The world lives on the host's PC. The host sends Raft's newest save of the world to every player after each save (Raft
saves on its own every few minutes, and at **Esc > Main menu**) and to each player who joins. Each player gets it in
their own Raft `World` folder under the world's name, with Raft's older saves of it kept as backups, so whoever hosts
next just picks the world in **Load Game**.

- A world of your own with the same name is left alone: the host's goes beside it as
  `<name> (<8 letters of the world id>)`.
- Only the last 3 saves you received of a world are kept; Raft's own saves and yours are never removed. Leave the two
  small marker files in Raft's world folders as they are: `CustomIslands-world.txt` (which world a folder holds) and
  `CustomIslands-received.txt` (a save folder received from the host: only those are ever removed, the oldest first).
- A save over 64 MB packed isn't sent (the host is told), and a player who wasn't there for the last save doesn't have
  it. Then copy the folder by hand, below.

**Host from the newest copy.** The world goes on from the copy the host loads: the mod can't tell an older copy on
purpose from one by mistake. A player who joins with a newer copy keeps theirs aside
(`worlds\<world id>.kept-<n>.txt` in their `Mods\DynamicIslands`) and the host is told "<player>'s copy ... is newer -
your world may be missing their progress". So host from the copy of whoever hosted last. Don't keep and play two
folders of the same world (a "backup" you also play in): they share one set of custom-island progress on your PC (the
host is told when a world looks like a copy of another).

**Copying the world by hand:**
1. The player who hosted last presses **Esc > Main menu** (this saves the world) and waits for the main menu.
2. They open `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\User` in Windows Explorer, then their
   `User_<Steam id>` folder and `World`: each world is a folder with its name. They zip the **whole** folder (right-click >
   **Send to > Compressed (zipped) folder**) and send it to the next host. It carries the mod's copy
   (`CustomIslands.txt`) and every dated save folder; a single save folder or only the `.rgd` files are not enough.
3. The next host unzips it into their own `...\User\User_<Steam id>\World`. If a folder with that name is already there,
   rename it first (`My world - old`) - never paste over it: two copies merged into one folder mix their saves.

**The next host's own islands:** a world finds a plan's islands by their content, not their name, so an own island
named like one of the plan's is never brought by mistake: the host is told the plan's island is missing, and a player
who joins with it brings it. Get a missing island from the plan's maker (their pack, **Import...**) or the island library
entry the message names. (Worlds saved before 2026-10-06 still look up the islands not brought yet by name: there,
**Rename** an own island named like the plan's - F10 `WorldPlan` lists them.)

#### Everyone runs the same version of the mod

Joining with another version shows a warning naming both versions, and the game goes on. Every release of a version
says the same number (3.0), so each build also carries a short stamp: another build of the same version shows a notice
naming both stamps. Either way, everyone installs the same, newest release - check the **EXPERIMENTAL ALPHA RELEASE**
box on the main menu, which shows the version - and updates **before** the next session, not in the middle of an
adventure. A newer world hosted by an older version keeps the lines it doesn't know and warns ("saved by a newer Custom
Islands"); versions from before 2026-10-06 drop them (the story chain, levels, the plan's copy, private storages) when
they host and save.

---

## 9. World settings: rules and extra systems

Everything in this section is **optional and separate from island building**: switches that change how a whole world
plays, for players who know Raft well and want it different. None of them is needed to make or meet custom islands, and
a world where they are all left alone plays as plain Raft plus your islands.

- **World rules** (9.2): tougher or softer monsters, a dearer build menu.
- **World randomizer** (9.3): a normal Raft world made different every time: colours, alphas, finds, odd islands, bosses.
- **Extra options** (9.4): ghost rafts, private storages, long voyage, iron raft, shared EXP, night is dangerous, daily
  quest, rogue shark, silver & golden barrels, storm days, bioluminescent sea, storm seas, trader raft, extra upgrades,
  UFO sightings, lightning strikes, a ghost ship, fog banks, whale breach, kraken arm and a head start raft (older worlds
  may still have scrambled blueprints or the story islands in a new order on).
- **Islands while sailing** (9.5): which of your islands a world may meet.
- **The level up system** (9.6): EXP and stat points - a switch in the window (a world option: islands don't turn it on).

All of them belong to the world: chosen when it is created, saved with it, the same for every player (the host's), and
they come along when the world moves to another host ([section 8](#8-playing-together)).

### 9.1 The World settings window

Everything else the mod lets you choose for a new world is in one window, in four groups. Each part has a **?** or
an explanation. **Raft's own** puts every setting back to plain Raft; **Defaults...** opens this PC's `spawnpool.txt` settings ([section 10](#10-settings-files)); **Done** closes the window.

![The World settings window](images/newgame-worldsettings.jpg)
*The World settings window: the world rules and the world randomizer on the left, the extra options on the right.*

| Group | What it does |
|---|---|
| **World rules: monster difficulty** | How tough monsters are in this world: Timid, Normal, Fierce, Savage or Nightmare (see [section 9.2](#92-world-rules-monster-difficulty-and-build-cost)) |
| **World rules: build cost** | How many more materials the build menu costs: Raft's own up to +100 %, rounded (never below Raft's own) |
| **Rare animal looks** | How often cluckers, goats and llamas come with a rare look: Normal (Raft's own), More or Lots, with three new looks per animal (see [section 9.2](#92-world-rules-monster-difficulty-and-build-cost)) |
| **World randomizer** | A normal Raft world made different: a ▼ list with Off, Light, Normal or Wild (each says what it means), and which parts take part (lit parts are on: click a part to switch it off; see [section 9.3](#93-the-world-randomizer)) |
| **Extra options** | More ways to play Raft again, for players who know it by heart: each switched **ON** or off with its own button (below) |
| **Islands while sailing** | Which of your islands (and which kinds of new islands) turn up by chance while you sail in this world: **CHOOSE ISLANDS...** opens the list (below) |

**Raft's own** sets monsters to Normal, the build cost to Raft's own, rare animal looks to Normal, the randomizer to Off and every extra option to
off, the level up system too (it leaves the island list alone). The line at the bottom of the window sums up what the world will get. Nothing is
final until you click **Create** in the New Game box.

**In a world:** press **Esc → Custom Islands** (a button in Raft's pause menu). The world's own settings window
opens: monster difficulty, build cost, the world randomizer, the extra options, the level up system and the islands
while sailing, plus the world's plan and story (where each island's quest stands is in the quest book). The **host** changes them there, and every player gets the change at
once. Players who joined see the host's settings, greyed out. (While you try an island from the editor, the same
window has **Back to the editor**, [4.9](#49-trying-the-island-in-a-world-test).)

The window is compact so it fits a 1366 x 768 screen: two columns of small groups, tick boxes for the randomizer's
parts, the extra options and the islands while sailing (two to a line), the options and islands in short scrolling
lists, and the whole window scrolls when the screen is too short. Each group, and each extra option on its own row,
says in small letters what a change does **in a world already under way** (the table below). Point at any setting
and its full description shows in the line at the bottom of the window. Regrow days, the Receiver dots and the unload
distance are not in this window: the world's regrow days are the F10 command `RegrowDays <days>`, and the others are
the host's **Defaults...** in the New Game box (`spawnpool.txt`).

![The world's settings in a running world](images/world-window.jpg)
*Esc → Custom Islands in a world: the same groups as the World settings window, changed for this world.*

The host can also change every group with a console command (F10):

| Group | Show it | Change it (host) |
|---|---|---|
| Monster difficulty | `Monsters` | `Monsters savage` |
| Build cost | `BuildCost` | `BuildCost 25` |
| Rare animal looks | `AnimalLooks` | `AnimalLooks lots` |
| World randomizer | `Randomizer` | `Randomizer wild`, `Randomizer -alphas +bosses` |
| Extra options | `WorldOptions` | `WorldOptions +ghostrafts -privatestorage` |
| Islands while sailing | `WorldIslands` | `WorldIslands -<island>`, `WorldIslands +<island>`, `WorldIslands all` |
| How often they come | `WorldIslandsGap` | `WorldIslandsGap 5-12` |

**Changing settings in a running world.** A change is saved with the world and sent to every player; nothing
already in the world is taken away. What each one does from the moment it changes:

| Setting | In a world already under way |
|---|---|
| Monster difficulty | At once, on the next hit - monsters already out too |
| Build cost | The build menu at once; a block built before gives back what it cost when it was placed |
| Rare animal looks | Animals that come from now on; an animal already out keeps its look |
| Regrow days (F10 `RegrowDays`) | Counts from the next time an island loads; an island's own rule still wins |
| Unload distance, Receiver dots and range (Defaults... / spawnpool.txt) | At once; kept in the host's spawnpool.txt for every world the host plays |
| Randomizer: colours, alphas | Animals not changed yet are looked at again at once; one already changed keeps its look |
| Randomizer: animals, finds, loot | Raft's islands not met yet; an island already looked at keeps what it got. Loot off puts moved crates and clams back at once |
| Randomizer: oddities, bosses, large | Counted while sailing from now on; switched off, one that is due doesn't come. Islands already here stay |
| Ghost rafts / Trader raft | The first after 1.5 km / 3 km sailed with it on; off, one afloat stays |
| Private storage | Only storages built while it is on are private; off, all open at once (their builders are remembered) |
| Long voyage | Spacing at once; fewer islands after the next random island |
| Iron raft, Shared EXP, Night danger | At once (the next shark bite, the next monster defeated) |
| Daily quest | On: a task when it is next light; off: today's task waits, unchanged |
| Rogue shark | On: today's chance is rolled at once; off: one already out stays until killed |
| Golden barrels | Barrels from now on; off: no extra loot and plain colours at once |
| Storm days | At once: a storm day starts, or a storm ends within seconds |
| Bioluminescent sea | At once: tonight's glow starts or fades within seconds |
| Storm seas | At once: Raft's next weather is more often a storm, and a storm blowing now rocks harder; off: no more broken floors (broken ones stay broken) |
| UFO sightings | The next sighting day has one; off, a saucer already in the sky finishes its crossing |
| Ghost ship | The next nightfall can bring one; off, a ship out goes at once |
| Fog banks | A bank due now rolls in (fading); off, the fog lifts within about half a minute |
| Whale breach | The first breach a few minutes later; off, a whale already breaching finishes its leap |
| Kraken arm | From now on (the next day or storm with one); off, an arm holding the raft lets go at once |
| Lightning strikes | The next storm can have bolts (one blowing now too); off, a bolt already in the sky finishes |
| Extra upgrades | Off: the recipes are hidden; upgrades built or worn keep working, learned ones are kept |
| Scrambled blueprints / Story order (retired, off only) | Untaken blueprints go back to Raft's places / the Receiver goes back to Raft's order |
| Level up system | At once; off keeps everyone's levels and takes the stat bonuses away until it is on again |
| Head start raft | Built once when the world is made: shown, can't be changed in a running world |
| Plan | Not changed here: the world keeps the plan it was made with (shown in the window) |
| Islands while sailing | The next random island is picked from the new list; islands already here stay |

### 9.2 World rules: monster difficulty and build cost

#### Monster difficulty

| Level | Monsters' health | Damage they deal to players |
|---|---|---|
| **Timid** | ×0.75 | ×0.75 |
| **Normal** | ×1 (as Raft made them) | ×1 |
| **Fierce** | ×1.25 | ×1.25 |
| **Savage** | ×1.5 | ×1.5 |
| **Nightmare** | ×2 | ×2 |

Monsters are the animals that fight players, on Raft's islands and custom ones: sharks, warthogs, bears, polar bears,
screechers, puffer fish, rats, hyenas, bees, angler fish and the bosses. Left as Raft has them: puffer
fish damage, and everything about Bruce and your raft (his bites on it, how often he comes, how soon he comes back).
In Peaceful monsters leave you alone (only Raft's Varuna Point and Utopia bosses still fight), and in Creative nothing can hurt you, so there mostly their health changes.

#### Build cost

Everything in the **build menu** (the hammer's foundations, floors, walls, roofs, stairs...) costs **0-100 % more**,
rounded to the nearest, never below Raft's own: up to +45 % one plank stays one; at +50 % one plank becomes two, two become three, three become five. Removing a block gives back
half of what it cost when it was placed (as in Raft; a later change of the cost doesn't change it), and repairing and reinforcing cost more too. The crafting menu (Tab) costs the same
as in Raft.

**Both rules** are the host's for every player, also players who join later. The host can change them in a world:
`Monsters savage` (the level's name; the first names `easy`, `moderate`, `hard`, `impossible`, which mean timid, fierce, savage and
nightmare, and the numbers 0-4 work too), `BuildCost 25` (any whole number from 0 to 100; the slider in the window moves
in steps of 5). At the main menu the same commands set the choice for the next new world.

#### Rare animal looks

Cluckers, goats and llamas sometimes come with a rare look (Raft has two per animal). **Rare animal looks** (World
settings, under the build cost) decides how often, and adds three new looks per animal, painted from Raft's own
textures:

| Level | How often an animal has a rare look | Which looks |
|---|---|---|
| **Normal** | About 1 in 8, as in Raft (88 % the usual look, 8 % and 4 % Raft's two rare ones) | Raft's own three |
| **More** | About 1 in 3 | Six per animal: Raft's three and three new ones |
| **Lots** | About half | Six per animal; golden is still the rarest |

| Animal | Raft's looks | New looks |
|---|---|---|
| Clucker | blue, brown, red | jade, speckled, golden |
| Goat | brown, grey, white | toggenburg, boer, golden |
| Llama | brown, patched, striped | piebald, cream, golden |

An animal keeps its look when the world is saved and loaded, and every player sees the same one (Raft's own rare look
message carries it). Changing the level later only changes animals that come from then on; at Normal, an animal that
already has a new look keeps it. Without the mod, an animal with a new look shows Raft's usual look.

**The collection:** every look you tame (catch with a lasso and keep in a pen) counts for the world. The journal's
quest list shows **Animal looks tamed** with x/6 per animal (a look still missing shows as ?), a note pops up for each
new look, and a bigger one when all six of an animal are tamed. The host changes the level in Esc → Custom Islands, or
with `AnimalLooks Normal`, `AnimalLooks More`, `AnimalLooks Lots` (or 0-2); `AnimalLooks` alone prints the level and
the looks tamed. At the main menu the command sets the choice for the next new world.

### 9.3 The world randomizer

The randomizer makes a **normal Raft world** play out differently every time, with or without custom islands, and
without touching Raft's story (the radio tower, Vasagatan, Balboa, Caravan Town, Tangaroa, Varuna Point, Temperance
and Utopia stay as they are). Choose it in the World settings window (New Game box, **WORLD SETTINGS...**): **Off**,
**Light**, **Normal** or **Wild**, and which parts take part (hover a level or a part: it says exactly what it does,
how often at each level, where, and what stays as Raft has it). The level sets how often things happen:

| | Light | Normal | Wild |
|---|---|---|---|
| Animals in another colour | about 15 % | about 30 % | about 55 % |
| Alpha fighters (sharks) | about 3 % (6 %) | about 6 % (12 %) | about 12 % (22 %) |
| An oddity island (after the first 1.5 km) | about every 12 km | about every 7 km | about every 4 km |
| A large island (after the first 3 km) | about every 20 km | about every 10 km | about every 7 km |
| A boss lair (after the first 4 km) | about every 40 km | about every 20 km | about every 12 km |

The parts (all on unless you click one off):

| Part | What you'll find |
|---|---|
| **Colours** | Animals and sharks (Bruce too) now and then in another colour: charcoal, ash, rust, moss, frost, night... lighter ones too (snow, cream, a pale shark: their textures lightened, dark parts too), and rarely a gold shark |
| **Animals** | More animals on Raft's islands, now and then puffer fish on the reef |
| **Alphas** | Rare bigger, darker **alpha** warthogs, bears, hyenas and screechers (3× health), and a huge **Big Bruce**. A banner warns you. Killed, they drop a **trophy head**, meat and leather |
| **Loot** | Some of the crates and giant clams on Raft's islands lie in other places; now and then extra crates and barrels |
| **Finds** | A **treasure hunt** (a map in a bottle on the beach leads to a buried chest), an **abandoned camp**, a **castaway's stash**, and on Raft's big islands a **den** with a guard and a hoard, and an **outpost** with props from the quest islands |
| **Oddities** | Small odd islands while you sail: a van, a caravan, a crashed plane, a stranded boat, a hermit's shack, a statue, rocket debris, a hut of raft blocks |
| **Bosses** | Now and then a **boss lair**: climb the plateau and a named beast (Old Ironhide, Frostfang, Ashmaw, the Tusk King, the Laughing One) wakes with two guards |
| **Large** | Now and then a **large island** as big as Raft's big ones, with a made-up name, animals, hidden loot, scenes from the quest islands, a den and up to two of the generator's landmarks (a wreck, a lighthouse, a jetty, a skyscraper...) |

![Randomizer colours](images/randomizer-colours.jpg)
*Colours on warthogs, from the left: Raft's own, snow, cream and charcoal.*

![Oddity islands](images/randomizer-oddities.jpg)
*The oddity islands. Each has loot and a note.*

![Large islands](images/randomizer-large-islands.jpg)
*Large islands: tropical or snowy, with warthogs and animals to catch, a made-up name ("Coral Cay") and a den whose
guard wakes when you walk in ("Something moves in the dark...").*

![Scenes from the quest islands](images/randomizer-scenes.jpg)
*Scenes on large islands, made from the props of Raft's quest islands, each with a chest and a note.*

![A den on one of Raft's islands](images/randomizer-dens.jpg)
*The Finds part can put a den on one of Raft's own big islands: a rock outcrop with a den inside, a guard and a hoard.*

A den's rock is a shell made to sit in a hillside. On a generated island the ground rises round it and buries its rim,
open only at the mouth; on Raft's own islands, whose ground can't change, a den goes only where the ground meets its rim
all round and the floor inside is even - where nothing fits, the island gets no den.

**Changing it later:** `Randomizer` (F10) shows what it does in your world; the host can change it, for example
`Randomizer wild` or `Randomizer -alphas`. Everything follows from the world's seed, so every player sees the same
colours, alphas and crates.

### 9.4 Extra options

More ways to play Raft again. Each option has its own button, switched **ON** or off, with an explanation under it, in one
list in the window's right column: about five are in view, scroll (mouse wheel or the bar) for the rest. They can be combined freely.

**Scrambled blueprints** and **Story islands in a new order** are no longer offered for new worlds (since 9 October
2026). A world that already has one on keeps it; World settings shows it only while it is on, so the host can switch it
off (or `WorldOptions -blueprints -storyorder`).

#### Scrambled blueprints (older worlds only)

The blueprints lying on Raft's story islands (Vasagatan, Balboa, Caravan Town, Tangaroa...) are swapped with each
other, so each is found on another story island than usual. The pickup's name tells you which blueprint you'll get. The
pairs come from the world's seed: every player finds the same blueprint in the same place, and no blueprint stays in its
own place. What the story needs is **never moved**: the Receiver and antenna, the steering wheel, the engine and its
fuel, the machete, the zipline, the headlight and the battery charger, so the story can always be finished. Only what a pickup gives changes;
which pickups there are, and which were taken, stays Raft's. Switching the option off (`WorldOptions -blueprints`) puts
what is left in the pickups back to Raft's own.

#### Story islands in a new order (older worlds only)

Radio Tower, Vasagatan, Balboa, Caravan Town, Tangaroa, Varuna Point and Temperance come in a shuffled order. Raft has
no fixed places for them: each appears near the raft when the Receiver is tuned to a frequency a note unlocked. With the
option on, the Receiver's first frequency leads to the new order's first island, and the note you find there to the
next. The frequency numbers written on the notes follow, with the name of the island each now leads to ("#1234 -
Caravan Town"); the notes' own words still speak of Raft's order. **Utopia**,
the ending, stays last. Each story island holds its own keys and parts, so any order can be finished. A world plan with its own story chain ([section 7](#7-world-plans-which-islands-a-world-gets)) sets the order instead: then this option does nothing.

#### Ghost rafts

Abandoned rafts of Raft's own blocks lie on the sea and come up ahead while you sail: none in the first 1.5 km of a
world, then about one every 3 km. Three sizes:

| Size | How often | What is on it |
|---|---|---|
| Small | about 6 in 10 | A few foundations, a barrel and a message in a bottle |
| Medium | about 3 in 10 | A hut of walls and a roof, a barrel and a box, a captain's log, sometimes a rat or two |
| Large | about 1 in 10 | A wide raft with huts, pillars and a lookout, a hoard chest and barrels, guarded by rats on the deck and screechers circling above |

You can walk on their decks (they float like a real raft, the deck just above the water) and loot what is on them.
Every player sees the same ghost raft: the host brings it, as an island of the map type `ghostraft`.

#### Private storages

A storage opens only for the player who built it. Looking at someone else's shows whose it is instead of "Open", and
**E** does nothing; the hammer doesn't take it down either. Storages built while the option was off (or before this version of the mod) have no builder and
open for everyone. The host keeps who built which with the world, so it stays after saving, loading and joining again.
The **host** may open the storage of a builder who isn't in the game (one who left and doesn't come back), so
nothing stays locked for good; other players still can't.

#### Long voyage

Random custom islands come half as often and lie further apart, so food, water and the raft matter more between
stops: one after every **6-12** of Raft's own islands instead of 3-6 (twice the world's span, [section 9.5](#95-islands-while-sailing)),
and at least **1.6 km** from each other instead of 800 m. Raft's own islands, and the islands of quests and plans,
keep their places. `WorldIslands` shows the span in use. Switched on in a running world, it counts from the next
random island.

#### Iron raft

The raft's blocks take **half damage** from shark bites, so there is less to repair. Taking pieces down with the
hammer or the axe works exactly as before, and shark bait is eaten as fast as ever.

#### Shared EXP

When a player defeats a monster, **every other player within 50 m** of it gets **60 %** of its EXP too (less what
their own hits on it already earned, so a helper ends up with at least 60 %). It doesn't count as a defeated monster
for them. Only defeated monsters are shared; playing alone nothing changes. See [Earning EXP](#earning-exp) for what each
monster is worth.

#### Night is dangerous

After dark the sea and the islands turn harsher: monsters have **x1.3** health and damage (on top of the world's
monster difficulty) and the shark comes for the raft more often (it looks for a block to bite in 0.6 of the usual
time). By day both are calmer: monsters x0.85, the shark less often (x1.25 the time). Day and night are Raft's own sky's.
How many monsters live at each spot stays as the island's builder made it.

#### Daily quest

Each morning the crew gets a small task for the day: **gather** some of a basic resource (12-20 planks, 10-16 plastic
or palm leaves, 4-8 scrap: what the players pick up by hand or with the hook), **catch 3-6 fish** with the rod, or
**defeat 2-4 monsters**. Every player's part counts towards it. Done before dark, each player gets a small reward once:
one or two of planks, rope, nails, scrap, raw fish, a watermelon or clay (and 40 EXP with the level up system on).
Not done by dark, it runs out; the next morning brings a new one. The task shows as a banner when it comes and as it
moves on, and at the top of the journal's quest list. Under the page on the right, the journal also keeps a **Daily
quest** block while the option is on: what to do and how far along (with a bar), the reward, whether it's done (and
whether you got the reward), and when the next one comes (at daybreak). `DailyQuest` shows today's task.

#### Rogue shark

Now and then (on about one day in eight, never before day 3) a **second shark** comes beside the usual one. It is
**rust-red**, and it doesn't leave on its own: it stays until it is killed. Once it is, no other comes for **5-8 days**.
About one rogue in five is a **giant**: twice Bruce's size, with three times his health and bites half again as hard on
players (its bites on the raft are a normal shark's). Its banner says "A giant rogue shark".
A banner says when it comes and when it dies. While it lives, the usual shark still comes back when killed; the rogue's
body brings no new shark. It is saved with the world, keeps its colour after a load, and every player sees the same one.
Switching the option off leaves a living rogue until it is killed. `RogueShark` shows where it is; the host's
`RogueShark giant` brings a giant one now.

#### Silver & golden barrels

Now and then a barrel drifting in the sea is **silver** (about 1 in 40) or **golden** (about 1 in 150). Pick it up by
hand or with the hook: besides Raft's usual barrel loot, a silver one holds **3-6 nails, 1-2 rope and 1-2 scrap**; a
golden one **6-10 nails, 2-4 rope and 2-3 scrap**, and one in three also a battery or bolts. A banner says what was
inside. Every player sees the same barrels shine; the extra goes to whoever picks it up. Switching the option off makes
new barrels plain again.

#### Storm days

On about one day in ten (never before day 3) the sea is **rough and it rains** from morning to the next morning
(in the snow region: Raft's rough snow). The shark comes for the raft **more often** (it looks for a block to bite
in 0.6 of the usual time, on top of Night is dangerous), and the rain waters the crops as Raft's rain always does.
At **nightfall the evening before**, every player is warned; a banner says when the storm day begins. Raft has no
rain collector of its own, so the rain helps the crops only. Which days are stormy comes from the world's seed: a
loaded world has the same storm days, and every player has the same weather. `StormDays` shows the next one.

#### Storm seas

A rougher sea for players who find Raft's weather too kind. It uses **Raft's own storms** (rain, or rough snow in the
snow region), only more of them, and they hit harder:

- **More storms:** when Raft picks its next weather, a calm pick turns into a storm **half the time**, and calm spells
  last **half as long**, so a storm is never far off. Raft's calm-only weather (the very start of a world) is left alone.
- **The raft rocks harder:** in a storm the raft tilts on the waves **two and a half times** as far as Raft's own, with a
  slow sway of its own on top, but never more than 12 degrees. The rocking eases in over about 20 seconds as a storm
  comes and eases out after it, so the raft never jumps.
- **A broken floor:** each storm has a **1 in 4** chance to break one floor (foundation) of the raft, some 15-45
  seconds after it starts. Only a foundation on the **edge or a corner** breaks (one with an open side), **never one in
  the middle**, and the storm picks one whose loss keeps the raft in one piece when it can. It goes the way Raft takes
  a destroyed block away: **what stands on it falls** with it. A banner tells every player: *The storm broke the raft*.
  A raft of 4 foundations or fewer is never broken.

Only the host decides: which storms break a floor and which floor comes from the world's seed and the storm's number,
so a world taken over by another host goes on the same way. Every player sees the same storm and the same broken
floor (Raft sends both itself), players who join later get the raft as it is, and the storms counted and the floors
broken are saved with the world. Switched off in a running world, no more floors break and the raft rocks as Raft's
own; a floor already broken stays broken. `StormSeas` (F10) shows the weather now, the storms counted, the floors
broken and how many foundations are on the edge.

#### Trader raft

Very rarely - about once in 7 km of sailing, never in the first 3 km - a **trader's raft** comes up ahead: a small
raft with the trader's hut, a lantern, a note, and **a stall for each offer** (three or four). Look at a stall:
**[E] Trade 12 Plank for 1 Watermelon seed (3 left)**. Trading takes the price from your inventory and gives the goods;
without the price the stall says what you are short of.

| Stall | Price | Goods | Stock |
|---|---|---|---|
| Fruit | 10-14 planks | 1 fruit seed (mango, pineapple, banana, watermelon or strawberry) | 2-3 |
| Flowers | 6-9 plastic | 2 flower seeds of one colour | 2-3 |
| Tree | 8-12 planks and 2-3 rope | 1 birch, pine or palm seed | 1-2 |
| Blueprint (on about 4 traders in 10) | 16-21 planks, 8-12 plastic, 4-6 scrap | 1 handy blueprint (canteen, decorative plank, firework, machete, head light, large storage or metal detector; never the story's) | 1 |

The stock is **shared**: what one player buys is gone for everyone, it is saved with the world, and it doesn't fill up
again. Every player sees the same trader with the same offers: the host brings it as an island of the map type
`traderraft`, and its offers come from the raft's seed. `Traders` lists the stalls loaded now and their stock.

#### Extra upgrades

Better versions of Raft's own things to craft. Each one has a strong tint of its own and a few slow **glints** in that
colour round it (a worn one while it is worn), so an upgrade is told from its base at a glance; the titanium spear is
a pale, almost white metal instead. One held in the hand is also a little bigger (built
ones keep their size, so they still fit on the grid next to other pieces). It sits in the crafting menu next to
the thing it improves (the **base**), once it is learned at the **upgrade research table** (below):

| Upgrade | What it is | Cost | Base |
|---|---|---|---|
| **Large battery** | A blue battery that holds **twice the charge** of Raft's battery: a machine runs twice as long on it | 2x the battery | Battery |
| **Titanium grill** | An advanced grill that **cooks twice as fast** | 2x the advanced grill + 2 titanium ingots | Advanced grill |
| **Blast furnace** | A smelter that **smelts twice as fast** | 2x the smelter | Smelter |
| **Reinforced storage** | A large storage with **half as many slots again** | 2x the large storage | Large storage |
| **Wide net** | An advanced collection net that catches from a **twice as wide** area and holds **twice as many** items | 2x the advanced net | Advanced collection net |
| **Large fuel tank** | A fuel tank that holds **twice as much** fuel | 2x the fuel tank | Fuel tank |
| **Large water tank** | A water tank that holds **twice as much** water | 2x the water tank | Water tank |
| **Greenhouse plot** | A large crop plot that **waters itself**: its plants never go dry | 2x the large crop plot | Large crop plot |
| **Turbo engine** | An engine that pushes **twice the weight** and drives the raft **half again as fast**, but burns fuel faster (a fuel lasts 60% as long) | 2x the engine | Engine |
| **Large wind turbine** | A wind turbine that charges batteries **twice as fast** | 2x the wind turbine | Wind turbine |
| **Comfy bed** | Sleeping heals **twice as fast** and makes you half as hungry and thirsty; you wake up from death with **75%** health, food and water instead of 50% | 2x the bed | Bed |
| **Magnet hook** | A titanium hook that **pulls in half again as fast**, **picks things up twice as fast** and lasts twice as long | 2x the titanium hook | Titanium hook |
| **Titanium rod** | A fishing rod on which **fish bite twice as fast**; it lasts twice as long | 2x the metal rod + 2 titanium ingots | Metal fishing rod |
| **Swift flippers** | Flippers that **add twice the swimming speed** the flippers add (x1.8 instead of x1.4); they last twice as long | 2x the flippers | Flippers |
| **Large air tank** | An oxygen bottle that makes you **lose air half as fast** under water as the bottle does; it lasts twice as long | 2x the oxygen bottle | Oxygen bottle |
| **Rapid charger** | A battery charger that charges **twice as much per fuel** | 2x the battery charger | Battery charger |
| **Fast recycler** | A recycler that works **twice as fast** | 2x the recycler | Recycler |
| **Bright lantern** | A metal lantern that lights **twice as far** and half again as bright | 2x the metal lantern | Metal lantern |
| **Telescope** | Binoculars that **zoom in twice as far** | 2x the binoculars | Binoculars |
| **Titanium greatsword** | A titanium sword that **hits half again as hard**; it lasts twice as long | 2x the titanium sword | Titanium sword |
| **Titanium spear** | A metal spear that **hits half again as hard**; it lasts twice as long | 2x the metal spear + 2 titanium ingots | Metal spear |
| **Long paddle** | A paddle that **pushes the raft twice as hard**; it lasts twice as long | 2x the paddle | Paddle |
| **Floodlight** | An advanced head light that lights **twice as far** and half again as bright; it lasts twice as long | 2x the advanced head light | Advanced head light |
| **Steel pot** | A cooking pot that **cooks twice as fast** | 2x the cooking pot | Cooking pot |
| **Fast juicer** | A juicer that **makes juice twice as fast** | 2x the juicer | Juicer |
| **Titanium machete** | A machete that **hits half again as hard**; it lasts twice as long | 2x the machete + 2 titanium ingots | Machete |
| **Sturdy scarecrow** | An advanced scarecrow that **never wears out** | 2x the advanced scarecrow | Advanced scarecrow |
| **Master hammer** | A building hammer that **repairs twice as much per swing**; it lasts twice as long | 2x the building hammer | Building hammer |
| **Longbow** | A bow that **draws half again as fast and shoots half again as far**; it lasts twice as long | 2x the bow | Bow |
| **Bright head light** | A head light that lights **twice as far** and half again as bright; it lasts twice as long | 2x the head light | Head light |
| **Bright lamp** | A lantern that lights **twice as far** and half again as bright | 2x the lantern | Lantern |
| **Lumber axe** | A titanium axe that **takes building pieces apart twice as fast**; it lasts twice as long | 2x the titanium axe | Titanium axe |

None of them needs a new model: they are Raft's own machines and tools, each in a colour of its own (worn ones are tinted
while they are worn). Raft's purifiers, biofuel refiner and beehive have
no upgrade, and neither does the sail (it only steers). A removed reinforced storage, titanium grill or blast furnace
gives back what it holds, as the base does.

The large battery fits **every machine that takes a battery** (electric grill, electric smelter, electric purifier,
juicer, recycler, sprinkler, the Receiver) and charges in the **battery charger** like a battery: twice as long, for
twice the fuel. In a machine it shows as a blue battery. Switch the option off and the upgrades are gone from the
crafting menu again (large batteries already made keep working, what is built stays); switch it back on and the ones
learned are back.

##### The upgrade research table

The upgrades are learned at a research table of their own, not with their base: learning the battery doesn't teach
the large battery. With the option on, the **upgrade research table** is in the crafting menu from the start, next to
Raft's research table. It is Raft's research table in **violet** and costs **four times** as much.

It works like Raft's table. Put an item in its slot and press **Research**. The menu lists only the upgrades, with the
items each one needs; an upgrade can be learned once **every item it costs** is researched there (one of each - a
titanium grill needs the advanced grill's items and titanium). Press **Learn** and every player can craft it. Raft's
own things are not in this menu, and the upgrades are not in Raft's.

The two tables keep **separate** research: what you research at Raft's table doesn't count at the upgrade table, and
the other way round: an item researched at Raft's table has to be researched again here for an upgrade that
needs it. An item that only upgrades need (none of Raft's recipes) can only be researched at the upgrade table.

What is researched and learned there is the world's, for every player, and is saved with it. A world made **before**
the upgrade table keeps what it had: the upgrades whose base was learned stay learned. The console command
`UpgradeTable` lists what a world has researched and learned there. If the table is picked up or broken while a player
has its menu open, what lies in its slot goes back to that player's inventory.

Every player needs the mod, as for any world with custom islands. The upgrades are saved with the world; a world
loaded **without** the mod loses them (a machine's slot comes back empty, a built upgrade is gone).

#### UFO sightings

Now and then a **flying saucer** crosses the sky high above the raft: it comes out of the haze on one side, flies
straight across (100-150 m up, in about half a minute) and is gone over the other horizon. It is there to look at -
nothing to catch, nothing it does to you - so it pays to look up now and then.

- **When:** on about two days in five (about three times in a week of Raft days, never on the first two days), at one
  hour of that day: half of them by day (8:00-18:00), half at night (21:00-3:00). Which days and hours come from the
  world's seed, so a loaded world has the same ones. A day slept through misses its saucer.
- **What it looks like:** a flat metal disc in a **new colour every time**, with a glass dome on top, a dark underside, a
  rim band and a ring of rim lights that blink round as it **spins**; it bobs and sways a little as it flies.
- **At night** it **glows green or purple**: the rim lights take that colour, a soft halo shines around it and a light
  under it lights its underside. By day only the rim lights shine.
- **Every player sees the same saucer at the same time:** the host picks its colour, glow, path and speed and sends them
  to everyone; each machine flies it on the same path over its raft. Switched on in a running world, the next sighting
  day has one. Nothing is saved.

#### Bioluminescent sea

On about **one night in four** the sea around the raft **glows blue**. Tiny lights twinkle on the waves and drift just
under them, thicker close to the raft, in deep blue, cyan and now and then nearly white; soft blue patches light the
water here and there, and a blue light low over the water lights the raft from below. Swim, and the water **sparks**
around you. Raft's **fish shine** and leave a few sparks behind them; a cast bobber has a ring of sparks, and when a
fish takes the bait it **flashes**.

- **Fish bite faster:** on a glowing night the wait for a bite with a fishing rod is **halved** (on top of the
  titanium rod of Extra upgrades). Otherwise the glow is only there to look at.
- **When:** the glow fades in over a few seconds when night falls (Raft's own night: dusk to dawn) and fades out at
  dawn. A banner says "The sea glows tonight" when it starts. Which nights glow comes from the world's seed, so a
  loaded world glows on the same nights.
- **Every player sees the same glowing nights:** the host's options decide, and Raft keeps the time of day the same
  for everyone. Each player sees the glow around the raft (or around themselves when far from it). Nothing is saved.
- `GlowSea` shows whether tonight glows and the next glowing night; `GlowSea roll <from> <to>` lists them.

#### Fog banks

Now and then **thick fog rolls in** over the sea for a few hours. In it you see only **45-75 m**: an island shows up
late, as a grey shape that firms up as you sail closer, so the **Receiver's radar** matters more than your eyes.

- **When:** on about three days in five (never on the first two days), at any hour of that day or night, for **three to
  six hours**. Each bank has its own thickness. Which days and hours come from the world's seed, so a loaded world has
  the same banks.
- **It fades:** the fog thickens over about an in-game hour and thins out the same way at the end - never a jump. A
  message says when it rolls in.
- **Raft's own sky draws it,** with a veil in the fog's colour over the whole view so the sea fades too, by day grey-white and at night dark, with Raft's colours of the hour; Raft's weather
  goes on as before underneath. **While a storm blows** (rain, rough snow, big waves - Raft's own or Storm days) the
  wind keeps the fog away. Under water it does nothing.
- **Every player sees the same fog at the same time:** each machine works out the banks from the host's world seed and
  the clock, so nothing is sent. Switched on in a running world, a bank due now rolls in at once (slowly); off, the fog
  lifts in about half a minute. Looks only: nothing is saved.
- `FogBanks` shows whether fog is in and the next bank; `FogBanks roll <from> <to>` lists them.

#### Whale breach

Now and then, by day, a **whale breaches** far out at sea, **170-320 m** from the raft: it rises out of the water, leaps
half its length into the air, rolls onto its side, falls back with a **big splash and spray**, and dives away. It takes
about eight seconds. It is Raft's own whale, with its own spout.

- **When:** about once or twice a Raft day, at random. The first one comes a few minutes after the world loads; the
  next one comes after 8 to 15 minutes. Never at night: a breach that is due waits for morning.
- **Where:** always in open sea (never close to an island or a floating raft) and side-on to the raft, so you see the
  whole whale.
- **Looks only:** it never comes near the raft or the players, nothing can hit it or catch it, and it is gone after the
  dive. Nothing is saved.
- **Every player sees the same whale:** the host decides when and where it breaches and sends that to everyone; each
  machine plays it at the same place by its own raft. Switched on in a running world, the first breach comes a few
  minutes later; off, a whale already breaching finishes its leap.

#### Kraken arm

![A kraken arm holding the raft's edge, a second arm curling beside it](images/world-kraken-arm.jpg)

Rarely, with a warning splash and a banner, a **giant tentacle** rises out of the deep beside the raft, curls over the
side and **grabs a floor on the raft's edge** - never one in the middle. Its base is far down in the water, so you
never see where it starts. One to three more arms rise **3-8 m off the edge** around it and sway and curl there; they
swipe: about every seven seconds each one **rears up for a second and slams down where the nearest player within 9 m stood**. A player still there takes **5 damage** (green and blue krakens 10% more; Raft's monster difficulty scales it), with a thump - and **one sighting takes at most 30 health from a player and never brings them below 25 health**, so a player can always beat it; step away when an arm rears up and it misses. One player alone can still fight it off. The arms are purple and darken toward red as they are hit, with pale suckers on the inner curl.

![A free kraken arm swinging down at the raft's edge where a player stands](images/world-kraken-swipe.jpg)

- **Fight it off:** hit the grabbing arm with a **melee weapon or a spear** (any swing that reaches it counts; arrows
  don't). **10 hits within a minute**, by any players together, and it lets go and sinks with all the other arms. The
  raft is whole.
- **Too slow, and it tears the raft:** after a minute it **tears the floor it holds away**, dragging it down into the
  deep. **Each floor right beside it** (the four sides on the same level, not the corners) **goes too with a 1 in 2
  chance**, rolled for each one on its own - so it takes one to five floors. What stood on those floors goes as when
  Raft removes a floor: it falls, drops or breaks as Raft does it. Torn floors stay torn, saved with the world.
- **When:** on about **one day in eight from day 8** (never before), at a time in that day (1-10 minutes after the day starts while you
  play); with **Storm seas** on also in about **one storm in four**. Which days and storms, the floor it grabs and the
  floors beside it that go all come from the world's seed. A raft of **8 floors or fewer** is never grabbed. It **never comes near land**: while the raft is within 80 m of an island's shore (Raft's own or a custom one) a due arm waits, checking every half minute, until the raft is in open sea or the day ends.
- **Colour:** each kraken arm (the whole sighting) is purple 3 times in 4, else greenish (1 in 8) or bluish (1 in 8); every player's PC shows the same colour.
- **Every player sees the same arms on the same floor,** and hits from every player count: the host decides when it
  comes, counts the hits and tears the floors with Raft's own block removal, so every raft loses the same floors. A
  player who joins while it holds on gets the arm. Nothing about an arm is saved: a world loaded mid-event has none.
  Switched on in a running world, the next day or storm with one brings it; off, an arm holding the raft lets go.
- `Kraken` (F10) shows whether an arm is out, its hits and the next days with one; `Kraken bring` brings one now,
  `Kraken away` sends it away and `Kraken tear` makes it tear now (host).

#### Ghost ship

On about **one night in four** a **ghost ship** comes up out of the dark a while after nightfall and drifts past the
raft, close enough to **swim or sail to**: a wrecked ship of Raft's own pieces - a foundation hull with a pointed bow,
a stern cabin with a roof, railings, a mast with a flag and a torn sail, an anchor and lanterns - lit by a pale
**cyan glow** in a thin mist. Climb aboard by the ladders on either side.

- **Its loot is good:** a large chest (the ghost ship's hoard: titanium, explosive goo, circuit boards, a battery,
  good healing salves, honey, metal, copper, bolts, hinges), the captain's chest in the cabin, two chests on deck and
  barrels. The captain's log lies on the cabin floor.
- **Its crew are ghosts:** Raft's own monsters, made **see-through with a faint glow** - hard rats on deck and a
  screecher or two over it. They don't come back once beaten.
- **At dawn it sinks:** a banner says so, and over about 20 seconds it goes down and its lights fade. **What is left
  aboard is lost** with it - the chests, their loot and the ghosts. Take what you want before the sky grows light.
- **Sleep, and it doesn't come:** if the players are asleep when night falls, or go to sleep while it is out, there is
  no ship that night (one out is gone at once).
- **When:** which nights have one comes from the world's seed (`GhostShip roll <from> <to>` lists them). It is rolled
  when night falls while you play: a world loaded in the middle of the night waits for the next nightfall.
- **Every player sees the same ship, the same ghosts and the same loot:** the host brings it, as an island
  (`gen-ghostship-night-<seed>`) of the map type `ghostship`, and sinks it; a chest is claimed like any island chest,
  so its loot goes to one player only. A player joining in the night gets the ship that is out. Nothing about it is
  saved: a ship that was out when the world was saved is gone when it loads by day. Switched on in a running world,
  the next nightfall can bring one; off, a ship out goes at once.
- `GhostShip` (F10) shows whether one is out and the next night with one; `GhostShip bring` brings one now and
  `GhostShip sink` sinks it (host).

#### Lightning strikes

Now and then, **in a storm, lightning strikes the sea** near the raft: a bright **flash** lights up the sky and the
sea, a jagged **bolt** with a few forks stands from the clouds to the water for a moment and flickers out, and a
**thunder** clap rolls (Raft has none of its own: it is made of Raft's bang and rumble sounds). It is there to look at - it does no damage, starts no fire and breaks nothing.

- **When:** only while a storm blows - Raft's own rain or rough-snow storms, and the storms of Storm days and Storm
  seas. It is rare: about three storms in ten have no bolt at all, the others one to four, the first within about three
  minutes of the storm's start and the next ones one to three minutes apart.
- **Where:** a bolt hits the sea **30-150 m from the raft**, in any direction. **Very rarely** (about one bolt in twelve)
  it hits the raft itself instead: the top of the **Receiver's antenna** or a **wind turbine**, the raft's highest
  metal. Raft has no lightning rod, so those two are the rod; a raft without either always gets the sea.
- **Looks only:** nothing on the raft is damaged or set on fire, and no player is hurt, also when it hits the antenna.
- **Every player sees the same bolt at the same time:** the host decides when and where a bolt strikes and sends it to
  everyone; a sea bolt hits the same spot next to the raft on every machine. Switched on in a running world, the next
  storm (or the one blowing now) can have bolts. Nothing is saved.

#### Head start raft

The world starts on a bigger raft that is already set up, to skip the first hours. The button goes **off -> LEVEL 1 ->
LEVEL 2 -> LEVEL 3 -> off**:

| Level | Raft | On it |
|---|---|---|
| 1 | 6 x 12 foundations | a simple grill, a simple purifier, the research table, a bed and a small storage with 10 planks, 10 plastic, 10 rope, 10 stones, 5 raw mackerel and 5 potatoes |
| 2 | 8 x 14 | level 1, plus 2 smelters, a sail, 10 item nets in the water and 2 medium storages: 10 each of sand, clay, stone, scrap, metal ore, copper ore, seaweed, palm leaves, dirt and vine goo, 2 batteries and 2 flippers |
| 3 | 10 x 20 | level 2, plus the Receiver with its 3 antennas and a battery in it, and 2 more medium storages (40 planks, 40 plastic and more) |

The host builds it once, a moment after the new world first loads, around Raft's start raft. They are Raft's own blocks,
saved with the world like any you build. A joiner gets them with the world. It is never built twice: the world file
remembers it is built (`@headstartdone=1`), and a raft that already has more than 12 foundations is never built on.
`HeadStart` (F10) says what this world has and what the next new world gets. In the main menu `HeadStart 2` chooses
level 2 for the next new world.

#### For every player

Every player in the world gets the host's options, also when joining later. The host can change them in a world:
`WorldOptions` (F10) shows them, `WorldOptions +ghostrafts -privatestorage` switches them.

### 9.5 Islands while sailing

With the plan **Random islands** (or a plan with random islands on), islands turn up by chance while you sail: every
island you have saved or downloaded, plus brand-new generated ones and the map types listed in `spawnpool.txt`
([section 10](#10-settings-files)). The group **Islands while sailing** in the World settings window chooses which of
them this world may meet. Its button, **CHOOSE ISLANDS...**, says how many take part (`all 42`, `30 of 42`, or `none` when you untick them all: then only a world plan's own islands come) and
opens the list:

- one compact row per island or kind of island, A to Z: a **tick box**, a **picture**, the name, a badge, two lines of
  the island's description and, in small print, whether it is a library island or your own, its author, its
  **Difficulty** (Easy to Impossible, counted from its monsters as in the library, [4.7](#47-saving-and-sharing)),
  size and date. The map-type and generated rows have none: they are new each time.
  The badge says **QUEST** (golden) when the island has a quest - any of its quests with steps - and **no quest**
  otherwise. Point at a row to read its whole description. First come **New ... islands** for each map type (a new
  sandbar, wreck, atoll... each time; badge **NEW EACH TIME**, the map type's description, a picture of one of Raft's
  islands) and **Brand-new generated islands**, then your islands;
- the picture of a downloaded library island is its library picture (when the library's list can be read); any other
  island gets a small map of its land, made once the first time the list shows it and kept in
  `Mods\DynamicIslands\thumbcache\` (made again when you save the island; safe to delete). Until a map is ready the
  frame is plain;
- **Brand-new generated islands** is the mod making up a new random island now and then while you sail: random size
  and style (tropical, snowy, desert, forest or volcanic), sometimes flying. Each one is saved as a `gen-<style>-<seed>.island`
  file and stays in that world (Tidy up moves unused ones away; a world that needs one later gets it back from `deleted`). Ticked, they join the pool next to your single islands and the
  map-type rows; unticked, only islands you made or downloaded and the map-type kinds come. How often they are picked is
  `generated` in `spawnpool.txt` ([section 10](#10-settings-files)): by default one comes as often as any one island of
  the list, whenever a list island is due (after every 3-6 of Raft's islands, the span under the button). The files are in
  `Mods\DynamicIslands`. **To keep one you liked:** EDITOR > An island > **Open an island...** lists them (marked "made by
  the generator"), as do the editor's **Open** and **My islands...** (Generated); open it and **Save as** a name of your
  own - then it is your island, in this list by its name. The **?** at the right end of the row says all this in the game;
- untick the ones this world shouldn't have;
- the **search** field narrows the list (type part of a name), and **Tick shown** / **Untick shown** do every row
  shown, for example all your test islands at once. **Untick all** unticks every row of the list, whatever the search
  shows; with any row unticked the same button says **Tick all** and ticks them all again.
- **Islands of world plans are never in the list**: the islands a world plan brings - each plan's own story islands,
  and the islands those bring - only come in their own plan, so you never meet another plan's island out of its story.
  The list holds the single islands: islands made or downloaded to be found while sailing
  ([the two groups](#single-islands-and-plan-islands)). The world randomizer's own
  extras (`rnd-...` files) are never in it either. In the journal and other lists an extra is called by what it holds (**A castaway's stash**, **An abandoned camp**, **A treasure hunt**) or **Randomizer finds**, never by its file name.

Only what you untick is kept, so islands you make or download later join older worlds too, unless you untick them when
you make a new world; an island whose file you delete later is simply left out (no error). The choice belongs to the world (saved with it, so it stays when another player hosts the world
later), and the next new world starts from it. In a world the host can change it with `WorldIslands -<island>` /
`+<island>` / `all`; `WorldIslands` alone shows the list.

**How often:** under the button, **One after every [-] 3 [+] to [-] 6 [+] of Raft's islands**: an island from the list
comes after every so many of Raft's own islands you meet - a number between the two, new each time. **3-6** by default;
the lower number can be **2 to 20**, the higher **4 to 50** (in ones up to 10, then in fives), and the higher is never
below the lower. So 2-4 brings custom islands often, 20-50 rarely - however many islands are ticked, they never crowd
the sea. The span is saved with the world; in a world the host can change it with `WorldIslandsGap 5-12`
(`WorldIslandsGap` alone shows it and how many of Raft's islands were met since the last one). No island comes by chance
in a new world's first 10 minutes of play (`quietMinutes` in `spawnpool.txt`).

Islands that a quest, an island's rule or a world plan brings ([section 6.4](#64-islands-that-bring-islands),
[section 7](#7-world-plans-which-islands-a-world-gets)) come anyway: the list is only about islands by chance.

### 9.6 The level up system

Levels are a world option: the **Level up system** switch among the extra options of **World settings** (New Game
box) turns them on for the world from the start (the choice is remembered for the next new world). Islands don't turn
them on: the editor and the generator have no level up choice, and an older island or preset that had one still loads
(the old choice is ignored).

The host can switch it in a world with `Levels on` / `Levels off` (F10). **Off keeps everyone's levels** (they come back
when it is on again). A world
without it plays as Raft always does.

#### Earning EXP

Hit a monster and the EXP it gave you floats up over it. Each hit gives the share of the monster's EXP that it took off
its health, so killing it gives all of it. If you fight it together with a friend, each of you gets your own share.
Every weapon counts: spears, swords, axes, arrows, thrown stones and fireworks (the EXP goes to whoever lit
the firework). The Damage stat makes your weapons hit harder; a firework hits as hard as Raft makes it.
Chickens, goats, llamas, turtles, stingrays, dolphins, whales, puffins, Utopia's butler bots and people give nothing.

![EXP floating over a warthog](images/levels-hit.jpg)
*A hit on a warthog: its EXP floats up.*

Tougher monsters that bite harder are worth more, measured against Bruce the shark, who is worth **180 EXP** (EXP
gained was doubled on 2026-10-02, tripled on 2026-10-04 and raised by half on 2026-10-09: nine times what the levels
are measured in):

| Monster | EXP | Monster | EXP |
|---|---|---|---|
| Bruce (shark) | 180 | Bear | 120 |
| Warthog | 114 | Polar bear | 132 |
| Screecher | 105 | Hyena | 60 |
| Puffer fish | 84 | Rat | 60 |
| Mama bear (Balboa) | 600 | Hyena boss | 360 |

An island's own Hard or Boss animals are worth more than Raft's plain ones.

| From level | EXP to the next | About |
|---|---|---|
| 1 → 2 | 100 | 1 shark |
| 2 → 3 | 200 | 2 sharks |
| 3 → 4 | 400 | 3 sharks |
| 4 → 5 | 600 | 4 sharks |
| then | 200 more each level | 1 or 2 more sharks each level |

#### Levelling up and spending points

A box shows when you reach a new level. **Click it** (in a menu, where the mouse is free) or press **K** to spend
your points. The **EXP bar** is one more of Raft's own stat bars, above thirst, hunger and health: a gold star on
its badge, a gold fill, the level on the left and the EXP to the next level on the right. It glows for a moment when you
earn EXP. A **+** after the level means points are waiting. While Raft's inventory (**Tab**) is open, a **Stats**
button sits next to the bar.

![The level up box](images/levels-levelup.jpg)
*Level 2: two stat points to spend. The EXP bar is at the bottom left.*

![The level bar and the Stats button](images/levels-bar.jpg)
*The EXP bar with its star, styled like Raft's thirst, hunger and health bars, and the Stats button while the inventory is open.*

Every level gives **2 stat points**. Each point makes a stat **1% better**, and a stat takes at most 15 points (+15%):

| Stat | A point makes it |
|---|---|
| Walk speed, Run speed, Swim speed | 1% faster |
| Jump height | Jump 1% higher |
| Damage | 1% more damage to monsters, with every weapon |
| Health | 1% more maximum health |
| Hunger, Thirst | Drain 1% slower |
| Oxygen | Breath lasts 1% longer under water |

![The stats page](images/levels-page.jpg)
*The stats page (K): the level, the EXP, the monsters you defeated, and the nine stats. **+** puts a point in, **−**
takes it back while the page is open.*

All 135 points are there at level 69 (a stat could take 10 points before 2026-10-06; points already spent stay). After that the levels go on, without points.

**Playing together:** each player has their own level, and the host keeps it with the world. A player who joins again
gets theirs back. The host works out everyone's EXP, so a monster is worth the same to everybody. Other players see a
small gold **Lv 5** under your name.

![A level under a player's name](images/level-tag.jpg)
*A friend at level 6, as the other players see them.*

---

## 10. Settings files

In `<Raft>\Mods\DynamicIslands\`. Text files: open them with Notepad. They explain themselves, and changes are picked
up while the game runs - but edit them with Raft closed where you can: the mod rewrites some files itself, and a change
made while Raft runs can be lost or mixed in half-way. Never edit the files in `worlds\` (the mod rewrites them at every
save, and Tidy up and host swaps read their first lines). You don't need to edit them by hand: **Defaults...** in the World settings window of Raft's
New Game box ([9.1](#91-the-world-settings-window)) sets the `spawnpool.txt` values in a window. It is only there:
**Esc > Custom Islands** in a world has the world's own settings, not these. In a world, the world's regrow days are the
F10 command `RegrowDays <days>`; the unload distance and the Receiver settings are the host's (players who joined see
the host's). A number with a decimal comma (`0,5`) is read as `0.5` in `spawnpool.txt`, `world_rules.txt` and
`randomizer.txt`, so a PC set to another language is fine.

**The Defaults... window** (`CUSTOM ISLANDS - DEFAULTS`) holds this PC's `spawnpool.txt` values and the keys of the
journal and the stats page. Each field is saved when you leave it and kept inside what it may be; the line at the
bottom of the window says what was saved, and when a number was kept within its range. In a world you host, the players
get the settings they share at once.

| Group | What is in it |
|---|---|
| **Random islands while sailing** | Random islands on or off (0 = off), the quiet minutes in a new world, the spacing, how far ahead an island appears (**Appear from** and **to**), and when a needed island returns |
| **Keys (this PC)** | The **Journal** key (J) and the **Stats** key (K): click one, then press the new key (Esc keeps the old one). A key Raft uses, a number key, Esc, Enter, Tab, Space or a mouse button is refused, and the line at the bottom says why |
| **Shared with every player** | **Receiver dots** on or off, the **Receiver range**, **Unload beyond** and **Regrow days (new worlds)** |
| **Generated islands** | The weight of new generated islands, the chance one flies, **Things to gather**, **Finds in the shallows** and how far down they go, the five styles (at least one stays lit) and a button for each kind of thing to gather (lit = on) |
| **Editor camera (this PC)** | The island editor's camera: **Look sensitivity**, **Move speed**, **Zoom speed**, **Pan speed**, **Drag up: look up / down** and **Wheel up: zoom in / out** - the same as the editor's **Camera...** button ([4.1](#41-the-screen)); kept in `world_rules.txt` |
| **Mod's own** | Click it twice: every setting in the window, the keys J and K and the editor camera too, goes back to what a new install has. The island list in `spawnpool.txt` stays |
| **Close** | Closes the window (Esc does too) |

**`spawnpool.txt`** (islands that appear on their own; the host's counts):

| Setting | Default | What it does |
|---|---|---|
| `chancePerKm` | 0.25 | Random islands on (above 0) or off (0). How often they come is the world's own span: one after every 3-6 of Raft's islands met by default ([9.5](#95-islands-while-sailing)) |
| `quietMinutes` | 10 | No random island in a new world's first minutes of play (game time, kept with the world; 0-240) |
| `minSpacing` | 800 | Metres kept between custom islands |
| `spawnDistanceMin`, `spawnDistanceMax` | 250, 350 | How far ahead of the raft an island appears (at least 20 m). The furthest is kept 150 m inside `unloadDistance`, because an island put beyond that would unload before anyone saw it: with 800 it can be at most 650, and a bigger number is cut to that (the log says so). The nearest is never further than the furthest |
| `unloadDistance` | 800 | Islands further away are unloaded (and come back when you return); at least 300 |
| `returnMinutes` | 12 | An island the players still need (its quest begun and not done, or one a plan waits for) that the raft left behind comes back ahead of the raft after this many minutes (0 = never; [3](#islands-appear-while-you-sail)) |
| `regrowDays` | 3 | In-game days until harvested things grow back (0 = never), for new worlds: a world keeps its own (the F10 command `RegrowDays`) |
| `showOnReceiver` | 1 | Custom islands as green dots on Raft's Receiver (0 = no) |
| `receiverDistance` | 2000 | ... only those within this many metres (0 = all); an island the players still need (its quest begun, one the plan waits for) shows however far |
| `defaultPlan` | Random islands | The plan new worlds get when none is chosen |
| `generated` | 1 | How often a brand-new generated island is picked (0 = never) |
| `generatedStyles` | all five | The styles generated islands can have |
| `generatedFlyingChance` | 0.1 | The chance a generated island flies |
| `generatedGather`, `generatedShallows` | 0, 0 | How much to gather on generated islands, on land and in the shallows (0-1, 0 = none) |
| `generatedShallowsDepth` | 6 | How deep the finds in the shallows go (2-20 m) |
| `generatedGatherOff` | (empty) | Kinds of things to gather switched off, separated by commas (empty = all on). On the land: `palm`, `datepalm`, `mango`, `pineapple`, `watermelon`, `banana`, `pine`, `birch`, `berry`, `flower`, `dirt`, `hive`. In the shallows: `sand`, `clay`, `stone`, `iron`, `copper`, `scrap`, `clam`, `seaweed`. Any other word is dropped. The Defaults... window has a button for each |
| `<island name> <weight>` | `* 1` | Which of your islands take part (`*` = every island you saved yourself that has no line of its own; weight 0 leaves one out) |
| `type:<map type> <weight>` | sandbar 0.4, wreck 0.4, atoll 0.3, sunken 0.2 | Map types that take part: a new island of that kind each time |

Lines that start with `#`, and empty lines, are notes. A setting is `key = value`; a number outside its range is kept
within it, and a key the mod doesn't know, or a word where a number belongs, is ignored (the log, F10, names the line).
An island line is the name and then the weight, which is the last word (`Old Camp 2`); a name alone counts as weight 1,
and names may have spaces. A higher weight makes an island more likely. `*` covers only islands you saved yourself: not
islands downloaded from a host (`<name>_<code>`), generated ones (`gen-...`) or the randomizer's extras (`rnd-...`),
which take part only with a line of their own. The islands a world plan brings never take part, listed or not: they
come in their own plan. A `type:` line with a map type that doesn't exist is left out.

**`world_rules.txt`** is your last World settings choices, the start for the next new world. The mod writes it (the
file starts with three `#` notes that say what is in it) and you can edit it: one `key=value` per line. A key that isn't
there means plain Raft.

| Key | Values | What it does | Written by |
|---|---|---|---|
| `monsters` | `timid`, `normal`, `fierce`, `savage`, `nightmare`; `easy`, `moderate`, `hard`, `impossible` or 0-4 are read too | The monster difficulty the next new world starts with | **Monster difficulty** in World settings, when you press **Create**; `Monsters <level>` in the main menu, at once |
| `rarelooks` | 0-2 (Normal, More, Lots) | How often animals have a rare look in the next new world | **Rare animal looks** in World settings (Create); `AnimalLooks <level>` in the main menu |
| `buildcost` | 0-100 | How many % more the build menu costs in the next new world | **Build cost** in World settings (Create); `BuildCost <0-100>` in the main menu |
| `options` | The extra options that are on, separated by commas: `ghostrafts`, `privatestorage`, `longvoyage`, `ironraft`, `sharedxp`, `nightdanger`, `dailyquest`, `rogueshark`, `barrels`, `stormdays`, `traderraft`, `upgrades`, `stormseas`, `ufo`, `glowsea`, `lightning`, `ghostship`, `whalebreach`, `fogbanks`, `kraken` | The extra options the next new world starts with | The option buttons in World settings (Create); `WorldOptions +option -option` in the main menu |
| `levels` | `on`, `off` | The level up system in the next new world | **Level up system** in World settings (Create); `Levels on` / `Levels off` in the main menu |
| `headstart` | 0-3 | The head start raft's level in the next new world (0 = none) | **Head start raft** in World settings (Create); `HeadStart <0-3>` in the main menu |
| `islandsoff` | Island names separated by a vertical bar; also `type:<map type>` and `<generated>` | The islands left out of the next new world's islands while sailing | **CHOOSE ISLANDS...** in World settings (Create); `WorldIslands -<island>`, `+<island>`, `all` in the main menu |
| `raftgap` | `min-max`, for example `3-6` (min 2-20, max 4-50) | One random island after every min to max of Raft's own islands | **One after every ... to ...** under CHOOSE ISLANDS..., at once; `WorldIslandsGap <min>-<max>` in the main menu |
| `journalkey`, `statskey` | A Unity key name, for example `J`, `K` or `F6` | The keys of the journal (J) and the stats page (K) in a world | **Defaults...** > **Keys (this PC)**, at once. A key that can't be used falls back to J or K, and the log says so |
| `cameralook`, `cameraspeed`, `camerazoom`, `camerapan` | A multiplier, 1 = the mod's own: look 0.2-4, speed 0.1-10, zoom and pan 0.25-4 | The island editor camera's look sensitivity, flying speed, wheel zoom step and pan speed (this PC) | The editor's **Camera...** window or **Defaults... > Editor camera (this PC)**, a moment after a change |
| `cameradragup`, `camerawheel` | `up` / `down`; `normal` / `reversed` | Which way dragging up turns the editor's view (up, the default; only a saved `down` turns it round) and the wheel's direction | The same |

**`randomizer.txt`** is the world randomizer's last choice, written when you press **Create**: a `#` note and one line,
for example `level=normal;seed=0;off=colours,loot`. `level` is `off`, `light`, `normal` or `wild`; `off=` lists the parts
switched off (`colours`, `animals`, `alphas`, `loot`, `finds`, `oddities`, `bosses`, `large`; no `off=` = all on). The
seed is always 0 there: every new world gets its own. The **World randomizer** group in World settings is the only
control for it.

Other files:

| File | What it holds |
|---|---|
| `*.island` | Your islands (and the ones you installed from packs) |
| `<name>_<hash>.island` | Islands downloaded from a host, or a version a saved world keeps after an update |
| `autosave\` | Unsaved editor work (4.7): offered when the editor opens, removed when the island is saved |
| `exports\` | Packs you exported (`<name>.zip`), and `exports.json` (what you filled in, so the next export is the next version) |
| `import\` | Put packs you got here to install them (Import...) |
| `library\installed.json` | What each installed pack wrote (so it can be updated and removed) |
| `library.txt` | The island library: `online = on/off`, its `address` (where its list is: a Google Drive folder link or a web address), and an optional `key =` (a Google API key: the Drive folder is then read through the Drive API instead of its public listing) |
| `library\cache\` | The library's pictures, kept so they needn't be downloaded again |
| `thumbcache\` | Small maps of your islands for **CHOOSE ISLANDS...** ([9.5](#95-islands-while-sailing)), made again when an island is saved (safe to delete) |
| `plans\*.plan` | World plans |
| `maptypes\*.maptype` | Your own map types ([4.6](#46-ready-made-islands-map-types)) |
| `world_rules.txt`, `randomizer.txt` | Your last World settings choices (monsters, build cost, extra options, level up system, islands left out and how often they come; the randomizer, the head start raft's level), the start for the next new world; also your J and K keys, the editor camera's settings (the keys are above); `newworld_plan.txt`: the plan the last new world was made with |
| `worlds\<world>.txt` | Each world's custom islands and their state (what was used, quests, journal, levels, settings); the world's own folder carries a copy, `CustomIslands.txt` |
| `worlds\<world id>.kept-<n>.txt` | Your own, newer copy of a world, set aside when you joined a host whose copy was older (n = its save count): the host's is played, yours stays here |
| `worlds\<world id>-<player id>.rewards` | Which quest rewards this player has had, or is still owed, in each world (each player gets their own share of a reward of Raft's items) |
| `groups\`, `stamps\` | Your saved object groups and terrain stamps |
| `generator_presets\` | Your generator presets (**Save these settings...**) |
| `notice.txt` | That you folded the alpha box (**Got it**) or closed it with its **X**, for this version of the mod, and where you dragged it |
| `editor_start.txt` | The last island and plan you worked on, for **Continue** in the EDITOR start window |
| `library\updated.txt` | The islands a pack update replaced, so a world that has one says so once when it loads |
| `rml_cache_noted.txt` | That the "first start compiles" note has been shown for this build of the mod |
| `editor_light.txt` | The editor's time of day (the Light button) |
| `editor_skysea.txt` | Whether the editor shows Raft's sky and sea |
| `rulescache.txt` | The islands' own rules, kept so lists open fast (made again if you delete it) |
| `summarycache.txt` | Each saved island's title, description and quests, so the island lists open at once (made again if you delete it) |
| `testkit.txt` | Whether the editor's **Test** gives the starter kit (**Kit: On/Off**, [4.9](#49-trying-the-island-in-a-world-test)) |
| `errors.txt` | The errors the mod noticed, to attach to a report ([14](#14-reporting-a-problem)) |
| `notbeforecache.txt` | The islands' "not before" distances, kept so random islands are picked fast (made again if you delete it) |
| `deleted\` | Islands you deleted in the editor (move one back to get it back; a saved world that needs one gets it back by itself, [4.7](#47-saving-and-sharing)); in its own folders: unsaved editor work thrown away (`autosave\`), generator presets (`presets\`), object groups and stamps (`groups\`, `stamps\`), world plans (`plans\`), the old copies **Give my worlds this version** set aside (`kept versions\`) and half-written files (`unfinished\`) |
| `worlds\removed\` | The files of worlds deleted in Raft, moved here by **Tidy up** ([4.7](#47-saving-and-sharing)) |
| `catalog_index.txt`, `placeables*.txt` | Where the editor finds Raft's objects. After a Raft update the main menu makes it again in the background (under a minute; the log says what it did) |
| `Custom-Islands-Guide.pdf` | This guide, written out of the mod when the alpha box's **Guide (PDF)** opens it |
| `raft_*.txt`, `island_thumbs\`, `island_heights\` | Measurements of Raft's islands the generator uses (they come with the mod; a copy here is read first) |

**Back up worlds you care about** (copy the world's folder, see [Hosting on another day](#hosting-on-another-day) for where
it is) before updating the mod or trying a new plan on them. Raft itself keeps only its last saves.

## 11. Console commands

Press **F10** for RML's console.

| Command | Where | What it does |
|---|---|---|
| `SpawnIsland <name> [distance] [height]` | World, host | An island ahead of the raft (default 250 m, 20-390 m), at its own height unless you give one (-100 to 250 m above the sea). It says **Careful** when the island would overlap one of Raft's own islands (players can fall through the ground there), or reach over the raft or within 10 m of it (it suggests a larger distance); the island is spawned anyway |
| `RemoveIsland <name>` / `RemoveIsland all` | World, host | Removes custom islands |
| `ListIslands` / `ListSpawned` | Anywhere / world | Your saved islands / the world's custom islands with their distance |
| `SpawnPool` | Anywhere | Which islands appear on their own, and how often. A player who joined is told that the host's `spawnpool.txt` decides, and which islands the host left out (until the host's copy of the world has come: that it comes with the host's next save) |
| `CustomIslandsAuto on` / `off` | World, host | Automatic islands on or off for this world |
| `WorldPlan` / `WorldPlan <name>` | World | The world's plan and its rules / give it another plan (host). A player who joined sees the host's plan, which of its rules are done and the story chain; trying to change it says only the host can |
| `StoryChain` | World | The world's story chain: Raft's story islands and the plan's own in order, what is unlocked and done, and the plan islands' Receiver frequencies |
| `Randomizer` / `Randomizer <off/light/normal/wild> [-part] [+part]` | World | What the randomizer does here / change it (host) |
| `WorldOptions` / `WorldOptions +option -option` | World or main menu | The world's World settings / change them (host; at the main menu: the next new world). Options: `ghostrafts`, `privatestorage`, `longvoyage`, `ironraft`, `sharedxp`, `nightdanger`, `dailyquest`, `rogueshark`, `barrels`, `stormdays`, `traderraft`, `upgrades`, `stormseas`, `ufo`, `glowsea`, `lightning`, `ghostship`, `whalebreach`, `fogbanks`, `kraken` ([9.4](#94-extra-options)) |
| `HeadStart` / `HeadStart 0-3` | World or main menu | The head start raft: this world's level and the next new world's / at the main menu choose the level for the next new world (0 = off) |
| `Levels` / `Levels on` / `Levels off` | World or main menu | The level up system in this world / switch it (host; off keeps everyone's levels; at the main menu: for the next new world) |
| `Resync` | World, joined player | Ask the host for its custom islands again (the list, and any island file that hasn't come) |
| `WorldIslands` / `WorldIslands -<island>` / `+<island>` / `all` | World or main menu | Which islands turn up by chance while sailing in this world / leave one out, let it take part again, all of them (host; also `type:<map type>`; at the main menu: the next new world). A player who joined sees the host's list, and changing it says only the host chooses |
| `WorldIslandsGap` / `WorldIslandsGap <min>-<max>` | World or main menu | How often random islands come: one after every min-max of Raft's own islands met (default 3-6; min 2-20, max 4-50) / change it (host; at the main menu: the next new world) |
| `Monsters` / `Monsters <level>` | World or main menu | The monster difficulty / change it (host; at the main menu: the next new world). The level is `timid`, `normal`, `fierce`, `savage` or `nightmare`; `easy`, `moderate`, `hard` and `impossible` (the words the levels were first called: they mean timid, fierce, savage and nightmare) and the numbers 0-4 (timid to nightmare) work too; any other word is refused and the five are listed |
| `BuildCost` / `BuildCost <0-100>` | World or main menu | The build cost / change it (host; at the main menu: the next new world). Any whole number from 0 to 100, with or without `%` (`BuildCost 25%`); the slider in World settings moves in steps of 5 |
| `AnimalLooks` / `AnimalLooks <level>` | World or main menu | Rare animal looks and the looks tamed per animal / change it (host; at the main menu: the next new world). The level is `Normal`, `More`, `Lots` or 0-2 |
| `RegrowDays` / `RegrowDays <days>` | World | This world's days until things come back / change it (host; 0 = never; an island's own rule wins) |
| `DailyQuest` / `DailyQuest new [gather/fish/monsters n [item]]` | World | Today's daily quest (option Daily quest): the task, how far you are, the reward / a new quest now (host; with a kind and a number: that quest, e.g. `DailyQuest new gather 20 Plank`) |
| `UpgradeTable` | World | The upgrade research table (option Extra upgrades): what this world has researched and learned there |
| `RogueShark` / `RogueShark spawn` / `RogueShark giant` | World | The rogue shark (option Rogue shark): alive or not, giant or not, and the day before which none comes / one now, or a giant one now (host) |
| `Barrels` / `Barrels roll <index> [<to>]` | World | The barrels in the sea now and which are silver or golden (option Silver & golden barrels) / what the barrel with that index is in this world (with `<to>`: the silver and golden ones in the range) |
| `StormSeas` / `StormSeas break` | World | The weather now, the raft's rocking, the storms counted, the floors broken and the edge foundations (option Storm seas) / (host, for tests) break an edge floor now |
| `StormDays` / `StormDays roll <from> <to>` | World | Whether today is a storm day, the next one, the weather and the shark's factor (option Storm days) / the storm days between two days |
| `GlowSea` / `GlowSea roll <from> <to>` | World | Whether tonight glows, the next glowing night, the glow and the fishing wait's factor (option Bioluminescent sea) / the glowing nights between two days |
| `GhostShip` / `GhostShip roll <from> <to>` / `GhostShip bring` / `GhostShip sink` | World | Whether a ghost ship is out and the next night with one (option Ghost ship) / the nights with one / one beside the raft now / sink it now (host) |
| `Kraken` / `Kraken bring` / `Kraken away` / `Kraken tear` | World | Whether a kraken arm holds the raft, its hits and the next days with one (option Kraken arm) / an arm grabs an edge floor now / it goes now / it tears now (host) |
| `FogBanks` / `FogBanks roll <from> <to>` | World | Whether fog is in now, how far you see and the next fog bank (option Fog banks) / the fog banks between two days, with their hours |
| `Traders` / `Traders offers <seed>` / `Traders bring` | World | The trader rafts loaded now and their stalls' stock (option Trader raft) / what a trader raft with that seed sells / a trader raft ahead now (host) |
| `ExportMapType <type> [file name]` | Anywhere | Writes a map type as a file in `maptypes\` to start your own from (default name `my-<type>`) |
| `ReloadMapTypes` | Anywhere | Reads the `maptypes\` files again and says which were left out and why |
| `LoadEditor` | Main menu | Opens the editor |

**In the editor** (each does what a button does; handy for a repeat or an exact value):

| Command | What it does |
|---|---|
| `SaveIsland [name]` / `LoadIsland <name>` | Saves the island (no name: under its own name) / opens a saved island - the Save and Open buttons ([4.7](#47-saving-and-sharing)) |
| `DeleteGroup <name>` | Deletes a saved object group (no name: lists them) - or use Manage... beside Save as group |
| `GenerateIsland [seed] [size m] [height m] [roughness 0-1] [peaks] [objects 0-1] [style]` | A random island in place of the one being edited (no seed: a random one; Ctrl+Z undoes it) - the Generate window in short ([4.5](#45-the-island-generator)). Example: `GenerateIsland 1234 120 30 0.5 2 0.6 Snowy` |
| `ReRollMapType <type> [seed]` | Puts a map type's content (chests, notes, creatures, zones, quest - not its land) on the island being edited; one Ctrl+Z takes it off |
| `SetStyle <Tropical/Snowy/Desert/Forest/Volcanic>` | The island's style (its ground textures) |
| `SetElevation <m>` | How high above the sea the island floats in a world, -100 to 250 m (60 = a flying island, -25 = under water, 0 = normal) - the Island tab's Height presets in short ([4.4](#44-the-island-tab)) |
| `SetToRaise`, `SetToLower`, `SetToFlatten`, `SetToSmooth`, `PaintTexture <sand/grass/rock/seabed>`, `SetToAutoPaint` | The terrain brush's tool, as the Terrain tab's buttons ([4.2](#42-shaping-the-land-terrain-tab)) |
| `ChangeWidth <m>` / `ChangeStrength <m/s>` | The brush's width (4-160 m) / strength (0.5-20 m a second) |

## 12. Limitations: what can't be changed

These come from Raft itself, from the game engine (Unity), or from a choice made on purpose. They are not bugs and are not
planned to change.

**Raft and its mod loader**
- **Every player needs the mod** (and the Raft Mod Loader): Raft can't show custom islands to a player without it.
- **Up to eight players**, Raft's own maximum.
- **Raft's own Join World list is empty** in this Raft version: join friends through Steam ("Join Game").
- **Levels, the place a player stood and private storages belong to a Steam account**: Raft knows players by their
  Steam id. A player who comes back with another Steam account starts over there.
- **Raft has no pets**: "catchable" animals are Raft's domestic ones (chicken, goat, llama), caught with the net launcher.
- **Story items live with the crew, not in Raft's inventory** (in the journal, or for the main story in Raft's
  notebook's Found items, which the mod builds again each time - nothing of it is in Raft's save). Raft's items are a
  fixed list; a new item would break the save for anyone who opens the world without the mod.
- **Objects from Raft's other islands and Raft's buildable items are decoration** in custom islands: a chest from
  Tangaroa doesn't store anything, a character doesn't move. Their game code belongs to Raft's own islands. The mod's own
  chests, notes, zones and creature spots, and the harvestable trees, rocks, ores and plants, have their gameplay.
- **The screecher's stone look can't be tinted** (it is not a normal texture).
- **Atmosphere zones** change Unity's fog and light plus a faint tint: how strong they look depends on Raft's own sky
  at that moment.

**The land**
- **One sea level.** Raft has one ocean: lakes and lagoons go down to sea level, and there is no water above it.
- **No caves or overhangs from the terrain.** An island's ground is one height map (one height at each point): build
  caves and arches from objects (rocks, Raft's big island pieces) instead.
- **Flying islands:** reaching one is up to the players (stairs or pillars from the raft) - that's the idea.
- **At most 12 000 objects per island** from the generator: more makes Raft stutter when the island loads.

**Versions**
- **An island saved with a newer version of the mod can't be opened by an older one** (the file says so). Update the mod.
- **Islands built with Unity (`.assets`, version 2)** are no longer read: rebuild them in the editor.

**Your PC and Raft's menus**
- **Keep Raft out of OneDrive and Program Files.** The mod writes its files next to Raft (`<Raft>\Mods\DynamicIslands`).
  In `C:\Program Files` Windows may not allow that, a synced folder (OneDrive, Dropbox, Google Drive, iCloud) can lock a
  file while it syncs, and an antivirus with "controlled folder access" can block it. Then a save fails, and the mod
  says so on the screen (the world's islands try again at the next save). To fix it: check where Raft is (Steam >
  right-click Raft > **Manage > Browse local files**); if it is under `Program Files` or a synced folder, add a drive or
  folder in Steam > **Settings > Storage** (for example `D:\SteamLibrary`) and use Raft > **Properties > Installed
  Files > Move install folder**; with **Controlled folder access** on, allow `Raft.exe` under **Allow an app through
  controlled folder access**. The start check names this problem on the main menu.
- **Raft's Load Game box** (the last 8 saves) takes the custom islands back to each save only for saves made with this
  version of the mod - not the very first save of a new world, and not saves from before this feature.
- **The mod loader's Unload / Load** (F9) can't take Custom Islands out cleanly: Unload removes the mod's patches and
  hooks, but its windows and the islands already in the world stay until Raft restarts. Restart Raft instead.

**Sharing**
- **Text inside islands** (notes, quests) is shown as its builder wrote it: a pack from someone you don't know can hold
  words you'd rather not see. The island library's entries are looked at before they go in.

## 13. Questions and problems

**No islands appear while I sail.** Run `SpawnPool`: are automatic islands on, and is the pool empty? The plan may be
"No custom islands" (`WorldPlan`). Islands appear only where there is room: near Raft's own islands they wait until
the sea is clear. `SpawnIsland <name>` places one right away.

**K does nothing, and there is no level bar.** The level up system is off in this world: switch it on in World settings
for a new world, or with `Levels on` (host, F10) in this one (see [section 9.6](#96-the-level-up-system)); islands
don't switch it on.

**A friend's storage won't open.** The world has **Private storages** on ([9.4](#94-extra-options)): a storage opens
only for the player who built it. When its builder isn't in the game, the host can open it; or the host switches the
option off with `WorldOptions -privatestorage`.

**The Receiver led me to the wrong story island / the blueprint isn't where the wiki says.** The world has **Story
islands in a new order** or **Scrambled blueprints** on ([9.4](#94-extra-options)); `WorldOptions` shows which.

**A note's frequency leads nowhere, or a story island never comes.** The world's plan may leave that island out of
Raft's story, or put one of its own islands in its place ([6.5](#65-your-islands-in-rafts-story-the-receiver)).
`StoryChain` (F10) shows the chain and the plan islands' frequencies; a plan island on the Receiver comes only after
the island before it is done.

**One of my islands never turns up.** It may be unticked for this world ([9.5](#95-islands-while-sailing)): run
`WorldIslands`, and `WorldIslands +<island>` lets it take part again. Its weight in `spawnpool.txt` may also be 0.

**A story item isn't in my inventory, or I can't find a custom island's note.** A **main story** island's notes and
story items are in Raft's notebook (**T**: its tab, and Found items); a **side quest** island's are in the journal
(**J**). Both are the whole crew's ([3](#the-journal-j)).

**A note I read isn't in the journal.** Notes with no text add no page, and each note adds one page once per world
(someone else may have read it first: look for its title).

**Raft takes a minute to start with the mod.** RML compiles the mod's code when Raft starts. Only the first start after
installing or updating Custom Islands does that (the main menu then says "Custom Islands is ready..."); later starts load
RML's kept copy (`%APPDATA%\RaftModLoader\cache\mods`, which the mod marks read-only so RML doesn't throw it away) and are
quicker. Nothing of the mod runs while RML compiles, so there is no sign before that; RML's own mod list (its menu)
shows "Compiling..." meanwhile.

**"Islands missing" when I load a world.** The world uses island files this PC doesn't have (deleted, renamed, or
from the island library and removed since). The box names them, and the library entry an island came with. **Play
without them**: the rest of the world plays; those islands are left out (no Receiver dot) but stay in the world, so they
come back once their file is here again - in multiplayer a player who joins with the file brings it. **Main menu**:
leaves without saving, so you can download them again in the island library (or put the files back in
`Mods\DynamicIslands`) and load the world again. Only the host (or a single player) sees the box; players who join get
missing files from the host as before.

**Load World is greyed out.** Raft is offline from Steam. Check that Steam is online and restart Raft.

**"The host has Custom Islands 3.x - you have 3.y".** You and the host have different versions of the mod: islands,
quests and settings may not match between you. The message says which version to install; both of you update to the same
version (raftmodding.com or GitHub) before playing on ([Everyone runs the same version of the mod](#everyone-runs-the-same-version-of-the-mod)).

**"This older save has no record of its custom islands".** You loaded one of Raft's older saves (Load Game > a backup)
that was made before the mod kept a copy with each save, or the very first save of a new world. Its custom islands'
chests, quests and story are as in the world's newest save.

**"This world was saved by a newer Custom Islands".** Someone with a newer version of the mod hosted this world. What
this version doesn't know is kept as it is; update the mod before playing on.

**My island can't be saved under that name.** Windows keeps some names for itself (CON, PRN, AUX, NUL, COM1-9, LPT1-9 -
also with anything after a dot) and doesn't allow a name beginning or ending with a space, or ending with a dot, or `\ / : * ? " < > |`. Names can
be up to 60 characters. Names can't start with `#` or `@` or hold `=`, `,` or `;` (the mod's own lists use them): an
island saved under such a name by an older version needs **Save as** a new name. The message says what is wrong;
choose another name.

**I loaded an older save of my world, and a chest I emptied is full again.** That's on purpose: Raft's Load Game box
keeps the last 8 saves of a world, and the mod's islands, chests, quests and journal go back with the save you pick, so
the world fits together (the items you took went back with Raft's save too). Leaving that world saves it as it is now,
so it becomes the newest save; to go back to where you were, load the **first older save** in the Load Game box -
the save you came from (the mod's islands and quests come back with it too).

**Raft closed while I was building an island.** Open the editor again: it offers the unsaved work it kept
([4.7](#47-saving-and-sharing)). Open it and **Save**.

**Raft crashed in a world: what is lost?** What came after the last save, as in Raft. The host's world is saved when
Raft closes (also with Alt+F4 or the window's X), about 3 seconds after each quest step, quest finish and story step
(at most once a minute), and with Raft's own saves every few minutes; players who joined rely on the host's save. A
crash, Task Manager or a power cut closes Raft without a save: the islands' chests, quests and story come back as at
Raft's last save. **Esc > Main menu** is the safest way to stop; after changing a setting during play, let Raft save
before you stop.

**My island is far away / I can't find it.** Build Raft's Receiver: custom islands are green dots with their distance.

**I can't get onto a flying island (or a cliff island).** Build stairs or pillars up from the raft. The generator's
reach line tells a builder beforehand.

**A friend can't see my island.** Every player needs the mod, and the host's islands are the ones that count. Island
files are sent to players who join; they appear as `<name>_<hash>.island` in their folder. If the host was busy or a
message got lost, the mod keeps asking (it says "Waiting for the island..." or, when the list itself hasn't come, "The
host hasn't sent its custom islands yet"; [8](#8-playing-together)); press F10 and type `Resync` to ask again at once.

**I joined and couldn't move.** When you join where you stood last time, the mod holds you there until the island under
you has come ("Waiting for the island you stood on to load..."): about half a minute, longer while that island is
still loading on a slow start (up to two minutes) or downloading (up to five), and it lets go at once if you are
hurt. A player who comes back onto the raft is never held: the raft drifts, so Raft itself puts them on it. Builds from
before 2026-10-11 could hold a player over the sea, where the shark came: install the newest release on every machine.

**"Some parts of Custom Islands are off".** A Raft update changed something the mod relies on: the box names the parts
that won't work until the mod is updated; everything else works and your islands and worlds aren't changed. Please
report it ([section 14](#14-reporting-a-problem)).

**Red boxes on my island in the editor.** Objects this Raft version doesn't have (after a Raft update): they are
kept as they were and saved back unchanged ([4.7](#47-saving-and-sharing)).

**Where are my islands?** In `<Raft>\Mods\DynamicIslands\` as `<name>.island`. To give one to a friend, **Export...** it
([4.7](#47-saving-and-sharing)): the pack has the islands it brings too.

**The story stopped: the next island never came.** The host is told on the screen when a plan (or an island's rule)
needs an island that isn't on the host's PC - usually after someone else started hosting the world. Install the pack
or library entry the message names, or ask the player who made the world to export the plan. For a world saved before
worlds kept their own plan, the plan file itself must be on the host's PC (see the table in
[section 8](#who-needs-what-every-case)).

**An installed island has an odd name like "Harbor (My Voyage)".** You had a different island called "Harbor", so
the pack's was installed under that name instead of replacing yours.

**A pack won't install.** The Import window says why: it may be made for a newer version of the mod, broken, or not an
island pack at all.

**How do I change a world's settings after it has started?** In the world: **Esc → Custom Islands**. The host changes
them there for every player ([9.1](#91-the-world-settings-window)).

**Test in the editor didn't get to the world.** The mod says why on the screen: the island needs a name (Save as
first), or Raft's Load button is off because Steam is offline. The test world **Custom Islands test** can also be
loaded by hand from Load Game; if it is broken, delete it there and Test makes a new one.

**"... is in use by another program".** Another program has the file open (a text editor, 7-Zip, a cloud folder
syncing it). Close it there and save (or delete, remove) again - nothing was changed meanwhile.

**Mods\DynamicIslands is full of old files.** The island library's Installed tab: **Tidy up** (see 4.7).

**The editor takes a moment to open.** The first time after starting Raft it loads about 700 objects from Raft's
islands; the loading box shows how far it is. After a Raft update it also scans Raft's other islands once, in the
background (a minute or two; the object browser's status line says so).

**Something went wrong.** Press F10: the mod's messages start with `[CUSTOM ISLANDS]`. Raft's log is
`%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\Player.log`. To tell us about it, see
[section 14](#14-reporting-a-problem).

More: the [README](../README.md) (every feature, file and command, the limitations and what has been tested).

## 14. Reporting a problem

This is an alpha, and your reports are how it gets better. A good report lets someone who wasn't there see what
happened, and make it happen again on their own PC. Please include:

1. **What happened, in detail.** What you did, step by step, from starting Raft or loading the world up to the problem:
   which island, which window or button, what you pressed or where you walked. Does it happen every time, now and then,
   or only once? Did it start after something (a new version of the mod, joining a friend, loading an older world)?
2. **What you expected** to happen if it had worked.
3. **Screenshots**, or a short video, if the problem can be seen: Steam's screenshot key (**F12**) or Windows'
   **Win+Shift+S**. If the console (**F10**) shows red messages, a screenshot of it helps too.
4. **The logs**, taken right after the problem and **before you restart Raft** (a new start replaces the log):
   - `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\Player.log`: Raft's log, with the mod's messages
     (`[CUSTOM ISLANDS]`). Paste the folder part of the path into the address bar of Windows Explorer to open it.
   - `Player-prev.log` in the same folder, if Raft crashed or you have started it again since: the previous start's log.
   - If Raft doesn't start with the mod at all: the mod loader's own log, `%APPDATA%\RaftModLoader\logs\hloader.log`.
   - **Playing together:** the logs of every player, the host's first, and who was the host.
   - The logs may show your Windows user name (in file paths) and your Steam name and id.
5. **The settings you used:**
   - **the versions:** Custom Islands (the box at the top right of the main menu says, for example, 3.0), Raft (bottom
     left of the main menu, for example 1.1.01), the mod loader (top of the main menu, for example v2.8.10), and which
     other mods you have;
   - **the world's settings:** Raft's game mode, the Custom Islands plan, and what you chose in World settings. In the
     world, press **F10** and run `WorldOptions`, `Monsters`, `BuildCost`, `Randomizer`, `WorldPlan`, `WorldIslands`,
     `StoryChain` and `SpawnPool`, then copy what they print (it is also in `Player.log`);
   - your settings files from `<Raft>\Mods\DynamicIslands\`: `spawnpool.txt`, and `world_rules.txt` and
     `randomizer.txt` if the problem is with a new world;
   - **single player or together;** if together: were you the host or did you join, and how many players were there.
6. **The islands and world plans involved:** their names, and the files or where to download them. Islands are
   `<Raft>\Mods\DynamicIslands\<name>.island`, world plans `<Raft>\Mods\DynamicIslands\plans\<name>.plan`. If the
   problem is in one world, add that world's `CustomIslands.txt` (in its folder, below), and if you
   can, the world itself: zip its folder from `%USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft\User\`.

**When the mod notices an error itself:** a note **Custom Islands: an error happened - click for details** appears at
the bottom left. Click it to see what happened and whose code it was; **Copy** puts it on the clipboard for a GitHub
issue or a Discord post, and **Open the log folder** finds Player.log. Every such error is also kept in
`Mods\DynamicIslands\errors.txt`.

**The quick way: Report a problem.** The alpha box on the main menu ([section 1](#1-installing)) has a **Report a
problem** button. It opens a box with the list above in short, and buttons that do the work for you:
- **Copy report form** puts a form like the one below, with the mod's and Raft's versions filled in, on the clipboard (paste it with Ctrl+V);
- **Open log folder** opens the folder with `Player.log` and `Player-prev.log`;
- **Report on GitHub** opens a new issue on the mod's GitHub page with the form already in it;
- **Post on Discord** opens the Custom Islands Discord server.

![The Report a problem box](images/report-box.jpg)
*Report a problem: what to include, and the buttons for the form, the logs, GitHub and Discord.*

**Where to send it** (either is fine):

- **GitHub:** open an issue on the mod's GitHub page,
  [github.com/SwedenJohansson/DynamicIslands/issues](https://github.com/SwedenJohansson/DynamicIslands/issues)
  (**New issue**). Give it a short title that says what went wrong ("A chest on my island is empty again after
  loading"), write the report, and attach the files (zip them if there are many).
- **Discord:** post it on the mod's Discord server, [discord.gg/U7DfKY9tN](https://discord.gg/U7DfKY9tN), with the
  same details and the files attached.

A template to copy into the issue or the post:

```
What happened:
Steps to make it happen:
  1.
  2.
  3.
What I expected:
How often: every time / now and then / once
Versions: Custom Islands 3.0, Raft 1.1.01, mod loader v2.8.10, other mods:
Single player or together: (host / joined, how many players)
World settings: (what WorldOptions, Monsters, BuildCost, Randomizer, WorldPlan, WorldIslands, SpawnPool print)
Islands and world plans used: (names, files or download links)
Attached: Player.log, Player-prev.log, screenshots, ...
```

---

## 15. Learn from the library: example islands and plans

The **island library** (ISLAND LIBRARY in the main menu) has islands and world plans made with this mod's own editors -
every hill, door, quest and rule in them was made with the buttons this guide shows. Play them, then **open them in
the editor** and take them apart: they are the quickest way to learn how a quest island or a whole adventure is put
together.

A plan from the library brings its own islands, and they come only in that plan: they get weight 0 in `spawnpool.txt`
and are never in the sailing list ([single and plan islands](#single-islands-and-plan-islands)). Islands you download
on their own are single islands: they join the pool, unless you set **Also let it turn up while sailing** to no in the installer.

Every island of the library also has **things to gather**, as Raft's own islands do: on land what suits the island -
palms, mangoes, pineapples, melons and bananas on the tropical ones, pines, berries and flowers on the snowy and forest
ones, palms and melons in the desert, black and red flowers on the volcanic ones - and in the shallows round it
Raft's sea finds: sand, clay, stones, metal and copper ore, scrap, giant clams and seaweed.

### 15.1 Opening an example in the editor

1. **ISLAND LIBRARY** → **Islands** (or **World plans**) → **Download** the one you want (a plan downloads with every
   island it needs).
2. **EDITOR** → **Open** (top bar, Ctrl+O) → pick the island. Everything is there as its builder left it.
3. Click any object to see its **inspector**: a chest's loot, a note's text, a creature's toughness, a zone's message,
   and **Behaviour & events...** for doors, winches, valves and levers (what happens on use, the checks, the signals).
4. **Edit quest...** shows the quest's steps and reward; **Story items...** the keys and logs it uses.
5. A plan: **EDITOR** → **A world plan** → **Open a world plan...** (or **Plan ▼** in the window) → pick it: one rule card per island, and **Check** explains the whole story.
6. To play a plan: **NEW WORLD** → **Choose plan...**: a downloaded plan says *library plan* in the list, with a map of
   its first island and the islands it brings.

Change anything and **Save as** a new name: the original stays as downloaded.
[Chapter 16](#16-learn-from-the-islands) says how to look at any island in the editor and which island shows each feature.

> The examples were built step by step by a script that clicks the editor's own buttons (the "recipe player"), so
> they test the editors too. The scripts are in the mod's source (`content\recipes`), one line per editor action -
> readable as a building diary of each island. They are the development build's build scripts for the library
> islands, run only in a development build: players and island makers don't need them.

**As thick with things as Raft's own islands.** Every example starts from the generator with its nature at **Like
Raft** - trees, bushes, rocks, things to pick up and the corals as thick on the ground as on Raft's own islands of the
style (see [4.5](#45-the-island-generator)). Where an island's theme asks for more (Thornwood's jungle) or less (a
sandbar, a snowfield), its script says so in a line starting `# (nature:`. To see the difference, generate an island
with **Like Raft** and with **Dense**, and dive at both.
The themed islands ([15.6](#156-the-themed-islands)) also have Raft's finds in the **shallow sea round them** - sand, clay, stones, metal and
copper ore, scrap and a giant clam to gather just off the shore, as every one of Raft's islands has.

### 15.2 The small and medium islands

| Island | What it is | What it shows you how to build |
|---|---|---|
| ![](images/library/signal_rock.jpg) **Signal Rock** | A rock with a radio relay station: climb the station, dive for the lost transmitter coil, put the radio on the air and light the beacon (Raft's Radio Tower) | A building from Raft's radio tower pieces on pillars, ladders, a story item from a sunk crate, an object that is "used" with a story item (the transmitter), things shown and hidden by a behaviour (the beacon) |
| ![](images/library/stranded_gull.jpg) **The Stranded Gull** | A motor yacht aground on a sandbar, the crew's camp, the yacht's stern on the reef - with an angler fish in it (Raft's Vasagatan) | A wreck from Raft's stranded boat, a camp of Vasagatan's furniture, a creature under water guarding a safe |
| ![](images/library/rangers_rest.jpg) **Ranger's Rest** | A jungle hill: a ranger's cabin, a watchtower and the den of Old Scar, the bear that stole the supplies (Raft's Balboa) | A boss (a bear made bigger and tougher, tinted), a watchtower to climb, a cave den |
| ![](images/library/stilt_hollow.jpg) **Stilt Hollow** | Trailers on sandstone stacks joined by scaffold walkways; follow the red pipe to the valve, start the pump (Raft's Caravan Town) | Walkways between heights (scaffolding as ramps), a pump started with a story item, a white screecher |
| ![](images/library/mayors_wharf.jpg) **Mayor's Wharf** | A drowned city block: City Hall's flooded floors, the mayor's office at its wharf, a cafe on a roof (Raft's Tangaroa) | Rooms under water, a keycard in a flooded locker, a vault, a pier of Raft's planks |
| ![](images/library/crane_yard.jpg) **Crane Yard** | A construction islet under a giant crane; the spotlight's parts went down with the pier's end (Raft's Varuna Point) | Three story items to collect under water, angler fish, a spotlight that is fixed and points to the reward |
| ![](images/library/frost_hollow.jpg) **Frost Hollow** | A snowy islet with igloos, a telescope and a dark lab; three cables, a power box, a polar bear (Raft's Temperance) | A power box that takes three story items, a sliding lab door that opens on a signal |
| ![](images/library/tide_farm.jpg) **Tide Farm** | A floating farm of Raft's foundations: crop beds, a glasshouse, a water wheel jammed by a crate (Raft's Utopia) | An island of only building blocks over water, a turning wheel, animals to **catch** as a quest step |
| ![](images/library/old_mine_islet.jpg) **Old Mine Islet** | An old mine in a rock mound: timbered tunnels, rails, ore carts - dark further in, where something has dug in | A cave from Balboa's cave pieces set into the land, darkness (an atmosphere zone) and lanterns, a head lamp in a locker |
| ![](images/library/shelter_atoll.jpg) **Shelter Atoll** | A bomb shelter under an atoll's islet: seven dark rooms, rats, a dry generator, a blast gate | Rooms **under the ground** (a pit under a concrete roof), a hatch and a ladder down, a generator that lights the rooms, opens doors and runs the radio room |

### 15.3 The very large islands

| Island | What it is | What it shows you how to build |
|---|---|---|
| ![](images/library/ironreef_caverns.jpg) **Ironreef Caverns** | A jungle mountain over four caves: a mine, a vine crawl, a flooded shaft and the Bear Hall behind a rockfall | Several caves in one mountain, a plank road up a flank, a rockfall blown with charges (an object hidden by a behaviour), a boss in its hall; a Raft store on the north beach |
| ![](images/library/drowned_metropolis.jpg) **The Drowned Metropolis** | Meridian, a city the sea took: tower crowns over the water, a ferry, a penthouse, offices on the sea floor, the Lantern Lord | Buildings standing in the sea, a penthouse on legs, three keycards, an angler fish boss in deep water; Raft's vending machine |
| ![](images/library/thornwood.jpg) **Thornwood** | A jungle split by a river gorge: a rope bridge, three shrines, a stone circle where the Warthog King guards the idol | A gorge cut with the terrain brushes, a rope bridge, trails through dense jungle, a boss and its guards |
| ![](images/library/glacier_station.jpg) **Glacier Station** | A station on a glacier: glowing domes, an observatory on the peak, a crevasse with the Polar Bear Queen, a reactor | A reactor that, once running, sends a **signal** that opens its vault; a crevasse cut into the ice; a Raft store by the domes |
| ![](images/library/scrapyard_haven.jpg) **Scrapyard Haven** | A junk town in a ring of stacked caravans: a gate trailer on a winch, a hyena pit, the scrap king's fortress on stilts | A gate that a **winch** lifts (moving objects), a stilt house set level on a ridge, a pack with an alpha |

### 15.4 The world plans

In every plan, a quest has a **Blueprint** step for each of Raft's blueprints its island holds, and one of the later
islands has a **Raft store** (Raft's goods, and any story blueprint a player missed): Glacier Station (The Long Voyage),
Ironreef Caverns (The Abyss Expedition), Storm Spire and The Drowned Gate (Raft 2), Utopia Remade (Raft Remade), and
Sundown Canyons and Highmoor Lodge (Silver Screen Seas). Frostgold Creek, Saltpan Delta, Crowfield Farm, Twin Forges and
Gullsong Bazaar have one too. Every storyline also has **Raft's vending machine** (Tangaroa's plants for its tokens,
with a few tokens about): The Drowned Metropolis (The Long Voyage, The Abyss Expedition), Tangaroa Remade (Raft Remade),
Hightide Harbor (Raft 2), The Arena (Silver Screen Seas) and The Isobar Club (The Rainmakers). Gullsong Bazaar has
one by its open stall.

**The Long Voyage** - Raft's whole story, twice as long. After every story island but Utopia, a big quest island of
the library comes on its own Receiver frequency (the story island's radio picks it up); its quest done, the story goes
on as Raft's. The smaller library islands turn up as side trips while you sail (one every few km). Its main islands are about 120% the size of Raft's own big islands.

| After | The plan's island | What it is |
|---|---|---|
| Radio Tower | ![](images/library/wreckers_cove.jpg) **Wreckers' Cove** | A cove with a wreckers' hamlet, the schooner Marigold on the reef, a smugglers' den in a sea cave and the false lantern that lured her there |
| Vasagatan | **Thornwood** | ([15.3](#153-the-very-large-islands)) |
| Balboa | **Scrapyard Haven** | ([15.3](#153-the-very-large-islands)) |
| Caravan Town | **Ironreef Caverns** | ([15.3](#153-the-very-large-islands)) |
| Tangaroa | **The Drowned Metropolis** | ([15.3](#153-the-very-large-islands)) |
| Varuna Point | ![](images/library/coral_observatory.jpg) **Coral Observatory** | A research platform moored in an atoll's lagoon, coral samples in cases on the lagoon floor and a giant angler fish at the reef's edge |
| Temperance | **Glacier Station** | ([15.3](#153-the-very-large-islands)) |

The **side trips** - every one 450 m ahead of the raft, with its message on screen and its name on the Receiver:

| Sailed | Island | The message |
|---|---|---|
| 2 km | **The Stranded Gull** | A white hull glints on a sandbar ahead. |
| 4 km | **Signal Rock** (quest "Dead Air") | A mast on a rock ahead - its light is dead. |
| 7 km | **Ranger's Rest** | A jungle hill with a watch tower comes up ahead. |
| 10 km | **Stilt Hollow** | Trailers on stilts over a reef - someone lived out here. |
| 13 km | **Mayor's Wharf** | Rooftops stick out of the sea ahead: a sunken city block. |
| 16 km | **Crane Yard** | A crane rises from the sea ahead. |
| 20 km | **Frost Hollow** | A snowy islet with a hut - it's getting colder. |
| 24 km | **Tide Farm** | Water wheels turn on a floating farm ahead. |
| 28 km | **Old Mine Islet** | A headframe stands over an old shaft on the islet ahead. |
| 32 km | **Shelter Atoll** | A quiet atoll ahead - and a hatch in the middle of it. |

The **main quest islands** are each on the Receiver, 700 m (Wreckers' Cove), 900 m (Thornwood, Scrapyard Haven,
Ironreef Caverns) or 1000 m (the rest) ahead when tuned, and each is done when its quest is done. The plan also has
**Random islands while sailing: on** and **Raft's story islands: on** (all eight).

What it shows: all [three kinds of islands](#the-three-kinds-of-islands-you-meet-at-sea) in one plan - rules with a
**story place** ("after" a story island) and a **Receiver** frequency, "done when its quest is done", and side trips
brought **after a distance sailed**, ahead of the raft.

**Raft Remade** - Raft's story as you know it, but every story island is made anew. Each of Raft's eight story
islands is replaced on the Receiver by a remade version of itself: its land made by the generator **like** Raft's own
island ([4.5](#45-the-island-generator) - its shape and style, something new each time it is built), then rebuilt with
new set pieces, new missions, other monsters and a boss. Each hands out the blueprints the original island has, so the
raft still gets its steering wheel, engine, fuel, machete and everything the story needs; the last island's quest is
the ending. Its main islands are about 70% the size of Raft's own big islands.

| Instead of | The plan's island | What it is |
|---|---|---|
| Radio Tower | ![](images/library/radio_tower_remade.jpg) **Radio Tower Remade** | A three-deck tower on a steep rock, a battery bank in the old shark cage, Old Beak on the roof |
| Vasagatan | ![](images/library/vasagatan_remade.jpg) **Vasagatan Remade** | A ship broken in two on the reef - her bow on a sandbar full of rats, her stern sunk in the bay, her crew's camp in the dunes |
| Balboa | ![](images/library/balboa_remade.jpg) **Balboa Remade** | A forest of pine and maple round a mountain: the rangers' station, the Mama bear's den behind a vine-grown barricade (cut with the machete), a radio mast on the summit |
| Caravan Town | ![](images/library/caravan_town_remade.jpg) **Caravan Town Remade** | Red sandstone stacks: a market in the inlet, a lift of ladders, a bridge slid across by a winch to the mayor's pillar - and a white screecher on his roof |
| Tangaroa | ![](images/library/tangaroa_remade.jpg) **Tangaroa Remade** | The floating city on a hill of its own: a harbour deck on one of the old ring pontoons, the pump house, the square with the founder's statue, the generator and the council's vault (three key cards); Raft's vending machine |
| Varuna Point | ![](images/library/varuna_point_remade.jpg) **Varuna Point Remade** | A construction site on a long rocky point: the half-built tower in its scaffolding, the floodlight's four parts on the sea floor, the floodlight tower, a giant angler in the deep |
| Temperance | ![](images/library/temperance_remade.jpg) **Temperance Remade** | An igloo village, Selene's station whose reactor opens its inner doors (three cable drums - one under the ice), the telescope on the peak, the polar bear mother |
| Utopia | ![](images/library/utopia_remade.jpg) **Utopia Remade** | The end of the voyage: docks with Utopia's market, the warden's prison house on its stilts, and his yard on the summit behind a palisade - a gate with two keys, his hyena pack and their alpha; a Raft store by the market |

Each island is on the Receiver at its story island's place (600 to 1200 m ahead when tuned), and each is done when its
quest is done; the plan also has **Random islands while sailing: on** and **Raft's story islands: on**.

What it shows: rules that take a story island's place (**instead of**, [7.4](#74-everything-a-rule-can-do)), islands
made from Raft's own islands with the generator's **like**, and on each island a different way to lock the reward:
a story item used on an object (a battery, a crank, cables, floodlight parts), a key card count (Tangaroa's vault takes
three), a signal (Varuna's strongbox opens only once the floodlight burns), two keys at once (Utopia's gate).

**The Abyss Expedition** - an expedition downwards beside Raft's story: each island comes **on the Receiver** (500 to
900 m ahead when tuned) when the one before is done - its quest done, or players reached it: an old mine, a buried
shelter, a **sunken island** (a map type, made new for every world), caverns, a **wreck** (a map type too), a drowned
city - and at the end **The Abyss**. Its main islands are about 90% the size of Raft's own big islands.

| The plan's last island | What it is | What it shows you how to build |
|---|---|---|
| ![](images/library/the_abyss.jpg) **The Abyss** | A ring of black sea stacks around a trench 42 m deep: the expedition's raft moored over its edge, its air line running down to three air pockets (a container on a shelf, a gas tank on a ledge, a shack on the floor) and, on the floor, the lair of the Abyssal Angler and its brood | A raft of Raft's foundations, a trench cut with the brushes (a bowl, a shaft, a shelf and a ledge flattened into its walls), **air pockets** - trigger zones with Air, hidden until the valve shows them, with rising bubbles ([5.4](#54-trigger-zones-and-ambushes)) |

The plan has **Random islands while sailing: off** and **Raft's story islands: on**.

What it shows: rules brought by **quests and visits**, main story islands **beside** Raft's story
([6.5](#65-your-islands-in-rafts-story-the-receiver)), and map types mixed with your own islands.

**Raft 2: The Drowned Frontier** - a sequel with a story of its own: years after Utopia the sea is rising again, and a
trail of the old Frontier Corps' stations leads across the storm belt to the last high ground. Raft's story islands are
off; the plan's ten quest islands come one after the other - the first is on the Receiver from the start, and each quest done
gives the next station's frequency on the Receiver. Raft's key blueprints are found along the way in Raft's order, so
the raft can still be steered, driven and fuelled; the tenth island's quest is the ending. Its main islands are about 90% the size of Raft's own big islands.

| Station | The plan's island | What it is |
|---|---|---|
| 1 | ![](images/library/frontier_beacon.jpg) **Frontier Beacon** | A black volcanic spike: the keeper's hut on the ash flats, a ramp of fill up to the beacon's terrace, a spare cell in a sunk supply boat, stone birds and their sentinel (the Receiver's and the antenna's blueprints) |
| 2 | ![](images/library/saltmarsh_ferry.jpg) **Saltmarsh Ferry** | A salt marsh cut by a tidal channel, the Corps' ferry beached at its end - pump her dry, clear the rats out of her hold (the motor wheel's and the steering wheel's) |
| 3 | ![](images/library/cinderfall.jpg) **Cinderfall** | A volcano with a geothermal plant at its foot - three relief valves on its flanks, the stone birds' matriarch, roaches in the turbine hall (the machete and the fuel blueprints) |
| 4 | ![](images/library/sunken_archive.jpg) **The Sunken Archive** | An archive standing in the sea between five palm islets, its stacks flooded - catalogue cards, an air pocket, an angler at the vault (the engine controls', the metal detector's, the firework's and the zipline's) |
| 5 | ![](images/library/hightide_harbor.jpg) **Hightide Harbor** | A fishing town on stilts round a bay - bears in the fish hall, the water works on the hill, a lamp tower of Raft's blocks to light (the water blueprints); Raft's vending machine |
| 6 | ![](images/library/iron_graveyard.jpg) **The Iron Graveyard** | A ship breakers' yard on a red mesa's beach - beached hulls to climb, a barge sunk off the beach, a crawler crane, the turbine on the mesa (the power blueprints); off the quest, Raft's **generator** and **radio** (a supply drop) and Vasagatan's **engine** (`lib_features`) |
| 7 | ![](images/library/storm_spire.jpg) **Storm Spire** | A weather station on a needle of rock in the storm belt - a short climb, three conductor rods, the storm bird; six steps, short on purpose (the electric smelter's, the advanced biofuel extractor's and anchor's); a Raft store at the needle's foot |
| 8 | ![](images/library/whiteout_reach.jpg) **Whiteout Reach** | The Corps' winter camp on a snowy hill - a supply ship frozen into the shore ice, the polar bear matriarch in a crevasse, the radio hut on the summit |
| 9 | ![](images/library/drowned_gate.jpg) **The Drowned Gate** | A sea wall across a strait between two hills - its lock of two steel gates that sink to open, the pump house's flooded hall with an angler in it, the gate office's vault (the titanium tools', the big backpack's and the electric zipline's); a Raft store on the east hill |
| 10 | ![](images/library/the_frontier.jpg) **The Frontier** | The last high ground: a harbour town under a great mesa, farm terraces climbing its face, the upper town and the Corps' hall on the rim, the Corps' lift down the cliff to a vault sunk in a cove - thirty-two steps in six chapters |

The first island is on the Receiver from the start (500 m ahead when tuned); each one after it (800 to 1300 m ahead when
tuned) once the one before is done, and each is done when its quest is done. The plan has **Random islands while
sailing: on** and **Raft's story islands: off**.

What it shows: a plan with a story chain of its own (**first** and **after**, [7.4](#74-everything-a-rule-can-do)) and
no island of Raft's; quests of every length - six steps on Storm Spire, thirty-two in six chapters on The Frontier;
story items an object only uses up once the quest has counted them (an **Only if** the quest reached that step before
the check that **uses them up**, so a player who finds things early can't lock the quest); a lift that moves the
player (**teleport to**); doors that slide aside when three seals are set.

**Silver Screen Seas** - Raft's whole story with the library's themed islands ([15.6](#156-the-themed-islands)). After
every story island a big themed island comes on its own Receiver frequency (after Temperance two, one after the other);
its quest done, the story goes on as Raft's. The ten normal-size themed islands turn up as side trips while you sail. Its main islands are about as big as Raft's own big islands.

| After | The plan's island | Size |
|---|---|---|
| Radio Tower | ![](images/library/camp_blackwater.jpg) **Camp Blackwater** | twice the size |
| Vasagatan | ![](images/library/sunken_liner.jpg) **The Sunken Liner** | twice the size |
| Balboa | ![](images/library/step_pyramid.jpg) **The Step Pyramid** | twice the size |
| Caravan Town | ![](images/library/the_arena.jpg) **The Arena** | three times the size |
| Tangaroa | ![](images/library/albatross_field.jpg) **Albatross Field** | three times the size |
| Varuna Point | ![](images/library/island_of_stations.jpg) **The Island of Stations** | five times the size |
| Temperance | ![](images/library/primeval_park.jpg) **Primeval Park** | five times the size |
| Primeval Park | ![](images/library/sundown_canyons.jpg) **Sundown Canyons** | ten times the size |

The **side trips**, every one 450 m ahead of the raft: Tiki Lagoon (2 km), Gilded Skull Cove (4 km), The Drowned
Labyrinth (7 km), Keeper's Light (10 km), Embers Isle (13 km), The Safe Room (16 km), Sun Atoll (20 km), The Rock Pen
(24 km), Highmoor Lodge (28 km) and Crater Lair (32 km).

The main quest islands are on the Receiver 700 m (Camp Blackwater), 900 m (the next three) or 1000 m (the rest) ahead
when tuned, and each is done when its quest is done. The plan has **Random islands while sailing: on** and **Raft's
story islands: on** (all eight).

What it shows: a story island's place taken by **two** islands in a row - Sundown Canyons comes **after** Primeval
Park, a rule of the plan itself ([7.4](#74-everything-a-rule-can-do)) - because nothing can come after Utopia, which
ends Raft's story and never counts as done.

**Keys kept for later islands.** In every plan, a lock on one island wants a story item found on an island met
before, kept by the crew (none of them is needed by the main quest, and each lock says where its key is): in Raft 2,
the Corps keycard, the bolt cutters and a radio part; in Raft Remade, Vasagatan's crowbar prises open a crate at
Caravan Town's market; in The Long Voyage and The Abyss Expedition, the pump store key in Ironreef Caverns' foreman's
locker opens the mining company's pump store on the Drowned Metropolis' ferry islet; in Silver Screen Seas, the chief
steward's key ring from the Sunken Liner opens Pia's sea chest at The Arena. Every plan gives Raft's blueprints in
Raft's order (Check and Progression, [7.5](#75-check-finding-and-fixing-problems)).

### 15.5 Where to look for...

| To learn how to make... | Open |
|---|---|
| Ladders up a building or a rock | Signal Rock, Scrapyard Haven (its fortress), Wreckers' Cove (the lantern) |
| A door, gate or bridge that opens (moves) | Shelter Atoll (the commander's door), Scrapyard Haven (the gate on a winch), Frost Hollow (the lab door), Caravan Town Remade (a bridge slid across by a winch), Tangaroa Remade (a shutter the generator slides aside), Temperance Remade (the reactor's inner doors), Utopia Remade (a gate with two locks), The Frontier (the hall's sliding doors) |
| Something that opens only after something else (a **signal**) | Glacier Station (the reactor and its vault), Wreckers' Cove (the doused lantern and the hoard), Coral Observatory (the analyzer and the safe) |
| Loose things to pick up inside houses (plastic, planks, scrap on the floors; they come back after a few days) | Halcyon Cove, The Frontier, Great Fen, The Safe Room, Trade Wind Archipelago, Drowned Gate, Keeper's Light and eleven more of the library's islands, and the generator's huts and cabins: Raft's land pickups (`Pickup_Landmark_Plastic3_Land`, `..._Plank_Land`, `"..._Scrap 1_Land"`) placed with `drop` on a house's floor |
| An object used with a story item (a key, a fuse, samples, a wrench) | Signal Rock (the coil), Stilt Hollow (the valve), Coral Observatory (three samples at once), The Abyss (the air valve), The Frontier (three valve wheels, the horn's reed, the seal press - each used up only once the quest has counted it) |
| A lift or anything that moves the player | The Frontier (the Corps' lift: **teleport to** its other end), The Safe Room (a hatch down an escape shaft and back), Rookery Cliffs (Raft's own scissor lift, which carries you) |
| Roads and terraces up a steep slope (the terrain brushes) | The Frontier (two farm terraces and three wide ramps up a 35 m mesa), The Iron Graveyard (a ramp cut up the mesa's cliff), The Safe Room (a driveway cut along a cliff face) |
| A town | Hightide Harbor (a fishing town on stilts), The Frontier (a harbour town and an upper town of four quarters) |
| Caves | Old Mine Islet, Ironreef Caverns (four), Wreckers' Cove (a sea cave) |
| Rooms under the ground | Shelter Atoll |
| Darkness and light (atmosphere zones, lamps that come on) | Shelter Atoll, Old Mine Islet, Ironreef Caverns, The Abyss (its lair) |
| Things under water to dive for | The Stranded Gull, Mayor's Wharf, Crane Yard, The Drowned Metropolis, Coral Observatory, The Abyss |
| Air pockets for a long dive | The Abyss |
| A boss (bigger, tougher, tinted) | Ranger's Rest (Old Scar), Thornwood (the Warthog King), Glacier Station (the Polar Bear Queen), The Drowned Metropolis (the Lantern Lord), Scrapyard Haven (the alpha), Coral Observatory (the giant angler), The Abyss (the Abyssal Angler), and one on each of Raft Remade's islands (the Mama bear, the white screecher, the giant angler, the polar bear mother, the hyena alpha...) |
| Animals to catch | Tide Farm, The Frontier (the grower's goats), Tiki Lagoon (the petting zoo's runaway goats) |
| Buildings from Raft's pieces | Signal Rock and Wreckers' Cove (radio tower pieces), Shelter Atoll and Coral Observatory (Selene rooms), Mayor's Wharf (City Hall), Hightide Harbor and The Frontier (Raft's own wooden walls with hip roofs on their pillars) |
| A raft or a deck of Raft's foundations | Tide Farm, The Abyss |
| Land shaped with the brushes | Thornwood (a gorge), Glacier Station (a crevasse), Coral Observatory (a blue hole), Scrapyard Haven (a pit and a terrace), The Abyss (a trench with a shelf and a ledge) |
| An island in Raft's story | The Long Voyage (after each story island), Raft Remade (instead of each story island) |
| An island made from one of Raft's own (the generator's **like**) | Raft Remade's eight islands |
| A chain of islands that bring each other | The Abyss Expedition |
| **Raft's zipline** (a line with its far end moved, the zipline tool in a chest) | Cablecar Stacks (from the summit down to the shore stack) |
| **Machete vines** hiding a chest that shows once they are cut | Thornwood (two caches; the Boar King's offering gives a machete), Gilded Skull Cove, Primeval Park, Old Vine Hill |
| **Buried treasure** for the metal detector and the shovel | Gilded Skull Cove (the detector in the hoard), The Cartographer's Isles (the detector and a shovel in the map case), Wreckers' Cove |
| Raft's **dirt spots** (the shovel) | Crowfield Farm, The Clockwork Orchard, Old Vine Hill, Primeval Park, Glasshouse Gardens |
| **Wild beehives** (honeycomb that fills up again) | Bramblehive Knoll, Crowfield Farm, Old Vine Hill, Thornwood, Glasshouse Gardens |
| Raft's **keycard door** (a ready piece: it wants Raft's own keycard, found in a chest on the island) and a **hatch** with a chest under it | Star Fort (the quartermaster's strongroom in the north barracks, his satchel on the glacis; the hatch on the parade ground), Whalebone Bay (a padlocked door on the cooper's workshop - the bolt cutters), Stranded Gull (a hatch), Whiteout Reach (the Corps strongroom: its keycard is on Frontier Beacon, in Raft 2) |
| Raft's **crank wheel** and **lever** (each sends a **signal**: a crate shown, a bolted chest let go) | The Clockwork Orchard (either side of the workshop), Keel and Mast (the yard's timber winch), Scrapyard Haven (a lever at the fortress stilts) |
| Raft's **lift** (Varuna Point's scissor lift: it carries you up, stands there extended, and carries you down again) | Rookery Cliffs (Eskil's egg hoist beside a ledge cut into the south crag), Old Vine Hill (a grape hoist between two terraces) |
| Raft's **cage** cut open with the **bolt cutters**, and a **security camera** that sweeps | Primeval Park (the feed chest caged in the boar paddock, the cutters in the keeper's tool chest; the camera on the keeper's hut), Big Top Atoll (a cage), Hightide Harbor (a dog cage: the cutters are on Saltmarsh Ferry, in Raft 2), Crater Lair (a camera over the jetty) |
| A **generator** started with Raft's generator part, a **radio** that works on its power and gives away a hidden supply drop, Vasagatan's **engine** started with a gas tank | The Iron Graveyard (the part in a mechanic's toolbox, the radio on the yard office's table; the trawler's engine on the yard, its fuel in a crate), Scrapyard Haven (the market's generator and radio), Stranded Gull (the tender's engine), The Frontier (the harbour office radio: its charger part is on The Drowned Gate, in Raft 2) |
| Temperance's **turning mirrors** (a chest that opens once both are turned) | Glasshouse Gardens (Lind's sun mirrors and her seed chest), Step Pyramid (two sun mirrors on the plaza) |
| The mod's **puzzle pieces** (15.8): a **laser** beam to turn onto a door panel, **water wheels** to turn in order, **pipes** to open before a test lever, **wire** hubs to cable in order, a **claw** machine, **scales** to weigh | Crater Lair (a roof laser and a hidden safe), The Aqueduct (three wheels and a hint tablet), Rig Seventeen (pipes and a test card), Tiki Lagoon (cable hubs on the generator shed), Big Top Atoll (a claw machine, one token a go), Gullsong Bazaar (weighing pails and a strongbox) |


### 15.6 The themed islands

Islands built round a theme the movies love - a pirate cove, a beach club, a desert villa with a safe room and more -
each with its own island type and its own quest. Ten are the normal size; the others are two, three, five or even ten times
as big (the island's **Radius** in the generator, [4.5](#45-the-island-generator)). Like the other examples, each one's
script is in `content\recipes` and the island opens in the editor. (Those scripts are the development build's
build scripts for the library islands, not something players need.)

| Island | What it is | What it shows you how to build |
|---|---|---|
| ![](images/library/gilded_skull_cove.jpg) **Gilded Skull Cove** | Two jungle peaks over a cove like the brows of a skull: a pirate captain's camp, her ship sunk in the cove's mouth, her map torn in three and her hoard under three standing stones | A map of three story items laid together on a table, a stilt lookout to climb, stones to push in a verse's order (each one moves only after the one before it: a chain of **signals**), feral hogs |
| ![](images/library/tiki_lagoon.jpg) **Tiki Lagoon** | A beach club on sandy keys, left in a hurry before a storm: a tiki bar, bungalows on stilts over the lagoon, a deep pool with a sunken pedal boat, two runaway goats | A bar of Raft's floor blocks under a thatch hip roof (`roof`), huts on stilts and walkways over water (`stilt_hut`, `boardwalk`), a generator that switches lights and music on (things **shown** by a behaviour) and opens a safe (a **signal**), animals to **catch** |
| ![](images/library/sun_atoll.jpg) **Sun Atoll** | A ring of sun-baked islets round a lagoon after a weapons test: an observation blockhouse buried in a dune, the tower's stumps round a crater at ground zero, measuring posts behind wire | Rooms in pits under concrete slabs (on the terrain's 1.95 m grid), a trench, a crater dive, a story item that turns a **zone** on (the dosimeter), three films for a projector that opens a safe |
| ![](images/library/the_safe_room.jpg) **The Safe Room** | A concrete villa on a red desert bluff, broken into one night: the owner and her daughter hid in the steel room behind the study's bookshelf, and the men's dogs still guard the garden | A driveway cut up a cliff face, rooms of the radio tower's pieces, a keypad that wants three story items, a bookshelf that **slides** aside (a mover) and shows hidden dogs, a hatch that **teleports** down an escape shaft |
| ![](images/library/keepers_light.jpg) **Keeper's Light** | Sea stacks in a grey sea joined by rope bridges: a lighthouse dark for three nights, the keepers' cottage, and a flooded cistern under a concrete slab where something with a lamp on its head waits | Buildings on three sea stacks joined by rope bridges (lanes of floor blocks with rope rails), a lighthouse of concrete pipes with ladders up its outside, a cistern dug into the rock and a hatch down into it, a lamp lit with a story item |
| ![](images/library/highmoor_lodge.jpg) **Highmoor Lodge** | A mountain lodge on a snowy hill, closed for the winter: the caretaker's typed pages all over the house, music in the empty ballroom, a maze of young firs with a white bear at its heart, a boiler to vent | A two-storey house of Raft's solid wooden walls with a stair and a hip roof, wings with their own roofs, a maze (evergreen runs with invisible walls, `Helper_Wall`), a **Find pages** step, three valves in order (signals), a door that **slides** aside on a key; a Raft store south of the lodge |
| ![](images/library/crater_lair.jpg) **Crater Lair** | A black volcanic cone with a great dish humming in its crater and a hangar hidden in its west bluff: a seaplane, a control room, a pet's pool behind bars and a getaway jetty | A slab of the mountain that a hidden panel lifts (a door that **moves**), rooms dug into a bluff, a keycard dived for in a pool behind bars, a **timed** escape - a blast door that shuts 45 seconds after the codes burn |
| ![](images/library/the_rock_pen.jpg) **The Rock Pen** | A prison on a flat granite rock in the fog: the yard, a guard tower, cell block B, the mess hall - and the drain inmate 1147 dug from his cell's vent down to the sea | Cells with fronts of bars that **slide** open on a key, a tower of thick legs with a ladder, a vent that **teleports** you into a tunnel at the cliff's foot, a kit shown only once you are in the tunnel, an outer rock raised from the sea floor |
| ![](images/library/embers_isle.jpg) **Embers Isle** | A black volcanic crescent round a bay: a whalers' village of stilt huts round a fire that never goes out, a captured botanist's journal - and her boat hidden in a vine-hung sea cave under the headland | A village of stilt huts round a fire, a chest that blows a horn and **shows** hunters with trained boars (a chase, with music), Balboa's vine cave set into a cliff at the waterline |
| ![](images/library/camp_blackwater.jpg) **Camp Blackwater** (twice the size) | A summer camp on a forest lake, empty since the season ended early: cabins, canoes, an archery range, a radio still playing - and the Ridge Bear the head counselor stayed behind to lure away | A lake dug down to the sea's level inland, a camp of Balboa's log house and relay-station cabins, a bait pile that **shows** the bear, a fire lookout with a ladder, a flare that **gives** a key |
| ![](images/library/step_pyramid.jpg) **The Step Pyramid** (twice the size) | A jungle plateau with sheer cliffs and the Rain Kings' stepped pyramid on top: a staked shrine, a sacred well straight down to the sea, a moat of stakes and three jade tablets for the summit altar | A pyramid raised from the ground in square tiers with walls of stone blocks and Raft's stairs, a well dug past the sea's level with a gorge out to sea, stepping stones **shown** when a tablet is taken, stakes that sink on a hidden lever |
| ![](images/library/drowned_labyrinth.jpg) **The Drowned Labyrinth** | Sandy desert islets on a turquoise reef and a dive camp on the largest - and 19 m under the reef a labyrinth of flooded tunnels: a guideline through the dark, an air bell, a hall of columns and a tide gate that opens for forty breaths | Temperance's underwater cave tunnels joined end to end under a reef (`kit_labyrinth` measures where each piece's passage runs), holes dug through the reef down to them, an air bell, a gate that **opens** for a while and shuts again |
| ![](images/library/sunken_liner.jpg) **The Sunken Liner** (twice the size) | Icy rocks in a cold sea and a great liner broken in two on the sea floor between them: the bow standing upright, the ballroom with a breath of air under its ceiling, the purser's safe with the Polar Star - and something with lamps in the stern | A trench dug 16-24 m down between the rocks, a ship's halves of Raft's wrecks set on the sea floor, rooms of the radio tower's walls under water with air in them (a **zone** that refills your breath), a survivors' camp on the biggest rock |
| ![](images/library/albatross_field.jpg) **Albatross Field** (three times the size) | A coral atoll with a wartime airfield: a runway along its west side, canvas hangars, a control tower with a glass cab, a power bunker, a fuel dump on the next islet, a fighter ditched in the lagoon - and one flight still out on patrol | Caravan Town's great roof on its poles as field hangars, a tower of the relay station's stairs and rooms, passes cut through an atoll's ring, four lamps lit in order (each needs the **signal** of the one before), a story item **given** when the generator starts |
| ![](images/library/the_arena.jpg) **The Arena** (three times the size) | Two forested peaks over a green plain built for a contest that still runs on its own: a great canopy of supplies ringed by starting plates, cameras on poles, three rounds of beasts, a gift dropped on a lake's islet - and a door in the peak that opens when the champion falls | Hidden animals **shown** round by round (each round's **defeat** shows the next), a chained crate that opens on a **signal**, a lake dug round an islet, a control room of Selene's cells in a pit cut into a slope under a slab, its sliding door **opened** by the champion's defeat; Raft's vending machine |
| ![](images/library/island_of_stations.jpg) **The Island of Stations** (five times the size) | A misty forest marsh where a plane came down among the stations of a vanished research initiative: a radio station, a lab sunk in the bay, a zoo of empty cages, a village round a green, an observatory - and a hatch whose vault opens for sixty seconds | Five story items gathered across a big island, rooms under a slab reached by a ladder down a shaft, a lab under water with air under its ceiling, a vault door that **opens** for a while and **closes** again (and a lever inside that opens it), a long quest in chapters |
| ![](images/library/primeval_park.jpg) **Primeval Park** (five times the size) | A park of giant beasts on a jungle crescent round a bay, dark since a storm blew its fuses: a visitor centre, a boar paddock, a river ride, a power house, an aviary, a show lagoon - and behind the great gate, the park's Queen | A low crescent from the generator (a tall one is all ridge), a terrace cut for a block of rooms, a channel dug from the bay inland, three story items **taken** together by a switch that **shows** the park's lights, a gate that wants power, a card and a key, animals that wake when you enter their **zone**, a quest in four chapters; Raft's machete vines, a chest caged until Raft's **bolt cutters** open it and a sweeping **security camera** (`lib_features`) |
| ![](images/library/paradise_archipelago.jpg) **Paradise Archipelago** (ten times the size) | Seven sandy keys joined by boardwalks: a marina, a hotel tower, a water park, villas, a lighthouse bar - and on its own key the Golden Gull casino with its gold still in the vault | A heist in chapters: five kits gathered from five keys (story items), breakers pulled in order to black out the casino, a dive through a service tunnel, lasers switched off for thirty seconds by a card (a **timed** hide/show), a time lock, the gold carried to the getaway boat |
| ![](images/library/sundown_canyons.jpg) **Sundown Canyons** (ten times the size) | A red-rock island of canyons and mesas: a frontier town on the west shore with a bank, saloon, jail and station, a silver train derailed in a canyon, a blown trestle over a river, a mine whose lift runs up through Black Mesa - and the outlaw gang's fort on top | A railroad of ties and rails laid up a canyon cut through the hills (`rot=0,0,0` keeps them lying flat), a river let in from the sea through a gorge, a lift that **teleports** you up through a mesa, a ladder trail up cliff bands faced with timber walls, animals to **catch** with a net, a long quest in chapters; a Raft store on the edge of town |
| ![](images/library/blackwall.jpg) **Blackwall** (twice the size) | Three black basalt stacks built over with concrete - a sea wall, blocks of flats, a mine head - closed in one night when the seam flooded; four letters never posted, a flooded shaft | Stacks raised from a shallow shelf, blocks of the radio tower's rooms with outside stairs, bridges between stacks, letters **taken** by a sorting rack that gives a key, a padlocked gate that **turns** open, a shaft dived into the dark |
| ![](images/library/frostgold_creek.jpg) **Frostgold Creek** (twice the size) | A gold-rush boomtown in the snow at the mouth of a creek between two peaks: sluice boxes along the creek, an assay office, a mine with a white bear, a claim post on the saddle | A creek dug from the coast up a valley to a pool, plank sluices ramped off its banks, five nuggets found in different ways (boxes, a cave, a dive) weighed on scales that **give** a slip, a chest that only opens on a **signal**; a Raft store down the creek |
| ![](images/library/halcyon_cove.jpg) **Halcyon Cove** (three times the size) | A perfect little town where it rains on cue and there are cameras in the flower beds - and out at sea a wall painted like the sky, with a door in it | A rain **zone** switched off by a machine, four hidden cameras that each **give** a tape once, a console that wants a key and all four tapes, a wall of the radio tower's walls on a raised sandbank with one piece that **swings open** like a door |
| ![](images/library/aurelis.jpg) **Aurelis** (five times the size) | A ring of volcanic hills round a lagoon, and in a blue hole in its middle a drowned city: an agora, temples, a canal, a tower, a gate of three crystals with a guardian, a throne hall with air under its ceiling | A hole dug in three terraces with angle loops, Varuna's pipes stacked into a tower, Selene's cells as a throne hall behind a gate that sinks when three story items are **taken**, an altar whose zone is **shown** later, a tower's top **shown** rising out of the water |
| ![](images/library/whalebone_bay.jpg) **Whalebone Bay** (three times the size) | A whaling station in a snowy bay: a quay with a steam crane, a boiler house, a flensing plan with its harpoon cannon, a white church and the manager's house - and the last catcher sunk at the quay with the men's pay | A boiler fired by three sacks **taken** from three places, a crane that hauls a strongbox up from the sea floor (one **hidden**, one **shown**), a firing pin that spikes a cannon and **shows** a whale |
| ![](images/library/krakens_wake.jpg) **The Kraken's Wake** (three times the size) | A chain of pine islets on a reef, and along its south side five wrecks in a line, each deeper than the last - the wake of something that pulled a squadron down - ending at a trench | A furrow dug deeper along its length with wrecks laid in it on the ground (`ground sit`), a harpoon gun whose three parts are **taken** by a battery, a boss and its brood **shown** together, a quest that leads you down and down |
| ![](images/library/heart_of_fire.jpg) **Heart of Fire** (ten times the size) | Two great peaks over an island of ash: a burnt village, a navigator's wreck in the bay, a lava tube, an obsidian temple, the Mother's spring and summit - and a fire-bird over the pass | A whole island in two layers: ash and dead trees **shown**, palms, fruit and flowers **hidden**, swapped (and the sky's atmosphere with them) when the quest's last story item is laid down; braziers lit in order (signals), a zone **shown** only when its step comes, a quest in four chapters |


### 15.7 The new islands

More islands of the library, each with its own theme and island type - and a quest of its own kind: a timed escape, an
underwater survey, a climb and a riddle, a barter chain, a round-up... (some have no quest at all: islands to wander).
They come in five sizes: normal, twice, four times, seven times and twenty times the usual size.
The last twenty rows are the **vehicle islands** (from Breakdown Bend on): each is built round Raft's own vehicles -
vans, caravans, excavators, forklifts, cranes, ore wagons, snow vehicles, planes and boats - and about half have a quest.

| Island | What it is | What it shows you how to build |
|---|---|---|
| ![](images/library/sulphur_vent.jpg) **Sulphur Vent** | A fuming volcanic cone with a sulphur works on its shoulder; lift the sample case off the vent's cap and you have forty seconds to reach the boathouse | A **timed** escape: a chest whose opening **shows** a gas zone, **opens** a shutter and **closes** it again 40 seconds later, and a lever that runs the same again for a second try |
| ![](images/library/kelpdeep_rock.jpg) **Kelpdeep Rock** | Pine islets over a forest of giant kelp, and four caves under it to measure | Kelp scattered on the sea floor (`scatter ... wet`), caves dug into a reef with fallen rocks over them, air pockets, four posts that each **give** a reading once (`!signal`), a recorder that **takes** all four |
| ![](images/library/hermits_table.jpg) **Hermit's Table** | A flat-topped rock on sheer cliffs, a ladder scaffold up its face, a hermit's hut with three riddles and three levers | A scaffold of thick pillars with ladders stacked up its face and a ledge, three levers of which two **show** hidden birds and one **gives** the key |
| ![](images/library/gullsong_bazaar.jpg) **Gullsong Bazaar** | A floating bazaar on a marsh lagoon, closed for the season: five stalls, one thing left at each | A lagoon dug in a marsh with a boardwalk and pontoons over it (a `stall` macro), a **barter chain**: each stall **takes** one story item and **gives** the next; a Raft store west of the lagoon |
| ![](images/library/crowfield_farm.jpg) **Crowfield Farm** | A small farm on a green island - a farmhouse, a barn, a windmill, scarecrows - and three goats loose on the hill | A farmyard levelled on a plain with fields of harvestables, a **catch** step with the net launcher from a chest, a basket that opens once the quest has reached it (`quest|4`); a Raft store on the plain |
| ![](images/library/viaduct_lagoon.jpg) **Viaduct Lagoon** (twice the size) | A snowbound railway atoll whose viaduct broke under the mail train; three cars on the lagoon's floor, air under their roofs | A viaduct of thick pillars standing from a trench 11 m deep (their feet buried where the floor is higher), its broken spans lying on the floor, **air pockets** (an air zone under each car's roof) for salvage dives |
| ![](images/library/old_vine_hill.jpg) **Old Vine Hill** (twice the size) | A terraced vineyard on two dry peaks, a cellar in the hill and a flooded reserve vault below it | Terraces flattened in steps round a peak with rows of vines (a `vines` macro), a **pages** step, four casks tapped in order (each needs the last one's **signal**; out of order they just drip), a vault that the pump **opens** and fills with air |
| ![](images/library/big_top_atoll.jpg) **Big Top Atoll** (twice the size) | A circus wintering on a sandy atoll: the big top, wagons in a circle, a trapeze rig - and its menagerie loose | Caravan Town's great roof on its pillars as a big top, objects placed round a circle with `cos`/`sin`, two **catch** steps one after the other, hyenas that wait for a zone, a trunk to dive for |
| ![](images/library/ashfall.jpg) **Ashfall** (twice the size) | A village on a volcanic table-land buried to its eaves in ash: a bell house, a town hall, a bakery, a school with its door buried, a chapel's crypt, and the shelter in a lava tube by the sea | A **pages** hunt through dug-out houses (banks of ground against the walls, the village's rocks cleared), a door of packed ash that sinks when dug with a shovel (`if.use=has`, a mover), a crypt in a pit under a slab, a bell that **opens** a gate far below and **shows** its zone, a road cut up a cliff in two legs |
| ![](images/library/redstack_maze.jpg) **Redstack Maze** (twice the size) | A maze of red rock stacks in the sea, six cells by six, with a heron shrine behind a water gate; the tide-keeper's lever opens the gate for 75 seconds | A maze laid out on a grid (walls of one rock stretched thin, a column in every unused cell), a **timed** gate (open, `wait|75`, close), landmarks a chart names, a shrine on a deck on posts, a bell that **teleports** the player home |
| ![](images/library/bramblehive_knoll.jpg) **Bramblehive Knoll** | A beekeeper's knoll cut into three ring terraces of hives, a mead shed by the landing and the bee-mother skep on the summit | Crisp ring terraces (flatten strokes round each band) with a ramp between each, hives as chests, swarms that can't be beaten, a press that **takes** three combs and **gives** the mead, a delivery to a chest that takes it |
| ![](images/library/clockwork_orchard.jpg) **The Clockwork Orchard** | An inventor's orchard on a flat-topped rock: three wind pumps at its rim, gutters to a dry fountain, his workshop under the cliff | A **resettable order puzzle** on state checks (`state|cog|shown`: a wrong pump hides every cog), hidden spinning cogs, a shutter that sinks when the fountain runs; Raft's **crank wheel** and **lever** by the workshop, each sending a **signal** (`lib_features`) |
| ![](images/library/turtlewatch_atoll.jpg) **Turtlewatch Atoll** | A sea-turtle sanctuary on a palm-ringed atoll: hatchery pens, a warden's hut on stilts, a reef lookout - **no quest**, a place to wander | An island without a quest: notes and boxes to find, animals in a lagoon (turtles, rays, dolphins), shallows in the lagoon as well as round the coast |
| ![](images/library/rookery_cliffs.jpg) **Rookery Cliffs** (twice the size) | Two snowy crags loud with sea-birds - **no quest**, a place to climb: an egg collector's cabin, a winch basket up a cliff, ladders from ledge to ledge past nests, screechers on the top | Ledges cut one above the other on a cliff face and joined by ladders, a basket that rides up a cliff (a `teleport`), nests and notes to find without a quest; Raft's own **lift** that carries you up to a ledge cut into a crag (`lib_features`) |
| ![](images/library/bell_stack.jpg) **The Bell Stack** | Three snowy sea-stacks joined by a rope bridge, a chapel bell, a torn chant and a hermit's cell | Sea-stacks flattened at two heights, a rope bridge, a **pages** step, a bell that refuses until the chant is whole and then **shows** a hidden plank bridge |
| ![](images/library/driftglass_bay.jpg) **Driftglass Bay** | A desert crescent round a shallow bay, a lighthouse built of glass bottles on its crest, a beachcomber's hut on stilts and a driftwood whale | A **collection** of eight story items from notes (a note that **gives** an item when read) on land and under water, a lamp that **takes** all eight and **shows** its light, a chest locked until then |
| ![](images/library/ice_organ.jpg) **The Ice Organ** (twice the size) | A frozen crescent with an organ of five ice pillars in the sea cave under its ridge, the organist's score cut in ice beside it and a crypt behind an iron gate | A **music sequence**: five pipes struck in the score's order with a mallet (each needs the last one's **signal**; out of order it just clangs), a great icicle that drops the crypt's key, a bear that sleeps until the gate opens |
| ![](images/library/saltpan_delta.jpg) **Saltpan Delta** (twice the size) | A bust salt works on a desert delta: three pans of brine, sluices between them, a pump house, a salt store full of rats, a wagon line down to the jetty | **Restarting a works**: a crank, sluice boards that **move** to drain three pans in turn, salt raked from each, a wagon that **takes** three sacks and rides the line to the jetty; a Raft store by the works |
| ![](images/library/lanternfall.jpg) **Lanternfall** (twice the size) | A palm crescent at dusk laid out for a lantern festival that never started: lantern posts along the shore and up the hill, food stalls, a stage, rockets in their rack | A **count** of twelve lanterns lit with a taper (each lantern **shows** its own light once), a stage that waits for all twelve and sets off a fireworks finale |
| ![](images/library/cartographers_isles.jpg) **The Cartographer's Isles** (twice the size) | A mapmaker's desert archipelago round a green lagoon: her drafting hut, a trig pillar, a survey tower, a drowned post and an X on a sandbar | A **map trail**: four pieces of a map, each found only where the last one's bearing points (hidden until then), one of them under water, and a dig at the X |
| ![](images/library/shellback.jpg) **Shellback** (four times the size) | An island like a sleeping tortoise with a shrine at each leg - a flooded sea cave, a needle of rock, a ring of stones, the sacred hens - and the Shell-keepers' temple on her head | **Four trials of four kinds** (a dive, a climb, a fight, a catch), each giving a scute, and a temple gate that **takes** all four |
| ![](images/library/cablecar_stacks.jpg) **Cablecar Stacks** (four times the size) | A cable car up four forested sea stacks, stopped by three faults: a spare fuse lost in the sea, screechers in the mid station's wheel, the high station's brakes locked | **Restoring a network**: each station's fault fixed in its own way (a dive, a fight, three brakes released in order), each car a ride (`wait`, then a `teleport` to the next station) |
| ![](images/library/sargasso_town.jpg) **Sargasso Town** (four times the size) | A floating town of wrecks in a volcano's lagoon: hulls roped to hulls, plank streets, a council house on a weed islet - and five captains away for the season | **Five favours for five votes**: a fetch, a fight in a galley, a dive under a capsized hull (air in it), a repair, a lost spyglass - plank streets as boardwalks turned with `push ... yaw=`, a ballot box that waits for all five |
| ![](images/library/glasshouse_gardens.jpg) **Glasshouse Gardens** (four times the size) | A botanist's gardens on a cliff-top plateau: a palm house, a lily house, a potting shed, and one slanting road cut up the cliff from the cove | One long **slanting road** cut into a cliff (flatten strokes stepping up, smoothed), doors walked through, a hidden lever in the lily house; Temperance's **turning mirrors** that unlock a chest together (`lib_features`) |
| ![](images/library/floe_reach.jpg) **Floe Reach** (four times the size) | An ice coast with floes on the shallows, fishing holes in the ice and a plunger that blasts the channel open | Floes as drifts **grounded** on flattened shoals (never floating), holes that the blast **hides** with the ice, a plunger that drops into place |
| ![](images/library/rig_seventeen.jpg) **Rig Seventeen** (four times the size) | An old oil rig on rusty legs among red sea stacks that always catches fire on a cold start: screechers on the derrick, three valves to shut through the flames, a lifeboat behind a fire door | A rig of radio tower storeys on legs with ladders between its decks, **fires** that hide and show (a glow, a wall), valves that each free the next, a **timed** run to the lifeboat |
| ![](images/library/keel_and_mast.jpg) **Keel & Mast** (four times the size) | A shipwright's yard at the head of a wooded bay and the Second Wind on her slipway, nearly finished - three planks, a mast spar, a sail, and a launch | A ship finished piece by piece (hidden strakes, mast, rigging and sail **shown** as they come), chocks knocked out in a rhyme's order, a **launch**: every piece of her moving together down the slip into the bay |
| ![](images/library/trade_wind_archipelago.jpg) **The Trade Wind Archipelago** (twenty times the size) | Palm islands on one vast reef, three harbour towns and a regatta course of eight buoys round them all, sailed in your own raft | A **course**: buoys hidden until the one before is rounded (each zone **shows** the next), a committee boat of foundations at sea, a stilt town on boardwalks over the water |
| ![](images/library/twin_forges.jpg) **Twin Forges** (four times the size) | Two rival smiths' forges at the ends of a high saddle, a chasm between with ladders down to a vault | A **saddle** flattened between two peaks with a building at each end, ladders down through a gap in a bridge's fence into a chasm, a contest between two forges; a Raft store on the Bellows' shoulder |
| ![](images/library/four_seasons.jpg) **Four Seasons** (seven times the size) | One island cut by four channels into four seasons - an ice spire, a spring hut, a summer reef, an autumn orchard - round a weather house in the middle | Four quarters with their own nature and tints, channels dug between them, a tower with ladders, a harvest bell, a weather house that waits for all four |
| ![](images/library/the_aqueduct.jpg) **The Aqueduct** (four times the size) | An old aqueduct striding across a valley on stretched pillars, a dry fountain square at its end and a harbour on the far coast | A deck **on piers** along a line at an angle (`push ... yaw=`), a cut along the line for its bed, a road in three legs up from the harbour, valves along the deck in order |
| ![](images/library/hollow_mountain.jpg) **Hollow Mountain** (seven times the size) | One snowy mountain the Delvers hollowed out: a gate sealed with three keystones, halls on three levels joined by lifts, a lake hall in its roots and a throne on an islet | Halls built in **notches** cut into a mountain, **lifts** (a lever-like use, `wait`, `teleport`), keystones set in order, a lake under a roof of slabs on columns, a stone guardian |
| ![](images/library/ribcage_reef.jpg) **Ribcage Reef** (seven times the size) | The bleached skeleton of a giant sea creature on a black volcanic crescent: a stilt village inside its ribs, a stage under its jaw, its skull half drowned in the bay | **Bones** of dead trees leaning in pairs, a **timed** song (four rib-chimes struck in order inside thirty breaths), a skull's chamber on the bay's floor with air in the eye, a festival that **shows** |
| ![](images/library/great_fen.jpg) **The Great Fen** (twenty times the size) | A vast wetland with seven stilt villages round a great mere and the lost town of Sevenbridge on its island, cut off behind seven raised drawbridges | **Seven drawbridges** (a raised leaf hidden, a lowered deck shown), each lowered by a different kind of task - a catch, a dive, horns in order, a fight, a delivery, a timed lock, a boss - and a bell that waits for all seven |
| ![](images/library/star_fort.jpg) **Star Fort** (seven times the size) | A star-shaped fortress on a desert table-land, its garrison gone, the island's beasts still coming over the walls bastion by bastion | **Banks** raised as strokes along a star, ramps up to each bastion, four **waves** that wait for their zones, a magazine that **takes** four keys, a great gun that calls the pack's leader; Raft's **keycard door** on a strongroom (the keycard in a chest outside the walls) and a **hatch** (`lib_features`) |
| ![](images/library/last_kingdom.jpg) **The Last Kingdom** (twenty times the size) | A whole small kingdom: a harbour town, a river with a watermill and farms, a dark forest, a lake with a drowned chapel and a castle on the hill - empty until its crown is found | A **river** dug from a lake to the sea with a ford and a toll bridge, a mill that turns when its sluice lifts, three seals gathered across the land, a coronation that **shows** banners on every tower |
| ![](images/library/clockwork_city.jpg) **The Clockwork City** (seven times the size) | A city of clockmakers on stacks above the sea: a harbour stack, the Hall of Hours on the plaza stack, gearworks on a pier, an observatory and a clock tower - joined by shuttles | **Shuttles** between levels (a lever, `wait`, a `teleport` to a zone), rooms in an undercroft, a clock tower whose hands are set in order |
| ![](images/library/starfall_crater.jpg) **Starfall Crater** (four times the size) | A crater lake round a grey-blue fallen star on its islet, closed like a clam; an observer's cradle on the rim and a sunk probe in the lake | A lake dug as a **ring** of strokes round an islet (cut down, never raised past 8 m), a **dive** for the probe's parts, a star that **shows** its glow when the cradle is fed |
| ![](images/library/lockkeepers_isle.jpg) **The Lockkeeper's Isle** | A low green island and its absent keeper: a tidy hut, a strongbox shed, a goat pen and a lookout | Featured, three quests: find the keeper's **keypad code** from his mother's letter, catch his two goats, read his log. Its main quest **brings** Lantern Spire ([16.3](#163-two-small-islands-with-many-features)) |
| ![](images/library/lantern_spire.jpg) **Lantern Spire** | A rock floating 60 m over the sea, a lantern hut and a dark beacon on top | Featured, a **flying island** reached by building up from the raft: fetch the sky keeper's oil from the ledge, light the beacon, open his chest ([16.3](#163-two-small-islands-with-many-features)) |
| ![](images/library/gannet_rock.jpg) **Gannet Rock** | A small rocky islet where the sea-birds nest, a plank jetty off its east shore; turtles and dolphins round it - no quest | A jetty on posts out into deeper water |
| ![](images/library/driftwood_key.jpg) **Driftwood Key** | A low sandy key with a few palms and a fisherman's driftwood cabin; stingrays in the shallows - no quest | A small cabin of Raft's blocks with a roof on its walls and posts |
| ![](images/library/bearback_isle.jpg) **Bearback Isle** | One great hump of rock with a cave on the north beach and a bear asleep on a hoard inside - no quest | A cave with a proper entrance and a monster guarding loot |
| ![](images/library/twin_horns.jpg) **Twin Horns** | Two horns of rock, a rope bridge hanging between their tops, ladders up the west one, a screecher on the east - no quest | A **hanging bridge** on slim posts, ladders up a cliff |
| ![](images/library/skiff_shoal.jpg) **Skiff Shoal** | A shoal that never breaks the surface: a fishing boat wrecked in the sand, pufferfish on guard - no quest | A **sunken** island with an underwater wreck |
| ![](images/library/castaway_knoll.jpg) **Castaway Knoll** | A knoll under the sea, a forest of kelp, a castaway's raft broken in two on the sand - no quest | A **sunken** island; furniture on a broken floor |
| ![](images/library/pelican_spit.jpg) **Pelican Spit** | A flat palm island with a long sand spit and an old landing stage out into deeper water - no quest | A **bridge into the water**: a landing stage on posts |
| ![](images/library/mango_dell.jpg) **Mango Dell** | A sheltered hollow with a wild mango grove, wild hives, boars and a chest behind vines - no quest | Harvestables, wild honey, a vine cache for the machete |
| ![](images/library/cormorant_skerry.jpg) **Cormorant Skerry** | A cold skerry with nesting cormorants, a few pines, a boat hauled up on the shore - no quest | Snow, nests and a treasure for the metal detector |
| ![](images/library/basalt_cay.jpg) **Basalt Cay** | A black volcanic cay, dead trees in the ash, ladders up the cliff to a lookout - no quest | Ladders on posts up a rock face |
| ![](images/library/bramblewood_isle.jpg) **Bramblewood Isle** (medium) | A wooded isle: a woodcutter's cabin, a jetty, a bear's den in a hollow rock, boars in the woods - no quest | A **hut**, a jetty and a monster den on one island |
| ![](images/library/gullwing_bay.jpg) **Gullwing Bay** (medium) | A green island round a deep bay with a fishing boat wrecked on the bottom, a jetty, a headland cave, boars - no quest | An underwater wreck, a jetty and a cave |
| ![](images/library/coyote_mesa.jpg) **Coyote Mesa** (medium) | Two desert mesas joined by a rope bridge, a hermit's cabin, hyenas on the flats - no quest | A **hanging bridge** between mesas, ladders, a cabin |
| ![](images/library/driftcloud_islet.jpg) **Driftcloud Islet** | A little green islet floating 55 m over the sea: a lean-to with a chest, a mango tree, a wild hive - no quest | A small **flying island**, reached by building up from the raft |
| ![](images/library/crowncrag_ruin.jpg) **Crowncrag Ruin** (medium) | A castle crumbled on a green crag - a stone ring, two broken towers, a plank lookout, a cellar hatch - a village of three cottages round a well below, a cave on the north shore; rats and a screecher - no quest | A ruin of Raft's stone pieces, a village and a cave on one island |
| ![](images/library/firedance_isle.jpg) **Firedance Isle** (medium) | A dark volcanic island: four thatched huts round a dancing ground and its fire pit, three tall idols and an offering stone on the hill, a cave in the rock; boars - no quest | A volcanic village and a holy place with fire baskets |
| ![](images/library/deepwatch_station.jpg) **Deepwatch Station** (medium) | An island under the sea: a sunken listening station with a pocket of air, a hatch in the sand, a wrecked submarine, a rock tunnel; puffer fish - no quest | A **sunken** island with an air pocket to breathe in |
| ![](images/library/longpier_haven.jpg) **Longpier Haven** (medium) | A harbour island: a quay with a long plank pier, the harbour master's cabin, a bridge to a rock stack with a watch hut, a boat sunk in the bay, a smugglers' cave; boars and puffer fish - no quest | A long **pier** on posts and a plank bridge over the water |
| ![](images/library/skyrope_peaks.jpg) **Skyrope Peaks** (medium) | An island floating high over the sea: two rocky peaks with a rope bridge between their tops, a cabin on the west peak, ladders up the east one; a screecher - no quest | A medium **flying island** with a hanging bridge |
| ![](images/library/mourners_deep.jpg) **Mourner's Deep** (medium) | A **sunken island**: a drowned graveyard with headstones and a fence, the gravedigger's shack with an air pocket, a fishing boat sunk beside it - no quest | Graves, an air pocket to breathe in, a wreck to search, all under the sea |
| ![](images/library/ringstone_hill.jpg) **Ringstone Hill** (medium) | A ring of standing stones on a green hill, a cave under the hill, a jetty and a sunken wreck offshore - no quest | Standing stones from Raft's own rocks; a cave entrance below a hill |
| ![](images/library/geiger_isle.jpg) **Geiger Isle** (medium) | A black volcanic isle: a fenced yard of leaking barrels with a decontamination tent, a guard's hut and a radio mast, a bunker in the rock hill with rats and roaches, hyenas, a jetty - no quest | A fence of Raft's own panels round a yard; a cave bunker behind posts |
| ![](images/library/charbone_cove.jpg) **Charbone Cove** (medium) | A jungle isle with a camp behind a palisade of stakes: tiki poles with shark jaws, spits over the fires, cages and bones, the camp's hoard in a hollow rock where hyenas den, boars, a wreck off the south shore - no quest | A camp set out of Raft's own tribal props; a hoard inside a rock |
| ![](images/library/skyhollow.jpg) **Skyhollow** (medium) | An island **flying 60 m** over the sea: a hermit's cabin on its meadow, a whole boat lifted with the rock and stuck in a hollow on its side, a rim where seabirds nest - peaceful, no quest | A closed rock cone underneath; build your way up from the raft |
| ![](images/library/glowfall_isle.jpg) **Glowfall Isle** (large) | Something fell from the sky: a scorched crater with a strange craft tipped over in it, its plates strewn along the furrow and a pool glowing green in the crater floor; thick jungle with a castaway's lean-to in the west, a cave on the south shore; warthogs, a bear in the cave, a screecher - no quest | A crash site and a glowing pool on a large jungle island |
| ![](images/library/dunecrown_isle.jpg) **Dunecrown Isle** (large) | A desert island: a great stepped pyramid with a stair up to a shrine on top, a smaller pyramid, a tomb dug into a rock hump with rats inside, an oasis with a caravan's tents, a smoking black ridge, a boat wrecked offshore; hyenas, rats, a screecher - no quest | Pyramids of stepped ground, a tomb to search, an oasis |
| ![](images/library/tidefall_reach.jpg) **Tidefall Reach** (large) | A volcanic island whose south half has sunk: a tribe's camp and hot springs by the smoking cone above the sea, and 8 m down a drowned town - house walls, a temple of columns round a statue, two wrecked boats; screechers, warthogs, stingrays - no quest | Half an island **under the sea**, with a drowned town to dive to |
| ![](images/library/vi_breakdown_bend.jpg) **Breakdown Bend** (small) | A van broken down on a sandy bend, two caravans camped beside it, hyenas at the end of the track - a quest | Raft's vans and caravans set on the sand as a camp |
| ![](images/library/vi_sledrun_halt.jpg) **Sledrun Halt** (small) | A snowmobile shed, a sled with an empty tank, a van snowed in on the shore, a fuel depot with a polar bear asleep beside it - a quest | Snow vehicles from Tallinn on a small snow island |
| ![](images/library/vi_pallet_point.jpg) **Pallet Point** (small) | A building yard on a rocky point, left mid-job: a forklift with a crate on its forks, an excavator, wagons of bricks, rats in the dumpster - a quest | Raft's building-site vehicles in a working yard |
| ![](images/library/vi_kestrel_strand.jpg) **Kestrel Strand** (small) | A small plane crashed on a desert island: a long skid of wreckage, the pilot's shade shelter, a dinghy on the beach, hyenas in the rocks - a quest | A plane wreck and its debris trail on sand |
| ![](images/library/vi_ashdig.jpg) **Ashdig** (small) | A small ore mine on a black volcanic island: an ore train of three wagons, an excavator at the cut, a light crane, screechers on the peak - a quest | Ore wagons on sleepers; a mine cut in volcanic rock |
| ![](images/library/vi_mudlock.jpg) **Mudlock** (small) | A swamp of reed pools and dead trees where a fishing boat ran aground in a storm: its crew's plastic boats, a stilt hut, nets drying - no quest | A whole boat sitting on swamp mud |
| ![](images/library/vi_countdown_cay.jpg) **Countdown Cay** (small) | A flat white cay where somebody tried to launch a rocket built from a caravan: the rocket on its pallet pad, its debris, the mission-control van and the workshop caravan - no quest | A caravan rocket on a pad of pallets |
| ![](images/library/vi_gantry_bluff.jpg) **Gantry Bluff** (small) | A high rock bluff where a salvage crew lifted wrecks from the sea: a big crane with its arm out over the water, a light crane, a forklift, an ore wagon, a ramp down to the beach - no quest | Cranes on a cliff top |
| ![](images/library/vi_pinewood_haul.jpg) **Pinewood Haul** (small) | A pine forest clearing worked by a logging crew: their van at the log pile, a bunkhouse caravan, an excavator, a wagon by the stumps - no quest | Vehicles among pines and stumps |
| ![](images/library/vi_lowtide_camp.jpg) **Lowtide Camp** (small) | A long low beach where friends drove two vans and a caravan into a circle round a fire pit, a plastic boat on the sand beside them - no quest | A ring of vehicles as a summer camp |
| ![](images/library/vi_caravan_rest.jpg) **Caravan Rest** (medium) | The last caravans of the coast in a ring round a desert well, more vans on the track, and Odo's rocket caravan on its own pad; a cave - a quest | Many caravans and vans placed as a settlement |
| ![](images/library/vi_hollowpick_quarry.jpg) **Hollowpick Quarry** (medium) | A grey quarry island: the excavator in the pit, five ore wagons down to the loading yard, a crane with a crate on its hook, a powder store behind a keypad, a lift to the blasters' ledge, a cave - a quest | A keypad and a generator that drive the quest |
| ![](images/library/vi_whitecap_depot.jpg) **Whitecap Depot** (medium) | A snowfield sled depot: the shed and sleds in the yard, a van snowed in, a bunk caravan, a power hut; out by an old igloo a lost sled and a sleeping polar bear; a cave - a quest | Snow vehicles and a whiteout story |
| ![](images/library/vi_wingfall_mire.jpg) **Wingfall Mire** (medium) | A swamp of pools, bamboo and birch where a small airliner came down on its belly; a van sunk in a deep pool, plastic boats, a hermit's blue caravan on a knoll - no quest | A belly-landed airliner in a swamp |
| ![](images/library/vi_cinderyard.jpg) **Cinderyard** (medium) | A black volcanic salvage yard: vans and a stripped caravan in rows on the cinder flat, the excavator that hauled them, a forklift by the scrap, a crane over the yard - no quest | A scrapyard of Raft's vehicles |
| ![](images/library/vi_slipway_meadow.jpg) **Slipway Meadow** (medium) | A green meadow with a boatyard: a trawler up the slipway, plastic boats on pallet trestles, a rowing boat in the grass, a forklift and cart, the boatwright's yellow caravan; a cave - no quest | Boats out of the water on trestles and a slipway |
| ![](images/library/vi_timberline_haul.jpg) **Timberline Haul** (large) | A big pine island of a timber company's last log drive: the excavator in the cut block, the logging camp and cook's caravan, the mill yard with forklifts, wagons and a haul crane, the loading crane at the landing; a cave - a quest | A whole logging operation across one island |
| ![](images/library/vi_coral_runway.jpg) **Coral Runway** (large) | A low white-sand island with a coral airstrip: the island-hopper plane, the fuel truck, baggage carts and a forklift at the hangar, the radio caravan and tower hut, a lagoon with boats at its jetty - a quest | An airstrip with its plane and ground vehicles |
| ![](images/library/vi_ridgeback_pass.jpg) **Ridgeback Pass** (large) | A high rock-and-snow island with a road over the saddle: four snow vehicles stopped on the pass, an excavator digging out a drift, a van stuck in the snow, the sled depot and the crane camp - no quest | A mountain road with a convoy |
| ![](images/library/vi_redmesa_harbour.jpg) **Red Mesa Harbour** (large) | A red desert of flat-topped mesas with an ore harbour: two cranes over the quay, forklifts, ore wagons on sleepers, a long jetty, a freighter stranded on the beach, vans and the harbour master's caravan - no quest | A harbour with cranes and a beached freighter |

---

*This guide describes Custom Islands 3.0, the Experimental Alpha Release. It is kept up to date with the mod: every
change to a feature updates this guide, the README and the PDF copy of this guide (`docs/Custom-Islands-Guide.pdf`)
together.*


### 15.8 Raft's story islands, piece by piece

What Raft's own story islands (the Radio Tower, Vasagatan, Balboa, Caravan Town, Tangaroa, Varuna Point, Temperance
and Utopia) are made of, and how to make the same on your island. From a scan of their scenes: 1,912 pieces with a
script that makes them do something (the dev command `CIStoryAudit`, left out of the release, writes the list).

| On Raft's islands | On your island |
|---|---|
| Things to pick up, harvest, cut and dig (460 pickups) | **Things to gather** (chapter 4), Raft's own, working as there - with Raft's **wild beehive** (Balboa, Caravan Town), its **dirt spots**, Tangaroa's banana trees, trees and strawberries, Caravan Town's acacias, Balboa's mushrooms and the finds on the story islands' land; a snowy pine to cut |
| Quest items (keys, keycards, tokens, tapes, parts: 319 pickups) | Raft's **quest item pickups** in the object list (42 of them: search "QuestItemPickup"); picked up with the interact key, the crew gets Raft's quest item as a story item (`story:raft-<type>`, Raft's name and picture), for checks and chests on any island of the world |
| Notes and notebook pages (68) | Notes ([5.2](#52-notes-and-signs)), the journal and Raft's notebook ([6.5](#65-your-islands-in-rafts-story-the-receiver)) |
| Buttons, levers, crank wheels, hatches, doors that open with a key or keycard (about 150 interactables) | Any object with **Behaviour & events**: players can use it, *only if* the player has an item (kept, or used up like a key), then open, move, show, hide, signal... Ready to place: Raft's **keycard and key doors** (Tangaroa's plantation door wants Raft's Tangaroa keycard, Utopia's entrance and hut doors their keys, Varuna's garage door the Motherlode key, Temperance's reactor door the reactor key, a padlocked door the bolt cutters, Detto's door the code) open (go) for a crew that holds that quest item and keep it for the next door (or use it up: **Card used up** in the inspector's Keycard door settings); **hatches** open when used; **crank wheels** and **levers** send the signals `crank` and `lever` that other objects, the quest and the world plan can wait for |
| Animations, lights, sounds, particles, screen shake when something is used | Behaviours (move, turn, show/hide lamps), sound and atmosphere zones |
| Trigger boxes | Trigger zones |
| Enemy spawners that keep coming until a quest step (30) | Creature spots that come back, or a show action (an ambush) |
| Elevators and lifts (23) | Ready **lifts**: Tangaroa's elevator and Varuna Point's skylifts go up 6 m when used and down on the next use, carrying the player standing on them (Behaviour: move, "Carries players" - any moving object can carry players); or the action **teleport to** |
| Generators, the radio and its battery, fuel and engine parts | Ready **generators** (Balboa's, Tangaroa's, Utopia's, Vasagatan's) start with Raft's generator part (used up) and send `power`; **radios** (the radio tower's, Balboa's) work with power - a started generator or Raft's battery charger part - and send `radio`; Vasagatan's **engine** starts with Raft's gas tank and sends `engine`. Started once, they run for good. The signals open what you like (show a cache, a quest step) |
| Cages, security cameras | Ready **cages** (Utopia's dog cage, the radio tower's shark cage) open (go) for a crew with Raft's bolt cutters, sending `cage`; the radio tower's **camera** sweeps back and forth |
| Zipline lines (7) | Raft's **zipline lines**, ridden with the zipline tool, their far end set in the object panel |
| The machete's vines (Balboa) | Raft's **choppable vines**, cut with the machete (kept for the next vines) |
| Treasure for the metal detector and the shovel (Caravan Town, 175 dig piles) | **Buried treasure** (Raft's own treasure points) |
| Bosses and their arenas | Creature spots with a bigger, tougher, tinted boss; a sound zone for its music; Utopia's boss room doors as scenery a defeat event can hide. A **boss arena** as the boss map type builds it: a zone whose "walk in" shows a ring of hidden stakes (only if the signal `boss` wasn't sent; a **wait** of 120 s then hides them again; the zone fires every time and also checks the stakes are hidden, so a lost fight re-arms it), the boss spot's "defeated" hides them, shows the hidden spoils chest and sends `boss`. Raft's own arenas (Olof's phases, the hyena boss) run on its story characters and are not on custom islands |
| The Receiver's frequencies and the story chain | World plans and Raft's story chain ([7](#7-world-plans-which-islands-a-world-gets), [6.5](#65-your-islands-in-rafts-story-the-receiver)) |
| Character unlocks | The action **unlock character** (a name such as Rouhi, or a number): the player gets that character as Raft's own story does - kept in their Raft profile, for every world |
| Cooking recipe pickups, mystery packages | Not on custom islands: they only work inside Raft's own islands (their scripts belong to its story save) |
| Keypads with a code (Tangaroa, Vasagatan) | A **keypad code** on any usable object ([6.2](#62-behaviour-and-events)) |
| Puzzle mini-games: Temperance's laser mirrors and igloo wires, Utopia's pipes, water wheels and justice scales, Tangaroa's claw crane | Ready puzzle pieces, rebuilt as the mod's own (Raft's puzzle scripts belong to its story scenes): **turning mirrors** (Temperance's), Utopia's **water wheels** and **pipes** (a straight, a big curve, the radio tower's bend) turn a quarter each use and send `mirror`, `wheel` or `pipe` - checks on their state (open = turned) make the puzzle (a chest that opens once the right ones are turned); Utopia's **wire connectors** take Raft's electrical cable (used up, once - after that they stay connected) and send `wire`; Tangaroa's **claw crane console** sends `claw`; the **justice scales** send `scales`. Or build a puzzle as a **sequence**: levers or wheels that send signals in the right order (The Clockwork Orchard, Old Vine Hill). **Laser beams**, the mod's own: Temperance's **laser emitter** shoots a red beam along its front; the turning mirrors and the **fixed mirror** send it on along their own front (turn a mirror, the beam turns); the first other object it reaches gets the event "a laser beam reaches it" - the **laser door panel** says so and sends `laser`. Any object can answer the beam (Behaviours, "When"): a door that opens when the beam is lined up |


### 15.9 A whole storyline: The Rainmakers

**The Rainmakers** is a plan of nine story islands in the library, made like Raft's own story: Raft's story is off and
the plan's own chain takes its place on the Receiver. Nimbus Works sold rain, and then the rain didn't stop. Each
station's logbook gives the frequency of the next, and the trail ends at the Rain Engine, the machine that still seeds
the whole sky. Each island has notes from the staff, a survivor or a family story, a blueprint cache (Raft's
blueprints in Raft's order; the quest has a **Blueprint** step for each, so taking them is part of it) and a frequency
for the next island. The Sleet Kilns and The Isobar Club each have a **Raft store**: Raft's goods, and any story
blueprint a player missed. At the end every player sees the credits and a closing note. Its main islands are about 70% the size of Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/hailstone_weir.jpg) **Hailstone Weir** (first, 500) | A forested island whose river mouth is closed by a concrete weir with three sluice gates, the pump house on its bank | A plan's first rule (`first`), Utopia's drawbridges set upright as sluice gates, a crank that drains the pump house, the next frequency on a maintenance card |
| ![](images/library/cirrus_silo_farm.jpg) **Cirrus Silo Farm** (800) | Six silos of seeding salt on grassland in the drizzle, a farmhouse, goats loose from their pen | Silos of Temperance's barrels at silo size, goats to **catch**, a silo hatch that opens with a key from a drawer, the farmer freed from a cage |
| ![](images/library/the_kite_meadow.jpg) **The Kite Meadow** (900) | A high meadow over sea cliffs: twelve winch masts with torn kites between them, the crew hut, the launch shed | Caravan Town's banners as kites, vines cut with the machete, a keypad whose digits come from three logs, a character (Rouhi) who joins the crew |
| ![](images/library/the_fogworks.jpg) **The Fogworks** (1000) | A fog factory town: boardwalks across a shallow bay, cottages on stilts, two hourglass cooling towers, the pump hall and the control room | Towers of Varuna's pillars (one upside down on the other), fog off the towers, valves and a cable hub, a safe behind a keypad |
| ![](images/library/vaporhold_reservoir.jpg) **Vaporhold Reservoir** (1000) | An embankment round a deep reservoir lake, the valve tower in the lake, the pump hall drowned on its floor | Utopia's glass bridge, a case dug up with the metal detector and shovel, a valve wheel that opens a flood gate under water, an air pocket in the rocks |
| ![](images/library/thunderhead_array.jpg) **Thunderhead Array** (1100) | A basalt plateau with a grid of forty lightning rods, the generator hall and the control bunker | Mist and a rain sound for a storm that comes in, wires from rod to rod, three generators and a grid board, a keycard door, the engineer in a cage, a zipline down to the shore |
| ![](images/library/the_sleet_kilns.jpg) **The Sleet Kilns** (1100) | Five bottle kilns of seeding salt on a black-sand volcano shore, a tramway up from the landing | Tangaroa's vases as kilns, salt sacks weighed on scales, a kiln door that is a chest, hyenas on the way out, a Raft store by the tramway |
| ![](images/library/the_isobar_club.jpg) **The Isobar Club** (1200) | The company's members' club on a granite shelf: a pavilion on the upper shelf, tidal pools and a wine cellar below | Tangaroa's facade panels as one low pavilion, Varuna's scissor lift as a funicular, a safe whose code is in the board minutes, Dagny Holm found in the cellar, a Raft store on the upper shelf; Raft's vending machine |
| ![](images/library/the_rain_engine.jpg) **The Rain Engine** (1400, the finale) | The seeding machine on a mountain island: four arms round a storm crown, the drum hall and the Heart | A 30-step quest over four areas: a gatehouse key in the salt caves, a lift up the mountain, four arm devices that each **signal** a master board, ziplines between the arms, a **laser** to turn with three mirrors, spinning drums stopped by a lever, a polar-bear boss that summons bears, and a master switch that hides the storm and shows a clear sky |

To learn from it: open the plan with **EDITOR** > **A world plan** and look at its rules (`first`, then `after:` the
one before, each on a Receiver frequency); open any of its islands in the editor and look at the behaviours on the
quest objects. Its quests are long like Raft's: 5 to 7 steps on the small stations, 10 to 13 on the large ones and 30
on the Rain Engine.

### 15.10 A whole storyline: The Last Post

**The Last Post** is a second plan of nine story islands in the library, made like Raft's own story (Raft's story off,
the plan's own chain on the Receiver, 500 to 1400). The Coastal Post was the last postal service on the sea. At its
first stop, the Dead Letter Office, a letter waits that is addressed to your raft, with five dead letters in the
pigeon-holes. Each dead letter is delivered on a later island (a `take` check on a letterbox, locker or pigeonhole) and
its answer points on down the route, to the General Post Office, where the postmistress Ada Lind waits with the last
letter. Each island has Raft's blueprints in Raft's order (a **Blueprint** step for each), Pneumatic Heights comes only
from day 10 on (`day:10`), and the later islands have a **Raft store**; the General Post Office also has Raft's
vending machine. A side island, **The Wandering Sorting Barge**, comes ahead of the raft every 6 km until its quest is
done (`km:6:again`). Its main islands are about 60% the size of Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/dead_letter_office.jpg) **The Dead Letter Office** (first, 500) | A brick post office on a granite skerry, its clock stopped at nine | Done when the letter to your raft is read (`note:1249`), five dead letters as story items carried to later islands |
| ![](images/library/pillarbox_pier.jpg) **Pillarbox Pier** (800) | A long pier with the ferry office and its sorting shed | Done when a zone is reached (`zone:sorting`), a dead letter posted with a `take` check |
| ![](images/library/parcel_canopy.jpg) **Parcel Canopy** (900) | A jungle airstrip with the mail pilot's plane in the trees | Vines cut with the machete, a beacon whose **signal** finishes the island (`signal:beacon`) |
| ![](images/library/pneumatic_heights.jpg) **Pneumatic Heights** (1000) | A terraced tube-post town round its exchange tower | Comes on day 10 (`day:10`), done after step 16 (`step:16`), valves, tubes and capsules, rats from the cellar |
| ![](images/library/sorting_rock.jpg) **Sorting Rock** (1000) | A sorting hall in a quarried bowl, five parcel scales | A buried safe found with the metal detector, scales that each give a story item |
| ![](images/library/telegraph_narrows.jpg) **Telegraph Narrows** (1100) | Two headlands and eight sea stacks, a telegraph pole and a zipline on each | Ziplines downhill stack to stack, a generator and a cable hub, a keypad code in Morse, a telegraphist in a cage |
| ![](images/library/franking_works.jpg) **The Franking Works** (1100) | A stamp factory: works hall, presses, ink tanks, chimney | Done on landing (`visit`), Tangaroa's tokens as rare stamps (`take` 3), a Raft store by the yard |
| ![](images/library/adas_round.jpg) **Ada's Round** (1200) | The postmistress's homestead on a green hill | A dovecote that shows the key box, Vasagatan's padlock door opened with a story key, a Raft store |
| ![](images/library/general_post_office.jpg) **The General Post Office** (1400, the finale) | A white colonnade, a clock tower and the sorting hall on a half-drowned square, the archive and the roof above | A 26-step quest over four areas: a crank that unbolts the doors, a pneumatic router and a vault keypad, two scissor lifts, a claw, a brood mother boss, a character (Ada) who joins the crew, a camera moment at the flag, a Raft store and a vending machine |
| ![](images/library/wandering_sorting_barge.jpg) **The Wandering Sorting Barge** (side, every 6 km) | A mail barge aground on a sandbar | A side rule that comes back until done (`km:6:again`) |

To learn from it: open the plan with **EDITOR** > **A world plan** and compare its "done when" parts (`note:`, `zone:`,
`signal:`, `step:`, `visit`, `quest`); then open The Dead Letter Office and a later island side by side to see how a
story item from one island opens a check on another.

### 15.11 A whole storyline: Relic of the Seventh Tide

**Relic of the Seventh Tide** is a plan of nine islands woven into Raft's own story (Raft's story on, the plan's
islands come after Raft's, 600 to 1300 on the Receiver). Professor Kett hunts the Tide Engine, the machine the sea
kings built to raise the sea, and the Syndicate is always one island ahead. Three gears from three islands open the
stone giant in the strait, and the giant points to the Engine. Sunstaff Mesa comes **instead of** Balboa
(`instead:Balboa`) and holds Balboa's four blueprints as **Blueprint** steps; the other islands come after a Raft
island (`after:RadioTower`, `after:CaravanTown` ...), so Raft's own blueprints stay where they are. Skyhook Mast has
Raft's vending machine and Tangaroa tokens to find; the Colossus and the Tide Engine have a **Raft store**. Two side
islands come ahead of the raft by distance and by day (`km:8`, `day:5`). Its main islands are about 80% the size of Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/marrow_key_museum.jpg) **Marrow Key Museum** (first, 600, after the Radio Tower) | A small sandstone museum on a coral key, its glass cases smashed | A looted museum told by its notes, a safe whose code hides on an exhibit card, Kett's field journal |
| ![](images/library/sunstaff_mesa.jpg) **Sunstaff Mesa** (800, instead of Balboa) | A red sandstone mesa split by a slot canyon, a map room on top | Vines that block the stair till cut, three turning mirrors that bring a sunbeam to a panel, Balboa's blueprints as steps |
| ![](images/library/sandsunk_necropolis.jpg) **Sandsunk Necropolis** (900, after Caravan Town) | Dunes over a city of the dead, the great tomb of the sea kings | A seal found with the metal detector and shovel, scarabs in the dark, a gate that drops after its lever, the first gear |
| ![](images/library/skyhook_mast.jpg) **Skyhook Mast** (900, after Tangaroa) | A steel mooring mast on a sea stack, Kett's airship wrecked below | A lift up the stack, screechers on the mast, a zipline down to the wreck, Raft's vending machine and Tangaroa tokens, done after step 5 (`step:5`) |
| ![](images/library/the_hollow_fang.jpg) **The Hollow Fang** (1000, after Varuna Point) | A rock spire with three trials inside | Spinning blades, lettered stones that throw you back (zones with `teleport`), an invisible bridge shown by thrown sand |
| ![](images/library/colossus_of_the_strait.jpg) **Colossus of the Strait** (1100, after Temperance) | A stone giant standing across a strait between two headlands | A keycard door and a lift up the leg, the three gears in a wheel that lights the giant's eyes, a Raft store |
| ![](images/library/the_tide_engine.jpg) **The Tide Engine** (1300, the finale) | A ring of desert rock round a vast well with the Engine's heart | A 28-step quest: tide wheels, a sluice, a zipline, Kett in a cage, a boss's beast, a beam through two mirrors, a Raft store |
| ![](images/library/dredgers_bank.jpg) **Dredgers Bank** (side, after 8 km) | The Syndicate's dredging rig on a sandbank | A safe whose code is in the manifest, a ladder up the rig, a side quest by distance (`km:8`) |
| ![](images/library/rope_bridge_gorge.jpg) **Rope Bridge Gorge** (side, from day 5) | A jungle island split by a gorge, a rope bridge to a temple porch | Doll idols with lanterns, the temple's warthogs, a side quest seen ahead of the raft from day 5 (`day:5`) |

To learn from it: open the plan with **EDITOR** > **A world plan** and look at its `after:` and `instead:` parts, which
fit a storyline into Raft's own; then open Sandsunk Necropolis, Skyhook Mast and The Hollow Fang to see how a story item
from each opens the gear wheel on the Colossus.

### 15.12 A whole storyline: The Lullaby Frequency

**The Lullaby Frequency** is a horror plan of nine islands beside Raft's own story (Raft's story on, the plan's chain
on the Receiver, 500 to 1200). At 3:12 every night the Receiver hums a lullaby by itself. It leads from a cannery to a
sanatorium, a shoal of effigies that turn to watch you, terraces where the statues stand closer each time you look up,
a causeway in the mist, a silent choir on a seabird cliff and a drowned children's dormitory. The end is a quarry of
five listening mirrors. The islands show how far the rule kinds go in one plan: a quest (`quest:cannery`), a day
(`day:8`), a side island that comes back every second day (`day:2:again`, `ahead:400`), a zone on an earlier island
(`zone:effigy:circle`), a quest step (`step:terraces:4`), a landing (`visit:causeway`) and notes read (`note:3`).
Raft's story is on, so Raft's blueprints stay on Raft's islands and the plan has no **Blueprint** steps. Saint Agnes has
Raft's vending machine and Tangaroa tokens; Gannet Cliff and The Listening Mirrors have a **Raft store**. Its main islands are about 60% the size of Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/hollins_cannery.jpg) **Hollins Cannery** (first, 500) | A fish cannery on a rocky shore, its slipway and yard | A small quest; the first lullaby on the hall's radio |
| ![](images/library/saint_agnes_sanatorium.jpg) **Saint Agnes Sanatorium** (800) | Wards, a day room and a cot veranda on a hill terrace | A locked ward, the hydrotherapy house, Raft's vending machine by the gate |
| ![](images/library/effigy_shoals.jpg) **Effigy Shoals** (day 8, 700) | Sandbars of scarecrow effigies | Done when three notes are read (`note:3`); a second set of effigies, shown later, faces the landing |
| ![](images/library/the_returning_islet.jpg) **The Returning Islet** (side, every 2nd day) | One palm and a hut on stilts with a candle lit | Comes back ahead of the raft (`day:2:again`); a timed `wait` step |
| ![](images/library/stillwater_terraces.jpg) **Stillwater Terraces** (900) | Flooded rice terraces with a shrine at the top | Three sets of statues, one shown at a time (`show`/`hide` on zones); three Bell stones set in a stand open the stair |
| ![](images/library/mistlock_causeway.jpg) **Mistlock Causeway** (900) | Plank decks on posts across a misty lagoon, two forks | A safe branch told by sound (`Zone_Sound`); the wrong one sends you back; a timed gate lever |
| ![](images/library/gannet_cliff.jpg) **Gannet Cliff** (1000) | A commune of tents on a cliff, a stone-lined listening pit, dishes in the ground | A gate that asks for a story item from Effigy Shoals (`if.use=has`), a deserter who joins the crew, a Raft store |
| ![](images/library/cradle_reef.jpg) **Cradle Reef** (1000) | A buoy at sea; deep below it a drowned dormitory of cribs | A dive to a sunken building; a music box whose song is a story item |
| ![](images/library/the_listening_mirrors.jpg) **The Listening Mirrors** (1200, the finale) | A quarry with five antenna mirrors round a pit | A 31-step quest over four areas: a generator and a keypad hut, a chapel, a cage and a bell, a crane and its claw, a boss in the pit, a Raft store |

To learn from it: open the plan with **EDITOR** > **A world plan** and compare its "when" parts (`quest:`, `day:`,
`zone:`, `step:`, `visit:`); then open Stillwater Terraces to see how zones show and hide sets of statues as you climb.

### 15.13 A whole storyline: The Gravity Sea

**The Gravity Sea** is a plan of twelve islands in place of Raft's own story (Raft's story off, no random islands,
the chain on the Receiver, 500 to 1300). The Halvard Institute's Lodestone Array tore islands out of the rising sea
and left their roots on the seabed. Each sky island brings its sunken half: you climb to an orchard 30 m up and dive
to its cellar 25 m down, cross a market on a rock plate and dig through the stalls it dropped on the sea floor. The
end is the Array's crown, 35 m up, and its root, 35 m under it. The islands show flying and sunken islands side by
side (`elevation`), air pockets under water, and every kind of "when": a quest (`quest:quay`), a signal from a winch
(`signal:orchard:roots`), a quest step (`step:9`), a zone (`zone:keel`, `zone:array:core`), a landing
(`visit:gravewell`), a distance (`km:5`) and a twin found near another island (`near:shoal:600:north`). Raft's story
is off, so all of Raft's story blueprints are **Blueprint** steps on the way. Altitude Market has Raft's vending
machine; Pendulum Isle, The Stair of Hours and The Lodestone Array have a **Raft store**. Its main islands are about 40% the size of Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/lodestone_quay.jpg) **Lodestone Quay** (first, 500) | A stone quay, its crane and compass needles; the hill behind it cut flat | A short quest; the Receiver and antenna plans |
| ![](images/library/upfall_orchard.jpg) **Upfall Orchard** (700, 30 m up) | An apple orchard and a farmhouse on a flying island | A well winch whose `signal` brings the island under it |
| ![](images/library/rootcellar_deep.jpg) **Rootcellar Deep** (600, 25 m down) | The orchard's drowned half: the farmhouse, the well's bottom, the roots | Air pockets (`zone.air`) to breathe in; a hatch into the cellar |
| ![](images/library/altitude_market.jpg) **Altitude Market** (900, 35 m up) | A market town on a rock plate, a clock tower on stilts | Choppable vines, a keypad, a zipline down, Raft's vending machine |
| ![](images/library/sinkmarket_floor.jpg) **Sinkmarket Floor** (700, 30 m down) | The market's lower town, a diving bell, a flooded cistern | 14 steps under water, air pockets in a bell and a cistern |
| ![](images/library/pendulum_isle.jpg) **Pendulum Isle** (1000, 12 m up) | A long island low over the sea, a pendulum hall | Sail under it; a shaft to its keel (`zone:keel`), a Raft store |
| ![](images/library/gravewell_shaft.jpg) **Gravewell Shaft** (900, 45 m down) | A well shaft cut into the seabed, a pump hall at its bottom | A spiral stair down the shaft, galleries with air, anglers in the dark |
| ![](images/library/the_stair_of_hours.jpg) **The Stair of Hours** (1100, 30 m up) | Terraces that make a giant sundial, an observatory | Hour stones walked in turn, a telescope aimed at the Array, a Raft store |
| ![](images/library/the_lodestone_array.jpg) **The Lodestone Array** (1300, 35 m up, finale part 1) | Four magnet arms round a core platform | A 22-step quest: ziplines between the arms, a glass bridge that breaks, a beam through two mirrors, a Raft store |
| ![](images/library/the_array_root.jpg) **The Array Root** (800, 35 m down, finale part 2) | The Array's foundation: four pylons round a pit | Comes when the crown is dark (`zone:array:core`); 12 steps: the root's guardian and its cutoff |
| ![](images/library/weatherglass_shoal.jpg) **Weatherglass Shoal** (side, km 5) | A weather station on a sandbank | Turns up ahead of the raft (`km:5`, `ahead:450`) |
| ![](images/library/weatherglass_deep.jpg) **Weatherglass Deep** (side) | Its drowned twin, north of it | Placed near another island (`near:shoal:600:north`) |

To learn from it: open Upfall Orchard and Rootcellar Deep side by side to see how a flying island and a sunken one are
set up (**Elevation** in the island settings), then open the plan and follow its signals and zones.

---

### 15.14 A whole storyline: The Last Green World

**The Last Green World** is a plan of eleven islands in place of Raft's own story (Raft's story off, no random islands,
the chain on the Receiver, 400 to 1200). Verdance is the last land where things still grow. The Seedkeepers grew a
living organism in its roots to green the drowned world, and lost control of it. Ranger Oda Fenn's logs lead you from
a pollen spit up a river delta, through a grove of giant trees and the caves under it, across a poisoned marsh and up a
stair of roots to the Heart of Green. There you turn one valve, **BURN** or **SPREAD**, and the Rootcore under the tree
is finished the same way. The islands show every kind of "when": a quest (`quest:spit`), a landing (`visit:landing`), a
quest step (`step:delta:6`), a zone (`zone:grove:roots`), a signal (`signal:heart:below`), a rule (`rule:mire`), a
day (`day:6`) and a frequency that needs the engine (`receiver:1000:needs=engine`). Raft's story is off, so all of Raft's
story blueprints are **Blueprint** steps on the way. Hollowtrunk Grove has Raft's vending machine; Canopy Stair and The
Heart of Green have a **Raft store**. Titanium lies buried on Blightmire, Canopy Stair, the Heart and the Rootcore. Its quest islands are as big as Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/pollenreach_spit.jpg) **Pollenreach Spit** (first, 500) | A sand spit in drifting pollen, a ranger beacon | A short quest; the Receiver and antenna plans |
| ![](images/library/ranger_landing.jpg) **Ranger Landing** (700) | A ranger station on stilts, hens under it | Ranger logs as notes; the motor wheel and steering plans |
| ![](images/library/riverfork_delta.jpg) **Riverfork Delta** (800) | River arms cut into the land, an outpost, a ferry crank | Arriving at the landing brings it (`visit:landing`); boars upriver |
| ![](images/library/trackers_rise.jpg) **Tracker's Rise** (700, a meadow 6 m up) | A tracker's tower, goats and llamas, a gully | A zipline from the tower; the goat Burr in a log cage |
| ![](images/library/hollowtrunk_grove.jpg) **Hollowtrunk Grove** (900) | Five giant firs and birches, a greenhouse, a water tower | Raft's vending machine; walking into the roots (`zone:roots`) brings the caves |
| ![](images/library/rootweave_caves.jpg) **Rootweave Caves** (600) | A rock hill with four of Raft's caves: fungus hall, bee hall, flooded passage, den | Air pockets, wild hives, a mama bear |
| ![](images/library/blightmire.jpg) **Blightmire** (1000, needs the engine) | A poisoned marsh, three purple roots | Roots burnt in order, chained by signals; `signal:cleansed` ends it |
| ![](images/library/canopy_stair.jpg) **Canopy Stair** (900, a shelf 10 m up) | Giant roots up a slope, the Seedkeepers' last camp | A Raft store; a zipline pointing at the Heart |
| ![](images/library/heart_of_green.jpg) **The Heart of Green** (1200, finale part 1) | A giant fir on a plateau, three balconies, a crown terrace | 23 steps: a generator, a spore press, a keypad, the BURN / SPREAD valves (`!signal|chosen`) |
| ![](images/library/the_rootcore.jpg) **The Rootcore** (400, finale part 2) | Raft's bear cave as the dome under the Heart | The Blightmother boss and her brood; core valves that check the Heart's choice (`has|story:burnway`) |
| ![](images/library/thornback_ridge.jpg) **Thornback Ridge** (side, day 6) | A thorny ridge, a hermit's camp, a mango grove | Turns up ahead of the raft on day 6 (`day:6`, `ahead:500`) |

To learn from it: open The Heart of Green and look at the two valves' behaviours - each refuses once `chosen` is sent,
and each gives its own story item, which the Rootcore's valves check (signals stay on their island; story items travel
with the crew).

### 15.15 A whole storyline: The Frozen Horizon

**The Frozen Horizon** is a plan of eleven islands in place of Raft's own story (Raft's story off, no random islands,
the chain on the Receiver, 300 to 1100). A sea gone white in the north: fragments of a call lead you from a relay mast
across the floes to a harbour warmed by a vent, a sledge camp, an icebreaker split in two by the pack, a city of frozen
whaling ships and a glacier with a sign cut in its face. Under the glacier lies the Coldhold, a seed bank with three
switches - **KEEP**, **CARRY** or **BROADCAST** - and, thirty metres down, the flooded Seed Ark it opens. The islands
show every kind of "when": a quest (`quest:stack`), a signal (`signal:leads:rescued`), a rule (`rule:sledge`), a zone
(`zone:bow:hatch`), a quest step (`step:decks:10`), a landing (`visit:mouth`), a day (`day:9:again`) and a frequency
that needs the engine (`receiver:1100:needs=engine`). All of Raft's story blueprints are **Blueprint** steps on the way.
The Iron Whale's bow has Raft's vending machine; Glacier Mouth and The Coldhold Vault have a **Raft store**. Titanium
lies buried on Saga Drift, Glacier Mouth and in the Coldhold. Its quest islands are as big as Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/signalstack_rock.jpg) **Signalstack Rock** (first, 500) | A basalt stack, a relay hut, a bent mast | A short quest; the Receiver and antenna plans; a path cut up the stack |
| ![](images/library/the_floelane.jpg) **The Floelane** (700) | Five floes, a crushed sealer, her crew caged on the ice | A winch shows an ice bridge for 40 s (`show`, `wait`, `hide`); `signal:rescued` brings the next |
| ![](images/library/steamvent_harbour.jpg) **Steamvent Harbour** (800) | Houses round a steaming vent, a greenhouse grown shut | A stuck valve; fruit trees on the warm ground |
| ![](images/library/orrins_sledge_camp.jpg) **Orrin's Sledge Camp** (900) | A fodder barn, goats and llamas, a lookout | Polar bears that wait for the barn (`creature.zone`) |
| ![](images/library/iron_whale_bow.jpg) **The Iron Whale - Bow** (1100, needs the engine) | An icebreaker's bow on a pressure ridge | Raft's vending machine, a zipline from the mast; the cargo hatch (`zone:hatch`) brings the stern |
| ![](images/library/iron_whale_decks.jpg) **The Iron Whale - Engine Decks** (300) | Her stern, sunk 15 m deeper than normal | Air pockets, anglers, a keycard door, the captain in a cage |
| ![](images/library/saga_drift.jpg) **Saga Drift** (900) | Nine whaling ships lashed into a city | Screechers on the masts, rats in the holds, buried titanium |
| ![](images/library/glacier_mouth.jpg) **Glacier Mouth** (1000) | A glacier front, a depot, the Ringwave cut in the ice | A Raft store; landing here (`visit:mouth`) brings the vault |
| ![](images/library/coldhold_vault.jpg) **The Coldhold Vault** (700, finale part 1) | A seed bank under an ice cap | 24 steps: a crevasse zipline, a keypad (a wrong code first), three canisters, KEEP / CARRY / BROADCAST |
| ![](images/library/seed_ark.jpg) **The Seed Ark** (400, finale part 2) | Pressure rooms 30 m down, racks of seeds | Air pockets in every room, an ice gate on a timer, the ark valve that lights the racks |
| ![](images/library/squall_shelf.jpg) **Squall Shelf** (side, day 9 and every 9 days) | A tent on a low ice shelf | A llama to catch, a bear that wakes at the tent (`day:9:again`, `ahead:600`) |

To learn from it: open The Coldhold Vault and look at the three switches' behaviours - each refuses once the choice is
made, and each shows its own verdict note; the flood door's signal (`signal:coldhold:flooddoor`) is what brings the Ark.

### 15.16 A whole storyline: The City That Never Sank

**The City That Never Sank** is a plan of eleven islands in place of Raft's own story (Raft's story off, no random
islands, the chain on the Receiver, 250 to 1000). Highwater is a city living on the top floors of a drowned metropolis.
Three powers run it - the Harbor Authority (the water), the Free Fleet (the hulks) and the Salvagers' Union (the cranes) -
and each hides a secret about where the water goes. You land at customs, trade on Raft Row, take jobs from all three, dive
the drowned high street, find the evidence in the cistern, see the hungry Rookery, and then the council meets on the
Bridgeworks Crown in a storm. There you decide: **Authority**, **Fleet**, **Union**, **expose** them all, or **free** the
Rookery. The storm cracks the piles under the city, and only the FREE choice brings the epilogue shoal. The islands show
every kind of "when": a quest (`quest:customs`), a quest step (`step:bazaar:5`), a rule (`rule:hookline`), a zone
(`zone:pumps:outfall`), a signal (`signal:cistern:evidence`, `signal:crown:free`), a frequency that needs the engine
(`receiver:900:needs=engine`) and an island turning up ahead (`ahead:700`). Every one of Raft's story blueprints is a
**Blueprint** step on the way, in Raft's order. Raft Row Bazaar has Raft's vending machine; Rookery Stacks and the
Bridgeworks Crown have a **Raft store**. Titanium lies buried on Cistern Hall, Rookery Stacks, the Crown and the Piles.
Raft's vehicles stand about the city: vans and caravans on the bazaar's key and in Lowstreet (the drowned tram),
forklifts and wagons at the Pumpcrown Works and on the Crown, ship hulks round the Fleet's moorings. Its quest islands are as big as Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/breakwater_customs.jpg) **Breakwater Customs** (first, 500) | A low key, a breakwater of slabs on drowned-tower legs, the customs house | A short quest; the Receiver and antenna plans |
| ![](images/library/raft_row_bazaar.jpg) **Raft Row Bazaar** (600) | Rafts lashed between four drowned roofs, stalls, string lights | Raft's vending machine; a van and a caravan on the key |
| ![](images/library/fleetbreak_moorings.jpg) **Fleetbreak Moorings** (700) | A drowned office block gone to jungle, a ring of moored hulks | The bosun's machete cuts the garden vines; the fuel plans |
| ![](images/library/hookline_stack.jpg) **Hookline Stack** (800) | The Union hall on a half-sunk block, three cranes, a sorting yard | A zipline from the crane; scrap, ore, clay and sand |
| ![](images/library/pumpcrown_works.jpg) **Pumpcrown Works** (900, needs the engine) | Three tower roofs joined by bridges with water pipes, a boom tower | Bees in the stores; the outfall grating (`zone:outfall`) brings Lowstreet |
| ![](images/library/lowstreet.jpg) **Lowstreet** (250) | A drowned high street 20 m down, a tram of caravans, a cinema roof | Air pockets; the lost expedition in four cages, a big stingray; the wind turbine plans |
| ![](images/library/cistern_hall.jpg) **Cistern Hall** (700) | The Authority's tower, an open cistern six metres deep | The old pump, the evidence (`signal:evidence`) |
| ![](images/library/rookery_stacks.jpg) **Rookery Stacks** (800) | Shanty huts on the lowest roofs, rope bridges, a soup kitchen | A Raft store; a zipline; the titanium tools plans |
| ![](images/library/bridgeworks_crown.jpg) **Bridgeworks Crown** (1000, finale part 1) | Four tower roofs and the council house in a storm | 26 steps: three secrets, a keypad door, five choices (`!signal|chosen`); a Raft store |
| ![](images/library/foundation_piles.jpg) **The Foundation Piles** (400, finale part 2) | Giant stone piles in a basin 10 m down, two cracked | Shoring jacks; the pile stingray in two phases |
| ![](images/library/freehold_shoal.jpg) **Freehold Shoal** (epilogue, ahead) | A sandy shoal, frames of new huts, the Rookery's boats | Turns up only after the FREE choice (`signal:crown:free`) |

To learn from it: open the Bridgeworks Crown and look at the five choice levers - each refuses once `chosen` is sent,
and each sends its own signal, which the plan's rules read (`signal:crown:free` brings the epilogue). A signal stays on
its island, but a plan rule can wait for it from anywhere.

### 15.17 A whole storyline: The Sunken Kingdom

**The Sunken Kingdom** is a plan of eleven islands in place of Raft's own story (Raft's story off, random islands on,
the chain on the Receiver, 500 to 1200). The sea here is clear enough to see a drowned city under it: an automated call
from the Pelagic Network leads you from a mast on coral-furred rooftops along a highway that runs down into the sea, up
a flooded lab tower, down to a glass dome 18 m under the surface, a diving-bell station, a glowing shelf, a caisson
yard and a city in the trench, 45 m down, where the last diver is held. At the Sealkeeper Spire two levers - **RESTORE**
or **SEAL** - decide the Network, and the descend lever opens The Stalker's Hollow under the trench lip, where the
choice is answered at the valve. The islands show every kind of "when": a quest (`quest:spire`), a quest step
(`step:9`, `step:24`), a signal (`signal:brinelab:drives`), a zone (`zone:thalassa:bell`), a rule (`rule:caisson`),
a frequency that needs the engine (`receiver:1000:needs=engine`) and a distance (`km:4`, `ahead:450`). All of Raft's
story blueprints are **Blueprint** steps on the way. Thalassa Mooring has Raft's vending machine; Caisson Yard and the
Sealkeeper Spire have a **Raft store**. Titanium lies buried on Thalassa Mooring, Caisson Yard and the Sealkeeper Spire. Its quest islands are as big as Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/spirecrest_roofs.jpg) **Spirecrest Roofs** (first, 500) | Drowned rooftops round a sending mast | A short quest; the Receiver and antenna plans |
| ![](images/library/overpass_shoal.jpg) **Overpass Shoal** (700) | A highway up out of the sea, over a bank, ending in mid-air | The road built with `ramp`; a toll booth on the top; Raft's vans and caravans stranded on it |
| ![](images/library/brinelab_tower.jpg) **Brinelab Tower** (800) | A flooded lab tower with three data drives | Storeys climbed by ladders; the third drive's `signal:drives` brings the dome |
| ![](images/library/biome_seven.jpg) **Biome Seven** (600) | A glass dome 18 m down with a dry garden inside | An air pocket under glass, glowing fish, the dome's rota |
| ![](images/library/thalassa_mooring.jpg) **Thalassa Mooring** (900) | A surface station on pontoons | Raft's vending machine; a diving bell that carries you 20 m down (`beh.move` with carry); buried titanium |
| ![](images/library/lumen_shelf.jpg) **Lumen Shelf** (600) | A glowing shelf edge 25 m down | An arch and a spar, air pockets along the route |
| ![](images/library/caisson_yard.jpg) **Caisson Yard** (1000, needs the engine) | A dry yard of cranes in the sea | A Raft store, crane ziplines, buried titanium; Raft's excavator and forklift |
| ![](images/library/hadal_commons.jpg) **Hadal Commons** (800) | A trench city 45 m down, lit by its own windows | Doors sealed with the Ringwave, Ines Marlowe in a cage, air decks on the way; a sunken van and caravan in the streets |
| ![](images/library/sealkeeper_spire.jpg) **Sealkeeper Spire** (1200, finale part 1) | The Network's switchboard in a storm | 24 steps: three fuses, a keypad (2817), RESTORE / SEAL, a Raft store, buried titanium |
| ![](images/library/stalkers_hollow.jpg) **The Stalker's Hollow** (500, finale part 2) | An arena 40 m down under the trench lip | 12 steps: lamp alcoves, the Stalker (a boss with `on.defeat`), the valve that answers the choice |
| ![](images/library/shuttlewreck_reef.jpg) **Shuttlewreck Reef** (side, km 4) | A buoy over a reef, a sunken shuttle | A cabin with an air pocket, luggage to search |

To learn from it: open the Sealkeeper Spire and look at the two levers - each gives a story item (`restorechoice` or
`sealchoice`), and the Hollow's valve checks it with `if.has` to choose its answer.

### 15.18 A whole storyline: The End of the Ocean

**The End of the Ocean** is a plan of eleven islands in place of Raft's own story (Raft's story off, random islands
off, the chain on the Receiver, 300 to 1000). A pulse on the Receiver, the same every day, leads you to the middle of the
sea: from a beacon on a rocky saddle through the stations of an old machine - a reef intake, three relay masts, a field
of static, an iron wall round a circle of calm, a canal of lock gates, a smelter hall and a cooling spire - to Engine
Court, where three rings of iron turn round the Concord Engine. There the Ringwave is explained, and you choose:
**reactivate**, **shut down** or **redirect** it. The court's housing opens the Command Well, 30 m down, where the choice
plays out at the engine's heart. The islands show every kind of "when": a quest (`quest:stormworks`), a quest step
(`step:breakwater:7`), a signal (`signal:triangle:fixed`, `signal:court:open`), a zone (`zone:reefmachine:cage`), a rule
(`rule:spire`), a visit (`visit:locks`), a day (`day:5`), a frequency that needs the engine (`receiver:900:needs=engine`)
and a distance (`km:3`, `km:12:again`). All of Raft's story blueprints are **Blueprint** steps on the way. The Outer
Breakwater has Raft's vending machine; the Cooling Spire and Engine Court have a **Raft store**. Titanium lies buried on
Stormworks, the Cooling Spire and Engine Court; puffer fish on the Reefmachine Atoll give explosive goo, the spire's
llamas wool, and chickens on Beacon Saddle and the Current Locks eggs. Its quest islands are as big as Raft's own big islands.

| Island | What it is | What to look at |
|---|---|---|
| ![](images/library/beacon_saddle.jpg) **Beacon Saddle** (first, 500) | Two sea stacks, a beacon tower and a radio hut repeating one pulse | A short quest; the Receiver and antenna plans |
| ![](images/library/reefmachine_atoll.jpg) **Reefmachine Atoll** (600) | A lagoon of iron frames grown over with reef: the engine's intake | A sunk service boat, a pump head with air in it, the cage zone that brings the next island |
| ![](images/library/triangulation_rocks.jpg) **Triangulation Rocks** (700) | Three rock tops, a relay mast on each | Wake the three masts; the last one sends `signal:fixed` |
| ![](images/library/static_shoals.jpg) **Static Shoals** (800) | Two sand bars, four masts strung with cable, a beached radio barge | Screechers over the bars; the barge to search |
| ![](images/library/outer_breakwater.jpg) **Outer Breakwater** (900, needs the engine, from day 5) | An iron wall across the island, storm on one side, a garden on the other | Raft's vending machine on the wall walk; a forklift and an ore wagon at the gate |
| ![](images/library/current_locks.jpg) **Current Locks** (1000) | A straight canal with three lock gates and a crank for each | Gates hidden by a crank (`beh.hidden`); a bear's den, a deep basin with anglers |
| ![](images/library/stormworks.jpg) **Stormworks** (800) | A dark rock with an iron smelter hall and a weather room on its roof | The last crew's three notes; buried titanium |
| ![](images/library/cooling_spire.jpg) **Cooling Spire** (700) | A pale four-storey tower in its own fog | Outside stairs up the storeys, a cable down from the roof, Hedda's Raft store, llamas, buried titanium |
| ![](images/library/engine_court.jpg) **Engine Court** (600, finale part 1) | A round court of three iron rings round the engine's housing | 28 steps: three ring cranks, a keypad, the choice (`reactivateway`, `shutdownway`, `redirectway`), Teo's Raft store, a forklift and caravans, buried titanium |
| ![](images/library/the_command_well.jpg) **The Command Well** (300, finale part 2) | A lift shaft 30 m down to a dry dome with three great rings | 12 steps: air on the way down, the command desk, the rings set the way the court chose |
| ![](images/library/drifting_relay_buoy.jpg) **Drifting Relay Buoy** (side, km 12, again) | A sand bar and a buoy that blinks Morse | An island that comes back every 12 km with a new message |

To learn from it: open Engine Court and look at its three choice levers - each gives a story item, and the Command Well
checks them with `if.use=has|story:...` on its three consoles: only the one for your choice works.

## 16. Learn from the islands

Every island you can open in the editor shows how it was made: the library's islands, the ready-made islands of the
generator and your own. This chapter says how to open one, how to look at the parts that don't show in the game (a
chest's loot, a door's checks, the quest, the rules that bring other islands) and which island to open to see each of
the editor's features. Two small featured islands of the library show many of them at once: **The Lockkeeper's Isle**
and **Lantern Spire** ([16.3](#163-two-small-islands-with-many-features)).

### 16.1 Opening any island in the editor

- **An island from the island library:** **ISLAND LIBRARY** → **Islands** → **Download**, then **EDITOR** → **Open**
  (top bar, Ctrl+O) and pick it. A plan's islands are there too once the plan is downloaded.
- **A ready-made island:** **EDITOR** → **Generate** → **Ready-made (with content)**, click a card and **Make**: the
  island is made from its seed and opened at once ([4.6](#46-ready-made-islands-map-types)). Each map type puts its
  chests, notes, creatures, zones and quest together in its own way.
- **Your own island:** **Open** and pick it; islands you saved, exported or imported are all in the list.
- Want to keep the original? **Save as** a new name before you change anything.

### 16.2 Looking around

1. **Fly round the island:** right-drag to look, **WASD** to fly, **F** frames the selection or the whole island
   ([4.1](#41-the-screen)). The island shows as it will in a world; on a flying or sunken island the blue plane is
   where the sea will be.
2. **Select a thing to see its settings:** click it (Objects tab). The object panel shows what it is: a chest's
   loot and whether it fills up again, a note's title and text, a creature spot's kind, count and toughness, a zone's
   id, message and radius, a sound or atmosphere zone's settings. **List...** shows every kind of object on the
   island with how many; **Select** one kind there to find it.
3. **Behaviour & events...** (in the object panel) shows what an object does: the "press E" text, the **Keypad
   code**, and for each event (use, walk in, read, open, defeat) its checks, what happens, and what happens when a
   check fails ([6.2](#62-behaviour-and-events)). Objects with behaviours are the doors, levers, bells and machines.
4. **The Island tab** has the rest: the height in the world, the name and welcome, the island's **rules** (regrow
   days, the earliest km) and the buttons for its parts:
   - **Edit quest...**: the quest's steps, reward and messages; the buttons at the top switch between the **Main
     quest** and the island's other quests ([6.1](#61-quests)).
   - **Islands it brings...**: the island's own rules - which island comes, when and where ([6.4](#64-islands-that-bring-islands)).
   - **Island events**: what happens when players arrive or the quest is done; **Story items...**: the keys, logs and
     other things the quest gives and takes ([6.3](#63-story-items-and-story-sets)).
5. **A world plan:** **EDITOR** → **A world plan** → **Open a world plan...** → pick it. Each rule card is one island: when it comes, where, and
   the message. **Check** says in words what the whole plan does ([7.5](#75-check-finding-and-fixing-problems)).
6. **See it work:** **Test** puts the island in a world in front of your raft ([4.9](#49-trying-the-island-in-a-world-test)).
   Play it, then go back and look at the setting behind what you just saw.

### 16.3 Two small islands with many features

| Island | What it shows |
|---|---|
| ![](images/library/lockkeepers_isle.jpg) **The Lockkeeper's Isle** (small) | A keeper's hut, a strongbox shed, a goat pen and a lookout on a low green island. A **keypad code lock** (the shed's power box; its use shows the hidden strongbox, which holds a story item that opens the sea chest at the lookout), **three quests on one island** (the main quest, a catch quest, a read quest), three **island rules** (regrow 3 days, comes after 8 km, levels up) and an **island the quest brings**: Lantern Spire, 400 m north, when the main quest is done |
| ![](images/library/lantern_spire.jpg) **Lantern Spire** (small) | A **flying island**: a rock 60 m over the sea with a lantern hut and a beacon on top (Island tab → Height → Flying). Nothing leads up: players build stairs or pillars from the raft. A ladder down to a ledge, a **story item** from a box (the oil), a **use** behaviour that takes it and gives another (the beacon gives the key), and a chest that only opens with that key |

![The Lockkeeper's Isle: the strongbox shed, its power box locked with a keypad code](images/learn-lockkeeper-shed.jpg)
*The Lockkeeper's Isle: the strongbox shed. Select the power box inside and open Behaviour & events: the Keypad code
field and the use event that shows the strongbox.*

### 16.4 Every feature, and an island to look at

One island each is enough to learn from; [15.5](#155-where-to-look-for) lists more examples for many of them.

| Feature | Open | Look at |
|---|---|---|
| Shaping the land with the brushes | Thornwood, The Frontier | Thornwood's gorge; The Frontier's terraces and ramps up a mesa |
| An island from the generator, as thick with things as Raft's own | Any library island | Its plants, rocks and pickups; [4.5](#45-the-island-generator) |
| **Height:** a flying island | Lantern Spire | Island tab → Height: Flying (60 m). Sunken works the same way (the blue plane shows the sea over the island); no library island is sunken |
| Things to gather (trees, bushes, flowers) and Raft's finds in the shallows | The Bell Stack, Crowfield Farm | The pines, berries and flowers on the stacks; sand, clay and scrap on the sea floor round the island |
| Buildings of Raft's pieces | The Bell Stack, Hightide Harbor | The huts' floors, walls and roofs; select one piece and **Same kind** |
| Notes and signs | The Lockkeeper's Isle | Any sign: the object panel's note title and text |
| Chests and loot (fills up again or not) | The Lockkeeper's Isle | The tack chest and the strongbox: their loot, **Fills up again** |
| Creatures | Crowfield Farm, The Lockkeeper's Isle | The goats' and hens' spots: kind and count |
| A boss (bigger, tougher, tinted) | Thornwood | The Boar King's spot: size, toughness, tint |
| Trigger zones (a message, a quest step) | The Bell Stack | The landing and the two stack zones: id, radius, message |
| An ambush | Signal Rock | A zone that brings creatures when walked into |
| Atmosphere and sound zones | The Bell Stack | The snowfall (fog, snow) and the wind |
| Air pockets for a long dive | The Abyss | The zones with **Air** in its trench |
| Things under water to dive for | Coral Observatory | Chests and finds on the sea floor |
| Quest steps: reach, read, open, collect, pages | The Bell Stack | **Edit quest...**: four steps, each with its target |
| Quest steps: kill | Thornwood | The step and the creature spot it counts |
| Quest steps: catch | Crowfield Farm, The Lockkeeper's Isle (quest 2) | The catch step; the net launcher in a chest |
| **Several quests on one island** | The Lockkeeper's Isle | **Edit quest...**: the buttons at the top switch between its three quests |
| Story items | The Bell Stack, Gullsong Bazaar | **Story items...**; the bell gives one; the bazaar's stalls take and give them (a barter chain) |
| Behaviour: something shown or hidden on use, with a check | The Bell Stack | The bell: it shows the bridge once the quest is far enough, and says something else before |
| Signals (one thing opens another) | Glacier Station | The reactor and its vault |
| Moving the player (teleport to) | The Frontier | The Corps' lift |
| **Keypad code lock** | The Lockkeeper's Isle | The shed's power box: **Behaviour & events...** → Keypad code |
| Island events (a message when players arrive) | The Lockkeeper's Isle | **Island events** |
| **Island rules:** regrow days, comes after km | The Lockkeeper's Isle | Island tab → Rules |
| **An island the quest brings** | The Lockkeeper's Isle | **Edit quest...** → *When the quest is done, bring a new island*: the saved island Lantern Spire, 400 m north |
| A chain of islands that bring each other | The Abyss Expedition (a plan) | Each island's **Islands it brings...** |
| Raft's zipline | Cablecar Stacks | The line from the summit and its far end |
| Machete vines over a cache | Thornwood | The vines and the chest they hide |
| Buried treasure (metal detector, shovel) | Gilded Skull Cove | The treasure spots and the detector in the hoard |
| Dirt spots (the shovel) | Crowfield Farm | The dirt by the fields |
| Wild beehives | Bramblehive Knoll | The hives on the terraces |
| Raft's keycard door and a hatch | Star Fort | The strongroom door and its keycard; the hatch on the parade ground |
| Raft's crank wheel and lever | The Clockwork Orchard | Each sends a signal |
| Raft's lift | Rookery Cliffs | The egg hoist |
| A cage (bolt cutters) and a security camera | Primeval Park | The caged feed chest; the camera on the keeper's hut |
| A generator, a radio and an engine | The Iron Graveyard | The generator part in the toolbox, the radio, the trawler's engine |
| Turning mirrors | Glasshouse Gardens | The two sun mirrors and the seed chest |
| A laser to aim | Crater Lair | The roof laser and the hidden safe |
| Water wheels, pipes | The Aqueduct | Three wheels and a hint tablet; the pipes |
| Wire hubs | Albatross Field | The hubs to cable in order |
| A claw machine | Big Top Atoll | The claw and its prize |
| Scales to weigh | Gullsong Bazaar | The scales at a stall |
| Raft's quest item pickups | Tiki Lagoon | The pickup and the story item it gives |
| A raft or deck of Raft's foundations | Tide Farm | The deck |
| Blueprints in loot | Crowfield Farm | The basket in the farmhouse |
| An island in Raft's story (the Receiver) | The Long Voyage, Raft Remade (plans) | Their rule cards ([15.4](#154-the-world-plans)) |
| A whole world plan | The Long Voyage and the other plans | [15.4](#154-the-world-plans) |

**Not on any island, on purpose:**

- **Unlock character** (an action of Behaviour & events): it gives a player one of Raft's characters in their Raft
  profile, for every world, so no example island uses it ([6.2](#62-behaviour-and-events)).
- Some of Raft's machines are **scenery only** in the editor: a water-logged hatch, the elevator's wheel, the bicycle,
  the relay station's buttons. Place them as decoration; give them **Behaviour & events** if they should do something.
- Raft's cooking recipe pickups and mystery packages work only on Raft's own islands
  ([15.8](#158-rafts-story-islands-piece-by-piece)).

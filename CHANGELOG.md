# Changelog

What changed in Custom Islands, newest first, for players and island makers. Internal clean-ups and the many test runs
behind each change are left out; the [guide](docs/GUIDE.md) explains every feature named here.

The last public release on raftmodding.com is **v1.1.1** by FranzFischer78 (2022). Everything below is the work on
**version 3.0**, the rebuild for Raft 1.1 (Unity 2021.3) by SwedenJohansson that started in September 2026. Version 3.0 is an
**experimental alpha**: things will still change, so back up the worlds you care about.

---

## 2026-10-24: plan Check knows every tool

- **Check tracks more of Raft's tools:** sea vines (machete), all of Raft's zipline lines, the dog cage, shark cage
  and padlocked door (bolt cutters), and checks on the headlight, binoculars, oxygen bottle, flippers, axe and metal
  hook. Variants count (electric zipline tool, advanced headlight, stone/titanium axe, titanium hook). Air zones,
  quest targets 15 m under the sea and dark zones round a quest give **advice** (oxygen bottle or flippers, headlight).
- **Check warns about codes in the wrong order:** a keypad whose code no note on its island or an earlier one tells.
- **Story-item warnings name the item and the island** that gives it later.
- The shovel and the zipline tool are no longer counted as researched at the start; Radio Tower and Caravan Town
  give their workbench blueprints (headlight, recycler, battery charger, zipline tool and base) in the plan's
  Progression.
- **Object list:** Raft's quest item pickups of Vasagatan, Temperance and Utopia (bolt cutters, keys, harpoon part...).
- **Editor:** the object panel has a "Raft's tools" note on every object that needs one.

## 2026-10-24: hazards and story-island gaps

- **Hazard zones:** a trigger zone's new Hazard group - acid, live water, fumes, spikes, cold (damage per second),
  darkness (black screen without a headlight) and deep water (breath drains without an oxygen tank); protection lines
  (a suit, worn or held items, states, signals); hide the zone to switch it off.
- **New checks and actions:** wears item, holds item (binoculars lookouts, hook- or axe-only openings), protective suit,
  hurt creatures.
- **Creatures:** Zap (butler bots placed from the list zap), a boss phase only blasts hurt, lights that keep creatures
  and jellyfish back.
- **Ready pieces:** Temperance's key socket, Varuna Point's explosive barrels, pillars and spotlights, Temperance's snow
  vehicles (repaired with 3 bolts, then they carry the players; not drivable). Generators placed from the list want 3
  parts (Parts needed, 1-6); old islands and recipes keep 1.
- Not done: carrying and stacking placed objects (the socket covers "put item X at Y").

## 2026-10-24: rare animal looks

- **New world setting, Rare animal looks** (World settings, under the build cost): Normal keeps Raft's odds; More
  gives about one clucker, goat or llama in three a rare look, Lots about half.
- **Three new looks per animal**, painted from Raft's own textures: jade, speckled and golden cluckers; toggenburg,
  boer and golden goats; piebald, cream and golden llamas. A look stays through save and load and is the same for
  every player.
- **Collection:** the journal's quest list counts the looks you have tamed (x/6 per animal), with a note for each new
  look and one at six of six. The host can change the level in Esc > Custom Islands or with `AnimalLooks`.

## 2026-10-24: vehicle islands

- **20 new library islands built round Raft's vehicles** (vans, caravans, excavators, forklifts, cranes, ore wagons, snow
  vehicles, planes, boats): 10 small (Breakdown Bend, Sledrun Halt, Pallet Point, Kestrel Strand, Ashdig, Mudlock,
  Countdown Cay, Gantry Bluff, Pinewood Haul, Lowtide Camp), 6 medium (Caravan Rest, Hollowpick Quarry, Whitecap Depot,
  Wingfall Mire, Cinderyard, Slipway Meadow) and 4 large (Timberline Haul, Coral Runway, Ridgeback Pass, Red Mesa
  Harbour). Ten have a quest; the others are islands to explore. All are listed in the guide's section 15.7.
- **Raft's vehicles in the older storylines:** forklifts, excavators, a building crane and two snow vehicles now
  stand on 13 islands of Raft 2, Raft Remade, Relic of the Seventh Tide, The Gravity Sea, The Last Post, The Long
  Voyage, The Lullaby Frequency and The Rainmakers, parked off the quest routes. The same pass removed or sank rocks
  that floated over slopes on 14 storyline islands, and the hyenas of The General Post Office now start on the
  square beside the arcade instead of on its roof.

## 2026-10-23: known-issues fixes

### New
- **Another whole storyline in the library, The End of the Ocean:** eleven islands in place of Raft's own story
  (Raft's story off; the chain on the Receiver, 300 to 1000). A pulse on the Receiver leads to the middle of the sea:
  Beacon Saddle, the Reefmachine Atoll, Triangulation Rocks, Static Shoals, the Outer Breakwater (Raft's vending
  machine; needs the engine, from day 5), the Current Locks, Stormworks, the Cooling Spire (a Raft store) and a finale in
  two parts: Engine Court (28 quest steps, a Raft store, reactivate / shut down / redirect the Concord Engine) and The
  Command Well 30 m under it, where the choice plays out. The Drifting Relay Buoy comes back every 12 km. Raft's
  forklifts, wagons and caravans stand at the wall and in the court. All of Raft's story blueprints are quest steps.
- **An eighth whole storyline in the library, The City That Never Sank:** eleven islands in place of Raft's own story
  (Raft's story off; the chain on the Receiver, 250 to 1000). Highwater lives on the top floors of a drowned city:
  Breakwater Customs, Raft Row Bazaar (Raft's vending machine), Fleetbreak Moorings, Hookline Stack, Pumpcrown Works (needs
  the engine), Lowstreet 20 m under the sea, Cistern Hall, Rookery Stacks (a Raft store) and a finale in two parts: the
  Bridgeworks Crown (26 quest steps in a storm, a Raft store, five choices) and The Foundation Piles under it (12 steps, the
  pile stingray). Choosing FREE brings Freehold Shoal. Raft's vans, caravans, forklifts and wagons stand about the city.
  All of Raft's story blueprints are quest steps on the way.
- **A sixth whole storyline in the library, The Last Green World:** eleven islands in place of Raft's own story
  (Raft's story off; the chain on the Receiver, 400 to 1200). Pollenreach Spit, Ranger Landing, Riverfork Delta, Tracker's
  Rise, Hollowtrunk Grove (Raft's vending machine), Rootweave Caves, Blightmire (needs the engine), Canopy Stair (a Raft
  store) and a finale in two parts: The Heart of Green (23 quest steps, a Raft store, one valve - BURN or SPREAD) and The
  Rootcore under it (12 steps, the Blightmother), which ends the way the Heart chose. Thornback Ridge turns up on day 6.
  All of Raft's story blueprints are quest steps on the way.
- **A seventh whole storyline in the library, The Frozen Horizon:** eleven islands in place of Raft's own story
  (Raft's story off; the chain on the Receiver, 300 to 1100). Signalstack Rock, The Floelane, Steamvent Harbour, Orrin's
  Sledge Camp, The Iron Whale - Bow (needs the engine; Raft's vending machine) and her Engine Decks under the pack, Saga
  Drift, Glacier Mouth (a Raft store) and a finale in two parts: The Coldhold Vault (24 quest steps, a Raft store, three
  switches - KEEP, CARRY or BROADCAST) and The Seed Ark 30 m under it. Squall Shelf turns up on day 9 and every 9 days.
  All of Raft's story blueprints are quest steps on the way.
- **Another whole storyline in the library, The Sunken Kingdom:** eleven islands in place of Raft's own story
  (Raft's story off; the chain on the Receiver, 500 to 1200). Spirecrest Roofs, Overpass Shoal, Brinelab Tower, Biome
  Seven (a dome 18 m down), Thalassa Mooring (Raft's vending machine, a diving bell), Lumen Shelf, Caisson Yard (needs the
  engine; a Raft store), Hadal Commons 45 m down and a finale in two parts: the Sealkeeper Spire (24 quest steps, a Raft
  store, RESTORE or SEAL) and The Stalker's Hollow 40 m down. Shuttlewreck Reef turns up 4 km out. All of Raft's story
  blueprints are quest steps on the way.

### Changed
- **Editor tools that match the storyline recipes:** the in-game editor can now build what the recipes build.
  - **Flatten to** a typed height above the sea.
  - **Scatter** has a **Seed** (the same seed gives the same spots) and **Anywhere** next to On land / Under water.
  - **Place exactly** takes sizes up to 10000.
  - An **Air pocket** button puts down Raft's air pocket, an Air trigger zone and bubbles in one step.
  - A zipline line shows both ends' positions and has **Near end: where I look**, to start it on a deck.
- **Raft's vehicles in the editor:** all 24 of them are in the object browser under their island's category, and
  they now rest on their wheels, tracks or hull. Utopia's crane no longer sinks 2.5 m and Varuna's excavator no longer
  floats. Caravan Town's toy boat and rocket are now solid.
- **Late-game resources in the storylines:** the last two or three islands of each of the ten storylines have a buried treasure with 3 titanium ore (metal detector and shovel; optional, never a quest step), as do a middle island in The Rainmakers (The Fogworks), Raft 2 (Cinderfall), The Gravity Sea (Pendulum Isle) and Raft Remade (Tangaroa Remade, which also gets three titanium rocks). The Last Post's chests hold titanium ingots (Franking Works, Ada's Round, General Post Office). Explosive goo: puffer fish and 3 goo in a chest on Cinderfall, The Fogworks and Telegraph Narrows. The Gravity Sea gets farm animals (a llama and hens at Upfall Orchard, a boar at Lodestone Quay), and Shelter Atoll's bunk locker holds wool and eggs.

### Fixed
- **Worlds saved before 6 October:** they now keep their plan's islands by content, as newer worlds do. A world that names no owner of its plan takes the host's islands as the plan's on its first load, and a library plan's islands are recorded from the installed entry when they are unchanged since the install. An island renamed or edited later is no longer taken for the plan's by its name alone.
- **A missing island is put back:** when a saved world loads an island whose file is gone (Tidy up moved it, you deleted it in the editor, or a `gen-` file was deleted by hand), it is copied back from `Mods\DynamicIslands\deleted` (and its folders): on the host by the world's recorded hash, else the newest copy of that name; on a player's PC only a copy with the host's hash. A note says "'X' was put back from the deleted folder for this world". The copy there stays.
- **No clash with Franz's original mod:** this mod has its own Harmony id (`com.franzfischer.customislands3`), and the PC check warns when the original mod is loaded too.
- **Decimal comma:** `0,5` in `spawnpool.txt`, `world_rules.txt` and `randomizer.txt` reads as 0.5.
- **Quest rewards that wait:** a player whose Raft-item reward waits at the island sees "Your reward for '<quest>' waits for you at the island - sail there to get it".
- **Save too big:** the host sees on screen when the world's save is over 64 MB and can't be sent to the players.
- **Kraken:** each sighting is purple 3 times in 4, else greenish or bluish (1 in 8 each), the same on every PC. Swipes do 5 damage (green and blue 5.5); one sighting takes at most 30 health from a player and never brings them below 25.
- **Event window warnings:** an event that can happen again (a zone that fires more than once, a chest that fills up, animals that come back) with show/hide or open/close gets an amber line, and a zone's enter event has a **Fires once ever** button.
- **Two copies of one island:** each copy's collect step counts only the story items found on that copy (and those found away from both); pages already counted per copy. Older saves count as before.
- **Object index after a Raft update:** the main menu notices a `catalog_index.txt` made with another Raft and makes it again in the background, a little each frame (no need to open the editor); the log says what was done.
- **Alphas in multiplayer:** when an animal turns alpha (Big Bruce too) the host sends its health once, so other players see the alpha's exact health instead of a weaker one that could die early.
- **Saving:** the host's world is saved when Raft is closed (also with Alt+F4 or the window's X), and about 3 seconds after each quest step, quest finish and story step (at most once a minute). A crash or power cut still loses what came after the last save.
- **Guide:** the known-issues chapter is gone - its advice is folded into the sections of each topic (hosting another day in chapter 8, files in 10, crashes and names in 13), and the later chapters are one number lower.

---

## 3.0 alpha - 6-21 October 2026: storylines, more extra options, editor tools, safety audit

### New
- **The editor's Camera window lists every key:** **Camera...** in the status bar now lists every mouse button and key
  of the editor - camera, tabs, undo/save, terrain and object keys - so new users can learn them in one place.
- **Lantern Spire and The Lockkeeper's Isle are featured islands now:** no more signs about the editor. The
  Lockkeeper's Isle has a 5-step main quest (the keeper's code is hidden in his mother's letter, his strongbox holds
  the key to his sea chest at the lookout), more to find and gather, and still brings Lantern Spire. Lantern Spire
  has a 5-step adventure: climb on to the flying rock, read the sky keeper's log, fetch his oil from a ledge down a
  ladder and light his beacon to find the key to his chest.
- **Fourteen new library islands to wander** (no quest, no banner): ten small - Gannet Rock, Driftwood Key,
  Bearback Isle, Twin Horns, Pelican Spit, Mango Dell, Cormorant Skerry, Basalt Cay and the sunken Skiff Shoal
  and Castaway Knoll - three medium - Bramblewood Isle, Gullwing Bay, Coyote Mesa - and Driftcloud Islet, a small
  flying island. Raft's own look and density: things to gather on land and in the sea, caves, an underwater wreck,
  jetties into the water, cabins, rope bridges, and monsters on the medium islands and a few small ones.
- **Three large library islands to wander** (no quest, no banner): Glowfall Isle, where a strange craft crashed
  into a jungle island beside a glowing green pool; Dunecrown Isle, a desert island with a stepped pyramid, a tomb
  and an oasis; Tidefall Reach, a volcanic island whose south half has sunk, a drowned town 8 m under the sea.
- **Five more medium library islands to wander** (no quest, no banner): Crowncrag Ruin, a castle crumbled on a
  crag over a village; Firedance Isle, a volcanic tribe's huts, dancing ground and idols; Deepwatch Station, a
  sunken listening station with an air pocket and a wrecked submarine; Longpier Haven, a harbour with a long pier
  and a watch hut on a rock stack; Skyrope Peaks, a flying island with a rope bridge between two peaks.
- **Two more medium library islands** (no quest, no banner): Mourner's Deep, a drowned graveyard with the
  gravedigger's shack, an air pocket and a sunken boat; Ringstone Hill, a ring of standing stones on a hill with
  a cave below it and a wreck offshore.
- **Three more medium library islands** (no quest, no banner): Geiger Isle, a fenced yard of toxic barrels with a
  guard's hut, a radio mast and a bunker in the rock; Charbone Cove, a jungle camp behind a palisade with tiki poles,
  cages and a hoard in a hollow rock; Skyhollow, a peaceful island flying 60 m up with a hermit's cabin and a boat
  stuck in its side.
- **Ghost ship** (World settings > Extra options, off by default): on about one night in four a glowing wreck
  drifts past the raft after dark, close enough to swim to - Raft's own pieces, lit cyan in a mist, with four chests
  of good loot (titanium, circuit boards, a battery...) and a crew of ghosts: Raft's monsters, see-through and
  glowing. At dawn it sinks, and whatever is left aboard is lost; sleep through the night and it never comes. The host
  brings it, so every player sees the same ship, ghosts and loot. New map type `ghostship`.
- **Lightning strikes** (World settings > Extra options, off by default): in a storm a bolt now and then strikes
  the sea 30-150 m from the raft - a bright flash, a jagged bolt with forks and a thunder clap. Rare: some storms
  have none, the others one to four; very rarely a bolt hits the raft's Receiver antenna or wind turbine instead.
  Looks only: no damage, no fire. Every player sees the same bolt at the same time.
- **Bioluminescent sea** (World settings > Extra options, off by default): on about one night in four the sea
  around the raft glows blue - sparks twinkling on and under the waves, soft glowing patches, sparks around a swimmer,
  shining fish and a flash at the bobber when a fish bites. On those nights fish bite twice as fast. Every player
  sees the same glowing nights (from the host's world seed).
- **Fog banks** (World settings > Extra options, off by default): on about three days in five thick fog rolls in
  over the sea for three to six hours, by day or at night, fading in and out over about an in-game hour. In it you see
  only 45-75 m, so islands show up late and the radar matters. Raft's own sky draws the fog, with a veil in its colour over the view so the sea
  fades too, and it fits Raft's light and weather; while a storm blows the wind keeps it away. Every player sees the same fog (from the host's world seed).
- **Kraken arm** (World settings > Extra options, off by default; in a later update): rarely - about one day in eight
  from day 8 (never while the raft is near an island), and with Storm seas one storm in four - a giant tentacle rises from deep below with a splash, sound and
  banner and grabs a floor on the raft's edge, never one in the middle, while one to three more arms sway 3-8 m off
  the edge and now and then slam at a player near them for a little damage (8 health; it rears up first, so step away). Ten melee or spear hits within a minute, by any players, make it let go. Too slow, and it
  tears that floor away, and each of the four floors beside it (same level, not the corners) goes too with a 1 in 2
  chance rolled from the event's seed, using Raft's own block removal. Rafts of 8 floors or fewer are left alone. The
  host decides; every player sees the same arm, a player joining mid-event gets it, and an active arm is not saved.
  Console `Kraken` (state, bring, away, tear).
- **Whale breach** (World settings > Extra options, off by default): now and then by day, about once or twice a
  Raft day, a whale breaches far out at sea (170-320 m from the raft): it rises, leaps half out of the water, falls back
  with a big splash and spray, and dives away. It is Raft's own whale and Raft's own splash. Looks only: it never comes
  near the raft or the players. Every player sees the same breach (the host sends where and which way).
- **Storm seas** (World settings > Extra options, off by default): Raft's storms come far more often and rock the
  raft harder. Each storm has a 1 in 4 chance to break one floor on the raft's edge or a corner (never one in the
  middle), and what stands on it falls. The host decides; every player sees the same storm and the same broken floor.
- **UFO sightings** (World settings > Extra options, off by default): on about two days in five a flying saucer
  crosses the sky high above the raft, horizon to horizon in about half a minute, in a new colour each time. It spins
  and blinks its rim lights; at night it glows green or purple. Looks only. Every player sees the same saucer at the
  same time.
- **Difficulty on every island:** the island library's list and World settings > **CHOOSE ISLANDS...** say
  **Difficulty: Easy / Medium / Hard / Extreme / Impossible** by each island (none, 1-5, 6-20, 21-49, 50 or more
  monsters; harmless and catchable animals don't count). Only a label: it changes nothing in the game.
- **Islands while sailing shows what each island is:** World settings > **CHOOSE ISLANDS...** lists the islands A to Z
  in compact rows: a picture (the library's, else a small map of the island), a **QUEST** / **no quest** badge, two
  lines of the description (all of it when you point at the row) and who made it. **Tick all** / **Untick all** does the
  whole list at once.
- **The island library says if an island has a quest:** the picked island's page shows **Quest: yes** or **No quest - an
  island to explore**, read from the entry's `quest` tag (plans show nothing new).
- **Story items as Raft's own items:** the Story items window's **Raft item...** links a story item to one of Raft's
  items; in the journal, **Take out** turns one into that item in your inventory (the host decides who gets the last).
- **Sounds and music from a trigger:** the when/then actions **play sound/music** (one of Raft's sound events or
  music cues, picked from a list, with a volume, once or on a loop, near the object or for everyone on the island) and
  **stop sound**. Every player near the island hears it; it stops when they leave or the island unloads, and a loop
  comes back after a reload. Check warns about a sound name Raft doesn't have.
- **Camera moments:** the when/then action **camera moment** shows the view from a marker toward an object for some
  seconds, then glides back; the player is held meanwhile and Esc skips it. Only players within its range see it.
- **Credits for a plan's ending:** a plan's ending card has a CREDITS section (a title and the makers' names). When
  the main story ends, every player sees the credits and the closing note once; the world plays on and Raft's own
  ending is untouched.
- **Story islands keep their look when randomized:** Something new like it and A variation of it for Caravan Town,
  Tangaroa, Utopia, Varuna Point, Balboa, Temperance and the Radio Tower lay the new island out like the original from
  its own big pieces, placed the way the original has them - red rock mesas with ladders, huts, caravans and rope
  bridges; the whole glass dome with its hexagon roads; Utopia's white towers; the motherlode floors on their pillars
  inside its walls with the crane on top; giant firs; snow rocks and the antenna; low rocks with concrete footings.
  Since 17 October the Vasagatan gets its lounge furniture and safe washed up on the beach, the Radio Tower's dish is
  put after the design (a tower on the top no longer clears it), and Caravan Town's sand between the mesas has
  caravans, vans, acacias, dead trees and boulders as thick as the original's.
- **Islands missing when a world loads:** when a world uses island files this PC doesn't have, the host (or single
  player) gets a box naming them, and the library entry each came with. **Play without them** leaves them out (no
  Receiver dot) but keeps them in the world, so they come back once the file is here; **Main menu** leaves without
  saving, to download or restore them first. Joining players get missing files from the host as before.
- **Again until done** for world plan rules **On a day** and **After sailing a distance**: a tick box on the rule card
  (`day:5:again` in the plan file). The island comes back ahead of the raft every N days (or km) after the players were
  last near it - even if they never reached it - until they reached it and finished its quest; the same island each
  time, with no limit. Check gives a tip when the island is on the Receiver (it doesn't come back by itself there).
- **Monster size up to x3:** a creature spot's **Size** now goes from x0.5 to x3 of Raft's own animal (was x2.5); its
  body and hit box grow with it, its health stays (set **Health** for a tougher one), and every player sees the same
  size. Balboa Remade's Mama bear is Raft's bear at x2 - as tall as Balboa's own.
- **Brand-new generated islands explained:** the row in World settings > Islands while sailing has a **?** saying what
  it does, how often one comes, where each is saved (`gen-<style>-<seed>.island`, kept in that world) and how to keep
  one; EDITOR > An island > **Open an island...** now lists generated islands too, to open and **Save as** your own.
- **Survivors to rescue:** a new object, **Survivor in a cage**, holds one of Raft's characters locked in with a story
  item. Bring the key and press E: the survivor says a line in a speech bubble and then stands on your raft. One per
  survivor id in a world, saved with it and seen by every player; the rescue sends a signal for quests and plans.
- **Boss phases:** a hostile creature spot can get a second phase: at a share of its health (half by default) it calls
  more creatures, gets faster and sends a signal, once per fight. A reload mid-fight keeps the phase. The Boss map
  type's beast has one.
- **Editor camera settings:** **Camera...** in the editor's status bar (and **Defaults... > Editor camera**) sets
  this PC's look sensitivity, move, zoom and pan speed, which way dragging up looks and the wheel's direction; saved in
  `world_rules.txt`, and the right mouse + wheel flying speed is now kept too. **Alt + right-drag** zooms, as in Unity.
- **Keycard doors and buried treasure (RF4):** select Raft's card reader door or another key door for its
  **Keycard door** settings: the story item that opens it (the island's own or one of Raft's quest items) and whether
  the card is used up. Without it the door stays shut; with it the reader beeps it through, for the whole crew and
  after loading. Check warns about a key door that no story item opens. Buried treasure is found only with the metal
  detector (it beeps faster nearer), dug with the shovel, and its chest can give **Extra loot** - story items too,
  once for the whole crew.
- **Islands and world plans apart:** **EDITOR** now opens a start window, "What do you want to make?", with cards
  for **An island**, **A world plan** and **The guide**; the one you made last is lit, with a **Continue** button. A
  world plan is made right on the main menu, without the 3D editor. In the island editor, **Use in a plan...**
  (replacing the **World plans** button) puts the island in a plan, next in the Main story line or as an island that
  comes on its own; a plan's **Edit island** opens it in the editor, with **Back to the plan**. A **?** after it
  explains it, and how it differs from **Save as**.
- **The plan window has three tabs:** **Main story line** (the islands the Receiver leads players to, in play order,
  with Raft's own islands as grey lines; **+ Add a saved island**, **+ New island**, and ▲/▼ to move an island along
  the story), **Other islands** (the WHEN / BRING / WHERE / TELL cards) and **Settings**. The window has a top row with
  **Plan**, **New...** (empty, step by step, a template or a copy), **Open...**, **Copy...**, **Delete**, **Templates...**.
- **A whole storyline in the library, The Rainmakers:** nine story islands told like Raft's own (Raft's story off,
  the plan's own chain on the Receiver): Hailstone Weir, Cirrus Silo Farm, The Kite Meadow, The Fogworks, Vaporhold
  Reservoir, Thunderhead Array, The Sleet Kilns, The Isobar Club and the finale, The Rain Engine (30 quest steps over
  four areas: a lift up the mountain, four arm machines, ziplines, a laser to turn with mirrors, a boss and the storm
  switched off). Raft's blueprints come in Raft's order; the ending has credits. Guide 16.9 shows what to look at.
- **Every storyline hands out all of Raft's story blueprints** (the 23 from Vasagatan to Utopia), so nothing is
  missing from the crafting menu: checked for all six library storylines. In the dev build only (not in
  the release): `CIPlanBlueprints [plan]` lists any Raft story blueprint a saved plan never gives, and a play test
  step `expect blueprints` checks it at the end.
- **Raft stores on custom islands:** the **TradingPost** object (Islands list) is Raft's trading post, working - Raft's
  store window, goods and prices, trash cubes and trade coins, trophy fish. Stock is kept per store with the world and
  shared by the crew. Recipes: `store [alias] at x z ground`.
- **Quest step "Blueprint":** find one of Raft's blueprints (carried, or already learned) while at the island. A
  missed story blueprint can be bought later at any Raft store (6 trash cubes, one each; only the ones a player lacks
  are listed).
- **Storylines ask for their blueprints:** The Rainmakers' quests now have a Blueprint step for each of Raft's
  blueprints its islands hold, and The Sleet Kilns and The Isobar Club each have a Raft store (Raft's goods, and any
  story blueprint a player missed).
- **Raft's vending machine on custom islands:** the **VendingMachine** object (Islands list) is Tangaroa's vending
  machine, working - Raft's six buttons, goods and prices (two of a Tangaroa plant for one Tangaroa token), the hatch.
  A waiting package is saved with the world and shared by the crew. Recipes: `vending [alias] at x z ground`; tokens
  from Raft's `QuestItemPickup_Tangaroa_Token` pickups.
- **Stores and vending machines in every storyline:** each of the five older library storylines asks for its
  blueprints too, and has a Raft store on a later island; every storyline has a vending machine on a story island
  (The Drowned Metropolis, Tangaroa Remade, Hightide Harbor, The Arena, The Isobar Club), and Gullsong Bazaar has one by
  its open stall. Five more library islands
  have a Raft store (Frostgold Creek, Saltpan Delta, Crowfield Farm, Twin Forges, Gullsong Bazaar).
- **Fixed:** on Caravan Town, Tangaroa, Utopia and Varuna Point Remade the ground and the big look-alike pieces had
  crept onto the story's ways (a hump at the bridge's end, Tangaroa's harbour office on a hill and its pump house's
  locker in the hill, pieces on Varuna's landing and Utopia's ramp); they are clear again. Sundown Canyons' bank vault and
  Albatross Field's control tower door are reached as before.
- **A second whole storyline in the library, The Last Post:** nine story islands down the Coastal Post's last route
  (Raft's story off, the plan's own chain on the Receiver): The Dead Letter Office, Pillarbox Pier, Parcel Canopy,
  Pneumatic Heights (from day 10), Sorting Rock, Telegraph Narrows, The Franking Works, Ada's Round and the finale, The
  General Post Office (26 quest steps over four areas: a clock crank, a pneumatic router and a vault keypad, two lifts,
  a claw, a brood mother on the roof, Ada joining the crew, a camera moment at the flag). Dead letters from the first
  island are delivered on later ones; Raft's blueprints are quest steps in Raft's order; Raft stores on the later
  islands and a vending machine at the General Post Office. A side island, The Wandering Sorting Barge, comes back every
  6 km until its quest is done. Guide 16.10 shows what to look at. In the dev build only: `CISetDay <n>` sets the
  day counter in a test world, for `day:N` rules.
- **A Receiver dot that waits for a blueprint:** a world-plan rule's WHERE has a **DOT** choice (in the plan file
  `needs=engine`, `steering` or `controls`). Until a player has that blueprint, the island comes straight ahead and has
  no dot on the Receiver; then its dot shows and a "near" island comes to its side. Default: always, as Raft's story
  islands. Check calls a blueprint Raft doesn't have a problem.
- **Story animals and goods to place:** Caravan Town's white screecher, Temperance's puffins and Utopia's butler bot
  (a new **Animals: harmless** category), and Utopia's market goods (sacks, baskets and crates of bananas) among the
  things to gather. The remade Caravan Town, Temperance and Utopia use them.
- **A third whole storyline in the library, Relic of the Seventh Tide:** nine islands woven into Raft's own story
  (Raft's story on; the plan's islands come after Raft's, 600 to 1300 on the Receiver): Marrow Key Museum, Sunstaff
  Mesa (instead of Balboa, with Balboa's four blueprints as quest steps), Sandsunk Necropolis, Skyhook Mast (Raft's
  vending machine and Tangaroa tokens), The Hollow Fang (three trials, an invisible bridge shown by thrown sand),
  Colossus of the Strait (three gears light the giant's eyes; a Raft store) and the finale, The Tide Engine (28 quest
  steps; a Raft store). Two side islands come ahead: Dredgers Bank (after 8 km) and Rope Bridge Gorge (from day 5).
  Guide 16.11 shows what to look at.
- **A fourth whole storyline in the library, The Lullaby Frequency:** a horror story of nine islands beside Raft's own
  (Raft's story on; the plan's chain on the Receiver, 500 to 1200): Hollins Cannery, Saint Agnes Sanatorium (Raft's
  vending machine and Tangaroa tokens), Effigy Shoals (from day 8), Stillwater Terraces (statues that come closer as you
  climb), Mistlock Causeway (a safe branch told by sound), Gannet Cliff (a Raft store), Cradle Reef (a drowned
  dormitory under a buoy) and the finale, The Listening Mirrors (31 quest steps over four areas, a boss in the pit, a
  Raft store). A side island, The Returning Islet, comes back ahead of the raft every second day. Guide 16.12 shows
  what to look at.
- **A fifth whole storyline in the library, The Gravity Sea:** twelve islands in place of Raft's own story (Raft's
  story off; the chain on the Receiver, 500 to 1300). Every sky island has a sunken half: Lodestone Quay, Upfall
  Orchard (30 m up) and Rootcellar Deep (25 m down), Altitude Market (Raft's vending machine) and Sinkmarket Floor,
  Pendulum Isle (a Raft store), Gravewell Shaft (45 m down), The Stair of Hours (a Raft store), and a finale in two
  halves: The Lodestone Array (35 m up, 22 quest steps, a Raft store) and The Array Root under it (12 steps). Two side islands, Weatherglass
  Shoal and Weatherglass Deep, come ahead after 5 km. All of Raft's story blueprints are quest steps on the way.
  Guide 16.13 shows what to look at.
- **Fixed:** The Rain Engine's three ziplines: Raft's zipline has no stand, and its boarding floor sank 19 m into
  the North spire, 2 m into the East one and hung 14 m over the crown in the South; each now starts on its spire's
  flat top and ends on the next one's. Stilt Hollow's banner hangs between two posts (its rope was tied to nothing)
  and the chair beside it stands on the stack, not half over the drop.
- **Fixed:** Tangaroa Remade's deck no longer buries two railings: the land's ridges under its south edge rose
  through the deck; a low ledge now runs along that edge under the deck, with the cliff behind it.
- **Fixed:** Raft's ziplines on Thunderhead Array, Pendulum Isle and Altitude Market: the boarding floor hung 12 m
  over Thunderhead's plateau edge and sank 23 m into Pendulum; both now stand on the ground, and Altitude's floor
  rests on an iron post.
- **Fixed:** The Rain Engine's third laser mirror stood beside its panel's line since beams turn at a mirror's centre;
  the beam now reaches the panel.
- **Fixed:** a laser beam turned at a mirror's box edge and could just graze the next mirror; it now turns at the
  mirror's centre.
- **Learn from the islands:** a new guide chapter (17) on opening any island in the editor - from the library, a
  ready-made one or your own - and looking at what it is made of, with a table of every editor feature and an island
  to look at for it. Two small library islands made to learn from, with a sign at each place: **The Lockkeeper's
  Isle** (a keypad code lock, three quests on one island, the island rules, an island its quest brings) and
  **Lantern Spire** (a flying island).
- **World plan Check: one story item on two islands:** Check warns when two islands of a plan each give a story item
  with the same id (two 'keycard's) - the one found on either would open the other's locks.
- **Raft's progression in order in world plans:** the plan Check warns about a blueprint given before the ones it
  builds on (the engine controls before the motor wheel and steering wheel, fuel pipes before a fuel tank...), and
  Progression shows it in yellow. A lock whose story item an island met before gives says where the item was kept from.
- **Keys kept for later islands in every library plan:** Raft Remade (Vasagatan's crowbar opens a crate at Caravan
  Town), The Long Voyage and The Abyss Expedition (Ironreef's pump store key, a box on the Drowned Metropolis), Silver
  Screen Seas (the liner's steward's keys, a sea chest at The Arena). Mayor's Wharf's keycard no longer shares its id
  with the Drowned Metropolis' council keycards in The Long Voyage. Embers Isle's chained dog cage is scenery now (in Silver
  Screen Seas it was a lock wanting bolt cutters only a later island gives). Blueprint names in Check read as words.
- **A clickable error note:** when something goes wrong in the mod, a note at the bottom left opens the details
  (whose code it was, the message), with Copy, the log folder and where to report. `errors.txt` keeps them.
- **The Islands window explains itself:** a **?** for Name, Height in the world, the island list and the sharing
  buttons. The height's ? says what 0, above 0 (flying) and below 0 (sunk) mean, and how players reach the island.
- **Giant rogue sharks:** with the Rogue shark option on, about one rogue in five is a giant - twice Bruce's size, three
  times his health, bites half again as hard on players (not on the raft).
- **The daily quest in the journal:** with the world's Daily quest option on, the journal shows today's quest under
  its page: what to do and how far along (3 / 14 planks, with a bar), the reward, whether it's done and when the next
  one comes.
- **Head start raft:** a new World setting (levels 1-3) starts the world on a bigger raft that is already set up: 6 x 12
  foundations with a grill, a purifier, the research table, a bed and first supplies; level 2 (8 x 14) adds smelters, a sail,
  10 item nets and island resources; level 3 (10 x 20) the Receiver with its antennas. Built once by the host.
- **More islands with Raft's special features:** every feature that was on one library island is on a second one
  now, and the mod's puzzle pieces (laser, water wheels, pipes, wires, claw, scales) are on six islands. In Raft 2,
  three locks want an item another island of the plan gives earlier (a keycard, bolt cutters, a radio part).
- **No more locks before their key in the library plans:** Raft's ready pieces that stood on islands only as scenery
  (wrecked engines and generators, cable hubs, shark and dog cages, dead radios) no longer want a story item the plan
  gives later. Whiteout Reach's summit radio now wants the crystal from the boiler's drawer, and Shelter Atoll's
  generator also runs its radio room. The plan Check no longer warns about a ready radio on an island with a generator:
  the generator runs it without the battery part.
- **Progression panel:** the world plan editor's new **Progression** button shows, in the plan's order, which rule's
  islands give which of Raft's story-island blueprints and story items, the locks that come before their item, and the
  blueprints the plan never gives.
- **Things to pick up indoors:** eighteen of the library's islands have a little plastic, planks or scrap lying on the
  floors of some of their houses; they come back after a few days like Raft's. The generator's huts and cabins (and
  the world randomizer's huts) get them too: none, one or two per house, on free floor beside the furniture.
- **Upgrade research table:** the extra upgrades are learned at a research table of their own (violet, four times the cost
  of Raft's, there from the start with the option on) instead of with their base item. Its research is separate from
  Raft's table; a world from before keeps the upgrades it had.
- **Laser beams:** Temperance's laser emitter shoots a beam the mirrors send on (turn a mirror, the beam turns); what
  it reaches gets the new event "a laser beam reaches it" - the laser door panel answers with the signal `laser`.
- **Unlock a character:** a new action gives the player one of Raft's characters (by name or number), as Raft's story does.
- **Boss arenas:** on a boss island, stakes burst up round the arena when the crew steps in and sink when the beast is
  defeated (two minutes at most); the spoils chest shows only then.
- **Ready puzzle pieces:** Utopia's water wheels and pipes (and the radio tower's pipe bend) turn a quarter each use
  like Temperance's mirrors; Utopia's wire connectors take Raft's electrical cable once and stay connected; Tangaroa's
  claw crane console and the justice scales send a signal. Each sends its own signal (`wheel`, `pipe`, `wire`, `claw`,
  `scales`) for quests, world plans and other objects.
- **The world travels with the players:** after each save, and when a player joins, the host sends Raft's newest save
  of the world to every player, into their own Raft World folder. Whoever hosts next loads it from Load Game; copying
  the world folder by hand is only needed for a player who missed the last save. A player's own world of the same name
  is left alone, and only the last 3 received saves are kept.
- **World option Extra upgrades:** better versions of Raft's things to craft, each tinted and crafted next to the
  thing it improves once that is learned: **large battery** (twice the charge; fits every battery slot), **titanium
  grill** and **blast furnace** (cook twice as fast), **reinforced storage** (half as many slots again), **wide net**
  (twice as wide, twice the items), **large fuel tank** and **large water tank** (twice as much), **greenhouse plot**
  (waters itself), **turbo engine** (twice the strength, half again as fast, burns fuel faster), **large wind
  turbine** (charges twice as fast), **comfy bed** (heals twice as fast while sleeping, respawn at 75%), **magnet
  hook** (pulls in half again as fast, gathers twice as fast), **titanium rod** (fish bite twice as fast), **swift
  flippers** (twice the flippers' extra swimming speed) and **large air tank** (air lasts twice as long as with the
  bottle). Also **rapid charger** (twice the charge per fuel), **fast recycler** (twice as fast), **bright lantern** and
  **floodlight** head light (light twice as far), **telescope** (twice the zoom), **titanium greatsword** and
  **titanium spear** (hit half again as hard) and **long paddle** (pushes twice as hard). Hand tools, weapons and worn
  things last twice as long as their base; every upgrade has a colour of its own (worn ones while worn). Also **steel
  pot** and **fast juicer** (cook twice as fast), **titanium machete** (hits half again as hard), **sturdy scarecrow**
  (never wears out), **master hammer** (twice the repair), **longbow** (draws and shoots half again as far), **bright
  head light** and **bright lamp** (light twice as far) and **lumber axe** (takes pieces apart twice as fast).
- **World option Long voyage:** random custom islands come half as often (after every 6-12 of Raft's own
  islands) and at least 1.6 km apart, so food, water and the raft matter more between stops.
- **World option Iron raft:** the raft's blocks take half damage from shark bites; taking pieces down with the
  hammer or the axe is unchanged.
- **World option Shared EXP:** when a player defeats a monster, every other player within 50 m gets 60 % of its
  EXP too.
- **Fix:** the daily quest's task, the rogue shark's days and the storm days are rolled from a well-mixed number of
  the world's seed and the day: with nearby seeds Raft's random numbers came out nearly the same, so days rolled alike
  (storm days came in runs, and the daily task changed little from day to day).
- **World option Trader raft:** very rarely (about once in 7 km of sailing) a trader's raft comes by, with a stall
  per offer: planks, plastic, rope and scrap for fruit, flower and tree seeds, and now and then a blueprint. The stock
  is shared by all players and saved with the world. Console: `Traders`; the host's `Traders bring` puts one ahead now.
- **World settings window:** the extra options are one list in the right column, about five in view; scroll for
  the rest, so the window stays the same size however many options there are.
- **World option Storm days:** on about one day in ten the sea is rough and it rains all day; the shark comes for
  the raft more often; everyone is warned at nightfall the evening before.
- **World option Silver & golden barrels:** now and then a barrel in the sea is silver (about 1 in 40) or golden
  (about 1 in 150), with a few extra nails, rope and scrap (gold: more, and sometimes a battery or bolts).
- **World option Rogue shark:** now and then a second, rust-red shark comes beside the usual one; it stays until
  it is killed, and then none comes for 5-8 days. Saved with the world; every player sees the same one.
- **World option Daily quest:** each morning a small task for the crew - gather some of a basic resource, catch a
  few fish or defeat a few monsters; done before dark, each player gets a small reward (basic resources or food, 40 EXP
  with levels) once; otherwise it runs out at dark. Shown as a banner and at the top of the journal's quest list.
- **World option Night is dangerous:** at night monsters have x1.3 health and damage and the shark comes for the
  raft more often; by day both are calmer.
- **Unsaved work, one at a time:** in *Unsaved work from last time* each island's row has an **X** that throws away
  only that island's unsaved changes (to `deleted\autosave`); the window closes when none are left.
- **Journal island count:** *Islands reached* now also counts the saved islands ticked for the world in World
  settings (Islands while sailing) that haven't come yet - a world with about 50 ticked showed 3/19.
- **Quest islands need finding:** the quest panel and introduction now show within about 70 m of an island's
  shore (was 150 m), and stay up out to 150 m.
- **Plan picker:** **Choose plan...** in the New Game box opens a window with every world plan - a search, its
  picture (or a map of its first island), description, random/story and the islands it brings - instead of a list.
- **Terrain brushes:** brush edge (falloff), Noise and Erode brushes, a sea floor switch, and a second style mixed
  into the island (8 ground textures).
- **Selection tools:** box select, select all, select the same kind, and a list of placed objects with hide and lock.
- **Copy and paste between islands**, and **Place exactly** (typed position, turn and size).
- **Elevation preview:** the blue sea plane moves to where the sea will be in a world.
- **Raft's sky and sea in the editor** (an option, off by default).
- **Rename an island:** worlds, plans, rules, the spawn pool and kept copies follow the new name.
- **My islands** window: every island file with its source, the worlds using it and its pool weight; open, rename,
  delete, tidy up. **My groups and stamps**: rename and delete saved groups and stamps.
- **Several quests per island.**
- **Raft's other story islands rebuilt from their own pieces** (14 designs) for the generator.
- Raft's doors, hatches, cranks and levers as ready pieces; Raft's door walls open on islands.
- A skyscraper landmark; landmarks on the randomizer's large islands.
- Generator option **Raft's features**: vine caches, buried treasure, ziplines, code strongboxes, beehives, dirt.
- **World Plans:** *Test this plan* starts a new test world with it; *View...* a plan from the New Game box; plan
  descriptions up to 400 characters.
- **Map types as data** (`.maptype` files you can write yourself), ReRollMapType, and a **Defaults** window for the
  `spawnpool.txt` settings.
- Version notices: plans and worlds remember which mod version wrote them and warn when they come from a newer one;
  players with different mod versions are told.
- **Starter kit for Test in a world:** you arrive with weapons, tools, a head light, flippers, an oxygen bottle, food
  and water, hunger and thirst full; in the test world **Esc → Custom Islands** has **Kit: On/Off** (for the next
  Test, kept in `testkit.txt`) and **Give the kit**.

### Changed
- **Editor camera: dragging up looks up** now, as in most games (holding the right mouse). The switch **Drag up: look
  down** in **Camera...** turns it round and is kept; **Mod's own** sets it back to up.
- **Arrival banner only for islands with a quest:** an island's name, author and description no longer show when you
  get near it unless the island has a quest. Zone and trigger messages are unchanged.
- **No update check:** the mod no longer asks GitHub for a newer version on the main menu (and never downloads one);
  new versions come from raftmodding.com. Players with different versions are still told when they meet.
- **Esc closes every window of the mod**, the topmost one first and one per press: the plan picker, the island
  library and its import window, World settings and its island list now close with Esc too. A window with unsaved
  changes (World plans, a quest, a note, story items, behaviour and events) asks first: Save and close, Close without
  saving or Keep editing. Esc in a text field no longer throws away what was typed into it.
- **The alpha box can be closed:** its **X** closes it until the next version; a small **Custom Islands: help** button
  in the main menu's bottom right corner opens it again (Discord, Guide, Report a problem).
- **The journal's key on the hotbar:** its tab at the hotbar's right end now shows its key (J, or the key it was
  changed to) the way the inventory's tab shows TAB; it showed none.
- **The .rmod holds only the mod's own files:** a stray `bash.exe.stackdump` was packed into the 10-17 build.
- **The editor's Islands window opens at once with many islands:** the first rows show straight away and the rest
  fill in over the next frames (1,000 saved islands held it for over 2 s).
- **Tidy up, Delete and Rename read islands faster:** they read only each island's own settings, not the whole
  file, when looking for the rules that name other islands.
- **The host checks what players report:** used things, harvests, quest progress and fired rules count only from a
  player near that island; levels are switched on by the host only, and a level record with more EXP than the host
  credited is put back.
- **Quicker starts:** RML compiled the mod on every start (about a minute), because its own start-up throws away the
  compiled copy it keeps. The mod now keeps that copy (read-only), so only the first start after installing or updating
  compiles; the main menu says so once. Older copies of the mod in RML's cache are removed.
- **The generator gives the same island on every PC:** its list of objects was sorted in the PC's language and could
  include other mods' buildables and follow `placeables.txt`. It now uses Raft's own island objects in a fixed order.
  **A seed now makes a different island than before** (worlds keep the islands already saved; only islands generated
  from now on - new `gen-` islands, a lost `gen-` file made again, map types - follow the new order).
- **No sample plans:** the mod no longer writes the sample world plans Adventure, Island hopping, Growing sea
  and Receiver adventure, and they are gone from **Templates...**. The plans that come with the mod are Random islands
  and No custom islands; full storylines come from the island library (**Get more plans...**). Sample plans written
  by an earlier version stay in your plans folder until you delete them.
- **The first Generate looks like Raft's small islands:** the generator's new layouts **Raft small island** (first in
  the list, and the default) and **Raft large island** take the outline and ground of one of Raft's own islands - the
  seed picks which, and turns, mirrors and wobbles it - with a beach all round to wade onto from the raft. The generator
  opens on one about 40 m across and 12 m high, and **Defaults** gives it; after a Randomize existing or Ready-made
  island the Normal tab no longer opens on that island's big, round settings.
- **Level up is a world option only:** the editor's Island tab and the generator (all three tabs) have no Level up
  choice any more; the level up system is switched in World settings or with `Levels on`. Older islands and presets
  with the choice still load (it is ignored).
- **World plan Check and Progression follow the play order:** Raft's story chain with your islands in their places,
  other islands by what they wait for. Raft's story islands give their blueprints when the story reaches them.
- **Check warns when a main quest needs a tool** (the machete for vines, the metal detector...) that no island before
  it gives. Optional finds (buried treasure, caches) needing a later tool are only listed in Progression.
- **World plans: the bottom buttons are two rows:** working on the plan (+ Add a rule, Check, Progression, Preview
  notebook, Test this plan) above, its files (Export..., Import..., Save, Close) below. In one row Close stuck out past
  the window's edge.
- **Island lists open at once:** **+ Add a saved island** and the other island lists in World plans open at
  once with a "Loading islands... (n of N)" line, then list your islands. Their titles are kept in `summarycache.txt`,
  so the next time takes no time (before, the list came only after every island's whole file was read).
- **Upgrades stand out:** stronger tints (also on parts that have only a texture), a few slow glints in the upgrade's
  colour round a built, hand-held or worn upgrade, and hand-held ones a little bigger. Pale tints hardly showed before.
  The titanium spear is a light, almost white metal (its tint was only a little darker than the scrap spear).
- **Check warns about a deleted story item:** a story item that a quest step, a check, a chest or an action
  still uses but that is no longer in the island's Story items list (or was mistyped) is now a quest trap in Check.
  A quest file edited by hand is read up to 99 steps; more are cut. (The editor adds up to 10; a recipe's quest runs
  longer - the library's finales have up to 40.)
- **Different builds of the same version are noticed:** every release says 3.0, so two players with different 3.0
  builds weren't told (a friend didn't see the new research table). Each build now has a stamp from its files; joining a
  host with another build (or one from before the stamps) shows a notice: both should install the same, newest release.
- **"The host didn't answer" says what to do:** when it keeps coming (a player who joined again within seconds of a
  crash can lose the link now and then), leave the world, wait half a minute and join again.
- **Esc → Custom Islands is compact:** the world's settings window fits a 1366 x 768 screen (it was taller than the
  screen): tick boxes for the randomizer's parts, the extra options and the islands while sailing, short scrolling
  lists, a hint line at the bottom, and the whole window scrolls when needed. Each group and option now says what a
  change does in a world already under way; the head start raft is shown (made with the world, can't change). The
  plan is shown but no longer picked there: a world keeps the plan it was made with. Regrow days, the Receiver dots
  and the unload distance left the window (F10 `RegrowDays`; **Defaults...** in the New Game box). The list of the
  world's islands left it too (the quest book shows where each island's quest stands); the extra options list is taller.
- **Two world options retired:** *Scrambled blueprints* and *Story islands in a new order* are no longer in
  World settings for new worlds. A world that has one on keeps it, and the host can still switch it off there.
- **EXP +50 %:** every monster is worth half as much EXP again (Bruce the shark 180, was 120); the EXP each
  level needs is the same, so levels come faster.
- **Editing islands your saved worlds have (R1b).** Every object of an island now has its own number, kept through
  edits (copies get new ones; files saved before keep working, numbered by place). Deleting objects, changing their
  order or making one a chest no longer keeps those worlds on the old version: they get the new one, with what was
  picked, looted, opened or done there kept on the same objects (what belonged to a deleted object goes with it).
  Only removing or reordering rules or quest steps still keeps the old version for them. **Give my worlds this
  version** carries what was used the same way.
- **Two packs, one story item id (T8).** Installing a pack whose story item id another installed pack already uses
  gives the new pack's item an id of its own (`key-<pack id>`), with its quests, chests and behaviours changed to
  match, so each key opens only its own door in a world with both. Updates keep the new id.
- **Library updates (T10).** An entry shows **Update** also when one of its files changed in the library without a new
  version number (installs now record each file's SHA-256). Updating over files you changed offers **Update, keep a
  copy** (yours stay as "name (yours)", never at random while sailing), **Keep my changes** or **Cancel**, and names the
  saved worlds that use them. A world with an island an update replaced says so once when it loads.
- **Removing an island's rule or quest step keeps saved worlds safe:** like removing objects, it now keeps the old
  version for the worlds that have the island, so a fired rule doesn't fire again and a quest doesn't jump a step.
- **Single islands and plan islands written down:** the guide (7, 9.5, 16) and README now say that islands a world plan
  brings (and the islands their rules bring) never come by chance, which files are never in the sailing list, and how
  to make an island a single island.
- **New islands come first:** a random pick counts an island the world already has (left behind, not finished) at a
  quarter of its weight, so the same island no longer comes back so often early on.
- **"Comes by chance after ... km sailed"** (island editor, Island tab, Rules): the earliest an island may come by
  chance, with the sailing time it means (e.g. 40 km, about 5 h); at most 192 km.
- **Missed notes bring the island back:** an island whose quest is done but with a note not found yet can come
  again by chance, as you left it; only once every note on it is found is it finished for good.
- **Notice boards look written on:** a readable notice board shows lines of made-up writing on both faces, so
  players can see it holds a note.
- **Text that didn't fit:** status lines, island names in the lists, behaviour rows and titles, World plans hints,
  the quest reward line and inspector loot names wrap and shrink to stay in their boxes; names on the World plans map
  no longer write over each other.
- **Islands while sailing:** the row *Brand-new generated islands* now says what it does (hover it, and the list's
  intro): the mod makes up a random island now and then, saved as a gen-... file in that world; unticked, only made or
  downloaded islands and map types come. The New Game summary notes when it is off.
- Plan islands are matched by their content, so the host can ask players for exactly the right file; newer kept
  copies are set aside instead of overwritten.
- Build cost is rounded to the nearest whole amount and never goes below Raft's own; blocks give back what they cost
  when they were placed.
- Quest "find" steps count story items found in all; "near" is measured from the middle of the land.
- Switching the story order mid-game replaces islands in place.
- The plan Check warns about quest traps and story items that come out of order.
- The electric zipline blueprint is never scrambled away.

### Fixed
- **Scrapyard excavator on slopes:** made islands set it on the ground under its tracks, not the lowest ground under its
  boom, so its tracks no longer sink up to 0.9 m into a slope.
- **Wooden boxes in scenes:** made islands keep room for a wooden box's real size (2.6 x 2.2 m), so a radio's power box
  no longer stands inside it.
- **Rescued survivors move on your raft:** the survivor stood stiff with its arms out; it now moves like Raft's own
  characters. An open cage whose survivor is missing from the world's list puts it back without counting a second
  rescue.
- **Generated islands: flat props lie along the slope.** A 4 m pallet on a slope was buried in it (it was set on the
  lowest ground under it); flat props now follow the slope and other props sink at most half their height.
- **Generated islands: nothing half buried round a den mound, no shore ice in the air on flying islands.** Grass and
  small objects on a mound's raised edge stood buried on one side and floating on the other; they are taken away now.
  Shore ice on a flying island hung 1-5.5 m in the air where the land under the waterline was cut; it is skipped.
- **Editor: the top bar fits a 1024 x 768 screen** again (it ran 6 px past the edge once Use in a plan got its help
  mark).
- **Choose islands...** (World settings): a section heading hides when the search leaves no row under it.
- **Generated scenes: props no longer stand inside each other.** On newly generated islands and randomizer outposts (scrapyards, markets, camps, gardens, radio sites and the rest) each prop, the chest and the note keep clear of each other however they are turned; two metal crates of a scrapyard stood 0.35 m into each other. Islands already generated keep their props until they are made again.
- **Old Mine Islet: the lantern crate by the mine's mouth stands on the floor.** It stood by the south wall where the den's rock floor doesn't reach, 1.2 m over the ground under it; it now stands 4 m further in, its lantern still on top.
- **Generated scrapyards: the excavator stands on its tracks.** It hovered about 0.8 m over the ground on generated islands (its measured size counted a hidden part down to its pivot). Grass and plants beside a generated radio tower or lighthouse also move with the levelled ground by its lowest change, so they no longer stand in the air on its slope.
- **Generated shacks: the satellite dish sits on top of its pole.** It hung 0.25 m out from the pole and 1 m under its top, with a gap between the two; it now sits on the pole's top, as on the hand-built islands.
- **Cave islands: no posts in the air, no sky through a cave, no box edges across an arch.** Sundown Canyons' gate frame on the mesa stands on the rock with both posts, and its canyon gallery no longer shows a slit of sky in the dark cave mouth; Stormwing Isle's hangar pad reaches out under every post of the frame; where a cave mountain's skin ends at a passage that runs out through the rock (Deepwatch Station's arch), its wall now leaves the passage open instead of cutting the arch with straight edges.
- **Look-alike story islands: the trees, rocks and caravans stand on the ground next to a design.** On Randomize existing, a Caravan Town look-alike's dead tree stood 1.1 m in the air where the design's levelled square blended into the land; the look's pieces now stand on the ground again once the designs, buildings and caves are done.
- **World settings: each extra option's text fits its box.** The text under each toggle is now short (one or two lines); the full description shows on hover. Extra upgrades, Storm seas, Lightning, Ghost ship and others ran out of the box.
- **Ziplines with a far end you choose (`zip.to`):** the far end's floor now moves with the line; it stayed where Raft's own line ends, sometimes in the air on a slope (The Lodestone Array).
- **Vending machine for a player who sets the island up late:** the host now sends the hatch state with each press, so a player whose island was set up after the host paid no longer misses Raft's message and sees the package.
- **Nothing hangs in the air on the story islands and the randomizer's extras:** The Listening Mirrors' satellite
  dishes stand on a mast on their plinths and its altar on the road; Gannet Cliff's red banners hang between two posts.
  The randomizer stands every satellite dish it adds on its pole.
- **A rescued survivor or a dug-up treasure could come back on an island with a looping sound:** the sound
  kept its state under the same numbers and cleared theirs when it stopped or the island loaded again. Each keeps
  its own numbers now, and a sound only clears its own.
- **Treasure dug up in an older save came back:** after the numbers above changed, a story treasure dug in a
  world saved before it was no longer remembered. The old number is read too.
- **An island coming back "ahead of the raft" could come back behind it:** when the first spots in front were
  taken, later tries went up to 145 degrees off the raft's course. A returning island now stays within 80 degrees of it.
- **A player could not join a world with buried treasure on a loaded island:** the host's world message for the
  joiner broke on the island's treasure points, and the joiner waited at "Requesting world". Each machine makes
  those points itself now; the host no longer sends them.
- **View rules... in the plan picker:** its Close button threw an error instead of closing the rules.
- **A joined player sometimes got no islands:** when Raft's world-arrived event never reached the mod, the
  joining player never asked the host for its islands. Now it asks anyway 10 s into the game.
- **The credits at a story's end:** no island banner shows faintly behind the top of the credits card any more (the
  story's last banner waits until the credits are closed).
  Scrapyard Haven's banner is tied to two posts, Cirrus Silo Farm's hatch and fence stand on level ground, and Varuna
  Point's yard is level under the excavator and dumpster. The Varuna Point look-alike sets its rubble heaps on the
  ground floor, not 0.5 m over the open top floor. The Radio Tower look-alike stands its satellite dish only on
  near-level ground: on Radio Tower Remade's sharp peak the rock stood 2 m through it.
- **More floating and buried pieces put right:** grass and plants on a generated island no longer stand 1-1.4 m
  over a den's reshaped ground (they follow the lowest change of the ground around them, not just under their middle).
  Albatross Field's control tower stands on its ground, Frost Hollow's research log lies on its table, The Kraken's Wake's
  and Whalebone Bay's posts reach their decks, and Caravan Town Remade no longer has four of Raft's ladders inside its
  rock stacks.
- **Main story quests stay out of the journal's side quests:** a main story island's further quests, and the quest of
  a generated island in the main story (a *type* rule), were listed in the journal under "Other islands with a quest";
  they count with the main story now, whose steps are in Raft's notebook. A copy of a story island in the
  random pool (a library import such as "Thornwood (The Long Voyage)") no longer shows as a side quest still to come. The guide has the rule for new story lines
  (every island the story depends on gets a place in it); all five story plans keep it.
- **Machinery placed as decoration stays decoration:** an island script's (recipe's) generator, radio, cage, hatch or
  lift placed without a name no longer gets the Ready pieces' working settings; one given a name does.
- **Survivor in a cage:** the cage no longer has a loose red rope lying on the ground beside it (the rope Raft's
  shark cage hangs from at Radio tower). In the editor and in a world, for every player.
- **A generated island whose file was gone** is made again when the world loads: in a session where the editor
  had already been opened, remaking it failed with an error (the generator's defaults were set up too early), so the
  island was missing from the world.
- **Flying islands seen from below:** the rocky underside now meets the ground all round the coast. Where the
  coast ran diagonally it hung down too early, and from below the sky showed through zigzag gaps between the rock and
  the ground (Lantern Spire showed it). Every flying island, made by hand or generated, at any height.
- **The island library's picture** no longer stays on the next entry you click: while that entry's picture is
  on its way the space is empty and its number says **(loading)**, or **(can't be loaded)** when it fails. A slow
  download had left one island's picture showing on every entry clicked after it.
- **The generator's reach line** (how far the island reaches from the raft) now measures the island Generate
  makes: on Randomize existing it left out the caves and buildings that are not made there.
- **Presets and map types that ask for too much** (buildings over 25, quest steps over 8, features over 10) are
  kept in range when generating, so the report no longer lists quest steps that "found no place".
- **Randomize existing on Temperance's small islets** no longer offers (and picks) the Rebuild it designs
  of Temperance itself: a design now matches only its own Raft island, not every island whose name starts the same.
- **No more "AN ERROR OCCURED!" while generating with buildings or caves:** Raft's story scripts no longer throw
  while the generator loads Raft's islands for buildings and caves (also the core catalogue's scenes).
- **Test in the editor got stuck:** it sometimes went to the main menu with Load game open and then did nothing on
  later clicks (Raft's Load box had a broken link after the editor). A test now starts behind a cover with Cancel. If it
  can't start, you are back in the editor and the status bar says why. Back to the editor no longer disappears after 4
  minutes in the test world.
- **Hover texts outside the editor:** only the editor's status bar and the in-world window drew a control's hover
  text, so the main menu's windows (the plan picker, the island library...), the stats page and the alpha box never
  showed theirs. Where there is no such bar, the text now comes in a small tip by the mouse after a moment.
- **Private storages after a join:** when storages that were taken down or sank were forgotten at the host's
  save, a player already in the game kept the old list until they joined again. The host now sends the new list
  to everyone at once.
- **World plan help caught up:** the plan editor's Help box still said to click "Custom Islands plan" in the New
  Game box; it now says Choose plan..., and its Help box and ? popups explain Test this plan, Progression, Preview
  notebook and New main story....
- **Carried off an island by the raft:** standing on a custom island's ground or on its Raft blocks (a wreck,
  a house of planks), Raft could take the player for someone on the raft, so the raft's drift and rocking moved them
  along with it - tens of metres in a second when the raft was far off. Island ground now counts as an island, as
  Raft's own islands do.
- **Into the sea after a slow start:** on a busy computer the island a player had saved on could spawn later than
  the 20 seconds the mod held them for, and they fell into the sea. The hold now lasts while that island is still
  coming, up to two minutes.
- **Held by a dead warthog:** a warthog killed while carrying a player on its charge (another player's spear, say)
  never let go: the player hung on the dead animal, unable to move, until they left the world. A dead warthog now
  drops them, as Raft does when the carried player dies.
- **An error picking up a crate:** picking up a crate (or anything that gives several items at once) with an empty
  hotbar slot selected could put a fishing rod or another item that charges up in hand and away again at once, and
  Raft then threw an error (a NullReferenceException) in the middle of the pickup. It no longer does.
- **Joining again on a ghost raft:** a player who left while standing on a ghost raft (a deck just above the sea)
  could come back on the host's raft: the deck wasn't loaded yet on their PC, so Raft took them for a swimmer and
  moved them. They are now held where they stood until it loads, as on any other island.
- **A broken player spot in a world file:** a spot saved as "NaN" or "Infinity" (a damaged or hand-edited file) was
  read as a number, and the player joining again was moved there. Such a line is now left out, like any other broken line.
  The same goes for the distance the raft sailed towards the next ghost raft: "NaN" there kept them away all session.
- **GenerateIsland with "NaN":** typed as a size, height, roughness or object amount it stopped the command with an
  error. Such a value is now left out, as if it wasn't given.
- **Generated radio towers in a world are whole again:** an island the world generated (a large island with
  a radio tower) laid the tower's floors at a third of their size when Raft hadn't loaded those pieces yet, so its walls
  and furniture stood partly in the air. Islands generated from now on get each piece's own size; the editor's
  generator was never affected.
- **Comfy bed missing from the crafting menu:** it copied the wooden bed, a bed *skin* with no crafting tab of its own,
  so no tab listed it. It now sits in the bed's own tab.
- **Trader raft and a bad world file line:** an `@tradersailed` of NaN or Infinity is now read as 0 (it stuck, and the
  trader raft never came again).
- **A quest step done early counted twice:** a zone step reached early and entered again (a player set down inside it
  after a load) was kept as done twice; early work now never counts more than the step needs.
- **Upgrade table taken away while in use:** the items in its slot go back to the player who had its menu open
  (Raft destroyed them with the table).
- **Barrels switched off:** the silver and golden barrels already in the sea turn plain-coloured at once
  (they lost their extra loot already but kept the colour until Raft spawned them again).
- **Boss arena re-arms:** after a lost fight (the stakes sank after two minutes) stepping back into the arena
  shuts the crew in again while the beast lives. Boss islands generated before keep the old one-shot arena.
- **Rogue shark switched off:** a rogue that was lost (not found after a load) no longer brings a new one
  when the Rogue shark option has been switched off meanwhile; it is let go.
- **Editor commands:** `SetElevation`, `ChangeWidth` and `ChangeStrength` with `NaN` show their usage instead of
  setting a value that is not a number.
- **Upgrade research table, two players:** when two players researched the same item at the same moment, the
  second one lost the item; it now goes back to that player. A world file's research lines with unknown items or
  upgrades (edited by hand, or from an upgrade a later version dropped) leave those out.
- **No errors when the editor takes objects from Temperance or Utopia:** two of Raft's story scripts there threw an
  error each time the editor loaded those islands (switched off) to take their objects; they are kept quiet then.
- **Placed objects stay as placed:** a few of Raft's objects (Varuna Point's debris planks) kept Raft's physics and
  tipped a little each time their island was saved and opened again; every placed object now stands still.
- **Things on generated islands lie on the ground:** metal sheets, firewood, tyres and pallets on a slope now lie
  along it instead of being half buried; a set piece (a stranded boat on a beach edge) is never sunk more than half
  its height; a lookout tower's back railing no longer runs off its deck and its side railings have no gaps. Shore
  ice on snowy islands lies at the waterline without the beach's sand rising over it, and only on gentle beaches.
- **The library's story islands stand on the ground:** a check of every island of every world plan found 73 things
  out of place on 28 islands - walls hanging over a slope, a radar screen wider than its desk, crates and chests set
  into banks, paintings, a camera and tarps off their walls and posts, a wind vane wider than its mast, ice pillars
  inside a den's walls. Each is moved, re-seated or given something to stand on.
- **No pause at the first random island of a session:** the first pick read every pool island's whole file for its
  "not before" distance (a fresh host with 130 pool islands: 0.5-0.7 s a pick on average over the first 20); the
  distances are now kept in `notbeforecache.txt`. The world plan's first look through a session's islands and the
  first pick no longer read every island's whole file either (a world of 152 islands: 9.4 s): an island's own rules
  come from `rulescache.txt`, and an island never reached isn't read to see whether it is finished.
- **A damaged downloaded island is fetched again:** a player's kept copy of the host's island that was cut short (a
  crash while it was written) was used as it was, failed to read and the island never appeared; its content is now
  checked when joining and a damaged copy is downloaded again.
- **A world folder renamed by hand:** Raft still loads it, and now the mod does too - the plan, levels and island
  state come from the folder Raft picked instead of being lost.
- **No hitch for a player who joined many worlds:** each random pick checked every kept copy of a host's island
  on disk again (up to 0.7 s a pick with hundreds of copies); the answer is now kept for a few seconds.
- **No hitch when a random island is chosen:** with many saved islands, picking the next one read the islands folder
  again for every line of spawnpool.txt (a quarter of a second with 1,600 islands); now about 20 ms. A 20-hour world
  (150 islands, a 200-page journal) was measured to load, save and show its Receiver dots without trouble.
- **Snow and cream animals are light now:** the world randomizer's light colours only multiplied the animal's
  texture, so a dark animal such as the warthog stayed almost as dark as Raft's own. They now lift the dark parts
  first, then tint.
- **Sea animals left behind no longer count as defeated:** Raft takes its harmless sea animals (dolphins, turtles, stingrays, whales) away after a while, and animals far from every player; the mod counted each one as killed, so an island's animals could be "defeated" - and the animals gone for good - just by sailing past. Those animals now count as taken away, not killed: they come back with the island. One the players kill still counts as defeated, also when Raft removes it at once (a stingray).
- **Dens on Raft's big islands:** the world randomizer's den had stopped fitting on any of Raft's big islands, as the
  ground under its rim had to stay within 1.5 m. The rock outcrop over the den now closes a drop down to the ground, so
  the den fits where the ground falls up to 3 m under its rim, and its rim still meets the ground all round.
- **Daily quest kills in multiplayer:** with the level up system off, monsters another player defeated (and the
  host's own) did not count for a "Defeat monsters" daily quest. They count now, with levels on or off.
- **Frozen over the sea when joining:** a player who joined could be held in place over the open sea (and bitten by
  the shark) while the mod waited for the island they had stood on - it also waited for the randomizer's extras on one
  of Raft's islands, which never load while that island isn't there. It waits only for an island that can be under the
  player, at most 30 seconds, says so on screen, and lets go at once if the player is hurt.
  And a player who joined onto the raft before its blocks came in is no longer held still while the raft drifts
  on without them (they dropped into the sea by the shark): the raft is left to Raft.
- **Caves that floated:** the randomizer's caves (on Raft's big islands and on generated islands) could stand with
  their rim over the ground, so you saw into the rock from outside and through the floor from inside. On a generated
  island the ground now rises round the cave and buries its rim, open only at the mouth; on Raft's islands a cave goes
  only where the ground meets its rim all round and the floor inside is even. And in a world every cave piece - generated,
  the randomizer's and those placed by hand on library islands - now sits inside a rock hill built over its back and
  sides down to the ground, of the island's own rock (snowy rock on a snowy island), open only at its mouth: no more
  thin shells in the air or sky seen through the mountain (nor a hole in the hill over a den's roofless stretch, nor a slot under a den's low edge where it hung over the ground).
  The hill stops where a cave's roof ends, in a face down to the ground: the way in, and the ledges and paths in front
  of the mouth and beside it, stay clear.
  Balboa's vine cave, a whole rock of its own, is left as it is.
- **See-through caves (X7):** from inside a cave you could see the sea or the sky where the island's ground ran
  through a room (Raft draws the ground from above only), and from outside - in the editor, or over a gap - a cave's
  rock wasn't drawn at all. Every cave piece's rock is now drawn from outside too, and the ground in a cave's rooms is
  let down under the room's floor and cut away where it lies wholly inside or runs steeply through (a hole dug down to
  a tunnel's end, as at The Drowned Labyrinth); in the editor the ground keeps the heights you sculpted. The rock hill over a cave no longer rises out of pits and dug stairs well away from the cave (it blocked Ashfall's crypt stair, 25 m from its bear den). `CICaveInside` looks round every cave piece from inside and fails on any look out.
- **See-through rooms (X7):** Selene's room pieces (walls, ceilings, floors, door frames, tunnels) were drawn from
  inside only too: a room's outer wall, or its ceiling seen from above, showed the room through it. They are drawn
  from both sides now. Hollow Mountain's rocks no longer stand inside its entry hall and granary, its high hall has a
  wall over its windows up to the ceiling, its lake roof reaches the islet and the snow no longer shows through the
  entry hall's floor; Ashfall's crypt slab covers the crypt and the ash is banked up to its edges (cut again after the
  crypt's pit, which had left thin slits under them), so the sky no longer shows under them. The Last Kingdom's crown
  vault has a wall with a doorway on its stair side and its sliding doors across that doorway (the pit and the stairs
  showed beside them), and the hill no longer comes in at its back corners. Rooms built from Selene's pieces in the
  recipes (`selene_cell` in `lib_selene`) had a slit of sky along the top of every wall: the walls stop 0.23 m under
  the ceiling, where Selene's own rooms put ceiling edge pieces; the walls (and the doors in their doorways) are drawn
  5 % taller now to meet the ceiling. Where a room opened straight onto Selene's ladder shaft (The Island of Stations,
  Shelter Atoll), the ground showed in slits beside the shaft's mouth and over it; the room has a doorway into the
  shaft there now. `CIRoomLooks` looks round a room from inside and
  `CISeleneBacks` checks the pieces' backs.
- **"Another build" told again after a rejoin:** a player who rejoined with the host's own build after joining
  with another one still got the old notice. A notice is now only about the host the player is with.
- **Animals walking into the sea:** llamas, goats and the other animals of the custom islands could wander into the
  shallows. They now stay on dry land (the beach just at the waterline at most).
- **The crafting menu:** the Weapons list was twice as wide as its recipes and covered the inventory (the upgrades
  not learned yet counted in its width); lists are now as wide as the recipes they show. Switching category now closes
  the last category's recipe box (the stone axe stayed open over Food/Water).
- **A new plan with an old plan's name:** deleting a plan and making another under its name gave the new plan to the
  worlds made with the old one. Each plan now has its own id, and those worlds keep their own copy.
- **EXP of a new alpha:** a monster that was already hit before the randomizer made it an alpha (switched on or
  re-seeded mid-game) kept its old, lower EXP worth. The worth is now worked out again when its max health changes.
- **Randomizer finds on a fresh island:** for a moment after the randomizer chose extras for one of Raft's islands,
  their file wasn't written yet; a look at it then logged "Could not read island 'rnd-…'" and could move the extras
  off the island. Their size is now known before they join the world.
- **The host's distances for other players:** the unload and Receiver distances went to other players in whole
  metres, so a host's 812.5 m was 813 m on theirs. They now go as they are.
- **Nothing ticked in Choose islands:** a world with every island unticked logged "the spawn pool is empty" every
  10 seconds of sailing. It says so once now, and the World settings button says `none`.
- **Two players in one once-only zone:** when both stepped in within a second, the one who came second was told
  nothing. They now see "Someone else got here first".
- **Very high EXP:** an EXP total past about two billion wrapped round to a negative number and broke the level count.
  It stops at the top now.
- **Fireworks give EXP:** Raft counts a firework's hit as the world's, so a monster hit by one gave no EXP. The
  player who lit it now gets the EXP, also in multiplayer.
- **Rig Seventeen's valve test works:** its lever looked for three valves that had no name, so the crew stash never
  dropped open. The aqueduct's three wheels are named too.
- **Crater Lair's roof laser works:** the mirror and the panel had lost their names (two settings on one line), and the
  panel stood just off the turned beam. Turning the mirror now lights the panel and opens the roof safe.
- **A Raft update that removes one of its objects no longer mixes up an island's trees:** the trees and items after the
  missing object keep what your world saved for them (picked, chopped, regrowing) instead of moving one place along.
- **A busy host no longer loses a chest:** when the host's answers to "may I open this?" kept arriving late (busy sending
  island files), player 2 asked again for ever and nobody got the chest. The host now holds it longer for that player.
- **Private storages and the hammer:** another player could take someone's private storage down with the
  hammer (and get what was inside). Only its builder can now (or the host, when the builder isn't in the game).
- **Build cost refunds after raising the cost:** in a world whose blocks were all built at Raft's own cost, raising
  the build cost and loading the world again made the earlier blocks count as built at the new cost, so taking them
  down gave back more than was paid. The world file now notes the cost they were built at.
- **Worlds from a newer version:** an island line with more fields than this version knows was dropped and then
  erased at the next save; the island now loads and its extra fields are written back as they were.
- **Older worlds save the same twice:** a world without a plan owner got one only at its next load, so each save of
  an older world changed the file once more; the owner is now written at the first save.
- **Another author's pack with the same id** was installed as that entry's update, over its files; it is an entry of its
  own now (`<id>-<author>`), and the import says so.
- **Story texts that disagreed with their islands** (Heart of Fire, Drowned Labyrinth, The Safe Room, Hightide Harbor,
  Glacier Station, The Rock Pen, Thornwood, Wreckers' Cove) and notes naming things their chests don't give (Floe Reach,
  Hermit's Table, The Great Fen, Mayor's Wharf, Shelter Atoll, Rookery Cliffs, Keel and Mast). The Abyss's last chapter
  no longer sends you back through every air pocket.
- **An island rule bringing its own island** brought a new copy each time a copy's rule fired (with "on day 2", one
  every second); a world now takes at most 10 copies that way.
- **A plan rule waiting for an island's Quest 2** (or later) was taken by the quest editor as the main quest's and could be
  overwritten on Save; it is left alone now, and World plans have a **quest no.** field for "When a quest is done" (the
  plan check says when the island has no such quest).
- **The New Game box forgot the plan** chosen for the last world: it now offers it again, like World settings (`newworld_plan.txt`).
- **Library buildings no longer float:** floors and decks over the water or a slope on the library islands now
  stand on posts down to the ground or sea floor (recipes stand them at the save; `posts off` leaves a deck
  meant to hang). `CIFloating` also names floors over nothing and chests with nothing under them, and
  `CIFloatingSweep` checks every saved island (the randomizer's extras left out).
- **Remove stopped with "Cannot create a file"** when the deleted folder already had a dated copy of the same file
  from the same second (an island removed twice); the older copy now gets a number.
- **Limits against runaway growth:** journal pages (2000, titles 200 and texts 8000 letters) and story item kinds
  (500) are capped, the host drops claims that ran out once it holds many, and a file already on its way to a
  player isn't sent again when they ask twice.
- **Quest steps already met move on at once:** a collect or pages step the crew already has enough for is ticked
  together with the step before it, not half a second later.
- **Stats page:** open when the world is left (a disconnect), it closes and no longer locks the cursor on the main menu.
- **Island music:** music zones on an island switched on while a world was still loading now work (the fix for their
  red error in the editor skipped them).
- **Quests:** two steps asking for the same kill (Whiteout Reach's two polar bears) no longer lock when both animals
  are defeated before the first step comes: each counts for one step. More than a step needs carries on to the next same step.
- **Arrival texts:** an island's name and description, its quest's intro and other messages now come strictly one
  after another, each up long enough to read (5-16 s by length); the next one used to replace it after 3 s.
- **Generator:** regenerating an island with 0 quest steps also takes away its old quest (its objects were gone);
  undo brings both back. Brand-new islands made while sailing follow the world's seed when it has one.
- **Generator:** closing it while Raft's islands still load no longer generates later on its own; a second click while
  loading says it is still loading. The preview's count now includes the under-water kinds *Randomize* adds.
- **Editor keys:** a key Raft already uses, or a number key (hotbar slots), can't be picked for an editor action.
- **Sea plane** in the editor now fits the island's ground size, also after it grows.
- **Give my worlds this version** leaves the editor's test world alone (its next save wrote the old version back).
- **World plan list:** Receiver, by-chance and story rules now show [done] once their island is in the world.
- **Edited plans:** a saved world now follows the plan's *random islands while sailing* switch when you changed it in
  World Plans (it kept its old switch).
- **WorldIslandsGap** with words it can't read now says how to write it and changes nothing (it reset to 3-6).
- **Hardening:** library pictures claiming more than 4096 pixels a side aren't unpacked; NaN numbers in a hand-edited
  spawnpool.txt or generator preset are ignored; a negative quest step in a hand-edited world file counts as 0.
- **In-game texts** brought up to date: import window, Open, Throw them away, Share..., World Plans help and Delete,
  tab colours (nine), the Randomizer and plan file help, the report form, lighthouse storeys, desert palms, regrow
  tooltips.
- **Boss health:** an animal's health can be set up to Ã—12 (was capped at Ã—4), so the lair bosses set to Ã—5-Ã—12
  are as tough as written. Damage stays at most Ã—4.
- A large audit (about 70 items) of saves, files and multiplayer: world copies written between Raft's saves no longer
  win after a crash; unloaded islands keep what happened on them; library installs check their files and roll back;
  half-written first saves are set aside; host file names are checked before use; deleted autosaves and plans go to
  the `deleted` folder.
- A raft that Raft left without its grounding colliders gets them back (it could drift through islands).
- Missing generated islands are made again; a stale object index in a world no longer mixes up objects.
- Randomizer extras appear at the island's place after a world shift.
- Two players reaching a once-only zone: the second one is told it was already used.
- The host can open a private storage whose builder is not in the game.
- Performance work on streaming islands and the library.

---

## 5 October 2026: Raft's own features for island makers

### New
- **Things to gather** on islands, with a Scatter tool in the editor; every library island now has them.
- Generator sliders for Things to gather and Finds in the shallows; 0-25 buildings and a Wrecks and landmarks kind.
- Raft's dirt spots and wild beehives, machete vines, ziplines (with a far end you choose), buried treasure for the
  metal detector and shovel, Raft's quest item pickups.
- **Keypad code locks** on any usable object.
- Raft's note frequencies name the custom island they now lead to; Receiver distances and rewards for players who
  were away.
- Lighter randomizer colours (snow, cream, pale).
- Plan Check shows which island gives which of Raft's blueprints, and warns when one is never given.
- About 25 new library islands (among them Cablecar Stacks, Sargasso Town, Rig Seventeen, Hollow Mountain,
  The Last Kingdom, Clockwork City, Starfall Crater).

---

## 4 October 2026: the quest book and the themed islands

### New
- **Quest book:** a world's main story gets its own tabs and pages in Raft's notebook; side quests stay in the
  journal. A *New main story* helper and *Preview notebook* in World Plans; endings for the five plans that come with
  the mod.
- **18 themed islands** (Gilded Skull Cove, Tiki Lagoon, The Safe Room, Primeval Park, Sundown Canyons and more) and
  the **Silver Screen Seas** world plan, plus a dozen more new islands.
- Raft's finds in the shallows around library islands.

### Changed
- Random islands: a finished one never comes again, and they come about once per 3-6 of Raft's islands.
- Main story islands always have coordinates on the Receiver.
- The guide can be opened from the library window.

### Fixed
- Spotlights no longer shoot at the player.

---

## 3 October 2026: Raft Remade, Raft 2 and a better journal

### New
- **Raft Remade:** every story island of Raft rebuilt (Radio Tower, Vasagatan, Balboa, Caravan Town, Tangaroa,
  Varuna Point, Temperance, Utopia) as a world plan played from start to end.
- **Raft 2:** ten new stations ending in The Frontier finale (32 steps in six chapters).
- Generator: **reefs** like Raft's, **buildings and caves**, a **quest made with the island**, and *Randomize existing
  / Rebuild it*.
- **Journal:** a Progress panel (quests, islands reached, notes, story items, pages), notes found per island, and
  "Quest Pages".
- Friendly animals on some islands.

### Changed
- Roofs rest on their pillars; generated huts stand on Raft's foundations.
- The quest panel shows five steps at a time and scrolls.
- Every library island rebuilt with the reef generator.

---

## 1-2 October 2026: the island library fills up

### New
- The **island library** reads the public library folder; the first library islands and world plans (The Long Voyage,
  The Abyss Expedition and others), each played through by an automatic tester.
- Very large islands (Ironreef Caverns, The Drowned Metropolis, Thornwood, Glacier Station, Scrapyard Haven).
- Dark zones really darken; air pockets under water; angler fish.
- Generator densities measured from Raft's own islands, per style and size ("Like Raft").

### Changed
- Nothing stands in the air: the generator, placing and sculpting set things down by their base.
- Editor camera: dragging up with the right mouse tilts the view down.

### Fixed
- Many fixes from scenario tests where Raft's gameplay meets the mod: ramming islands, flying and sunken islands,
  world shifts, full inventories, deaths, waits, quest counts with an older host, joining while islands are made.
- Moving a player lets go of a ladder first.

---

## 29-30 September 2026: safety, easier windows, settings in a running world

### New
- **Esc > Custom Islands** in a running world: rules, randomizer, options, levels, islands while sailing, plan and
  story; the host changes them and picks another plan from a list.
- **Test in a world** from the editor.
- **World Plans** made easier: each rule is a card (WHEN, BRING, WHERE, TELL, STORY) with drop-down lists that say
  what each choice does, and a thorough **Check** that walks the plan like a world plays it.
- Drop-down lists instead of cycling buttons in the quest editor, Behaviour and events, styles and layouts, Light
  and the New Game box.
- Hotbar key tabs for the journal (J) and the stats page (K).
- **Tidy up** in the library window.
- Raft's abandoned-raft crate as a placeable object.
- The level up system as a World setting and a host switch (Levels on/off).

### Changed
- Deleting an island moves it to `Mods\DynamicIslands\deleted` and names the worlds and plans that use it.
- Island settings are undo steps (they were lost on leaving).
- Saving over an island that saved worlds use keeps those worlds on the version they started with when the change
  would mix things up.
- Editor light presets retuned.

### Fixed
- Saves replace the old file in one step; a half-finished save is recovered.
- An object this Raft version doesn't have is kept as a red placeholder instead of being dropped.
- Joining players keep asking for the host's islands instead of giving up; a *Resync* command.
- File names Windows refuses, "in use by another program" errors, and a version handshake between players.

---

## 27-28 September 2026: world settings, story chain, packs and the library

### New
- **World settings** window in the New Game box: world rules (monster difficulty, build cost), the **world
  randomizer**, extra options (scrambled blueprints, story islands in a new order, ghost rafts, private storages) and
  islands while sailing.
- **Level up system:** EXP from monsters, stat points, the same for every player.
- **Story chain:** a world plan's islands in Raft's Receiver chain (first, after or instead of a story island),
  Raft's story on or off, islands on their own frequency.
- **Host swap:** another player can host the world next time.
- **Packs:** share islands and plans (Export / Import); each world keeps its own copy of its plan.
- **Island library** window to download islands and plans.
- Choose which islands a new world gets while sailing.
- The **illustrated guide** (docs/GUIDE.md and a PDF), help ? marks in the game, and how to report a problem
  (including the mod's Discord).
- The **EXPERIMENTAL ALPHA RELEASE** box on the main menu; a loading box while the editor opens.

### Changed
- Rafts of Raft's blocks on islands float like the player's raft and have a deck to walk on.

### Fixed
- Editor autosave; an edited plan plays in its world; older Raft saves load the mod's state of that save.

---

## 25-26 September 2026: a stronger generator, randomizer and multiplayer

### New
- A much more powerful **island generator**, with underwater slopes like Raft's islands and trees, bushes, rocks and
  resources where Raft puts them.
- A modern **editor camera** like Unity's scene view.
- **World randomizer:** a normal Raft world that plays out differently every time; randomized islands like Raft's;
  up to eight players.
- World rules shared by every player (monster difficulty, build cost, regrowing, creature health).

### Fixed
- First two-player tests: axes on custom trees, island sync while joining, world shifts after the menu, players put
  back where they stood.

---

## 23-24 September 2026: version 3.0 rebuilt for Raft 1.1

### New
- The mod compiles and runs on current Raft (Unity 2021.3) again, with save and load fixed.
- **Editor:** textured terrain, soft brush with a ring, undo, texture painting (Sand, Grass, Rock, Seabed), an islands
  window, and every object of Raft (about 1,900) placeable from a browser with pictures.
- **Island styles:** Tropical, Snowy, Desert, Forest, Volcanic.
- **Islands while sailing:** custom and brand-new generated islands appear on their own, far ones unload; islands on
  the Receiver; flying and underwater islands; sunken props and scrap to dive for.
- Islands behave like Raft's: saved with the world, walkable, harvestable, remembered when harvested.
- Huts and abandoned rafts from Raft's building blocks.
- **Making islands come alive:** creatures, notes and signs, loot chests (also locked), trigger zones, quests,
  atmosphere and sound zones, behaviours and events, story items and the journal, object groups and terrain stamps,
  invisible walls and ramps.
- **World plans and map types.**
- **Multiplayer:** players get the host's islands and missing files; harvesting and pickups sync.

### Changed
- Islands built as Unity `.assets` files are no longer supported; islands use the `.island` format.

---

## v1.1.1 and earlier (2022-2024, FranzFischer78)

The original Custom Islands: islands built in Unity and loaded into Raft, async loading, a shader fix, and the first
terrain editing (2023). See [raftmodding.com](https://www.raftmodding.com/) for that release.

# Plan Check in play order - report

Branch `cloud/plan-check-order` from `origin/content` at 09161a8. Nothing was compiled or run: there is no compiler or
Raft here. The code was checked by reading it (see "Checked by hand" at the end). Every `.cs` file in this checkout,
including the new ones, has LF line endings, because `.gitattributes` uses `* text=auto` and the repository converts
to CRLF on Windows checkouts. The new files use LF too.

## What changed

### 1. Play order (`DynamicIslands/PlanChecker.cs`)
- `PlanChecker.PlayOrder(plan)` (l. 615) and `Stop` (l. 597) list the plan as a player meets it:
  - Raft's Receiver chain comes from `StoryChain.BuildSteps` (first / after:X / instead:X, plus the story islands it
    keeps). Each chain step has rank k+1.
  - Every other rule is placed by its WHEN:
    - `start`: before the chain.
    - `km:N` / `day:N`: after as many of Raft's story islands as the player passes on the way, about one every
      `KmPerStoryIsland` = 4 km. A day counts as `DayKm` = 2 km (l. 613). With Raft's story off, the chain's own
      islands are counted instead.
    - quest / step / zone / visit / signal / rule: right after the rule it waits for.
  - A chain rule that also waits for another rule comes after both.
  - A rule waiting for something unknown, or caught in a circle, goes last.
  - `StopName` (l. 587) names a stop: "Caravan Town" or "rule 4 'ironreef' (Ironreef Caverns)".
- `Progression(Ctx)` (l. 521) follows `PlayOrder`:
  - Each story island the plan keeps is now a step of its own (`Step.Rule = -1`, `Step.Raft`). It gives its
    blueprints, and the tools those blueprints make, at its place in the chain instead of at the start.
    `BlueprintsOn` (l. 581) reads them from `raft_blueprints.txt`.
  - New step fields: `Tools` (the tools given at that step) and `ToolNeeds` (`Progress.ToolNeed`: island, feature,
    tool, `Main`, and `Later`, the first stop after it that gives the tool).
- `Check` (l. 361) runs `Progression` once, then `CheckBlueprints(c, p)` (l. 446, now in play order),
  `CheckTools` (l. 467), `CheckStoryOrder` (l. 376) and `CheckStoryClash` (l. 411).
- `CheckStoryOrder` walks `PlayOrder` and skips Raft's story islands. Their quest items are Raft's own and are never
  keys for the plan's locks (decision 4).
  - `RaftsOwn` (l. 397) adds a note when a lock wants one of Raft's quest items, e.g. "(Raft's own on Tangaroa opens
    only Tangaroa's doors, never the plan's)".
  - The phrases "later rule" and "no island of the plan gives" are kept, so `CIStoryOrderCheck` still matches them.
  - In the game, our `has|story:raft-...` checks read only the crew's StoryBook, never Raft's quest-item inventory, so
    the game already behaves this way.

### 2. Needs table (`DynamicIslands/RaftFeatures.cs`, new)
- `RaftFeatures.Tools` lists Machete, MetalDetector, Shovel and ZiplineTool. Each entry has a label, a blueprint and a
  `Researched` flag.
- `RaftFeatures.Features` maps objects to tools:

  | Object | Tools | Optional? |
  |---|---|---|
  | `ChoppableVines` | machete | |
  | `Treasure_Buried` | metal detector + shovel | always optional |
  | `Pickup_Landmark_DirtPickup` | shovel | always optional |
  | `ZiplinePath_Landmark` and `ZiplinePath` | zipline tool | |

- Helpers: `Of`, `ToolsOf`, `ToolOf`, `LabelOf`, `GivenIn` (finds `Tool*n` or `Blueprint_Tool*n` in an island's
  settings) and `FromBlueprints`.
- `GenFeatures.cs` l. 78, 92, 110 and 244: the explorer's chest now takes its tools from the table. The order and
  the output are unchanged, so `CIGenParity` / `CIGenRaftFeatures` should not change.
- The vines' own `has|Machete|1` check stays in `ObjectProps.Defaults`, which already used
  `ContentCatalog.MacheteItem`.
- Story-item locks (keycard doors, a cage and its bolt cutters, a generator and its part) are not in the table. They
  already go through `ReadyPieces.ItemOf` and are checked as story items.

### 3. Tools like Raft (decision 3)
- `Facts` (PlanChecker l. 54) gets three new fields:
  - `NeedsTools`: tool -> the features that want it.
  - `MainNeeds`: `"tool|feature"` pairs the main quest can't do without.
  - `GivesTools`: the tools the island gives (l. 114).
- `FindTools` (l. 179) treats any object as wanting a tool when it is in the table or has an "Only if... has/uses up
  <tool>" check.
- `QuestWants` (l. 229) decides "main": a main-quest step wants the object itself, or something its actions
  `show` / `open` (a chest by title, a note by title, a zone by id, a chest holding a `collect` item), or its `give`
  action hands out the story item a step collects. Buried treasure and dirt are never main.
- `CheckTools` (l. 467) gives a **warning** only for a main-quest need with no giver earlier in play order. Its text
  says either "only <stop> gives it, later in play" (plus ": the story stops here." for a chain rule) or "no island of
  the plan gives it (Raft has its blueprint on X)".
- Optional finds get no Check finding at all. The Progression panel shows them as a muted "Optional: buried treasure
  wants the metal detector - Caravan Town gives it later; players can come back" line.

### 4. Progression window (`DynamicIslands/PlanProgressWindow.cs`)
- `Fill` (l. 98) shows one row per step in play order:
  - Raft's story islands appear in their places ("RADIO TOWER: Raft's story island | Blueprints ... | Tools ...").
    The single "RAFT'S STORY" row is gone.
  - Rule rows add a "Tools:" line and the tool needs: main needs in the warning colour, optional ones muted.
- The footer and the Progression button's tooltip (`WorldPlanWindow.cs` l. 357) now say "play order".

### 5. Story id clash (decision 5)
- `CheckStoryClash` (l. 411) gives a **warning** when one story item id is *given* on two or more islands of the plan.
  - Map-type sample islands are left out.
  - An island that only *wants* an item another island gives is a key found elsewhere, which is intended, so it is
    not flagged.
- Example text: "The story item 'keycard' (Council keycard) is given on 2 islands of the plan: 'The Drowned
  Metropolis' (rule 5 'metropolis') and 'Mayor's Wharf' (rule 12 'wharf'). The crew's story items go by their id, so
  the one found on either island opens the other's locks too, and counts and "uses up" checks mix them up."
- Fix text: "Give each island its own id for it (Island tab > Story items...: e.g. 'metropolis-keycard' and
  'mayor-keycard'), and change that island's locks, chests and quest steps to match."
- For Raft quest-item pickups (`raft-...`) the fix text says to use a story item of the island's own instead.

### 6. CI tests (`DynamicIslands/DevTestsPlanOrder.cs`, new; csproj l. 253)
Both tests are registered like the others: a `[ConsoleCommand(name: "CI...")]` in a `DevTests*.cs` file, plus a
`<Compile Include>` line.

- **`CIPlanProgressOrder`** (l. 32):
  - Builds 3 test islands in code, as `CIStoryOrderCheck` does: vines in front of the quest's chest plus a buried
    treasure, a chest with a machete, and a treasure alone.
  - Builds a plan whose file order (vines, tools, cache) differs from its play order (cache first, Radio Tower, vines
    after the Radio Tower, tools waiting for the vines' quest, Vasagatan...).
  - Checks the `PlayOrder`.
  - Checks the main-quest warning: it names `'citools'` and says "the story stops here".
  - Checks that the treasure gets no warning, and that Progression notes it quietly with Later = "Caravan Town".
  - Checks that Balboa's Machete blueprint comes at its place in the chain.
  - Checks two plans where the machete comes first (no warning) and a story-off plan with no machete (warned).
  - Checks the panel's rows in play order.
- **`CIStoryIdClash`** (l. 118):
  - Two islands that each give 'keycard': warned, and the fix contains "-keycard'".
  - One island gives it and one only wants it: fine.
  - Two islands with Raft's bolt cutters: warned, with the raft fix.
  - A plan door wanting `raft-Tangaroa_KeyCard` while Tangaroa is kept: still "no island of the plan gives", with
    "Raft's own on Tangaroa".
- **`CIPlanBlueprints`** (`DevTestsPlanBlueprints.cs`): updated because the panel no longer has the "RAFT'S STORY"
  row (rows = steps). Blueprint sums now count only rule steps.

## Doc lines to paste

These files were not edited, as asked (GUIDE, README and CHANGELOG are off limits, and the docs/claude text is given
here as paste-ready text).

**GUIDE.md, `### 7.5 Check: finding and fixing problems`:**
> Check walks the plan in **play order**, the order a player meets its islands. Raft's story chain comes first,
> with your islands in their places (first, after, instead of). Every other rule comes where its WHEN puts it: at the
> start, after the island it waits for, or for a distance or a day, after the story islands a player passes on the
> way (about one every 4 km). Raft's story islands give their blueprints, the machete on Balboa and the metal
> detector on Caravan Town, when the story reaches them, not at the start.
>
> **Tools.** Vines need the machete, buried treasure the metal detector and a shovel, dirt the shovel, ziplines the
> zipline tool. If a step of an island's main quest can't be done because no island before it gives the tool (or its
> blueprint), Check warns. In the story chain the story would stop there. Optional finds that need a tool given
> later get no warning, as in Raft: a cache behind vines no step needs, or buried treasure. Players come back with
> the tool. The Progression panel lists them quietly.
>
> **Story items on two islands.** If two islands of the plan each give a story item with the same id (two
> 'keycard's), the one found on either island opens the other's locks too. Check warns: give each island its own
> id. Raft's own quest items (the Vasagatan keycard, the bolt cutters...) found on Raft's story islands open only
> that island and are never keys for your locks.

**GUIDE.md, Progression panel paragraph (7.x, where the Progression button is described):**
> Progression lists the plan in play order. Raft's story islands it keeps appear in their places with the
> blueprints and tools they give. Each rule's islands show what they give (blueprints, story items, tools) and what
> their locks and features want. A main-quest need comes in the warning colour; an optional find is a muted line.

**README.md, `### Source overview`, new row:**
> | `RaftFeatures.cs` | Which of Raft's tools each feature wants (vines: machete; buried treasure: metal detector and shovel; dirt: shovel; ziplines: zipline tool) and how a tool is had (a blueprint on a story island, given, or researched); read by the plan Check, the Progression panel and the generator's explorer's chest |

**CHANGELOG.md, `### Changed`:**
> - World plan Check and Progression follow the play order: Raft's story chain with your islands in their places,
>   other islands by what they wait for. Raft's story islands give their blueprints when the story reaches them.
> - Check warns when a main quest needs a tool (machete, metal detector...) that no island before it gives. Optional
>   finds (buried treasure, caches) needing a later tool are only listed in Progression.
>
> **`### New`:**
> - Check warns when one story item id is given on two islands of a plan (one island's key would open the other's
>   lock).

**docs/claude/areas/world-plans.md, Files table, PlanChecker row and PlanProgressWindow row:**
> | `PlanChecker.cs`, `PlanCheckWindow.cs` | Check: `Finding`s (`Level.Problem` / `Warning` / `Tip`) and the report window. `PlayOrder` / `Stop` / `StopName`: the plan in play order (`StoryChain.BuildSteps`, then WHEN; km/day placed by `KmPerStoryIsland` / `DayKm`); `CheckTools`, `CheckStoryOrder`, `CheckStoryClash` walk it |
> | `PlanProgressWindow.cs` | Progression panel: `PlanChecker.Progression(plan)` in play order - Raft's story islands kept (`Step.Rule` -1, `Step.Raft`) at their places, each rule's blueprints, story items, tools (`Step.Tools`, `Step.ToolNeeds`), never given (CIPlanBlueprints, CIPlanProgressOrder) |
> | `RaftFeatures.cs` | Feature -> tools table (vines, buried treasure, dirt, ziplines), tool blueprints, `Researched`; `GivenIn` |

**docs/claude/areas/world-plans.md, "Where to change X":**
> | Play order (Check, Progression) | `PlanChecker.PlayOrder` (`KmPerStoryIsland`, `DayKm`) |
> | Which tool a feature wants | `RaftFeatures.Features` / `RaftFeatures.Tools` |

**docs/claude/areas/world-plans.md, Traps:**
> - `Progress.Steps` holds Raft's story islands too (`Rule` -1): filter `Rule >= 0` before indexing `plan.Rules`.
> - Story items found on Raft's own story islands never count for the plan's locks (`CheckStoryOrder` skips Raft stops).

**docs/claude/areas/quests-story.md, Files table, row for `BuriedTreasure.cs`, `ZiplineEnds.cs`:**
> ... ; the tools each needs: `RaftFeatures.cs` (also the vines, dirt).

**docs/claude/CHANGE_CHECKLISTS.md, "New per-object setting" > Usually also:**
> - An object players need one of Raft's tools for (a feature): add it to `RaftFeatures.Features` (and the tool to
>   `RaftFeatures.Tools`). The plan Check and the generator's explorer's chest follow it.

**docs/claude/areas/testing.md:** no change needed. The new tests follow the `DevTests*.cs` + `CI...` pattern.

## What to test in game
1. `compile.ps1` and `compile.ps1 -Release`: both must print BUILD OK. Nothing here was compiled.
2. `CIPlanProgressOrder` and `CIStoryIdClash`, anywhere; run them in the editor too, so the Progression-panel row
   checks run.
3. `CIPlanBlueprints` (changed), `CIStoryOrderCheck`, `CIStoryChainUnit`, `CIPlanTest`, `CIGenRaftFeatures`,
   `CIGenParity` (GenFeatures now uses the table).
4. By hand, in the World Plans window:
   - Open The Long Voyage and press Check: one new warning about 'keycard' (Metropolis / Mayor's Wharf), and no tool
     warning.
   - Press Progression: rows in play order, The Stranded Gull first, then Radio Tower, Signal Rock, Ranger's Rest,
     Wreckers' Cove (with a muted "Optional: buried treasure wants the metal detector - Caravan Town gives it
     later..."), Vasagatan...
   - Check that the story-island tags fit in the 120 px tag ("VARUNA POINT", "CARAVAN TOWN").
5. Silver Screen Seas > Progression: Gilded Skull Cove shows "Optional: vines want the machete - Balboa gives it
   later"; no Check warning.

## Per-plan Check results (found by reading the plans and island recipes in content/)

I read `content/recipes/plan_*.recipe` and expanded every island recipe's macros (`lib_features` and the others) with
a script, then applied the same rules as the code. "New" means findings the old Check did not give. The existing
findings (tips, the blueprint tip, rule tips) are not repeated here.

| Plan | Play order (short) | New findings |
|---|---|---|
| **The Long Voyage** | Stranded Gull (km 2) > Radio Tower > Signal Rock (km 4), Ranger's Rest (km 7) > Wreckers' Cove > Vasagatan > Stilt Hollow > Thornwood > Balboa > Mayor's Wharf > Scrapyard Haven > Caravan Town > Crane Yard > Ironreef > Tangaroa > Frost Hollow > Metropolis > Varuna > Tide Farm > Observatory > Temperance > Old Mine Islet > Glacier > Utopia > Shelter Atoll | **Warning (clash):** 'keycard' given on The Drowned Metropolis (rule 5) and Mayor's Wharf (rule 12). The Wharf comes first in play, so its keycard counts toward the Metropolis vault's three. **Info only (Progression):** Wreckers' Cove buried treasure wants the metal detector, which Caravan Town gives later (Old Mine Islet's detector blueprint comes even later, km 28). Thornwood's vine caches are optional, and Thornwood gives the machete itself. No tool warnings. |
| **Silver Screen Seas** | Tiki Lagoon > Radio Tower > Gilded Skull Cove, Drowned Labyrinth > Camp Blackwater > Vasagatan > Keeper's Light > Sunken Liner > Balboa > ... > Temperance > Highmoor > Primeval Park > Sundown Canyons > Utopia > Crater Lair | **Info only:** Gilded Skull Cove's vine cache wants the machete, which Balboa gives later. Its buried treasure is covered: the island gives a metal detector. Primeval Park's vines come after Balboa, so nothing to report. No warnings. |
| **The Abyss Expedition** | the expedition first (mine > shelter > sunken > caverns > wreck > city > abyss, each after the one before), then Raft's chain | Nothing new. Only The Drowned Metropolis has a 'keycard' in this plan, so there is no clash here. The clash you found is within The Long Voyage (Metropolis + Mayor's Wharf). Map-type islands (sunken, wreck) are left out of the clash check. |
| **Raft 2 - The Drowned Frontier** | the file's order (story off, a chain of its own) | Nothing new. Cinderfall gives the machete blueprint and The Sunken Archive the detector blueprint; no island has vines, treasure or ziplines. |
| **Raft Remade** | the file's order (every story island replaced) | Nothing new. No Raft story-island stops are left; no features. |
| **CI Plan Recipe** | Radio Tower > Signal Rock > Stranded Gull (in Vasagatan's place) > Caravan Town > ... (Balboa left out) | Nothing new. The existing "Raft's blueprints never given" warning for Balboa's blueprints stays as before. |

Story-item lock order: no plan gets a new "lock before its key" warning, because every library island gives its own
keys. This is based on the explicit `has|story:` checks in the recipes. Doors placed with a ready piece's default
check come from macros that put their key on the same island.

## Content gaps found
- **Story id clash:** The Long Voyage, 'keycard' on The Drowned Metropolis and Mayor's Wharf. Suggested fix: rename
  one, e.g. Mayor's Wharf `wharfkeycard`, in `content/recipes/mayors_wharf.recipe` (its `story` line, loot and
  checks). Not done here: content was off limits.
- **Main quest steps needing a tool that is never given:** none in the library plans. No vines or tool-locked object
  stands in front of a main-quest target.
- **Optional finds before their tool** (fine by decision 3): Long Voyage / Wreckers' Cove treasure (detector at
  Caravan Town); Silver Screen / Gilded Skull Cove vines (machete at Balboa).

## Unsure
- **Shovel and zipline tool marked `Researched` (no warning ever).** I believe the shovel comes from Raft's research
  table. The zipline tool is less certain: `raft_blueprints.txt` has no story island with `Blueprint_ZiplineTool`,
  `ScrambledBlueprints.Keep` lists it, and several library islands give its blueprint. If it is really found on a
  story island (or given by a note), set `Researched = false` in `RaftFeatures.Tools`. Its blueprint is already
  filled in. Ziplines are never "main" today (they have no actions to follow), so this only changes the quiet
  Progression lines.
- **km/day placement:** "one story island every 4 km, a day = 2 km" is a rough guess (`KmPerStoryIsland`, `DayKm`).
  It decides where side-trip islands' gifts count. It matches the user's example (Old Mine Islet at km 28 comes after
  Caravan Town) but is not measured.
- **Beside / expedition rules** (Abyss) that start with the world all come before Raft's chain. In play they run
  alongside it.
- **"Main" detection** is a heuristic: show/open targets, a chest titled as an `open` step, a note titled as a `read`
  step, a zone of a `reach` step, a chest holding a `collect` item, or a `give` of a collected story item. Chains
  through signals or zones are not followed. The titanium machete (`DI_TitaniumMachete`) does not count as a machete,
  matching the vines' `has|Machete` check.
- **Clash check** uses islands that *give* the id. Generated map-type islands are skipped, though two generated
  islands in one world can give the same `raft-...` bolt cutters or generator part (`GenFeatures`).
- **Decision 4, runtime side** ("several can show as found at the same time, each tied to its own island"): this
  branch only changes the Check. I did not change how the notebook or 'Found items' show Raft's own quest items. Our
  locks already read only the crew's StoryBook.

## Checked by hand (no compiler)
- Every API called was grepped: `StoryChain.BuildSteps/IsRaft/TypeOfStep/RuleKey`, `StoryOrder.Parse/Name/Chain`,
  `ScrambledBlueprints.OnIslands/Read`, `QuestItemPickups.IdPrefix`, `ObjCheck.ParseLines/IsItem/Not/Target`,
  `ObjAction.ParseLines/Verb/Target/Arg`, `ObjectProps.Get/IsLoot/IsNote/NoteTitle/ZoneId/LootItems/TreasureKind`,
  `BehaviourProps.Name/Hidden/EventPrefix/CheckPrefix/EventKey/CheckKey`, `IslandQuest.KeyTitle/KeySteps/Steps`,
  `ContentCatalog.MacheteVines/MacheteItem/ZiplineItem/BuriedTreasure/TriggerZone/ItemLabel`, `UIKit.TextMuted`,
  `IslandNetwork.IsDownloadName`, `IslandSpawner.ListSavedIslands/PathFor`.
- C# 7.3 only. Lambda parameter names never clash with an enclosing local; a lambda reused as a foreach collection
  filter follows the file's existing pattern. Braces and brackets balance in every changed file.
- No `DevTests` use outside `DevTests*.cs`.

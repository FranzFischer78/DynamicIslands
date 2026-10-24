# Recipe format (.recipe)

A recipe builds an island, or a world plan, through the island editor's own operations, one step a line. Most steps are
undo steps, as when a person works in the editor. The player is `DevTests.RecipeRoutine` in `DevTestsRecipe.cs`
(dev builds only).

## Running one

- `CIRecipe <name>` (RML console or `dev_commands.txt`). It reads `Mods\DynamicIslands\recipes\<name>.recipe`
  (`DevTests.RecipeFolder`). The name may contain spaces.
- It opens the editor when needed (`WaitForEditor`), so it runs from the main menu or the editor.
- Sources are in the repo at `content\recipes\`. The game reads only `Mods\DynamicIslands\recipes\`, and nothing in the
  repo copies them there.
- Pass: `[CITEST] PASS: recipe <name> (<n> steps, <m> objects placed, [<k> left out under the ground, ]<t> s[, saved '<island>'])`.
- Fail: `[CITEST] FAIL: recipe <name>, <file>:<line> (<line text>): <error>`. The first error stops the recipe.
  An expand error is `[CITEST] FAIL: recipe <name>: <file>:<line> (<line>): <error>`.
- After a run, `DevTests.RecipeSaved` holds the saved island or plan name, and `DevTests.RecipeObjects` the last object
  placed under each alias (other tests read them).

## File structure

- Blank lines and lines whose first non-space character is `#` are skipped. A `#` later in a line is not a comment.
- Words are split at spaces. `"double quotes"` keep spaces together and are dropped (`Tokens`).
- Text arguments run to the end of the line (`Rest`); there the quotes stay. In text, `\n` is a new line (`Unescape`).
- Numbers use `.` as the decimal point. Step names ignore case.
- The whole file is first expanded (`ExpandRecipe` -> `ExpandLines`), then the steps run in order.

An island recipe (from `bell_stack.recipe`, shortened):

```
include lib_huts
new
gen Seed=9114 Radius=45 Height=22 Shape=3 Style=1
gen SeaFloor=0 Shelf=0.45
generate
brush flatten at 0 -26 r=10 s=20 frames=80 h=5
push at -6 -29 h=5
call floors w=2 d=2 y=0.1
place Block_Wall_Wood - at 0.75 0 y=0.1
roof 2 2 at 0 0 y=0.1 wood
place Note_Papers page2 at 1.2 1.2 y=0.2 drop yaw=30
pop
note page2 Chant, second page | II.\nThe pilgrims slept here and went home.
info title=The Bell Stack
prop bell obj.name=bell
quest title=The Torn Chant
step reach|landing|1|Land at the foot of the Bell Stack
save The Bell Stack
```

A world-plan recipe has only `plan` lines (from `plan_citest.recipe`):

```
plan new CI Plan Recipe
plan description A test plan made by the recipe player.
plan random off
plan story on
plan leaveout Balboa
plan rule rock | island:Signal Rock | start | receiver:500 | A weak signal crackles. | Signal Rock | after:RadioTower | quest
plan save
```

## Preprocessor (`ExpandLines`)

| Keyword | Meaning |
|---|---|
| `set <name> <value>` | A variable. The value is stored as a number when it works out as arithmetic, else as text. Names ignore case. |
| `$name` | Replaced by the variable (letters, digits, `_`). An unknown `$name` stays as it is. |
| `{expr}` | Worked out after `$` replacement, innermost braces first. |
| `for <v> <from> <to> [step]` ... `next` | A loop, `to` included. Step may be negative. Step 0 or more than 5000 rounds is an error. |
| `macro <name> <p1> <p2=default> ...` ... `end` | Defines a macro. Its body is expanded at each `call`. |
| `call <name> <args>` | Args by position or `param=value`. A missing param with no default, or one arg too many, is an error. |
| `include <file>` | Reads `<file>` from the recipes folder; `.recipe` is added unless it ends in `.recipe` or `.play`. It shares variables and macros with the file that includes it. |

- Arithmetic (`ExprParser`): `+ - * / %`, `^` (power), unary `-`, `( )`, `pi`, `sin cos tan` (degrees),
  `atan2(y,x)` (degrees), `sqrt abs min max floor ceil round`, `rnd(a,b)`.
- `rnd` is seeded from the top file's name, so a recipe gives the same numbers every run. `scatter` without `seed=` draws
  from the same generator.
- A `set` inside a `for` body or a macro call stays inside it. Nesting deeper than 40 is an error.
- Shared macro files: `lib_huts`, `lib_rt`, `lib_sea`, `lib_relay`, `lib_selene`, `lib_features`.

## Coordinates and units

- Metres and degrees. A point `x z` is measured from the recipe's origin, in the current frame.
- With no frame turned, `x` is the editor's world x and `z` its world z.
- The origin is (500, 500) at the start and after `new`. `generate` and `randomize` move it to the land centre
  (`EditorLandCentre`). `origin x z` sets it to an editor world point; `origin` alone sets it to the land centre.
- Heights (`HeightAt`): `h=` metres above the sea. `y=` metres above the frame's floor, or above the ground when the
  frame has no floor or `ground` is given. Neither: on the frame's floor if it has one, else on the ground.
- Frames: `push at x z [y=|h=] [yaw=] [ground]` ... `pop`. Points inside are turned with the frame and its yaw adds to
  the parent's. The frame has a floor when `y=` or `h=` is given, or when its parent has one (unless `ground`).

## Steps (`RecipeRoutine`)

| Step | Arguments | What it does |
|---|---|---|
| `new` | | A new island (`DynamicIslands.NewIsland`); origin reset, pending quests dropped. |
| `gen` | `Name=value ...` | Collects generator settings for the next `generate`/`randomize`. Several lines add up. |
| `generate` | | `IslandGenerator.GenerateInEditor` with the collected settings; origin = land centre. |
| `randomize` | `<part of a Raft island scene name> [like] [Name=value ...]` | `RaftIslands.VariationOf`, or `RaftIslands.LikeIt` with `like`, of the first `RaftIslands.Offered` island whose scene contains the text; then the sliders given. |
| `origin` | `[x z]` | See above. Clears the frames. |
| `push` / `pop` | see above | A frame for a building. |
| `style` | `Tropical`, `Snowy`, `Desert`, `Forest`, `Volcanic` or a number | The island's style (`StyleOf`). |
| `elevation` | `<m>` | Height above the sea, clamped to `IslandSpawner.MinElevation`..`MaxElevation`. |
| `island` | `key=value` | An island setting in `DynamicIslands.currentIslandProps`. Empty value removes it. |
| `info` | `key=value` | Same, with `info.` before the key: `info title=`, `info author=`, `info description=`. |
| `brush` | `<tool> at x z` or `<tool> from x1 z1 to x2 z2`, `[r=15] [s=4] [frames=30] [rot=] [h=]` | A terrain stroke (`Brush`). Tools: `raise lower flatten smooth sample sand grass rock seabed auto stamp:<i>`. r 2-80, s 0.5-20, frames 1-600. `h=`: flatten to that height above the sea. `rot=`: the stamp's turn (plus the frame's yaw). |
| `clear` | `at x z [r=5]` or `from x1 z1 to x2 z2 [r=5]`, `[all] [body] [only=<regex>] [below=] [above=] [on=sand\|grass\|rock\|seabed]` | Hides the editor objects in the circle or along the line, as one undo step. Without `all` the recipe's own objects stay. `body`: by how near each object's body (its renderers' box) comes, not its pivot (big look pieces whose pivot stands metres off them). `below`/`above`: height above the sea. |
| `store` | `[alias\|-] at x z [options]` | Raft's trading post as a working store (`place TradingPost ...`, SL7 `RaftStore.cs`): Raft's goods and prices, plus the story blueprints the player lacks (Trashcube*6, one each). Use `ground`. |
| `vending` | `[alias\|-] at x z [options]` | Tangaroa's vending machine, working (`place VendingMachine ...`, SL7b `RaftVending.cs`): Raft's goods and prices (Tangaroa tokens; place `QuestItemPickup_Tangaroa_Token` pickups near it). Use `ground`. |
| `place` | `<Object> <alias\|-> at x z [options]` | One object (`Place`). Object `none` places nothing (an empty macro slot). A Ready piece gets its working settings (`ReadyPieces.Defaults`, CB1) only with an alias (or `as=`); with `-` it stays decoration. |
| `scatter` | `<Object> <count> at x z r= [options]` | Random spots in the circle: dry land by default, `wet` under the sea, `any` both. `deep=` max depth, `free=` metres clear of editor objects, `seed=`, `scale=a-b`, `yaw=` fixed (else random), `as=<alias>`. |
| `line` | `<Object> from x1 z1 to x2 z2 [step=1.5] [ends] [yaw=] [y=\|h=] [sit] [follow]` | A row, each object's x axis along the line. `ends`: pieces at both ends. `follow`: end to end over the ground (`LineFollow`). |
| `ramp` | `<Object> from x1 z1 h1 to x2 z2 h2 [len=6] [top=1] [rot=]` | Pieces end to end between two heights (above the sea, or the frame's floor), tilted and stretched. `len`: piece length along its x; `top`: walking surface above its pivot. |
| `roof` | `<w> <d> at x z [y=\|h=] [wood]` | A hipped roof (`RaftRoof.Hip`) over w x d cells of the 1.5 m grid, its -x -z corner at x z. `at` must be the fourth word. |
| `beside` | `<Object> <alias\|-> <of-alias> [d=2] [yaw=] [turn=]` | On the ground d m in front of the first object of `of-alias` (its facing turned by `yaw`), facing it (+`turn`). |
| `prop` / `beh` | `<alias> key=value` | An object setting on every object of the alias (`PropsCommand.Change`). Empty value removes it. The alias is not the object's name: actions and `state` checks find objects by `obj.name` only. |
| `zipto` | `<alias> x z` | A zipline's far end on the ground there (the `zip.to` setting, `ZiplineEnds.ZipTo`). |
| `loot` | `<alias> <items>` | Chest loot (`ObjectProps.LootItems`), e.g. `Wool*4;Rope*4`. |
| `note` | `<alias> <title> \| <text>` | The note editor's Apply (`NoteEditorWindow.Apply`). |
| `story` | `id\|name\|icon\|description[\|raft=<Raft item>]` | A story item added, or replaced by id (`StoryItemDef.Parse`). |
| `quest` | `title=`, `intro=`, `done=` or `reward=` (one a line) | The island's quest. `quest2`..`quest9`: further quests. |
| `step` | `type\|target\|count\|text` | A quest step. Types are `IslandQuest.Types`: `reach read open kill catch collect pages blueprint` (`blueprint\|Blueprint_X\|1\|text`: one of Raft's blueprints carried or learned while at the island; empty target = any story blueprint). `step2`..`step9` for further quests. |
| `questbring` | `<rule line>` | `QuestEditorWindow.SetQuestBringRule`. |
| `rule` | `id \| what \| when \| where \| message \| label` | An island rule (`IntroRule.Parse`); the same id replaces. |
| `posts` | `[off]` | Posts (`RT_Pillar`, stretched) under each floor or deck corner nothing holds, down into the ground or sea floor (`StandPosts`, the check `FloorsOverNothing`, ROADMAP_DONE CA30). `save` does it too; `posts off` leaves them out at the save (a deck meant to hang). |
| `save` | `<island name>` | Stands posts under floors over nothing (unless `posts off`), then saves the island (`DynamicIslands.SaveIsland`); the name must pass `FileNames.IslandProblem`. Fails first when an action or `state` check names an object no object is named, or an `obj.name` holds a second key=value from a joined `prop` line (`UnnamedTargets`), or when a `step kill\|<kind>` / `step catch\|<kind>`, or a `kill <kind>` under `island <this island>` in any `.play` of the folder, names a creature label the island doesn't place (`UnplacedCreatures`). |
| `plan` | see below | World plans window. |
| `log` | `<text>` | Writes the text to the log. |
| `where` | `<alias>` | Logs where the alias's objects ended up (x z from the first origin, `h=` above the sea, turn). |
| `wait` | `[n]` | Waits n frames (default 1). |

`place` options (also passed on by `scatter`, `line`): `y=`, `h=`, `ground`, `yaw=`, `tilt=`, `roll=`, `rot=x,y,z` (own
turn instead of the straightened spawn turn), `keep` (keep the spawned turn), `scale=` (0.05-20), `sx=` `sy=` `sz=`
(stretch one axis, 0.05-20), `centred` (visible middle at the point), `sit` (bottom at the height, not the pivot),
`drop` (then set down on whatever is under it).

## World-plan steps

`plan new <name>` (a valid name, not a built-in plan; opens World plans via `WorldPlanWindow.RecipeNew`),
`plan description <text>`, `plan ending <text>`, `plan credits <title> | <name> | <name>...` (the credits' title and
the makers' names, one per line; `WorldPlanWindow.RecipeCredits`), `plan random on|off`, `plan story on|off`,
`plan leaveout <Raft story island>`, `plan rule <id | what | when | where | message | label [| story place | done when]>`,
`plan save`. `plan save` runs Check (`WorldPlanWindow.RecipeSave`), logs each finding as `check: ...`, fails on any
problem, needs the file at `WorldPlan.PathFor(name)`, then closes the window. Every `plan` line except `new` needs a
plan already open.

## Traps

- `wait` counts frames here; in a `.play` test it counts seconds.
- Every `{...}` on any line, note and message text too, is worked out as arithmetic; text that isn't fails the expand.
  A `$word` that names a variable is replaced in text too.
- `gen` names are the public field names of `IslandGenSettings`, case-sensitive. Unknown names and unreadable values are
  silently ignored (`IslandGenSettings.FromText`).
- Before the first step, objects named in `place`, `scatter`, `line` and `ramp` lines are loaded
  (`PlaceableCatalog.EnsureLoaded`). One the editor doesn't have fails the recipe before it starts.
- `prop`, `beh`, `loot`, `note`, `zipto` and `beside`'s `of-alias` fail unless the alias was placed earlier in the same
  run (by `place`, `beside` or `scatter as=`); `where` with an unknown alias logs nothing. Aliases ignore case.
- Quest fields are written only once the quest has a step. `save` fails when the main quest has a title and no steps.
- An object placed with alias `-` at a set height is left out when it is under the ground whole; so are `line` and
  `ramp` pieces. Objects on the ground with no height go down to the lowest ground under their base.
- `save` hides Raft's sea finds that the recipe's strokes lifted out of the water, and plants, rocks and finds buried
  whole.
- A strokes line after a `place` can leave that object floating or buried: shape the land first, then place. Check
  each island with `CIFloating` (editor) and `CIGroundIslands <name>` (world) - CHANGE_CHECKLISTS "Everything stands on
  the ground".
- `save` logs `play tests of '<island>' need 'offset X Z'` when the land centre moved from the origin. Put that
  `offset` line in the island's `.play` tests (see `PLAY_FORMAT.md`) whenever the recipe's land changes.

## Helpers for writing recipes

`CIMeasureObjects` (writes `recipes\objects_<category>.txt`), `CIHeightMap`, `CISeaSpots`, `CIProfile`, `CIBoundsOf`,
`CIColliders`, `CIFloating`, `CIView` (writes `recipes\view_<name>_<n>.jpg`), `CIViewAt`, `CIProtoInfo`,
`CIQuestIcons` (`questicons.txt`), `CISounds` (`sounds.txt`), `CIScene`, `CIInstances`. All are in `DevTestsRecipe.cs`.
Object and item lists for writing recipes are in the repo at `content\objects\`.

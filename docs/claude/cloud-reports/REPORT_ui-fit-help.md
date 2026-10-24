# REPORT: ui-fit-help

Branch `cloud/ui-fit-help`, from `origin/content` (09161a8). **None of this was compiled or run.** The session has no C#
compiler and no Raft, so everything was checked by reading the code. Run `compile.ps1` and `compile.ps1 -Release` first.

## What changed

### 1. World plans: the bottom buttons are now two rows

**The cause:** the old row (`WorldPlanWindow.cs` ~353-369) had fixed minimum widths that added up to about 1214 px. The
panel has 1168 px inside, so Close stuck out past the right edge.

**The new layout** (`WorldPlanWindow.cs:355-378`): a "Buttons" box with two rows inside it, everything visible:
- `PlanButtons` (work on the plan): + Add a rule, Check, ? (Check), Progression, Preview notebook, Test this plan. About
  708 px wide.
- `FileButtons` (the plan's files): Export..., Import..., ? (Export/Import), a gap, Save, Close. About 498 px wide.

**Island rules mode is unchanged:** it still hides Progression, Preview, Export, Import and their ? (`Show`, :158-160),
which leaves + Add a rule, Check and its ? on row 1, and Save and Close on row 2.

**Sizes** (constants at `WorldPlanWindow.cs:282`):
- The buttons are 30 px high (they were 34), the rows are 6 px apart, and the box is 66 px high.
- The body (the rules list and the map column) is now 408 px high instead of 440. The problems text under the map is 128
  px instead of 160 (`:336-353`).
- The window's total height is therefore the same as before, about 765 px of the 800 a 16:9 screen gives the mod's
  canvas. Without this trim, the second row would have pushed the window past 800.

### 2. `UIKit.RowOverflow(window, screen, uiScale)` (`UIKit.cs:339-415`)

This is a dev/test helper. It works out the canvas size that a screen of that size gives (from the root canvas's
`CanvasScaler`, the same way Unity does) and returns one line for each problem it finds:
- the window is past the edge of the screen;
- a button or ? lies outside the window;
- a button or ? is squeezed below 4 px.

Buttons inside a masked scroll list are only checked across (side to side). Scrolled out of view up or down is fine.
Nothing in the game calls it; only the tests do.

### 3. Tests (new file `DevTestsUiFit.cs`, plus its `<Compile Include>` in `DynamicIslands.csproj:72`)

**`CIButtonsInside`** (Dev, editor) opens these windows in turn:
- Islands, My islands
- the generator's 3 tabs
- World plans (Adventure) and the island's rules
- Plan check, Progression and New main story (each over World plans)
- quest, island events, story items, pieces, stamps, placed list
- item picker, sound picker, choice, name prompt
- Share (Export), Import, the island library, an info box

It checks each one with `RowOverflow` at 11 screen sizes (1024x768 to 3840x2160, including 21:9 and 16:10) and UI scales
1 and 0.8. For World plans it also checks that Test this plan is in the first row and Close in the second, and takes a
picture (`shot_fit_worldplans`).

**`CIIslandFilesHelp`** (Dev, editor) checks the Islands window:
- each of the four ? marks is there and has a real text;
- the height's text names 0, 60, -30 and the Island tab;
- each ? sits inside the window;
- hovering each one shows its own text, and moving off closes the popup;
- `RowOverflow` finds nothing at 1920x1080.

### 4. Other windows' button rows

I added up the fixed widths of every row in every editor window. Only World plans' bottom row was over its window's
width. The next widest are World plans' plan row (about 944 of 1168), Share's buttons (532 of 788) and New main story's
buttons (524 of 864).

Buttons without a fixed width shrink, because their minimum width is 0, so they can't push a row out. Whatever this
static count missed (texts, rows built at run time) is what `CIButtonsInside` finds in game. If it fails, its FAIL line
names each button and the window it sticks out of.

### 5. ISLANDS window: "?" marks (`IslandFilesWindow.cs`)

**Labels row** (`:97-107`): `Name` (40 px), then its ?, then a gap, then `Height in the world (m)` and its ?. The height
label is 124 px wide and shrinks its text to fit, so the label and its ? together are as wide as the height field below
(124 + 8 + 18 = 150). The row is now 18 px high (it was 16).

**List** (`:129-132`): a new line above the list, "Click an island to pick it, double-click to open it", with a ?.

**Share row** (`:146`): a ? after My islands..., covering Export..., Import..., Library... and My islands....

**The texts** are the public consts `HelpName`, `HelpHeight`, `HelpList` and `HelpShare` (`:163-182`). The height's text
covers everything in the request:
- 0 = at sea level;
- above 0 = flying (60): build stairs or pillars up from the raft, and a raft fits underneath;
- below 0 = sunk (-30): dive to it;
- from -100 to 250 m;
- saved with the island, and Ctrl+Z undoes a change;
- it is the same value as the Island tab's Height (both fields edit `currentElevation`).

## Doc lines to add (I did not edit GUIDE, README or CHANGELOG)

**CHANGELOG, under the newest entry:**
- World plans: the buttons at the bottom are two rows: working on the plan (+ Add a rule, Check, Progression, Preview
  notebook, Test this plan), and its files (Export..., Import..., Save, Close). Close stuck out past the window's edge on
  one row.
- The Islands window: a ? for Name, Height in the world, the island list and the sharing buttons. The height's ? says what
  0, above 0 (flying) and below 0 (sunk) mean, and how players reach the island.
- Dev: `CIButtonsInside` (every button of the editor's windows inside its window, at 11 screen sizes) and
  `CIIslandFilesHelp`.

**GUIDE, where the Islands window is described:**
> Each part has a **?**: hover it for what Name, Height in the world, the list and the sharing buttons do. Height in the
> world is the same as the Island tab's Height.

**GUIDE, World plans section (wherever the bottom buttons are listed):**
> The buttons at the bottom are in two rows: **+ Add a rule**, **Check**, **Progression**, **Preview notebook** and
> **Test this plan** above; **Export...**, **Import...**, **Save** and **Close** below.

**README, Source overview, the line for `DevTests*.cs` or `UIKit.cs`:**
> `UIKit.RowOverflow`: finds buttons outside their window (tests).

**docs/claude/areas/testing.md, Editor tests row:** add `CIButtonsInside` and `CIIslandFilesHelp`.

## What to test in game

1. Run `compile.ps1`, then `compile.ps1 -Release`. The release build must compile without `DevTestsUiFit.cs`; nothing
   outside the `DevTests*.cs` files uses it.
2. `CIEditor`, then `CIButtonsInside`. Any FAIL names the window and button. Look at `shot_fit_worldplans.png`: two rows,
   with Close inside the right edge.
3. `CIIslandFilesHelp`.
4. `CIPlanHelp` (it counts World plans' ? marks: still 8 for the window's parts, unchanged) and `CIButtons` (it presses
   every button, including the 4 new ?).
5. By eye at 1920x1080 and 1280x720:
   - World plans: the two bottom rows; the map column with the problems text (now 128 px high); the window not touching
     the screen's top or bottom.
   - The Islands window: the label row lines up with the Name and Height fields under it.

## Unsure

- **UI scales:** the mod has no UI scale setting. Its canvases always fit a 1400 x 800 layout (`CanvasScaler` Expand), so
  the test checks scale 1 and 0.8 (a smaller UI). Above 1, the 1200 px World plans window and the 1240 px library could
  not fit by design.
- **Window position:** `RowOverflow` assumes the window is centred on its canvas, as every editor window is. For a window
  anchored elsewhere, its screen-edge check would be rough.
- **Problems text:** under the map it is 32 px shorter. Long lists of findings are cut sooner, but Check's own window
  (Plan check) shows them all.
- **Window height:** the World plans height (about 765 px) is an estimate from the row heights. `CIButtonsInside` checks
  the real one.
- **Name rules:** `HelpName` says which characters a name can't have, from `FileNames.IslandProblem`. Check that this
  matches what the game says.

## Merge notes (2026-10-16)

- Compile fix: `UIKit.RowOverflow` and its helpers used `Rect`, which inside `UIKit` is the `Rect(string, Transform)`
  method; now `UnityEngine.Rect`.
- `CIButtonsInside` looked for a button named `Button_Test this plan`; the button is `Button_TestPlan`.
- `HelpName` said 64 characters; `FileNames.MaxLength` is 60.
- In game: `CIButtonsInside` 26/26, `CIIslandFilesHelp` 17/17, `CIPlanHelp` 15/15. `CIButtons` hangs Raft (no output,
  not responding) on this PC's ~750 islands - the same on origin/content without this branch, so not caused by it.

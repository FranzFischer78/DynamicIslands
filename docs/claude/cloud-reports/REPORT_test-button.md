# Report: the editor's Test button (branch cloud/test-button)

The bug: the first click on Test left the user in the main menu with the Load game box open. After that, Test did
nothing. In Player.log, `LoadGameBox.Button_SelectLoad` (and Raft's own `LoadGameBox.LoadInSavedGames`) threw a
NullReferenceException inside `MenuBox.IsOpen`, because the box's `versionControlBox` was a destroyed object. The
exception ended the `OpenWorld` coroutine and left the state at `Opening`, so `Busy` stayed true for up to 240 s and
`Start` ignored every click.

Nothing here was compiled or run: there is no Raft and no C# compiler in this environment.

## What changed

### `DynamicIslands/IslandTest.cs` (LF line endings, as before)

- **(a) A start can't stay Busy.**
  - `Guarded` (:259) runs a coroutine one `MoveNext` at a time inside try/catch. A coroutine can't yield inside a try
    with a catch, so the catching happens here. An exception in any step calls `onFail`. The loop also ends as soon as
    the state isn't the one it runs in (after Cancel or Abort).
  - `OpenWorld` runs under it with `OpenWorldFailed` (:281, which calls `Abort` and names the step). `BringIsland` runs
    under it with `BringIslandFailed` (:286). That one only shows a notice: the world is already in, so the player
    stays in it and Back to the editor still works.
  - `Abort(reason)` (:63) sets `LastFailure` and goes back to the editor. From the main menu, or on the way to it,
    the state becomes `Returning`; the editor then opens again with the island (or World Plans for a plan test). From
    a world that is in, it leaves the way Back does. On the way back already, it only stops.
  - Once the editor is in again (:242), the reason shows in the status bar (`EditorUI.Flash`, 12 s) and as a notice.
  - Every early exit in `OpenWorld` now calls `Abort`, not `Stop`.
  - The timeout (:190) now goes through `Abort`, and it no longer runs while `Testing`. Before, `giveUpAt` (240 s
    after Test) also applied while testing: after 4 minutes in the test world the state went to None and "Back to the
    editor" disappeared from the world window. I fixed this as well.
  - `Returning` waits 1 s on the main menu before it opens the editor, so the menu is settled after an abort.
- **(b) Raft's boxes are never kept between steps.**
  - `LiveBox<T>()` (:296) finds the `LoadGameBox` or `NewGameBox` again at every step and after every wait. It looks in
    loaded scenes, MainMenuScene first.
  - Each time, `MendDeadLinks` (:313) checks every field of the box and its base classes (static fields too) that
    holds a Component. A field pointing at a destroyed object gets the live object of the same runtime type from the
    loaded scenes; it is logged, and a missing replacement is logged only once. This is aimed at the dead
    `versionControlBox`, and it runs before `Open()` too, so Raft's own `LoadInSavedGames` should fill the list again.
  - `PickWorld` (:339) calls `Button_SelectLoad` in a try. If the pick didn't take (`selectedGame` and
    `SaveAndLoad.WorldToLoad` are not the test world), it sets those two directly by reflection (`SetMember`, :351,
    using HarmonyLib's `AccessTools`). These are the two things CILoadWorld already checks after `Button_SelectLoad`.
- **(c) The cover.**
  - `TestCover` (:504) is a full-screen, opaque canvas with sorting order 32100 that takes every click. It is in the
    editor loading box's look, kept through scene changes with `DontDestroyOnLoad`, and shows "STARTING THE TEST",
    what is being started, the current step and a Cancel button.
  - It goes up in `Go` and comes down when the world is in (`InWorld` -> `Testing`). On the way back to the editor,
    it comes down right after `LoadEditor`, whose own loading box covers from there. Its ticker removes it whenever no
    test is starting.
  - Raft's main menu, Load and New Game boxes still open, but under the cover.
  - `Cancel` (:80): in `ToMenu`/`Opening` it aborts at once. In `InWorld` (Raft is already loading the world) it marks
    the start cancelled; once the world is in, it leaves it as Back does and returns to the editor. Raft's world load
    can't be stopped half way, so Cancel waits it out.
- **For tests:** `Starts` (:40), `LastFailure`, `FailNextStartForTests` (:42; it throws a NullReferenceException in
  the first main-menu step, :371) and `TestCover.Showing` / `CancelShown`.

### `DynamicIslands/DevTestsPolish.cs`

New command `CITestButton` (:253, routine `TestButtonRoutine` :280). Run it in the editor:

1. A forced failure. Test is clicked twice in one frame: it starts once and the cover is up. The start then goes back
   to the editor with the island open, `Busy` false, and a `LastFailure` naming the NullReferenceException.
2. Test clicked twice, then Cancel in the main menu: back to the editor, `LastFailure` = "Test cancelled".
3. Test clicked twice again: it starts once, reaches the test world with the island, and the cover is gone.
4. Every frame while a test starts: if the main menu is there and neither the cover nor the editor's loading box is
   up, that frame counts as uncovered. The test expects 0.
5. Back, then the editor again. The test island is deleted.

Run: `ci.ps1 -Command "CITestButton" -Timeout 900` (after `CIEditor`).

### `docs/claude/areas/editor.md`

The IslandTest row now names `Guarded`, `Cancel`, `Abort` and `TestCover`, and there is a new trap line about Busy
and live boxes.

## Doc lines to add (not edited here)

- **CHANGELOG.md:** "Fixed: Test in the editor sometimes went to the main menu with Load game open and then did
  nothing on later clicks (Raft's Load box had a broken link after the editor). A test now starts behind a cover with
  Cancel. If it can't start, you are back in the editor and the status bar says why. Back to the editor no longer
  disappears after 4 minutes in the test world."
- **GUIDE.md 4.9 Test:** "While the test starts, a STARTING THE TEST box covers the screen (Raft's main menu loads the
  test world behind it). Cancel takes you back to the editor. If the test can't start, you are back in the editor with
  the reason in the status bar - click Test again."
- **README.md source overview, IslandTest.cs:** add "; the cover while it starts (TestCover, Cancel); a start that fails
  goes back to the editor with the reason".

## To test in game

1. The user's case: open the editor from the main menu (several times if needed), make or open an island, click Test.
   The cover should stay up the whole time, with no main menu or Load box visible, until you are in the test world
   next to the island.
2. Player.log: look for `[CUSTOM ISLANDS] [test] Raft's LoadGameBox.versionControlBox pointed at a destroyed ...`.
   - "now at the live one": the mend worked.
   - "there is no live one": the pick fallback (`Raft's pick didn't take: the test world set directly`) has to carry
     it, and `Button_LoadGame` may still fail. You should then be back in the editor with the reason, and clicking
     Test again should do something.
3. Click Cancel at each stage: right after Test, while the Load list fills, and while Raft's loading screen shows. Each
   should end in the editor.
4. Stay in the test world more than 4 minutes, then Esc > Custom Islands: Back to the editor is still there.
5. Test this plan and Preview notebook (World Plans) work as before, with the same cover.
6. `CITestButton`, `CIIslandTest`, `CIPlanTest` and `CIQuestBook` (preview).

## Unsure

- **The root cause.** I don't have Raft's source here. I don't know whether `versionControlBox` is an instance field,
  a static field, or something read from `ComponentManager<T>`. `MendDeadLinks` covers instance and static fields; if
  it is read through `ComponentManager<>`, it won't help, and the reflection fallback in `PickWorld` plus `Abort` are
  what is left. Then `Button_LoadGame` may still throw (it may also look at the version control box): the user gets
  back to the editor with a reason instead of being stuck, but the test still doesn't start. The Player.log line from
  test step 2 tells which case it is.
- **`SaveAndLoad.WorldToLoad` and `LoadGameBox.selectedGame`.** I don't know if they are fields or properties, or
  whether they can be set; `SetMember` tries both. If `Button_SelectLoad` throws before it turns the Load button on,
  the button stays off, and the abort says "Raft's Load button is off (is Steam online?)", which would be misleading
  in that case.
- **Step 2, loading the save directly (not implemented, not clearly safe).** This would replace the Load box with a
  direct load:
  1. Read the test world's newest `.rgd` (`SaveAndLoad.Load<RGD_Game>`, as CILoadWorld's backup path does).
  2. Set `SaveAndLoad.WorldToLoad`.
  3. Start the game the way `LoadGameBox.Button_LoadGame` does internally (most likely a `LoadSceneManager` /
     `Raft_Network` host call with the world name).

  This needs `Button_LoadGame` decompiled (dnSpy on Assembly-CSharp.dll) to copy its exact calls: Steam lobby setup,
  game mode, password, `CurrentGameFileName`. With that, the main menu wouldn't be needed for Load at all. A new world
  still needs `NewGameBox`, because of the mod's Harmony patch on its `Open` and the plan/randomizer panels.
- **A cancel or failure after `Button_LoadGame` or `Button_CreateNewGame` started the load but threw.** `Abort` sends
  the state to `Returning`, which waits for the main menu. Raft may finish loading the world instead. Then the timeout
  (120 s) stops the test and the player is left in the test world without Back to the editor. This is rare, and the
  player can still leave by Raft's own menu.
- **`WorldDirector.PendingPlan`** stays set ("No custom islands") when Create throws after it was set. The same was
  true before this change.
- **The normal Back from a test** still shows the main menu for a moment before the editor's loading box. Only the
  start is covered.

## Merge notes (2026-10-16)
- Merged into content by the merge agent; builds OK (Debug and Release).
- Review: Guarded, Abort, LiveBox/MendDeadLinks and TestCover all run on the main thread; MendDeadLinks only rewrites fields whose object is destroyed. No code changes needed in IslandTest.cs.
- Test fix: CITestButton step 2 waited for LastStep "In the main menu", which lasts no frame (the next step follows at once), so Cancel came only after the world was in. It now waits for the main menu canvas.
- Tests (Raft, merged build): CITestButton 13/13 (the Load path; the New Game path ran in an earlier run), CIIslandTest 8/8, CIPlanTest 8/8. The versionControlBox mend line did not show: the user's bug did not happen here, so the mend itself is untested.

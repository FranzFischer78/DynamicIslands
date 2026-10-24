using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of ROADMAP.md step 1: the level up system as a World setting and a host switch (T5), the editor's keys (E5),
	/// the editor's object limit (E7).
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CILevelsSwitch", docs: "Dev, world (host, a test world 'CI ...'): the level up system switched on and off by the host - off keeps everyone's levels (world file @levels=off with the records), an island made with levels doesn't switch it back on, on brings the levels back. What the world had is put back after")]
		public static void LevelsSwitchCommand() { StartTest(LevelsSwitchRoutine()); }

		static IEnumerator LevelsSwitchRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("levels switch: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			bool wasOn = PlayerLevels.On, wasOff = PlayerLevels.OffByHost;
			try
			{
				PlayerLevels.SetEnabled(true);
				yield return null;
				LevelRecord mine = PlayerLevels.Mine;
				Check(ref ok, PlayerLevels.On && mine != null, "switched on by the host");
				int xpBefore = mine != null ? mine.Xp : 0;
				if (mine != null) { mine.Xp += 250; }
				int xp = mine != null ? mine.Xp : 0;
				PlayerLevels.SetEnabled(false);
				yield return null;
				List<string> lines = PlayerLevels.WriteLines().ToList();
				Check(ref ok, !PlayerLevels.On && PlayerLevels.OffByHost && PlayerLevels.Mine == null, "switched off: no level for anyone now");
				Check(ref ok, lines.Contains("@levels=off") && lines.Any(l => l.StartsWith("@level=")), "the world file keeps it off, with everyone's levels (" + string.Join(" ", lines.ToArray()) + ")");
				PlayerLevels.SetEnabled(true);
				yield return null;
				Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == xp, "on again: the level is back (" + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp : -1) + " EXP)");
				if (PlayerLevels.Mine != null) PlayerLevels.Mine.Xp = xpBefore;
			}
			finally
			{
				if (wasOn) PlayerLevels.SetEnabled(true);
				else if (wasOff) PlayerLevels.SetEnabled(false);
				else PlayerLevels.TurnOff();
			}
			if (ok) Log("PASS: levels switch"); else Fail("levels switch");
		}

		[ConsoleCommand(name: "CILevelsChoice", docs: "Dev, main menu: the World settings window's Level up system switch - clicked on and off (its label, the box's count of changed settings), Raft's own switches it off; the choice is put back after")]
		public static void LevelsChoiceCommand() { StartTest(LevelsChoiceRoutine()); }

		static IEnumerator LevelsChoiceRoutine()
		{
			bool ok = true;
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("levels choice: at the main menu"); yield break; }
			bool before = PlayerLevels.Chosen;
			try
			{
				try { box.Close(); } catch { }
				box.Open();
				yield return new WaitForSecondsRealtime(0.5f);
				WorldSettingsWindow.Open();
				yield return null;
				UnityEngine.UI.Button toggle = WorldSettingsWindow.LevelsToggle;
				Check(ref ok, toggle != null && toggle.gameObject.activeInHierarchy, "World settings has a Level up system switch");
				PlayerLevels.Chosen = false;
				WorldSettingsWindow.Close(); WorldSettingsWindow.Open();
				int changed = WorldSettingsWindow.Changed;
				if (toggle != null) toggle.onClick.Invoke();
				yield return null;
				Check(ref ok, PlayerLevels.Chosen && UIKit.LabelOf(toggle).text.EndsWith("ON") && WorldSettingsWindow.Changed == changed + 1, "clicked: on, the label says so, one more setting changed (" + UIKit.LabelOf(toggle).text + ")");
				WorldSettingsWindow.RaftsOwn();
				yield return null;
				Check(ref ok, !PlayerLevels.Chosen && UIKit.LabelOf(toggle).text.EndsWith("off"), "Raft's own: off");
				WorldSettingsWindow.Close();
			}
			finally
			{
				PlayerLevels.Chosen = before;
				WorldSettingsWindow.Close();
				try { box.Close(); } catch { }
			}
			if (ok) Log("PASS: levels choice"); else Fail("levels choice");
		}

		[ConsoleCommand(name: "CIWorldWindow", docs: "Dev, world (either player; host: a test world 'CI ...'): the pause menu's CUSTOM ISLANDS button is there; the world window opens - the host clicks Savage, build cost +5 %, the randomizer Light and one part off, an extra option, the level up system and one island of the list, and the world's settings follow (put back after); a player who joined sees the host's settings and can't change them. Picture shot_world_window.png")]
		public static void WorldWindowCommand() { StartTest(WorldWindowRoutine()); }

		static IEnumerator WorldWindowRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("world window: in a world"); yield break; }
			bool host = Raft_Network.IsHost;
			if (host && !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("world window: a test world 'CI ...'"); yield break; }
			bool ok = true;
			Check(ref ok, Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>().Any(b => b != null && b.name == WorldWindow.ButtonName && b.gameObject.scene.IsValid()), "Raft's pause menu has the Custom Islands button");
			int monsters = MonsterDifficulty.Current, cost = BuildCost.Current;
			RandomizerSettings rnd = WorldRandomizer.Current.Copy();
			var options = new HashSet<string>(WorldOptions.Current);
			bool levels = PlayerLevels.On, levelsOff = PlayerLevels.OffByHost;
			string island = host ? WorldIslands.Candidates().FirstOrDefault(WorldIslands.TakesPart) : null;
			WorldWindow.Open();
			yield return null;
			try
			{
				Check(ref ok, WorldWindow.IsOpen, "the world window opens");
				Func<string, UnityEngine.UI.Button> b = WorldWindow.ButtonNamed;
				if (!host)
				{
					Check(ref ok, b("Monsters_Savage") != null && !b("Monsters_Savage").interactable && !b("Option_ghostrafts").interactable && !b("Levels").interactable, "a player who joined can't change the host's settings");
					Check(ref ok, UIKit.LabelOf(b("BuildCost_Value")).text == BuildCost.Describe(BuildCost.Current), "... and sees the host's build cost: " + UIKit.LabelOf(b("BuildCost_Value")).text);
				}
				else
				{
					b("Monsters_Savage").onClick.Invoke();
					b("BuildCost_More").onClick.Invoke();
					b("Randomizer_Light").onClick.Invoke();
					b("Part_" + RandomizerSettings.Features[0]).onClick.Invoke();
					b("Option_ghostrafts").onClick.Invoke();
					b("Levels").onClick.Invoke();
					if (island != null) b("Island_" + island).onClick.Invoke();
					yield return null;
					Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Savage, "Savage clicked: monsters are Savage in this world");
					Check(ref ok, BuildCost.Current == Mathf.Min(BuildCost.Max, cost + BuildCost.Step), "+5 %: the build cost is " + BuildCost.Current + " %");
					Check(ref ok, WorldRandomizer.Current.Level == 1 && WorldRandomizer.Current.Disabled.Contains(RandomizerSettings.Features[0]), "the randomizer Light, " + RandomizerSettings.FeatureLabels[0] + " off");
					Check(ref ok, WorldOptions.On(WorldOptions.GhostRafts) != options.Contains(WorldOptions.GhostRafts), "ghost rafts switched");
					Check(ref ok, PlayerLevels.On != levels, "the level up system switched");
					Check(ref ok, island == null || !WorldIslands.TakesPart(island), "the island '" + island + "' left out of this world");
					Check(ref ok, UIKit.IsChecked(b("Option_ghostrafts")) == WorldOptions.On(WorldOptions.GhostRafts), "the window shows it: ticked " + UIKit.IsChecked(b("Option_ghostrafts")));
					Check(ref ok, island == null || UIKit.IsChecked(b("Island_" + island)) == WorldIslands.TakesPart(island), "the island's row shows it");
				}
				// (the islands list was taken out 2026-10-10: the quest book shows where each island's quest is)
				Check(ref ok, GameObject.Find("IslandsHere") == null, "no 'Islands in this world' box (the quest book shows them)");
				Screenshot(new[] { "world_window" });
				yield return new WaitForSeconds(1f);
			}
			finally
			{
				if (host)
				{
					MonsterDifficulty.Set(monsters);
					BuildCost.Set(cost);
					WorldRandomizer.Set(rnd);
					WorldOptions.Set(options);
					if (levels) PlayerLevels.SetEnabled(true); else if (levelsOff) PlayerLevels.SetEnabled(false); else PlayerLevels.TurnOff();
					if (island != null) WorldIslands.Set(island, true);
				}
				WorldWindow.Close();
			}
			if (ok) Log("PASS: world window"); else Fail("world window");
		}

		[ConsoleCommand(name: "CIEditorLight", docs: "Dev, editor: the Light list: Morning, Noon, Evening, Night, Overcast - a directional sun in the scene, Raft's sun setting and the sky's light follow; pictures shot_light_<time>.png; the player's choice is put back")]
		public static void EditorLightCommand() { StartTest(EditorLightRoutine()); }

		static IEnumerator EditorLightRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("editor light: in the editor"); yield break; }
			bool ok = true;
			int before = EditorLighting.Current;
			UnityEngine.UI.Button button = EditorUI.Canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(x => x.name == "Button_Light");
			Check(ref ok, button != null && UIKit.LabelOf(button).text == "Light: " + EditorLighting.Names[EditorLighting.Current], "the top bar's Light button says the time of day: " + (button != null ? UIKit.LabelOf(button).text : "none"));
			var ambients = new List<Color>();
			for (int i = 0; i < EditorLighting.Names.Length; i++)
			{
				EditorLighting.Apply(i, false);
				EditorUI.RefreshLight();
				yield return new WaitForSeconds(0.4f);
				Light sun = RenderSettings.sun;
				ambients.Add(RenderSettings.ambientSkyColor);
				Check(ref ok, sun != null && sun.type == LightType.Directional && sun.isActiveAndEnabled && sun.shadows != LightShadows.None, EditorLighting.Names[i] + ": a sun with shadows, " + (sun != null ? sun.intensity.ToString("0.00") + " bright, " + sun.transform.eulerAngles.x.ToString("0") + "° up" : "none"));
				Screenshot(new[] { "light_" + EditorLighting.Names[i].ToLowerInvariant() });
				yield return new WaitForSeconds(0.8f);
			}
			Check(ref ok, ambients.Distinct().Count() == EditorLighting.Names.Length, "each time of day has its own sky light");
			bool picked = DropList.Click(button, "0");
			Check(ref ok, picked && EditorLighting.Current == 0 && UIKit.LabelOf(button).text == "Light: Morning", "the button is a list: picking Morning in it (" + (button != null ? UIKit.LabelOf(button).text : "none") + ")");
			EditorLighting.Apply(before, true);
			EditorUI.RefreshLight();
			if (ok) Log("PASS: editor light"); else Fail("editor light");
		}

		[ConsoleCommand(name: "CIViewPlan", docs: "Dev, main menu (the New Game box open, e.g. after CINewGameBoxShot): ROADMAP T6 - the plan box's View... shows what the chosen plan does; picture shot_view_plan.png")]
		public static void ViewPlanCommand(string[] args) { StartTest(ViewPlanRoutine()); }

		static IEnumerator ViewPlanRoutine()
		{
			bool ok = true;
			string name = WorldPlan.All().FirstOrDefault(n => n == "The Long Voyage") ?? WorldPlan.All().FirstOrDefault(n => !WorldPlan.IsBuiltIn(n));
			WorldPlan p = name != null ? WorldPlan.Load(name) : null;
			string text = NewWorldOptions.PlanText(p);
			Check(ref ok, p != null && p.Rules.Count > 0 && text.Split('\n').Count(l => l.StartsWith("• ")) == Mathf.Min(24, p.Rules.Count), "the plan's text lists its rules (" + (p != null ? p.Rules.Count : 0) + ")");
			string keep = WorldDirector.PendingPlan;
			WorldDirector.PendingPlan = name;
			// (Raft's New Game box opened, as a player clicks New Game)
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded);
			if (box != null && !box.gameObject.activeInHierarchy) { box.gameObject.SetActive(true); try { box.Close(); } catch { } box.Open(); yield return new WaitForSecondsRealtime(1.5f); }
			UnityEngine.UI.Button view = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == "Button_ViewPlan" && b.gameObject.activeInHierarchy);
			Check(ref ok, view != null, "the New Game box's plan has a View... button");
			if (view != null)
			{
				view.onClick.Invoke();
				yield return new WaitForSecondsRealtime(0.6f);
				Screenshot(new[] { "view_plan" });
				yield return new WaitForSecondsRealtime(0.6f);
			}
			WorldDirector.PendingPlan = keep;
			InfoWindow.Close();
			if (box != null) try { box.Close(); } catch { }
			if (ok) Log("PASS: view plan"); else Fail("view plan");
		}

		[ConsoleCommand(name: "CITestThisPlan", docs: "Dev, editor: ROADMAP T2b - World Plans' Test this plan: a small plan saved, a new world 'Plan test <time>' made with it, its island comes, Back to the editor in the world window, World Plans open again on the plan")]
		public static void TestThisPlanCommand(string[] args) { StartTest(TestThisPlanRoutine()); }

		static IEnumerator TestThisPlanRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("plan test: in the editor"); yield break; }
			bool ok = true;
			string island = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().FirstOrDefault(n => !n.StartsWith("gen-") && !n.StartsWith("ci"));
			var plan = new WorldPlan { Name = "CI Plan Test", Random = false, Description = "Test this plan (CIPlanTest)" };
			plan.Rules.Add(IntroRule.Parse("first | island:" + island + " | start | ahead:300 | A first island | First"));
			plan.Save();
			IslandTest.StartPlan(plan.Name);
			Check(ref ok, IslandTest.Busy && IslandTest.PlanToTest == plan.Name, "Test this plan: on its way");
			for (float t = 0; t < 300f && !IslandTest.Testing; t += 1f) yield return new WaitForSeconds(1f);
			for (float t = 0; t < 40f && !IslandWorldState.Islands.Any(x => x.HostName.Equals(island, StringComparison.OrdinalIgnoreCase)); t += 1f) yield return new WaitForSeconds(1f);
			string world = SaveAndLoad.CurrentGameFileName ?? "";
			Check(ref ok, IslandTest.Testing && world.StartsWith("Plan test"), "in a new world '" + world + "' (" + IslandTest.LastStep + ")");
			Check(ref ok, WorldDirector.PlanName == plan.Name, "the world's plan is '" + WorldDirector.PlanName + "'");
			Check(ref ok, IslandWorldState.Islands.Any(x => x.HostName.Equals(island, StringComparison.OrdinalIgnoreCase)), "the plan's first island '" + island + "' came");
			WorldWindow.Open();
			yield return null;
			UnityEngine.UI.Button back = WorldWindow.ButtonNamed("BackToEditor");
			Check(ref ok, back != null && back.gameObject.activeInHierarchy, "the world window offers Back to the editor");
			if (back != null) back.onClick.Invoke();
			for (float t = 0; t < 240f && !(DynamicIslands.InEditor() && !IslandTest.Busy); t += 1f) yield return new WaitForSeconds(1f);
			yield return new WaitForSeconds(2f);
			Check(ref ok, DynamicIslands.InEditor() && WorldPlanWindow.IsOpen, "back in the editor with World Plans open");
			WorldPlanWindow.Close();
			// (the test plan isn't left in the players' plan list - the user, 2026-10-06)
			try { if (System.IO.File.Exists(WorldPlan.PathFor(plan.Name))) System.IO.File.Delete(WorldPlan.PathFor(plan.Name)); } catch { }
			if (ok) Log("PASS: plan test"); else Fail("plan test");
		}

		[ConsoleCommand(name: "CITestButton", docs: "Dev, editor: the editor's Test button never stuck (the user, 2026-10-16: Raft's Load box threw, the main menu stayed and Test did nothing after) - Test clicked twice in a row starts once; a start made to fail in the main menu goes back to the editor with the reason and Test works again; Cancel goes back to the editor; Test clicked twice again: the test world with the island; while a test starts no main menu shows (the cover is over it). Several minutes; the test island is deleted after")]
		public static void TestButtonCommand() { StartTest(TestButtonRoutine()); }

		/// <summary>Frames the main menu showed while a test started without the cover (or the editor's loading box) over it.</summary>
		static int testButtonUncovered;

		/// <summary>Waits until done (or the time is up), counting frames the main menu was there uncovered.</summary>
		static IEnumerator TestButtonWatch(Func<bool> done, float seconds)
		{
			for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until && !done(); )
			{
				bool menu = !LoadSceneManager.IsGameSceneLoaded && GameObject.Find("MainMenuCanvas") != null;
				if (menu && IslandTest.Busy && !TestCover.Showing && !EditorLoadingBox.Showing) testButtonUncovered++;
				yield return null;
			}
		}

		/// <summary>Presses the editor's Test button twice in the same frame, as a double click.</summary>
		static bool TestButtonClickTwice()
		{
			UnityEngine.UI.Button test = EditorUI.Canvas != null ? EditorUI.Canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(b => b.name == "Button_Test") : null;
			if (test == null) return false;
			test.onClick.Invoke();
			test.onClick.Invoke();
			return true;
		}

		static IEnumerator TestButtonRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("test button: in the editor"); yield break; }
			bool ok = true;
			const string name = "citest-testbutton";
			Func<bool> backInEditor = () => DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt && !IslandTest.Busy;
			DynamicIslands.NewIsland();
			yield return null;
			GeneratorWindow.Open();
			yield return null;
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 4243, Radius = 50f, Height = 20f, Style = TerrainPainter.Tropical });
			GeneratorWindow.Close();
			yield return null;
			Check(ref ok, DynamicIslands.SaveIsland(name), "a test island saved as '" + name + "'");
			testButtonUncovered = 0;

			// 1. A start that fails in the main menu (Raft's Load box threw for the user): back in the editor, not busy
			IslandTest.FailNextStartForTests = true;
			int starts = IslandTest.Starts;
			Check(ref ok, TestButtonClickTwice(), "the editor has its Test button");
			Check(ref ok, IslandTest.Busy && IslandTest.Starts == starts + 1 && TestCover.Showing, "Test clicked twice: one start, under the cover (" + (IslandTest.Starts - starts) + " start(s))");
			yield return TestButtonWatch(backInEditor, 150f);
			yield return new WaitForSeconds(1f);
			Check(ref ok, backInEditor() && DynamicIslands.currentIslandName == name && !TestCover.Showing, "the failed start went back to the editor with '" + DynamicIslands.currentIslandName + "' open (" + IslandTest.LastStep + ")");
			Check(ref ok, (IslandTest.LastFailure ?? "").Contains("NullReferenceException"), "and says why: " + IslandTest.LastFailure);

			// 2. Cancel while it starts: back in the editor
			starts = IslandTest.Starts;
			TestButtonClickTwice();
			// (as soon as the main menu is there: "In the main menu" lasts no frame - the next step follows at once)
			yield return TestButtonWatch(() => (!LoadSceneManager.IsGameSceneLoaded && GameObject.Find("MainMenuCanvas") != null) || !IslandTest.Busy, 60f);
			Check(ref ok, IslandTest.Busy && TestCover.CancelShown, "starting again (" + IslandTest.LastStep + "), Cancel offered");
			IslandTest.Cancel();
			yield return TestButtonWatch(backInEditor, 150f);
			yield return new WaitForSeconds(1f);
			Check(ref ok, backInEditor() && IslandTest.Starts == starts + 1 && IslandTest.LastFailure == "Test cancelled" && DynamicIslands.currentIslandName == name, "Cancel went back to the editor with '" + DynamicIslands.currentIslandName + "' (" + IslandTest.LastFailure + ")");

			// 3. Test clicked twice again: the test world with the island
			starts = IslandTest.Starts;
			TestButtonClickTwice();
			Check(ref ok, IslandTest.Busy && IslandTest.Starts == starts + 1, "Test works again after the failure: one start");
			yield return TestButtonWatch(() => IslandTest.Testing || !IslandTest.Busy, 300f);
			yield return new WaitForSeconds(6f);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => x.Name == name);
			Check(ref ok, IslandTest.Testing && (SaveAndLoad.CurrentGameFileName ?? "") == IslandTest.WorldName && e != null && e.Root != null, "in the test world with the island (" + IslandTest.LastStep + ")");
			Check(ref ok, !TestCover.Showing, "the cover is gone once the world is in");
			Check(ref ok, testButtonUncovered == 0, "no main menu showed while the tests started (" + testButtonUncovered + " frame(s) uncovered)");
			IslandTest.Back();
			yield return TestButtonWatch(backInEditor, 240f);
			yield return new WaitForSeconds(2f);
			Check(ref ok, backInEditor() && DynamicIslands.currentIslandName == name, "back in the editor with '" + DynamicIslands.currentIslandName + "' open");
			try { DynamicIslands.NewIsland(); IslandFilesWindow.MoveToDeleted(name); System.IO.File.Delete(System.IO.Path.Combine(System.IO.Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), name + IslandFile.Extension)); } catch { }
			if (ok) Log("PASS: test button"); else Fail("test button");
		}

		[ConsoleCommand(name: "CIIslandTest", docs: "Dev, editor: Test in a world as a builder uses it (with the starter kit, its switch and Give the kit) - a test island is saved and tried: the main menu, the test world 'Custom Islands test' (made the first time; islands tried before are taken away), the island beside the raft and the player on it, Back to the editor in the world window, the editor again with the island open. Several minutes; the test island is deleted after. CIIslandTest big: a big generated island (about 6500 objects: the editor takes longer to leave - the main menu it found then was the old one, and Raft's Create threw)")]
		public static void IslandTestCommand(string[] args) { StartTest(IslandTestRoutine(args != null && args.Length > 0 && args[0] == "big")); }

		static IEnumerator IslandTestRoutine(bool big)
		{
			if (!DynamicIslands.InEditor()) { Fail("island test: in the editor"); yield break; }
			bool ok = true;
			const string name = "citest-tryme";
			DynamicIslands.NewIsland();
			yield return null;
			GeneratorWindow.Open();
			yield return null;
			IslandGenerator.GenerateInEditor(big ? new IslandGenSettings { Seed = 497871, Radius = 226f, Height = 151f, Style = TerrainPainter.Snowy } : new IslandGenSettings { Seed = 4242, Radius = 60f, Height = 25f, Style = TerrainPainter.Tropical });
			GeneratorWindow.Close();
			yield return null;
			Check(ref ok, DynamicIslands.SaveIsland(name), (big ? "a big" : "a small") + " test island saved as '" + name + "'");
			bool kitWas = IslandTest.KitOn;
			IslandTest.KitOn = true;
			IslandTest.Start();
			Check(ref ok, IslandTest.Busy && IslandTest.Island == name, "Test in a world: on its way");
			for (float t = 0; t < 300f && !IslandTest.Testing; t += 1f) yield return new WaitForSeconds(1f);
			yield return new WaitForSeconds(6f);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => x.Name == name);
			Network_Player me = RAPI.GetLocalPlayer();
			float dist = e != null && me != null ? new Vector2(me.transform.position.x - e.Position.x, me.transform.position.z - e.Position.z).magnitude : -1f;
			Check(ref ok, IslandTest.Testing && (SaveAndLoad.CurrentGameFileName ?? "") == IslandTest.WorldName, "in the test world '" + SaveAndLoad.CurrentGameFileName + "' (" + IslandTest.LastStep + ")");
			Check(ref ok, e != null && e.Root != null && dist >= 0f && dist < CustomIslandSpawner.LandRadius(name) + 5f, "the island is beside the raft and the player on it (" + dist.ToString("0") + " m from its middle)");
			Check(ref ok, IslandWorldState.Islands.Count() == 1, "only the island being tried is in the test world (" + IslandWorldState.Islands.Count() + " island(s); ones tried before are taken away)");
			// The starter kit (the user, 2026-10-03): weapons, tools, food and water, hunger and thirst full
			PlayerInventory inv = me != null ? me.Inventory : null;
			List<string> missing = IslandTest.KitItems.Where(k => inv == null || inv.GetItemCount(k.Key) < 1).Select(k => k.Key).ToList();
			Check(ref ok, inv != null && missing.Count == 0, "the starter kit is in the inventory (" + IslandTest.KitItems.Length + " kinds" + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing.ToArray()) : "") + ")");
			Check(ref ok, me != null && me.Stats.stat_hunger.Normal.Value >= me.Stats.stat_hunger.Normal.Max - 1f && me.Stats.stat_thirst.Normal.Value >= me.Stats.stat_thirst.Normal.Max - 1f,
				"hunger and thirst are full (" + (me != null ? me.Stats.stat_hunger.Normal.Value.ToString("0") + "/" + me.Stats.stat_thirst.Normal.Value.ToString("0") : "-") + ")");
			WorldWindow.Open();
			yield return null;
			UnityEngine.UI.Button back = WorldWindow.ButtonNamed("BackToEditor");
			Check(ref ok, back != null && back.gameObject.activeInHierarchy && back.interactable, "the world window offers Back to the editor");
			UnityEngine.UI.Button kitButton = WorldWindow.ButtonNamed("TestKit"), giveKit = WorldWindow.ButtonNamed("GiveKit");
			Check(ref ok, kitButton != null && kitButton.gameObject.activeInHierarchy && UIKit.LabelOf(kitButton).text == "Kit: On" && giveKit != null && giveKit.gameObject.activeInHierarchy,
				"the world window has the kit switch (\"" + (kitButton != null ? UIKit.LabelOf(kitButton).text : "-") + "\") and Give the kit");
			int arrows = inv != null ? inv.GetItemCount("Arrow_Metal") : 0;
			if (inv != null) inv.RemoveItem("Arrow_Metal", Mathf.Min(5, arrows));
			if (giveKit != null) giveKit.onClick.Invoke();
			yield return null;
			Check(ref ok, inv != null && inv.GetItemCount("Arrow_Metal") >= arrows - 5 + 20, "Give the kit gives it again (metal arrows " + (inv != null ? inv.GetItemCount("Arrow_Metal") : 0) + ")");
			if (kitButton != null) kitButton.onClick.Invoke();
			yield return null;
			Check(ref ok, !IslandTest.KitOn && kitButton != null && UIKit.LabelOf(kitButton).text == "Kit: Off", "the kit switched off for the next Test (testkit.txt), the button says so");
			if (kitButton != null) kitButton.onClick.Invoke();
			yield return null;
			Check(ref ok, IslandTest.KitOn, "and on again");
			Screenshot(new[] { "island_test" });
			yield return new WaitForSeconds(1f);
			if (back != null) back.onClick.Invoke();
			for (float t = 0; t < 240f && !(DynamicIslands.InEditor() && !IslandTest.Busy); t += 1f) yield return new WaitForSeconds(1f);
			yield return new WaitForSeconds(2f);
			Check(ref ok, DynamicIslands.InEditor() && DynamicIslands.currentIslandName == name, "back in the editor with '" + DynamicIslands.currentIslandName + "' open");
			IslandTest.KitOn = kitWas;
			try { DynamicIslands.NewIsland(); IslandFilesWindow.MoveToDeleted(name); System.IO.File.Delete(System.IO.Path.Combine(System.IO.Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), name + IslandFile.Extension)); } catch { }
			if (ok) Log("PASS: island test"); else Fail("island test");
		}

		[ConsoleCommand(name: "CIEditorLimits", docs: "Dev, editor: the object limit (placing past what an island file holds is refused, a few more are allowed) and the gizmo's keys (C isn't the gizmo's any more - it is the camera's down; the Objects tab lists X, P and Ctrl)")]
		public static void EditorLimitsCommand()
		{
			bool ok = true;
			if (!DynamicIslands.InEditor()) { Fail("editor limits: in the editor"); return; }
			int n = ObjectLimit.Count();
			Check(ref ok, ObjectLimit.Allow(1), "one more object is allowed (" + n + " now)");
			Check(ref ok, !ObjectLimit.Allow(IslandFile.MaxObjects + 1 - n), "past " + IslandFile.MaxObjects + " objects is refused");
			var gizmo = UnityEngine.Object.FindObjectOfType<RuntimeGizmos.TransformGizmo>();
			Check(ref ok, gizmo != null && gizmo.SetCenterTypeToggle == KeyCode.None && gizmo.SetSpaceToggle == KeyCode.X && gizmo.SetPivotModeToggle == KeyCode.P, "the gizmo's keys: C left to the camera, X and P kept");
			if (ok) Log("PASS: editor limits"); else Fail("editor limits");
		}

		[ConsoleCommand(name: "CILooseEndsUnit", docs: "Dev, anywhere: the rules of the loose-ends fixes on test files - the editor's test world isn't a world using an island, a world's kept plan counts, saved worlds naming a version no longer on the PC are pointed at the kept copy (others left alone), plan rules name islands (island:, oneof:), the story chain message carries done and brought, an unnamed island")]
		public static void LooseEndsUnit()
		{
			bool ok = true;
			string folder = System.IO.Path.Combine(DynamicIslands.assetpath, "worlds");
			System.IO.Directory.CreateDirectory(folder);
			string fTest = System.IO.Path.Combine(folder, "citest-le-test.txt"), fWorld = System.IO.Path.Combine(folder, "citest-le-world.txt");
			const string isl = "citest-le", planned = "citest-le-planned", gone = "aaaaaaaaaaaa", kept = "bbbbbbbbbbbb";
			try
			{
				var rule = new IntroRule { Id = "le1", What = "oneof", WhatArg = "x, " + planned, When = "start", Where = "ahead", Distance = 300f };
				System.IO.File.WriteAllLines(fTest, new[] { "# Custom islands in world '" + IslandTest.WorldName + "': ...", isl + "|0|0|0||||" + gone });
				System.IO.File.WriteAllLines(fWorld, new[] { "# Custom islands in world 'CI LooseEnds': ...", "@planrule=" + rule.ToLine(), isl + "|0|0|0||||" + gone, "other|0|0|0||||" + gone });
				List<string> using1 = LibraryPack.WorldsUsing(isl);
				Check(ref ok, using1.Contains("CI LooseEnds") && !using1.Contains(IslandTest.WorldName), "the editor's test world isn't a world using the island: " + string.Join(", ", using1.ToArray()));
				Check(ref ok, LibraryPack.WorldsUsing(planned).Contains("CI LooseEnds"), "a world whose kept plan brings an island (oneof) uses it");
				Check(ref ok, LibraryPack.RuleNames(rule, planned) && !LibraryPack.RuleNames(rule, "citest-nope") && LibraryPack.RuleNames(new IntroRule { What = "island", WhatArg = isl }, isl), "plan rules name islands: island: and oneof:");
				int n = LibraryPack.RepointWorlds(isl, kept);
				string[] after = System.IO.File.ReadAllLines(fWorld);
				Check(ref ok, n >= 1 && after.Any(l => l.StartsWith(isl + "|") && l.EndsWith("|" + kept)) && after.Any(l => l.StartsWith("other|") && l.EndsWith("|" + gone)),
					"a world naming a version no longer on this PC is pointed at the kept copy; another island's line is left alone (" + n + " file(s))");
				Check(ref ok, LibraryPack.RepointWorlds(isl, kept) == 0, "... and pointing again changes nothing");
				string data = StoryChain.Message().Data ?? "";
				Check(ref ok, data.Length == 0 || data.Split('|').Length == 6, "the story chain message has done and brought (" + (data.Length == 0 ? "no chain here" : data.Split('|').Length + " parts") + ")");
				Check(ref ok, DynamicIslands.UnnamedIsland == "myisland", "a new island is '" + DynamicIslands.UnnamedIsland + "' until it has its own name (Ctrl+S and Test ask for one)");
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e.Message); }
			finally
			{
				foreach (string f in new[] { fTest, fWorld }) if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
			}
			if (ok) Log("PASS: loose ends unit"); else Fail("loose ends unit");
		}
	}
}

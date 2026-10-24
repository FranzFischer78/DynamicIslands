using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Test in a world" (the editor's top bar): the island being edited, saved, is tried in a world - the mod goes to the
	/// main menu, loads the test world "Custom Islands test" (or makes it the first time, with the plan "No custom islands"),
	/// puts the island beside the raft and the player on it. "Back to the editor" (Esc > Custom Islands) leaves without
	/// saving - the test world stays as it was - and opens the editor on the island again. Before, trying an island meant
	/// saving, leaving, loading a world and typing SpawnIsland. While a test starts, TestCover hides the main menu and Raft's
	/// boxes, with Cancel; a start that fails or is cancelled goes back to the editor with the reason (Abort) - never left
	/// Busy (the user, 2026-10-16: a NullReferenceException in Raft's Load box ended the start half way, and Test did nothing
	/// after).
	/// </summary>
	public static class IslandTest
	{
		public const string WorldName = "Custom Islands test";
		enum State { None, ToMenu, Opening, InWorld, Testing, Returning, OpeningEditor }
		static State state;
		static float waitUntil, giveUpAt;
		/// <summary>The main menu Test asked for has loaded. (The editor's own main menu is still there for a moment: with a big
		/// island the new one took longer, and Raft's New Game box found then was the old one being destroyed - its Create threw.)</summary>
		static bool menuLoaded;

		/// <summary>The island being tried (null when none).</summary>
		public static string Island { get; private set; }
		/// <summary>In the test world with the island (the world window offers Back to the editor).</summary>
		public static bool Testing { get { return state == State.Testing; } }
		public static bool Busy { get { return state != State.None; } }
		/// <summary>What happened last (tests, and the player when something didn't work).</summary>
		public static string LastStep { get; private set; }
		/// <summary>Why the last start went back to the editor (Abort), or null (tests).</summary>
		public static string LastFailure { get; private set; }
		/// <summary>Tests: how many starts there were (a second click on Test while one starts is not another).</summary>
		internal static int Starts { get; private set; }
		/// <summary>Tests: the next start fails in the main menu, as Raft's Load box did for the user (CITestButton).</summary>
		internal static bool FailNextStartForTests;
		/// <summary>Cancel was pressed while the world was loading: back to the editor once it is in.</summary>
		static bool cancelled;
		/// <summary>The reason shown in the editor's status bar once it is open again (Abort).</summary>
		static string failReason;

		static void Step(string s) { LastStep = s; Debug.Log("[CUSTOM ISLANDS] [test] " + s); }

		#region The starter kit

		/// <summary>
		/// What the player gets on arrival in the test world (the user, 2026-10-03: "the character would start with weapons and
		/// gathering tools, food and water"): weapons, tools to gather and dig, a head light, flippers and an oxygen bottle for
		/// caves and dives, food and fresh water. Hunger, thirst, health and oxygen are filled up too.
		/// </summary>
		public static readonly KeyValuePair<string, int>[] KitItems =
		{
			new KeyValuePair<string, int>("Spear_Scrap", 1), new KeyValuePair<string, int>("Machete", 1),
			new KeyValuePair<string, int>("Bow", 1), new KeyValuePair<string, int>("Arrow_Metal", 20),
			new KeyValuePair<string, int>("Axe", 1), new KeyValuePair<string, int>("Hammer", 1),
			new KeyValuePair<string, int>("Hook_Scrap", 1), new KeyValuePair<string, int>("FishingRod_Metal", 1),
			new KeyValuePair<string, int>("Shovel", 1), new KeyValuePair<string, int>("HeadLight", 1),
			new KeyValuePair<string, int>("Flipper", 1), new KeyValuePair<string, int>("OxygenBottle", 1),
			new KeyValuePair<string, int>("Cooked_GenericMeat", 10), new KeyValuePair<string, int>("Canteen_Water", 3),
		};

		static string KitFile { get { return Path.Combine(DynamicIslands.assetpath, "testkit.txt"); } }

		/// <summary>Whether Test gives the starter kit (on by default; switched in Esc > Custom Islands while testing, remembered
		/// in testkit.txt).</summary>
		public static bool KitOn
		{
			get
			{
				try { return !File.Exists(KitFile) || !File.ReadAllText(KitFile).Contains("kit=off"); }
				catch { return true; }
			}
			set
			{
				try { Directory.CreateDirectory(DynamicIslands.assetpath); SafeFile.WriteAllText(KitFile, "# Test in a world: start with the starter kit (on/off)\nkit=" + (value ? "on" : "off") + "\n"); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [test] testkit.txt: " + e.Message); }
			}
		}

		/// <summary>Gives the local player the starter kit (what doesn't fit in the inventory is dropped at their feet) and
		/// fills hunger, thirst, health and oxygen. Returns what was given.</summary>
		public static List<string> GiveKit()
		{
			var given = new List<string>();
			Network_Player player = RAPI.GetLocalPlayer();
			PlayerInventory inv = player != null ? player.Inventory : null;
			if (inv == null) return given;
			foreach (KeyValuePair<string, int> k in KitItems)
			{
				Item_Base item = ItemManager.GetItemByName(k.Key);
				if (item == null) { Debug.LogWarning("[CUSTOM ISLANDS] [test] Raft has no item '" + k.Key + "'"); continue; }
				try
				{
					int before = inv.GetItemCount(k.Key);
					inv.AddItem(k.Key, k.Value);
					int left = k.Value - (inv.GetItemCount(k.Key) - before);
					if (left > 0) inv.DropItem(item, left);
					given.Add(ContentCatalog.ItemLabel(k.Key) + (k.Value > 1 ? " \u00D7" + k.Value : ""));
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [test] Giving " + k.Key + " failed: " + e.Message); }
			}
			try
			{
				PlayerStats s = player.Stats;
				s.stat_hunger.Normal.Value = s.stat_hunger.Normal.Max;
				s.stat_thirst.Normal.Value = s.stat_thirst.Normal.Max;
				s.stat_health.Value = s.stat_health.Max;
				s.stat_oxygen.Value = s.stat_oxygen.Max;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [test] Filling the stats failed: " + e.Message); }
			Step("Starter kit: " + string.Join(", ", given.ToArray()));
			return given;
		}

		#endregion

		static void Stop(string why, bool warn)
		{
			Step(why);
			if (warn) DynamicIslands.Notify(why, true);
			state = State.None;
			TestCover.Hide();
		}

		/// <summary>
		/// A start that went wrong, or Cancel: back to the editor (World Plans for a plan) with the reason in its status bar.
		/// From the main menu the editor opens again; from a world that is in, it is left as Back does; on the way back
		/// already (or in the editor) it only stops.
		/// </summary>
		static void Abort(string why)
		{
			Step(why);
			LastFailure = why;
			failReason = why;
			// (right after Test the editor is still there for a frame, but the main menu is on its way: back from there)
			if (state == State.Returning || state == State.OpeningEditor || (DynamicIslands.InEditor() && state != State.ToMenu)) { Stop(why, true); return; }
			TestCover.Status((fromMenu ? "Back to the plan: " : "Back to the editor: ") + why);
			TestCover.CanCancel(false);
			if (LoadSceneManager.IsGameSceneLoaded) { state = State.Testing; Back(); return; }
			// (the main menu, or on the way to it: the editor opens once it is there)
			state = State.Returning;
			giveUpAt = Time.unscaledTime + 120f;
			waitUntil = Time.unscaledTime + 1f;
		}

		/// <summary>The cover's Cancel: back to the editor now, or once the world Raft is loading is in.</summary>
		public static void Cancel()
		{
			if (state == State.ToMenu || state == State.Opening) Abort("Test cancelled");
			else if (state == State.InWorld)
			{
				cancelled = true;
				TestCover.CanCancel(false);
				TestCover.Status("Cancelled - Raft is loading the world, then back to " + (fromMenu ? "the plan" : "the editor"));
			}
		}

		/// <summary>World Plans' Preview notebook: the plan (as it is in the window, saved or not) shown in Raft's notebook in
		/// the test world (QuestBookPreview), then back to World Plans on it.</summary>
		public static WorldPlan PreviewPlan { get; private set; }
		/// <summary>Test this plan (ROADMAP T2b): the plan played in a new test world, then back to World Plans on it.</summary>
		public static string PlanToTest { get; private set; }

		/// <summary>The island the editor had open when the preview started (opened again after it).</summary>
		static string previewIsland;
		/// <summary>The plan test or preview started from the plan window on the main menu (TODO 5b): back to the menu, not the editor.</summary>
		static bool fromMenu;
		/// <summary>The plan test or preview started on the main menu and goes back there.</summary>
		public static bool FromMenu { get { return fromMenu; } }

		static bool OnMainMenu() { return !LoadSceneManager.IsGameSceneLoaded && GameObject.Find("MainMenuCanvas") != null; }

		/// <summary>A plan test or preview can start: from the editor (its island saved first) or from the main menu.</summary>
		static bool PlanStart()
		{
			bool editor = DynamicIslands.InEditor();
			if (!editor && !OnMainMenu()) return false;
			fromMenu = !editor;
			previewIsland = editor && !DynamicIslands.IsUnnamed ? DynamicIslands.currentIslandName : null;
			return previewIsland == null || !EditorAutosave.Unsaved || DynamicIslands.SaveIsland(previewIsland);
		}

		/// <summary>The editor's Test in a world: saves the island (when it has a name) and goes to try it.</summary>
		public static void Start()
		{
			if (!DynamicIslands.InEditor() || Busy) return;
			string name = DynamicIslands.currentIslandName;
			if (DynamicIslands.IsUnnamed)
			{
				DynamicIslands.Notify("Give the island a name and save it first (Save as), then Test in a world", true);
				IslandFilesWindow.Open();
				return;
			}
			if (EditorAutosave.Unsaved || !System.IO.File.Exists(IslandSpawner.PathFor(name)))
				if (!DynamicIslands.SaveIsland(name)) return;
			Island = name;
			PreviewPlan = null;
			PlanToTest = null;
			fromMenu = false;
			Go("Trying '" + name + "' in the world '" + WorldName + "'", "Trying '" + name + "' in a world...");
		}

		/// <summary>World Plans' Test this plan: a new test world made with the plan (its islands come as in any world).</summary>
		public static void StartPlan(string plan)
		{
			if (Busy || string.IsNullOrEmpty(plan) || !PlanStart()) return;
			PlanToTest = plan;
			PreviewPlan = null;
			Island = null;
			Go("Testing the plan '" + plan + "' in a new world", "Making a test world for the plan '" + plan + "'...");
		}

		/// <summary>World Plans' Preview notebook: to the test world with the plan (the island being edited saved first, as Test does).</summary>
		public static void StartPreview(WorldPlan plan)
		{
			if (Busy || plan == null || !PlanStart()) return;
			PreviewPlan = WorldPlan.Parse(plan.Name, plan.ToText());
			PlanToTest = null;
			Island = null;
			Go("Previewing the notebook of '" + plan.Name + "' in the world '" + WorldName + "'", "Opening Raft's notebook for '" + plan.Name + "'...");
		}

		static void Go(string step, string notice)
		{
			state = State.ToMenu;
			Starts++;
			giveUpAt = Time.unscaledTime + 240f;
			cancelled = false;
			failReason = null;
			LastFailure = null;
			Step(step);
			DynamicIslands.Notify(notice);
			// (the editor's look stays on the screen: the main menu and Raft's Load and New Game boxes come and go under it)
			TestCover.Show(notice);
			TestCover.Status("Leaving the editor");
			menuLoaded = false;
			UnityEngine.SceneManagement.SceneManager.sceneLoaded -= MenuLoaded;
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += MenuLoaded;
			UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
		}

		static void MenuLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
		{
			if (scene.name != "MainMenuScene") return;
			UnityEngine.SceneManagement.SceneManager.sceneLoaded -= MenuLoaded;
			menuLoaded = true;
			waitUntil = Time.unscaledTime + 2f;
		}

		/// <summary>The world window's Back to the editor: leaves the test world without saving it.</summary>
		public static void Back()
		{
			if (state != State.Testing) return;
			state = State.Returning;
			giveUpAt = Time.unscaledTime + 120f;
			Step(PreviewPlan != null ? "Back to the plan '" + PreviewPlan.Name + "'" : PlanToTest != null ? "Back to the plan '" + PlanToTest + "'" : "Back to the editor with '" + Island + "'");
			QuestBookPreview.End();
			WorldWindow.Close();
			// (Raft's leave without saving; the pause menu's own button leaves for no scene unless its exit box chose one)
			Raft_Network net = ComponentManager<Raft_Network>.Value;
			if (net != null) net.LeaveGame(DisconnectReason.SelfDisconnected, SceneName.Lobby, false, false);
			else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
		}

		/// <summary>Every frame from the mod.</summary>
		public static void Tick()
		{
			if (state == State.None) return;
			// (not while testing: the builder plays as long as they like - it used to stop after 240 s, and Back to the editor went)
			if (state != State.Testing && Time.unscaledTime > giveUpAt) { Abort("Trying the island took too long - stopped (" + LastStep + ")"); return; }
			bool menu = !LoadSceneManager.IsGameSceneLoaded && GameObject.Find("MainMenuCanvas") != null;
			switch (state)
			{
				case State.ToMenu:
					if (menu && menuLoaded && Time.unscaledTime >= waitUntil) { state = State.Opening; DynamicIslands.instance.StartCoroutine(Guarded(OpenWorld(), State.Opening, OpenWorldFailed)); }
					break;
				case State.InWorld:
					if (LoadSceneManager.IsGameSceneLoaded && CustomIslandSpawner.RaftPosition.HasValue && RAPI.GetLocalPlayer() != null && Time.unscaledTime >= waitUntil)
					{
						if (cancelled) { Abort("Test cancelled"); break; }
						state = State.Testing;
						TestCover.Hide();
						DynamicIslands.instance.StartCoroutine(Guarded(BringIsland(), State.Testing, BringIslandFailed));
					}
					break;
				case State.Testing:
					if (!LoadSceneManager.IsGameSceneLoaded && menu) { QuestBookPreview.End(); PreviewPlan = null; PlanToTest = null; fromMenu = false; Stop("Left the test world", false); } // (by Raft's own menu)
					break;
				case State.Returning:
					if (menu && fromMenu) { state = State.OpeningEditor; waitUntil = Time.unscaledTime + 1f; TestCover.Hide(); }
					else if (menu && Time.unscaledTime >= waitUntil)
					{
						state = State.OpeningEditor;
						giveUpAt = Time.unscaledTime + 120f;
						DynamicIslands.LoadEditor(new string[0]);
						// (the editor's own loading box covers the screen from here)
						TestCover.Hide();
					}
					break;
				case State.OpeningEditor:
					if (fromMenu)
					{
						// (the plan was tested from the main menu: its start window and the plan open there again)
						if (!menu || Time.unscaledTime < waitUntil) break;
						state = State.None;
						fromMenu = false;
						MenuWindows.Ensure();
						CreateWindow.Open();
						WorldPlan back = PreviewPlan ?? (PlanToTest != null ? WorldPlan.Load(PlanToTest) : null);
						string what = PreviewPlan != null ? PreviewPlan.Name : PlanToTest;
						PreviewPlan = null;
						PlanToTest = null;
						if (back != null) WorldPlanWindow.OpenWith(back);
						Step("Back in World Plans with '" + what + "' (main menu)");
						break;
					}
					if (DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt)
					{
						string n = Island;
						state = State.None;
						if (PlanToTest != null)
						{
							string pn = PlanToTest;
							PlanToTest = null;
							if (previewIsland != null) DynamicIslands.LoadIsland(previewIsland);
							WorldPlan wp = WorldPlan.Load(pn);
							if (wp != null) WorldPlanWindow.OpenWith(wp);
							Step("Back in World Plans with '" + pn + "'");
						}
						else if (PreviewPlan != null)
						{
							WorldPlan p = PreviewPlan;
							PreviewPlan = null;
							if (previewIsland != null) DynamicIslands.LoadIsland(previewIsland);
							WorldPlanWindow.OpenWith(p);
							Step("Back in World Plans with '" + p.Name + "'");
						}
						else if (DynamicIslands.LoadIsland(n)) Step("Back in the editor with '" + n + "'");
						if (failReason != null)
						{
							// (why the test didn't start, where the builder looks: the status bar, and a notice)
							EditorUI.Flash(failReason, 12f);
							DynamicIslands.Notify(failReason, true);
							failReason = null;
						}
					}
					break;
			}
		}

		/// <summary>
		/// Runs a coroutine one step at a time: an exception in a step ends it with onFail (a coroutine can't yield inside a
		/// try with a catch, so the steps are caught here). It also ends when the state is no longer the one it runs in
		/// (Cancel, Abort).
		/// </summary>
		static IEnumerator Guarded(IEnumerator steps, State during, Action<Exception> onFail)
		{
			while (state == during)
			{
				object current;
				Exception failed = null;
				try
				{
					if (!steps.MoveNext()) break;
					current = steps.Current;
				}
				catch (Exception e) { failed = e; current = null; }
				if (failed != null)
				{
					Debug.LogWarning("[CUSTOM ISLANDS] [test] " + LastStep + ": " + failed);
					onFail(failed);
					break;
				}
				yield return current;
			}
		}

		static void OpenWorldFailed(Exception e)
		{
			Abort("The test didn't start: Raft's main menu failed at '" + LastStep + "' (" + e.GetType().Name + ") - try Test again");
		}

		static void BringIslandFailed(Exception e)
		{
			// (the world is in: the builder stays in it, Back to the editor still works)
			DynamicIslands.Notify("Bringing the island failed at '" + LastStep + "' (" + e.GetType().Name + ") - Esc > Custom Islands > Back to the editor", true);
		}

		/// <summary>
		/// Raft's box of this kind in the main menu now - found again at every step, never kept: the one found before can be
		/// gone, and its links to other boxes mended (MendDeadLinks).
		/// </summary>
		static T LiveBox<T>() where T : Component
		{
			// (the main menu scene's first, should there be one in another loaded scene too)
			T box = Resources.FindObjectsOfTypeAll<T>().Where(b => b != null && b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded)
				.OrderByDescending(b => b.gameObject.scene.name == "MainMenuScene").FirstOrDefault();
			if (box != null) MendDeadLinks(box);
			return box;
		}

		/// <summary>The dead links with no live one, said once (LiveBox looks every second).</summary>
		static readonly System.Collections.Generic.HashSet<string> mendWarned = new System.Collections.Generic.HashSet<string>();

		/// <summary>
		/// Links of Raft's box to objects that were destroyed - after the editor, LoadGameBox's versionControlBox was one
		/// (the user's Player.log, 2026-10-16: MenuBox.IsOpen threw in Button_SelectLoad and in Raft's own LoadInSavedGames) -
		/// are pointed at the live object of their kind in the loaded scenes, when there is one.
		/// </summary>
		static void MendDeadLinks(Component box)
		{
			for (Type t = box.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
				foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					if (f.IsLiteral || f.IsInitOnly || !typeof(Component).IsAssignableFrom(f.FieldType)) continue;
					try
					{
						var o = f.GetValue(f.IsStatic ? null : box) as UnityEngine.Object;
						if (ReferenceEquals(o, null) || o != null) continue; // (not set, or alive)
						// (the live one of the dead one's own kind - a field of type MenuBox must not get just any box)
						UnityEngine.Object live = Resources.FindObjectsOfTypeAll(o.GetType()).OfType<Component>()
							.FirstOrDefault(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded);
						if (live != null) f.SetValue(f.IsStatic ? null : box, live);
						string what = t.Name + "." + f.Name;
						if (live != null || mendWarned.Add(what))
							Debug.LogWarning("[CUSTOM ISLANDS] [test] Raft's " + what + " pointed at a destroyed " + o.GetType().Name + ": " + (live != null ? "now at the live one" : "there is no live one"));
					}
					catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [test] Looking at " + t.Name + "." + f.Name + ": " + e.Message); }
				}
		}

		/// <summary>
		/// Picks the test world in Raft's Load list as a click on it does. Should Raft's pick throw, what it sets - the box's
		/// chosen world and the world to load - is set directly. True when both are the test world.
		/// </summary>
		static bool PickWorld(LoadGameBox load, LoadGame_Selection pick)
		{
			try { load.Button_SelectLoad(pick); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [test] Raft's pick of the test world failed: " + e); }
			if (load.selectedGame == pick && SaveAndLoad.WorldToLoad == pick.rgdGame) return true;
			bool set = SetMember(load, typeof(LoadGameBox), "selectedGame", pick);
			set &= SetMember(null, typeof(SaveAndLoad), "WorldToLoad", pick.rgdGame);
			Step("Raft's pick didn't take: the test world set directly (" + (set ? "done" : "failed") + ")");
			return load.selectedGame == pick && SaveAndLoad.WorldToLoad == pick.rgdGame;
		}

		/// <summary>Sets a field or property (a private setter too) of Raft's; false when there is none or it threw.</summary>
		static bool SetMember(object target, Type type, string name, object value)
		{
			try
			{
				FieldInfo f = HarmonyLib.AccessTools.Field(type, name);
				if (f != null && !f.IsInitOnly) { f.SetValue(target, value); return true; }
				PropertyInfo p = HarmonyLib.AccessTools.Property(type, name);
				if (p != null && p.GetSetMethod(true) != null) { p.SetValue(target, value, null); return true; }
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [test] Setting " + type.Name + "." + name + ": " + e.Message); }
			return false;
		}

		/// <summary>The test world: loaded when it exists, made the first time (plan "No custom islands", Raft's default mode).
		/// Each step under Guarded; Raft's boxes found again after every wait (LiveBox).</summary>
		static IEnumerator OpenWorld()
		{
			TestCover.Status("Waiting for Raft's main menu");
			yield return new WaitForSecondsRealtime(1f);
			Step("In the main menu");
			if (FailNextStartForTests) { FailNextStartForTests = false; throw new NullReferenceException("a failure forced by a test"); }
			LoadGame_Selection pick = null;
			// (the world is on disk or not; when it is, Raft's list shows it after a while - right after the editor it took
			// over 10 s to start filling, and with many saved worlds it is long)
			bool saved = false;
			try { saved = !string.IsNullOrEmpty(SaveAndLoad.WorldPath) && System.IO.Directory.Exists(System.IO.Path.Combine(SaveAndLoad.WorldPath, WorldName)); } catch { }
			// (a plan is tested in a world of its own, new each time: a plan's rules start with its world)
			if (PlanToTest != null) saved = false;
			if (saved)
			{
				Step("Opening Raft's Load list");
				TestCover.Status("Finding the test world in Raft's saved worlds");
				LoadGameBox load = LiveBox<LoadGameBox>();
				if (load == null) { Abort("Raft's Load box wasn't found - the island wasn't tried"); yield break; }
				load.gameObject.SetActive(true);
				try { load.Close(); } catch { }
				load.Open();
				int count = -1;
				for (float until = Time.realtimeSinceStartup + 90f; Time.realtimeSinceStartup < until && pick == null; )
				{
					yield return new WaitForSecondsRealtime(1f);
					load = LiveBox<LoadGameBox>();
					if (load == null) { Abort("Raft's Load box went away - the island wasn't tried"); yield break; }
					int now = load.loadGameSelections != null ? load.loadGameSelections.Count : 0;
					if (now > 0 && now != count)
						pick = load.loadGameSelections.FirstOrDefault(s => s != null && (s.text_GameName != null && s.text_GameName.text.Equals(WorldName, StringComparison.OrdinalIgnoreCase)
							|| s.directoryInfo != null && s.directoryInfo.Name.Equals(WorldName, StringComparison.OrdinalIgnoreCase)));
					count = now;
				}
				Step("Raft's Load list: " + count + " world(s), the test world " + (pick != null ? "is there" : "isn't there"));
				if (pick == null) { Abort("The test world '" + WorldName + "' is saved but Raft's Load list doesn't show it - the island wasn't tried"); yield break; }

				Step("Picking the test world");
				load = LiveBox<LoadGameBox>();
				if (load == null || !PickWorld(load, pick)) { Abort("Raft's Load box couldn't pick the test world - the island wasn't tried"); yield break; }
				yield return null;
				for (float t = 0; t < 10f; t += 0.5f)
				{
					load = LiveBox<LoadGameBox>();
					if (load == null || load.loadButton == null || load.loadButton.interactable) break;
					yield return new WaitForSecondsRealtime(0.5f);
				}
				if (load == null) { Abort("Raft's Load box went away - the island wasn't tried"); yield break; }
				if (load.loadButton != null && !load.loadButton.interactable) { Abort("Raft's Load button is off (is Steam online?) - the island wasn't tried"); yield break; }
				Step("Loading the test world");
				TestCover.Status("Raft is loading the test world");
				load.Button_LoadGame();
			}
			else
			{
				Step("Opening Raft's New Game box");
				TestCover.Status("Making the test world");
				LoadGameBox load = LiveBox<LoadGameBox>();
				try { if (load != null) load.Close(); } catch { }
				NewGameBox box = LiveBox<NewGameBox>();
				if (box == null) { Abort("Raft's New Game box wasn't found - the island wasn't tried"); yield break; }
				box.gameObject.SetActive(true);
				try { box.Close(); } catch { }
				box.Open();
				yield return new WaitForSecondsRealtime(1f);
				box = LiveBox<NewGameBox>();
				if (box == null) { Abort("Raft's New Game box went away - the island wasn't tried"); yield break; }
				// (a short name: Raft refuses longer new world names)
				string worldName = PlanToTest != null ? "Plan test " + DateTime.Now.ToString("HHmm") : WorldName;
				Step("Naming the test world '" + worldName + "'");
				box.inputfield_GameName.text = worldName;
				box.GameNameEndEdit(worldName);
				for (float t = 0; t < 5f; t += 0.5f)
				{
					box = LiveBox<NewGameBox>();
					if (box == null || box.createGameButton == null || box.createGameButton.interactable) break;
					yield return new WaitForSecondsRealtime(0.5f);
				}
				if (box == null) { Abort("Raft's New Game box went away - the island wasn't tried"); yield break; }
				if (box.createGameButton != null && !box.createGameButton.interactable) { Abort("Raft's Create button is off - the island wasn't tried"); yield break; }
				WorldDirector.PendingPlan = PlanToTest ?? WorldPlan.NoneName; // (only the island being tried; or the plan being tested)
				Step("Making the test world");
				TestCover.Status("Raft is making the test world");
				box.Button_CreateNewGame();
			}
			state = State.InWorld;
			waitUntil = Time.unscaledTime + 6f;
		}

		/// <summary>The island beside the raft, the player on it.</summary>
		static IEnumerator BringIsland()
		{
			yield return new WaitForSeconds(2f);
			// (a plan's own new world: its islands are the plan's - nothing to take away)
			if (PlanToTest != null)
			{
				Step("Testing the plan '" + PlanToTest + "' (" + WorldDirector.PlanName + ")");
				IslandInfo.Show("Testing the plan '" + PlanToTest + "'", "", "Its islands come as in any world. Esc > Custom Islands > Back to the editor (the test world stays, under Load)");
				yield break;
			}
			// (islands tried before stay in the test world when Raft saved it meanwhile: only the one being tried now)
			int old = IslandWorldState.Remove(null);
			if (old > 0) Step("Took away " + old + " island(s) tried before");
			// (and their journal pages and story items: a quest counting pages or items started half done)
			if (StoryBook.HasState) { StoryBook.Reset(); Step("Cleared the journal of the islands tried before"); }
			if (PreviewPlan != null)
			{
				QuestBookPreview.Begin(PreviewPlan);
				Step("Previewing the notebook of '" + PreviewPlan.Name + "'");
				yield break;
			}
			Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
			float radius = Mathf.Max(20f, CustomIslandSpawner.LandRadius(Island));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft, radius, radius + 400f);
			if (!spot.HasValue) spot = raft + CustomIslandSpawner.SailDirection() * (radius + 80f);
			Vector3 at = spot.Value;
			at.y = CustomIslandSpawner.Elevation(Island);
			yield return DynamicIslands.instance.SpawnIslandFile(Island, at, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => x.Name.Equals(Island, StringComparison.OrdinalIgnoreCase));
			if (e == null || e.Root == null) { Stop("'" + Island + "' couldn't be placed in the test world", true); yield break; }
			yield return new WaitForSeconds(1f);
			// (the player on the island's highest ground near its middle)
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 top = e.Position + Vector3.up * 400f;
			RaycastHit hit;
			Vector3 stand = Physics.Raycast(top, Vector3.down, out hit, 800f, ~0, QueryTriggerInteraction.Ignore) ? hit.point + Vector3.up * 1.5f : e.Position + Vector3.up * 5f;
			PlayerMove.To(player, stand);
			Step("Testing '" + Island + "' (" + e.Position + ")");
			bool kit = KitOn;
			if (kit) GiveKit();
			IslandInfo.Show("Testing '" + Island + "'", kit ? "With the starter kit: weapons, tools, food and water" : "", "Esc > Custom Islands > Back to the editor (the test world isn't saved)");
		}
	}

	/// <summary>
	/// The cover while a test starts, from Test until the world is in (the user, 2026-10-16: the main menu with Raft's Load
	/// box showed instead): the whole screen in the editor's loading look, taking every click, with what is happening and
	/// Cancel. Raft's main menu and its Load and New Game boxes come and go under it. Kept through the scene changes; on
	/// the way back the editor's own loading box takes over.
	/// </summary>
	public static class TestCover
	{
		/// <summary>Above Raft's menu and the editor's loading box (32000).</summary>
		public const int SortingOrder = 32100;
		static GameObject root;
		static Text status;
		static Button cancel;
		static string message = "";

		public static bool Showing { get { return root != null; } }
		/// <summary>Tests: whether Cancel is offered now.</summary>
		public static bool CancelShown { get { return cancel != null && cancel.gameObject.activeSelf; } }

		public static void Show(string title)
		{
			message = "";
			if (root != null) { CanCancel(true); return; }
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_TestCover", SortingOrder);
			root = canvas.gameObject;
			UnityEngine.Object.DontDestroyOnLoad(root);

			// (the whole screen, and it takes the clicks: the main menu under it neither shows nor reacts)
			RectTransform back = UIKit.Rect("Backdrop", canvas.transform);
			UIKit.Stretch(back);
			back.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.06f, 0.035f, 1f);

			RectTransform box = UIKit.Rect("Box", canvas.transform);
			UIKit.Anchor(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 200f));
			UIKit.Surface(box, 8);

			Text head = UIKit.Label(box, "STARTING THE TEST", 22, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
			UIKit.Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(580f, 40f));
			Text sub = UIKit.Label(box, title ?? "", 14, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, "What");
			UIKit.Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(580f, 26f));
			status = UIKit.Label(box, "", 16, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, "Status");
			status.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(580f, 44f));

			cancel = UIKit.Button(box, "Cancel", IslandTest.Cancel, "Stop starting the test and go back to the editor", 140, 32f, 14);
			UIKit.Anchor((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(140f, 32f));

			root.AddComponent<Ticker>();
		}

		/// <summary>What it is doing now (a line under the title).</summary>
		public static void Status(string text) { message = text ?? ""; }

		/// <summary>Cancel offered, or not (once a cancel or a failure is on its way back).</summary>
		public static void CanCancel(bool on) { if (cancel != null) cancel.gameObject.SetActive(on); }

		public static void Hide()
		{
			if (root != null) UnityEngine.Object.Destroy(root);
			root = null;
			status = null;
			cancel = null;
		}

		/// <summary>Keeps the line up to date, and takes the cover away when no test is starting (never a covered screen).</summary>
		class Ticker : MonoBehaviour
		{
			void Update()
			{
				if (!IslandTest.Busy || IslandTest.Testing) { Hide(); return; }
				if (status != null) status.text = message.TrimEnd('.') + new string('.', 1 + (int)(Time.realtimeSinceStartup * 2f) % 3);
			}
		}
	}
}

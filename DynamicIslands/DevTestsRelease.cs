using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Tests before a release: every window at every screen size players use, Raft's settings the mod lives with.
	/// </summary>
	public static partial class DevTests
	{
		#region Every window at every screen size

		/// <summary>Screen sizes players use: small laptop, 4:3, HD, 16:10, full HD, 1440p, ultra-wide.</summary>
		static readonly Vector2Int[] ScreenSizes =
		{
			new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900),
			new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440),
		};

		/// <summary>The window classes of the editor (each a singleton with a private static instance, Open... and Close).</summary>
		static GameObject WindowObject(Type t)
		{
			FieldInfo f = t.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
			Component c = f != null ? f.GetValue(null) as Component : null;
			return c != null && c.gameObject.activeInHierarchy ? c.gameObject : null;
		}

		/// <summary>
		/// What of a window is off the screen: its panel and every control (buttons, fields, sliders, toggles) not inside a
		/// scrolling list (those scroll into view). Screen-space canvases: corners are pixels.
		/// </summary>
		static List<string> OffScreen(GameObject win, int w, int h)
		{
			var off = new List<string>();
			var parts = win.GetComponentsInChildren<RectTransform>(false).Where(r => r.name == "Panel" || r.GetComponent<Selectable>() != null);
			foreach (RectTransform r in parts)
			{
				if (r.GetComponentInParent<ScrollRect>() != null && r.GetComponent<ScrollRect>() == null && r.name != "Panel") continue;
				Vector3[] c = new Vector3[4];
				r.GetWorldCorners(c);
				if (c[0].x < -1f || c[0].y < -1f || c[2].x > w + 1f || c[2].y > h + 1f)
					off.Add(r.name + " (" + c[0].x.ToString("F0") + "," + c[0].y.ToString("F0") + " to " + c[2].x.ToString("F0") + "," + c[2].y.ToString("F0") + ")");
			}
			return off;
		}

		[ConsoleCommand(name: "CIScreens", docs: "Dev, editor: every window of the editor at 8 screen sizes (1024x768 to 3440x1440 ultra-wide): the editor's bars, the generator (each tab), world plans, island files, quests, story items, behaviours, the item, sound and note pickers, a text prompt and a choice - each panel and every control on the screen; pictures shot_screen_<w>x<h>_*. The screen size is put back after")]
		public static void ScreensCommand() { StartTest(ScreensRoutine()); }

		static IEnumerator ScreensRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			int w0 = Screen.width, h0 = Screen.height;
			FullScreenMode mode0 = Screen.fullScreenMode;
			GameObject root = GameObject.Find("PlacedObjects");
			Vector3 at = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 2f, 500f);
			EditorGameObject note = PlaceForTest("Note_Paper", at, root.transform);
			EditorGameObject chest = PlaceForTest("Loot_Chest", at + new Vector3(4f, 0f, 0f), root.transform);
			// (name, open it, its class)
			var windows = new List<KeyValuePair<string, KeyValuePair<Action, Type>>>
			{
				Win("generator", () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(0); }, typeof(GeneratorWindow)),
				Win("generator_2", () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(1); }, typeof(GeneratorWindow)),
				Win("generator_3", () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(2); }, typeof(GeneratorWindow)),
				Win("worldplans", () => WorldPlanWindow.Open(), typeof(WorldPlanWindow)),
				Win("islandfiles", IslandFilesWindow.Open, typeof(IslandFilesWindow)),
				Win("quest", QuestEditorWindow.Open, typeof(QuestEditorWindow)),
				Win("storyitems", StoryItemsWindow.Open, typeof(StoryItemsWindow)),
				Win("behaviours", BehaviourWindow.OpenIsland, typeof(BehaviourWindow)),
				Win("itempicker", () => ItemPickerWindow.Open(chest), typeof(ItemPickerWindow)),
				Win("soundpicker", () => SoundPickerWindow.OpenFor(s => { }), typeof(SoundPickerWindow)),
				Win("noteeditor", () => NoteEditorWindow.Open(note), typeof(NoteEditorWindow)),
				Win("prompt", () => TextPromptWindow.Open("A name", "A long question that a window like this asks, to see it wraps and fits", "some text", s => { }), typeof(TextPromptWindow)),
				Win("choice", () => ChoiceWindow.Open("Pick one", Enumerable.Range(1, 30).Select(i => new ChoiceWindow.Choice("c" + i, "Choice number " + i, "with a detail line")), s => { }), typeof(ChoiceWindow)),
			};
			try
			{
				foreach (Vector2Int size in ScreenSizes)
				{
					Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
					yield return new WaitForSecondsRealtime(1.2f);
					int w = Screen.width, h = Screen.height;
					string sz = w + "x" + h + (w != size.x || h != size.y ? " (asked " + size.x + "x" + size.y + ")" : "");
					// The editor itself: its top bar, tabs, the tool panel, the status bar
					var editorOff = OffScreen(EditorUI.Canvas.gameObject, w, h).Where(n => !n.StartsWith("Panel")).ToList();
					Check(ref ok, editorOff.Count == 0, sz + " the editor: " + (editorOff.Count == 0 ? "every control on the screen" : "off the screen: " + string.Join(", ", editorOff.Take(6).ToArray())));
					Screenshot(new[] { "screen_" + w + "x" + h + "_editor" });
					yield return new WaitForSecondsRealtime(0.6f);
					foreach (var win in windows)
					{
						// (the window can be resized from outside meanwhile - maximised by Windows or a click: set the size again)
						if (Screen.width != w || Screen.height != h)
						{
							Log("  (the screen became " + Screen.width + "x" + Screen.height + ": " + sz + " set again)");
							Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
							yield return new WaitForSecondsRealtime(1.2f);
							w = Screen.width; h = Screen.height;
						}
						try { win.Value.Key(); } catch (Exception e) { Check(ref ok, false, sz + " " + win.Key + ": opening it threw " + e.Message); continue; }
						yield return null; yield return null; yield return null;
						GameObject go = WindowObject(win.Value.Value);
						if (go == null) { Check(ref ok, false, sz + " " + win.Key + ": didn't open"); continue; }
						var off = OffScreen(go, w, h);
						Check(ref ok, off.Count == 0, sz + " " + win.Key + ": " + (off.Count == 0 ? "on the screen" : "off the screen: " + string.Join(", ", off.Take(5).ToArray())));
						if (size.x == 1280 || size.x == 3440 || off.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_" + win.Key }); yield return new WaitForSecondsRealtime(0.6f); }
						MethodInfo close = win.Value.Value.GetMethod("Close", BindingFlags.Public | BindingFlags.Static);
						if (close != null) close.Invoke(null, null);
						yield return null;
					}
				}
			}
			finally { Screen.SetResolution(w0, h0, mode0); }
			yield return new WaitForSecondsRealtime(1f);
			foreach (EditorGameObject e in new[] { note, chest }) if (e != null) UnityEngine.Object.Destroy(e.gameObject);
			Check(ref ok, Screen.width == w0 && Screen.height == h0, "the screen size put back: " + Screen.width + "x" + Screen.height);
			if (ok) Log("PASS: every window at every screen size"); else Fail("every window at every screen size");
		}

		static KeyValuePair<string, KeyValuePair<Action, Type>> Win(string name, Action open, Type t) { return new KeyValuePair<string, KeyValuePair<Action, Type>>(name, new KeyValuePair<Action, Type>(open, t)); }

		[ConsoleCommand(name: "CIScreensMenu", docs: "Dev, main menu: Raft's New Game box with the mod's panels (world plan, randomizer, world rules) at 8 screen sizes: the box, every one of its buttons, sliders and fields - Raft's Create too - on the screen, and each of the mod's panels inside the box; pictures shot_screen_<w>x<h>_newgame. The screen size is put back after")]
		public static void ScreensMenuCommand() { StartTest(ScreensMenuRoutine()); }

		static IEnumerator ScreensMenuRoutine()
		{
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("every screen size in the New Game box: no New Game box (main menu?)"); yield break; }
			bool ok = true;
			int w0 = Screen.width, h0 = Screen.height;
			FullScreenMode mode0 = Screen.fullScreenMode;
			try
			{
				foreach (Vector2Int size in ScreenSizes)
				{
					Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
					yield return new WaitForSecondsRealtime(1.2f);
					int w = Screen.width, h = Screen.height;
					box.gameObject.SetActive(true);
					try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
					yield return new WaitForSecondsRealtime(1f);
					var off = OffScreen(box.gameObject, w, h);
					// (the box's own rectangle, and each of the mod's panels inside it)
					RectTransform boxRect = (RectTransform)box.transform;
					Vector3[] b = new Vector3[4];
					boxRect.GetWorldCorners(b);
					// (Raft's box rectangle is larger than the panel it draws: its controls, checked above, are what must fit)
					var outside = new List<string>();
					foreach (Transform t in box.transform)
					{
						if (!t.name.StartsWith("CustomIslands_") || !t.gameObject.activeInHierarchy) continue;
						Vector3[] c = new Vector3[4];
						((RectTransform)t).GetWorldCorners(c);
						if (c[0].x < b[0].x - 1f || c[0].y < b[0].y - 1f || c[2].x > b[2].x + 1f || c[2].y > b[2].y + 1f) outside.Add(t.name);
					}
					Check(ref ok, off.Count == 0 && outside.Count == 0, w + "x" + h + " New Game box: " + (off.Count == 0 && outside.Count == 0 ? "every control on the screen, the mod's panels inside the box" :
						(off.Count > 0 ? "off the screen: " + string.Join(", ", off.Take(5).ToArray()) : "") + (outside.Count > 0 ? " sticking out of the box: " + string.Join(", ", outside.ToArray()) : "")));
					if (size.x == 1024 || size.x == 1280 || size.x == 3440 || off.Count > 0 || outside.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_newgame" }); yield return new WaitForSecondsRealtime(0.6f); }
					// The World settings window at this size: every control of it on the screen
					WorldSettingsWindow.Open();
					yield return new WaitForSecondsRealtime(0.6f);
					Canvas.ForceUpdateCanvases();
					var wOff = WorldSettingsWindow.Window != null ? OffScreen(WorldSettingsWindow.Window.gameObject, w, h) : new List<string> { "no window" };
					Check(ref ok, wOff.Count == 0, w + "x" + h + " World settings window: " + (wOff.Count == 0 ? "every control on the screen" : "off the screen: " + string.Join(", ", wOff.Take(5).ToArray())));
					if (size.x == 1024 || size.x == 1920 || wOff.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_worldsettings" }); yield return new WaitForSecondsRealtime(0.6f); }
					// (and the list of islands it opens)
					IslandPickerWindow.Open();
					yield return new WaitForSecondsRealtime(0.6f);
					Canvas.ForceUpdateCanvases();
					var pOff = IslandPickerWindow.Window != null ? OffScreen(IslandPickerWindow.Window.gameObject, w, h) : new List<string> { "no window" };
					Check(ref ok, pOff.Count == 0, w + "x" + h + " island list (Choose islands): " + (pOff.Count == 0 ? "every control on the screen" : "off the screen: " + string.Join(", ", pOff.Take(5).ToArray())));
					if (size.x == 1024 || pOff.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_islandpicker" }); yield return new WaitForSecondsRealtime(0.6f); }
					IslandPickerWindow.Close();
					WorldSettingsWindow.Close();
					// (and the island library window)
					LibraryWindow.Open();
					yield return new WaitForSecondsRealtime(0.6f);
					Canvas.ForceUpdateCanvases();
					var lOff = LibraryWindow.Root != null ? OffScreen(LibraryWindow.Root.gameObject, w, h) : new List<string> { "no window" };
					Check(ref ok, lOff.Count == 0, w + "x" + h + " island library window: " + (lOff.Count == 0 ? "every control on the screen" : "off the screen: " + string.Join(", ", lOff.Take(5).ToArray())));
					if (size.x == 1024 || lOff.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_library" }); yield return new WaitForSecondsRealtime(0.6f); }
					LibraryWindow.Close();
					// (and the plan picker, CT5)
					PlanPickerWindow.Open();
					yield return new WaitForSecondsRealtime(0.6f);
					Canvas.ForceUpdateCanvases();
					var ppOff = PlanPickerWindow.Root != null ? OffScreen(PlanPickerWindow.Root.gameObject, w, h) : new List<string> { "no window" };
					Check(ref ok, ppOff.Count == 0, w + "x" + h + " plan picker window: " + (ppOff.Count == 0 ? "every control on the screen" : "off the screen: " + string.Join(", ", ppOff.Take(5).ToArray())));
					if (size.x == 1280 || ppOff.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_planpicker" }); yield return new WaitForSecondsRealtime(0.6f); }
					PlanPickerWindow.Close();
					box.gameObject.SetActive(false);
					yield return null;
				}
			}
			finally { Screen.SetResolution(w0, h0, mode0); box.gameObject.SetActive(false); }
			yield return new WaitForSecondsRealtime(1f);
			if (ok) Log("PASS: every screen size in the New Game box"); else Fail("every screen size in the New Game box");
		}

		[ConsoleCommand(name: "CIScreensLevels", docs: "Dev, world: the level up system's stats page (every stat full, the longest texts), the level bar and a level up's box at 8 screen sizes: every row, text and the Close button on the screen; pictures shot_screen_<w>x<h>_stats. The player's record and the screen size are put back after (IL19)")]
		public static void ScreensLevelsCommand() { StartTest(ScreensLevelsRoutine()); }

		static IEnumerator ScreensLevelsRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || RAPI.GetLocalPlayer() == null) { Fail("every screen size for the levels: run in a world"); yield break; }
			bool ok = true;
			int w0 = Screen.width, h0 = Screen.height;
			FullScreenMode mode0 = Screen.fullScreenMode;
			bool wasOn = PlayerLevels.On;
			if (!wasOn) PlayerLevels.TurnOn(false);
			LevelRecord mine = PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : new LevelRecord();
			var full = new LevelRecord { Xp = LevelRules.TotalFor(70), Kills = 900 };
			for (int i = 0; i < LevelRules.StatCount; i++) full.Points[i] = LevelRules.MaxPoints;
			PlayerLevels.SetMine(full);
			try
			{
				foreach (Vector2Int size in ScreenSizes)
				{
					Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
					yield return new WaitForSecondsRealtime(1.2f);
					int w = Screen.width, h = Screen.height;
					LevelHud.ShowBar();
					LevelHud.Announce("LEVEL UP!", "Level 70. Every level gives " + LevelRules.PointsPerLevel + " stat points: press " + PlayerLevels.Key + " to spend them - a long line to see it wraps and fits.", true);
					LevelWindow.Open();
					yield return new WaitForSecondsRealtime(0.6f);
					Canvas.ForceUpdateCanvases();
					GameObject page = GameObject.Find("CustomIslands_Stats"), hud = GameObject.Find("CustomIslands_Levels");
					var off = page != null ? OffScreen(page, w, h) : new List<string> { "no stats page" };
					Check(ref ok, LevelWindow.IsOpen && off.Count == 0, w + "x" + h + " stats page: " + (off.Count == 0 ? "every row and the Close button on the screen" : "off the screen: " + string.Join(", ", off.Take(5).ToArray())));
					var hudOff = hud != null ? OffScreen(hud, w, h) : new List<string> { "no level bar" };
					Check(ref ok, LevelHud.BarShown && hudOff.Count == 0, w + "x" + h + " level bar and box: " + (hudOff.Count == 0 ? "on the screen" : "off the screen: " + string.Join(", ", hudOff.Take(5).ToArray())));
					if (size.x == 1280 || size.x == 3440 || off.Count > 0 || hudOff.Count > 0) { Screenshot(new[] { "screen_" + w + "x" + h + "_stats" }); yield return new WaitForSecondsRealtime(0.6f); }
					LevelWindow.Close();
					LevelHud.Clear();
					yield return null;
				}
			}
			finally
			{
				Screen.SetResolution(w0, h0, mode0);
				LevelWindow.Close();
				PlayerLevels.SetMine(mine);
				if (!wasOn) PlayerLevels.TurnOff();
			}
			yield return new WaitForSecondsRealtime(1f);
			if (ok) Log("PASS: every screen size for the levels"); else Fail("every screen size for the levels");
		}

		#endregion


		#region A story plan made from the player's own islands, played in a new world

		const string StoryPlan = "CI Story";
		static readonly string[] StoryIslands = { "cistory-home", "cistory-bay" };

		/// <summary>A button of a window by its label (UIKit names them Button_&lt;label&gt;), clicked as a player would.</summary>
		static bool Click(GameObject root, string label)
		{
			if (root == null) return false;
			Button b = root.GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.name == "Button_" + label || (UIKit.LabelOf(x) != null && UIKit.LabelOf(x).text == label));
			if (b == null || !b.interactable) return false;
			b.onClick.Invoke();
			return true;
		}

		/// <summary>Types into a window's field found by its placeholder text, as a player would (the field applies it when left).</summary>
		static bool TypeInto(GameObject root, string placeholder, string text, int nth = 0)
		{
			InputField f = root.GetComponentsInChildren<InputField>(false).Where(x => x.placeholder is Text && ((Text)x.placeholder).text == placeholder).Skip(nth).FirstOrDefault();
			if (f == null) return false;
			f.text = text;
			f.onEndEdit.Invoke(text);
			return true;
		}

		[ConsoleCommand(name: "CIStoryPlanMake", docs: "Dev, editor: a player's story plan made in the World Plans window from their own islands: two islands saved (a home island with a gate zone and a quest, a bay with a chest and a quest); a new plan 'CI Story' through New... and its name prompt; four rules added with + Add a rule and filled in through each card's fields and drop-down lists (start: home ahead; home's gate zone fires: the bay north of home; the bay's quest done: a treasure island east of the bay; after 2 km: an oddity ahead); a description; Check finds no problem; Save; closed, opened again: the same plan. Then Copy..., Delete, Templates..., the rule arrows and remove, the random islands switch on a scratch plan")]
		public static void StoryPlanMakeCommand() { StartTest(StoryPlanMakeRoutine()); }

		static IEnumerator StoryPlanMakeRoutine()
		{
			yield return WaitForEditor(false);
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			// Two islands of the player's own, as the generator and Save as make them
			for (int i = 0; i < StoryIslands.Length; i++)
			{
				var s = new IslandGenSettings { Seed = 3100 + i, Style = TerrainPainter.Tropical, Shape = IslandShapes.Round, Radius = 55f, Height = 14f, ObjectDensity = 0.6f };
				IslandFile f = IslandGenerator.CreateFile(s, StoryIslands[i]);
				var k = new MapKit(f, 7 + i);
				Vector2? spot = k.Find(k.Mid, 25f, MapKit.Dry, 6f);
				Vector2 at = spot ?? k.Mid;
				f.Props[IslandProps.Title] = i == 0 ? "Home Island" : "The Bay";
				if (i == 0)
				{
					k.Zone(at, "gate", 5f, "The gate opens: something appears to the north");
					k.Quest("Find the gate", "Look for the gate on this island.", "The gate is found!", "Plank*3", "reach|gate|1|Walk to the gate");
				}
				else
				{
					k.Chest("Loot_Chest", at, "Bay chest", "Rope*2");
					k.Quest("The bay's chest", "A chest lies in the bay.", "You found the bay's chest", "Nail*4", "open|Bay chest|1|Open the bay's chest");
				}
				f.Save(IslandSpawner.PathFor(StoryIslands[i]));
			}
			Check(ref ok, StoryIslands.All(n => System.IO.File.Exists(IslandSpawner.PathFor(n))), "two islands saved: " + string.Join(", ", StoryIslands));
			if (System.IO.File.Exists(WorldPlan.PathFor(StoryPlan))) System.IO.File.Delete(WorldPlan.PathFor(StoryPlan));
			if (System.IO.File.Exists(WorldPlan.PathFor(StoryPlan + " copy"))) System.IO.File.Delete(WorldPlan.PathFor(StoryPlan + " copy"));

			// The World Plans window, as the player uses it
			WorldPlanWindow.Open();
			yield return null; yield return null;
			GameObject win = WindowObject(typeof(WorldPlanWindow));
			Check(ref ok, win != null, "World Plans opens");
			if (win == null) { Fail("story plan made"); yield break; }
			Check(ref ok, Click(win, "New..."), "New... clicked");
			yield return null; yield return null;
			// (New... asks how to start first: an empty plan - TODO 5b)
			Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.PickValue(WorldPlanWindow.NewEmpty), "New...: an empty plan");
			yield return null; yield return null;
			GameObject prompt = WindowObject(typeof(TextPromptWindow));
			Check(ref ok, prompt != null && TypeInto(prompt, "Name", StoryPlan) && Click(prompt, "OK"), "the name prompt: '" + StoryPlan + "', OK");
			yield return null; yield return null;
			Func<List<GameObject>> cards = () => win.GetComponentsInChildren<RectTransform>(false).Where(r => r.name == "Rule").Select(r => r.gameObject).ToList();
			// (rule: id, when, when's island, when's arg, what, which, where, metres, direction, near which, message, Receiver name -
			// when, what, where and direction are picked from each card's drop-down lists, as a player picks them)
			var rules = new[]
			{
				new[] { "home", "start", "", "", "island", StoryIslands[0], "ahead", "300", "", "", "A story begins", "Home" },
				new[] { "bay", "zone", "home", "gate", "island", StoryIslands[1], "near", "600", "north", "home", "Something to the north", "Bay" },
				new[] { "treasure", "quest", "bay", "", "type", "treasure", "near", "600", "east", "bay", "", "Treasure" },
				new[] { "far", "km", "", "2", "type", "oddity", "ahead", "400", "", "", "", "" },
			};
			// (islands that come on their own are on the Other islands tab - TODO 5b)
			Check(ref ok, ClickTab(win, "Tab_Other"), "the Other islands tab");
			yield return null;
			for (int i = 0; i < rules.Length; i++)
			{
				string[] r = rules[i];
				Check(ref ok, Click(win, "+ Add a rule"), "rule " + (i + 1) + ": + Add a rule");
				yield return null; yield return null;
				int idx = i;
				Func<GameObject> card = () => { var c = cards(); return idx < c.Count ? c[idx] : null; };
				bool picked = true;
				picked &= card() != null && DropList.Pick(card(), "Drop_When", r[1]); yield return null; yield return null;
				picked &= card() != null && DropList.Pick(card(), "Drop_What", r[4]); yield return null; yield return null;
				picked &= card() != null && DropList.Pick(card(), "Drop_Where", r[6]); yield return null; yield return null;
				if (r[8].Length > 0) { picked &= card() != null && DropList.Pick(card(), "Drop_Direction", r[8]); yield return null; yield return null; }
				Check(ref ok, picked, "rule " + (i + 1) + ": WHEN, BRING, WHERE" + (r[8].Length > 0 ? " and the direction" : "") + " picked from the card's lists");
				GameObject cd = card();
				if (cd == null) { Check(ref ok, false, "rule " + (i + 1) + ": no card"); continue; }
				bool typed = TypeInto(cd, "id", r[0]);
				if (r[2].Length > 0) typed &= TypeInto(cd, "rule id / island", r[2]);
				if (r[3].Length > 0) typed &= TypeInto(cd, r[1] == "zone" ? "zone name" : "km", r[3]);
				typed &= TypeInto(cd, r[4] == "type" ? "map type" : "island name", r[5]);
				typed &= TypeInto(cd, "300", r[7]);
				if (r[9].Length > 0) typed &= TypeInto(cd, "where it happened", r[9]);
				if (r[10].Length > 0) typed &= TypeInto(cd, "Message to every player (optional)", r[10]);
				if (r[11].Length > 0) typed &= TypeInto(cd, "Receiver name", r[11]);
				Check(ref ok, typed, "rule " + (i + 1) + " '" + r[0] + "': filled in through its card");
			}
			Check(ref ok, ClickTab(win, "Tab_Settings"), "the Settings tab");
			yield return null;
			Check(ref ok, TypeInto(win, "Description, shown when choosing the plan (e.g. A story across five islands)", "A test story across four islands"), "the description typed");
			Check(ref ok, Click(win, "Check"), "Check clicked");
			yield return null;
			// (what Check found: the window's problems text)
			FieldInfo pt = typeof(WorldPlanWindow).GetField("problemsText", BindingFlags.Instance | BindingFlags.NonPublic);
			Text ptext = pt != null ? pt.GetValue(win.GetComponent<WorldPlanWindow>()) as Text : null;
			string problems = ptext != null ? ptext.text : "";
			Log("  Check says: " + problems.Replace("\n", " / "));
			Check(ref ok, Click(win, "Save"), "Save clicked");
			yield return null;
			WorldPlan saved = WorldPlan.Load(StoryPlan);
			Check(ref ok, saved != null && saved.Rules.Count == 4, "the plan saved with " + (saved != null ? saved.Rules.Count : 0) + " rules (4)" + (problems.Length > 0 ? "; Check says: " + problems.Replace("\n", " / ") : ""));
			if (saved != null)
			{
				for (int i = 0; i < rules.Length && i < saved.Rules.Count; i++)
				{
					IntroRule s = saved.Rules[i];
					string[] r = rules[i];
					string want = r[0] + "|" + r[5] + "|" + r[7];
					string got = s.Id + "|" + s.WhatArg + "|" + s.Distance.ToString("0");
					Check(ref ok, want == got && s.Where == r[6] && (r[8].Length == 0 || s.Direction == r[8]) && (r[3].Length == 0 || s.WhenArg == r[3]) && (r[2].Length == 0 || s.WhenRef == r[2]), "rule " + (i + 1) + " as typed: " + s.Describe());
				}
				Check(ref ok, saved.Description == "A test story across four islands", "the description saved");
			}
			Check(ref ok, Click(win, "Close"), "Close clicked");
			yield return null;
			WorldPlanWindow.Open(StoryPlan);
			yield return null; yield return null;
			Check(ref ok, cards().Count == 4, "opened again: its 4 rules (" + cards().Count + ")");

			// Copy..., then the copy: the random switch, a template, the arrows and remove, Delete
			Check(ref ok, Click(win, "Copy..."), "Copy... clicked");
			yield return null; yield return null;
			prompt = WindowObject(typeof(TextPromptWindow));
			if (prompt != null) Click(prompt, "OK"); // (the name it suggests: "CI Story copy")
			yield return null; yield return null;
			Check(ref ok, System.IO.File.Exists(WorldPlan.PathFor(StoryPlan + " copy")), "the copy saved as '" + StoryPlan + " copy'");
			// (the plan's rules, not the cards shown: since 5b a tab shows only its own - the main story line's or the other islands')
			int before = WorldPlanWindow.Plan.Rules.Count;
			ClickTab(win, "Tab_Settings");
			yield return null;
			Button random = win.GetComponentsInChildren<Button>(false).FirstOrDefault(b => UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text.StartsWith("Random islands while sailing"));
			string r0 = random != null ? UIKit.LabelOf(random).text : "";
			if (random != null) random.onClick.Invoke();
			yield return null;
			Check(ref ok, random != null && UIKit.LabelOf(random).text != r0, "the random islands switch: '" + r0 + "' -> '" + (random != null ? UIKit.LabelOf(random).text : "") + "'");
			ClickTab(win, "Tab_Other");
			yield return null;
			Check(ref ok, Click(win, "Templates..."), "Templates... clicked");
			yield return null; yield return null;
			GameObject choice = WindowObject(typeof(ChoiceWindow));
			Button firstTemplate = choice != null ? choice.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name.StartsWith("Button_") && UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text.Length > 0 && b.name != "Button_Close" && b.name != "Button_Cancel") : null;
			if (firstTemplate != null) firstTemplate.onClick.Invoke();
			yield return null; yield return null;
			int withTemplate = WorldPlanWindow.Plan.Rules.Count;
			// (the arrows on a tab with two cards at least)
			if (cards().Count < 2) { WorldPlanWindow.ShowTab(WorldPlanWindow.Tab == WorldPlanWindow.TabStory ? WorldPlanWindow.TabOther : WorldPlanWindow.TabStory); yield return null; }
			Check(ref ok, withTemplate > before, "a template adds its rules: " + before + " -> " + withTemplate);
			GameObject last = cards().LastOrDefault();
			string lastId = last != null ? last.GetComponentsInChildren<InputField>(false).Select(f => f.text).FirstOrDefault() : "";
			if (last != null) Click(last, "▲");
			yield return null; yield return null;
			var afterUp = cards();
			string movedId = afterUp.Count >= 2 ? afterUp[afterUp.Count - 2].GetComponentsInChildren<InputField>(false).Select(f => f.text).FirstOrDefault() : "";
			Check(ref ok, movedId == lastId, "the up arrow moves the last rule up one ('" + lastId + "')");
			GameObject first = cards().FirstOrDefault();
			if (first != null) Click(first, "Remove");
			yield return null; yield return null;
			Check(ref ok, WorldPlanWindow.Plan.Rules.Count == withTemplate - 1, "the remove button takes a rule out: " + withTemplate + " -> " + WorldPlanWindow.Plan.Rules.Count);
			Check(ref ok, Click(win, "Delete"), "Delete clicked");
			yield return null; yield return null;
			Check(ref ok, !System.IO.File.Exists(WorldPlan.PathFor(StoryPlan + " copy")) && System.IO.File.Exists(WorldPlan.PathFor(StoryPlan)), "the copy deleted, the story plan kept");
			WorldPlanWindow.Close();
			if (ok) Log("PASS: story plan made"); else Fail("story plan made");
		}

		[ConsoleCommand(name: "CIStoryPlay", docs: "Dev, in game (host, a new world made with the plan 'CI Story': CIPlanBox \"CI Story\" then CINewWorld): the plan played through. The home island comes ahead of the raft with its Receiver name and message, loaded exactly as saved (the same ground and every object of its file); its gate zone brings the bay 600 m north of it; the bay's quest (open its chest) brings a treasure island east of the bay; 2 km sailed bring an oddity. Each once, never twice. Logs STORY lines for CIStoryCheck")]
		public static void StoryPlayCommand() { StartTest(StoryPlayRoutine()); }

		/// <summary>An island in the world against its file: the same ground heights and every object of the file there.</summary>
		static string LoadedAsSaved(IslandWorldState.Entry e)
		{
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor(e.Name));
			if (f == null || e.Root == null) return "no file or not loaded";
			int spawned = e.Root.GetComponentsInChildren<IslandObjectRef>(true).Length;
			Transform objs = e.Root.transform.Find("Objects");
			int children = objs != null ? objs.childCount : spawned;
			Terrain t = e.Root.GetComponentInChildren<Terrain>();
			float fileTop = 0f, worldTop = 0f;
			foreach (float h in f.Heights) fileTop = Mathf.Max(fileTop, h * f.TerrainSize.y);
			if (t != null) foreach (float h in t.terrainData.GetHeights(0, 0, t.terrainData.heightmapResolution, t.terrainData.heightmapResolution)) worldTop = Mathf.Max(worldTop, h * t.terrainData.size.y);
			bool same = children == f.Objects.Count && (t == null || Mathf.Abs(fileTop - worldTop) < 0.05f);
			return (same ? "" : "DIFFERENT: ") + "objects " + children + " of the file's " + f.Objects.Count + ", highest ground " + worldTop.ToString("F2") + " m (file " + fileTop.ToString("F2") + ")";
		}

		static IEnumerator StoryPlayRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("story played: host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Check(ref ok, WorldDirector.Plan != null && WorldDirector.Plan.Name == StoryPlan, "the world's plan: '" + (WorldDirector.Plan != null ? WorldDirector.Plan.Name : "none") + "'");
			Func<string, IslandWorldState.Entry> byName = n => IslandWorldState.Islands.FirstOrDefault(e => e.HostName == n || e.Name == n);
			Func<string, IEnumerator> waitLoaded = n => WaitFor(() => byName(n) != null && byName(n).Root != null, 90f);
			// 1. The home island, at the start
			yield return waitLoaded(StoryIslands[0]);
			IslandWorldState.Entry home = byName(StoryIslands[0]);
			Check(ref ok, home != null && home.Root != null, "the world starts with '" + StoryIslands[0] + "'" + (home != null ? " (Receiver: '" + home.Label + "')" : ""));
			if (home == null || home.Root == null) { Fail("story played"); yield break; }
			Check(ref ok, home.Label == "Home", "its Receiver name 'Home' (" + home.Label + ")");
			string h1 = LoadedAsSaved(home);
			Check(ref ok, !h1.StartsWith("DIFFERENT"), "home loaded as saved: " + h1);
			// 2. Its gate zone brings the bay north of it
			TriggerZone gate = home.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "gate");
			Check(ref ok, gate != null, "home's gate zone");
			if (gate != null) { yield return PutPlayer(RAPI.GetLocalPlayer(), gate.transform.position + Vector3.up * 1.5f, false); yield return new WaitForSeconds(2f); }
			yield return waitLoaded(StoryIslands[1]);
			IslandWorldState.Entry bay = byName(StoryIslands[1]);
			if (bay != null)
			{
				Vector3 d = bay.Position - home.Position;
				float bearing = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
				Check(ref ok, bearing < 30f || bearing > 330f, "the gate brought the bay north of home: " + new Vector2(d.x, d.z).magnitude.ToString("F0") + " m at " + bearing.ToString("F0") + "°");
				if (bay.Root != null) Check(ref ok, !LoadedAsSaved(bay).StartsWith("DIFFERENT"), "the bay loaded as saved: " + LoadedAsSaved(bay));
				Check(ref ok, QuestTracker.StepOf(home) >= QuestTracker.QuestOf(home).Steps.Count, "home's quest done by reaching the gate");
			}
			else Check(ref ok, false, "the gate zone brought no bay");
			// 3. The bay's quest: open its chest; a treasure island east of the bay
			if (bay != null && bay.Root != null)
			{
				LootCrate chest = bay.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault();
				if (chest != null) { PutPlayerNear(chest.transform); chest.Open(); NoteReader.Close(); }
				IslandWorldState.Entry treasure = null;
				for (float t = 0; t < 90f && treasure == null; t += 1f) { treasure = IslandWorldState.Islands.FirstOrDefault(e => e.Label == "Treasure"); if (treasure == null) yield return new WaitForSeconds(1f); }
				if (treasure != null)
				{
					Vector3 d = treasure.Position - bay.Position;
					float bearing = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
					// (the east half: when Raft's islands are in the way the spot search turns up to 80° from the direction -
					// WorldDirector.Candidates; once it came at 125°, 35° off)
					Check(ref ok, bearing > 5f && bearing < 175f, "the bay's quest brought the treasure island east of it: " + bearing.ToString("F0") + "° ('" + treasure.Name + "')");
				}
				else Check(ref ok, false, "the bay's quest brought no treasure island");
			}
			// 4. 2 km sailed: an oddity ahead
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(RAPI.GetLocalPlayer(), raftObj.transform.position + Vector3.up * 2f, false);
			int before = IslandWorldState.Islands.Count;
			yield return SailRoutine(100f, 25f);
			yield return new WaitForSeconds(5f);
			bool far = IslandWorldState.Islands.Count > before && IslandWorldState.Islands.Skip(before).Any(e => (e.Name ?? "").StartsWith("gen-"));
			Check(ref ok, far, "2.5 km sailed: the 'far' rule brought an island (" + string.Join(", ", IslandWorldState.Islands.Skip(before).Select(e => e.Name).ToArray()) + ")");
			// Each rule once
			var names = IslandWorldState.Islands.Select(e => e.HostName ?? e.Name).ToList();
			Check(ref ok, names.Count(n => n == StoryIslands[0]) == 1 && names.Count(n => n == StoryIslands[1]) == 1 && IslandWorldState.Islands.Count(e => e.Label == "Treasure") == 1, "each island of the plan once");
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands) Log("STORY " + (e.HostName ?? e.Name) + " label=" + e.Label);
			Log("STORY done " + string.Join(",", WorldDirector.Done.OrderBy(x => x).ToArray()));
			if (ok) Log("PASS: story played"); else Fail("story played");
		}

		static IEnumerator WaitFor(Func<bool> done, float seconds)
		{
			for (float t = 0; t < seconds && !done(); t += 0.5f) yield return new WaitForSeconds(0.5f);
		}

		[ConsoleCommand(name: "CIStoryCheck", docs: "Dev, in game (host): after the story world was saved and loaded again: the plan still the world's, its islands the same (each once), the rules that fired don't fire again; logs STORY lines to compare with CIStoryPlay's. On player 2 (joined): home and the bay once each, with their Receiver names, and the same STORY island lines as the host's")]
		public static void StoryCheckCommand() { StartTest(StoryCheckRoutine()); }

		static IEnumerator StoryCheckRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("story check: in a world"); yield break; }
			yield return new WaitForSeconds(8f);
			bool ok = true;
			bool host = Raft_Network.IsHost;
			if (host) Check(ref ok, WorldDirector.Plan != null && WorldDirector.Plan.Name == StoryPlan, "loaded again: the world's plan '" + (WorldDirector.Plan != null ? WorldDirector.Plan.Name : "none") + "'");
			var names = IslandWorldState.Islands.Select(e => e.HostName ?? e.Name).ToList();
			Check(ref ok, names.Count(n => n == StoryIslands[0]) == 1 && names.Count(n => n == StoryIslands[1]) == 1, "loaded again: home and the bay once each");
			if (!host) Check(ref ok, IslandWorldState.Islands.Any(e => (e.HostName ?? e.Name) == StoryIslands[0] && e.Label == "Home") && IslandWorldState.Islands.Any(e => (e.HostName ?? e.Name) == StoryIslands[1] && e.Label == "Bay"), "player 2: home and the bay with their Receiver names");
			else Check(ref ok, WorldDirector.Done.Contains("home") && WorldDirector.Done.Contains("bay") && WorldDirector.Done.Contains("treasure"), "loaded again: the rules that fired are remembered (" + string.Join(",", WorldDirector.Done.OrderBy(x => x).ToArray()) + ")");
			// (an island whose file the host hasn't got - removed since - isn't sent to joining players: marked, so the comparison
			// with player 2's lines leaves it out)
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
				Log("STORY " + (e.HostName ?? e.Name) + " label=" + e.Label + (host && IslandNetwork.HashOf(e.Name) == null ? " (not on this PC)" : ""));
			Log("STORY done " + string.Join(",", WorldDirector.Done.OrderBy(x => x).ToArray()));
			if (ok) Log("PASS: story check"); else Fail("story check");
		}

		#endregion

		[ConsoleCommand(name: "CIBoxClicks", docs: "Dev, main menu: Raft's New Game box clicked as a player does: the plan button goes round every plan (built-in and the player's own) and back to the first, each shows its name and description; the randomizer level button goes Off, Light, Normal, Wild and round; each part button switches its part off and on (greyed when Off). Leaves the plan <plan> (default CI Story) chosen and the randomizer Off: CIBoxClicks [plan]")]
		public static void BoxClicksCommand(string[] args) { StartTest(BoxClicksRoutine(args != null && args.Length > 0 ? string.Join(" ", args) : StoryPlan)); }

		static IEnumerator BoxClicksRoutine(string wantPlan)
		{
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("New Game box clicks: no New Game box (main menu?)"); yield break; }
			bool ok = true;
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			yield return new WaitForSecondsRealtime(1f);
			Transform planRow = box.transform.Find("CustomIslands_Plan");
			// ("Choose plan...", the plan picker window (CT5), not the head's "Get more..." - the island library)
			Button plan = planRow != null ? planRow.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == "Button_ChoosePlan") : null;
			Text detail = planRow != null ? planRow.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Detail") : null;
			if (plan == null) { Fail("New Game box clicks: no plan list"); yield break; }
			// The plan list: every plan picked in turn in the picker
			List<string> plans = WorldPlan.All();
			string first = NewWorldOptions.Selected;
			var seen = new List<string>();
			var wrongDetail = new List<string>();
			foreach (string name in plans)
			{
				bool picked = ChoosePlanInPicker(plan, name);
				yield return null;
				string label = ChosenPlanText(planRow);
				if (picked) seen.Add(label);
				WorldPlan p = WorldPlan.Load(label);
				if (p == null || detail == null || !detail.text.StartsWith(p.Description.Length > 0 ? p.Description : p.Rules.Count + " rule(s)")) wrongDetail.Add(label);
			}
			ChoosePlanInPicker(plan, first);
			yield return null;
			Check(ref ok, seen.Distinct().Count() == plans.Count && NewWorldOptions.Selected == first, "the plan list has all " + plans.Count + " plans, each picked from it (" + string.Join(", ", seen.ToArray()) + "), and back to '" + first + "'");
			Check(ref ok, wrongDetail.Count == 0, "each plan shows its description" + (wrongDetail.Count > 0 ? " - not: " + string.Join(", ", wrongDetail.ToArray()) : ""));
			Check(ref ok, plans.Contains(wantPlan), "the player's plan '" + wantPlan + "' is among them");
			ChoosePlanInPicker(plan, wantPlan);
			yield return null;
			Check(ref ok, NewWorldOptions.Selected == wantPlan && ChosenPlanText(planRow).StartsWith(wantPlan), "picked '" + wantPlan + "' from the list: chosen (" + NewWorldOptions.Selected + ")");
			// The randomizer: level round, then each part
			// (the randomizer is in the World settings window now: opened from the box's button, as a player does)
			if (WorldSettingsWindow.OpenButton != null) WorldSettingsWindow.OpenButton.onClick.Invoke();
			yield return null;
			Button[] rb = NewWorldOptions.LevelButton != null ? new[] { NewWorldOptions.LevelButton }.Concat(NewWorldOptions.PartButtons).ToArray() : new Button[0];
			Button level = rb.FirstOrDefault();
			if (level == null) { Check(ref ok, false, "no randomizer buttons"); }
			else
			{
				var levels = new List<string>();
				for (int i = 0; i < RandomizerSettings.LevelNames.Length; i++) { if (DropList.Click(level, i.ToString())) levels.Add(NewWorldOptions.Randomizer.LevelName + (UIKit.LabelOf(level).text == NewWorldOptions.Randomizer.LevelName ? "" : "(label " + UIKit.LabelOf(level).text + ")")); yield return null; }
				Check(ref ok, levels.Distinct().Count() == RandomizerSettings.LevelNames.Length && levels.All(l => !l.Contains("(label")), "the level list: each picked from it - " + string.Join(" > ", levels.ToArray()));
				DropList.Click(level, RandomizerSettings.Wild.ToString()); yield return null;
				var parts = rb.Skip(1).ToArray();
				var badParts = new List<string>();
				for (int i = 0; i < parts.Length && i < RandomizerSettings.Features.Length; i++)
				{
					string f = RandomizerSettings.Features[i];
					// (a part may start off: the player's saved choice)
					bool was = NewWorldOptions.Randomizer.Disabled.Contains(f);
					parts[i].onClick.Invoke(); yield return null;
					bool flipped = NewWorldOptions.Randomizer.Disabled.Contains(f) != was;
					parts[i].onClick.Invoke(); yield return null;
					bool back = NewWorldOptions.Randomizer.Disabled.Contains(f) == was;
					if (!flipped || !back) badParts.Add(f);
					if (was) { parts[i].onClick.Invoke(); yield return null; }
				}
				Check(ref ok, parts.Length == RandomizerSettings.Features.Length && badParts.Count == 0, parts.Length + " part buttons, each switches its part off and on" + (badParts.Count > 0 ? " - not: " + string.Join(", ", badParts.ToArray()) : ""));
				DropList.Click(level, RandomizerSettings.Off.ToString()); yield return null;
				Check(ref ok, parts.All(b => !b.interactable), "Off: the part buttons greyed out");
			}
			Screenshot(new[] { "newgame_clicked" });
			yield return new WaitForSecondsRealtime(0.8f);
			WorldSettingsWindow.Close();
			box.gameObject.SetActive(false);
			if (ok) Log("PASS: New Game box clicks"); else Fail("New Game box clicks");
		}

		#region Broken and odd files

		[ConsoleCommand(name: "CIBadFiles", docs: "Dev, main menu or editor: files a player hand-edits or that got broken are read without errors, with warnings and safe values: spawnpool.txt with words for numbers, negative and crossed distances, unknown keys and styles, a missing default plan, island names with spaces and letters like åäö; randomizer.txt and world_rules.txt with nonsense; a generator preset with nonsense and huge numbers; a world plan with broken lines; island files that are empty, random bytes, cut in half or from a newer version. SL3: an island file with broken quest lines (an unknown step kind, counts abc / 0 / 500, one field only, 12 step lines, quest10) is read without errors with safe counts and Check runs on it. Every real file is put back after")]
		public static void BadFilesCommand() { StartTest(BadFilesRoutine()); }

		static IEnumerator BadFilesRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			string dir = DynamicIslands.assetpath;
			string pool = System.IO.Path.Combine(dir, "spawnpool.txt"), rnd = System.IO.Path.Combine(dir, WorldRandomizer.DefaultsFileName), rules = System.IO.Path.Combine(dir, WorldRules.DefaultFileName);
			var backup = new Dictionary<string, string>();
			foreach (string p in new[] { pool, rnd, rules }) backup[p] = System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p) : null;
			var errors = new List<string>();
			var warnings = new List<string>();
			Application.LogCallback watch = (text, trace, type) =>
			{
				if (type == LogType.Error || type == LogType.Exception) errors.Add(text.Length > 160 ? text.Substring(0, 160) : text);
				else if (type == LogType.Warning && text.Contains("CUSTOM ISLANDS")) warnings.Add(text);
			};
			Application.logMessageReceived += watch;
			string defaultPlanBefore = WorldDirector.DefaultPlan;
			try
			{
				// spawnpool.txt, hand-edited badly
				float chanceBefore = CustomIslandSpawner.ChancePerKm;
				System.IO.File.WriteAllText(pool, "# a player's spawnpool\nchancePerKm = lots\nspawnDistanceMin = -50\nspawnDistanceMax = 10\nunloadDistance = 5\nregrowDays = -3\nminSpacing = -1\ngeneratedStyles = Martian, Lunar\ndefaultPlan = No such plan\nwingspan = 12\n   \nCastaway's hut 2\nÅäö island\n= 5\ncitest two thousand\n");
				warnings.Clear(); errors.Clear();
				CustomIslandSpawner.LoadPool(true);
				Check(ref ok, errors.Count == 0, "spawnpool.txt with nonsense: read without errors" + (errors.Count > 0 ? " - " + errors[0] : ""));
				Check(ref ok, CustomIslandSpawner.ChancePerKm == chanceBefore && CustomIslandSpawner.SpawnDistanceMin >= 20f && CustomIslandSpawner.SpawnDistanceMax >= CustomIslandSpawner.SpawnDistanceMin &&
					CustomIslandSpawner.UnloadDistance >= 300f && CustomIslandSpawner.RegrowDays >= 0 && CustomIslandSpawner.GeneratedStyles.Length > 0,
					"spawnpool.txt with nonsense: safe values (chance " + CustomIslandSpawner.ChancePerKm + ", spawn " + CustomIslandSpawner.SpawnDistanceMin + "-" + CustomIslandSpawner.SpawnDistanceMax + " m, unload " + CustomIslandSpawner.UnloadDistance + " m, regrow " + CustomIslandSpawner.RegrowDays + " days, styles " + CustomIslandSpawner.GeneratedStyles.Length + ")");
				Check(ref ok, warnings.Count(w => w.Contains("Ignoring unknown line")) >= 3, warnings.Count(w => w.Contains("Ignoring unknown line")) + " lines warned about (words for numbers, unknown keys and styles)");
				Check(ref ok, WorldPlan.Load(WorldDirector.DefaultPlan) == null && !WorldDirector.SetPlan(WorldDirector.DefaultPlan, false), "a default plan that doesn't exist: not used (the New Game box falls back to Random islands)");
				// randomizer.txt and world_rules.txt
				foreach (string junk in new[] { "level=extreme;seed=abc;off=colours,nonsense,;;", "\u0000ÿ binary \u0001", "", "level=" })
				{
					System.IO.File.WriteAllText(rnd, junk);
					errors.Clear();
					RandomizerSettings d = WorldRandomizer.Defaults;
					Check(ref ok, errors.Count == 0 && d != null && d.Level >= 0 && d.Level < RandomizerSettings.LevelNames.Length, "randomizer.txt '" + junk.Replace("\u0000", "\\0") + "': " + (d != null ? d.Describe() : "null"));
				}
				System.IO.File.WriteAllText(rules, "monsters=brutal\nbuildcost=-500\nbuildcost=abc\n=\nmonsters\n");
				errors.Clear();
				int m = MonsterDifficulty.Default, bc = BuildCost.Default;
				Check(ref ok, errors.Count == 0 && m == MonsterDifficulty.Normal && bc >= 0 && bc <= 100, "world_rules.txt with nonsense: monsters " + MonsterDifficulty.Name(m) + ", build cost " + bc + " %" + (errors.Count > 0 ? " - error: " + errors[0] : ""));
				System.IO.File.WriteAllText(rules, "buildcost=500\n");
				Check(ref ok, BuildCost.Default <= 100, "world_rules.txt with 500 %: clamped to " + BuildCost.Default + " %");
			}
			finally
			{
				foreach (var kv in backup) { if (kv.Value != null) System.IO.File.WriteAllText(kv.Key, kv.Value); else if (System.IO.File.Exists(kv.Key)) System.IO.File.Delete(kv.Key); }
				CustomIslandSpawner.LoadPool(true);
				WorldDirector.DefaultPlan = defaultPlanBefore;
			}
			// The upgrade table's world file lines with nonsense
			UtBadLines(ref ok, errors);
			// A generator preset with nonsense and huge numbers
			IslandGenSettings s = IslandGenSettings.FromText("Radius=99999\nHeight=-5\nSeed=abc\nStyle=77\nTrees=5\nnonsense\n=\n");
			Check(ref ok, s.Radius <= IslandGenSettings.MaxRadius && s.Radius >= IslandGenSettings.MinRadius && s.Height >= 2f && s.Style >= 0 && s.Style < TerrainPainter.Styles.Length && s.Amount(s.Trees) <= 1f,
				"a preset with nonsense: clamped (radius " + s.Radius + ", height " + s.Height + ", style " + s.Style + ", trees used as " + s.Amount(s.Trees) + ")");
			// A world plan with broken lines
			string planPath = WorldPlan.PathFor("cibadplan");
			System.IO.Directory.CreateDirectory(WorldPlan.Folder);
			System.IO.File.WriteAllText(planPath, "description=A broken plan\nrandom=perhaps\nrule=\nrule=|||||\nrule=home|start||island|cistory-home|ahead|300\nthis is not a rule\n\u0000\n");
			errors.Clear();
			WorldPlan bad = null;
			try { bad = WorldPlan.Load("cibadplan"); } catch (Exception e) { errors.Add(e.Message); }
			Check(ref ok, errors.Count == 0 && bad != null && WorldPlan.All().Contains("cibadplan"), "a plan with broken lines: read (" + (bad != null ? bad.Rules.Count + " rule(s) kept" : "null") + "), listed");
			System.IO.File.Delete(planPath);
			// Island files: empty, random bytes, cut in half, from a newer version - each refused with a message, never a crash
			// (the tests' own sample island's bytes, not whichever island the player has saved)
			byte[] whole = DynamicIslands.instance.GetEmbeddedFileBytes(SampleFixture) ?? new byte[0];
			var rng = new System.Random(5);
			byte[] noise = new byte[4096]; rng.NextBytes(noise);
			byte[] newer = (byte[])whole.Clone();
			if (newer.Length > 8) { newer[4] = 99; newer[5] = 0; newer[6] = 0; newer[7] = 0; }
			var cases = new Dictionary<string, byte[]> { { "empty", new byte[0] }, { "random bytes", noise }, { "cut in half", whole.Take(whole.Length / 2).ToArray() }, { "a newer format", newer } };
			foreach (var c in cases)
			{
				string path = IslandSpawner.PathFor("cibadisland");
				System.IO.File.WriteAllBytes(path, c.Value);
				string result;
				errors.Clear();
				try { IslandFile f = IslandFile.Load(path); result = f == null ? "refused" : "read (" + f.Objects.Count + " objects)"; }
				catch (Exception e) { result = "refused: " + e.GetType().Name + " " + e.Message; }
				bool listed = IslandSpawner.ListSavedIslands().Contains("cibadisland");
				// (in the editor: Open says it failed and keeps the island being edited)
				if (DynamicIslands.InEditor())
				{
					string editing = DynamicIslands.currentIslandName;
					bool opened = true;
					try { opened = DynamicIslands.LoadIsland("cibadisland"); } catch (Exception e) { result += "; the editor threw " + e.Message; }
					if (opened || DynamicIslands.currentIslandName != editing) result = "read: the editor opened it";
					yield return null;
				}
				System.IO.File.Delete(path);
				Check(ref ok, !result.StartsWith("read"), "an island file " + c.Key + ": " + (result.Length > 120 ? result.Substring(0, 120) : result) + (listed ? " (listed)" : ""));
			}
			// SL3: an island file whose quest lines are broken - read without errors, unknown steps skipped, counts kept to 1-99, Check runs
			{
				const string badQuestIsland = "cibadquest";
				string bqPath = IslandSpawner.PathFor(badQuestIsland);
				try
				{
					int res = 17;
					var heights = new float[res, res]; for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) heights[y, x] = 0.4f;
					var bq = new IslandFile { TerrainSize = new Vector3(64f, 60f, 64f), HeightmapResolution = res, Heights = heights, Name = badQuestIsland };
					var lines = new List<string> { "teleport|nowhere|1|an unknown kind", "collect|cikey|abc|count is a word", "collect|cikey|0|count is zero", "kill|Warthog|500|count is far too big", "reach" };
					for (int i = 0; i < 7; i++) lines.Add("reach|gate|1|step " + (i + 6)); // (12 lines in all)
					bq.Props["quest.title"] = "Broken quest";
					bq.Props["quest.steps"] = string.Join("\n", lines.ToArray());
					bq.Props["quest2.title"] = "Quest two";
					bq.Props["quest2.steps"] = "reach|gate|1|fine\nnonsense|x|1";
					bq.Props["quest3.title"] = "Far too long";
					bq.Props["quest3.steps"] = string.Join("\n", Enumerable.Range(1, IslandQuest.MaxFileSteps + 20).Select(i => "reach|gate|1|step " + i).ToArray());
					bq.Props["quest9.steps"] = "reach|gate|1|the ninth";
					bq.Props["quest10.title"] = "A tenth quest"; // (an island has at most nine)
					bq.Props["quest10.steps"] = "reach|gate|1|the tenth";
					bq.Save(bqPath);
					errors.Clear();
					IslandFile back = null;
					string thrown = null;
					IslandQuest main = null, second = null, third = null;
					int quests = 0;
					try
					{
						back = IslandFile.Load(bqPath);
						main = IslandQuest.From(back.Props, 0, true);
						second = IslandQuest.From(back.Props, 1, true);
						third = IslandQuest.From(back.Props, 2, true);
						quests = IslandQuest.CountIn(back.Props);
					}
					catch (Exception e) { thrown = e.GetType().Name + " " + e.Message; }
					Check(ref ok, thrown == null && errors.Count == 0 && main != null, "SL3 broken quest lines: read without errors" + (thrown != null ? " - " + thrown : errors.Count > 0 ? " - " + errors[0] : ""));
					if (main != null)
					{
						Check(ref ok, !main.Steps.Any(st => st.Type == "teleport") && main.Steps.All(st => IslandQuest.Types.Contains(st.Type)), "SL3: the unknown step kind is skipped (" + main.Steps.Count + " of 12 lines kept)");
						Check(ref ok, main.Steps.All(st => st.Count >= 1 && st.Count <= 99), "SL3: every count is kept to 1-99");
						var counts = main.Steps.Take(4).Select(st => st.Count).ToList();
						Check(ref ok, counts.Count == 4 && counts[0] == 1 && counts[1] == 1 && counts[2] == 99 && counts[3] == 1, "SL3: 'abc' and 0 become 1, 500 becomes 99, one field only becomes a step with count 1 (" + string.Join(",", counts.Select(c => c.ToString()).ToArray()) + ")");
						// (11 good lines: more than the editor's 10 - a recipe's quest runs to 40 - all read)
						Check(ref ok, main.Steps.Count == 11, "SL3: a quest with more steps than the editor adds is read whole (" + main.Steps.Count + " of 11)");
					}
					Check(ref ok, third != null && third.Steps.Count == IslandQuest.MaxFileSteps, "SL3: a quest file with " + (IslandQuest.MaxFileSteps + 20) + " steps is read up to " + IslandQuest.MaxFileSteps + " (" + (third != null ? third.Steps.Count : 0) + ")");
					Check(ref ok, second != null && second.Steps.Count == 1 && quests <= IslandQuest.MaxQuests && quests == 9, "SL3: quest 2 skips its unknown line; the island counts " + quests + " quests (quest10.* ignored, at most " + IslandQuest.MaxQuests + ")");
					// Check runs on it: the island's own quest findings, and a plan whose rule brings it
					errors.Clear();
					string checkThrown = null;
					int found = 0;
					try
					{
						found += PlanChecker.QuestFindings(back).Count;
						var badPlan = new WorldPlan { Name = "CI bad quest", Random = false };
						badPlan.Rules.Add(IntroRule.Parse("start | island:" + badQuestIsland + " | start | ahead:300 | msg | Start"));
						badPlan.Rules.Add(IntroRule.Parse("after | type:camp | step:start:12 | ahead:300 | msg | After"));
						found += PlanChecker.Check(badPlan, false, true).Count;
					}
					catch (Exception e) { checkThrown = e.GetType().Name + " " + e.Message; }
					Check(ref ok, checkThrown == null && errors.Count == 0, "SL3: Check runs on the broken quest without throwing (" + found + " finding(s))" + (checkThrown != null ? " - " + checkThrown : ""));
				}
				finally { try { if (System.IO.File.Exists(bqPath)) System.IO.File.Delete(bqPath); } catch { } }
			}
			// World file lines with nonsense (needs a test world 'CI ...': skipped, and logged, in the menu or the editor)
			GwBadWorldLines(ref ok);
			// Broken terrain stamp and object group files
			var gwStamps = new[] { true };
			yield return GwBadStampsPart(gwStamps);
			ok &= gwStamps[0];
			Application.logMessageReceived -= watch;
			if (ok) Log("PASS: broken files"); else Fail("broken files");
		}

		#endregion

		[ConsoleCommand(name: "CIPlanRules", docs: "Dev, in game (host): the plan rules no other test plays in a world - 'on day' (today: brought; day 999: waits), 'quest step done' (the start island's first step), 'after rule' (a chain), 'from the spawn pool'; a rule whose saved island is missing waits, says why once, and breaks nothing; a plan edited while the world plays (a new rule fires); a plan deleted (its rules stop, the islands stay, nothing breaks). The world's plan, its islands and km are put back after")]
		public static void PlanRulesCommand() { StartTest(PlanRulesRoutine()); }

		static IEnumerator PlanRulesRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("plan rules: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string planBefore = WorldDirector.PlanName;
			bool autoBefore = CustomIslandSpawner.Enabled;
			float sailedBefore = WorldDirector.Sailed;
			var doneBefore = WorldDirector.Done.ToList();
			int before = IslandWorldState.Islands.Count;
			int today = 0;
			try { today = WorldManager.DayCounter; } catch { }
			const string plan = "ci plan rules";
			RuleIsland("ciplanr", "Rules Isle").Save(IslandSpawner.PathFor("ciplanr"));
			if (System.IO.File.Exists(IslandSpawner.PathFor("cigoneisland"))) System.IO.File.Delete(IslandSpawner.PathFor("cigoneisland"));
			WorldPlan.Parse(plan, "random = off\n" +
				"rule = start | island:ciplanr | start | ahead:300 | | Start\n" +
				"rule = today | type:tropical | day:" + today + " | ahead:900 | | Today\n" +
				"rule = later | type:desert | day:999 | ahead:900 | | Later\n" +
				"rule = stepped | type:snowy | step:start:1 | near:start:700:north | | Step\n" +
				"rule = chained | type:forest | rule:stepped | near:stepped:700:east | | Chain\n" +
				"rule = pooled | pool | rule:chained | ahead:1200 | | Pool\n" +
				"rule = gone | island:cigoneisland | start | ahead:600 | | Gone\n").Save();
			var warnings = new List<string>();
			Application.LogCallback watch = (text, trace, type) => { if (text.Contains("cigoneisland")) warnings.Add(text); };
			Application.logMessageReceived += watch;
			try
			{
				Check(ref ok, WorldDirector.SetPlan(plan, true), "the world gets the plan");
				WorldDirector.Sailed = 0f;
				WorldDirector.Done.Clear();
				WorldDirector.Evaluate();
				IslandWorldState.Entry start = WorldDirector.Refs("start", null).FirstOrDefault();
				Check(ref ok, start != null, "'start' brings ciplanr");
				Check(ref ok, WorldDirector.Refs("today", null).Count == 1, "'on day " + today + "' (today) brings its island");
				Check(ref ok, WorldDirector.Refs("later", null).Count == 0 && !WorldDirector.Done.Contains("later"), "'on day 999' waits");
				Check(ref ok, WorldDirector.Refs("stepped", null).Count == 0, "'quest step done' waits for the step");
				for (float t = 0; start != null && start.Root == null && t < 40f; t += 0.5f) yield return new WaitForSeconds(0.5f);
				if (start != null && start.Root != null)
				{
					start.Root.GetComponentInChildren<TriggerZone>().Enter();
					WorldDirector.Evaluate();
					Check(ref ok, WorldDirector.Refs("stepped", null).Count == 1, "the start island's first quest step brings 'stepped' (" + QuestTracker.StepOf(start) + " step(s) done)");
					WorldDirector.Evaluate();
					Check(ref ok, WorldDirector.Refs("chained", null).Count == 1, "'after rule stepped' brings 'chained' after it");
					WorldDirector.Evaluate();
					string pool = CustomIslandSpawner.PickFromPool();
					Check(ref ok, WorldDirector.Done.Contains("pooled") == (pool != null), "'from the spawn pool' after 'chained': " + (pool != null ? "brought " + string.Join(", ", WorldDirector.Refs("pooled", null).Select(e => e.HostName).ToArray()) : "the pool is empty - it waits"));
				}
				// A saved island that isn't there: the rule waits, says why once, nothing breaks
				for (int i = 0; i < 5; i++) WorldDirector.Evaluate();
				Check(ref ok, !WorldDirector.Done.Contains("gone") && WorldDirector.Refs("gone", null).Count == 0, "a rule whose island is missing waits (" + warnings.Count + " log line(s) about it over 6 checks)");
				Check(ref ok, warnings.Count <= 2, "... and doesn't fill the log (" + warnings.Count + " lines)");
				// The plan edited while the world plays: a new rule fires; then deleted: its rules stop, the islands stay
				WorldPlan p = WorldPlan.Load(plan);
				p.Rules.Add(IntroRule.Parse("added | type:volcanic | start | ahead:1500 | | Added"));
				p.Save();
				Check(ref ok, WorldDirector.SetPlan(plan, false), "the plan edited and read again");
				WorldDirector.Evaluate();
				Check(ref ok, WorldDirector.Refs("added", null).Count == 1, "the edited plan's new rule fires");
				int islands = IslandWorldState.Islands.Count;
				System.IO.File.Delete(WorldPlan.PathFor(plan));
				bool reread = true;
				try { reread = WorldDirector.SetPlan(plan, false); } catch (Exception e) { Check(ref ok, false, "a deleted plan threw: " + e.Message); }
				for (int i = 0; i < 3; i++) WorldDirector.Evaluate();
				Check(ref ok, !reread && IslandWorldState.Islands.Count == islands, "the plan file deleted: it can't be read again, the world goes on with its own copy (nothing new is due), the islands it brought stay (" + islands + ")");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(before).Select(e => e.Id).ToList(), true);
				if (System.IO.File.Exists(WorldPlan.PathFor(plan))) System.IO.File.Delete(WorldPlan.PathFor(plan));
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				WorldDirector.Sailed = sailedBefore;
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
				CustomIslandSpawner.Enabled = autoBefore;
			}
			if (ok) Log("PASS: plan rules"); else Fail("plan rules");
		}

		#region The island files window and the click-twice buttons

		static T Private<T>(object o, string field) where T : class
		{
			FieldInfo f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
			return f != null ? f.GetValue(o) as T : null;
		}

		[ConsoleCommand(name: "CIFilesWindow", docs: "Dev, editor: the Islands window (Open / Save as) clicked as a player does: Save with no name and with a name holding : * ? is refused with a message; a new name saves and closes; the name of another saved island asks first and overwrites on the second Save; the height field clamps 9999 to 250, reads nonsense as 0; Open with a missing name says so; clicking an island fills its name, a double-click opens it; Delete asks first, typing in between cancels the question, the second Delete removes the file and its row; Close. Then the other click-twice buttons: the top bar's New (once: nothing happens; twice: an empty island) and a generator preset's ×. The island open before is opened again after")]
		public static void FilesWindowCommand() { StartTest(FilesWindowRoutine()); }

		static IEnumerator FilesWindowRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			string nameBefore = DynamicIslands.currentIslandName;
			float elevationBefore = DynamicIslands.currentElevation;
			string keep = "cifiles-keep";
			DynamicIslands.SaveIsland(keep);
			const string a = "cifiles-a", b = "cifiles-b";
			DynamicIslands.SaveIsland(a);
			DynamicIslands.SaveIsland(keep);
			if (System.IO.File.Exists(IslandSpawner.PathFor(b))) System.IO.File.Delete(IslandSpawner.PathFor(b));
			Type t = typeof(IslandFilesWindow);
			try
			{
				IslandFilesWindow.Open();
				yield return null;
				GameObject w = WindowObject(t);
				if (w == null) { Fail("files window: it didn't open"); yield break; }
				IslandFilesWindow win = w.GetComponent<IslandFilesWindow>();
				InputField name = Private<InputField>(win, "nameField"), height = Private<InputField>(win, "elevationField");
				Text status = Private<Text>(win, "status");
				Check(ref ok, name.text == keep, "opens with the current island's name (" + name.text + ")");
				name.text = "   "; Click(w, "Save"); yield return null;
				Check(ref ok, IslandFilesWindow.IsOpen && status.text.Contains("Type a name"), "Save with no name: refused (" + status.text + ")");
				name.text = "bad:name*?"; Click(w, "Save"); yield return null;
				Check(ref ok, IslandFilesWindow.IsOpen && status.text.Contains("can't contain") && !IslandSpawner.ListSavedIslands().Any(n => n.StartsWith("bad")), "Save with : * ?: refused (" + status.text + ")");
				name.text = b; Click(w, "Save"); yield return null;
				Check(ref ok, !IslandFilesWindow.IsOpen && System.IO.File.Exists(IslandSpawner.PathFor(b)) && DynamicIslands.currentIslandName == b, "a new name: saved as '" + b + "', the window closes");
				IslandFilesWindow.Open(); yield return null;
				DateTime written = System.IO.File.GetLastWriteTimeUtc(IslandSpawner.PathFor(a));
				yield return new WaitForSecondsRealtime(1.1f);
				name.text = a; Click(w, "Save"); yield return null;
				Check(ref ok, IslandFilesWindow.IsOpen && status.text.Contains("already exists") && System.IO.File.GetLastWriteTimeUtc(IslandSpawner.PathFor(a)) == written, "another island's name: asks first, the file untouched (" + status.text + ")");
				Click(w, "Save"); yield return null;
				Check(ref ok, !IslandFilesWindow.IsOpen && System.IO.File.GetLastWriteTimeUtc(IslandSpawner.PathFor(a)) > written, "Save again: overwritten, closed");
				IslandFilesWindow.Open(); yield return null;
				var heights = new List<string>();
				foreach (var c in new[] { new KeyValuePair<string, float>("9999", IslandSpawner.MaxElevation), new KeyValuePair<string, float>("-9999", IslandSpawner.MinElevation), new KeyValuePair<string, float>("abc", 0f), new KeyValuePair<string, float>("-30", -30f), new KeyValuePair<string, float>("0", 0f) })
				{
					height.text = c.Key; height.onEndEdit.Invoke(c.Key); yield return null;
					heights.Add(c.Key + " > " + height.text);
					if (DynamicIslands.currentElevation != c.Value || height.text != c.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)) Check(ref ok, false, "the height field: '" + c.Key + "' gave " + DynamicIslands.currentElevation + " (wanted " + c.Value + ")");
				}
				Check(ref ok, true, "the height field: " + string.Join(", ", heights.ToArray()));
				name.text = "cifiles-missing"; Click(w, "Open"); yield return null;
				Check(ref ok, IslandFilesWindow.IsOpen && status.text.Contains("no saved island"), "Open with a missing name: says so (" + status.text + ")");
				Button row = w.GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.name == "Island_" + b);
				Check(ref ok, row != null, "'" + b + "' is in the list");
				if (row != null)
				{
					row.onClick.Invoke(); yield return null;
					Check(ref ok, name.text == b && IslandFilesWindow.IsOpen, "clicking an island fills its name");
					DynamicIslands.currentIslandName = keep;
					row.onClick.Invoke(); row.onClick.Invoke(); yield return null;
					Check(ref ok, !IslandFilesWindow.IsOpen && DynamicIslands.currentIslandName == b, "a double-click opens it (" + DynamicIslands.currentIslandName + ")");
				}
				IslandFilesWindow.Open(); yield return null;
				name.text = b; Click(w, "Delete"); yield return null;
				Check(ref ok, System.IO.File.Exists(IslandSpawner.PathFor(b)) && status.text.Contains("Delete again"), "Delete: asks first, the file stays (" + status.text + ")");
				name.text = b + "x"; name.text = b; Click(w, "Delete"); yield return null;
				Check(ref ok, System.IO.File.Exists(IslandSpawner.PathFor(b)) && status.text.Contains("Delete again"), "typing in between: asks again");
				Click(w, "Delete"); yield return null; yield return null;
				Check(ref ok, !System.IO.File.Exists(IslandSpawner.PathFor(b)) && !w.GetComponentsInChildren<Button>(false).Any(x => x.name == "Island_" + b), "Delete again: the file and its row are gone");
				Click(w, "Close"); yield return null;
				Check(ref ok, !IslandFilesWindow.IsOpen, "Close closes it");
			}
			finally { IslandFilesWindow.Close(); }
			// The top bar's New: once nothing, twice an empty island
			DynamicIslands.LoadIsland(keep);
			yield return null;
			MethodInfo confirmNew = typeof(EditorUI).GetMethod("ConfirmNew", BindingFlags.NonPublic | BindingFlags.Static);
			int objects = GameObject.Find("PlacedObjects").transform.childCount;
			confirmNew.Invoke(null, null); yield return null;
			Check(ref ok, DynamicIslands.currentIslandName == keep && GameObject.Find("PlacedObjects").transform.childCount == objects, "New once: nothing happens yet (" + objects + " objects)");
			confirmNew.Invoke(null, null); yield return null; yield return null;
			Check(ref ok, DynamicIslands.currentIslandName == "myisland" && GameObject.Find("PlacedObjects").transform.childCount == 0, "New twice: an empty island");
			// A generator preset's ×
			System.IO.Directory.CreateDirectory(GeneratorWindow.PresetFolder);
			string preset = System.IO.Path.Combine(GeneratorWindow.PresetFolder, "cipreset-del.txt");
			System.IO.File.WriteAllText(preset, new IslandGenSettings().ToText());
			GeneratorWindow.Open(); yield return null;
			GeneratorWindow gw = GeneratorWindow.Instance;
			typeof(GeneratorWindow).GetMethod("RefreshPresets", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gw, null);
			yield return null;
			Button x2 = null;
			foreach (Transform r in gw.GetComponentsInChildren<Transform>(true).Where(r => r.name == "PresetRow"))
			{
				Button[] bs = r.GetComponentsInChildren<Button>(true);
				for (int i = 0; i + 1 < bs.Length; i++) if (UIKit.LabelOf(bs[i]) != null && UIKit.LabelOf(bs[i]).text == "cipreset-del") x2 = bs[i + 1];
			}
			Check(ref ok, x2 != null, "the preset has its × button");
			if (x2 != null)
			{
				x2.onClick.Invoke(); yield return null;
				Check(ref ok, System.IO.File.Exists(preset), "preset × once: asks, kept");
				x2.onClick.Invoke(); yield return null;
				Check(ref ok, !System.IO.File.Exists(preset), "preset × twice: deleted");
			}
			GeneratorWindow.Close();
			if (System.IO.File.Exists(preset)) System.IO.File.Delete(preset);
			DynamicIslands.LoadIsland(keep);
			DynamicIslands.currentIslandName = nameBefore;
			DynamicIslands.currentElevation = elevationBefore;
			EditorUI.RefreshIsland();
			foreach (string n in new[] { a, b, keep }) if (System.IO.File.Exists(IslandSpawner.PathFor(n))) System.IO.File.Delete(IslandSpawner.PathFor(n));
			if (ok) Log("PASS: files window"); else Fail("files window");
		}

		#endregion

		[ConsoleCommand(name: "CIPlanWindowEdits", docs: "Dev, editor: the World Plans window's rule buttons and Close: ▼ moves a rule down, ▲ on the first and ▼ on the last do nothing; the first template added twice gets new ids for the second copy and its references follow them (never pointing at the first copy); Close throws unsaved changes away (the file and a reopened plan as before); Save keeps a new order")]
		public static void PlanWindowEditsCommand() { StartTest(PlanWindowEditsRoutine()); }

		static IEnumerator PlanWindowEditsRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			const string name = "ci plan edits";
			string text = "description = Edits\nrandom = off\nrule = a | type:tropical | start | ahead:300 | |\nrule = b | type:desert | rule:a | ahead:600 | |\nrule = c | type:snowy | rule:b | ahead:900 | |\n";
			WorldPlan.Parse(name, text).Save();
			string onDisk = System.IO.File.ReadAllText(WorldPlan.PathFor(name));
			Func<WorldPlan> plan = () => { GameObject w = WindowObject(typeof(WorldPlanWindow)); return w != null ? Private<WorldPlan>(w.GetComponent<WorldPlanWindow>(), "plan") : null; };
			Func<string> order = () => plan() != null ? string.Join(",", plan().Rules.Select(r => r.Id).ToArray()) : "";
			Func<List<GameObject>> cards = () => { GameObject w = WindowObject(typeof(WorldPlanWindow)); return w == null ? new List<GameObject>() : w.GetComponentsInChildren<RectTransform>(false).Where(r => r.name == "Rule").Select(r => r.gameObject).ToList(); };
			try
			{
				WorldPlanWindow.Open(name);
				yield return null; yield return null;
				GameObject win = WindowObject(typeof(WorldPlanWindow));
				if (win == null || plan() == null) { Fail("plan window edits: it didn't open"); yield break; }
				Check(ref ok, order() == "a,b,c" && cards().Count == 3, "opens with 3 rule cards (" + order() + ")");
				Click(cards()[0], "▲"); yield return null; yield return null;
				Check(ref ok, order() == "a,b,c", "▲ on the first rule: nothing (" + order() + ")");
				Click(cards()[2], "▼"); yield return null; yield return null;
				Check(ref ok, order() == "a,b,c", "▼ on the last rule: nothing (" + order() + ")");
				Click(cards()[0], "▼"); yield return null; yield return null;
				Check(ref ok, order() == "b,a,c", "▼ on the first rule moves it down (" + order() + ")");
				// The first template twice
				string key = WorldPlanTemplates.All[0].Key;
				WorldPlan t = WorldPlanTemplates.Get(key);
				var tIds = new HashSet<string>(t.Rules.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
				for (int n = 0; n < 2; n++)
				{
					Check(ref ok, Click(win, "Templates..."), "Templates... clicked (" + (n + 1) + ")");
					yield return null; yield return null;
					GameObject choice = WindowObject(typeof(ChoiceWindow));
					Button b = choice != null ? choice.GetComponentsInChildren<Button>(false).FirstOrDefault(x => UIKit.LabelOf(x) != null && (UIKit.LabelOf(x).text == key || UIKit.LabelOf(x).text.StartsWith(key + "   "))) : null;
					Check(ref ok, b != null, "the template '" + key + "' is offered");
					if (b != null) b.onClick.Invoke();
					yield return null; yield return null;
				}
				List<IntroRule> rules = plan().Rules;
				var ids = rules.Select(r => r.Id).ToList();
				Check(ref ok, rules.Count == 3 + 2 * t.Rules.Count && ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ids.Count, "the template twice: " + rules.Count + " rules, every id different (" + string.Join(",", ids.ToArray()) + ")");
				var second = rules.Skip(3 + t.Rules.Count).ToList();
				var wrong = new List<string>();
				for (int i = 0; i < second.Count && i < t.Rules.Count; i++)
				{
					IntroRule o = t.Rules[i], c = second[i];
					if (tIds.Contains(o.WhenRef) && (c.WhenRef.Equals(o.WhenRef, StringComparison.OrdinalIgnoreCase) || !ids.Contains(c.WhenRef))) wrong.Add(c.Id + " when " + c.WhenRef);
					if (tIds.Contains(o.WhereRef) && (c.WhereRef.Equals(o.WhereRef, StringComparison.OrdinalIgnoreCase) || !ids.Contains(c.WhereRef))) wrong.Add(c.Id + " where " + c.WhereRef);
				}
				Check(ref ok, wrong.Count == 0, "the second copy's references follow its new ids" + (wrong.Count > 0 ? " - not: " + string.Join(", ", wrong.ToArray()) : ""));
				// Close throws it all away
				Private<InputField>(win.GetComponent<WorldPlanWindow>(), "descriptionField").text = "changed, not saved";
				Check(ref ok, Click(win, "Close"), "Close clicked");
				yield return null;
				Check(ref ok, !WorldPlanWindow.IsOpen && System.IO.File.ReadAllText(WorldPlan.PathFor(name)) == onDisk, "Close: the file as before");
				WorldPlanWindow.Open(name);
				yield return null; yield return null;
				win = WindowObject(typeof(WorldPlanWindow));
				Check(ref ok, order() == "a,b,c" && plan().Description == "Edits", "opened again: the saved plan (" + order() + ", '" + plan().Description + "')");
				Click(cards()[1], "▼"); yield return null; yield return null;
				Check(ref ok, Click(win, "Save"), "Save clicked");
				yield return null;
				WorldPlan saved = WorldPlan.Load(name);
				Check(ref ok, saved != null && string.Join(",", saved.Rules.Select(r => r.Id).ToArray()) == "a,c,b", "Save keeps the new order (" + (saved != null ? string.Join(",", saved.Rules.Select(r => r.Id).ToArray()) : "null") + ")");
			}
			finally
			{
				WorldPlanWindow.Close();
				ChoiceWindow.Close();
				if (System.IO.File.Exists(WorldPlan.PathFor(name))) System.IO.File.Delete(WorldPlan.PathFor(name));
			}
			if (ok) Log("PASS: plan window edits"); else Fail("plan window edits");
		}
		#region Raft's own settings

		/// <summary>A type of Raft's by its full name, from whichever of the game's assemblies has it.</summary>
		static Type RaftType(string fullName)
		{
			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type t = null;
				try { t = a.GetType(fullName, false); } catch { }
				if (t != null) return t;
			}
			return null;
		}

		[ConsoleCommand(name: "CIRaftSettings", docs: "Dev, world (host, a custom island loaded near the raft): Raft's own settings as a player sets them (the settings screen's dropdowns, toggles and sliders, so Raft applies them): every graphics quality, water, texture, shadow, reflection and FPS option, ambient occlusion and anti-aliasing, FOV at its ends; then every language of Raft's. After each: no error of the mod, the island still there with its ground drawn, the mod's item names, journal and quest panel still work. Everything put back after; pictures shot_settings_*")]
		public static void RaftSettingsCommand(string[] args) { StartTest(RaftSettingsRoutine(args != null && args.Length > 0 ? args[0] : null)); }

		static IEnumerator RaftSettingsRoutine(string only = null)
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("Raft's settings: run in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			IslandWorldState.Entry island = IslandWorldState.Islands.Where(e => e.Root != null && e.Root.GetComponentInChildren<Terrain>() != null).OrderBy(e => (e.Position - (CustomIslandSpawner.RaftPosition ?? Vector3.zero)).sqrMagnitude).FirstOrDefault();
			if (island == null) { Fail("Raft's settings: no custom island with land loaded (CIMPIsland first)"); yield break; }
			int objects = island.Root.GetComponentsInChildren<IslandObjectRef>(true).Length;
			var errors = new List<string>();
			Application.LogCallback watch = (text, trace, type) =>
			{
				if ((type == LogType.Error || type == LogType.Exception) && (text.Contains("CUSTOM ISLANDS") || (trace ?? "").Contains("DynamicIslands"))) errors.Add(text.Length > 160 ? text.Substring(0, 160) : text);
			};
			Func<string> islandState = () =>
			{
				if (island.Root == null) return "the island is gone";
				Terrain t = island.Root.GetComponentInChildren<Terrain>();
				int now = island.Root.GetComponentsInChildren<IslandObjectRef>(true).Length;
				if (t == null || !t.enabled || !t.drawHeightmap || t.materialTemplate == null) return "its ground isn't drawn";
				if (now != objects) return "objects " + objects + " -> " + now;
				return null;
			};
			Application.logMessageReceived += watch;
			try
			{
				// Graphics: every option of every dropdown, then the toggles and the field of view
				Type boxType = RaftType("GraphicsSettingsBox");
				Component box = boxType == null ? null : Resources.FindObjectsOfTypeAll(boxType).OfType<Component>().FirstOrDefault(c => c.gameObject.scene.IsValid());
				if (box == null) Check(ref ok, false, "Raft's graphics settings box not found");
				else
				{
					Type bt = box.GetType();
					var dropdowns = new[] { "qualitySettingsDropdown", "waterQualityDropdown", "textureQualityDropdown", "shadowTypeDropdown", "shadowCascadeDropdown", "shadowResolutionDropdown", "reflectionsDropdown", "fpsCapDropdown" };
					foreach (string name in dropdowns)
					{
						if (only != null && name != only) continue; // (CIRaftSettings <dropdown>: that one only)
						FieldInfo f = bt.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
						Dropdown d = f != null ? f.GetValue(box) as Dropdown : null;
						if (d == null) { Log("  (no " + name + " in this Raft)"); continue; }
						int was = d.value;
						var bad = new List<string>();
						for (int i = 0; i < d.options.Count; i++)
						{
							errors.Clear();
							d.value = i; // (Raft's own handler applies it)
							yield return new WaitForSecondsRealtime(0.8f);
							string s = islandState();
							if (errors.Count > 0 || s != null) bad.Add(d.options[i].text + ": " + (s ?? errors[0]));
							if (name == "qualitySettingsDropdown" && (i == 0 || i == d.options.Count - 1)) { Screenshot(new[] { "settings_quality_" + i }); yield return new WaitForSecondsRealtime(0.6f); }
						}
						d.value = was;
						yield return new WaitForSecondsRealtime(0.5f);
						Check(ref ok, bad.Count == 0, "graphics " + name.Replace("Dropdown", "") + ": " + d.options.Count + " options" + (bad.Count == 0 ? ", the island fine with each" : " - " + string.Join("; ", bad.Take(3).ToArray())));
					}
					if (only == null) foreach (string name in new[] { "aoToggle", "aaToggle", "vsyncToggle" })
					{
						FieldInfo f = bt.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
						Toggle tg = f != null ? f.GetValue(box) as Toggle : null;
						if (tg == null) continue;
						bool was = tg.isOn;
						errors.Clear();
						tg.isOn = !was; yield return new WaitForSecondsRealtime(0.5f);
						string s = islandState();
						tg.isOn = was; yield return new WaitForSecondsRealtime(0.3f);
						Check(ref ok, errors.Count == 0 && s == null, "graphics " + name.Replace("Toggle", "") + " switched: " + (s ?? (errors.Count > 0 ? errors[0] : "the island fine")));
					}
					FieldInfo ff = bt.GetField("FOVSlider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					Slider fov = ff != null ? ff.GetValue(box) as Slider : null;
					if (fov != null && only == null)
					{
						float was = fov.value;
						foreach (float v in new[] { fov.minValue, fov.maxValue })
						{
							errors.Clear();
							fov.value = v; yield return new WaitForSecondsRealtime(0.5f);
							Check(ref ok, errors.Count == 0 && islandState() == null, "field of view " + v.ToString("F0") + ": the island fine");
						}
						fov.value = was;
					}
				}

				// Languages: every one of Raft's; the mod's texts that come from Raft (item names) and its own windows
				Type lm = RaftType("I2.Loc.LocalizationManager");
				if (lm == null || only != null) Log("  (languages not checked" + (only != null ? ": " + only + " only)" : ": no I2 localization in this Raft)"));
				else
				{
					PropertyInfo current = lm.GetProperty("CurrentLanguage", BindingFlags.Public | BindingFlags.Static);
					MethodInfo all = lm.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GetAllLanguages");
					string was = current != null ? current.GetValue(null, null) as string : null;
					var langs = new List<string>();
					if (all != null)
					{
						object list = all.Invoke(null, all.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)true : null).ToArray());
						if (list is IEnumerable) langs = ((IEnumerable)list).Cast<object>().Select(o => o.ToString()).ToList();
					}
					var bad = new List<string>();
					foreach (string lang in langs)
					{
						errors.Clear();
						try { current.SetValue(null, lang, null); } catch (Exception e) { bad.Add(lang + ": " + e.Message); continue; }
						yield return new WaitForSecondsRealtime(0.5f);
						string plank = ContentCatalog.ItemLabel("Plank"), rope = ContentCatalog.ItemLabel("Rope");
						try { JournalWindow.Open(); JournalWindow.Close(); IslandInfo.Show("Quest", "", "A test line"); } catch (Exception e) { bad.Add(lang + ": journal or quest panel: " + e.Message); }
						yield return null;
						if (string.IsNullOrEmpty(plank) || string.IsNullOrEmpty(rope) || errors.Count > 0) bad.Add(lang + ": items '" + plank + "', '" + rope + "'" + (errors.Count > 0 ? ", " + errors[0] : ""));
					}
					if (was != null && current != null) current.SetValue(null, was, null);
					Check(ref ok, langs.Count > 0 && bad.Count == 0, langs.Count + " languages (" + string.Join(", ", langs.Take(12).ToArray()) + ")" + (bad.Count == 0 ? ": item names, journal and quest panel fine in each" : " - " + string.Join("; ", bad.Take(4).ToArray())));
				}
			}
			finally { Application.logMessageReceived -= watch; }
			if (ok) Log("PASS: Raft's settings"); else Fail("Raft's settings");
		}

		#endregion
	}
}

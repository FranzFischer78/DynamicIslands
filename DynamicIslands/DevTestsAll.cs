using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using RuntimeGizmos;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// The "everything" test (tools\alltests.ps1): every button of the editor and its windows, the in-world windows,
	/// the editor's state for the keyboard test, save/load round trips of every saved island, and snapshots of a
	/// world to compare after saving, loading, leaving, joining again and restarting Raft.
	/// </summary>
	public static partial class DevTests
	{
		#region Every button

		/// <summary>Buttons the walk leaves alone: leaving the editor is tested on its own at the end.</summary>
		// (all leave the editor for another scene - the walk would lose its screens; Test in a world has its own suite, CIIslandTest;
		// the plan window's Test this plan and Preview notebook have CIPlanTest and CIQuestBookPreview - 10-16 a plan with a main
		// story let them through, the plan's test world opened mid-walk and every screen after it had no editor;
		// Use in a plan... saves the island and adds it to a real plan - CIUseInPlan has it: 10-16 the walk put an island into
		// the (then) sample plan Adventure and then sat in that 90-rule plan's Check)
		static readonly HashSet<string> ButtonSkip = new HashSet<string> { "Main menu", "Test", "Test this plan", "Preview notebook", "Use in a plan..." };
		/// <summary>A list with more buttons than this (object tiles, items, sounds, plan cards) is sampled: its first three.</summary>
		const int ListSample = 3, ListThreshold = 8;

		class ButtonRun
		{
			public int Pressed, Windows;
			public readonly HashSet<Button> Done = new HashSet<Button>();
			public readonly HashSet<string> Keys = new HashSet<string>(), Walked = new HashSet<string>();
			public readonly List<string> Errors = new List<string>(), Unclosed = new List<string>(), Sampled = new List<string>();
			public readonly Dictionary<string, int> PerScreen = new Dictionary<string, int>();
			public string Current = "";
			public readonly float Start = Time.realtimeSinceStartup;
			// (where it is and since when: RUN2 10-21 - 45 min silent on the first screen of a minimised box Raft, twice, and
			// nothing said where; each screen has its own time limit, CIStopTests says where it was stopped)
			public string Screen = "";
			public float ScreenStart = Time.realtimeSinceStartup, ScreenLimit = float.PositiveInfinity, ThisLimit = float.PositiveInfinity;   // (CIButtons: 4 min a screen; CIWorldButtons: none)
			public bool Active;
			public bool ScreenOver { get { return Time.realtimeSinceStartup - ScreenStart > ThisLimit; } }
			public string Where()
			{
				return "'" + Current + "' (screen '" + Screen + "', " + Pressed + " pressed, " + Mathf.RoundToInt(Time.realtimeSinceStartup - ScreenStart) + " s on this screen, "
					+ Mathf.RoundToInt(Time.realtimeSinceStartup - Start) + " s in all, a frame " + Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f) + " ms, " + PlacedCount() + " objects)";
			}
		}

		/// <summary>Labels of presses that may take long or open something (a window, a download, a new island): said before the press.</summary>
		static readonly string[] ButtonMayBlock = { "...", "Generate", "Download", "Install", "Update", "Open", "Load", "Import", "Export", "Library", "New" };

		static int PlacedCount()
		{
			GameObject placed = GameObject.Find("PlacedObjects");
			return placed != null ? placed.transform.childCount : 0;
		}

		/// <summary>From a small island again when a press made a big one (all windows closed).</summary>
		// (the Island tab's Generate made an 11300-object one, and each screen after it took 5-10 minutes - 10-13; a Generate
		// pressed early on a screen made the rest of that screen crawl the same way)
		static bool ResetBigIsland(ButtonRun run)
		{
			int n = PlacedCount();
			if (!DynamicIslands.InEditor() || n <= 1000) return false;
			Log("  buttons: a new small island after '" + run.Current + "' (" + n + " objects)");
			DynamicIslands.NewIsland();
			DynamicIslands.currentIslandName = "cibuttons";
			return true;
		}

		static ButtonRun buttonRun;

		[ConsoleCommand(name: "CIButtons", docs: "Dev, main menu or editor: presses every button of the editor (each tab, each kind of selected object) and of every window they open, and reports errors, windows that don't close, and buttons never reached. 'CIButtons <minutes>': each screen's time limit (4; the first, with the top bar's windows, 3 times that)")]
		public static void ButtonsCommand(string[] args)
		{
			float minutes;
			if (args == null || args.Length == 0 || !float.TryParse(args[0].TrimEnd('m'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out minutes) || minutes <= 0f) minutes = 4f;
			StartTest(ButtonsRoutine(minutes * 60f));
		}

		static IEnumerator ButtonsRoutine(float screenLimit = 240f)
		{
			buttonRun = new ButtonRun();
			ButtonRun run = buttonRun;
			run.ScreenLimit = screenLimit;
			run.Active = true;
			Application.LogCallback watch = (msg, trace, type) =>
			{
				if ((type == LogType.Exception || type == LogType.Error) && !msg.StartsWith("[CITEST]"))
					run.Errors.Add(run.Current + ": " + msg.Split('\n')[0] + (type == LogType.Exception ? " @ " + (trace ?? "").Split('\n').FirstOrDefault() : ""));
			};
			Application.logMessageReceived += watch;
			// (the guide, Discord, report and folder buttons only note what they'd open: no browser, PDF reader or Explorer
			// window on the screen of someone using the PC while the tests run)
			HelpLinks.TestMode = true;
			try
			{
				// The main menu's EDITOR button (the mod's own) opens the editor
				if (!DynamicIslands.InEditor())
				{
					GameObject menu = GameObject.Find("MainMenuCanvas");
					Transform ed = menu != null ? menu.transform.Find("MenuButtons/EDITOR") : null;
					Button eb = ed != null ? ed.GetComponent<Button>() : null;
					if (eb == null) { Fail("no EDITOR button in the main menu"); yield break; }
					run.Current = "Main menu > EDITOR";
					eb.onClick.Invoke();
					run.Pressed++;
					Log("Pressed the main menu's EDITOR button");
					// (it opens the start window: its New island opens the editor - TODO 5b)
					yield return new WaitForSecondsRealtime(0.5f);
					Button ni = CreateWindow.IsOpen ? CreateWindow.Root.GetComponentsInChildren<Button>().FirstOrDefault(x => x.name == "Button_New island") : null;
					if (ni == null) { Fail("the EDITOR button did not open the start window with New island"); yield break; }
					run.Current = "Start window > New island";
					ni.onClick.Invoke();
					run.Pressed++;
					// (its own load: WaitForEditor would start a second one)
					float t0 = Time.realtimeSinceStartup;
					while (!(DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt && EditorUI.Canvas != null) && Time.realtimeSinceStartup - t0 < 120f) yield return new WaitForSecondsRealtime(0.5f);
					if (!DynamicIslands.InEditor()) { Fail("the EDITOR button did not open the editor in 120 s"); yield break; }
				}
				yield return WaitForEditor(false);
				if (!DynamicIslands.InEditor()) { Fail("the editor did not open"); yield break; }
				yield return new WaitForSecondsRealtime(1f);

				// A test island with one object of each kind that has its own inspector
				DynamicIslands.currentIslandName = "cibuttons";
				var kinds = new List<KeyValuePair<string, string>>
				{
					new KeyValuePair<string, string>("plain object", "Log"),
					new KeyValuePair<string, string>("creature", "Creature_Boar"),
					new KeyValuePair<string, string>("note", "Note_Paper"),
					new KeyValuePair<string, string>("chest", "Loot_Chest"),
					new KeyValuePair<string, string>("trigger zone", ContentCatalog.TriggerZone),
					new KeyValuePair<string, string>("atmosphere zone", ContentCatalog.AtmosphereZoneName),
					new KeyValuePair<string, string>("sound zone", ContentCatalog.SoundZoneName),
					new KeyValuePair<string, string>("invisible wall", ContentCatalog.HelperWall),
					new KeyValuePair<string, string>("raft block", "Block_Foundation"),
				};
				// (Open, New and Generate replace the island and its objects: each screen's setup places its object again when it's gone)
				var objs = new EditorGameObject[kinds.Count];
				Func<int, EditorGameObject> objectOf = n =>
				{
					if (objs[n] == null)
					{
						GameObject root = GameObject.Find("PlacedObjects");
						if (root != null) objs[n] = PlaceForTest(kinds[n].Value, terraineditor.terrain.transform.position + new Vector3(500f + n * 6f, DynamicIslands.EditorWaterLevel + 2f, 500f), root.transform);
					}
					return objs[n];
				};
				int missingKinds = 0;
				for (int n = 0; n < kinds.Count; n++)
					if (objectOf(n) == null) { missingKinds++; Log("  (no " + kinds[n].Value + " in the catalog)"); }
				yield return null;

				// The screens: each tab, and the Objects tab with each kind of selection
				yield return ButtonScreen(run, "Terrain tab", () => { DynamicIslands.EditorGizmoHandler.ClearTargets(false); EditorUI.SetTab(TAB.TerrainEdit); });
				yield return ButtonScreen(run, "Island tab", () => { DynamicIslands.EditorGizmoHandler.ClearTargets(false); EditorUI.SetTab(TAB.Island); });
				yield return ButtonScreen(run, "Objects tab, nothing selected", () => { EditorUI.SetTab(TAB.ObjectPlace); DynamicIslands.EditorGizmoHandler.ClearTargets(false); });
				for (int n = 0; n < kinds.Count; n++)
				{
					int k = n;
					if (objectOf(k) == null) continue;
					yield return ButtonScreen(run, "Objects tab, a " + kinds[k].Key + " selected", () =>
					{
						EditorUI.SetTab(TAB.ObjectPlace);
						EditorGameObject target = objectOf(k);
						if (target == null) throw new Exception("no " + kinds[k].Value + " to select");
						TransformGizmoSelect(target.transform);
					});
				}
				yield return ButtonScreen(run, "Objects tab, two objects selected", () =>
				{
					EditorUI.SetTab(TAB.ObjectPlace);
					EditorGameObject a = objectOf(0), b = objectOf(1);
					if (a == null || b == null) throw new Exception("no two objects to select");
					DynamicIslands.EditorGizmoHandler.ClearTargets(false);
					DynamicIslands.EditorGizmoHandler.AddTarget(a.transform, false);
					DynamicIslands.EditorGizmoHandler.AddTarget(b.transform, false);
				});
				yield return GeneratorScreens(run);
				if (missingKinds > 0) run.Errors.Add(missingKinds + " kind(s) of object not in the catalog: their screens were not tested");
			}
			finally { Application.logMessageReceived -= watch; HelpLinks.TestMode = false; run.Active = false; }

			// Coverage: every button the mod made that still exists (and isn't a sampled list entry or skipped)
			UIKit.AllButtons.RemoveAll(b => b == null);
			var listCache = new Dictionary<Transform, Dictionary<Transform, int>>();
			// (a window that rebuilds its rows makes new buttons in the same place: those count as pressed)
			List<Button> missed = UIKit.AllButtons.Where(b => !run.Done.Contains(b) && !run.Keys.Contains(KeyOf(b)) && !ButtonSkip.Contains(LabelOfButton(b)) && !InSampledList(b, listCache) && !IsWorldOnly(b)).ToList();
			foreach (var s in run.PerScreen) Log("  " + s.Key + ": " + s.Value + " button(s)");
			Log("Pressed " + run.Pressed + " buttons (" + run.Keys.Count + " different), " + run.Windows + " window(s) opened; lists sampled: " + run.Sampled.Distinct().Count());
			foreach (string e in run.Errors.Distinct().Take(30)) Log("  ERROR " + e);
			foreach (string u in run.Unclosed.Distinct()) Log("  NOT CLOSED " + u);
			foreach (var g in missed.GroupBy(b => OwnerOf(b) + " > " + LabelOfButton(b)).Take(40)) Log("  NEVER PRESSED " + g.Key + (g.Count() > 1 ? " (x" + g.Count() + ")" : ""));
			bool ok = run.Errors.Count == 0 && run.Unclosed.Count == 0;
			// (300 on 2026-09-25, with all 13 screens)
			Check(ref ok, run.Keys.Count >= 280, "at least 280 different buttons pressed (" + run.Keys.Count + ")");
			Log("Buttons never pressed: " + missed.Count + " (of " + UIKit.AllButtons.Count + " the mod made)");
			if (ok) Log("PASS: every button"); else Fail("every button (" + run.Errors.Count + " errors, " + run.Unclosed.Count + " windows not closed)");
		}

		static IEnumerator ButtonScreen(ButtonRun run, string screen, Action setup)
		{
			// (a press that left the editor would leave every later screen empty: said once, with the press that did it)
			if (!DynamicIslands.InEditor()) { run.Errors.Add(screen + ": not in the editor (left it after " + run.Current + ")"); yield break; }
			// (a line a screen: a runner timeout said nothing of where it stalled - RUN2 10-20, 45 min silent on a minimised Raft)
			Log("  buttons: screen '" + screen + "' (" + run.Pressed + " pressed so far, " + Mathf.RoundToInt(Time.realtimeSinceStartup - run.Start) + " s)");
			// (the first screen also presses the top bar and walks its ~140 windows: three times as long)
			ScreenStarts(run, screen, run.PerScreen.Count == 0 ? 3f : 1f);
			CloseAllWindows();
			// (from a small island again; the setup places the screen's own object again)
			if (ResetBigIsland(run)) yield return null;
			try { setup(); } catch (Exception e) { run.Errors.Add(screen + " (setting up): " + e.Message); }
			yield return null; yield return null;
			int before = run.Pressed, at = 0;
			// (a snapshot of this screen's buttons: pressing one can rebuild the panel, the loop re-checks each)
			List<Button> list = SampleList(run, MainButtons());
			foreach (Button b in list)
			{
				at++;
				if (run.ScreenOver) break;
				if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
				if (!DynamicIslands.InEditor()) { run.Errors.Add(screen + ": left the editor"); yield break; }
				yield return Press(run, b, screen, 0);
				CloseAllWindows();
				KeepWalkIsland();
				bool reset = ResetBigIsland(run);
				if (reset) yield return null;
				// Buttons this press showed (a tool's own options, e.g. the stamps): pressed now, before the setup hides them again
				List<Button> revealed = reset ? new List<Button>() : SampleList(run, MainButtons().Where(x => !list.Contains(x) && !run.Done.Contains(x)).ToList());
				foreach (Button r in revealed)
				{
					if (run.ScreenOver) break;
					if (r == null || !r.gameObject.activeInHierarchy || !r.interactable || run.Done.Contains(r)) continue;
					if (!DynamicIslands.InEditor()) { run.Errors.Add(screen + ": left the editor"); yield break; }
					yield return Press(run, r, screen + " > " + LabelOfButton(b), 0);
					CloseAllWindows();
					KeepWalkIsland();
					if (ResetBigIsland(run)) yield return null;
				}
				// (the screen may have changed: selection lost, tab switched - set it up again)
				try { setup(); } catch { }
				yield return null;
			}
			// (its time limit: this screen FAILs with the press it was on, the next screen starts)
			if (run.ScreenOver) { CloseAllWindows(); ScreenTimeLimit(run, screen, run.Pressed - before, ", " + Math.Max(0, at - 1) + " of its " + list.Count + " buttons reached"); }
			run.PerScreen[screen] = run.Pressed - before;
			Log("  buttons: screen '" + screen + "' done, " + (run.Pressed - before) + " pressed (" + Mathf.RoundToInt(Time.realtimeSinceStartup - run.Start) + " s)");
		}

		/// <summary>After an Open... that loaded someone's island, the walk is back on its own one, so a later Save never writes over it.</summary>
		// (10-16 Open... loaded "Wreckers' Cove_d969a15eef52", the Island tab's Generate replaced its land and Save wrote it)
		static void KeepWalkIsland()
		{
			if (!DynamicIslands.InEditor() || DynamicIslands.currentIslandName == "cibuttons") return;
			DynamicIslands.NewIsland();
			DynamicIslands.currentIslandName = "cibuttons";
		}

		static IEnumerator Press(ButtonRun run, Button b, string context, int depth)
		{
			string label = LabelOfButton(b);
			if (ButtonSkip.Contains(label)) yield break;
			string owner = OwnerOf(b);
			string key = KeyOf(b);
			if (run.ScreenOver) yield break;
			List<MonoBehaviour> before = OpenWindows();
			run.Current = context + " > " + label;
			if (ButtonMayBlock.Any(x => label.Contains(x))) Log("  buttons: pressing " + run.Where());
			float t0 = Time.realtimeSinceStartup;
			try { b.onClick.Invoke(); }
			catch (Exception e) { run.Errors.Add(run.Current + ": " + (e.InnerException ?? e).Message); }
			run.Pressed++;
			run.Done.Add(b);
			run.Keys.Add(key);
			yield return null; yield return null;
			float took = Time.realtimeSinceStartup - t0;
			if (took > 5f) Log("  buttons: slow (" + took.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " s) " + run.Where());
			else if (run.Pressed % 25 == 0) Log("  buttons: at " + run.Where());
			// (here too, not only between screens: inside the Open... window the next press after loading an island was a Save)
			KeepWalkIsland();
			if (depth >= 3) yield break;
			foreach (MonoBehaviour w in OpenWindows().Where(x => !before.Contains(x)).ToList())
			{
				run.Windows++;
				// (the top bar is the same on every screen: its windows are walked once, not grown on each screen - e.g. templates added to a plan)
				if (owner == "TopBar" &&!run.Walked.Add(key + " " + w.GetType().Name)) continue;
				yield return TestWindow(run, w, b, run.Current, depth + 1);
			}
			// Whatever this press opened is closed again
			foreach (MonoBehaviour w in OpenWindows().Where(x => !before.Contains(x)).ToList())
			{
				if (!CloseWindow(w)) run.Unclosed.Add(w.GetType().Name + " (opened by " + context + " > " + label + ")");
			}
		}

		/// <summary>A screen of the button walk starts: its time limit counts from now.</summary>
		static void ScreenStarts(ButtonRun run, string screen, float times = 1f)
		{
			run.Screen = screen; run.ScreenStart = Time.realtimeSinceStartup; run.Current = screen; run.ThisLimit = run.ScreenLimit * times;
		}

		/// <summary>When the screen's time limit was reached: it FAILs with the press it was on (the next screen starts).</summary>
		static void ScreenTimeLimit(ButtonRun run, string screen, int pressed, string reached)
		{
			if (!run.ScreenOver) return;
			run.Errors.Add("screen '" + screen + "': its time limit (" + (run.ThisLimit / 60f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " min) reached at '" + run.Current + "' - " + pressed
				+ " pressed" + reached + " (a frame " + Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f) + " ms, " + PlacedCount() + " objects)");
			Log("  buttons: screen '" + screen + "' TIME LIMIT at '" + run.Current + "'");
		}

		/// <summary>Presses every button of a window; when a press closes the window, it's opened again with the same button.</summary>
		static IEnumerator TestWindow(ButtonRun run, MonoBehaviour w, Button opener, string context, int depth)
		{
			Type type = w.GetType();
			var done = new HashSet<string>();
			string openerKey = opener != null ? KeyOf(opener) : null;
			for (int guard = 0; guard < 400; guard++)
			{
				if (run.ScreenOver) break;
				if (w == null || !w.gameObject.activeInHierarchy)
				{
					// (a press can rebuild the panel the opener is on - "Use" gives the zone its sound and the inspector
					// is made again: the new button in the same place opens it again)
					if ((opener == null || !opener.gameObject.activeInHierarchy) && openerKey != null)
						opener = UIKit.AllButtons.FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy && x.interactable && KeyOf(x) == openerKey);
					if (opener == null || !opener.gameObject.activeInHierarchy) break;
					run.Current = context + " (opened again)";
					try { opener.onClick.Invoke(); } catch { break; }
					yield return null; yield return null;
					w = OpenWindows().FirstOrDefault(x => x.GetType() == type);
					if (w == null) break;
				}
				MonoBehaviour win = w;
				// (a window's tab buttons would hide the rest of the tab being walked: each tab is a screen of its own, see GeneratorScreens)
				Button next = SampleList(run, win.GetComponentsInChildren<Button>(false).Where(x => x.interactable && WindowOf(x) == win && !IsTabButton(x)).ToList())
					.FirstOrDefault(x => !done.Contains(PathOf(x.transform, win.transform) + "|" + LabelOfButton(x)) && !ButtonSkip.Contains(LabelOfButton(x)));
				if (next == null) break;
				done.Add(PathOf(next.transform, win.transform) + "|" + LabelOfButton(next));
				yield return Press(run, next, context + " [" + type.Name + "]", depth);
			}
			if (w != null && w.gameObject.activeInHierarchy && !CloseWindow(w)) run.Unclosed.Add(type.Name + " (" + context + ")");
		}

		/// <summary>The buttons of the editor screen itself (not in a window).</summary>
		static List<Button> MainButtons()
		{
			if (EditorUI.Canvas == null) return new List<Button>();
			return EditorUI.Canvas.GetComponentsInChildren<Button>(false).Where(b => b.interactable && WindowOf(b) == null).ToList();
		}

		/// <summary>Long lists (tiles, items, sounds, cards) keep their first few buttons.</summary>
		static List<Button> SampleList(ButtonRun run, List<Button> list)
		{
			var result = new List<Button>();
			var cache = new Dictionary<Transform, Dictionary<Transform, int>>();
			foreach (Button b in list)
			{
				Transform owner; int index;
				if (!ListItemOf(b, cache, out owner, out index)) { result.Add(b); continue; }
				run.Sampled.Add(PathOf(owner, owner.root));
				if (index < ListSample) result.Add(b);
			}
			return result;
		}

		/// <summary>A long list: a button among more than ListThreshold sibling buttons (tiles), or a row among more than
		/// ListThreshold rows alike (same name, a button label they share - the ▲ ▼ × of rule cards, not a form's rows).</summary>
		static bool ListItemOf(Button b, Dictionary<Transform, Dictionary<Transform, int>> cache, out Transform owner, out int index)
		{
			Transform item = b.transform;
			for (int up = 0; up < 3 && item.parent != null; up++, item = item.parent)
			{
				owner = item.parent;
				Dictionary<Transform, int> items;
				if (!cache.TryGetValue(owner, out items)) cache[owner] = items = ListItems(owner);
				if (items.TryGetValue(item, out index)) return true;
			}
			owner = null; index = -1;
			return false;
		}

		/// <summary>The children of a transform that are entries of a long list, with their place in it (each child looked at once).</summary>
		static Dictionary<Transform, int> ListItems(Transform owner)
		{
			var result = new Dictionary<Transform, int>();
			var tiles = owner.Cast<Transform>().Where(c => c.GetComponent<Button>() != null).ToList();
			if (tiles.Count > ListThreshold) for (int i = 0; i < tiles.Count; i++) result[tiles[i]] = i;
			// (rows alike: one name, or one name before a "_<what>" tail - the kinds window's Kind_<object> rows were each pressed
			// on every screen: ~300 buttons a screen on an 11634-object generated island, the run over 2 h - 10-13)
			foreach (var group in owner.Cast<Transform>().Where(c => c.GetComponent<Button>() == null).GroupBy(c => c.name.Split('_')[0]))
			{
				var rows = group.Select(c => new KeyValuePair<Transform, HashSet<string>>(c, new HashSet<string>(c.GetComponentsInChildren<Button>(true).Select(LabelOfButton)))).Where(r => r.Value.Count > 0).ToList();
				if (rows.Count <= ListThreshold) continue;
				var shared = new HashSet<string>(rows.SelectMany(r => r.Value).GroupBy(l => l).Where(g => g.Count() > ListThreshold).Select(g => g.Key));
				int n = 0;
				foreach (var r in rows) if (r.Value.Overlaps(shared)) result[r.Key] = n++;
			}
			return result;
		}

		static bool InSampledList(Button b, Dictionary<Transform, Dictionary<Transform, int>> cache) { Transform owner; int index; return ListItemOf(b, cache, out owner, out index); }

		/// <summary>The journal and the note reader belong to the world (CIWorldButtons).</summary>
		static bool IsWorldOnly(Button b) { MonoBehaviour w = WindowOf(b); return w is JournalWindow || w is NoteReader; }

		static List<MonoBehaviour> OpenWindows()
		{
			return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(m => m != null && m.gameObject.activeInHierarchy && IsWindowType(m.GetType())).ToList();
		}

		static bool IsWindowType(Type t) { return t.Name.EndsWith("Window") || t == typeof(NoteReader); }

		static MonoBehaviour WindowOf(Button b)
		{
			for (Transform t = b.transform; t != null; t = t.parent)
				foreach (MonoBehaviour m in t.GetComponents<MonoBehaviour>())
					if (m != null && IsWindowType(m.GetType())) return m;
			return null;
		}

		static string OwnerOf(Button b)
		{
			MonoBehaviour w = WindowOf(b);
			if (w != null) return w.GetType().Name;
			for (Transform t = b.transform; t != null; t = t.parent)
				if (t.name == "ToolPanel" || t.name == "ObjectBrowser" || t.name == "TopBar" || t.name == "StatusBar") return t.name;
			return b.transform.root.name;
		}

		/// <summary>A button by where it is and what it says (a rebuilt panel's new button has the same key).</summary>
		static string KeyOf(Button b) { return OwnerOf(b) + " > " + PathOf(b.transform, b.transform.root) + " '" + LabelOfButton(b) + "'"; }

		static string LabelOfButton(Button b)
		{
			if (b == null) return "";
			Text t = UIKit.LabelOf(b) ?? b.GetComponentInChildren<Text>(true);
			string s = t != null ? t.text.Replace("\n", " ").Trim() : "";
			return s.Length > 0 ? (s.Length > 30 ? s.Substring(0, 30) : s) : b.name;
		}

		static bool CloseWindow(MonoBehaviour w)
		{
			if (w == null || !w.gameObject.activeInHierarchy) return true;
			try
			{
				var close = w.GetType().GetMethod("Close", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
				if (close != null) close.Invoke(null, null);
			}
			catch (Exception e) { Debug.LogWarning("[CITEST] closing " + w.GetType().Name + ": " + (e.InnerException ?? e).Message); }
			return w == null || !w.gameObject.activeInHierarchy;
		}

		static void CloseAllWindows()
		{
			DropList.Close(); // (a drop-down list a pressed button opened)
			foreach (MonoBehaviour w in OpenWindows()) CloseWindow(w);
			// (the windows made by static classes - the library, export/import, info boxes, World settings, the island
			// picker - aren't MonoBehaviours: a library window left open by the button test covered the editor, and every
			// mouse test after it failed)
			foreach (Type t in StaticWindows)
				try
				{
					var open = t.GetProperty("IsOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
					var close = t.GetMethod("Close", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
					if (open != null && close != null && (bool)open.GetValue(null, null)) close.Invoke(null, null);
				}
				catch (Exception e) { Debug.LogWarning("[CITEST] closing " + t.Name + ": " + (e.InnerException ?? e).Message); }
		}

		static readonly Type[] StaticWindows = { typeof(LibraryWindow), typeof(LibraryExportWindow), typeof(LibraryImportWindow), typeof(IslandPickerWindow), typeof(WorldSettingsWindow), typeof(PlanCheckWindow) };

		/// <summary>The static windows open now (names).</summary>
		static IEnumerable<string> OpenStaticWindows()
		{
			foreach (Type t in StaticWindows)
			{
				var open = t.GetProperty("IsOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				bool isOpen = false;
				try { isOpen = open != null && (bool)open.GetValue(null, null); } catch { }
				if (isOpen) yield return t.Name;
			}
		}

		/// <summary>The in-world windows: the journal (J) and the note reader, every button.</summary>
		[ConsoleCommand(name: "CIWorldButtons", docs: "Dev, in game: presses every button of the journal and the note reader")]
		public static void WorldButtonsCommand()
		{
			StartTest(WorldButtonsRoutine());
		}

		static IEnumerator WorldButtonsRoutine()
		{
			var run = new ButtonRun();
			Application.LogCallback watch = (msg, trace, type) => { if ((type == LogType.Exception || type == LogType.Error) && !msg.StartsWith("[CITEST]")) run.Errors.Add(run.Current + ": " + msg.Split('\n')[0]); };
			Application.logMessageReceived += watch;
			bool ok = true;
			try
			{
				StoryBook.Give("ci-button-test", 1);
				StoryBook.AddPage("ci:buttons", "A test page", "Written by the button test.", "");
				JournalWindow.Open();
				yield return null; yield return null;
				MonoBehaviour j = OpenWindows().FirstOrDefault(w => w is JournalWindow);
				Check(ref ok, j != null, "the journal opens");
				if (j != null) { run.Windows++; yield return TestWindow(run, j, null, "Journal", 1); }
				Check(ref ok, !JournalWindow.IsOpen, "the journal closes");
				var go = new GameObject("CI_TestNote");
				// (beside the player: the reader closes itself when the player is more than 8 m from the note - at the world's
				// origin it was hundreds of metres off, and closed before the check when a frame was slow)
				Network_Player me = RAPI.GetLocalPlayer();
				if (me != null) go.transform.position = me.transform.position + Vector3.forward;
				CustomNote n = go.AddComponent<CustomNote>();
				n.Title = "Button test"; n.Text = "A note for the button test.";
				NoteReader.Open(n);
				yield return null; yield return null;
				MonoBehaviour r = OpenWindows().FirstOrDefault(w => w is NoteReader);
				Check(ref ok, r != null, "the note reader opens");
				if (r != null) { run.Windows++; yield return TestWindow(run, r, null, "Note reader", 1); }
				Check(ref ok, !NoteReader.IsOpen, "the note reader closes");
				UnityEngine.Object.Destroy(go);
			}
			finally { Application.logMessageReceived -= watch; }
			foreach (string e in run.Errors.Distinct()) Log("  ERROR " + e);
			ok &= run.Errors.Count == 0 && run.Unclosed.Count == 0;
			Log("Pressed " + run.Pressed + " buttons in the journal and the note reader");
			if (ok) Log("PASS: in-world windows"); else Fail("in-world windows");
		}

		#endregion

		#region Editor state (keyboard test)

		/// <summary>One line with what the keyboard shortcuts change, for tools\alltests.ps1's keyboard phase.</summary>
		[ConsoleCommand(name: "CIEditorState", docs: "Dev, editor: logs the tab, brush, gizmo, placement options, selection, undo steps, open windows, stamp turn and the placer's turn and size")]
		public static void EditorStateCommand()
		{
			if (!DynamicIslands.InEditor()) { Fail("not in the editor"); return; }
			var g = DynamicIslands.EditorGizmoHandler;
			ObjectPlacer placer = ObjectPlacer.Current;
			Log("Editor state: tab " + EditorUI.CurrentTab + ", brush " + terraineditor.modificationAction + ", gizmo " + (g != null ? g.transformType.ToString() : "?") +
				", random " + PlacementOptions.RandomTurnAndSize + ", slope " + PlacementOptions.AlignToSlope + ", grid " + PlacementOptions.SnapToGrid +
				", selected " + (g != null ? g.SelectedRoots.Count : 0) + ", objects " + PlacedEditorObjects().Count +
				", undo " + CommandUndoRedo.UndoRedoManager.UndoCount + ", windows [" + string.Join(",", OpenWindows().Select(w => w.GetType().Name).Concat(OpenStaticWindows()).Distinct().ToArray()) + "]" +
				", stamp turn " + TerrainStamps.Rotation.ToString("F0") + ", placer " + (placer != null ? placer.name + " yaw " + placer.Yaw.ToString("F0") + " scale " + placer.ScaleFactor.ToString("F2") : "none") +
				", island '" + DynamicIslands.currentIslandName + "'" +
				(Camera.main != null ? string.Format(System.Globalization.CultureInfo.InvariantCulture, ", camera {0:F1} {1:F1} {2:F1} yaw {3:F1} pitch {4:F1}", Camera.main.transform.position.x, Camera.main.transform.position.y, Camera.main.transform.position.z, Camera.main.transform.eulerAngles.y, Camera.main.transform.eulerAngles.x) : ""));
		}

		/// <summary>The placed objects that are shown (deleting hides them until undone; hidden ones aren't saved).</summary>
		static List<EditorGameObject> PlacedEditorObjects()
		{
			GameObject root = GameObject.Find("PlacedObjects");
			return root != null ? root.GetComponentsInChildren<EditorGameObject>(false).ToList() : new List<EditorGameObject>();
		}

		[ConsoleCommand(name: "CIStartPlacing", docs: "Dev, editor (Objects tab): starts placing an object as clicking its tile does: CIStartPlacing <object name>")]
		public static void StartPlacingCommand(string[] args)
		{
			string name = args != null && args.Length > 0 ? args[0] : "Log";
			EditorUI.SetTab(TAB.ObjectPlace);
			ObjectBrowser.StartPlacing(name);
			Log("Placing '" + name + "'");
		}

		[ConsoleCommand(name: "CIStamp", docs: "Dev, editor: picks a terrain stamp as its button does: CIStamp <index>")]
		public static void StampCommand(string[] args)
		{
			int i;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out i)) i = 0;
			EditorUI.SetTab(TAB.TerrainEdit);
			EditorUI.SetStamp(i);
			Log("Stamp " + i + " picked (" + (TerrainStamps.Current != null ? TerrainStamps.Current.Name : "none") + ")");
		}

		[ConsoleCommand(name: "CISelect", docs: "Dev, editor (Objects tab): selects the first placed object (or the one named) as a click does")]
		public static void SelectCommand(string[] args)
		{
			// (a loaded island's objects sit in PlacedObjects/LoadedObjects: the placed objects, not the holders)
			List<EditorGameObject> objects = PlacedEditorObjects();
			if (objects.Count == 0) { Fail("nothing placed to select"); return; }
			string name = args != null && args.Length > 0 ? args[0] : null;
			EditorGameObject eo = objects.FirstOrDefault(c => name == null || c.GameObjectName.StartsWith(name) || c.name.StartsWith(name));
			if (eo == null) { Fail("no placed '" + name + "'"); return; }
			Transform t = eo.transform;
			EditorUI.SetTab(TAB.ObjectPlace);
			TransformGizmoSelect(t);
			Log("Selected " + t.name);
		}

		[ConsoleCommand(name: "CIJournalState", docs: "Dev, in game: whether the journal and the note reader are open (the keyboard test presses J and Esc)")]
		public static void JournalStateCommand()
		{
			Log("Journal " + (JournalWindow.IsOpen ? "open" : "closed") + ", note reader " + (NoteReader.IsOpen ? "open" : "closed"));
		}

		[ConsoleCommand(name: "CIJournalShot", docs: "Dev, in game: the journal opened on a page (the newest, or the one whose title contains <text>) and its picture taken: shot_journal_world.png (the guide's). CIJournalShot [text]")]
		public static void JournalShotCommand(string[] args)
		{
			string part = args != null && args.Length > 0 ? string.Join(" ", args) : null;
			StartTest(JournalShotRoutine(part));
		}

		static IEnumerator JournalShotRoutine(string part)
		{
			JournalWindow.Open();
			yield return new WaitForSeconds(0.5f);
			StoryBook.Page p = StoryBook.Pages.LastOrDefault(x => part == null || x.Title.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
			if (p != null) JournalWindow.ShowKey(p.Key);
			yield return new WaitForSeconds(0.8f);
			Log("  the list: " + string.Join(" | ", JournalWindow.ListLines().ToArray()));
			Screenshot(new[] { "journal_world" });
			yield return new WaitForSeconds(0.6f);
			JournalWindow.Close();
			Log("PASS: journal picture (" + StoryBook.Pages.Count + " pages)");
		}

		[ConsoleCommand(name: "CIJournalTour", docs: "Dev, in game: the journal opened and its notes list scrolled island by island - each island's line at the top, its first note on the paper - a picture of each: shot_journal_<nn>_<island>.png at the game's resolution (the user, 2026-10-03: pictures of the journal, its notes scrolled for each island). CIJournalTour [most islands, 30]")]
		public static void JournalTourCommand(string[] args)
		{
			int most = args != null && args.Length > 0 ? Mathf.Max(1, (int)F(args[0])) : 30;
			StartTest(JournalTourRoutine(most));
		}

		static IEnumerator JournalTourRoutine(int most)
		{
			JournalWindow.Open();
			yield return new WaitForSeconds(0.8f);
			// (the journal's own lists, read as the window has them: the pages list and the page each of its buttons shows)
			const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
			object window = typeof(JournalWindow).GetField("instance", Any).GetValue(null);
			var pageList = window != null ? typeof(JournalWindow).GetField("pageList", Any).GetValue(window) as RectTransform : null;
			var pageKeys = window != null ? typeof(JournalWindow).GetField("pageKeys", Any).GetValue(window) as Dictionary<Button, string> : null;
			ScrollRect scroll = pageList != null ? pageList.GetComponentInParent<ScrollRect>() : null;
			if (pageList == null || pageKeys == null || scroll == null) { Fail("journal tour: the journal's lists not found"); JournalWindow.Close(); yield break; }
			var children = pageList.Cast<Transform>().Where(c => c.gameObject.activeSelf).ToList();
			int shots = 0;
			for (int i = 0; i < children.Count && shots < most; i++)
			{
				if (children[i].GetComponent<Button>() != null) continue;
				Text line = children[i].GetComponent<Text>() ?? children[i].GetComponentInChildren<Text>();
				if (line == null || string.IsNullOrEmpty(line.text) || children[i].gameObject.name == "Label" && line.text.StartsWith("<i>")) continue;
				// (the island's line at the top of the list, its first note on the paper)
				Canvas.ForceUpdateCanvases();
				float y = -((RectTransform)children[i]).anchoredPosition.y - ((RectTransform)children[i]).rect.height * (1f - ((RectTransform)children[i]).pivot.y);
				float room = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
				scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, Mathf.Clamp(y - 2f, 0f, room));
				Button first = children.Skip(i + 1).TakeWhile(c => c.GetComponent<Button>() != null).Select(c => c.GetComponent<Button>()).FirstOrDefault();
				string key;
				if (first != null && pageKeys.TryGetValue(first, out key)) JournalWindow.ShowKey(key);
				yield return new WaitForSeconds(0.8f);
				string island = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(line.text, "<[^>]+>", ""), "[^A-Za-z0-9]+", "_").Trim('_');
				if (island.Length > 40) island = island.Substring(0, 40);
				shots++;
				Screenshot(new[] { "journal_" + shots.ToString("00") + "_" + island });
				Log("  " + shots + ": " + System.Text.RegularExpressions.Regex.Replace(line.text, "<[^>]+>", ""));
				yield return new WaitForSeconds(0.8f);
			}
			JournalWindow.Close();
			Log("PASS: journal tour (" + shots + " islands, " + StoryBook.Pages.Count + " pages)");
		}

		#endregion

		#region Round trip of every saved island

		/// <summary>Loads each saved island in the editor, saves it under a test name and compares the two files (terrain, paint, objects with settings, island settings).</summary>
		[ConsoleCommand(name: "CIRoundTrip", docs: "Dev, editor: loads every saved island (or the one named, or @<file> of names), saves it again under a test name and compares: nothing may change. Of the machine-written ones (gen-*, rnd-*, downloaded copies) a fixed sample of 20; 'all' takes every one. '<n>m' = its time limit in minutes (13, under alltests' 15); it stops when another test starts")]
		public static void RoundTripCommand(string[] args)
		{
			List<string> a = args != null ? args.ToList() : new List<string>();
			float minutes = 13f, m;
			if (a.Count > 0 && a[a.Count - 1].EndsWith("m") && float.TryParse(a[a.Count - 1].TrimEnd('m'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m)) { minutes = m; a.RemoveAt(a.Count - 1); }
			bool all = a.Count > 0 && a[0] == "all";
			if (all) a.RemoveAt(0);
			StartExclusiveTest("island round trip", RoundTripRoutine(a.Count > 0 ? string.Join(" ", a.ToArray()) : null, all, minutes * 60f));
		}

		// (machine-written islands - generated, the randomizer's extras, downloaded copies - are world spill: 270+ of them by
		// 10-20 made the run 1600+ islands long, and it went on saving 44 min after the runner gave up; 20 of them cover the kind)
		const int RoundTripMachineSample = 20;
		static bool MachineWritten(string n)
		{
			return n.StartsWith(CustomIslandSpawner.GeneratedPrefix, StringComparison.OrdinalIgnoreCase) || n.StartsWith(WorldRandomizer.ExtrasPrefix, StringComparison.OrdinalIgnoreCase) || IslandNetwork.IsDownloadName(n);
		}

		[ConsoleCommand(name: "CIComponents", docs: "Dev: the components on a catalog object's prototype and on up to 3 of its copies in the scene (name) - a Rigidbody left on one lets it settle after loading")]
		public static void ComponentsCommand(string[] args)
		{
			string name = args != null && args.Length > 0 ? string.Join(" ", args) : "";
			Func<GameObject, string> list = g => string.Join(", ", g.GetComponentsInChildren<Component>(true).Where(c => c != null && !(c is Transform)).GroupBy(c => c is Rigidbody ? "Rigidbody(" + (((Rigidbody)c).isKinematic ? "kinematic" : "dynamic") + (((Rigidbody)c).useGravity ? ", gravity" : "") + ")" : c.GetType().Name).Select(gr => gr.Key + (gr.Count() > 1 ? " x" + gr.Count() : "")).ToArray());
			GameObject proto = PlaceableCatalog.Get(name);
			Log("Components of " + name + ": prototype " + (proto != null ? list(proto) : "none (not loaded)") + "; timeScale " + Time.timeScale + ", physics " + (Physics.autoSimulation ? "on" : "off"));
			foreach (Transform t in UnityEngine.Object.FindObjectsOfType<Transform>().Where(t => t.name == name && (proto == null || t.gameObject != proto)).Take(3))
				Log("  copy at " + t.position + " rot " + t.eulerAngles + ": " + list(t.gameObject));
		}

		const string RoundTripName = "ci_roundtrip";

		static IEnumerator RoundTripRoutine(string only, bool all = false, float limit = 780f)
		{
			yield return WaitForEditor(false);
			// ("@<file>": the islands named in that file, one a line - a rerun of the ones that changed)
			HashSet<string> listed = only != null && only.StartsWith("@") && File.Exists(only.Substring(1)) ? new HashSet<string>(File.ReadAllLines(only.Substring(1)).Select(l => l.Trim()).Where(l => l.Length > 0)) : null;
			List<string> names = IslandSpawner.ListSavedIslands().Where(n => n != RoundTripName && (only == null || (listed != null ? listed.Contains(n) : n == only))).ToList();
			int machineAll = 0;
			if (only == null && !all)
			{
				List<string> machine = names.Where(MachineWritten).OrderBy(n => n, StringComparer.Ordinal).ToList();
				machineAll = machine.Count;
				// (the same spread-out 20 each run, from the sorted names)
				List<string> sample = machine.Count <= RoundTripMachineSample ? machine
					: Enumerable.Range(0, RoundTripMachineSample).Select(i => machine[(int)((i + 0.5f) * machine.Count / RoundTripMachineSample)]).Distinct().ToList();
				names = names.Where(n => !MachineWritten(n)).Concat(sample).ToList();
			}
			int passed = 0, done = 0;
			var failed = new List<string>();
			float start = Time.realtimeSinceStartup;
			try
			{
			foreach (string name in names)
			{
				if (Time.realtimeSinceStartup - start > limit) break;
				done++;
				IslandFile a;
				try { a = IslandFile.Load(IslandSpawner.PathFor(name)); }
				catch (Exception e) { failed.Add(name + ": can't read (" + e.Message + ")"); continue; }
				if (a == null) { failed.Add(name + ": can't read"); continue; }
				bool loaded = false;
				// (an island with pieces of Raft's own islands loads their scenes first and only then itself: wait for it)
				DynamicIslands.currentIslandName = "";
				try { loaded = DynamicIslands.LoadIsland(name); } catch (Exception e) { failed.Add(name + ": loading failed: " + e.Message); continue; }
				if (!loaded) { failed.Add(name + ": the editor didn't load it"); continue; }
				for (float t = 0; t < 120f && DynamicIslands.currentIslandName != name; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
				if (DynamicIslands.currentIslandName != name) { failed.Add(name + ": still not loaded after 2 minutes"); continue; }
				yield return null; yield return null;
				// (the copy written a moment ago can still be held by a virus scan: "in use by another program" - try again, or the
				// last island's copy would be compared)
				bool saved = DynamicIslands.SaveIsland(RoundTripName);
				for (int tries = 0; !saved && tries < 8; tries++) { yield return new WaitForSecondsRealtime(0.5f); saved = DynamicIslands.SaveIsland(RoundTripName); }
				if (!saved) { failed.Add(name + ": the copy couldn't be saved"); continue; }
				yield return null;
				IslandFile b = IslandFile.Load(IslandSpawner.PathFor(RoundTripName));
				string diff = CompareIslands(a, b);
				if (diff == null) passed++; else failed.Add(name + ": " + diff);
			}
			}
			finally
			{
				// (also when it is stopped: another test started)
				try { if (File.Exists(IslandSpawner.PathFor(RoundTripName))) File.Delete(IslandSpawner.PathFor(RoundTripName)); } catch { }
			}
			foreach (string f in failed) Log("  ROUND TRIP " + f);
			Log("Round trip: " + passed + " of " + names.Count + " islands unchanged" + (machineAll > 0 ? " (machine-written: a sample of " + Math.Min(machineAll, RoundTripMachineSample) + " of " + machineAll + ")" : ""));
			if (done < names.Count) { Fail("island round trip: time limit " + Mathf.RoundToInt(limit / 60f) + " min - " + done + " of " + names.Count + " checked, " + failed.Count + " changed (run it alone with a longer limit: CIRoundTrip <n>m)"); yield break; }
			if (failed.Count == 0 && names.Count > 0) Log("PASS: every saved island survives loading and saving"); else Fail("island round trip (" + failed.Count + " changed)");
		}

		/// <summary>What differs between two island files (null: the same, within float precision).</summary>
		static string CompareIslands(IslandFile a, IslandFile b)
		{
			if (b == null) return "the saved copy can't be read";
			if (a.HeightmapResolution != b.HeightmapResolution || (a.TerrainSize - b.TerrainSize).magnitude > 0.01f) return "terrain size " + a.TerrainSize + "/" + a.HeightmapResolution + " -> " + b.TerrainSize + "/" + b.HeightmapResolution;
			float maxH = 0f;
			for (int z = 0; z < a.HeightmapResolution; z++)
				for (int x = 0; x < a.HeightmapResolution; x++)
					maxH = Mathf.Max(maxH, Mathf.Abs(a.Heights[z, x] - b.Heights[z, x]));
			if (maxH * a.TerrainSize.y > 0.05f) return "heights differ by up to " + (maxH * a.TerrainSize.y).ToString("F2") + " m";
			if (a.HasPaint)
			{
				if (!b.HasPaint || a.Alphamaps.Length != b.Alphamaps.Length) return "ground paint lost or resized";
				// (loading normalises each pixel's layers to sum to 1: an older file whose weights didn't is compared normalised,
				// and the new copy's weights must sum to 255, give or take the rounding of each layer)
				int res = a.AlphamapResolution, layers = a.AlphamapLayers, maxP = 0, notNormal = 0, badSaved = 0;
				for (int p = 0; p < res * res; p++)
				{
					int sumA = 0, sumB = 0;
					for (int l = 0; l < layers; l++) { sumA += a.Alphamaps[l * res * res + p]; sumB += b.Alphamaps[l * res * res + p]; }
					if (Math.Abs(sumA - 255) > layers) notNormal++;
					if (Math.Abs(sumB - 255) > layers) badSaved++;
					for (int l = 0; l < layers; l++)
					{
						int va = a.Alphamaps[l * res * res + p];
						int expect = sumA > 0 ? Mathf.RoundToInt(va * 255f / sumA) : (l == 0 ? 255 : 0);
						maxP = Math.Max(maxP, Math.Abs(expect - b.Alphamaps[l * res * res + p]));
					}
				}
				if (badSaved > 0) return "saved ground paint not normalised at " + badSaved + " pixel(s)";
				if (maxP > 2) return "ground paint differs (up to " + maxP + "/255)";
				if (notNormal > 0) Log("  (" + notNormal + " pixel(s) of the file's paint didn't sum to 255: normalised on loading, as expected)");
			}
			if (Mathf.Abs(a.Elevation - b.Elevation) > 0.01f) return "height in the world " + a.Elevation + " -> " + b.Elevation;
			if ((a.Style ?? "") != (b.Style ?? "")) return "style '" + a.Style + "' -> '" + b.Style + "'";
			string pd = CompareProps(a.Props, b.Props);
			if (pd != null) return "island settings: " + pd;
			if (a.Objects.Count != b.Objects.Count)
			{
				// (which kinds: the names whose count changed)
				Dictionary<string, int> ca = a.Objects.GroupBy(o => o.Name ?? "").ToDictionary(g => g.Key, g => g.Count()), cb = b.Objects.GroupBy(o => o.Name ?? "").ToDictionary(g => g.Key, g => g.Count());
				Func<Dictionary<string, int>, string, int> n = (c, k) => c.ContainsKey(k) ? c[k] : 0;
				return "objects " + a.Objects.Count + " -> " + b.Objects.Count + " (" + string.Join(", ", ca.Keys.Union(cb.Keys).Where(k => n(ca, k) != n(cb, k)).Take(5).Select(k => k + " " + n(ca, k) + "->" + n(cb, k)).ToArray()) + ")";
			}
			// (the editor may save objects in another order: match each to the nearest of the same name)
			var left = new List<IslandObject>(b.Objects);
			foreach (IslandObject o in a.Objects)
			{
				IslandObject m = left.Where(x => x.Name == o.Name).OrderBy(x => (x.Position - o.Position).sqrMagnitude).FirstOrDefault();
				if (m == null) return "object " + o.Name + " missing";
				if ((m.Position - o.Position).magnitude > 0.02f) return "object " + o.Name + " moved by " + (m.Position - o.Position).magnitude.ToString("F3") + " m";
				if (Quaternion.Angle(Quaternion.Euler(m.EulerRotation), Quaternion.Euler(o.EulerRotation)) > 0.5f) return "object " + o.Name + " turned " + Quaternion.Angle(Quaternion.Euler(m.EulerRotation), Quaternion.Euler(o.EulerRotation)).ToString("F1") + " deg (" + o.EulerRotation + " -> " + m.EulerRotation + ")";
				// (a scene piece saved at scale one is placed at its prototype's own size - Balboa's cave x3 - and the editor then saves that size: the same)
				bool protoSize = o.Scale == Vector3.one && o.Props != null && o.Props.ContainsKey("set.piece");
				if (!protoSize && (m.Scale - o.Scale).magnitude > 0.01f) return "object " + o.Name + " resized " + o.Scale + " -> " + m.Scale;
				string od = CompareProps(o.Props, m.Props);
				if (od != null) return "object " + o.Name + " settings: " + od;
				left.Remove(m);
			}
			return null;
		}

		static string CompareProps(Dictionary<string, string> a, Dictionary<string, string> b)
		{
			a = a ?? new Dictionary<string, string>(); b = b ?? new Dictionary<string, string>();
			foreach (var kv in a) { string v; if (!b.TryGetValue(kv.Key, out v)) return "'" + kv.Key + "' lost"; if (v != kv.Value) return "'" + kv.Key + "' " + Short(kv.Value) + " -> " + Short(v); }
			foreach (var kv in b) if (!a.ContainsKey(kv.Key)) return "'" + kv.Key + "' added";
			return null;
		}

		#endregion

		#region World snapshots (saving, loading, leaving, joining, restarting)

		static string SnapshotPath(string name) { return Path.Combine(Path.Combine(DynamicIslands.assetpath, "snapshots"), name + (Raft_Network.IsHost ? "_host" : "_client") + ".txt"); }

		/// <summary>
		/// The world as this player has it, as lines "key = value": positions relative to the named reference island
		/// (the raft drifts and Raft shifts the world), the raft's too, the player's position and items, every island
		/// (label, height, used objects), quests, the story book, and the world plan's state (host).
		/// </summary>
		static List<KeyValuePair<string, string>> Snapshot(string reference)
		{
			var lines = new List<KeyValuePair<string, string>>();
			Action<string, string> add = (k, v) => lines.Add(new KeyValuePair<string, string>(k, v));
			IslandWorldState.Entry refIsland = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == reference);
			Vector3 origin = refIsland != null ? refIsland.Position : Vector3.zero;
			Func<Vector3, string> rel = p => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2:F1}", p.x - origin.x, p.y - origin.y, p.z - origin.z);
			add("reference", refIsland != null ? reference : "(none)");
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.OrderBy(x => x.HostName).ThenBy(x => x.Position.x))
			{
				if (e.Root != null) IslandObjectState.Capture(e);
				string id = "island " + e.HostName + "@" + rel(e.Position);
				add(id + " label", e.Label);
				// (a client leaves out the host's own bookkeeping - creature spots 1xxxx, the director's 5xxxx, once-events' 8xxxx: it gets
				// them only when joining, so they would show up as changes after joining again)
				add(id + " state", string.Join(" ", e.State.Where(kv => Raft_Network.IsHost || !((kv.Key >= 0x10000 && kv.Key < 0x20000) || (kv.Key >= 0x50000 && kv.Key < 0x60000) || (kv.Key >= Behaviours.SharedOnceBase && kv.Key < Behaviours.SharedOnceBase + 0x10000))).OrderBy(kv => kv.Key).Select(kv => kv.Key.ToString("X") + "=" + (kv.Value.Active ? 1 : 0) + "/" + kv.Value.Yield).ToArray()));
				IslandQuest q = QuestTracker.QuestOf(e);
				if (q.Steps.Count > 0) add(id + " quest", QuestTracker.StepOf(e) + "/" + q.Steps.Count);
			}
			add("story items", string.Join(", ", StoryBook.Items.Select(h => h.Def.Id + " x" + h.Count).ToArray()));
			add("story pages", string.Join(", ", StoryBook.Pages.Select(p => p.Key).OrderBy(k => k).ToArray()));
			if (Raft_Network.IsHost) add("world plan", WorldDirector.PlanName + "; done " + string.Join(",", WorldDirector.Done.OrderBy(d => d).ToArray()));
			Network_Player player = RAPI.GetLocalPlayer();
			if (player != null)
			{
				add("player position", rel(player.transform.position));
				PlayerInventory inv = player.Inventory;
				if (inv != null) add("player items", string.Join(", ", new[] { "Plank", "Stone", "Rope", "Nail", "Scrap", "Plastic", "Palm Leaf" }.Select(i => i + " x" + inv.GetItemCount(i)).ToArray()));
			}
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (raft.HasValue) add("raft position", rel(raft.Value));
			return lines;
		}

		[ConsoleCommand(name: "CISnapshot", docs: "Dev, in game (either player): writes a snapshot of the world as this player has it (positions relative to an island): CISnapshot <name> <reference island>")]
		public static void SnapshotCommand(string[] args)
		{
			if (args == null || args.Length < 2) { Fail("usage: CISnapshot <name> <reference island>"); return; }
			List<KeyValuePair<string, string>> lines = Snapshot(args[1]);
			string path = SnapshotPath(args[0]);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllLines(path, lines.Select(kv => kv.Key + " = " + kv.Value).ToArray());
			Log("Snapshot '" + args[0] + "': " + lines.Count + " lines -> " + path);
			foreach (var kv in lines) Log("  " + kv.Key + " = " + kv.Value);
		}

		/// <summary>Compares the world now with a snapshot. Positions: islands 2 m, the player 3 m; the raft only reported (it drifts).</summary>
		[ConsoleCommand(name: "CISnapCheck", docs: "Dev, in game (either player): compares the world now with a snapshot: CISnapCheck <name> <reference island> [what to leave out, e.g. raft]")]
		public static void SnapCheckCommand(string[] args)
		{
			if (args == null || args.Length < 2) { Fail("usage: CISnapCheck <name> <reference island>"); return; }
			string path = SnapshotPath(args[0]);
			if (!File.Exists(path)) { Fail("no snapshot " + path); return; }
			var then = File.ReadAllLines(path).Select(l => { int i = l.IndexOf(" = "); return new KeyValuePair<string, string>(l.Substring(0, i), l.Substring(i + 3)); }).ToList();
			List<KeyValuePair<string, string>> now = Snapshot(args[1]);
			var ignore = new HashSet<string>(args.Skip(2));
			int diffs = 0;
			Func<string, string> baseKey = k => k.StartsWith("island ") ? System.Text.RegularExpressions.Regex.Replace(k, "@[^ ]+", "") : k;
			var nowLeft = new List<KeyValuePair<string, string>>(now);
			foreach (var t in then)
			{
				if (ignore.Any(x => t.Key.Contains(x))) continue;
				// Islands: the same island (name) nearest to where it was
				KeyValuePair<string, string> m = nowLeft.Where(n => baseKey(n.Key) == baseKey(t.Key)).OrderBy(n => IslandOffsetDiff(t.Key, n.Key)).FirstOrDefault();
				if (m.Key == null) { Log("  DIFF " + t.Key + ": gone (was " + Short(t.Value) + ")"); diffs++; continue; }
				nowLeft.Remove(m);
				if (t.Key.StartsWith("island ") && IslandOffsetDiff(t.Key, m.Key) > 2f) { Log("  DIFF " + baseKey(t.Key) + " moved: " + t.Key + " -> " + m.Key); diffs++; }
				if (t.Key == "player position" || t.Key == "raft position")
				{
					float d = (ParseVec(t.Value) - ParseVec(m.Value)).magnitude;
					bool bad = t.Key == "player position" && d > 3f;
					Log("  " + (bad ? "DIFF " : "") + t.Key + ": " + t.Value + " -> " + m.Value + " (" + d.ToString("F1") + " m)");
					if (bad) diffs++;
					continue;
				}
				if (t.Value != m.Value) { Log("  DIFF " + baseKey(t.Key) + ": " + t.Value + " -> " + m.Value); diffs++; }
			}
			foreach (var n in nowLeft) if (!ignore.Any(x => n.Key.Contains(x))) { Log("  DIFF new: " + n.Key + " = " + Short(n.Value)); diffs++; }
			if (diffs == 0) Log("PASS: the world matches snapshot '" + args[0] + "' (" + then.Count + " lines)");
			else Fail("the world differs from snapshot '" + args[0] + "' in " + diffs + " place(s)");
		}

		static Vector3 ParseVec(string s)
		{
			string[] p = (s ?? "").Split(',');
			float x = 0, y = 0, z = 0;
			if (p.Length == 3)
			{
				float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
				float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
				float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z);
			}
			return new Vector3(x, y, z);
		}

		static float IslandOffsetDiff(string a, string b)
		{
			var ma = System.Text.RegularExpressions.Regex.Match(a, "@([^ ]+)");
			var mb = System.Text.RegularExpressions.Regex.Match(b, "@([^ ]+)");
			if (!ma.Success || !mb.Success) return 0f;
			return (ParseVec(ma.Groups[1].Value) - ParseVec(mb.Groups[1].Value)).magnitude;
		}

		#endregion
	}
}

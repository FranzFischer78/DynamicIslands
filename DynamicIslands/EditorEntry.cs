using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The ways into and out of the editor (TODO 5b): the main menu's EDITOR opens the start window (CreateWindow), which
	/// opens the 3D editor on an island (new, or one to open once it is ready) or a world plan on the menu itself, with no
	/// island loaded. It remembers the last island, the last plan and which of the two was made last (editor_start.txt),
	/// so the start window offers "Continue" on them. The editor's Main menu comes back to the start window; an island
	/// opened from a plan's card (Edit island) gets "Back to the plan" in the editor's top bar.
	/// </summary>
	public static class EditorEntry
	{
		public const string Island = "island", Plan = "plan";
		static string FilePath { get { return Path.Combine(DynamicIslands.assetpath, "editor_start.txt"); } }

		/// <summary>The island to open once the editor is ready (null: a new island).</summary>
		public static string OpenIsland;
		/// <summary>The editor's Main menu was used: the start window opens on the menu again.</summary>
		public static bool ReturnToCreate;
		/// <summary>The plan an island was opened from (Edit island on its card): the editor's top bar offers Back to the plan.</summary>
		public static string BackToPlan;

		static Dictionary<string, string> Read()
		{
			var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (File.Exists(FilePath))
					foreach (string line in File.ReadAllLines(FilePath))
					{
						int eq = line.IndexOf('=');
						if (eq > 0) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
					}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] editor_start.txt: " + e.Message); }
			return d;
		}

		static void Write(string key, string value)
		{
			if (string.IsNullOrEmpty(value)) return;
			Dictionary<string, string> d = Read();
			string old, last;
			if (d.TryGetValue(key, out old) && old == value && d.TryGetValue("last", out last) && last == key) return;
			d[key] = value;
			d["last"] = key;
			try { File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "=" + kv.Value).ToArray()); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] editor_start.txt: " + e.Message); }
		}

		/// <summary>The island open in the editor now (named and saved ones only).</summary>
		public static void RememberIsland(string name)
		{
			if (string.IsNullOrEmpty(name) || name.Equals(DynamicIslands.UnnamedIsland, StringComparison.OrdinalIgnoreCase)) return;
			Write(Island, name);
		}

		/// <summary>The world plan shown in the plan window now.</summary>
		public static void RememberPlan(string name)
		{
			if (string.IsNullOrEmpty(name) || WorldPlan.IsBuiltIn(name)) return;
			Write(Plan, name);
		}

		/// <summary>The last island, when its file is still there.</summary>
		public static string LastIsland
		{
			get { string n; return Read().TryGetValue(Island, out n) && n.Length > 0 && File.Exists(IslandSpawner.PathFor(n)) ? n : null; }
		}

		/// <summary>The last world plan, when its file is still there.</summary>
		public static string LastPlan
		{
			get { string n; return Read().TryGetValue(Plan, out n) && n.Length > 0 && !WorldPlan.IsBuiltIn(n) && File.Exists(WorldPlan.PathFor(n)) ? n : null; }
		}

		/// <summary>"island" or "plan": what was worked on last (the start window's card for it is the highlighted one).</summary>
		public static string Last { get { string n; return Read().TryGetValue("last", out n) && n == Plan ? Plan : Island; } }

		/// <summary>The start window's New island / Open an island: the 3D editor, on that island once it is ready.</summary>
		public static void StartIsland(string name)
		{
			OpenIsland = name;
			BackToPlan = null;
			if (name != null) RememberIsland(name);
			DynamicIslands.LoadEditor(new string[0]);
		}

		/// <summary>The plan card's Edit island: the island in the 3D editor, with Back to the plan in its top bar.</summary>
		public static void EditFromPlan(string island, string plan)
		{
			bool inEditor = DynamicIslands.InEditor();
			// (the island open now is kept: saved when it has a name, else in its autosave)
			if (inEditor && EditorAutosave.Unsaved)
			{
				if (!DynamicIslands.IsUnnamed) { if (!DynamicIslands.SaveIsland(DynamicIslands.currentIslandName)) return; }
				else EditorAutosave.WriteNow();
			}
			OpenIsland = island;
			BackToPlan = plan;
			BackIsland = island;
			backToMenu = !inEditor;
			RememberIsland(island);
			if (inEditor) { OpenIsland = null; DynamicIslands.LoadIsland(island); EditorUI.RefreshIsland(); EditorUI.RefreshBackToPlan(); return; }
			DynamicIslands.LoadEditor(new string[0]);
		}

		/// <summary>The island Edit island opened (Back to the plan is offered while it is the one open).</summary>
		public static string BackIsland;
		static bool backToMenu;

		/// <summary>An island was opened in the editor: another one than Edit island's ends Back to the plan.</summary>
		public static void OnIslandOpened(string name)
		{
			// (an island still to open: the editor's first, empty island comes before it)
			if (OpenIsland != null) return;
			if (BackToPlan == null || string.Equals(name, BackIsland, StringComparison.OrdinalIgnoreCase)) return;
			BackToPlan = null;
			BackIsland = null;
			EditorUI.RefreshBackToPlan();
		}

		/// <summary>OpenEditor, once Raft's objects are loaded: the island asked for is opened.</summary>
		public static void OnEditorReady()
		{
			if (newFromPlan != null) { string n = newFromPlan; newFromPlan = null; MakeNew(n); return; }
			string name = OpenIsland;
			OpenIsland = null;
			if (string.IsNullOrEmpty(name)) return;
			if (!File.Exists(IslandSpawner.PathFor(name))) { DynamicIslands.Notify("There is no saved island named '" + name + "' any more", true); return; }
			DynamicIslands.LoadIsland(name);
		}

		/// <summary>The editor's Main menu: back to Raft's menu, where the start window opens again.</summary>
		public static void ToMainMenu()
		{
			if (!DynamicIslands.IsUnnamed) RememberIsland(DynamicIslands.currentIslandName);
			ReturnToCreate = true;
			BackToPlan = null;
			EditorAutosave.WriteNow();
			UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
		}

		/// <summary>The editor's Back to the plan: the island is saved (when it has changes), then the plan opens on the menu.</summary>
		public static void ToPlan()
		{
			string plan = BackToPlan;
			if (plan == null) return;
			if (!DynamicIslands.IsUnnamed && EditorAutosave.Unsaved && !DynamicIslands.SaveIsland(DynamicIslands.currentIslandName)) return;
			if (!DynamicIslands.IsUnnamed) RememberIsland(DynamicIslands.currentIslandName);
			BackToPlan = null;
			BackIsland = null;
			EditorUI.RefreshBackToPlan();
			// (Edit island from a plan in the editor: the plan opens again there)
			if (!backToMenu) { WorldPlanWindow.Open(plan); return; }
			ReturnToCreate = true;
			reopenPlan = plan;
			UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
		}

		static string reopenPlan;

		public const string UseNewPlan = "*new*", UseInStory = "story", UseOther = "other";

		/// <summary>
		/// The island editor's Use in a plan...: the island (saved first) is added to a world plan picked from a list, or a new
		/// one - next in its Main story line, or as another island that comes on its own - and the plan opens on that rule.
		/// </summary>
		public static void UseInPlan()
		{
			if (DynamicIslands.IsUnnamed) { DynamicIslands.Notify("Save the island first (Save as): a plan brings islands by their names", true); IslandFilesWindow.Open(); return; }
			string island = DynamicIslands.currentIslandName;
			if (EditorAutosave.Unsaved && !DynamicIslands.SaveIsland(island)) return;
			var plans = new List<ChoiceWindow.Choice> { new ChoiceWindow.Choice(UseNewPlan, "A new world plan...", "Start a new plan with '" + island + "' in it") };
			plans.AddRange(WorldPlan.All().Where(n => !WorldPlan.IsBuiltIn(n)).Select(n =>
			{
				WorldPlan p = WorldPlan.Load(n);
				int story = p != null ? p.Rules.Count(r => r.InStory) : 0;
				bool has = p != null && p.Rules.Any(r => r.What == "island" && r.WhatArg.Equals(island, StringComparison.OrdinalIgnoreCase));
				return new ChoiceWindow.Choice(n, n, (has ? "has '" + island + "' already - " : "") + story + " island(s) in its Main story line" + (p != null && p.Description.Length > 0 ? " - " + p.Description : ""));
			}));
			ChoiceWindow.Open("Use '" + island + "' in a plan", plans, picked =>
			{
				if (picked == UseNewPlan)
					TextPromptWindow.Open("New world plan", "A name for the new world plan with '" + island + "' in it:", "", n =>
					{
						n = (n ?? "").Trim();
						if (n.Length == 0) return;
						if (WorldPlan.IsBuiltIn(n) || File.Exists(WorldPlan.PathFor(n))) { DynamicIslands.Notify("There is a plan named '" + n + "' already", true); return; }
						AskHow(island, n);
					});
				else AskHow(island, picked);
			}, "");
		}

		static void AskHow(string island, string planName)
		{
			ChoiceWindow.Open("Add '" + island + "' to '" + planName + "' as", new[]
			{
				new ChoiceWindow.Choice(UseInStory, "Next in the Main story line", "The Receiver leads players to it after the story's last island now"),
				new ChoiceWindow.Choice(UseOther, "Another island (comes on its own)", "Not in the story: it comes at the start, ahead of the raft - change WHEN and WHERE on its card"),
			}, how => AddToPlan(island, planName, how == UseInStory, true), "");
		}

		/// <summary>Adds the island to the plan (made when it isn't there) and saves it; the plan opens unless a test says not.</summary>
		public static string AddToPlan(string island, string planName, bool story, bool open)
		{
			WorldPlan p = File.Exists(WorldPlan.PathFor(planName)) ? WorldPlan.Load(planName) : null;
			if (p == null) p = new WorldPlan { Name = planName, Random = false, Description = "" };
			IntroRule r = RuleFor(p, island, story);
			string id = r.Id;
			p.Rules.Add(r);
			try { p.Save(); }
			catch (Exception e) { DynamicIslands.Notify("Could not save the plan: " + e.Message, true); return null; }
			RememberPlan(p.Name);
			DynamicIslands.Notify("'" + island + "' is in '" + p.Name + "' now" + (story ? ": the Main story line's island " + StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules).Count : ""));
			if (open) WorldPlanWindow.Open(p.Name);
			return id;
		}

		/// <summary>
		/// A rule that brings the island into the plan (not added yet): next in its Main story line (on the Receiver, after
		/// the story's last island) or another island (at the start, ahead of the raft). Its name comes from the island's.
		/// </summary>
		public static IntroRule RuleFor(WorldPlan p, string island, bool story)
		{
			string id = IntroRule.CleanId(new string(island.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()));
			if (id.Length == 0) id = WorldPlanWindow.NewRuleId(p);
			while (p.Rules.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) id += "b";
			var r = new IntroRule { Id = id, What = "island", WhatArg = island, When = "start", Label = island.Length > 18 ? island.Substring(0, 18) : island };
			if (story)
			{
				r.StoryPlace = StoryEnd(p);
				r.Where = "receiver";
				r.Distance = 800f;
			}
			else { r.Where = "ahead"; r.Distance = 300f; }
			return r;
		}

		/// <summary>A new island to make from the plan (+ New island): saved empty under its name once the editor is ready.</summary>
		static string newFromPlan;

		/// <summary>
		/// The plan's + New island: the 3D editor on a new, empty island saved under the name (so the plan's rule finds it),
		/// with Back to the plan in its top bar.
		/// </summary>
		public static void NewIslandFromPlan(string island, string plan)
		{
			bool inEditor = DynamicIslands.InEditor();
			if (inEditor && EditorAutosave.Unsaved)
			{
				if (!DynamicIslands.IsUnnamed) { if (!DynamicIslands.SaveIsland(DynamicIslands.currentIslandName)) return; }
				else EditorAutosave.WriteNow();
			}
			BackToPlan = plan;
			BackIsland = island;
			backToMenu = !inEditor;
			if (inEditor) { MakeNew(island); return; }
			newFromPlan = island;
			OpenIsland = null;
			DynamicIslands.LoadEditor(new string[0]);
		}

		static void MakeNew(string island)
		{
			string plan = BackToPlan;
			BackToPlan = null; // (the empty island opened on the way is not the plan's)
			DynamicIslands.NewIsland();
			bool saved = DynamicIslands.SaveIsland(island);
			BackToPlan = plan;
			BackIsland = island;
			if (saved) RememberIsland(island);
			EditorUI.RefreshIsland();
			EditorUI.RefreshBackToPlan();
			DynamicIslands.Notify("New island '" + island + "' for the plan '" + plan + "': shape the land on the Terrain tab, then Back to the plan");
		}

		/// <summary>The story place after the Main story line's last island (Raft's ending, Utopia, stays the last).</summary>
		public static string StoryEnd(WorldPlan p)
		{
			List<string> steps = StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules);
			for (int i = steps.Count - 1; i >= 0; i--)
			{
				string s = steps[i];
				if (!StoryChain.IsRaft(s)) return "after:" + StoryChain.RuleIdOf(s);
				ChunkPointType t = StoryChain.TypeOfStep(s);
				if (t != ChunkPointType.Landmark_Utopia) return "after:" + StoryOrder.Key(t);
			}
			return "first";
		}

		/// <summary>The main menu was built (HookUI): the start window (and the plan, after Back to the plan) open again.</summary>
		public static void OnMainMenu()
		{
			if (!ReturnToCreate) return;
			ReturnToCreate = false;
			string plan = reopenPlan;
			reopenPlan = null;
			CreateWindow.Open();
			if (plan != null) WorldPlanWindow.Open(plan);
		}
	}
}

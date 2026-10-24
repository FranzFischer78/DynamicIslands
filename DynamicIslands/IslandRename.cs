using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Renames a saved island (ROADMAP E9): its file, the copies kept for saved worlds (&lt;name&gt;_&lt;hash&gt;), its line in
	/// spawnpool.txt, the plans and other islands whose rules name it (island:, oneof:, a quest/step/zone/visit/signal of it,
	/// near it), and the saved worlds that have it or whose kept plan brings it - every copy of their state, in Raft's
	/// world folders too - with their quest rewards. Nothing else changes: the island's file keeps its content (and hash).
	/// </summary>
	public static class IslandRename
	{
		static string WorldsFolder { get { return Path.Combine(DynamicIslands.assetpath, "worlds"); } }

		/// <summary>Why the island can't be renamed to that name, or null.</summary>
		public static string Problem(string from, string to)
		{
			string f = FromProblem(from);
			if (f != null) return f;
			from = from.Trim(); to = (to ?? "").Trim();
			if (to.Length == 0) return "Type the new name.";
			string p = FileNames.IslandProblem(to);
			if (p != null) return p;
			if (to == from) return "That is its name already - type a new one.";
			if (IsCopyName(to)) return "A name ending in _ and 12 letters/digits is how the mod names copies kept for saved worlds - pick another.";
			if (!to.Equals(from, StringComparison.OrdinalIgnoreCase) && File.Exists(IslandSpawner.PathFor(to))) return "There is an island called '" + to + "' already.";
			return null;
		}

		/// <summary>Why this island can't be renamed at all, or null.</summary>
		public static string FromProblem(string from)
		{
			from = (from ?? "").Trim();
			if (from.Length == 0 || !File.Exists(IslandSpawner.PathFor(from))) return "Pick a saved island to rename.";
			if (IslandNetwork.IsDownloadName(from)) return "'" + from + "' is a copy kept for a saved world - rename the island itself.";
			return null;
		}

		static bool IsCopyName(string name)
		{
			int i = name.LastIndexOf('_');
			return i > 0 && name.Length - i - 1 == 12 && name.Substring(i + 1).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
		}

		/// <summary>Renames the island; the report says what else changed. Throws when the file can't be moved (nothing changed then).</summary>
		public static List<string> Rename(string from, string to)
		{
			from = from.Trim(); to = to.Trim();
			string problem = Problem(from, to);
			if (problem != null) throw new InvalidOperationException(problem);
			var report = new List<string>();

			// The file first (a change of case only goes by a temporary name: Windows sees the same file)
			MoveFile(IslandSpawner.PathFor(from), IslandSpawner.PathFor(to));
			int copies = 0;
			// (a copy is name_<its content's hash>: a player's own island "camp_202609281530" only looks like one - review 2026-10-06)
			var movedCopies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (string file in Directory.GetFiles(DynamicIslands.assetpath, from + "_*" + IslandFile.Extension))
				{
					string n = Path.GetFileNameWithoutExtension(file);
					if (n.Length != from.Length + 13 || !n.StartsWith(from + "_", StringComparison.OrdinalIgnoreCase) || !IsCopyName(n) || !IslandNetwork.IsDownloadName(n)) continue;
					// (each on its own: one in use - an antivirus - left every copy after it under the old name)
					try { MoveFile(file, IslandSpawner.PathFor(to + n.Substring(from.Length))); movedCopies.Add(n); copies++; }
					catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming the copy " + n + ": " + e.Message); }
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming the copies of '" + from + "': " + e.Message); }
			if (copies > 0) report.Add(copies + " cop" + (copies == 1 ? "y" : "ies") + " kept for saved worlds");

			if (RenamePoolLine(from, to)) report.Add("spawnpool.txt");

			// The island library's record of what it installed (its Remove would miss the island)
			try
			{
				List<LibraryInstalled> installed = LibraryPack.Installed();
				bool lib = false;
				foreach (LibraryInstalled e in installed)
					foreach (LibraryInstalledFile f in e.files)
						if (f.kind != LibraryPack.KindPlan && f.name.Equals(from, StringComparison.OrdinalIgnoreCase)) { f.name = to; lib = true; }
				if (lib) { LibraryPack.SaveInstalled(installed); report.Add("the island library's list of what it installed"); }
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming '" + from + "' in installed.json: " + e.Message); }

			// Plans and islands whose rules name it
			var plans = new List<string>();
			foreach (string p in WorldPlan.All().Where(x => !WorldPlan.IsBuiltIn(x)))
			{
				try
				{
					WorldPlan plan = WorldPlan.Load(p);
					if (plan != null && RenameIn(plan.Rules, from, to)) { plan.Save(); plans.Add(p); }
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming '" + from + "' in the plan '" + p + "': " + e.Message); }
			}
			if (plans.Count > 0) report.Add("the plan" + (plans.Count == 1 ? " " : "s ") + string.Join(", ", plans.Select(x => "'" + x + "'").ToArray()));

			var islands = new List<string>();
			foreach (string file in Directory.GetFiles(DynamicIslands.assetpath, "*" + IslandFile.Extension))
			{
				string other = Path.GetFileNameWithoutExtension(file);
				if (IslandNetwork.IsDownloadName(other)) continue;
				try
				{
					List<IntroRule> rules = IslandCache.RulesOf(other);
					if (!RenameIn(rules, from, to)) continue;
					IslandFile f = IslandFile.Load(file);
					rules = WorldDirector.RulesFromProps(f.Props);
					RenameIn(rules, from, to);
					WorldDirector.SetRulesInProps(f.Props, rules);
					f.Save(file);
					// (the island open in the editor: its settings in memory too, or its next save would bring the old name back)
					if (other.Equals(DynamicIslands.currentIslandName, StringComparison.OrdinalIgnoreCase) || (other.Equals(to, StringComparison.OrdinalIgnoreCase) && from.Equals(DynamicIslands.currentIslandName, StringComparison.OrdinalIgnoreCase)))
					{
						// (as an undo step in the editor, like any change to its rules: Ctrl+Z takes it back, and it counts as unsaved)
						List<IntroRule> open = WorldDirector.RulesFromProps(DynamicIslands.currentIslandProps);
						if (RenameIn(open, from, to))
						{
							if (DynamicIslands.InEditor()) IslandSettingsUndo.Change(() => WorldDirector.SetRulesInProps(DynamicIslands.currentIslandProps, open));
							else WorldDirector.SetRulesInProps(DynamicIslands.currentIslandProps, open);
						}
					}
					if (!other.Equals(to, StringComparison.OrdinalIgnoreCase)) islands.Add(other);
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming '" + from + "' in the rules of '" + other + "': " + e.Message); }
			}
			IslandCache.SaveRules();
			if (islands.Count > 0) report.Add("the rules of " + string.Join(", ", islands.Take(6).Select(x => "'" + x + "'").ToArray()) + (islands.Count > 6 ? " and " + (islands.Count - 6) + " more" : ""));

			// Saved worlds: their island lists, kept plans and story rules, and quest rewards
			var worlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string file in WorldFiles())
			{
				try
				{
					string[] lines = File.ReadAllLines(file);
					bool changed = false;
					for (int i = 0; i < lines.Length; i++)
					{
						string l = lines[i];
						string ruleKey = new[] { "@planrule=", "@storyrule=", "@storybeside=" }.FirstOrDefault(k => l.StartsWith(k));
						if (ruleKey != null)
						{
							IntroRule r = IntroRule.Parse(l.Substring(ruleKey.Length));
							if (r != null && RenameIn(new List<IntroRule> { r }, from, to, false)) { lines[i] = ruleKey + r.ToLine(); changed = true; }
						}
						else if (l.StartsWith("@planhash="))
						{
							// (the version of the island the world's plan brings - AU6: lost with the old name, and the copy it names
							// looked unused to Tidy up)
							int colon = l.LastIndexOf(':');
							if (colon > 10 && l.Substring(10, colon - 10).Trim().Equals(from, StringComparison.OrdinalIgnoreCase)) { lines[i] = "@planhash=" + to + l.Substring(colon); changed = true; }
						}
						else if (l.StartsWith("@islandsoff="))
						{
							// (the pool's islands left out of this world: the island stays left out - review 2026-10-06)
							HashSet<string> off = WorldIslands.Parse(l.Substring("@islandsoff=".Length));
							if (off.Remove(from)) { off.Add(to); lines[i] = "@islandsoff=" + WorldIslands.Join(off); changed = true; }
						}
						else if (!l.StartsWith("@") && !l.StartsWith("#") && l.Split('|')[0].Trim().Equals(from, StringComparison.OrdinalIgnoreCase))
						{
							lines[i] = to + l.Substring(l.IndexOf('|') < 0 ? l.Length : l.IndexOf('|'));
							changed = true;
						}
					}
					if (!changed) continue;
					SafeFile.WriteAllLines(file, lines);
					worlds.Add(WorldName(lines, file));
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming '" + from + "' in " + file + ": " + e.Message); }
			}
			try
			{
				if (Directory.Exists(WorldsFolder))
					foreach (string file in Directory.GetFiles(WorldsFolder, "*.rewards"))
					{
						string[] lines = File.ReadAllLines(file);
						bool changed = false;
						for (int i = 0; i < lines.Length; i++)
							foreach (string k in new[] { "rewarded ", "owed " })
							{
								if (!lines[i].StartsWith(k)) continue;
								string key = lines[i].Substring(k.Length).Trim();
								// (the island's second and later quests are "<name>#quest2"... - review 2026-10-06)
								if (key.Equals(from, StringComparison.OrdinalIgnoreCase)) { lines[i] = k + to; changed = true; }
								else if (key.StartsWith(from + "#quest", StringComparison.OrdinalIgnoreCase) || key.StartsWith(from + "|", StringComparison.OrdinalIgnoreCase)) { lines[i] = k + to + key.Substring(from.Length); changed = true; }
							}
						if (changed) SafeFile.WriteAllLines(file, lines);
					}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Renaming '" + from + "' in the quest rewards: " + e.Message); }
			QuestRewards.Forget();
			worlds.Remove(IslandTest.WorldName);
			if (worlds.Count > 0) report.Add("the saved world" + (worlds.Count == 1 ? " " : "s ") + string.Join(", ", worlds.Take(6).Select(x => "'" + x + "'").ToArray()) + (worlds.Count > 6 ? " and " + (worlds.Count - 6) + " more" : ""));

			// The world loaded now (the editor's): its list in memory, or its next save would write the old name again
			if (WorldIslands.Off.Remove(from)) WorldIslands.Off.Add(to);
			string planHash;
			if (WorldDirector.PlanHashes.TryGetValue(from, out planHash)) { WorldDirector.PlanHashes.Remove(from); WorldDirector.PlanHashes[to] = planHash; }
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.HostName.Equals(from, StringComparison.OrdinalIgnoreCase)) e.HostName = to;
				if (e.Name.Equals(from, StringComparison.OrdinalIgnoreCase)) e.Name = to;
				else if (e.Name.StartsWith(from + "_", StringComparison.OrdinalIgnoreCase) && movedCopies.Contains(e.Name)) e.Name = to + e.Name.Substring(from.Length);
			}
			if (DynamicIslands.currentIslandName.Equals(from, StringComparison.OrdinalIgnoreCase))
			{
				DynamicIslands.currentIslandName = to;
				EditorAutosave.Renamed(from, to);
			}
			Debug.Log("[CUSTOM ISLANDS] Renamed the island '" + from + "' to '" + to + "'" + (report.Count > 0 ? " (also in " + string.Join("; ", report.ToArray()) + ")" : ""));
			return report;
		}

		static void MoveFile(string from, string to)
		{
			if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
			{
				string temp = to + ".renaming";
				File.Move(from, temp);
				File.Move(temp, to);
			}
			else File.Move(from, to);
			// (the island lists' summaries: neither name's is right now)
			IslandSummaries.Forget(Path.GetFileNameWithoutExtension(from));
			IslandSummaries.Forget(Path.GetFileNameWithoutExtension(to));
		}

		/// <summary>
		/// The island renamed in these rules: what they bring (island:, oneof:), when (the quest, a step, a zone, a visit or a
		/// signal of it) and where (near it). A reference to a rule of the same name is left alone (WorldDirector.Refs looks
		/// for rules first). True if any changed.
		/// </summary>
		internal static bool RenameIn(List<IntroRule> rules, string from, string to, bool sameList = true)
		{
			bool ruleNamed = sameList && rules.Any(r => r.Id.Equals(from, StringComparison.OrdinalIgnoreCase));
			bool changed = false;
			foreach (IntroRule r in rules)
			{
				if (r.What == "island" && r.WhatArg.Trim().Equals(from, StringComparison.OrdinalIgnoreCase)) { r.WhatArg = to; changed = true; }
				if (r.What == "oneof")
				{
					string[] parts = r.WhatArg.Split(',');
					if (parts.Any(x => x.Trim().Equals(from, StringComparison.OrdinalIgnoreCase)))
					{
						r.WhatArg = string.Join(", ", parts.Select(x => x.Trim().Equals(from, StringComparison.OrdinalIgnoreCase) ? to : x.Trim()).ToArray());
						changed = true;
					}
				}
				if (!ruleNamed && r.When != "rule" && r.WhenRef.Trim().Equals(from, StringComparison.OrdinalIgnoreCase)) { r.WhenRef = to; changed = true; }
				if (!ruleNamed && r.Where == "near" && r.WhereRef.Trim().Equals(from, StringComparison.OrdinalIgnoreCase)) { r.WhereRef = to; changed = true; }
			}
			return changed;
		}

		/// <summary>Every copy of every world's state on this PC: the mod's world files and the copies in Raft's world folders.</summary>
		static List<string> WorldFiles()
		{
			var files = new List<string>();
			try { if (Directory.Exists(WorldsFolder)) files.AddRange(Directory.GetFiles(WorldsFolder, "*.txt")); } catch { }
			try
			{
				string raft = SaveAndLoad.WorldPath;
				if (!string.IsNullOrEmpty(raft) && Directory.Exists(raft))
					foreach (string world in Directory.GetDirectories(raft))
					{
						files.AddRange(Directory.GetFiles(world, WorldCopy.FileName));
						foreach (string save in Directory.GetDirectories(world)) files.AddRange(Directory.GetFiles(save, WorldCopy.FileName));
					}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking through Raft's world folders: " + e.Message); }
			return files;
		}

		static string WorldName(string[] lines, string file)
		{
			return Housekeeping.WorldName(lines, file);
		}

		static bool RenamePoolLine(string from, string to)
		{
			try
			{
				string path = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
				if (!File.Exists(path)) return false;
				string[] lines = File.ReadAllLines(path);
				bool changed = false;
				for (int i = 0; i < lines.Length; i++)
				{
					string l = lines[i].Trim();
					if (l.Length == 0 || l.StartsWith("#") || l.Contains("=")) continue;
					if (l.Equals(from, StringComparison.OrdinalIgnoreCase)) { lines[i] = to; changed = true; continue; }
					int sp = l.LastIndexOf(' ');
					float w;
					if (sp > 0 && l.Substring(0, sp).Trim().Equals(from, StringComparison.OrdinalIgnoreCase) && float.TryParse(l.Substring(sp + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w))
					{ lines[i] = to + l.Substring(sp); changed = true; }
				}
				if (!changed) return false;
				SafeFile.WriteAllLines(path, lines);
				CustomIslandSpawner.LoadPool(true);
				return true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not change spawnpool.txt: " + e.Message); return false; }
		}
	}
}

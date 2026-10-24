using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Tidy up (the island library window's Installed tab): files the mod made that nothing uses any more pile up in
	/// Mods\DynamicIslands otherwise -
	///  - island copies downloaded from hosts or kept for a saved world (&lt;name&gt;_&lt;hash&gt;) that no saved world uses
	///    (moved to the deleted folder: a copy kept for a saved world can be the only one of that version);
	///  - generated islands (gen-...: made while sailing, or with the generator's Make and never given a name) that no
	///    saved world, plan, island rule, library entry or the editor uses (moved to the deleted folder: they can be
	///    got back);
	///  - the mod's file of a world whose Raft world was deleted (moved to worlds\removed). Only a world this PC hosted
	///    ("@savedby=" its own Steam id): the copies of worlds a player joined stay, they are kept to host them later.
	/// "Used" counts every copy of every world's state: the mod's own, and the ones in Raft's world folders, the older
	/// saves too (a copy an older save still needs was removed before).
	/// </summary>
	public static class Housekeeping
	{
		public const string RemovedWorldsFolder = "removed";
		static string WorldsFolder { get { return Path.Combine(DynamicIslands.assetpath, "worlds"); } }
		static ulong LocalSteamId { get { try { return Steamworks.SteamUser.GetSteamID().m_SteamID; } catch { return 0UL; } } }

		/// <summary>The line that says which PC hosted the world when this state was saved (IslandWorldState.Save).</summary>
		public static string SavedByLine() { ulong id = LocalSteamId; return id != 0 ? "@savedby=" + id.ToString(CultureInfo.InvariantCulture) : null; }

		/// <summary>What a look through the files found.</summary>
		public class Scan
		{
			/// <summary>Each island copy (name_hash) and the worlds that use it.</summary>
			public List<KeyValuePair<string, List<string>>> Copies = new List<KeyValuePair<string, List<string>>>();
			public List<string> UnusedGenerated = new List<string>();
			/// <summary>The mod's files of worlds this PC hosted whose Raft world is gone: path and world name.</summary>
			public List<KeyValuePair<string, string>> DeletedWorlds = new List<KeyValuePair<string, string>>();
			public int UnusedCopies { get { return Copies.Count(c => c.Value.Count == 0); } }
			public int Total { get { return UnusedCopies + UnusedGenerated.Count + DeletedWorlds.Count; } }

			public string Describe()
			{
				if (Total == 0) return "Nothing to tidy up" + (Copies.Count > 0 ? " (" + Copies.Count + " island copies from hosts, all used by your saved worlds)." : ".");
				var parts = new List<string>();
				if (UnusedCopies > 0) parts.Add(UnusedCopies + " island copies from hosts no saved world uses");
				if (UnusedGenerated.Count > 0) parts.Add(UnusedGenerated.Count + " generated islands (gen-...) nothing uses");
				if (DeletedWorlds.Count > 0) parts.Add("the files of " + DeletedWorlds.Count + " deleted world(s)");
				return "To tidy up: " + string.Join(", ", parts.ToArray()) + ".";
			}

			/// <summary>What this (newer) look found that the earlier one showed too: Tidy up's second click looks again and
			/// clears only what the player was shown and is still unused (a world saved between the clicks keeps its files).</summary>
			public Scan Within(Scan earlier)
			{
				var r = new Scan();
				r.Copies.AddRange(Copies.Where(c => earlier.Copies.Any(e => e.Value.Count == 0 && e.Key.Equals(c.Key, StringComparison.OrdinalIgnoreCase))));
				r.UnusedGenerated.AddRange(UnusedGenerated.Where(g => earlier.UnusedGenerated.Contains(g, StringComparer.OrdinalIgnoreCase)));
				r.DeletedWorlds.AddRange(DeletedWorlds.Where(w => earlier.DeletedWorlds.Any(e => e.Key.Equals(w.Key, StringComparison.OrdinalIgnoreCase))));
				return r;
			}
		}

		/// <summary>Copies of worlds' state the last AllWorldCopies couldn't read (locked, a folder that can't be listed): what
		/// they use isn't known, so Tidy up takes nothing away then.</summary>
		static int unreadable;

		/// <summary>Every copy of every world's state on this PC: (world name, lines).</summary>
		public static IEnumerable<KeyValuePair<string, string[]>> AllWorldCopies()
		{
			foreach (var f in WorldCopyFiles()) yield return new KeyValuePair<string, string[]>(WorldName(f.Value, f.Key), f.Value);
		}

		/// <summary>Every world copy's file and its lines (file -> lines).</summary>
		static IEnumerable<KeyValuePair<string, string[]>> WorldCopyFiles()
		{
			unreadable = 0;
			var files = new List<string>();
			try { if (Directory.Exists(WorldsFolder)) files.AddRange(Directory.GetFiles(WorldsFolder, "*.txt")); } catch { unreadable++; }
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
			catch (Exception e) { unreadable++; Debug.LogWarning("[CUSTOM ISLANDS] Looking through Raft's world folders: " + e.Message); }
			foreach (string f in files)
			{
				string[] lines;
				try { lines = File.ReadAllLines(f); } catch (Exception e) { unreadable++; Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + f + ": " + e.Message); continue; }
				yield return new KeyValuePair<string, string[]>(f, lines);
			}
		}

		/// <summary>
		/// The world's name from its file's first line ("# Custom islands in world '&lt;name&gt;': ..."), else the file's name.
		/// The name runs to the "': " after it (ROADMAP AU29: it was read up to the first apostrophe - "Bob's raft" became
		/// "Bob", looked deleted, and Tidy up moved the live world's file away).
		/// </summary>
		public static string WorldName(string[] lines, string file)
		{
			const string prefix = "# Custom islands in world '";
			string head = lines.FirstOrDefault(l => l.StartsWith(prefix));
			if (head == null) return Path.GetFileNameWithoutExtension(file);
			string rest = head.Substring(prefix.Length);
			int end = rest.LastIndexOf("':");
			if (end < 0) end = rest.LastIndexOf('\'');
			return end >= 0 ? rest.Substring(0, end) : rest;
		}

		/// <summary>The island names a world's state uses: its islands (and the copy its hash names), the islands its kept plan brings.</summary>
		static IEnumerable<string> NamesIn(string[] lines)
		{
			foreach (string l in lines)
			{
				if (l.StartsWith("#")) continue;
				if (l.StartsWith("@planrule="))
				{
					IntroRule r = IntroRule.Parse(l.Substring(10));
					if (r != null) foreach (string n in RuleIslands(r)) yield return n;
					continue;
				}
				// (the copy of a plan island the world will bring - its recorded version: not tidied away - AU6)
				if (l.StartsWith("@planhash="))
				{
					int colon = l.LastIndexOf(':');
					if (colon > 10 && IslandNetwork.IsHash(l.Substring(colon + 1).Trim())) yield return IslandNetwork.DownloadName(l.Substring(10, colon - 10).Trim(), l.Substring(colon + 1).Trim());
					continue;
				}
				if (l.StartsWith("@")) continue;
				string[] p = l.Split('|');
				if (p.Length < 4 || p[0].Trim().Length == 0) continue;
				yield return p[0].Trim();
				if (p.Length > 7 && p[7].Trim().Length > 0) yield return IslandNetwork.DownloadName(p[0].Trim(), p[7].Trim());
			}
		}

		static IEnumerable<string> RuleIslands(IntroRule r)
		{
			if (r.What == "island") yield return r.WhatArg.Trim();
			else if (r.What == "oneof") foreach (string x in r.WhatArg.Split(',')) if (x.Trim().Length > 0) yield return x.Trim();
		}

		/// <summary>Each island named in a saved world (its list or its kept plan), with those worlds (not the editor's test world) - My islands.</summary>
		public static Dictionary<string, HashSet<string>> WorldsByIsland()
		{
			var usedBy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var w in AllWorldCopies())
			{
				if (w.Key.Equals(IslandTest.WorldName, StringComparison.OrdinalIgnoreCase)) continue;
				foreach (string n in NamesIn(w.Value))
				{
					HashSet<string> set;
					if (!usedBy.TryGetValue(n, out set)) usedBy[n] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					set.Add(w.Key);
				}
			}
			return usedBy;
		}

		/// <summary>Looks through everything (a moment with many worlds: done when the Installed tab is shown).</summary>
		public static Scan Look()
		{
			var clock = System.Diagnostics.Stopwatch.StartNew();
			long tWorlds, tNamed;
			var scan = new Scan();
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var usedBy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			// (the files read here are kept for DeletedWorldFiles: it read the worlds folder a second time - 2-3 s in a slow box)
			var read = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
			foreach (var f in WorldCopyFiles())
			{
				read[Path.GetFullPath(f.Key)] = f.Value;
				string world = WorldName(f.Value, f.Key);
				foreach (string n in NamesIn(f.Value))
				{
					used.Add(n);
					HashSet<string> set;
					if (!usedBy.TryGetValue(n, out set)) usedBy[n] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					if (!world.Equals(IslandTest.WorldName, StringComparison.OrdinalIgnoreCase)) set.Add(world);
				}
			}
			tWorlds = clock.ElapsedMilliseconds;
			// (a world's copy that couldn't be read - an antivirus, a sync: the islands it uses looked unused and were taken
			// away from it)
			if (unreadable > 0)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Tidy up: " + unreadable + " world file(s) couldn't be read - no island is tidied away this time");
				scan.DeletedWorlds = DeletedWorldFiles(read);
				return scan;
			}
			// (plans, islands' own rules, library entries, the pool and the editor count too - for generated islands; only
			// looked at when there is a generated island no world uses: reading every island's rules takes a while)
			var named = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
			List<string> islands = IslandSpawner.ListSavedIslands().ToList();
			bool genToCheck = islands.Any(n => n.StartsWith("gen-", StringComparison.OrdinalIgnoreCase) && !IslandNetwork.IsDownloadName(n) && !used.Contains(n));
			if (genToCheck) {
			try { foreach (string p in WorldPlan.All()) { WorldPlan plan = WorldPlan.Load(p); if (plan != null) foreach (IntroRule r in plan.Rules) foreach (string n in RuleIslands(r)) named.Add(n); } } catch { }
			// (the files' times from one listing of the folder, not two disk calls an island)
			var times = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
			try { foreach (FileInfo fi in new DirectoryInfo(DynamicIslands.assetpath).GetFiles("*" + IslandFile.Extension)) times[Path.GetFileNameWithoutExtension(fi.Name)] = fi.LastWriteTimeUtc.Ticks; } catch { }
			foreach (string i in islands.Where(n => !IslandNetwork.IsDownloadName(n)))
			{
				long ticks;
				try { foreach (IntroRule r in IslandCache.RulesOf(i, times.TryGetValue(i, out ticks) ? ticks : 0)) foreach (string n in RuleIslands(r)) named.Add(n); } catch { }
			}
			try { foreach (LibraryInstalled e in LibraryPack.Installed()) foreach (LibraryInstalledFile f in e.files) named.Add(f.name); } catch { }
			try
			{
				string pool = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
				if (File.Exists(pool)) foreach (string l in File.ReadAllLines(pool)) { string t = l.Trim(); if (t.Length > 0 && !t.StartsWith("#") && !t.Contains("=")) named.Add(t.Split(' ')[0]); }
			}
			catch { }
			if (!string.IsNullOrEmpty(DynamicIslands.currentIslandName)) named.Add(DynamicIslands.currentIslandName);
			IslandCache.SaveRules();
			}
			tNamed = clock.ElapsedMilliseconds;

			foreach (string n in islands)
			{
				if (IslandNetwork.IsDownloadName(n))
				{
					HashSet<string> by;
					scan.Copies.Add(new KeyValuePair<string, List<string>>(n, used.Contains(n) && usedBy.TryGetValue(n, out by) ? (by.Count > 0 ? by.ToList() : new List<string> { IslandTest.WorldName }) : new List<string>()));
				}
				else if (n.StartsWith("gen-", StringComparison.OrdinalIgnoreCase) && !named.Contains(n)) scan.UnusedGenerated.Add(n);
			}
			scan.DeletedWorlds = DeletedWorldFiles(read);
			Debug.Log("[CUSTOM ISLANDS] Tidy up looked through " + islands.Count + " islands and every world's saves in " + clock.ElapsedMilliseconds + " ms (worlds " + tWorlds + ", plans and rules " + (tNamed - tWorlds) + ")");
			return scan;
		}

		/// <summary>The mod's files of worlds this PC hosted ("@savedby=" its id) whose Raft world folder is gone.</summary>
		public static List<KeyValuePair<string, string>> DeletedWorldFiles() { return DeletedWorldFiles(null); }

		/// <summary>The same, with world files already read (full path -> lines) not read again.</summary>
		static List<KeyValuePair<string, string>> DeletedWorldFiles(Dictionary<string, string[]> read)
		{
			var result = new List<KeyValuePair<string, string>>();
			ulong me = LocalSteamId;
			string raft = null;
			try { raft = SaveAndLoad.WorldPath; } catch { }
			if (me == 0 || string.IsNullOrEmpty(raft) || !Directory.Exists(raft) || !Directory.Exists(WorldsFolder)) return result;
			string mine = "@savedby=" + me.ToString(CultureInfo.InvariantCulture);
			foreach (string f in Directory.GetFiles(WorldsFolder, "*.txt"))
			{
				string[] lines;
				try { if (read == null || !read.TryGetValue(Path.GetFullPath(f), out lines)) lines = File.ReadAllLines(f); } catch { continue; }
				if (!lines.Any(l => l.Trim() == mine)) continue; // (a world joined, or saved before the line was written: kept)
				string name = WorldName(lines, f);
				if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) continue;
				if (Directory.Exists(Path.Combine(raft, name))) continue;
				result.Add(new KeyValuePair<string, string>(f, name));
			}
			return result;
		}

		/// <summary>Tidies up what Look found: returns what was done, for the player.</summary>
		public static string TidyUp(Scan scan = null)
		{
			if (scan == null) scan = Look();
			var done = new List<string>();
			int copies = 0, generated = 0, worlds = 0;
			foreach (var c in scan.Copies.Where(c => c.Value.Count == 0))
				// (to the deleted folder, as every island: a copy kept for a saved world can be the only one of that version)
				try { IslandFilesWindow.MoveToDeleted(c.Key); copies++; } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not remove " + c.Key + ": " + e.Message); }
			foreach (string g in scan.UnusedGenerated)
				try { IslandFilesWindow.MoveToDeleted(g); generated++; } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not move " + g + ": " + e.Message); }
			string removed = Path.Combine(WorldsFolder, RemovedWorldsFolder);
			foreach (var w in scan.DeletedWorlds)
				try
				{
					Directory.CreateDirectory(removed);
					string to = Path.Combine(removed, Path.GetFileName(w.Key));
					// (one moved there before - the world's folder brought back and deleted again - is kept beside it)
					if (File.Exists(to)) to = Path.Combine(removed, Path.GetFileNameWithoutExtension(w.Key) + " " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".txt");
					File.Move(w.Key, to);
					worlds++;
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not move " + w.Key + ": " + e.Message); }
			if (copies > 0) done.Add("moved " + copies + " unused island copies from hosts to Mods\\DynamicIslands\\" + IslandFilesWindow.DeletedFolderName);
			if (generated > 0) done.Add("moved " + generated + " unused generated islands to Mods\\DynamicIslands\\" + IslandFilesWindow.DeletedFolderName);
			if (worlds > 0) done.Add("moved the files of " + worlds + " deleted world(s) (" + string.Join(", ", scan.DeletedWorlds.Take(3).Select(w => "'" + w.Value + "'").ToArray()) + (worlds > 3 ? "..." : "") + ") to worlds\\" + RemovedWorldsFolder);
			if (copies + generated > 0) IslandCache.Forget();
			string text = done.Count == 0 ? "Nothing to tidy up." : "Tidied up: " + string.Join("; ", done.ToArray()) + ".";
			Debug.Log("[CUSTOM ISLANDS] " + text);
			return text;
		}
	}
}

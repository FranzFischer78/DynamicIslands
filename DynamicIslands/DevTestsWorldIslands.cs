using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of the islands a world gets while sailing (WorldIslands, IslandPickerWindow): the list in the New Game box's
	/// World settings clicked as a player does, and a world made with some islands left out (worldislands.ps1).
	/// </summary>
	public static partial class DevTests
	{
		static HashSet<string> EntriesArg(string[] args)
		{
			return WorldIslands.Parse(args != null ? string.Join(" ", args) : "");
		}

		[ConsoleCommand(name: "CIIslandPickerBox", docs: "Dev, main menu: the World settings' 'Choose islands' list clicked as a player does - every entry of the spawn pool has a row with a tick box, a click unticks and ticks it again, the search narrows the rows, Untick shown / Tick shown do the rows shown, the counts follow, Done closes it. Leaves the entries named unticked for the next world: CIIslandPickerBox [entry|entry...] (default none)")]
		public static void IslandPickerBoxCommand(string[] args) { StartTest(IslandPickerBoxRoutine(EntriesArg(args))); }

		static IEnumerator IslandPickerBoxRoutine(HashSet<string> want)
		{
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("island picker: no New Game box (main menu?)"); yield break; }
			bool ok = true;
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			yield return new WaitForSecondsRealtime(1f);
			WorldSettingsWindow.Open();
			yield return null;
			Button entry = IslandPickerWindow.EntryButton;
			Check(ref ok, entry != null && entry.gameObject.activeInHierarchy, "the World settings window has 'Islands while sailing' ('" + (entry != null ? UIKit.LabelOf(entry).text : "") + "')");
			if (entry == null) { Fail("island picker"); yield break; }
			var unknown = want.Where(w => !WorldIslands.Candidates().Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
			Check(ref ok, unknown.Count == 0, "the entries to leave out are in the pool" + (unknown.Count > 0 ? " - not: " + string.Join(", ", unknown.ToArray()) : ""));
			// (from a clean start: everything ticked)
			WorldIslands.Chosen.Clear();
			entry.onClick.Invoke();
			yield return null;
			Check(ref ok, IslandPickerWindow.IsOpen, "the button opens the list");
			List<string> all = WorldIslands.Candidates();
			var missing = all.Where(e => IslandPickerWindow.Row(e) == null).ToList();
			Check(ref ok, missing.Count == 0 && all.Count > 0, "a row for each of the pool's " + all.Count + " entries" + (missing.Count > 0 ? " - missing: " + string.Join(", ", missing.Take(5).ToArray()) : ""));
			Check(ref ok, all.All(IslandPickerWindow.IsTicked), "every row ticked at first");
			Check(ref ok, IslandPickerWindow.Count.text.StartsWith(all.Count + " of " + all.Count), "the count: '" + IslandPickerWindow.Count.text + "'");
			Screenshot(new[] { "islandpicker" });
			yield return new WaitForSecondsRealtime(0.6f);

			// A click unticks a row, another ticks it again
			string first = all.FirstOrDefault(WorldIslands.IsIsland) ?? all[0];
			IslandPickerWindow.Row(first).onClick.Invoke(); yield return null;
			Check(ref ok, !IslandPickerWindow.IsTicked(first) && WorldIslands.Chosen.Contains(first) && IslandPickerWindow.Count.text.StartsWith((all.Count - 1) + " of " + all.Count),
				"a click unticks '" + first + "' (" + IslandPickerWindow.Count.text + "; the World settings button: '" + UIKit.LabelOf(entry).text + "')");
			IslandPickerWindow.Row(first).onClick.Invoke(); yield return null;
			Check(ref ok, IslandPickerWindow.IsTicked(first) && WorldIslands.Chosen.Count == 0, "another click ticks it again");

			// The search, and Untick shown / Tick shown on what it found
			string part = first.Length > 3 ? first.Substring(0, 3) : first;
			IslandPickerWindow.Search.text = part; yield return null;
			List<string> shown = IslandPickerWindow.Shown();
			List<string> expect = all.Where(e => WorldIslands.Label(e).IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			Check(ref ok, shown.Count > 0 && new HashSet<string>(shown).SetEquals(expect), "searching '" + part + "' shows the " + expect.Count + " matching rows (shown " + shown.Count + ")");
			Check(ref ok, Click(IslandPickerWindow.Window.gameObject, "Untick shown"), "Untick shown clicked");
			yield return null;
			Check(ref ok, WorldIslands.Chosen.Count == shown.Count && shown.All(e => !IslandPickerWindow.IsTicked(e)) && all.Except(shown).All(IslandPickerWindow.IsTicked),
				"Untick shown unticks exactly the rows shown (" + WorldIslands.Chosen.Count + ")");
			Check(ref ok, Click(IslandPickerWindow.Window.gameObject, "Tick shown"), "Tick shown clicked");
			yield return null;
			Check(ref ok, WorldIslands.Chosen.Count == 0 && all.All(IslandPickerWindow.IsTicked), "Tick shown ticks them again");
			IslandPickerWindow.Search.text = ""; yield return null;
			Check(ref ok, IslandPickerWindow.Shown().Count == all.Count, "an empty search shows every row again");

			// The entries asked for, left out
			foreach (string w in want) { Button r = IslandPickerWindow.Row(w); if (r != null) { r.onClick.Invoke(); yield return null; } }
			var off = IslandPickerWindow.Window.gameObject.activeSelf ? OffScreen(IslandPickerWindow.Window.gameObject, Screen.width, Screen.height) : new List<string>();
			Check(ref ok, off.Count == 0, "the list fits the screen (" + Screen.width + "x" + Screen.height + ")" + (off.Count > 0 ? " - off: " + string.Join(", ", off.Take(4).ToArray()) : ""));
			Check(ref ok, Click(IslandPickerWindow.Window.gameObject, "Done"), "Done clicked");
			yield return null;
			Check(ref ok, !IslandPickerWindow.IsOpen && WorldIslands.Chosen.SetEquals(want) && WorldIslands.Pending != null && WorldIslands.Pending.SetEquals(want),
				"Done closes it; left out of the next world: " + (want.Count == 0 ? "none" : string.Join(", ", want.ToArray())) + " (the button: '" + UIKit.LabelOf(entry).text + "')");
			WorldSettingsWindow.Close();
			box.gameObject.SetActive(false);
			if (ok) Log("PASS: island picker box"); else Fail("island picker box");
		}

		[ConsoleCommand(name: "CIWorldIslandsCheck", docs: "Dev, world (host): the world leaves out exactly these entries of the spawn pool - the world file's line, the pool the spawner picks from (400 picks: never one left out, every other one possible), the WorldIslands command (-/+ an entry, saved at once), SpawnPool's text: CIWorldIslandsCheck [entry|entry...]")]
		public static void WorldIslandsCheckCommand(string[] args)
		{
			HashSet<string> want = EntriesArg(args);
			bool ok = true;
			Check(ref ok, LoadSceneManager.IsGameSceneLoaded && Raft_Network.IsHost, "in a world, as its host");
			Check(ref ok, WorldIslands.Off.SetEquals(want), "the world leaves out: " + WorldIslands.Describe() + " (wanted " + (want.Count == 0 ? "none" : string.Join(", ", want.ToArray())) + ")");
			List<string> all = WorldIslands.Candidates();
			List<string> pool = CustomIslandSpawner.Pool().Select(p => p.Key).ToList();
			Check(ref ok, pool.All(WorldIslands.TakesPart) && all.Where(WorldIslands.TakesPart).All(pool.Contains), "the spawner's pool is the whole pool less those (" + pool.Count + " of " + all.Count + ")");
			var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < 400; i++) { string p = CustomIslandSpawner.PickFromPool(); if (p != null) picked.Add(p); }
			Check(ref ok, !picked.Any(p => want.Contains(p)), "400 picks: none left out (" + picked.Count + " different ones picked)");

			// The world file
			IslandWorldState.Save();
			string file = IslandWorldState.WorldFilePath;
			string line = File.Exists(file) ? File.ReadAllLines(file).FirstOrDefault(l => l.StartsWith("@islandsoff=")) : null;
			Check(ref ok, want.Count == 0 ? line == null : line != null && WorldIslands.Parse(line.Substring("@islandsoff=".Length)).SetEquals(want), "the world file: " + (line ?? "(no @islandsoff line)"));

			// The command: leave one more out, then let it take part again
			string extra = all.FirstOrDefault(e => !want.Contains(e) && WorldIslands.IsIsland(e));
			if (extra != null)
			{
				WorldIslands.WorldIslandsCommand(new[] { "-" + extra });
				string l2 = File.ReadAllLines(file).FirstOrDefault(l => l.StartsWith("@islandsoff="));
				Check(ref ok, !WorldIslands.TakesPart(extra) && !CustomIslandSpawner.Pool().Any(p => p.Key.Equals(extra, StringComparison.OrdinalIgnoreCase)) && l2 != null && l2.Contains(extra),
					"WorldIslands -" + extra + ": left out, and saved");
				WorldIslands.WorldIslandsCommand(new[] { "+" + extra });
				Check(ref ok, WorldIslands.TakesPart(extra) && WorldIslands.Off.SetEquals(want), "WorldIslands +" + extra + ": takes part again");
			}
			Check(ref ok, CustomIslandSpawner.Describe().Contains("This world's choice"), "SpawnPool says what the world left out");
			Log("WORLDISLANDS " + WorldIslands.Describe());
			if (ok) Log("PASS: world islands check"); else Fail("world islands check");
		}

		[ConsoleCommand(name: "CIWorldIslandsLater", docs: "Dev (UW5), world (host): an island saved after the world was made joins its pool (it isn't in @islandsoff, the spawner picks it), and its file taken away later is left out quietly (also when the world had left it out)")]
		public static void WorldIslandsLaterCommand()
		{
			bool ok = true;
			Check(ref ok, LoadSceneManager.IsGameSceneLoaded && Raft_Network.IsHost, "in a world, as its host");
			if (!ok) { Fail("world islands later"); return; }
			string name = "ciuw5_later";
			string file = IslandSpawner.PathFor(name), backup = Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName);
			string from = CustomIslandSpawner.Pool().Select(p => p.Key).FirstOrDefault(k => WorldIslands.IsIsland(k) && File.Exists(IslandSpawner.PathFor(k)));
			Check(ref ok, from != null, "an island of the pool to copy: " + (from ?? "none"));
			if (from == null) { Fail("world islands later"); return; }
			HashSet<string> offWas = new HashSet<string>(WorldIslands.Off, StringComparer.OrdinalIgnoreCase);
			try
			{
				// Saved after the world was made: a file of its own (as the editor's Save writes it), nothing else told
				File.Copy(IslandSpawner.PathFor(from), file, true);
				Check(ref ok, WorldIslands.TakesPart(name) && !WorldIslands.Off.Contains(name), "'" + name + "' takes part in this world (not left out: " + WorldIslands.Describe() + ")");
				Check(ref ok, CustomIslandSpawner.Pool().Any(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)), "the spawner's pool has it (" + CustomIslandSpawner.Pool().Count + " entries)");
				bool picked = false;
				for (int i = 0; i < 4000 && !picked; i++) picked = name.Equals(CustomIslandSpawner.PickFromPool(), StringComparison.OrdinalIgnoreCase);
				Check(ref ok, picked, "the spawner picks it by chance (within 4000 picks)");
				IslandWorldState.Save();
				string line = File.ReadAllLines(IslandWorldState.WorldFilePath).FirstOrDefault(l => l.StartsWith("@islandsoff="));
				Check(ref ok, line == null || !WorldIslands.Parse(line.Substring("@islandsoff=".Length)).Contains(name), "the world file doesn't leave it out (" + (line ?? "no @islandsoff line") + ")");

				// Its file taken away (to the deleted folder): left out quietly, also while the world leaves it out by name
				WorldIslands.WorldIslandsCommand(new[] { "-" + name });
				Directory.CreateDirectory(backup);
				string moved = Path.Combine(backup, name + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + IslandFile.Extension);
				File.Move(file, moved);
				List<string> pool = null; string err = null;
				try { pool = CustomIslandSpawner.Pool().Select(p => p.Key).ToList(); for (int i = 0; i < 400; i++) if (name.Equals(CustomIslandSpawner.PickFromPool(), StringComparison.OrdinalIgnoreCase)) err = "picked"; WorldIslands.Describe(); CustomIslandSpawner.Describe(); IslandWorldState.Save(); }
				catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
				Check(ref ok, err == null && pool != null && !pool.Contains(name, StringComparer.OrdinalIgnoreCase), "its file gone: out of the pool, never picked, SpawnPool and the world's save go on (" + (err ?? "no error") + ")");
				WorldIslands.WorldIslandsCommand(new[] { "+" + name });
				Check(ref ok, !CustomIslandSpawner.Pool().Any(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)), "taken back in by name with no file: still not in the pool");
			}
			finally
			{
				if (File.Exists(file)) { Directory.CreateDirectory(backup); File.Move(file, Path.Combine(backup, name + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + IslandFile.Extension)); }
				if (!WorldIslands.Off.SetEquals(offWas)) { foreach (string o in WorldIslands.Off.Except(offWas).ToList()) WorldIslands.WorldIslandsCommand(new[] { "+" + o }); }
			}
			if (ok) Log("PASS: world islands later"); else Fail("world islands later");
		}

		[ConsoleCommand(name: "CIWorldIslandsTurns", docs: "Dev (UW4), Choose islands at its extremes: CIWorldIslandsTurns choose none|one <island>|generated (main menu: the next world's list set so, the World settings button and text checked); CIWorldIslandsTurns none|one <island>|generated (world, host: the pool, a random island's turns and their log lines)")]
		public static void WorldIslandsTurnsCommand(string[] args)
		{
			string mode = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (mode == "choose") { StartTest(ChooseExtreme(args.Skip(1).ToArray())); return; }
			StartTest(WorldIslandsTurns(mode, args != null ? string.Join(" ", args.Skip(1).ToArray()).Trim() : ""));
		}

		static IEnumerator ChooseExtreme(string[] rest)
		{
			bool ok = true;
			string what = rest.Length > 0 ? rest[0].ToLowerInvariant() : "";
			string island = string.Join(" ", rest.Skip(1).ToArray()).Trim();
			Check(ref ok, !LoadSceneManager.IsGameSceneLoaded, "at the main menu");
			List<string> all = WorldIslands.Candidates();
			string keep = what == "generated" ? CustomIslandSpawner.GeneratedEntry : what == "one" ? all.FirstOrDefault(c => c.Equals(island, StringComparison.OrdinalIgnoreCase)) : null;
			Check(ref ok, what == "none" || keep != null, "'" + (what == "one" ? island : what) + "' is in the spawn pool (" + all.Count + " entries)");
			if (!ok) { Fail("world islands choose"); yield break; }
			// (the World settings window is made the first time the New Game box opens: open it as a player does)
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(x => x.gameObject.scene.IsValid());
			if (box != null) { box.gameObject.SetActive(true); try { box.Close(); } catch { } box.Open(); yield return new WaitForSecondsRealtime(1f); }
			WorldSettingsWindow.Open();
			yield return null;
			WorldIslands.Chosen.Clear();
			foreach (string c in all) if (!c.Equals(keep, StringComparison.OrdinalIgnoreCase)) WorldIslands.Chosen.Add(c);
			WorldIslands.SaveDefaults(WorldIslands.Chosen);
			try { WorldSettingsWindow.Show(); } catch { }
			IslandPickerWindow.ShowEntry();
			yield return null;
			Button b = IslandPickerWindow.EntryButton;
			string label = b != null ? UIKit.LabelOf(b).text : "(no button)";
			Check(ref ok, b != null && (what == "none" ? label.EndsWith("none") : label.EndsWith("1 of " + all.Count)), "the World settings button: '" + label + "'");
			Log("CHOOSE " + what + ": " + (all.Count - WorldIslands.Chosen.Count) + " of " + all.Count + " ticked");
			WorldSettingsWindow.Close();
			if (box != null) { try { box.Close(); } catch { } }
			if (ok) Log("PASS: world islands choose"); else Fail("world islands choose");
		}

		static IEnumerator WorldIslandsTurns(string mode, string island)
		{
			bool ok = true;
			Check(ref ok, LoadSceneManager.IsGameSceneLoaded && Raft_Network.IsHost, "in a world, as its host");
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			Check(ref ok, raft.HasValue, "the raft is found");
			if (!ok) { Fail("world islands turns"); yield break; }
			var lines = new List<string>();
			Application.LogCallback grab = (text, trace, type) => { if (text.Contains("Auto spawn skipped") || text.Contains("No random custom island can come")) lines.Add(text); };
			List<string> pool = CustomIslandSpawner.Pool().Select(p => p.Key).ToList();
			if (mode == "none")
			{
				Check(ref ok, pool.Count == 0, "nothing ticked: the world's pool is empty (" + pool.Count + ")");
				Check(ref ok, CustomIslandSpawner.PickFromPool() == null, "no pick");
			}
			else if (mode == "generated")
			{
				Check(ref ok, pool.Count == 1 && pool[0] == CustomIslandSpawner.GeneratedEntry, "only new each time: the pool is just that (" + string.Join(", ", pool.ToArray()) + ")");
				int gen = 0;
				for (int i = 0; i < 50; i++) if (CustomIslandSpawner.PickFromPool() == CustomIslandSpawner.GeneratedEntry) gen++;
				Check(ref ok, gen == 50, "50 picks, all a brand-new island (" + gen + ")");
			}
			else if (mode == "one")
			{
				Check(ref ok, pool.Count == 1 && pool[0].Equals(island, StringComparison.OrdinalIgnoreCase), "one ticked: the pool is just '" + island + "' (" + string.Join(", ", pool.ToArray()) + ")");
				// Its turn: it comes (a few tries for a free spot)
				string said = null;
				for (int i = 0; i < 6 && ok; i++)
				{
					said = CustomIslandSpawner.TakeTurn(raft.Value);
					if (CustomIslandSpawner.NotAgain(island)) break;
					yield return new WaitForSeconds(2f);
				}
				Check(ref ok, CustomIslandSpawner.NotAgain(island), "its turn: it comes (" + said + ")");
				pool = CustomIslandSpawner.Pool().Select(p => p.Key).ToList();
				Check(ref ok, pool.Count == 0, "while it's here the pool is empty: it doesn't come twice (" + pool.Count + ")");
			}
			else { Fail("CIWorldIslandsTurns none|one <island>|generated, or choose ..."); yield break; }
			if (mode != "generated")
			{
				// Turns with nothing to pick: said once, then quiet (it used to log every 10 s of sailing)
				CustomIslandSpawner.saidEmpty = false;
				int islandsBefore = IslandWorldState.Islands.Count;
				Application.logMessageReceived += grab;
				try { for (int i = 0; i < 8; i++) CustomIslandSpawner.TakeTurn(raft.Value); }
				finally { Application.logMessageReceived -= grab; }
				Check(ref ok, lines.Count == 1 && lines[0].Contains("No random custom island can come"), "8 turns with nothing to pick: one log line (" + lines.Count + ": " + string.Join(" / ", lines.ToArray()) + ")");
				Check(ref ok, IslandWorldState.Islands.Count == islandsBefore, "no island placed by them");
			}
			Log("TURNS " + mode + ": pool " + string.Join(", ", pool.ToArray()));
			if (ok) Log("PASS: world islands turns"); else Fail("world islands turns");
		}
	}
}

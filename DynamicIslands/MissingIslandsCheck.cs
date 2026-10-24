using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// When the host (or a single player) loads a world: every island file the world uses is checked - its island lines and
	/// the islands its plan's rules still have to bring. When some aren't on this PC a box names them (and the library entry
	/// each came with) and asks: play on without them (they are left out, kept in the world file, and come back when the
	/// file is here again - a player who has it may bring it) or go back to the main menu (no save) to download or restore
	/// them. Players who join never see it: they get a missing file from the host, as before (IslandNetwork).
	/// </summary>
	public static class MissingIslandsCheck
	{
		public const string Title = "Islands missing", ContinueLabel = "Play without them", MenuLabel = "Main menu";

		public class Missing
		{
			public string Name;
			/// <summary>"library:id@v" / "import:..." when an installed entry brought it (or the world's plan came with one), else null.</summary>
			public string Source;
			/// <summary>A rule of the world's plan brings it (not yet on the sea).</summary>
			public bool ByPlan;
			public IslandWorldState.Entry Entry;
		}

		/// <summary>The last check's islands (tests).</summary>
		public static readonly List<Missing> Last = new List<Missing>();
		/// <summary>Tests: the box isn't shown by itself (the list is still made).</summary>
		public static bool Quiet;
		static bool waiting, cursorFreed;
		/// <summary>Tests: the box had the cursor free (it was free already, or the box freed it).</summary>
		public static bool CursorFree;
		static float showAt;

		/// <summary>From IslandWorldState.OnWorldLoaded on the host, after the world's file and plan are read.</summary>
		internal static void OnWorldRead(IList<IslandWorldState.Entry> islands)
		{
			Last.Clear();
			waiting = false;
			if (!Raft_Network.IsHost) return;
			try
			{
				if (string.Equals(SaveAndLoad.CurrentGameFileName, IslandTest.WorldName, StringComparison.OrdinalIgnoreCase)) return;
				Last.AddRange(Find(islands));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Checking the world's island files: " + e.Message); return; }
			if (Last.Count == 0) return;
			Debug.LogWarning("[CUSTOM ISLANDS] This world's islands missing on this PC: " + string.Join(", ", Last.Select(m => m.Name).ToArray()));
			// (left out until the player answers - and after "Play without them")
			foreach (Missing m in Last) if (m.Entry != null) m.Entry.Excluded = true;
			if (Quiet) return;
			waiting = true;
			showAt = Time.unscaledTime + 2f;
		}

		/// <summary>The world's islands without a file here: island lines (not generated ones - they are made again - nor the
		/// randomizer's extras) and the islands of the plan's rules still to come (a "one of" only when none of its islands is here).</summary>
		internal static List<Missing> Find(IList<IslandWorldState.Entry> islands)
		{
			var result = new List<Missing>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (IslandWorldState.Entry e in islands)
			{
				if (e == null || WorldRandomizer.IsExtras(e) || File.Exists(IslandSpawner.PathFor(e.Name))) continue;
				string name = e.HostName ?? e.Name;
				if (CustomIslandSpawner.RemakeOf(name) != null || CustomIslandSpawner.RemakeOf(e.Name) != null) continue;
				if (seen.Add(name)) result.Add(new Missing { Name = name, Source = SourceOf(name, !string.IsNullOrEmpty(e.Rule)), Entry = e });
			}
			WorldPlan plan = WorldDirector.Plan;
			if (plan != null)
				foreach (IntroRule r in plan.Rules)
				{
					if (r == null || WorldDirector.Done.Contains(r.Id)) continue;
					List<string> names = r.What == "island" ? new List<string> { r.WhatArg.Trim() }
						: r.What == "oneof" ? r.WhatArg.Split(',').Select(n => n.Trim()).ToList() : null;
					if (names == null) continue;
					names = names.Where(n => n.Length > 0 && CustomIslandSpawner.RemakeOf(n) == null).ToList();
					if (names.Count == 0 || names.Any(n => WorldDirector.FileFor(n) != null)) continue;
					foreach (string n in names)
						if (seen.Add(n)) result.Add(new Missing { Name = n, Source = SourceOf(n, true), ByPlan = true });
				}
			return result;
		}

		/// <summary>The installed library (or imported) entry that brought the island, by its file name; else, for an island of
		/// the world's plan, the entry the plan came with.</summary>
		static string SourceOf(string name, bool byPlan)
		{
			try
			{
				LibraryInstalled owner = LibraryPack.Installed().FirstOrDefault(x => x.files.Any(f => f.kind == LibraryPack.KindIsland &&
					(name.Equals(f.name, StringComparison.OrdinalIgnoreCase) || name.Equals(f.original, StringComparison.OrdinalIgnoreCase))));
				if (owner != null) return owner.Source;
			}
			catch { }
			string from = WorldDirector.PlanFrom ?? "";
			return byPlan && from.Contains(":") ? from : null;
		}

		/// <summary>The box's text (tests read it too).</summary>
		internal static string Text(List<Missing> missing)
		{
			var lines = new List<string> { "This world uses island files that aren't on this PC:", "" };
			foreach (Missing m in missing)
			{
				string where = m.Source == null ? "" : m.Source.StartsWith("library:") ? " - from the island library: '" + LibrarySource.Describe(m.Source) + "'"
					: " - it came with '" + LibrarySource.Describe(m.Source) + "' (import it again)";
				lines.Add("  - " + m.Name + (m.ByPlan ? " (the world's plan brings it)" : "") + where);
			}
			lines.Add("");
			lines.Add(ContinueLabel + ": the rest of the world plays. These islands are left out but kept in the world, so they come back once their file is here (in multiplayer a player who has one can bring it).");
			lines.Add(MenuLabel + ": leave without saving, then download them in the island library" + " or put their files back in Mods\\DynamicIslands, and load the world again.");
			return string.Join("\n", lines.ToArray());
		}

		/// <summary>Every frame: shows the box once the world is on the screen.</summary>
		internal static void Tick()
		{
			if (!waiting || Time.unscaledTime < showAt) return;
			if (!LoadSceneManager.IsGameSceneLoaded) { waiting = false; return; }
			if (RAPI.GetLocalPlayer() == null) return;
			waiting = false;
			Show();
		}

		internal static void Show()
		{
			InfoWindow.Open(Title, Text(Last),
				new InfoWindow.Choice(ContinueLabel, PlayWithout, "Play on; the missing islands are left out", true),
				new InfoWindow.Choice(MenuLabel, ToMenu, "Leave without saving, to download or restore them"));
			InfoWindow.Closed = OnClosed;
			cursorFreed = !Cursor.visible;
			CursorFree = !cursorFreed;
			if (cursorFreed) try { RAPI.ToggleCursor(true); CursorFree = true; } catch { }
		}

		static void OnClosed()
		{
			if (cursorFreed) try { RAPI.ToggleCursor(false); } catch { }
			cursorFreed = false;
		}

		internal static void PlayWithout()
		{
			Debug.Log("[CUSTOM ISLANDS] Playing on without the missing islands");
			InfoWindow.Close();
		}

		internal static void ToMenu()
		{
			InfoWindow.Close();
			Debug.Log("[CUSTOM ISLANDS] Back to the main menu (islands missing), without saving");
			Raft_Network net = ComponentManager<Raft_Network>.Value;
			if (net != null) net.LeaveGame(DisconnectReason.SelfDisconnected, (SceneName)1, false, false);
			else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
		}
	}
}

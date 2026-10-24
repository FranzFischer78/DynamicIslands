using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// The world's missing islands box (MissingIslandsCheck), the generator's culture-free object list (CA19) and the kept RML
	/// cache (CW2).
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIGenNames", docs: "Dev, anywhere (the catalog built: the editor opened once): ROADMAP CA19 - the generator's object list is in ordinal order whatever the PC's language (tr-TR, th-TH, sv-SE), holds no buildables (other mods' items), and has every core object its styles pick from")]
		public static void GenNamesCommand()
		{
			if (!PlaceableCatalog.IsBuilt) { Fail("the catalog isn't built - open the editor first"); return; }
			bool ok = true;
			CultureInfo was = Thread.CurrentThread.CurrentCulture;
			List<string> plain = PlaceableCatalog.GenNames.ToList();
			try
			{
				foreach (string c in new[] { "tr-TR", "th-TH", "sv-SE", "de-DE" })
				{
					Thread.CurrentThread.CurrentCulture = new CultureInfo(c);
					List<string> other = PlaceableCatalog.GenNames.ToList();
					if (!other.SequenceEqual(plain)) { ok = false; Fail("under " + c + " the list's order differs"); }
					// (what the culture's order did: the list as it was sorted before)
					List<string> cultureOrder = plain.OrderBy(n => n).ToList();
					Log(c + ": the culture's own order " + (cultureOrder.SequenceEqual(plain) ? "is the same" : "differs at " + cultureOrder.Zip(plain, (a, b) => a == b).TakeWhile(x => x).Count()));
				}
			}
			finally { Thread.CurrentThread.CurrentCulture = was; }
			List<string> ordinal = plain.OrderBy(n => n, StringComparer.Ordinal).ToList();
			if (!ordinal.SequenceEqual(plain)) { ok = false; Fail("the list isn't in ordinal order"); }
			string[] buildables = plain.Where(n => { string cat = PlaceableCatalog.CategoryOf(n); return cat == PlaceableCatalog.BuildablesCategory || cat == PlaceableCatalog.RaftBlocksCategory; }).ToArray();
			if (buildables.Length > 0) { ok = false; Fail("buildables in the list: " + string.Join(", ", buildables.Take(5).ToArray())); }
			// (every core object a style's pools pick is still there: islands on a PC without placeables.txt keep their kinds)
			var gen = new HashSet<string>(plain);
			string[] lost = PlaceableCatalog.CoreNames.Where(n => IslandGenerator.InAnyPool(n) && !gen.Contains(n)).ToArray();
			if (lost.Length > 0) { ok = false; Fail("core objects the styles pick that the list lost: " + string.Join(", ", lost.Take(8).ToArray())); }
			int picked = plain.Count(IslandGenerator.InAnyPool);
			Log((ok ? "PASS" : "FAIL") + ": the generator's list has " + plain.Count + " names (" + picked + " picked by the styles), ordinal under every culture; core has " + PlaceableCatalog.CoreNames.Count());
		}

		const string MissingTestIsland = "ci_missing_load";

		[ConsoleCommand(name: "CIMissingIslands", docs: "Dev, world (host, a CI world): the missing islands box - an island line without a file is named, a library one says so, a generated one isn't; Play without them leaves it out but keeps it in the world file; CIMissingIslands menu: Main menu leaves without saving")]
		public static void MissingIslandsCommand(string[] args)
		{
			StartTest(MissingIslandsRoutine(args != null && args.Length > 0 && args[0] == "menu"));
		}

		static IEnumerator MissingIslandsRoutine(bool menu)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("load a CI world as host first"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("only in a \"CI ...\" world"); yield break; }
			bool ok = true;
			if (File.Exists(IslandSpawner.PathFor(MissingTestIsland))) { Fail(MissingTestIsland + " exists - it must be missing"); yield break; }
			Vector3 near = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			IslandWorldState.Remove(MissingTestIsland);
			IslandWorldState.Add(MissingTestIsland, near + new Vector3(60f, 0f, 60f), null, false);
			// (a generated island without its file is made again, so it isn't named)
			IslandWorldState.Add("gen-tropical-919191", near + new Vector3(-400f, 0f, 400f), null, false);
			IslandWorldState.Save();
			InfoWindow.Close();
			IslandWorldState.OnWorldLoaded();
			List<string> names = MissingIslandsCheck.Last.Select(m => m.Name).ToList();
			Log("missing: " + string.Join(", ", names.ToArray()));
			if (!names.Contains(MissingTestIsland)) { ok = false; Fail("the missing island isn't named"); }
			if (names.Any(n => n.StartsWith("gen-"))) { ok = false; Fail("a generated island is named"); }
			// (the box comes once the world is on the screen)
			float until = Time.unscaledTime + 15f;
			while (!InfoWindow.IsOpen && Time.unscaledTime < until) yield return null;
			if (!InfoWindow.IsOpen || InfoWindow.Title != MissingIslandsCheck.Title.ToUpperInvariant()) { Fail("no missing islands box"); yield break; }
			if (!InfoWindow.Body.Contains(MissingTestIsland)) { ok = false; Fail("the box doesn't name it"); }
			List<string> labels = InfoWindow.ButtonLabels;
			if (!labels.Contains(MissingIslandsCheck.ContinueLabel) || !labels.Contains(MissingIslandsCheck.MenuLabel)) { ok = false; Fail("buttons: " + string.Join(", ", labels.ToArray())); }
			// (a minimised test Raft has no focus, so Unity's Cursor.visible can read false there: the box's own record is checked)
			if (!MissingIslandsCheck.CursorFree) { ok = false; Fail("no cursor for the box"); }
			Log("cursor: freed by the box " + MissingIslandsCheck.CursorFree + ", visible " + Cursor.visible + ", lock " + Cursor.lockState + ", focused " + Application.isFocused);
			// (a library island's line says where it came from)
			string text = MissingIslandsCheck.Text(new List<MissingIslandsCheck.Missing> { new MissingIslandsCheck.Missing { Name = "Some camp", Source = "library:no-such-entry@2" } });
			if (!text.Contains("from the island library: 'no-such-entry'")) { ok = false; Fail("a library island's line: " + text.Split('\n')[2]); }
			Screenshot(new[] { "missing_islands" });
			yield return new WaitForSecondsRealtime(1f);
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == MissingTestIsland || x.Name == MissingTestIsland);
			if (menu)
			{
				InfoWindow.ButtonNamed(MissingIslandsCheck.MenuLabel).onClick.Invoke();
				until = Time.unscaledTime + 60f;
				while (LoadSceneManager.IsGameSceneLoaded && Time.unscaledTime < until) yield return null;
				if (LoadSceneManager.IsGameSceneLoaded) { ok = false; Fail("still in the world"); }
				Log((ok ? "PASS" : "FAIL") + ": Main menu left the world");
				yield break;
			}
			InfoWindow.ButtonNamed(MissingIslandsCheck.ContinueLabel).onClick.Invoke();
			yield return new WaitForSecondsRealtime(3f);
			if (InfoWindow.IsOpen) { ok = false; Fail("the box is still open"); }
			if (e == null || !e.Excluded) { ok = false; Fail("the island isn't left out"); }
			if (e != null && (e.Failed || e.Loading || e.Root != null)) { ok = false; Fail("the island was tried: failed " + e.Failed + ", loading " + e.Loading); }
			IslandWorldState.Save();
			string[] lines = File.ReadAllLines(IslandWorldState.WorldFilePath);
			if (!lines.Any(l => l.StartsWith(MissingTestIsland + "|"))) { ok = false; Fail("the island's line is gone from the world file"); }
			// (tidy: the test lines go)
			IslandWorldState.Remove(MissingTestIsland);
			IslandWorldState.Remove("gen-tropical-919191");
			IslandWorldState.Save();
			Log((ok ? "PASS" : "FAIL") + ": the missing islands box - named, left out, kept in the world file");
		}

		[ConsoleCommand(name: "CIRmlNote", docs: "Dev, main menu: shows the once-only note of a start that compiled (CW2) and takes CIShot rml_note")]
		public static void RmlNoteCommand()
		{
			StartTest(RmlNoteRoutine());
		}

		static IEnumerator RmlNoteRoutine()
		{
			DynamicIslands.Notify(RmlCache.Note, false, 15);
			yield return new WaitForSecondsRealtime(1.5f);
			Screenshot(new[] { "rml_note" });
			yield return new WaitForSecondsRealtime(1f);
			Log("PASS: the compile note shown (shot_rml_note.png)");
		}

		[ConsoleCommand(name: "CIRmlCache", docs: "Dev, anywhere (30 s after the start): ROADMAP CW2 - this mod's RML cache file is kept read-only (RML's start deletes it otherwise), there is one, and whether this start compiled")]
		public static void RmlCacheCommand()
		{
			string folder = RmlCache.Folder;
			FileInfo[] ours = Directory.Exists(folder) ? new DirectoryInfo(folder).GetFiles("*.cachedmod").Where(f => f.Name.Contains(RmlCache.Key)).ToArray() : new FileInfo[0];
			bool ok = ours.Length == 1 && (ours[0].Attributes & FileAttributes.ReadOnly) != 0 && RmlCache.Compiled != null;
			Log((ok ? "PASS" : "FAIL") + ": RML cache in " + folder + ": " + string.Join(", ", ours.Select(f => f.Name + ((f.Attributes & FileAttributes.ReadOnly) != 0 ? " (read-only)" : "")).ToArray()) +
				"; this start " + (RmlCache.Compiled == true ? "compiled" : RmlCache.Compiled == false ? "loaded the cache" : "unknown") + ", mod started " + RmlCache.StartedAt.ToString("0.0") + " s after Raft");
		}
	}
}

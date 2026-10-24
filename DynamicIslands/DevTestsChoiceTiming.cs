using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using DynamicIslands.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>"+ Add a saved island" and the other island lists: they open at once and load (IslandSummaries).</summary>
	public static partial class DevTests
	{
		const string TimingPrefix = "citiming-";
		const int TimingCount = 50;

		[ConsoleCommand(name: "CIChoiceTiming", docs: "Dev, editor: World plans > New main story... > + Add a saved island with 50 more islands (citiming-*): the old way (every whole file read for its title) timed against the new (the window opens at once saying 'Loading islands... (n of N)', the button greyed and a second click doing nothing, then the list with the titles); a second time from summarycache.txt reads no island file; a saved, renamed and deleted island is read again or gone; a broken summarycache.txt is made again. Makes and removes the citiming-* islands")]
		public static void ChoiceTimingCommand() { StartTest(ChoiceTimingRoutine()); }

		static string TimingName(int i) { return TimingPrefix + i.ToString("00"); }
		static string TimingTitle(int i) { return "Timing island " + i.ToString("00"); }

		static void SaveTimingIsland(string name, string title)
		{
			const int res = 513; // (the editor's own heightmap: a file as big as a real island's)
			var f = new IslandFile { Name = name, TerrainSize = new Vector3(1000, 600, 1000), HeightmapResolution = res, Heights = new float[res, res], WaterLevel = 60f };
			for (int y = 0; y < res; y++)
				for (int x = 0; x < res; x++)
					f.Heights[y, x] = 0.08f + 0.04f * Mathf.Sin(x * 0.05f) * Mathf.Cos(y * 0.07f) + 0.001f * ((x * 7 + y * 13) % 11);
			for (int k = 0; k < 20; k++) f.Objects.Add(new IslandObject { Name = "Log", Position = new Vector3(400 + k, 62, 500), Scale = Vector3.one });
			f.Props[IslandProps.Title] = title;
			f.Save(IslandSpawner.PathFor(name));
		}

		static void RemoveTimingIslands()
		{
			foreach (string file in Directory.GetFiles(DynamicIslands.assetpath, TimingPrefix + "*" + IslandFile.Extension))
				try { File.Delete(file); IslandCache.ForgetFile(file); } catch (Exception e) { Log("Could not remove " + file + ": " + e.Message); }
			string deleted = Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName);
			if (Directory.Exists(deleted))
				foreach (string file in Directory.GetFiles(deleted, TimingPrefix + "*" + IslandFile.Extension))
					try { File.Delete(file); } catch { }
		}

		/// <summary>Clicks + Add a saved island and waits for the list (realtime). Returns ms until the click returned and until the list was there.</summary>
		static IEnumerator OpenAddList(Button add, float[] ms, bool[] sawLoading)
		{
			var clock = System.Diagnostics.Stopwatch.StartNew();
			add.onClick.Invoke();
			ms[0] = clock.ElapsedMilliseconds;
			sawLoading[0] = ChoiceWindow.Loading;
			for (float until = Time.realtimeSinceStartup + 120f; ChoiceWindow.Loading && Time.realtimeSinceStartup < until; ) yield return null;
			ms[1] = clock.ElapsedMilliseconds;
		}

		static IEnumerator ChoiceTimingRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("choice timing: open the editor first"); yield break; }
			bool ok = true;
			RemoveTimingIslands();
			for (int i = 0; i < TimingCount; i++)
			{
				SaveTimingIsland(TimingName(i), TimingTitle(i));
				if (i % 10 == 9) yield return null;
			}
			IslandCache.Forget();
			int all = IslandSpawner.ListSavedIslands().Count(n => !IslandNetwork.IsDownloadName(n) && !n.StartsWith(CustomIslandSpawner.GeneratedPrefix, StringComparison.OrdinalIgnoreCase));
			Log(TimingCount + " islands made; " + all + " saved islands in the list");

			// Before: what the list did - every island's whole file read for its title
			var clock = System.Diagnostics.Stopwatch.StartNew();
			int titled = IslandSpawner.ListSavedIslands().Where(n => !IslandNetwork.IsDownloadName(n) && !n.StartsWith(CustomIslandSpawner.GeneratedPrefix, StringComparison.OrdinalIgnoreCase))
				.Select(n => ObjectProps.Get(IslandCache.Props(n), IslandProps.Title)).Count(t => t.Length > 0);
			long before = clock.ElapsedMilliseconds;
			Log("Before: reading every file for the titles took " + before + " ms (" + titled + " with a title)");
			yield return null;

			WorldPlanWindow.RecipeNew("CI choice timing");
			yield return null;
			MainStoryHelper.Open();
			yield return null;
			Button add = EditorUI.Canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Helper_Add");
			if (add == null || !MainStoryHelper.IsOpen) { Fail("choice timing: no + Add a saved island button"); RemoveTimingIslands(); yield break; }

			// After, the test islands not in the cache yet (just saved): the window at once, loading
			var ms = new float[2];
			var saw = new bool[1];
			int readBefore = IslandSummaries.FilesRead;
			var clicking = System.Diagnostics.Stopwatch.StartNew();
			add.onClick.Invoke();
			ms[0] = clicking.ElapsedMilliseconds;
			saw[0] = ChoiceWindow.Loading;
			string loadingText = ChoiceWindow.LoadingText;
			bool greyed = !add.interactable;
			string title = ChoiceWindow.Title;
			add.onClick.Invoke(); // (a second click while it loads: nothing)
			bool secondIgnored = ChoiceWindow.Loading && ChoiceWindow.Title == title;
			yield return null;
			string counting = ChoiceWindow.LoadingText;
			for (float until = Time.realtimeSinceStartup + 120f; ChoiceWindow.Loading && Time.realtimeSinceStartup < until; ) yield return null;
			ms[1] = clicking.ElapsedMilliseconds;
			Log("After, new islands: the window opened in " + ms[0] + " ms, the list was there after " + ms[1] + " ms (" + (IslandSummaries.FilesRead - readBefore) + " files read on the worker)");
			Check(ref ok, ChoiceWindow.IsOpen && ms[0] < 250f, "the window opens at once (" + ms[0] + " ms)");
			Check(ref ok, saw[0] && loadingText.StartsWith("Loading islands...") && loadingText.Contains(" of " + all + ")"), "it says it is loading: '" + loadingText + "', then '" + counting + "'");
			Check(ref ok, greyed && secondIgnored, "the button is greyed while it loads and a second click does nothing");
			Check(ref ok, !ChoiceWindow.Loading && add.interactable, "loaded: the button is back");
			List<ChoiceWindow.Choice> shown = ChoiceWindow.Shown;
			Check(ref ok, Enumerable.Range(0, TimingCount).All(i => shown.Any(c => c.Value == TimingName(i) && c.Detail == TimingTitle(i))), "the list has the 50 islands with their titles (" + shown.Count + " rows)");
			Check(ref ok, IslandSummaries.FilesRead - readBefore >= TimingCount, "the new islands were read (" + (IslandSummaries.FilesRead - readBefore) + ")");
			ChoiceWindow.Close();
			yield return null;

			// After, from summarycache.txt (as in the next session): no island file read
			IslandSummaries.Unload();
			readBefore = IslandSummaries.FilesRead;
			yield return OpenAddList(add, ms, saw);
			Log("After, from the cache: the window opened in " + ms[0] + " ms, the list was there after " + ms[1] + " ms");
			Check(ref ok, IslandSummaries.FilesRead == readBefore, "no island file read the second time (" + (IslandSummaries.FilesRead - readBefore) + ")");
			Check(ref ok, ms[1] < Math.Max(before, 50L), "from the cache (" + ms[1] + " ms) faster than reading every file (" + before + " ms)");
			Check(ref ok, ChoiceWindow.Shown.Any(c => c.Value == TimingName(7) && c.Detail == TimingTitle(7)), "titles from the cache");
			string cache = Path.Combine(DynamicIslands.assetpath, IslandSummaries.FileName);
			Check(ref ok, File.Exists(cache) && File.ReadAllLines(cache).Count(l => l.StartsWith(TimingPrefix)) == TimingCount, "summarycache.txt has the 50 islands");
			ChoiceWindow.Close();
			yield return null;

			// Saved again, renamed, deleted: read again or gone
			SaveTimingIsland(TimingName(1), "Saved again");
			try { IslandRename.Rename(TimingName(2), TimingPrefix + "renamed"); } catch (Exception e) { Check(ref ok, false, "rename: " + e.Message); }
			try { IslandFilesWindow.MoveToDeleted(TimingName(3)); } catch (Exception e) { Check(ref ok, false, "delete: " + e.Message); }
			readBefore = IslandSummaries.FilesRead;
			yield return OpenAddList(add, ms, saw);
			shown = ChoiceWindow.Shown;
			Check(ref ok, shown.Any(c => c.Value == TimingName(1) && c.Detail == "Saved again"), "a saved island shows its new title");
			Check(ref ok, shown.Any(c => c.Value == TimingPrefix + "renamed" && c.Detail == TimingTitle(2)) && !shown.Any(c => c.Value == TimingName(2)), "a renamed island is listed under its new name");
			Check(ref ok, !shown.Any(c => c.Value == TimingName(3)), "a deleted island is gone");
			Check(ref ok, IslandSummaries.FilesRead - readBefore == 2, "only the saved and the renamed island were read (" + (IslandSummaries.FilesRead - readBefore) + ")");
			ChoiceWindow.Close();
			yield return null;

			// A broken summarycache.txt: made again
			File.WriteAllText(cache, "not a summary cache\n\u0001\u0002 garbage\tx\ty\n");
			IslandSummaries.Unload();
			yield return OpenAddList(add, ms, saw);
			Check(ref ok, ChoiceWindow.Shown.Any(c => c.Value == TimingName(9) && c.Detail == TimingTitle(9)), "with a broken cache the list still has the titles");
			string[] lines = File.Exists(cache) ? File.ReadAllLines(cache) : new string[0];
			Check(ref ok, lines.Length > TimingCount && lines[0].StartsWith("# Custom Islands island summaries"), "the broken cache was made again (" + lines.Length + " lines)");
			ChoiceWindow.Close();

			MainStoryHelper.Close();
			WorldPlanWindow.Close();
			RemoveTimingIslands();
			IslandCache.Forget();
			if (ok) Log("PASS: choice timing (before " + before + " ms, after: opens in " + ms[0] + " ms)"); else Fail("choice timing");
		}
	}
}

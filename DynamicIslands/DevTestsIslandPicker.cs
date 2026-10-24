using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// The "Islands while sailing" list's rows (IslandPickerWindow): picture, description, QUEST / no quest badge, A to Z,
	/// Tick all / Untick all, the search, and the choice kept after Done.
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIIslandPickerRows", docs: "Dev, main menu: World settings > CHOOSE ISLANDS... - the rows show a picture, the description and the right quest badge (Rookery Cliffs: no quest; an island with a quest: QUEST), islands A to Z, the window smaller than the island library, search + a row's tick + Tick shown, Tick all / Untick all, and Done keeps the choice (reopened, it shows). Screenshots picker_top, picker_quest, picker_search, picker_alloff. Leaves every island ticked")]
		public static void IslandPickerRowsCommand(string[] args) { StartTest(IslandPickerRowsRoutine()); }

		static IEnumerator IslandPickerRowsRoutine()
		{
			const string noQuest = "Rookery Cliffs";
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("picker rows: no New Game box (main menu?)"); yield break; }
			bool ok = true;
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open();
			yield return new WaitForSecondsRealtime(1f);
			WorldSettingsWindow.Open();
			yield return null;
			Button entry = IslandPickerWindow.EntryButton;
			if (entry == null) { Fail("picker rows: no CHOOSE ISLANDS button"); yield break; }
			List<string> all = WorldIslands.Candidates();
			Check(ref ok, all.Contains(noQuest, StringComparer.OrdinalIgnoreCase), "'" + noQuest + "' is in the list (library island, installed)");
			// The quest island: the first in the list whose file has a quest (read here, not from the cache)
			string quest = null;
			foreach (string e in all.Where(WorldIslands.IsIsland).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).Take(80))
			{
				try { if (IslandQuest.HasQuest(IslandFile.Load(IslandSpawner.PathFor(e)).Props)) { quest = e; break; } } catch { }
			}
			Check(ref ok, quest != null, "an island with a quest is in the list: " + quest);
			bool rookeryHasQuest = true;
			string rookeryDesc = "";
			try { var props = IslandFile.Load(IslandSpawner.PathFor(noQuest)).Props; rookeryHasQuest = IslandQuest.HasQuest(props); rookeryDesc = ObjectProps.Get(props, IslandProps.Description).Trim(); } catch { }
			Check(ref ok, !rookeryHasQuest, "'" + noQuest + "' has no quest in its file");

			WorldIslands.Chosen.Clear();
			entry.onClick.Invoke();
			yield return null;
			Check(ref ok, IslandPickerWindow.IsOpen, "the button opens the list");
			float until = Time.realtimeSinceStartup + 90f;
			while (!IslandPickerWindow.Loaded && Time.realtimeSinceStartup < until) yield return null;
			Check(ref ok, IslandPickerWindow.Loaded, "the descriptions and badges are filled in");

			// Compact: smaller than the island library (1240 x 720)
			Canvas.ForceUpdateCanvases();
			RectTransform panel = (RectTransform)IslandPickerWindow.Window.Find("Panel");
			Check(ref ok, panel != null && panel.rect.width <= 720f && panel.rect.height <= 680f, "the window is compact: " + (panel != null ? panel.rect.width.ToString("0") + " x " + panel.rect.height.ToString("0") : "no panel"));
			var off = OffScreen(IslandPickerWindow.Window.gameObject, Screen.width, Screen.height);
			Check(ref ok, off.Count == 0, "the list fits the screen" + (off.Count > 0 ? " - off: " + string.Join(", ", off.Take(4).ToArray()) : ""));

			// A to Z: the island rows in the list's order
			List<string> islandOrder = all.Where(WorldIslands.IsIsland).Select(e => new { e, b = IslandPickerWindow.Row(e) }).Where(x => x.b != null)
				.OrderBy(x => x.b.transform.GetSiblingIndex()).Select(x => x.e).ToList();
			List<string> sorted = islandOrder.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();
			Check(ref ok, islandOrder.Count > 0 && islandOrder.SequenceEqual(sorted), "the islands are A to Z (" + string.Join(", ", islandOrder.Take(4).ToArray()) + " ...)");
			List<string> kinds = all.Where(e => !WorldIslands.IsIsland(e)).ToList();

			// Badges, descriptions
			Check(ref ok, IslandPickerWindow.BadgeOf(noQuest) == "no quest", "'" + noQuest + "' shows 'no quest' (" + IslandPickerWindow.BadgeOf(noQuest) + ")");
			if (quest != null) Check(ref ok, IslandPickerWindow.BadgeOf(quest) == "QUEST", "'" + quest + "' shows QUEST (" + IslandPickerWindow.BadgeOf(quest) + ")");
			string shownDesc = IslandPickerWindow.DescriptionOf(noQuest) ?? "";
			Check(ref ok, rookeryDesc.Length == 0 ? shownDesc == "(no description)" : shownDesc.StartsWith(rookeryDesc.Replace("\n", " ").Substring(0, Math.Min(30, rookeryDesc.Length))),
				"'" + noQuest + "' shows its description: '" + (shownDesc.Length > 60 ? shownDesc.Substring(0, 60) + "..." : shownDesc) + "'");
			Check(ref ok, kinds.All(k => IslandPickerWindow.BadgeOf(k) == "NEW EACH TIME" && (IslandPickerWindow.DescriptionOf(k) ?? "").Length > 0), "the map-type and generated rows say NEW EACH TIME and what they are");
			string small = IslandPickerWindow.SmallPrintOf(noQuest) ?? "";
			Check(ref ok, (small.StartsWith("library island") || small.StartsWith("your island")) && small.Contains(" KB") && small.Contains(" - "), "'" + noQuest + "' small print: '" + small + "'");

			// Pictures (maps made once in the background, cached after)
			until = Time.realtimeSinceStartup + 120f;
			while ((IslandPickerWindow.PictureOf(noQuest) == null || (quest != null && IslandPickerWindow.PictureOf(quest) == null)) && Time.realtimeSinceStartup < until) yield return null;
			Check(ref ok, IslandPickerWindow.PictureOf(noQuest) != null && (quest == null || IslandPickerWindow.PictureOf(quest) != null), "the islands' rows have a picture" + (IslandPickerWindow.HasLibraryPicture(noQuest) ? " (" + noQuest + ": the library's)" : ""));
			Check(ref ok, kinds.All(k => IslandPickerWindow.PictureOf(k) != null) || RaftIslands.All.Count == 0, "the map-type rows have one of Raft's islands' pictures");
			Check(ref ok, System.IO.File.Exists(IslandThumbs.PngPath(noQuest)), "the map is kept in thumbcache");
			yield return new WaitForSecondsRealtime(1.5f);
			Screenshot(new[] { "picker_top" });
			yield return new WaitForSecondsRealtime(0.5f);

			// The search, a row's tick, Tick shown
			if (quest != null)
			{
				IslandPickerWindow.Search.text = quest; yield return new WaitForSecondsRealtime(0.5f);
				Screenshot(new[] { "picker_quest" });
				yield return new WaitForSecondsRealtime(0.5f);
			}
			IslandPickerWindow.Search.text = "Rookery"; yield return null;
			List<string> shown = IslandPickerWindow.Shown();
			Check(ref ok, shown.Contains(noQuest, StringComparer.OrdinalIgnoreCase) && shown.All(e => WorldIslands.Label(e).IndexOf("Rookery", StringComparison.OrdinalIgnoreCase) >= 0),
				"searching 'Rookery' shows only its rows (" + shown.Count + ")");
			IslandPickerWindow.Row(noQuest).onClick.Invoke(); yield return null;
			Check(ref ok, !IslandPickerWindow.IsTicked(noQuest) && WorldIslands.Chosen.Contains(noQuest), "a click on its row unticks it");
			yield return new WaitForSecondsRealtime(0.5f);
			Screenshot(new[] { "picker_search" });
			yield return new WaitForSecondsRealtime(0.5f);
			Check(ref ok, Click(IslandPickerWindow.Window.gameObject, "Tick shown"), "Tick shown clicked");
			yield return null;
			Check(ref ok, IslandPickerWindow.IsTicked(noQuest) && WorldIslands.Chosen.Count == 0, "Tick shown ticks it again");
			IslandPickerWindow.Search.text = ""; yield return null;

			// Tick all / Untick all (one button, whatever the search shows)
			Button allB = IslandPickerWindow.AllButton;
			Check(ref ok, allB != null && UIKit.LabelOf(allB).text == "Untick all", "with every row ticked the button says Untick all");
			IslandPickerWindow.Search.text = "Rookery"; yield return null;
			allB.onClick.Invoke(); yield return null;
			IslandPickerWindow.Search.text = ""; yield return null;
			Check(ref ok, all.All(e => !IslandPickerWindow.IsTicked(e)) && WorldIslands.Chosen.IsSupersetOf(all) && UIKit.LabelOf(allB).text == "Tick all" && UIKit.LabelOf(entry).text.Contains("none"),
				"Untick all unticks every row, not only those shown (button: '" + UIKit.LabelOf(entry).text + "')");
			yield return new WaitForSecondsRealtime(0.5f);
			Screenshot(new[] { "picker_alloff" });
			yield return new WaitForSecondsRealtime(0.5f);
			allB.onClick.Invoke(); yield return null;
			Check(ref ok, all.All(IslandPickerWindow.IsTicked) && WorldIslands.Chosen.Count == 0 && UIKit.LabelOf(allB).text == "Untick all", "Tick all ticks them all again");

			// Done keeps the choice; reopened, the list shows it
			IslandPickerWindow.Row(noQuest).onClick.Invoke(); yield return null;
			Check(ref ok, Click(IslandPickerWindow.Window.gameObject, "Done"), "Done clicked");
			yield return null;
			Check(ref ok, !IslandPickerWindow.IsOpen && WorldIslands.Pending != null && WorldIslands.Pending.Count == 1 && WorldIslands.Pending.Contains(noQuest),
				"Done keeps '" + noQuest + "' left out for the next world (button: '" + UIKit.LabelOf(entry).text + "')");
			entry.onClick.Invoke();
			yield return null;
			Check(ref ok, IslandPickerWindow.IsOpen && !IslandPickerWindow.IsTicked(noQuest) && quest != null && IslandPickerWindow.IsTicked(quest), "reopened, '" + noQuest + "' is still unticked");
			WorldIslands.Chosen.Clear();
			IslandPickerWindow.Close();
			WorldSettingsWindow.Close();
			box.gameObject.SetActive(false);
			if (ok) Log("PASS: island picker rows"); else Fail("island picker rows");
		}

		[ConsoleCommand(name: "CIIslandDifficulty", docs: "Dev, main menu: TODO 4d, the Difficulty label - the bands (0 Easy, 1-5 Medium, 6-20 Hard, 21-49 Extreme, 50+ Impossible); an island's monsters (spawn point counts, a 2..3 range its highest, boss calls; catchable, harmless, harmless sea life and damage 0 not); the island picker's small print says each island's; with a folder (a library copy whose index.json build-index.ps1 wrote) the library list's rows say theirs, the same count the game makes of each .island. Screenshots difficulty_picker, difficulty_library")]
		public static void IslandDifficultyCommand(string[] args) { StartTest(IslandDifficultyRoutine(args != null ? string.Join(" ", args).Trim() : "")); }

		static IEnumerator IslandDifficultyRoutine(string libraryFolder)
		{
			bool ok = true;
			int[] at = { 0, 1, 5, 6, 20, 21, 49, 50, 300 };
			string[] want = { "Easy", "Medium", "Medium", "Hard", "Hard", "Extreme", "Extreme", "Impossible", "Impossible" };
			Check(ref ok, at.Select(IslandDifficulty.NameOf).SequenceEqual(want) && IslandDifficulty.Label(-1) == "" && IslandDifficulty.Label(7) == "Difficulty: Hard",
				"the bands: " + string.Join(", ", at.Select(n => n + " " + IslandDifficulty.NameOf(n)).ToArray()));
			Func<string, string, string, IslandObject> obj = (name, key, value) => new IslandObject { Name = name, Props = key == null ? null : new Dictionary<string, string> { { key, value } } };
			var file = new IslandFile();
			file.Objects.AddRange(new[]
			{
				obj("Creature_Chicken", ObjectProps.CreatureCount, "4"), obj("Creature_Goat", null, null), obj("Creature_Llama", null, null), obj("Creature_Puffin", null, null),
				obj("Creature_ButlerBot", null, null), obj("Creature_Turtle", null, null), obj("Creature_Dolphin", null, null), obj("Creature_Stingray", null, null),
				obj("Creature_Boar", ObjectProps.CreatureCount, "2..3"), obj("Creature_Rat", null, null), obj("Creature_Bear", ObjectProps.CreatureDamage, "0"),
				obj("Creature_Bear", BossPhases.Summon, "2"), obj("Creature_PufferFish", ObjectProps.CreatureCount, "2"), obj("Placeable_Sign", ObjectProps.CreatureCount, "5"),
			});
			int counted = IslandDifficulty.MonstersOf(file);
			Check(ref ok, counted == 9, "a made-up island counts 9 monsters (boar 2..3 = 3, rat 1, boss 1 + 2 called, puffer fish 2; the animals to catch, harmless ones, sea life, a damage-0 bear, a sign: none): " + counted);

			// The island picker: each island's small print says its difficulty
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("difficulty: no New Game box (main menu?)"); yield break; }
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open();
			yield return new WaitForSecondsRealtime(1f);
			WorldSettingsWindow.Open();
			yield return null;
			Button entry = IslandPickerWindow.EntryButton;
			if (entry == null) { Fail("difficulty: no CHOOSE ISLANDS button"); yield break; }
			WorldIslands.Chosen.Clear();
			entry.onClick.Invoke();
			float until = Time.realtimeSinceStartup + 90f;
			while (!IslandPickerWindow.Loaded && Time.realtimeSinceStartup < until) yield return null;
			Check(ref ok, IslandPickerWindow.Loaded, "the picker's rows are filled in");
			List<string> islands = WorldIslands.Candidates().Where(WorldIslands.IsIsland).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).Take(60).ToList();
			var bands = new Dictionary<string, int>();
			var wrong = new List<string>();
			foreach (string e in islands)
			{
				int n;
				try { n = IslandDifficulty.MonstersOf(IslandFile.Load(IslandSpawner.PathFor(e))); } catch { continue; }
				string label = IslandDifficulty.Label(n), small = IslandPickerWindow.SmallPrintOf(e) ?? "";
				if (!small.Contains(label)) wrong.Add(e + " (" + n + ", '" + small + "')");
				bands[IslandDifficulty.NameOf(n)] = (bands.ContainsKey(IslandDifficulty.NameOf(n)) ? bands[IslandDifficulty.NameOf(n)] : 0) + 1;
			}
			Check(ref ok, islands.Count > 0 && wrong.Count == 0, "the picker's small print says each island's difficulty (" + islands.Count + " islands: " + string.Join(", ", bands.Select(kv => kv.Value + " " + kv.Key).ToArray()) + ")" + (wrong.Count > 0 ? " - wrong: " + string.Join("; ", wrong.Take(3).ToArray()) : ""));
			Debug.Log("[CITEST] picker small print: " + (islands.Count > 0 ? IslandPickerWindow.SmallPrintOf(islands[0]) : ""));
			yield return new WaitForSecondsRealtime(1.5f);
			Screenshot(new[] { "difficulty_picker" });
			yield return new WaitForSecondsRealtime(0.5f);
			IslandPickerWindow.Close();
			WorldSettingsWindow.Close();
			box.gameObject.SetActive(false);

			// The library list: index.json's "monsters" (build-index.ps1), the same as the game counts its .island
			if (libraryFolder.Length > 0 && System.IO.File.Exists(System.IO.Path.Combine(libraryFolder, "index.json")))
			{
				// (the New Game box may have started a list load from the real library: let it end first, or it lands
				// after the test's own and replaces its entries)
				yield return LoadList(false);
				yield return new WaitForSecondsRealtime(1f);
				LibraryClient.TestAddress = "file:///" + System.IO.Path.GetFullPath(libraryFolder).Replace('\\', '/').TrimEnd('/') + "/";
				int local = System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(System.IO.Path.Combine(libraryFolder, "index.json")), "\"path\"\\s*:").Count;
				for (int attempt = 0; attempt < 2; attempt++)
				{
					LibraryClient.Forget();
					try { if (LibraryWindow.IsOpen) LibraryWindow.Close(); LibraryWindow.Open(1); }
					catch (Exception x) { Check(ref ok, false, "the library opens: " + x.Message); }
					yield return LoadList(false);
					yield return new WaitForSecondsRealtime(2f);
					if (LibraryClient.Entries != null && LibraryClient.Entries.Count == local) break;
				}
				Check(ref ok, LibraryClient.Entries != null && LibraryClient.Entries.Count == local, "the list is the folder's (" + local + " entries): " + (LibraryClient.Entries != null ? LibraryClient.Entries.Count : -1));
				List<LibraryEntry> isl = (LibraryClient.Entries ?? new List<LibraryEntry>()).Where(x => !x.Info.IsPlan).ToList();
				int same = 0, shown = 0;
				var off = new List<string>();
				foreach (LibraryEntry x in isl)
				{
					LibraryFileRef f = x.Files.FirstOrDefault(r => r.Name.EndsWith(IslandFile.Extension, StringComparison.OrdinalIgnoreCase));
					int game = -1;
					try { if (f != null) game = IslandDifficulty.MonstersOf(IslandFile.Load(System.IO.Path.Combine(libraryFolder, x.Path.Replace('/', '\\'), f.Name))); } catch { }
					if (game == x.Monsters) same++; else off.Add(x.Info.id + " list " + x.Monsters + " / game " + game);
					Button row = LibraryWindow.Row(x.Info.id);
					Text sub = row != null ? row.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Sub") : null;
					if (sub != null && x.Monsters >= 0 && sub.text.Contains(IslandDifficulty.Label(x.Monsters))) shown++;
					else off.Add(x.Info.id + " row '" + (sub != null ? sub.text : "none") + "'");
				}
				Check(ref ok, isl.Count > 0 && same == isl.Count, "the list's monsters match the game's count of each .island (" + same + " of " + isl.Count + ")" + (off.Count > 0 ? " - " + string.Join("; ", off.Take(3).ToArray()) : ""));
				Check(ref ok, isl.Count > 0 && shown == isl.Count, "each island row says its difficulty (" + shown + " of " + isl.Count + ")");
				if (isl.Count > 0) Debug.Log("[CITEST] library row: " + (LibraryWindow.Row(isl[0].Info.id) != null ? LibraryWindow.Row(isl[0].Info.id).GetComponentsInChildren<Text>(true).First(t => t.name == "Sub").text : ""));
				Screenshot(new[] { "difficulty_library" });
				yield return new WaitForSecondsRealtime(0.6f);
				LibraryWindow.Close();
				LibraryClient.TestAddress = null;
				LibraryClient.Forget();
			}
			else Log("(no library folder given: the library list isn't looked at)");
			if (ok) Log("PASS: island difficulty"); else Fail("island difficulty");
		}
	}
}

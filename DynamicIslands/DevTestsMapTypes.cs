using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandUndoRedo;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>ROADMAP T7: map types as data (.maptype files) - export, read back, roll, content lines, bad files, re-roll.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIMapTypeFiles", docs: "Dev, editor: ROADMAP T7 - every built-in map type written as a .maptype text and read back has the same key settings and rolls the same islands (3 seeds); ExportMapType boss -> citest-mt-boss.maptype makes the same island as the built-in boss; a file of content lines (chest, note, creature, zone, atmosphere, quest) rolls an island with them; bad files (a bad number, a bad spot, a bad name, a built-in's name) are left out, never crash, and a file with an unknown key is read with that line kept and warned about; ReRollMapType puts content on the open island as one undo step. The test files are deleted again")]
		public static void MapTypeFilesCommand(string[] args) { StartTest(MapTypeFilesRoutine()); }

		const string MtRules =
@"label = CI rules island
title = CI Rules
settings = ranges
styles = desert
shape = round
radius = 60 - 70
height = 20 - 25
roughness = 0.4
density = 0.3
peaks = 2
set = Hostiles 0
chest = Loot_Chest | top:0.5 | CI cache | Treasure | Found it.\nWell done.
note = Note_Bottle | dry | CI map | Climb to the top.
creature = Rat | near:30 | 2 | Hard
zone = top:0.5 | ci-top | 6 | The top!
atmosphere = mid | 40 | #334455 | 0.4 | #FFEEDD | 0.3 | mist
quest = CI quest | Look around. | Done! |
step = read | CI map | 1 | Read the map
step = open | CI cache | 1 |
";

		static IEnumerator MapTypeFilesRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("map type files: in the editor"); yield break; }
			bool ok = true;
			var made = new List<string>();
			Func<string, string, string> write = (name, text) =>
			{
				Directory.CreateDirectory(MapTypeFiles.Folder);
				string p = Path.Combine(MapTypeFiles.Folder, name + MapTypeFiles.Extension);
				File.WriteAllText(p, text);
				made.Add(p);
				return p;
			};
			try
			{
				foreach (string f in Directory.Exists(MapTypeFiles.Folder) ? Directory.GetFiles(MapTypeFiles.Folder, "citest-mt-*") : new string[0]) File.Delete(f);
				MapTypeFiles.LoadAll();

				// 1. Every built-in type, written and read back: the same key settings, the same islands from the same seeds
				var wrong = new List<string>();
				foreach (MapType t in MapTypes.All.Where(x => x.FromFile == null).ToList())
				{
					string error;
					MapType p = MapTypeFiles.Parse("citest-mt-" + t.Name, MapTypeFiles.ToText(t), out error);
					if (p == null) { wrong.Add(t.Name + ": not read (" + error + ")"); continue; }
					var d = new List<string>();
					if (p.Label != t.Label || p.Description != t.Description || p.Title != t.Title) d.Add("texts");
					if (p.FlyingChance != t.FlyingChance || p.FlyingMin != t.FlyingMin || p.FlyingMax != t.FlyingMax || p.SunkenDepth != t.SunkenDepth) d.Add("elevation");
					if (t.Ranges != null && t.Settings == null) { string r = t.Ranges.Differs(p.Ranges); if (r.Length > 0 || p.SettingsFrom.Length > 0) d.Add("ranges " + r); }
					else if (p.SettingsFrom != t.Name) d.Add("settings from '" + p.SettingsFrom + "'");
					if (p.ContentFrom != (t.Content != null || t.Build != null ? t.Name : "")) d.Add("content from '" + p.ContentFrom + "'");
					foreach (int seed in new[] { 1, 42, 4242 })
					{
						float e1, e2;
						IslandGenSettings a = MapTypes.Roll(t, new System.Random(seed), out e1), b = MapTypes.Roll(p, new System.Random(seed), out e2);
						if (a.ToText() != b.ToText() || e1 != e2) { d.Add("seed " + seed + " rolls differently"); break; }
					}
					if (d.Count > 0) wrong.Add(t.Name + ": " + string.Join(", ", d.ToArray()));
				}
				Check(ref ok, wrong.Count == 0, MapTypes.All.Count(x => x.FromFile == null) + " built-in types written and read back: same key settings and rolls" + (wrong.Count > 0 ? " - " + string.Join("; ", wrong.ToArray()) : ""));

				// 2. ExportMapType: a file, read at once, the same island as the built-in
				string error2;
				string path = MapTypeFiles.Export("boss", "citest-mt-boss", out error2);
				if (path != null) made.Add(path);
				MapType fileBoss = MapTypes.Get("citest-mt-boss");
				Check(ref ok, path != null && File.Exists(path) && fileBoss != null && fileBoss.FromFile != null && File.ReadAllText(path).Contains("# Custom Islands map type"), "ExportMapType boss: " + (path ?? error2) + ", with the help at its top, and read as a type");
				if (fileBoss != null)
				{
					float e1, e2;
					IslandGenSettings a = MapTypes.Roll(MapTypes.Get("boss"), new System.Random(4242), out e1), b = MapTypes.Roll(fileBoss, new System.Random(4242), out e2);
					IslandFile fa = MapTypes.Create(MapTypes.Get("boss"), a, e1, "citype"), fb = MapTypes.Create(fileBoss, b, e2, "citype");
					Check(ref ok, Fingerprint(fa) == Fingerprint(fb) && fb.Objects.Count > 0, "the exported boss makes the built-in's island (" + fb.Objects.Count + " objects, the same land, content and quest)");
				}
				Check(ref ok, MapTypeFiles.Export("boss", "citest-mt-boss", out error2) == null && error2.Contains("already"), "exporting over an existing file is refused (" + error2 + ")");

				// 3. A file of its own content lines
				write("citest-mt-rules", MapTypeFiles.Help + MtRules);
				MapTypeFiles.LoadAll();
				MapType rules = MapTypes.Get("citest-mt-rules");
				Check(ref ok, rules != null && rules.Rules.Count == 8 && rules.Sets.Count == 1, "a file with content lines is read (" + (rules != null ? rules.Rules.Count + " lines" : string.Join("; ", MapTypeFiles.Skipped.ToArray())) + ")");
				if (rules != null)
				{
					float el;
					IslandGenSettings s = MapTypes.Roll(rules, new System.Random(77), out el);
					Check(ref ok, s.Style == TerrainPainter.Desert && s.Radius >= 60f && s.Radius <= 70f && s.Height >= 20f && s.Height <= 25f && s.Peaks <= 2 && s.Hostiles == 0 && el == 0f,
						"its land rolls in its ranges: desert, radius " + s.Radius.ToString("F0") + ", height " + s.Height.ToString("F0") + ", " + s.Peaks + " peak(s), Hostiles " + s.Hostiles);
					IslandFile f = MapTypes.Create(rules, s, el, "citest-mt-rules");
					Func<string, string, bool> has = (obj, title) => f.Objects.Any(o => o.Name == obj && (title == null || (o.Props != null && o.Props.ContainsKey(ObjectProps.NoteTitle) && o.Props[ObjectProps.NoteTitle] == title)));
					IslandObject chest = f.Objects.FirstOrDefault(o => o.Name == "Loot_Chest" && o.Props != null && o.Props.ContainsKey(ObjectProps.NoteTitle) && o.Props[ObjectProps.NoteTitle] == "CI cache");
					Check(ref ok, chest != null && chest.Props.ContainsKey(ObjectProps.LootItems) && chest.Props[ObjectProps.NoteText] == "Found it.\nWell done.", "the chest, with its loot and a two-line note");
					Check(ref ok, has("Note_Bottle", "CI map"), "the note");
					Check(ref ok, f.Objects.Any(o => o.Name == "Creature_Rat" && o.Props != null && o.Props.ContainsKey(ObjectProps.CreatureCount) && o.Props[ObjectProps.CreatureCount] == "2"), "the creatures (2 rats)");
					Check(ref ok, f.Objects.Any(o => o.Name == ContentCatalog.TriggerZone && o.Props != null && o.Props.ContainsKey(ObjectProps.ZoneId) && o.Props[ObjectProps.ZoneId] == "ci-top"), "the zone");
					Check(ref ok, f.Objects.Any(o => o.Name == ContentCatalog.AtmosphereZoneName && o.Props != null && o.Props.ContainsKey(ObjectProps.AtmoParticles) && o.Props[ObjectProps.AtmoParticles] == "mist"), "the atmosphere");
					string qt, qs, arrival;
					f.Props.TryGetValue(IslandQuest.KeyTitle, out qt); f.Props.TryGetValue(IslandQuest.KeySteps, out qs); f.Props.TryGetValue(IslandProps.Title, out arrival);
					Check(ref ok, qt == "CI quest" && (qs ?? "").Contains("CI map") && (qs ?? "").Contains("CI cache") && arrival == "CI Rules", "its quest (" + qt + ": " + qs + ") and title (" + arrival + ")");
					string again;
					MapType back = MapTypeFiles.Parse("citest-mt-rules", MapTypeFiles.ToText(rules), out again);
					Check(ref ok, back != null && back.Rules.SequenceEqual(rules.Rules) && back.Sets.SequenceEqual(rules.Sets) && back.Ranges.Differs(rules.Ranges) == "", "written again, it reads back the same (" + (again ?? "ok") + ")");
				}

				// 4. Bad files: left out with a line in the log, never a crash, the others still read (an unknown key: read, the line warned about)
				write("citest-mt-bad1", "radius = big\r\n");
				write("citest-mt-bad2", "colour = blue\r\n");
				write("citest-mt-bad3", "chest = Loot_Chest | nowhere | x | Basics\r\n");
				write("citest mt bad4", "label = spaces\r\n");
				string clash = Path.Combine(MapTypeFiles.Folder, "Sandbar" + MapTypeFiles.Extension);
				bool clashMade = !File.Exists(clash);
				if (clashMade) { File.WriteAllText(clash, "label = Not the sandbar\r\n"); made.Add(clash); }
				int read = -1;
				try { read = MapTypeFiles.LoadAll(); } catch (Exception e) { Check(ref ok, false, "reading bad files throws: " + e.Message); }
				Func<string, bool> skipped = n => MapTypeFiles.Skipped.Any(x => x.StartsWith(n + ":"));
				Check(ref ok, read >= 2 && skipped("citest-mt-bad1") && !skipped("citest-mt-bad2") && skipped("citest-mt-bad3") && skipped("citest mt bad4") && (!clashMade || skipped("Sandbar")),
					"bad files left out: " + string.Join("; ", MapTypeFiles.Skipped.ToArray()));
				Check(ref ok, MapTypes.Get("citest-mt-bad2") != null && MapTypes.Get("citest-mt-bad2").Rules.Contains("colour = blue") && MapTypeFiles.Unknown.Any(x => x.StartsWith("citest-mt-bad2:")),
					"a file with an unknown key is read, the line kept and warned about: " + string.Join("; ", MapTypeFiles.Unknown.ToArray()));
				Check(ref ok, MapTypes.Get("citest-mt-bad1") == null && MapTypes.Get("citest-mt-rules") != null && MapTypes.Get("citest-mt-boss") != null && MapTypes.Get("sandbar").FromFile == null && MapTypes.Get("sandbar").Label == "Sandbar",
					"the good files are still read, and the built-in sandbar is the built-in one");

				// 5. ReRollMapType: the content on the open island as one undo step, the land as it was
				MapType rr = MapTypes.Get("citest-mt-rules");
				GameObject root = GameObject.Find("PlacedObjects");
				if (rr != null && root != null)
				{
					Func<int> count = () => root.GetComponentsInChildren<EditorGameObject>(false).Length;
					string questBefore;
					DynamicIslands.currentIslandProps.TryGetValue(IslandQuest.KeyTitle, out questBefore);
					float[,] heights = terraineditor.terrain.terrainData.GetHeights(0, 0, 8, 8);
					int before = count(), changes = UndoRedoManager.Changes;
					string message;
					bool done = MapTypeFiles.ReRoll(rr, 7, out message);
					int after = count();
					bool chestThere = root.GetComponentsInChildren<EditorGameObject>(false).Any(x => x.Props != null && x.Props.ContainsKey(ObjectProps.NoteTitle) && x.Props[ObjectProps.NoteTitle] == "CI cache");
					string questAfter;
					DynamicIslands.currentIslandProps.TryGetValue(IslandQuest.KeyTitle, out questAfter);
					Check(ref ok, done && chestThere && UndoRedoManager.Changes == changes + 1 && questAfter == "CI quest", "ReRollMapType: " + message + " (objects " + before + " -> " + after + ", one undo step)");
					float[,] heights2 = terraineditor.terrain.terrainData.GetHeights(0, 0, 8, 8);
					bool same = true;
					for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++) if (heights[i, j] != heights2[i, j]) same = false;
					Check(ref ok, same, "the land is left as it was");
					if (done)
					{
						UndoRedoManager.Undo();
						string questUndone;
						DynamicIslands.currentIslandProps.TryGetValue(IslandQuest.KeyTitle, out questUndone);
						Check(ref ok, count() == before && questUndone == questBefore, "Ctrl+Z takes it off again (objects " + count() + ", quest '" + questUndone + "')");
					}
					string refused;
					Check(ref ok, !MapTypeFiles.ReRoll(MapTypes.Get("wreck"), 7, out refused), "a type without content (wreck) is refused: " + refused);
				}
				else Check(ref ok, false, "ReRollMapType: no island open in the editor");
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e); }
			finally
			{
				foreach (string p in made) try { if (File.Exists(p)) File.Delete(p); } catch { }
				try { MapTypeFiles.LoadAll(); } catch { }
			}
			yield return null;
			if (ok) Log("PASS: map type files"); else Fail("map type files");
		}
	}
}

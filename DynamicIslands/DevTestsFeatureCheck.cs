using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>The features of Raft the library's islands use (the user, 2026-10-05: every Raft feature on at least one island).</summary>
		static readonly KeyValuePair<string, Func<IslandObject, bool>>[] RaftFeatures =
		{
			new KeyValuePair<string, Func<IslandObject, bool>>("zipline", o => o.Name.StartsWith("ZiplinePath", StringComparison.Ordinal)),
			new KeyValuePair<string, Func<IslandObject, bool>>("machete vines", o => o.Name == ContentCatalog.MacheteVines),
			new KeyValuePair<string, Func<IslandObject, bool>>("buried treasure", o => o.Name == ContentCatalog.BuriedTreasure),
			new KeyValuePair<string, Func<IslandObject, bool>>("dirt", o => o.Name == "Pickup_Landmark_DirtPickup"),
			new KeyValuePair<string, Func<IslandObject, bool>>("wild beehive", o => o.Name == ContentCatalog.WildHive),
			// Raft's story machinery, the ready pieces (ReadyPieces; the user, 2026-10-06): each kind working as it is set up
			// when placed - a door that wants one of Raft's quest items, a cage the bolt cutters, a generator its part...
			new KeyValuePair<string, Func<IslandObject, bool>>("keycard / key door", o => Ready(o, ReadyPieces.Door) && Checks(o).Contains("has|" + StoryItems.Prefix + QuestItemPickups.IdPrefix)),
			new KeyValuePair<string, Func<IslandObject, bool>>("hatch", o => Ready(o, ReadyPieces.Hatch) && Uses(o).Contains("hide|")),
			new KeyValuePair<string, Func<IslandObject, bool>>("crank wheel", o => Ready(o, ReadyPieces.Crank) && Uses(o).Contains("signal||")),
			new KeyValuePair<string, Func<IslandObject, bool>>("lever", o => Ready(o, ReadyPieces.Lever) && Uses(o).Length > 0),
			new KeyValuePair<string, Func<IslandObject, bool>>("lift", o => Ready(o, ReadyPieces.Lift) && BehaviourProps.Offset(o.Props).y > 0f && ObjectProps.GetBool(o.Props, BehaviourProps.Carry, false)),
			new KeyValuePair<string, Func<IslandObject, bool>>("cage (bolt cutters)", o => Ready(o, ReadyPieces.Cage) && Checks(o).Contains(StoryItems.Ref(ReadyPieces.ItemOf(o.Name)))),
			new KeyValuePair<string, Func<IslandObject, bool>>("sweeping camera", o => Ready(o, ReadyPieces.Camera) && ObjectProps.Get(o.Props, BehaviourProps.MoveMode) == "loop" && ObjectProps.GetFloat(o.Props, BehaviourProps.Turn, 0f) != 0f),
			new KeyValuePair<string, Func<IslandObject, bool>>("generator (its part)", o => Ready(o, ReadyPieces.Generator) && Checks(o).Contains("take|" + StoryItems.Ref(ReadyPieces.ItemOf(o.Name))) && Uses(o).Contains("signal||" + ReadyPieces.PowerSignal)),
			new KeyValuePair<string, Func<IslandObject, bool>>("radio (power)", o => Ready(o, ReadyPieces.Radio) && Checks(o).Contains("signal|" + ReadyPieces.PowerSignal)),
			new KeyValuePair<string, Func<IslandObject, bool>>("engine (gas tank)", o => Ready(o, ReadyPieces.Engine) && Checks(o).Contains("take|" + StoryItems.Ref(ReadyPieces.ItemOf(o.Name)))),
			new KeyValuePair<string, Func<IslandObject, bool>>("turning mirror", o => Ready(o, ReadyPieces.Mirror) && ObjectProps.GetFloat(o.Props, BehaviourProps.Turn, 0f) != 0f && Uses(o).Contains("switch|")),
		};

		static bool Ready(IslandObject o, string kind) { ReadyPieces.Piece p = ReadyPieces.Of(o.Name); return p != null && p.Kind == kind && o.Props != null; }
		static string Checks(IslandObject o) { return ObjectProps.Get(o.Props, BehaviourProps.CheckKey("use")); }
		static string Uses(IslandObject o) { return ObjectProps.Get(o.Props, BehaviourProps.EventKey("use")); }

		[ConsoleCommand(name: "CIFeatureCheck", docs: "Dev, in game (host): spawns saved islands by name and checks their Raft features work there - zipline ends on the ground, vines with their hidden chest, buried treasure made, dirt, wild hives; the story machinery (ready pieces: doors, hatches, cranks, levers, lifts, cages, cameras, generators, radios, the engine, mirrors) usable or moving, the quest item each wants on the island, what it shows hidden till then, chests locked on a signal something sends. CIFeatureCheck <island>[,<island>...]")]
		public static void FeatureCheckCommand(string[] args)
		{
			string[] names = string.Join(" ", args ?? new string[0]).Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToArray();
			StartTest(FeatureCheckRoutine(names));
		}

		static IEnumerator FeatureCheckRoutine(string[] names)
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			Check(ref ok, new[] { ContentCatalog.MacheteItem, ContentCatalog.ZiplineItem, "MetalDetector", "Shovel" }.All(ContentCatalog.ItemExists), "Raft's tools exist: machete, zipline tool, metal detector, shovel");
			TreasurePointManager tm = UnityEngine.Object.FindObjectOfType<TreasurePointManager>();
			FieldInfo pa = typeof(MeshPath_Zipline_Landmark).GetField("pointA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (string name in names)
			{
				IslandFile f = IslandFile.Load(IslandSpawner.PathFor(name));
				if (f == null) { Check(ref ok, false, "'" + name + "': no saved island"); continue; }
				string found = string.Join(", ", RaftFeatures.Where(ft => f.Objects.Any(ft.Value)).Select(ft => ft.Key + " " + f.Objects.Count(ft.Value)).ToArray());
				IslandWorldState.Remove(name);
				Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 900f);
				if (!spot.HasValue) { Check(ref ok, false, "'" + name + "': no open sea"); continue; }
				yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
				IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
				if (e == null || e.Root == null) { Check(ref ok, false, "'" + name + "' did not spawn"); continue; }
				yield return new WaitForSeconds(1.5f);
				var said = new List<string>();
				// Ziplines: both floors on the ground (or a deck), the line made
				foreach (MeshPath_Zipline_Landmark line in e.Root.GetComponentsInChildren<MeshPath_Zipline_Landmark>(true))
				{
					Transform a = pa.GetValue(line) as Transform;
					var floors = line.transform.root.GetComponentsInChildren<Transform>(true).Where(t => t.IsChildOf(line.transform) && t.name.IndexOf("ZiplineBase", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
					foreach (Transform fl in floors)
					{
						RaycastHit hit;
						float gap = Physics.Raycast(fl.position + Vector3.up * 0.5f, Vector3.down, out hit, 30f, LayerMasks.MASK_GroundMask, QueryTriggerInteraction.Ignore) ? hit.distance - 0.5f : 99f;
						Check(ref ok, gap < 1.2f && gap > -1.5f, "'" + name + "': zipline floor " + fl.name + " on the ground (" + gap.ToString("F2") + " m over it)");
					}
					said.Add("zipline " + (a != null ? Vector3.Distance(line.transform.position, a.position).ToString("F0") + " m" : "?"));
				}
				// Vines: each with a chest it shows
				foreach (IslandObjectRef v in e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(r => r.ObjectName == ContentCatalog.MacheteVines || (r.Props != null && ObjectProps.Get(r.Props, BehaviourProps.Use).Contains("machete"))))
				{
					string onUse = ObjectProps.Get(v.Props, BehaviourProps.EventKey("use"));
					string target = onUse.Split(new[] { "\\n", "\n" }, StringSplitOptions.None).Where(l => l.StartsWith("show|")).Select(l => l.Substring(5)).FirstOrDefault();
					IslandObjectRef chest = target != null ? ScObjOf(e, target) : null;
					Check(ref ok, ObjectProps.Get(v.Props, BehaviourProps.CheckKey("use")).Contains(ContentCatalog.MacheteItem) && (target == null || (chest != null && !chest.gameObject.activeInHierarchy)),
						"'" + name + "': vines need the machete" + (target != null ? ", their chest '" + target + "' hidden until cut (" + (chest == null ? "missing" : chest.gameObject.activeInHierarchy ? "shown!" : "hidden") + ")" : ""));
				}
				// Raft's story machinery (the ready pieces): each usable (or moving), the quest item it wants on the island (in a
				// chest or as Raft's pickup), what its use shows hidden till then, a chest it unlocks waiting for its signal
				string allLoot = string.Join(";", f.Objects.Where(o => o.Props != null).Select(o => ObjectProps.Get(o.Props, ObjectProps.LootItems)).ToArray());
				// (the signals any of the island's events send - a use, a defeat, a zone...)
				var sent = new HashSet<string>(f.Objects.Where(o => o.Props != null).SelectMany(o => o.Props.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix)).Select(kv => kv.Value ?? ""))
					.SelectMany(v => v.Split(new[] { "\\n", "\n" }, StringSplitOptions.None)).Where(l => l.StartsWith("signal||")).Select(l => l.Substring(8).Trim()));
				foreach (IslandObjectRef r in e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(x => ReadyPieces.Of(x.ObjectName) != null && x.Props != null))
				{
					ReadyPieces.Piece p = ReadyPieces.Of(r.ObjectName);
					string label = "'" + name + "': " + p.Kind + " " + r.ObjectName + (!string.IsNullOrEmpty(r.Name) ? " '" + r.Name + "'" : "");
					// (a piece whose island took its use off - a door that only opens from something else - is not for players to use)
					if (p.Use != null && ObjectProps.Get(r.Props, BehaviourProps.Use).Length > 0) Check(ref ok, r.GetComponentInChildren<UseInteract>(true) != null, label + " - players can use it");
					if (BehaviourProps.Moves(r.Props)) Check(ref ok, r.GetComponentInChildren<IslandBehaviour>(true) != null, label + " - it moves");
					string checks = ObjectProps.Get(r.Props, BehaviourProps.CheckKey("use"));
					// (a piece left with its ready settings by an island that wires nothing to it - one placed as decoration before the
					// pieces had settings - is only noted: the island's own pieces must have what they want on the island)
					Dictionary<string, string> ready = ObjectProps.Defaults(r.ObjectName);
					bool untouched = ObjectProps.Get(r.Props, BehaviourProps.EventKey("use")) == ObjectProps.Get(ready, BehaviourProps.EventKey("use")) && checks == ObjectProps.Get(ready, BehaviourProps.CheckKey("use"));
					// (checks that start with "any" are alternatives - a radio works on a generator's power or on Raft's battery
					// part: one of them on the island is enough)
					string[] lines = checks.Split(new[] { "\\n", "\n" }, StringSplitOptions.None).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
					bool any = lines.Length > 0 && lines[0] == ObjCheck.AnyLine;
					var wants = new List<string>();
					bool oneThere = false;
					foreach (string l in lines)
					{
						System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(l, @"^(?:has|take)\|(" + System.Text.RegularExpressions.Regex.Escape(StoryItems.Prefix + QuestItemPickups.IdPrefix) + @"[A-Za-z0-9_]+)");
						if (m.Success)
						{
							string item = m.Groups[1].Value, id = item.Substring(StoryItems.Prefix.Length);
							bool there = allLoot.Contains(item + "*") || f.Objects.Any(o => QuestItemPickups.IsModel(o.Name) && QuestItemPickups.StoryId(o.Name) == id);
							if (any) { wants.Add(id); oneThere |= there; }
							else if (untouched && !there) Log("  " + label + ": left as placed (its ready settings, the island wires nothing to it) - the " + id + " it wants isn't on the island");
							else Check(ref ok, there, label + " - the " + id + " it wants is on the island (a chest or Raft's pickup)");
						}
						else if (any && l.StartsWith("signal|"))
						{
							// (sent by something else on the island: a generator's own "power" doesn't start it)
							string sg = l.Substring(7).Trim('|', ' ');
							wants.Add("the signal '" + sg + "'");
							int self = r.Index;
							oneThere |= f.Objects.Where((o, i) => i != self && o.Props != null).Any(o => o.Props.Any(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) && (kv.Value ?? "").Contains("signal||" + sg)));
						}
					}
					if (any && wants.Count > 0 && untouched && !oneThere) Log("  " + label + ": left as placed (its ready settings) - none of what it wants is on the island");
					else if (any && wants.Count > 0) Check(ref ok, oneThere, label + " - one of what it wants is on the island (" + string.Join(" or ", wants.ToArray()) + ")");
					// (a piece hidden at the start - a lift's top - shows its partner back: only those shown from the start count)
					if (!BehaviourProps.StartsHidden(r.Props))
					foreach (string target in ObjectProps.Get(r.Props, BehaviourProps.EventKey("use")).Split(new[] { "\\n", "\n" }, StringSplitOptions.None).Where(l => l.StartsWith("show|")).Select(l => l.Substring(5).Trim()))
					{
						IslandObjectRef shown = ScObjOf(e, target);
						Check(ref ok, shown != null && !shown.gameObject.activeInHierarchy, label + " - shows '" + target + "' (" + (shown == null ? "missing" : shown.gameObject.activeInHierarchy ? "shown already!" : "hidden till then") + ")");
					}
				}
				foreach (IslandObjectRef c in e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(x => x.Props != null && x.GetComponentInChildren<LootCrate>(true) != null))
					foreach (string sig in ObjectProps.Get(c.Props, BehaviourProps.CheckKey("open")).Split(new[] { "\\n", "\n" }, StringSplitOptions.None).Where(l => l.StartsWith("signal|")).Select(l => l.Substring(7).Trim('|', ' ')))
						Check(ref ok, sent.Contains(sig), "'" + name + "': the chest '" + ObjectProps.Get(c.Props, ObjectProps.NoteTitle) + "' opens on the signal '" + sig + "' - something on the island sends it");
				int buried = e.Root.GetComponentsInChildren<BuriedTreasure>(true).Length;
				if (buried > 0) Check(ref ok, tm != null && BuriedTreasure.PointsOf(tm, e.Root.transform).Count == buried, "'" + name + "': " + buried + " buried treasure(s) made as Raft's treasure points");
				Log("  '" + name + "': " + (found.Length > 0 ? found : "no Raft features") + (said.Count > 0 ? " (" + string.Join(", ", said.ToArray()) + ")" : ""));
				IslandWorldState.Remove(name);
				yield return new WaitForSeconds(0.5f);
			}
			if (ok) Log("PASS: feature check"); else Fail("feature check");
		}

		[ConsoleCommand(name: "CIFeatureCoverage", docs: "Dev, anywhere: every Raft feature (zipline, machete vines, buried treasure, dirt, wild beehive, and the story machinery: a keycard door, a hatch, a crank wheel, a lever, a lift, a cage for the bolt cutters, a sweeping camera, a generator, a radio, the engine, a turning mirror - each with its working settings) is on at least one of the library's islands (installed island files): the table")]
		public static void FeatureCoverageCommand(string[] args) { StartTest(FeatureCoverageRoutine()); }

		static IEnumerator FeatureCoverageRoutine()
		{
			bool ok = true;
			// (one file at a time: all of them at once ran out of memory; a frame between files - in one go 2000 files held
			// Raft still for over 5 minutes, and the next tests' commands waited behind it)
			int read = 0;
			var on = RaftFeatures.ToDictionary(ft => ft.Key, ft => new List<string>());
			foreach (string path in System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(IslandSpawner.PathFor("x")), "*" + IslandFile.Extension))
			{
				IslandFile f = IslandFile.Load(path);
				if (f == null) continue;
				// (a ready piece counts where what it wants is on the island too: a keycard door with its keycard in a chest)
				string loot = string.Join(";", f.Objects.Where(o => o.Props != null).Select(o => ObjectProps.Get(o.Props, ObjectProps.LootItems)).ToArray());
				Func<IslandObject, bool> supplied = o =>
				{
					var items = System.Text.RegularExpressions.Regex.Matches(Checks(o), @"(?:has|take)\|(" + System.Text.RegularExpressions.Regex.Escape(StoryItems.Prefix + QuestItemPickups.IdPrefix) + @"[A-Za-z0-9_]+)")
						.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();
					if (items.Count == 0 || items.Any(i => loot.Contains(i + "*") || f.Objects.Any(x => QuestItemPickups.IsModel(x.Name) && StoryItems.Ref(QuestItemPickups.StoryId(x.Name) ?? "") == i))) return true;
					// (or, for alternatives - "any": a radio on a generator's power - a signal one of the island's other objects sends)
					if (!Checks(o).StartsWith(ObjCheck.AnyLine)) return false;
					return Checks(o).Split(new[] { "\\n", "\n" }, StringSplitOptions.None).Where(l => l.StartsWith("signal|")).Select(l => l.Substring(7).Trim('|', ' '))
						.Any(sg => f.Objects.Any(x => x != o && x.Props != null && x.Props.Any(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) && (kv.Value ?? "").Contains("signal||" + sg))));
				};
				foreach (var ft in RaftFeatures) if (f.Objects.Any(o => ft.Value(o) && supplied(o))) on[ft.Key].Add(f.Name);
				f = null;
				if (++read % 25 == 0) { GC.Collect(); yield return null; }
				if (read % 500 == 0) Log("  " + read + " island files read");
			}
			foreach (var ft in RaftFeatures)
			{
				string[] names = on[ft.Key].Distinct().OrderBy(n => n).ToArray();
				Check(ref ok, names.Length > 0, ft.Key + ": " + names.Length + " island(s) - " + string.Join(", ", names.Take(12).ToArray()));
			}
			if (ok) Log("PASS: feature coverage"); else Fail("feature coverage");
		}
	}
}

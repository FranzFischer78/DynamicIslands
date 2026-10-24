using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using DynamicIslands.Editor;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CILookDump", docs: "Dev, editor: where the placeable objects of one of Raft's island scenes stand (name, position, turn, scale; the scene's sea at y 0), biggest first: Mods/DynamicIslands/lookdump_<part>.txt. For the look-alike randomized islands (IslandLooks). CILookDump <scene part>")]
		public static void LookDumpCommand(string[] args)
		{
			if (args == null || args.Length < 1 || !DynamicIslands.InEditor()) { Fail("CILookDump <scene part> (in the editor)"); return; }
			string want = args[0].Replace('_', ' ');
			string scene = PlaceableCatalog.LandmarkSceneNames().FirstOrDefault(s => s.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0);
			if (scene == null) { Fail("no scene like '" + want + "'"); return; }
			StartTest(PlaceableCatalog.VisitScene(scene, s => LookDump(s, args[0])));
		}

		static IEnumerator LookDump(Scene src, string part)
		{
			var rows = new List<KeyValuePair<float, string>>();
			foreach (KeyValuePair<string, Transform> kv in PlaceableCatalog.PlaceablesOf(src))
			{
				Transform t = kv.Value;
				if (t == null) continue;
				Renderer[] rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
				Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
				foreach (Renderer r in rs) b.Encapsulate(r.bounds);
				Vector3 e = t.eulerAngles, sc = t.lossyScale;
				rows.Add(new KeyValuePair<float, string>(b.size.magnitude, string.Format(CultureInfo.InvariantCulture,
					"{0}\t{1:F2} {2:F2} {3:F2}\t{4:F1} {5:F1} {6:F1}\t{7:F2} {8:F2} {9:F2}\tsize {10:F1} {11:F1} {12:F1}\tmid {14:F1} {15:F1} {16:F1}\t{13}",
					kv.Key, t.position.x, t.position.y, t.position.z, e.x, e.y, e.z, sc.x, sc.y, sc.z, b.size.x, b.size.y, b.size.z, t.gameObject.activeInHierarchy ? "" : "off", b.center.x, b.center.y, b.center.z)));
			}
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "lookdump_" + part + ".txt"));
			File.WriteAllLines(file, rows.OrderByDescending(r => r.Key).Select(r => r.Value).ToArray());
			Log("PASS: look dump " + src.name + ": " + rows.Count + " objects, written to " + file);
			yield break;
		}

		/// <summary>The islands CILookAlike tries, one or two per look: look id, scene part, signature pieces it must put at least
		/// [, the pieces that must all be there (X7: Tangaroa's balconies and cockpit, Utopia's towers, the Radio Tower's dish)].</summary>
		static readonly string[][] LookIslands =
		{
			new[] { "caravan", "Landmark_CaravanSmall#3", "4" }, new[] { "caravan", "Landmark_CaravanIsland", "4" }, new[] { "tangaroa", "Landmark_Tangaroa", "6", "Dome_Base_2,TangaroaRoads_Combined,Dome_Base_1,Balcony01,Balcony02,Cockpit,CockpitExt,Outside" },
			new[] { "utopia", "Landmark_Utopia", "3", "UT_Set_Tower01,UT_Set_Tower02,UT_Set_Tower03,UT_Set_Tower_EndRoom,UT_Set_Tower_EndRoom_Glass" }, new[] { "varuna", "Landmark_VarunaPoint", "3" }, new[] { "balboa", "Landmark_BalboaSmall#1", "2" },
			new[] { "temperance", "Landmark_Temperance", "2" }, new[] { "vasagatan", "Landmark_Vasagatan", "3" }, new[] { "radio", "Landmark_Radar", "3", "RT_SatteliteDisc" },
		};

		[ConsoleCommand(name: "CILookAlike", docs: "Dev, editor: TODO §3 - look-alike randomized story islands: for each look (or those whose id or island has <part>) its island's something-new-like-it (var: a variation) generated as the Randomize existing tab does, with seed [seed] (7): built without errors, at least N of its signature pieces, every piece loaded, at least half the land objects the original island's own pieces, the standing ones on the ground; a picture shot_look_<island>_<seed>.png. CILookAlike [part] [seed] [var]")]
		public static void LookCommand(string[] args)
		{
			string part = ""; int seed = 7, n; bool variation = false;
			foreach (string a in args ?? new string[0]) { if (int.TryParse(a, out n)) seed = n; else if (a == "var") variation = true; else if (a.Trim().Length > 0) part = a.Trim(); }
			StartTest(LookRoutine(part, seed, variation));
		}

		static IEnumerator LookRoutine(string part, int seed, bool variation)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("CILookAlike (in the editor)"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			int count = 0;
			var errors = new List<string>();
			Application.LogCallback catcher = (msg, trace, type) => { if (type == LogType.Exception || type == LogType.Error) errors.Add(msg); };
			foreach (string[] row in LookIslands.Where(x => part.Length == 0 || x[0].Contains(part) || x[1].IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0))
			{
				RaftIsland island = RaftIslands.All.FirstOrDefault(i => i.Scene.Contains(row[1]));
				if (island == null) { Check(ref ok, false, row[1] + ": not measured (raft_islands.txt)"); continue; }
				Remakes.Look look = Remakes.LookFor(island.Scene);
				if (look == null || look.Id != row[0]) { Check(ref ok, false, island.Label + ": look " + (look != null ? look.Id : "none") + ", not " + row[0]); continue; }
				// (as the Randomize existing tab does: Choose, ApplyChosen, Effective)
				IslandGenSettings gs = variation ? RaftIslands.VariationOf(island, new IslandGenSettings { Seed = seed }) : RaftIslands.LikeIt(island, new IslandGenSettings { Seed = seed });
				gs.Seed = seed;
				List<Remakes.Design> designs = Remakes.For(island.Scene);
				gs.Remake = designs.Count > 0 ? designs[0].Scene + Remakes.Any : "";
				gs.Buildings = false; gs.Caves = false; gs.RaftSeaKinds = true;
				yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(gs).Concat(IslandGenerator.SeaNamesNeeded(gs)).Distinct().ToList());
				// (the original's own pieces: what its scene has, its look's kit and its designs' kits)
				var own = new HashSet<string>(look.Kit.Concat(designs.SelectMany(d => d.Kit ?? new string[0])));
				string scene = PlaceableCatalog.LandmarkSceneNames().FirstOrDefault(sn => sn.Contains(row[1]));
				if (scene != null) yield return PlaceableCatalog.VisitScene(scene, src => { foreach (var kv in PlaceableCatalog.PlaceablesOf(src)) own.Add(kv.Key); return NoSteps(); });
				errors.Clear();
				Application.logMessageReceived += catcher;
				int made = 0;
				try { made = IslandGenerator.GenerateInEditor(gs); }
				catch (Exception e) { errors.Add(e.GetType().Name + ": " + e.Message); }
				finally { Application.logMessageReceived -= catcher; }
				yield return new WaitForSecondsRealtime(0.6f);
				List<string> built = IslandGenerator.LastReport.Built;
				List<EditorGameObject> objs = PlacedEditorObjects();
				Terrain terrain = terraineditor.terrain;
				float sea = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
				var kit = new HashSet<string>(look.Kit);
				List<EditorGameObject> mine = objs.Where(e => kit.Contains(e.GameObjectName)).ToList();
				// (the land's scenery: loot, creatures and pickups are the game's, not the island's look)
				List<EditorGameObject> land = objs.Where(e => !Regex.IsMatch(e.GameObjectName, "^Loot_|^Creature_|^Pickup_") && terrain.SampleHeight(e.transform.position) + terrain.transform.position.y > sea + 0.3f).ToList();
				int owned = land.Count(e => own.Contains(e.GameObjectName));
				float share = land.Count > 0 ? owned / (float)land.Count : 0f;
				int missing = objs.Count(e => e.gameObject.name.Contains(IslandSpawner.MissingTag));
				// (standing: rocks, trees, caravans, the huts' pillars on the ground - bridges, ladders, roofs and the built places' sets are meant to be up)
				var floating = new List<string>();
				foreach (EditorGameObject e in mine.Where(x => !Regex.IsMatch(x.GameObjectName, "Bridge|Ladder|^Roof$|^Tower|^UT_|^Dome|^Balcony|^Cockpit|^Arrows|^Tangaroa|^Outside$|^VP_|^RT_SatteliteDisc$|^RT_PlasticBoat$")))
				{
					Renderer[] rs = PlacementOptions.ShapeRenderers(e.gameObject);
					if (rs.Length == 0) continue;
					Bounds b = rs[0].bounds;
					foreach (Renderer r in rs) b.Encapsulate(r.bounds);
					float ground = terrain.SampleHeight(b.center) + terrain.transform.position.y;
					// (or on another piece: a caravan on a design's scaffold deck stands)
					if (b.min.y - ground > 0.6f && !objs.Any(o => o != e && OnTopOf(b, o)))
					{
						floating.Add(e.GameObjectName + " +" + (b.min.y - ground).ToString("F1") + " at " + b.center.x.ToString("F0", CultureInfo.InvariantCulture) + " " + b.center.z.ToString("F0", CultureInfo.InvariantCulture));
						// (a close picture from the side, 10 m south of it at its foot, looking north)
						if (floating.Count == 1)
							Log("floating " + e.GameObjectName + ": CIShotFrom " + string.Join(" ", new[] { b.center.x, b.min.y + 1f, b.center.z - Mathf.Max(10f, b.extents.z + 6f), 0f, -5f }.Select(v => v.ToString("F1", CultureInfo.InvariantCulture)).ToArray()) + " float_" + e.GameObjectName);
					}
				}
				int need = int.Parse(row[2]);
				List<string> absent = row.Length > 3 ? row[3].Split(',').Where(n => !mine.Any(e => e.GameObjectName == n)).ToList() : new List<string>();
				var top = mine.GroupBy(e => e.GameObjectName).OrderByDescending(g => g.Count()).Take(8).Select(g => g.Key + " x" + g.Count());
				Check(ref ok, errors.Count == 0 && made > 0 && mine.Count >= need && absent.Count == 0 && missing == 0 && floating.Count == 0 && share >= 0.5f,
					island.Label + " (" + look.Id + (variation ? ", variation" : "") + "), seed " + seed + ": " + made + " objects; " + string.Join("; ", built.ToArray()) + " - " + mine.Count + " look pieces (" + string.Join(", ", top.ToArray()) + ")" +
					", own pieces " + (share * 100f).ToString("F0") + " % of " + land.Count + " on land" + (missing > 0 ? ", " + missing + " not loaded" : "") + (absent.Count > 0 ? ", not put: " + string.Join(", ", absent.ToArray()) : "") +
					(share < 0.5f ? ", not own: " + string.Join(", ", land.Where(e => !own.Contains(e.GameObjectName)).GroupBy(e => e.GameObjectName).OrderByDescending(g => g.Count()).Take(6).Select(g => g.Key + " x" + g.Count()).ToArray()) : "") +
					(floating.Count > 0 ? ", in the air: " + string.Join(", ", floating.Take(6).ToArray()) : "") + (errors.Count > 0 ? ", ERRORS: " + string.Join(" | ", errors.Take(3).ToArray()) : ""));
				if (Camera.main != null)
				{
					// (from the south-west, high, as CIRaftView's pictures of the originals: CIRaftView <island> 0 0 10 45 32 <dist> <name>)
					Vector2 mid = EditorLandCentre();
					float spread = 0f;
					foreach (EditorGameObject e in mine) spread = Mathf.Max(spread, (new Vector2(e.transform.position.x, e.transform.position.z) - mid).magnitude);
					float dist = Mathf.Clamp(Mathf.Max(gs.Radius * 2.6f, spread * 1.8f), 90f, 700f);
					Quaternion q = Quaternion.Euler(32f, 45f, 0f);
					Vector3 at = new Vector3(mid.x, sea + 10f, mid.y);
					Camera.main.transform.SetPositionAndRotation(at - q * Vector3.forward * dist, q);
					yield return new WaitForSecondsRealtime(1.5f);
					Screenshot(new[] { "look_" + row[1].Replace("Landmark_", "").Replace("#", "") + "_" + seed + (variation ? "_var" : "") });
					yield return new WaitForSecondsRealtime(0.5f);
					Log("view " + island.Label + ": CIRaftView " + row[1].Replace("Landmark_", "").Replace("#", "_") + " 0 0 10 45 32 " + dist.ToString("F0", CultureInfo.InvariantCulture) + " look_orig");
				}
				count++;
			}
			if (ok && count > 0) Log("PASS: looks (" + count + ")"); else Fail("looks");
		}

		static IEnumerator NoSteps() { yield break; }

		/// <summary>Whether a box's bottom rests on another placed piece (its top within 0.6 m, their footprints overlapping).</summary>
		static bool OnTopOf(Bounds b, EditorGameObject o)
		{
			foreach (Renderer r in PlacementOptions.ShapeRenderers(o.gameObject))
			{
				Bounds ob = r.bounds;
				if (Mathf.Abs(ob.max.y - b.min.y) < 0.6f && ob.min.x < b.center.x && ob.max.x > b.center.x && ob.min.z < b.center.z && ob.max.z > b.center.z) return true;
			}
			return false;
		}
	}
}

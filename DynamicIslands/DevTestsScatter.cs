using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>The editor's scatter (ROADMAP LM11): things to gather spread round a point - on land, kept off what was
		/// already there - and Raft's sea finds on the sea floor; one undo takes each scatter back.</summary>
		[ConsoleCommand(name: "CIScatterTool", docs: "Dev, editor with an island open: the scatter - pineapples on land kept 3 m off the island's objects, seaweed on the sea floor, each taken back by one undo")]
		public static void ScatterToolCommand(string[] args) { StartTest(ScatterToolRoutine()); }

		static IEnumerator ScatterToolRoutine()
		{
			if (!DynamicIslands.InEditor() || terraineditor.terrain == null) { Fail("CIScatterTool (in the editor, an island open)"); yield break; }
			bool ok = true;
			float sea = DynamicIslands.EditorWaterLevel;
			Terrain t = terraineditor.terrain;
			Vector3 mid = t.transform.position + new Vector3(t.terrainData.size.x / 2f, 0f, t.terrainData.size.z / 2f);
			// (the editor's new island is open sea since the 10-16 cleanup - no saved island opens with it: then the tests'
			// sample island is opened for the land)
			Vector3 g0, n0g;
			if (!PlacementOptions.GroundAt(new Vector3(mid.x, sea + 200f, mid.z), out g0, out n0g) || g0.y < sea + 0.3f)
			{
				bool opened = DynamicIslands.LoadIsland(SaveSampleIsland());
				yield return null;
				yield return null;
				RemoveSampleIsland();
				if (!opened || terraineditor.terrain == null) { Fail("scatter tool: the sample island didn't open"); yield break; }
				t = terraineditor.terrain;
				Vector2 c = IslandSpawner.LandCentre(SampleIsland());
				mid = t.transform.position + new Vector3(c.x, 0f, c.y);
				Log("The sample island opened (the island open had no land in the middle)");
			}
			GameObject root = GameObject.Find("PlacedObjects");
			Func<List<Vector3>> before = () => root.GetComponentsInChildren<EditorGameObject>(false).Select(e => e.transform.position).ToList();
			Func<int> count = () => root.GetComponentsInChildren<EditorGameObject>(false).Length;

			// On land: pineapples, 3 m off everything already placed
			List<Vector3> had = before();
			int n0 = count();
			List<GameObject> land = ScatterTool.Scatter("Pickup_Landmark_PineappleLandmark", mid, 8, Mathf.Min(60f, t.terrainData.size.x / 3f), 3f, false, 4242);
			Check(ref ok, land.Count >= 4, "pineapples scattered on land: " + land.Count + " of 8");
			Check(ref ok, land.All(g => g.transform.position.y > sea), "all on land, above the sea");
			float nearest = land.Count == 0 || had.Count == 0 ? 99f : land.Min(g => had.Min(p => Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(g.transform.position.x, 0, g.transform.position.z))));
			Check(ref ok, nearest > 2.9f, "kept off the island's own objects (nearest " + nearest.ToString("F1") + " m)");
			Check(ref ok, land.All(g => g.GetComponent<EditorGameObject>() != null), "each an editor object (saved with the island)");
			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, count() == n0, "one undo takes them all back (" + count() + " objects, " + n0 + " before)");

			// Under water: seaweed on the sea floor round the island
			int n1 = count();
			List<GameObject> sea1 = ScatterTool.Scatter("SeaVine3_klump", mid, 6, t.terrainData.size.x / 2f, 1f, true, 4343);
			Check(ref ok, sea1.Count >= 3, "seaweed scattered under water: " + sea1.Count + " of 6");
			Check(ref ok, sea1.All(g => g.transform.position.y < sea - 0.3f), "all on the sea floor");
			Check(ref ok, ScatterTool.OnlyUnderWater("SeaVine3_klump") && ScatterTool.OnlyUnderWater("Pickup_Landmark_Scrap 1_OceanBottom") && !ScatterTool.OnlyUnderWater("Pickup_Landmark_PineappleLandmark"),
				"sea finds go under water by themselves");
			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, count() == n1, "one undo takes the seaweed back");
			if (ok) Log("PASS: scatter tool"); else Fail("scatter tool");
		}

		/// <summary>The editor tools that match the storyline recipes (SL10-SL14 editor gaps, 2026-10-23): Flatten to a typed
		/// height, a seeded scatter (same seed, same spots) on land / under water / anywhere, sizes above 100 in Place exactly,
		/// the Air pocket button's set, and a zipline's near end put where the camera looks.</summary>
		[ConsoleCommand(name: "CIEditorGapTools", docs: "Dev, editor with an island open: Flatten to 4 m, seeded scatter repeats, land/under water/anywhere, size 250, the air pocket set, the zipline's near end on a spot")]
		public static void EditorGapToolsCommand(string[] args) { StartTest(EditorGapToolsRoutine()); }

		static IEnumerator EditorGapToolsRoutine()
		{
			if (!DynamicIslands.InEditor() || terraineditor.terrain == null) { Fail("CIEditorGapTools (in the editor, an island open)"); yield break; }
			bool ok = true;
			float sea = DynamicIslands.EditorWaterLevel;
			Terrain t = terraineditor.terrain;
			Vector3 mid = t.transform.position + new Vector3(t.terrainData.size.x / 2f, 0f, t.terrainData.size.z / 2f);
			Vector3 g0, n0g;
			if (!PlacementOptions.GroundAt(new Vector3(mid.x, sea + 200f, mid.z), out g0, out n0g) || g0.y < sea + 0.3f)
			{
				bool opened = DynamicIslands.LoadIsland(SaveSampleIsland());
				yield return null;
				yield return null;
				RemoveSampleIsland();
				if (!opened || terraineditor.terrain == null) { Fail("editor gap tools: the sample island didn't open"); yield break; }
				t = terraineditor.terrain;
				Vector2 c = IslandSpawner.LandCentre(SampleIsland());
				mid = t.transform.position + new Vector3(c.x, 0f, c.y);
			}
			GameObject root = GameObject.Find("PlacedObjects");
			const string pine = "Pickup_Landmark_PineappleLandmark";
			Terrain tt = t;
			Func<Vector3, float> above = p => tt.SampleHeight(p) + tt.transform.position.y - sea;

			// 1. Flatten to a typed height (a recipe's "brush flatten ... h=4")
			var editor = Camera.main.GetComponent<terraineditor>();
			var savedAction = terraineditor.modificationAction;
			Vector3 spot = mid + new Vector3(5f, 0f, 5f);
			float hBefore = above(spot);
			EditorUI.SetFlattenHeight("4");
			Check(ref ok, terraineditor.FlattenHeight.HasValue && Mathf.Approximately(terraineditor.FlattenHeight.Value, 4f), "Flatten to: the field takes 4 m");
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Flatten;
			editor.SimulateStroke(spot, 120, 0.033f, null, true);
			float hAfter = above(spot);
			Check(ref ok, Mathf.Abs(hAfter - 4f) < 0.5f, "the stroke levels the ground to 4 m above the sea (" + hBefore.ToString("F1") + " -> " + hAfter.ToString("F2") + " m)");
			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, Mathf.Abs(above(spot) - hBefore) < 0.05f, "one undo puts the ground back");
			EditorUI.SetFlattenHeight("");
			Check(ref ok, !terraineditor.FlattenHeight.HasValue, "an empty field: flatten to where the stroke starts again");
			terraineditor.modificationAction = savedAction;

			// 2. Seeded scatter: the same seed gives the same spots, another seed others
			float r = Mathf.Min(25f, t.terrainData.size.x / 4f);
			Vector3 m0 = mid;
			Func<int, List<Vector3>> spots = seed =>
			{
				List<Vector3> made = ScatterTool.Scatter(pine, m0, 6, r, 0f, 0, seed).Select(g => g.transform.position).ToList();
				if (made.Count > 0) CommandUndoRedo.UndoRedoManager.Undo();
				return made;
			};
			List<Vector3> s1 = spots(777), s2 = spots(777), s3 = spots(778);
			Check(ref ok, s1.Count >= 3 && s1.Count == s2.Count && s1.Zip(s2, (a, b) => (a - b).magnitude).All(d => d < 0.01f), "seed 777 twice: the same " + s1.Count + " spots");
			Check(ref ok, s3.Count > 0 && (s3.Count != s1.Count || s1.Zip(s3, (a, b) => (a - b).magnitude).Any(d => d > 0.5f)), "seed 778: other spots");

			// 3. Where: under water only, or anywhere (both)
			float wide = t.terrainData.size.x / 2.2f;
			List<GameObject> wet = ScatterTool.Scatter(pine, mid, 10, wide, 0f, 1, 31);
			Check(ref ok, wet.Count >= 3 && wet.All(g => g.transform.position.y < sea), "under water: all " + wet.Count + " below the sea");
			if (wet.Count > 0) CommandUndoRedo.UndoRedoManager.Undo();
			List<GameObject> any = ScatterTool.Scatter(pine, mid, 40, wide, 0f, 2, 32);
			int up = any.Count(g => g.transform.position.y > sea), down = any.Count - up;
			Check(ref ok, up > 0 && down > 0, "anywhere: " + up + " on land and " + down + " under water");
			if (any.Count > 0) CommandUndoRedo.UndoRedoManager.Undo();
			EditorUI.SetTab(TAB.ObjectPlace);
			yield return null;
			ObjectBrowser browser = ObjectBrowser.Instance;
			Button whereButton = browser != null ? browser.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Button_ScatterWhere") : null;
			if (whereButton == null) Check(ref ok, false, "the scatter's where button is in the object list");
			else
			{
				var seen = new List<string>();
				for (int i = 0; i < 3; i++) { whereButton.onClick.Invoke(); seen.Add(UIKit.LabelOf(whereButton).text); }
				Check(ref ok, string.Join(",", seen.ToArray()) == "Under water,Anywhere,On land", "the where button cycles: " + string.Join(" > ", seen.ToArray()));
			}
			Check(ref ok, browser != null && browser.GetComponentsInChildren<InputField>(true).Any(f => f.name == "Field_ScatterSeed"), "the scatter has a seed field");

			// 4. Place exactly: size 250 (above the old cap of 100)
			var gizmo = DynamicIslands.EditorGizmoHandler;
			List<GameObject> one = ScatterTool.Scatter(pine, mid, 1, r, 0f, 0, 5);
			if (one.Count == 1 && gizmo != null)
			{
				gizmo.ClearTargets(false);
				gizmo.AddTarget(one[0].transform, false);
				for (int i = 0; i < 5; i++) yield return null;
				IList<InputField> f = ObjectInspector.PlaceFields;
				if (f.Count == 9)
				{
					for (int i = 6; i < 9; i++) f[i].text = "250";
					f[8].onEndEdit.Invoke(f[8].text);
					Check(ref ok, Mathf.Abs(one[0].transform.lossyScale.x - 250f) < 0.5f && Mathf.Abs(one[0].transform.lossyScale.z - 250f) < 0.5f, "Place exactly takes size 250 (" + one[0].transform.lossyScale.x.ToString("F0") + ")");
					CommandUndoRedo.UndoRedoManager.Undo();
				}
				else Check(ref ok, false, "the inspector shows Place exactly");
				gizmo.ClearTargets(false);
				CommandUndoRedo.UndoRedoManager.Undo();
			}
			else Check(ref ok, false, "a pineapple to size");

			// 5. The Air pocket button's set (lib_frozen's airpocket macro)
			Check(ref ok, Resources.FindObjectsOfTypeAll<Button>().Any(b => b.name == "Button_AirPocket"), "the Objects tab has an Air pocket button");
			yield return PlaceableCatalog.EnsureLoaded(new[] { ScatterTool.AirPocketObject, "ZiplinePath_Landmark" });
			List<GameObject> pocket = ScatterTool.AirPocketAt(mid + new Vector3(-6f, 0f, -6f));
			EditorGameObject trig = pocket.Select(g => g.GetComponent<EditorGameObject>()).FirstOrDefault(e => e != null && e.GameObjectName == ContentCatalog.TriggerZone);
			EditorGameObject atmo = pocket.Select(g => g.GetComponent<EditorGameObject>()).FirstOrDefault(e => e != null && e.GameObjectName == ContentCatalog.AtmosphereZoneName);
			Check(ref ok, pocket.Count == 3 && pocket.Any(g => g.GetComponent<EditorGameObject>().GameObjectName == ScatterTool.AirPocketObject), "air pocket: Raft's air pocket and two zones (" + pocket.Count + ")");
			Check(ref ok, trig != null && ObjectProps.Get(trig.Props, ObjectProps.ZoneAir) == "1" && ObjectProps.Get(trig.Props, ObjectProps.ZoneRadius) == "2.5", "... the trigger zone has Air on, radius 2.5");
			Check(ref ok, atmo != null && ObjectProps.Get(atmo.Props, ObjectProps.AtmoParticles) == "bubbles", "... the atmosphere zone has bubbles");
			if (pocket.Count > 0) { CommandUndoRedo.UndoRedoManager.Undo(); Check(ref ok, pocket.All(g => !g.activeSelf), "one undo takes the set back"); }

			// 6. Zipline: its near end where the camera looks (on a deck: here 6 m up in the air), readout shown
			GameObject zip = PlaceableCatalog.Spawn("ZiplinePath_Landmark", root.transform);
			if (zip == null) Check(ref ok, false, "a zipline to place");
			else
			{
				zip.transform.position = new Vector3(mid.x + 8f, above(mid + new Vector3(8f, 0f, 0f)) + sea, mid.z);
				EditorGameObject ze = EditorGameObject.Attach(zip, "ZiplinePath_Landmark");
				CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(new[] { zip }, true));
				Vector3 before = zip.transform.position, deck = new Vector3(mid.x + 10f, sea + 6f, mid.z + 2f);
				Check(ref ok, ObjectInspector.NearEndTo(ze, deck), "Near end: where I look - moves it");
				Vector3? near = ZiplineEnds.NearFloor(zip);
				Check(ref ok, near.HasValue && (near.Value - deck).magnitude < 0.05f, "the near end's floor stands on the spot (" + (near.HasValue ? (near.Value - deck).magnitude.ToString("F3") : "?") + " m off)");
				gizmo.ClearTargets(false);
				gizmo.AddTarget(zip.transform, false);
				for (int i = 0; i < 5; i++) yield return null;
				Text ends = UnityEngine.Object.FindObjectsOfType<Text>().FirstOrDefault(x => x.name == "ZipEnds");
				Check(ref ok, ends != null && ends.text.StartsWith("Near end "), "the inspector shows the ends: " + (ends != null ? ends.text : "none"));
				gizmo.ClearTargets(false);
				CommandUndoRedo.UndoRedoManager.Undo();
				Check(ref ok, (zip.transform.position - before).magnitude < 0.01f, "one undo puts it back");
				CommandUndoRedo.UndoRedoManager.Undo();
			}
			if (ok) Log("PASS: editor gap tools"); else Fail("editor gap tools");
		}

		/// <summary>Raft's vehicles (2026-10-23, the user: "Raft's vehicles must be placeable in the editor's object browser").</summary>
		static string[] RaftVehicles { get { return PlacementOptions.Vehicles.ToArray(); } }

		[ConsoleCommand(name: "CIVehicles", docs: "Dev, editor: every Raft vehicle (vans, caravans, Varuna's machines, snowmobiles, cranes, boats, the plane) is in the object list, places on the ground, is solid, and saves and loads")]
		public static void VehiclesCommand(string[] args) { StartTest(VehiclesRoutine()); }

		static IEnumerator VehiclesRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("CIVehicles (in the editor)"); yield break; }
			bool ok = true;
			bool opened = DynamicIslands.LoadIsland(SaveSampleIsland());
			yield return null;
			yield return null;
			RemoveSampleIsland();
			if (!opened || terraineditor.terrain == null) { Fail("vehicles: the sample island didn't open"); yield break; }
			Terrain t = terraineditor.terrain;
			Vector2 c = IslandSpawner.LandCentre(SampleIsland());
			Vector3 mid = t.transform.position + new Vector3(c.x, 0f, c.y);
			float r = Mathf.Min(45f, t.terrainData.size.x / 3f);
			yield return PlaceableCatalog.EnsureLoaded(RaftVehicles);
			var listed = new HashSet<string>(PlaceableCatalog.ByCategory().SelectMany(kv => kv.Value));
			var placed = new List<string>();
			int seed = 100;
			foreach (string v in RaftVehicles)
			{
				if (!PlaceableCatalog.IsLoaded(v)) { Check(ref ok, false, v + ": in Raft's scenes"); continue; }
				Check(ref ok, listed.Contains(v), v + ": in the object list (" + PlaceableCatalog.CategoryOf(v) + ")");
				List<GameObject> one = ScatterTool.Scatter(v, mid, 1, r, 4f, 0, seed++);
				if (one.Count == 0) one = ScatterTool.Scatter(v, mid, 1, r, 0f, 0, seed++);
				if (one.Count != 1) { Check(ref ok, false, v + ": places on land"); continue; }
				GameObject go = one[0];
				Physics.SyncTransforms();
				Collider[] solid = go.GetComponentsInChildren<Collider>().Where(k => k.enabled && !k.isTrigger).ToArray();
				Renderer[] rs = PlacementOptions.ShapeRenderers(go);
				Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
				foreach (Renderer x in rs) b.Encapsulate(x.bounds);
				float low = PlacementOptions.LowestGroundUnder(go), gap = b.min.y - low;
				RaycastHit hit;
				bool hits = Physics.Raycast(new Vector3(b.center.x, b.max.y + 5f, b.center.z), Vector3.down, out hit, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore)
					&& hit.collider.transform.IsChildOf(go.transform);
				Check(ref ok, solid.Length > 0, v + ": solid (" + solid.Length + " colliders, a ray from above " + (hits ? "hits it" : "misses it") + ")");
				Check(ref ok, gap > -0.15f && gap < 0.3f, v + ": stands on the ground (bottom " + gap.ToString("F2") + " m over the lowest ground under it, size " + b.size.x.ToString("F1") + "x" + b.size.y.ToString("F1") + "x" + b.size.z.ToString("F1") + ")");
				placed.Add(v);
			}

			// Save and load keep them all
			const string file = "civehicles";
			DynamicIslands.SaveIsland(file);
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor(file));
			List<string> missing = placed.Where(v => f == null || !f.Objects.Any(o => o.Name == v)).ToList();
			Check(ref ok, placed.Count > 0 && missing.Count == 0, "saved: " + (placed.Count - missing.Count) + " of " + placed.Count + " vehicles" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing.ToArray()) + ")" : ""));
			DynamicIslands.LoadIsland(file);
			GameObject root = GameObject.Find("PlacedObjects");
			float t0 = Time.realtimeSinceStartup;
			Func<int> back = () => root == null ? 0 : placed.Count(v => root.GetComponentsInChildren<EditorGameObject>().Any(e => e.GameObjectName == v));
			while (Time.realtimeSinceStartup - t0 < 20f && back() < placed.Count) yield return null;
			Check(ref ok, back() == placed.Count, "loaded back: " + back() + " of " + placed.Count + " vehicles");
			System.IO.File.Delete(IslandSpawner.PathFor(file));
			DynamicIslands.NewIsland();
			if (ok) Log("PASS: vehicles"); else Fail("vehicles");
		}
	}
}

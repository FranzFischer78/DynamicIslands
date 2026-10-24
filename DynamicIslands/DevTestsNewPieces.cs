using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>ROADMAP LM11/LM12's rest (2026-10-06): the new things to gather and Raft's story machinery as ready pieces.</summary>
	public static partial class DevTests
	{
		/// <summary>The things to gather added for LM11's rest (the snowy pine, seaweed, and Raft's others).</summary>
		static readonly string[] NewGatherNames =
		{
			PlaceableCatalog.SnowyPine, PlaceableCatalog.Seaweed, "Pickup_Landmark_Tree_Banana",
			"Pickup_Landmark_Palmtree 1", "Pickup_Landmark_Palmtree 2", "Pickup_Landmark_Palmtree 3", "Pickup_Landmark_Palmtree 4",
			"Pickup_Landmark_Tree_AcaciaTree_Big 1", "Pickup_Landmark_Tree_AcaciaTree_Big 3", "Pickup_Landmark_Tree_Tangaroa_1", "Pickup_Landmark_Strawberry",
			"Pickup_Landmark_Mushroom 1", "Pickup_Landmark_Mushroom 2", "Pickup_Landmark_Mushroom 3", "Pickup_Landmark_Rock_2_Land",
			"Pickup_Landmark_Scrap 1_Land", "Pickup_Landmark_Scrap 2_Land", "Pickup_Landmark_Scrap 3_Land", "Pickup_Landmark_Scrap 4_Land",
			"Pickup_Landmark_Titanium_Land", "Pickup_Landmark_Plank_Land", "Pickup_Landmark_Plastic3_Land", "Pickup_Landmark_Plastic6_Land",
		};

		[ConsoleCommand(name: "CINewPieces", docs: "Dev, editor with an island open: ROADMAP LM11/LM12 - one of each new thing to gather (snowy pine, seaweed, banana tree, small palms, acacias, Tangaroa's tree, strawberries, mushrooms, finds on land) and each new ready piece (lifts, cages, camera, generators, radios, engine, mirrors) placed in the editor: each listed and placed; each thing to gather under a style in Things to gather, its world copy with Raft's pickup (the snowy pine also Raft's tree script); each piece with its settings and, as in a world, its working parts (use, mover, the lift carrying a player standing on it). One undo takes them back")]
		public static void NewPiecesCommand(string[] args) { StartTest(NewPiecesRoutine()); }

		static IEnumerator NewPiecesRoutine()
		{
			if (!DynamicIslands.InEditor() || terraineditor.terrain == null) { Fail("CINewPieces (in the editor, an island open)"); yield break; }
			bool ok = true;
			yield return PlaceableCatalog.EnsureBuilt();
			List<string> pieces = ReadyPieces.Machinery.Select(p => p.Name).ToList();
			yield return PlaceableCatalog.EnsureLoaded(NewGatherNames.Concat(pieces).ToList());
			string[] notLoaded = NewGatherNames.Concat(pieces).Where(n => PlaceableCatalog.Get(n) == null).ToArray();
			Check(ref ok, notLoaded.Length == 0, NewGatherNames.Length + " things to gather and " + pieces.Count + " pieces load" + (notLoaded.Length > 0 ? " - missing: " + string.Join(", ", notLoaded) : ""));

			Terrain t = terraineditor.terrain;
			float sea = DynamicIslands.EditorWaterLevel;
			Vector3 mid = t.transform.position + new Vector3(t.terrainData.size.x / 2f, 0f, t.terrainData.size.z / 2f);
			GameObject root = GameObject.Find("PlacedObjects");
			int before = PlacedEditorObjects().Count;
			var placed = new List<GameObject>();
			int slot = 0;
			// One by one in a grid round the island's middle (sea finds as they come: the test looks at what they are, not where)
			Func<string, EditorGameObject> place = name =>
			{
				GameObject go = PlaceableCatalog.Spawn(name, root.transform);
				if (go == null) return null;
				Vector3 at = mid + new Vector3((slot % 8) * 7f - 24.5f, 0f, (slot / 8) * 7f - 21f);
				slot++;
				Vector3 ground, normal;
				go.transform.position = PlacementOptions.GroundAt(new Vector3(at.x, sea + 200f, at.z), out ground, out normal) ? ground : new Vector3(at.x, sea, at.z);
				placed.Add(go);
				return EditorGameObject.Attach(go, name);
			};

			// LM11: the things to gather
			List<string> listed = PlaceableCatalog.GatherNames();
			var bad = new List<string>();
			foreach (string n in NewGatherNames.Where(x => PlaceableCatalog.Get(x) != null))
			{
				EditorGameObject e = place(n);
				GameObject proto = PlaceableCatalog.Get(n);
				bool tree = n == PlaceableCatalog.SnowyPine; // (Raft's pine underneath: cut with the axe)
				string why = e == null ? "not placed" : !listed.Contains(n) || PlaceableCatalog.CategoryOf(n) != PlaceableCatalog.HarvestableCategory ? "not under Things to gather"
					: GenGather.StylesOf(n).Contains(GenGather.Styles[GenGather.Styles.Length - 1]) ? "in no style group"
					: proto.GetComponentInChildren<PickupItem_Networked>(true) == null ? "no Raft pickup in its world copy"
					: tree && proto.GetComponentInChildren<HarvestableTree>(true) == null ? "no tree script (axe)"
					: n != PlaceableCatalog.Seaweed && e.GetComponentInChildren<PickupItem>(true) != null ? "the editor copy kept Raft's scripts" : null;
				if (why != null) bad.Add(PlaceableCatalog.DisplayName(n) + ": " + why);
			}
			Check(ref ok, bad.Count == 0, "each new thing to gather placed, under a style in Things to gather, its world copy picked or cut as Raft's" + (bad.Count > 0 ? " - " + string.Join("; ", bad.ToArray()) : ""));
			GameObject pine = PlaceableCatalog.Get(PlaceableCatalog.SnowyPine);
			Transform look = pine != null ? pine.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "CI_SnowyLook") : null;
			Check(ref ok, look != null && look.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled) &&
				pine.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(look) && (r is MeshRenderer || r is SkinnedMeshRenderer)).All(r => !r.enabled),
				"the snowy pine wears Temperance's snowy pine, Raft's pine's own look off");
			Check(ref ok, GenGather.StylesOf(PlaceableCatalog.SnowyPine).Contains(GenGather.Styles[1]) && GenGather.StylesOf("Pickup_Landmark_Tree_AcaciaTree_Big 1").Contains(GenGather.Styles[2]) &&
				GenGather.StylesOf("Pickup_Landmark_Flower_Black").Contains(GenGather.Styles[4]) && GenGather.StylesOf(PlaceableCatalog.Seaweed).Contains(GenGather.Styles[5]),
				"grouped by style: snowy pine - snowy, acacia - desert, black flower - volcanic, seaweed - sea finds");

			// LM12: the ready pieces, as placed, then as a world gives them their working parts
			var badPieces = new List<string>();
			foreach (ReadyPieces.Piece p in ReadyPieces.Machinery.Where(x => PlaceableCatalog.Get(x.Name) != null))
			{
				EditorGameObject e = place(p.Name);
				if (e == null) { badPieces.Add(p.Name + ": not placed"); continue; }
				Dictionary<string, string> props = e.Props;
				string checks = ObjectProps.Get(props, BehaviourProps.CheckKey("use")), use = ObjectProps.Get(props, BehaviourProps.EventKey("use"));
				string why = null;
				switch (p.Kind)
				{
					case ReadyPieces.Lift: if (!BehaviourProps.Switches(props) || !ObjectProps.GetBool(props, BehaviourProps.Carry, false) || BehaviourProps.Offset(props).y <= 0f) why = "no lift settings"; break;
					case ReadyPieces.Camera: if (ObjectProps.Get(props, BehaviourProps.MoveMode) != "loop" || ObjectProps.GetFloat(props, BehaviourProps.Turn, 0f) == 0f) why = "doesn't sweep"; break;
					case ReadyPieces.Cage: if (!checks.Contains("has|" + StoryItems.Ref(ReadyPieces.ItemOf(p.Name))) || !use.Contains("hide|")) why = "doesn't want the bolt cutters"; break;
					case ReadyPieces.Generator: if (!checks.Contains("signal|" + ReadyPieces.PowerSignal) || !checks.Contains("take|" + StoryItems.Ref(ReadyPieces.ItemOf(p.Name))) || !use.Contains("signal||" + ReadyPieces.PowerSignal)) why = "no power settings"; break;
					case ReadyPieces.Engine: if (!checks.Contains("take|" + StoryItems.Ref(ReadyPieces.ItemOf(p.Name))) || !use.Contains("signal||" + ReadyPieces.EngineSignal)) why = "no fuel settings"; break;
					case ReadyPieces.Radio: if (!checks.Contains("signal|" + ReadyPieces.PowerSignal) || !use.Contains("signal||" + ReadyPieces.RadioSignal)) why = "no radio settings"; break;
					case ReadyPieces.Mirror:
					case ReadyPieces.Wheel:
					case ReadyPieces.Pipe: if (ObjectProps.GetFloat(props, BehaviourProps.Turn, 0f) == 0f || !use.Contains("switch|") || !use.Contains("signal||" + p.Kind)) why = "doesn't turn"; break;
					case ReadyPieces.Wire: if (!checks.Contains("state||open") || !checks.Contains("take|" + StoryItems.Ref(ReadyPieces.ItemOf(p.Name))) || !use.Contains("open|") || !use.Contains("signal||" + ReadyPieces.WireSignal)) why = "no wire settings"; break;
					case ReadyPieces.Claw:
					case ReadyPieces.Scales: if (!use.Contains("signal||" + p.Kind)) why = "sends no signal"; break;
					case ReadyPieces.Laser: if (ObjectProps.Get(props, LaserBeam.Prop) != LaserBeam.Beam) why = "no beam"; break;
					case ReadyPieces.FixedMirror: if (ObjectProps.Get(props, LaserBeam.Prop) != LaserBeam.Mirror) why = "doesn't send the beam on"; break;
					case ReadyPieces.LaserTarget: if (!ObjectProps.Get(props, BehaviourProps.EventKey(LaserBeam.Event)).Contains("signal||" + ReadyPieces.LaserSignal)) why = "doesn't answer the beam"; break;
				}
				// (a copy as an island in a world makes it: the settings give it its parts)
				GameObject live = PlaceableCatalog.Spawn(p.Name, null);
				if (live != null)
				{
					live.transform.position = e.transform.position;
					Behaviours.Attach(live, p.Name, props, 0);
					Physics.SyncTransforms();
					IslandBehaviour mover = live.GetComponent<IslandBehaviour>();
					bool usable = live.GetComponentInChildren<UseInteract>(true) != null;
					if (why == null && p.Use != null && !usable) why = "players can't use it";
					if (why == null && (p.Kind == ReadyPieces.Lift || p.Kind == ReadyPieces.Camera || ReadyPieces.Turns(p.Kind)) && mover == null) why = "doesn't move";
					if (why == null && p.Kind == ReadyPieces.Lift)
					{
						// (someone standing on its floor rides along: a ray down from them meets the lift)
						Bounds b;
						var feet = new GameObject("CI_Feet").transform;
						feet.position = CustomNote.LocalBounds(live, out b) ? live.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z)) + Vector3.up * 0.05f : live.transform.position + Vector3.up;
						// (a cabin - Raft's elevator: its floor is at the bottom, under the roof - the lowest of its surfaces under the top)
						if (mover.Carry && !mover.Carries(feet))
						{
							RaycastHit[] down = Physics.RaycastAll(feet.position + Vector3.up * 0.5f, Vector3.down, b.size.y + 2f, ~0, QueryTriggerInteraction.Ignore)
								.Where(h => h.collider != null && h.collider.transform.IsChildOf(live.transform)).ToArray();
							if (down.Length > 0) feet.position = down.OrderBy(h => h.point.y).First().point + Vector3.up * 0.05f;
						}
						if (!mover.Carry || !mover.Carries(feet))
						{
							Collider[] cs = live.GetComponentsInChildren<Collider>(true);
							why = "a player standing on it isn't carried (no floor under them: " + cs.Count(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger) + " of " + cs.Length +
								" colliders solid, feet at " + (feet.position - live.transform.position).ToString("F1") + ", " + string.Join(", ", cs.Take(6).Select(c => c.name + (c.enabled ? "" : " off") + (c.isTrigger ? " trigger" : "")).ToArray()) + ")";
						}
						UnityEngine.Object.DestroyImmediate(feet.gameObject);
					}
					UnityEngine.Object.DestroyImmediate(live);
				}
				else if (why == null) why = "no copy for a world";
				if (why != null) badPieces.Add(p.Name + ": " + why);
			}
			Check(ref ok, badPieces.Count == 0, ReadyPieces.Machinery.Count() + " pieces placed with their working settings and parts" + (badPieces.Count > 0 ? " - " + string.Join("; ", badPieces.ToArray()) : ""));
			Check(ref ok, PlacedEditorObjects().Count == before + placed.Count, "each an editor object (" + placed.Count + " placed)");
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(placed, true));
			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, PlacedEditorObjects().Count == before, "one undo takes them back");
			if (ok) Log("PASS: new pieces"); else Fail("new pieces");
		}

		[ConsoleCommand(name: "CIGenMachinery", docs: "Dev, editor: ROADMAP LM12 - the generator's Raft's features past six (10 on a hilly tropical island): a generator with its part in a toolbox, a radio that shows a cache, an engine with fuel in a crate, a chest in a cage with the bolt cutters in the explorer's chest, a lift up a cliff; 6 features give none of them")]
		public static void GenMachineryCommand(string[] args) { StartTest(GenMachineryRoutine()); }

		static IEnumerator GenMachineryRoutine()
		{
			yield return WaitForEditor(false);
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			var gs = new IslandGenSettings { Seed = 5252, Radius = 90f, Height = 34f, Peaks = 3, Shape = IslandShapes.Plateau, Terraces = 0.8f, ObjectDensity = 0.3f, Style = TerrainPainter.Tropical, Features = GenFeatures.Max };
			yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(gs));
			IslandGenerator.GenerateInEditor(gs);
			yield return new WaitForSecondsRealtime(0.5f);
			List<EditorGameObject> objs = PlacedEditorObjects();
			Func<string, EditorGameObject> one = n => objs.FirstOrDefault(e => e.GameObjectName == n);
			Func<string, string, bool> chestWith = (title, item) => objs.Any(e => ObjectProps.Get(e.Props, ObjectProps.NoteTitle) == title && ObjectProps.Get(e.Props, ObjectProps.LootItems).Contains(item));
			string report = string.Join(", ", IslandGenerator.LastReport.Built.ToArray());
			EditorGameObject radio = one("RT_CommRadio"), engine = one("VG_DecorationPrefabBase_Engine Variant"), cage = one("RT_SharkCage"), lift = one("VP_Skylift");
			Check(ref ok, one("VG_DecorationPrefabBase_EmergencyGenerator Variant") != null && radio != null && ObjectProps.Get(radio.Props, BehaviourProps.EventKey("use")).Contains("show|radiocache") &&
				chestWith("Mechanic's toolbox", StoryItems.Ref(ReadyPieces.ItemOf("VG_DecorationPrefabBase_EmergencyGenerator Variant"))), "a generator, a radio showing a cache, the part in a toolbox");
			Check(ref ok, engine != null && chestWith("Fuel crate", StoryItems.Ref(ReadyPieces.ItemOf("VG_DecorationPrefabBase_Engine Variant"))), "an engine, its fuel in a crate");
			Check(ref ok, cage != null && chestWith("Explorer's chest", StoryItems.Ref(ReadyPieces.ItemOf("RT_SharkCage"))) && objs.Any(e => ObjectProps.Get(e.Props, BehaviourProps.Name).StartsWith("cagechest") && BehaviourProps.StartsHidden(e.Props)),
				"a hidden chest in a cage, the bolt cutters in the explorer's chest");
			Check(ref ok, lift != null && BehaviourProps.Offset(lift.Props).y >= 2.5f && ObjectProps.GetBool(lift.Props, BehaviourProps.Carry, false), "a lift up a cliff" + (lift != null ? " (" + BehaviourProps.Offset(lift.Props).y.ToString("F1") + " m)" : ""));
			Check(ref ok, report.Contains("generator") && report.Contains("vines"), "the report: " + report);
			var six = new IslandGenSettings { Seed = 5252, Radius = 90f, Height = 34f, Peaks = 3, ObjectDensity = 0.3f, Style = TerrainPainter.Tropical, Features = 6 };
			IslandGenerator.GenerateInEditor(six);
			yield return new WaitForSecondsRealtime(0.4f);
			Check(ref ok, !PlacedEditorObjects().Any(e => GenFeatures.MoreNames.Contains(e.GameObjectName)), "6 features: none of the machinery (islands as before)");
			if (ok) Log("PASS: generator machinery"); else Fail("generator machinery");
		}
	}
}

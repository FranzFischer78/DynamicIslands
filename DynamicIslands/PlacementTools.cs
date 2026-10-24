using System.Collections.Generic;
using System.Linq;
using CommandUndoRedo;
using RuntimeGizmos;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>Editor options for placing objects (Objects tab buttons "Random" and "Slope").</summary>
	public static class PlacementOptions
	{
		/// <summary>Each placed object gets a random turn and a size between 80 and 125 %.</summary>
		public static bool RandomTurnAndSize;
		/// <summary>Placed and grounded objects lean with the slope of the ground instead of standing straight up.</summary>
		public static bool AlignToSlope;
		/// <summary>Positions snap to Raft's 1.5 m building grid and Q/E turn in 90° steps (for Raft blocks).</summary>
		public static bool SnapToGrid;
		public const float GridSize = 1.5f;
		/// <summary>How far down a floating object may go (m below the sea).</summary>
		public const float FloatDepth = 0.35f;
		/// <summary>A floating foundation's own height above the sea, and the deck a player walks on above it - measured on the
		/// player's raft (CIRaftDeckProbe: the block at 0.13 m, Raft's raft collider 0.22 m higher).</summary>
		public const float FoundationFloat = 0.13f, FoundationTop = 0.22f;
		/// <summary>Where Raft's walls, pillars and things stand on a Block_Foundation: its planks, whose top is at the block's
		/// pivot (its mesh's top 0.001 m over it; kit_found, 2026-10-03) - a wall's foot is 0.04 m under its own pivot, so 0.03 m
		/// over the block's sets it on the planks. At FoundationTop they hung 0.17 m in the air (the user saw a generated hut's
		/// walls float). Players still walk on FoundationTop: Raft's raft collider, the islands' CustomIslands_Deck.</summary>
		public const float FoundationPlanks = 0.03f;

		/// <summary>Grid-snapped position (x/z only) when "Grid" is on.</summary>
		public static Vector3 Snap(Vector3 p)
		{
			if (!SnapToGrid) return p;
			return new Vector3(Mathf.Round(p.x / GridSize) * GridSize, p.y, Mathf.Round(p.z / GridSize) * GridSize);
		}

		/// <summary>Raft's building blocks float: over water they sit at the sea surface instead of on the seabed.</summary>
		public static Vector3 FloatIfBlock(string objectName, Vector3 p)
		{
			if (PlaceableCatalog.CategoryOf(objectName) != PlaceableCatalog.RaftBlocksCategory) return p;
			float sea = (terraineditor.terrain != null ? terraineditor.terrain.transform.position.y : 0f) + DynamicIslands.EditorWaterLevel;
			if (p.y < sea + FoundationFloat) p.y = sea + FoundationFloat;
			return p;
		}

		/// <summary>The editor terrain's surface below a point (ignores objects). False if there is no terrain there.</summary>
		public static bool GroundAt(Vector3 position, out Vector3 point, out Vector3 normal)
		{
			point = position; normal = Vector3.up;
			Terrain terrain = terraineditor.terrain;
			TerrainCollider collider = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
			RaycastHit hit;
			if (collider == null || !collider.Raycast(new Ray(new Vector3(position.x, 5000f, position.z), Vector3.down), out hit, 10000f)) return false;
			point = hit.point; normal = hit.normal;
			return true;
		}

		/// <summary>
		/// The renderers that make an object's shape: its meshes (on, not the editor's marker) - not a particle effect, a
		/// rope's line or a trail, whose bounds can reach 100 m: an anchor and a receiver aerial set down by them hung 100 m and
		/// 50 m over the Abyss's raft, a sprinkler 20 m over Tide Farm's glasshouse (2026-10-02).
		/// </summary>
		public static Renderer[] ShapeRenderers(GameObject go)
		{
			// (its plain meshes; skinned ones only when it has nothing else - the anchor's rope and the aerial's wire are skinned,
			// and with their joints' scripts gone their bones drift: those boxes were the 200 m and 100 m ones)
			Renderer[] meshes = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r is MeshRenderer && r.name != ContentCatalog.MarkerOnly).ToArray();
			return meshes.Length > 0 ? meshes : go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r is SkinnedMeshRenderer && r.name != ContentCatalog.MarkerOnly).ToArray();
		}

		/// <summary>
		/// The lowest ground under an object's base - under the parts of it that reach down to its bottom (a stilt house's
		/// legs, a van's wheels, the underside of a rock or a bush), not just under its pivot: set down by its pivot on a
		/// slope, its low side stood in the air. The terrain only (an object on another object is put there by hand).
		/// </summary>
		public static float LowestGroundUnder(GameObject go)
		{
			Vector3 point, normal;
			float pivot = GroundAt(go.transform.position, out point, out normal) ? point.y : go.transform.position.y;
			Renderer[] rs = ShapeRenderers(go);
			if (rs.Length == 0) return pivot;
			float bottom = rs.Min(r => r.bounds.min.y);
			Bounds foot = new Bounds();
			bool any = false;
			foreach (Renderer r in rs)
			{
				if (r.bounds.min.y > bottom + 0.6f) continue;
				if (!any) { foot = r.bounds; any = true; } else foot.Encapsulate(r.bounds);
			}
			// (the ground under its pivot only counts when the pivot is over its base: some of Raft's scene objects have their
			// pivot tens of metres away - a stilt house's over the sea sank it 15 m into its shelf)
			Vector3 at = go.transform.position;
			bool pivotOverBase = at.x >= foot.min.x && at.x <= foot.max.x && at.z >= foot.min.z && at.z <= foot.max.z;
			return pivotOverBase ? Mathf.Min(pivot, LowestGroundUnder(foot)) : LowestGroundUnder(foot);
		}

		/// <summary>
		/// Where an object's pivot goes when it is set down on the ground: at the lowest ground under its base - and, when
		/// its pivot is well above its bottom (Raft's stranded boat has it in its middle, 3.4 m up), that much higher, so its
		/// bottom rests there. Set down by its pivot, the boat sank whole under a beach and the locker on its deck hung in
		/// the air (the user, 2026-10-02). An object whose pivot is at or near its base - most; a tree's roots or a rock's
		/// underside reaching a little below it belong in the ground - is set down by its pivot as before.
		/// </summary>
		public static float RestingPivotY(GameObject go)
		{
			float low = LowestGroundUnder(go);
			return low + PivotAboveBottom(go);
		}

		/// <summary>How far an object's pivot is above its bottom when that is more than a base's own depth (more than 1 m and
		/// a third of its height): the lift RestingPivotY gives it; 0 for an object set down by its pivot.</summary>
		public static float PivotAboveBottom(GameObject go)
		{
			Renderer[] rs = ShapeRenderers(go);
			if (rs.Length == 0) return 0f;
			float bottom = rs.Min(r => r.bounds.min.y), top = rs.Max(r => r.bounds.max.y);
			float below = go.transform.position.y - bottom;
			// (a vehicle rests on its wheels or hull exactly: Utopia's crane has its pivot 2.5 m up and sank that far, Varuna's
			// excavator its pivot under its tracks and hung 0.8 m in the air - CIVehicles, 2026-10-23)
			if (Vehicles.Contains(go.name)) return below;
			return below > Mathf.Max(1f, (top - bottom) / 3f) ? below : 0f;
		}

		/// <summary>Raft's vehicles (the user, 2026-10-23: they must be placeable in the editor): Caravan Town's vans,
		/// caravans, mini boat and rocket, Varuna Point's machines, Temperance's snowmobiles, Utopia's cranes, the radio
		/// tower's plastic boat, the pilot island's plane and the stranded boat. Each stays in its town's category of the
		/// object list; placed, it rests on its bottom and is solid (MakeSolid).</summary>
		public static readonly HashSet<string> Vehicles = new HashSet<string> {
			"Van_1", "Van_2", "Van3", "Van_4", "Van_5", "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Green_02", "Caravan_Yellow_01",
			"CaravanBarebones1", "BoatMini", "CaravanRocket", "VP_Excavator", "VP_Forklift", "VP_ApexCrane", "VP_Wagon01", "VP_Wagon02",
			"TP_SnowVehicle01", "TP_SnowVehicle02", "UT_ElectricityIsland_Crane01", "UT_ElectricityIsland_Crane02", "RT_PlasticBoat",
			"Airplane", "BoatStranded" };

		/// <summary>A vehicle without a solid collider of its own (Caravan Town's mini boat and rocket: Raft's copies stand out
		/// of reach) gets a box on each of its meshes, so players stand on it and bump into it.</summary>
		public static void MakeSolid(GameObject go)
		{
			if (go.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger)) return;
			foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
			{
				if (mf.sharedMesh == null) continue;
				BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
				box.center = mf.sharedMesh.bounds.center;
				box.size = mf.sharedMesh.bounds.size;
			}
		}

		/// <summary>The lowest terrain under a footprint (its edges kept 12 % in: a base's rounded rim, a crown hanging over).</summary>
		public static float LowestGroundUnder(Bounds foot)
		{
			float low = float.MaxValue;
			int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(foot.size.x, foot.size.z) / 1.5f), 1, 8);
			for (int i = 0; i <= n; i++)
				for (int j = 0; j <= n; j++)
				{
					var p = new Vector3(Mathf.Lerp(foot.min.x + foot.size.x * 0.12f, foot.max.x - foot.size.x * 0.12f, i / (float)n), 0f,
						Mathf.Lerp(foot.min.z + foot.size.z * 0.12f, foot.max.z - foot.size.z * 0.12f, j / (float)n));
					Vector3 point, normal;
					if (GroundAt(p, out point, out normal) && point.y < low) low = point.y;
				}
			return low == float.MaxValue ? foot.min.y : low;
		}

		/// <summary>Leans up to this much (degrees) are taken off when an object is placed (Straight).</summary>
		public const float StraightenUpTo = 25f;

		/// <summary>
		/// An object of Raft's islands spawns turned and leaning as the copy found in Raft's scene stood - a ladder leant on
		/// a wall 14°. Placed, an object that stands almost straight stands exactly straight: the axis of its own that
		/// points most nearly up is turned to point straight up, its heading kept (headingToo: also turned to face north,
		/// its own axes lined up with the world's - the recipes build with them). A bigger lean (a boulder lying on its
		/// side) is the object's look and is kept.
		/// </summary>
		public static Quaternion Straight(Quaternion r, bool headingToo = false)
		{
			Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward, Vector3.left, Vector3.down, Vector3.back };
			Vector3 upAxis = axes.OrderByDescending(a => Vector3.Dot(r * a, Vector3.up)).First();
			if (Vector3.Angle(r * upAxis, Vector3.up) > StraightenUpTo) return r;
			Quaternion s = Quaternion.FromToRotation(r * upAxis, Vector3.up) * r;
			if (!headingToo) return s;
			// (the heading: a level axis of the object's own - z, or x when z is its up axis - turned to face north / east)
			bool zUp = Mathf.Abs(upAxis.z) > 0.5f;
			Vector3 level = s * (zUp ? Vector3.right : Vector3.forward);
			float heading = Mathf.Atan2(level.x, level.z) * Mathf.Rad2Deg - (zUp ? 90f : 0f);
			return Quaternion.Euler(0f, -heading, 0f) * s;
		}

		/// <summary>Rotation for an object standing on ground with this normal: its turn around the vertical, tilted with the slope if wanted.</summary>
		public static Quaternion Upright(float yaw, Quaternion baseRotation, Vector3 normal)
		{
			Quaternion turn = Quaternion.Euler(0f, yaw, 0f) * baseRotation;
			return AlignToSlope ? Quaternion.FromToRotation(Vector3.up, normal) * turn : turn;
		}

		/// <summary>Puts the selected objects on the terrain surface (and tilts them with the slope if "Slope" is on), as one undo step.</summary>
		public static int DropSelectionToGround()
		{
			TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			if (gizmo == null) return 0;
			var group = new CommandGroup();
			int n = 0;
			foreach (Transform t in gizmo.SelectedRoots.Where(t => t != null).ToList())
			{
				Vector3 point, normal;
				if (!GroundAt(t.position, out point, out normal)) continue;
				var command = new TransformCommand(gizmo, t);
				t.position = point;
				// (standing straight, all of its base on the ground: down to the lowest ground under it - on a slope the low
				// side of a house on legs, a van or a rock stood in the air)
				if (!AlignToSlope) t.position = new Vector3(point.x, RestingPivotY(t.gameObject), point.z);
				if (AlignToSlope)
				{
					// Keep the object's turn around its own up axis, lean it with the ground
					Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
					if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
					t.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.LookRotation(forward.normalized, Vector3.up);
				}
				command.StoreNewTransformValues();
				group.Add(command);
				n++;
			}
			if (n > 0)
			{
				UndoRedoManager.Insert(group);
				gizmo.SetPivotPoint();
			}
			return n;
		}

		/// <summary>
		/// The placed object (its EditorGameObject root) under a ray, or null for the terrain, the sea or nothing.
		/// Objects are found by their colliders, else (decorations without colliders, colliders on other layers) by their meshes' bounds.
		/// </summary>
		public static Transform PickObject(Ray ray, LayerMask mask)
		{
			float terrainDistance = float.MaxValue;
			foreach (RaycastHit hit in Physics.RaycastAll(ray, 5000f, mask).OrderBy(h => h.distance))
			{
				if (hit.collider.GetComponent<Terrain>() != null) { terrainDistance = hit.distance; break; }
				EditorGameObject owner = hit.collider.GetComponentInParent<EditorGameObject>();
				if (owner != null && owner.gameObject.activeInHierarchy) { if (SelectionTools.IsLocked(owner)) continue; return owner.transform; }
			}
			GameObject placedRoot = GameObject.Find("PlacedObjects");
			if (placedRoot == null) return null;
			Transform best = null;
			float bestDistance = terrainDistance;
			foreach (EditorGameObject o in placedRoot.GetComponentsInChildren<EditorGameObject>(false))
			{
				if (!SelectionTools.Pickable(o)) continue; // (hidden or locked - ROADMAP E3)
				foreach (Renderer r in o.GetComponentsInChildren<Renderer>())
				{
					float d;
					if (r.enabled && r.name != ContentCatalog.MarkerOnly && !(r is ParticleSystemRenderer) && r.bounds.IntersectRay(ray, out d) && d < bestDistance) { bestDistance = d; best = o.transform; }
				}
			}
			return best;
		}

		/// <summary>
		/// Copies the selected objects (next to the originals: one grid step with Grid on, else 2 m) as one undo step,
		/// and selects the copies so they can be moved straight away.
		/// </summary>
		public static int DuplicateSelection()
		{
			TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			GameObject placedRoot = GameObject.Find("PlacedObjects");
			if (gizmo == null || placedRoot == null) return 0;
			Vector3 offset = SnapToGrid ? new Vector3(GridSize, 0, 0) : new Vector3(2f, 0, 0);
			if (!ObjectLimit.Allow(gizmo.SelectedRoots.Count(t => t != null))) return 0;
			var copies = new List<GameObject>();
			foreach (Transform t in gizmo.SelectedRoots.Where(t => t != null).ToList())
			{
				EditorGameObject info = t.GetComponent<EditorGameObject>();
				if (info == null) continue;
				GameObject copy = PlaceableCatalog.Spawn(info.GameObjectName, placedRoot.transform);
				// (a red box: an object this Raft version doesn't have - nothing to copy; it did nothing and said nothing)
				if (copy == null) { DynamicIslands.Notify("'" + info.GameObjectName + "' isn't in this Raft version: it can't be duplicated", true); continue; }
				copy.transform.position = t.position + offset;
				copy.transform.rotation = t.rotation;
				copy.transform.localScale = t.lossyScale;
				foreach (Collider c in copy.GetComponentsInChildren<Collider>()) c.enabled = true;
				EditorGameObject.Attach(copy, info.GameObjectName, info.Props); // a copy keeps the settings (creature stats, note text, tint)
				copies.Add(copy);
			}
			if (copies.Count == 0) return 0;
			UndoRedoManager.Insert(new ObjectVisibilityCommand(copies, true));
			gizmo.ClearTargets(false);
			foreach (GameObject c in copies) gizmo.AddTarget(c.transform, false);
			return copies.Count;
		}

		/// <summary>
		/// The editor's clipboard (ROADMAP E4): objects copied with Copy (Ctrl+C), around their middle at the lowest one's
		/// height, with their settings. It stays while another island is opened, so Paste puts them on that one.
		/// </summary>
		static readonly List<GroupLibrary.Member> clipboard = new List<GroupLibrary.Member>();
		public static int ClipboardCount { get { return clipboard.Count; } }

		/// <summary>Copy: the selected objects to the clipboard. Returns how many.</summary>
		public static int CopySelection()
		{
			TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			if (gizmo == null) return 0;
			List<EditorGameObject> list = gizmo.SelectedRoots.Where(t => t != null).Select(t => t.GetComponent<EditorGameObject>()).Where(e => e != null).ToList();
			if (list.Count == 0) return 0;
			Vector3 pivot = new Vector3(list.Average(o => o.transform.position.x), list.Min(o => o.transform.position.y), list.Average(o => o.transform.position.z));
			clipboard.Clear();
			foreach (EditorGameObject o in list)
				clipboard.Add(new GroupLibrary.Member { Name = o.GameObjectName, Position = o.transform.position - pivot, Euler = o.transform.rotation.eulerAngles, Scale = o.transform.lossyScale,
					Props = new Dictionary<string, string>(o.Props ?? new Dictionary<string, string>()) });
			return clipboard.Count;
		}

		/// <summary>
		/// Paste: the clipboard's objects put down where the mouse points on the ground (atMouse; Ctrl+V) or where the
		/// middle of the screen does (the button), selected and one undo step. Returns how many.
		/// </summary>
		public static int PasteClipboard(bool atMouse)
		{
			TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			GameObject placedRoot = GameObject.Find("PlacedObjects");
			Camera cam = Camera.main;
			if (gizmo == null || placedRoot == null || cam == null || clipboard.Count == 0) return 0;
			if (!ObjectLimit.Allow(clipboard.Count)) return 0;
			Ray ray = atMouse ? cam.ScreenPointToRay(Input.mousePosition) : cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
			Vector3 at = cam.transform.position + cam.transform.forward * 20f;
			RaycastHit hit;
			TerrainCollider tc = terraineditor.terrain != null ? terraineditor.terrain.GetComponent<TerrainCollider>() : null;
			if (tc != null && tc.Raycast(ray, out hit, 5000f)) at = hit.point;
			var copies = new List<GameObject>();
			foreach (GroupLibrary.Member m in clipboard)
			{
				GameObject copy = PlaceableCatalog.Spawn(m.Name, placedRoot.transform);
				if (copy == null) { DynamicIslands.Notify("'" + m.Name + "' isn't in this Raft version: it can't be pasted", true); continue; }
				copy.transform.position = at + m.Position;
				copy.transform.rotation = Quaternion.Euler(m.Euler);
				copy.transform.localScale = m.Scale;
				foreach (Collider c in copy.GetComponentsInChildren<Collider>()) c.enabled = true;
				EditorGameObject.Attach(copy, m.Name, new Dictionary<string, string>(m.Props));
				copies.Add(copy);
			}
			if (copies.Count == 0) return 0;
			UndoRedoManager.Insert(new ObjectVisibilityCommand(copies, true));
			gizmo.ClearTargets(false);
			foreach (GameObject c in copies) gizmo.AddTarget(c.transform, false);
			return copies.Count;
		}
	}

}

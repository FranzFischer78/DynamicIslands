using System;
using System.Collections.Generic;
using System.Linq;
using CommandUndoRedo;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The editor's scatter (ROADMAP LM11, the user 2026-10-05): many copies of one object - a thing to gather, a bush, a
	/// rock - spread at random round a point, on land or under water, never within a few metres of anything already on the
	/// island (its buildings, its quest's objects), each set down on the ground as the placer does. One undo takes them all
	/// back. The recipes' "scatter ... free=" does the same for the library's islands.
	/// </summary>
	public static class ScatterTool
	{
		/// <summary>Raft's finds under water (sand, clay, stone and ores lie on the sea floor too; these only there).</summary>
		public static bool OnlyUnderWater(string name)
		{
			return name.EndsWith("_OceanBottom") || name.Contains("GiantClam") || name.Contains("SilverAlgae") || name.StartsWith("SeaVine") || name.StartsWith("Seavine");
		}

		/// <summary>Places up to count copies of name within radius of centre: under water (underWater) or on land, at least
		/// free metres from any object already placed. Returns the placed objects (fewer when the area has no room).</summary>
		public static List<GameObject> Scatter(string name, Vector3 centre, int count, float radius, float free, bool underWater, int seed)
		{
			return Scatter(name, centre, count, radius, free, underWater ? 1 : 0, seed);
		}

		/// <summary>Where scattered copies may go (where: 0 on land, 1 under water, 2 anywhere - a recipe's default, "wet", "any").</summary>
		public static readonly string[] WhereNames = { "On land", "Under water", "Anywhere" };

		/// <summary>As above, with where (0 land, 1 under water, 2 anywhere). The same seed gives the same spots, as a
		/// recipe's "scatter ... seed=" does.</summary>
		public static List<GameObject> Scatter(string name, Vector3 centre, int count, float radius, float free, int where, int seed)
		{
			bool underWater = where == 1;
			var made = new List<GameObject>();
			if (!DynamicIslands.InEditor() || string.IsNullOrEmpty(name) || count <= 0) return made;
			if (!ObjectLimit.Allow(count)) return made;
			GameObject rootObject = GameObject.Find("PlacedObjects");
			if (rootObject == null) return made;
			float sea = DynamicIslands.EditorWaterLevel;
			var rnd = new System.Random(seed);
			// (also by their pivots: plants and small things without a collider count too)
			List<Vector2> there = rootObject.GetComponentsInChildren<EditorGameObject>(false).Select(e => new Vector2(e.transform.position.x, e.transform.position.z)).ToList();
			for (int k = 0; k < count * 80 && made.Count < count; k++)
			{
				double ang = rnd.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rnd.NextDouble()) * radius;
				var at = new Vector3(centre.x + (float)(Math.Cos(ang) * dist), 0f, centre.z + (float)(Math.Sin(ang) * dist));
				Vector3 ground, normal;
				if (!PlacementOptions.GroundAt(new Vector3(at.x, sea + 200f, at.z), out ground, out normal)) continue;
				if (where != 2 && (underWater ? ground.y > sea - 0.6f : ground.y < sea + 0.3f)) continue;
				if (underWater && ground.y < sea - 40f) continue;
				Physics.SyncTransforms();
				if (free > 0f && Physics.OverlapSphere(ground + Vector3.up, free, ~0, QueryTriggerInteraction.Ignore).Any(c => c.GetComponentInParent<EditorGameObject>() != null)) continue;
				var flat = new Vector2(ground.x, ground.z);
				if (free > 0f && there.Any(p => (p - flat).sqrMagnitude < free * free)) continue;
				GameObject go = PlaceableCatalog.Spawn(name, rootObject.transform);
				if (go == null) break;
				go.transform.rotation = Quaternion.Euler(0f, (float)(rnd.NextDouble() * 360.0), 0f) * PlacementOptions.Straight(go.transform.rotation, true);
				go.transform.position = ground;
				float y = Mathf.Min(ground.y, PlacementOptions.LowestGroundUnder(go));
				go.transform.position = new Vector3(ground.x, y + PlacementOptions.PivotAboveBottom(go), ground.z);
				Collider own = go.GetComponent<Collider>();
				if (own != null) own.enabled = true;
				EditorGameObject.Attach(go, name);
				made.Add(go);
				there.Add(new Vector2(ground.x, ground.z));
			}
			if (made.Count > 0) UndoRedoManager.Insert(new ObjectVisibilityCommand(made, true));
			return made;
		}

		/// <summary>The point the editor's camera looks at (on the ground or the sea floor), or null.</summary>
		public static Vector3? ViewCentre()
		{
			Camera cam = Camera.main;
			if (cam == null) return null;
			RaycastHit hit;
			Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
			if (Physics.Raycast(ray, out hit, 2000f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
			// (looking at open water: where the ray meets the sea's level)
			float sea = DynamicIslands.EditorWaterLevel;
			if (Mathf.Abs(ray.direction.y) > 0.01f)
			{
				float t = (sea - ray.origin.y) / ray.direction.y;
				if (t > 0f) return ray.origin + ray.direction * t;
			}
			return null;
		}
	

		public const string AirPocketObject = "AirPocket_1";

		/// <summary>The Air pocket button (Objects tab): loads Raft's air pocket if needed, then puts the set down at the
		/// middle of the view.</summary>
		public static void PlaceAirPocket()
		{
			if (PlaceableCatalog.IsLoaded(AirPocketObject)) { ReportAirPocket(AirPocketAt(ViewCentre())); return; }
			DynamicIslands.instance.StartCoroutine(LoadAirPocket());
		}

		static System.Collections.IEnumerator LoadAirPocket()
		{
			DynamicIslands.Notify("Loading Raft's air pocket...");
			yield return PlaceableCatalog.EnsureLoaded(new[] { AirPocketObject });
			ReportAirPocket(AirPocketAt(ViewCentre()));
		}

		static void ReportAirPocket(List<GameObject> placed)
		{
			if (placed.Count > 0) DynamicIslands.Notify("Air pocket placed: Raft's air pocket, a zone with Air on, bubbles - Ctrl+Z takes them back");
			else DynamicIslands.Notify("Look at the sea floor where the air pocket should go, then click Air pocket again", true);
		}

		/// <summary>lib_frozen's airpocket macro: AirPocket_1 (0.6 size) on the ground, a trigger zone 1.6 m up (radius
		/// 2.5, Air on, every time, a message) and a bubbles atmosphere zone 1.2 m up (radius 3). One undo step.</summary>
		public static List<GameObject> AirPocketAt(Vector3? at)
		{
			var list = new List<GameObject>();
			GameObject rootObject = GameObject.Find("PlacedObjects");
			if (at == null || rootObject == null || !DynamicIslands.InEditor() || !ObjectLimit.Allow(3)) return list;
			Vector3 ground, normal;
			float gy = PlacementOptions.GroundAt(new Vector3(at.Value.x, DynamicIslands.EditorWaterLevel + 200f, at.Value.z), out ground, out normal) ? ground.y : at.Value.y;
			var parts = new[]
			{
				new { Name = AirPocketObject, Up = 0.05f, Props = new Dictionary<string, string>() },
				new { Name = ContentCatalog.TriggerZone, Up = 1.6f, Props = new Dictionary<string, string> { { ObjectProps.ZoneId, "air" }, { ObjectProps.ZoneRadius, "2.5" },
					{ ObjectProps.ZoneRepeat, "1" }, { ObjectProps.ZoneAir, "1" }, { ObjectProps.ZoneMessage, "An air pocket - breathe." } } },
				new { Name = ContentCatalog.AtmosphereZoneName, Up = 1.2f, Props = new Dictionary<string, string> { { ObjectProps.AtmoParticles, "bubbles" }, { ObjectProps.ZoneRadius, "3" } } },
			};
			foreach (var part in parts)
			{
				GameObject go = PlaceableCatalog.Spawn(part.Name, rootObject.transform);
				if (go == null) { Debug.LogWarning("[CUSTOM ISLANDS] Air pocket: could not make " + part.Name); break; }
				go.transform.rotation = PlacementOptions.Straight(go.transform.rotation, true);
				go.transform.position = new Vector3(at.Value.x, gy + part.Up, at.Value.z);
				if (part.Name == AirPocketObject) go.transform.localScale *= 0.6f;
				Collider own = go.GetComponent<Collider>();
				if (own != null) own.enabled = true;
				Dictionary<string, string> props = ObjectProps.Defaults(part.Name);
				foreach (var kv in part.Props) props[kv.Key] = kv.Value;
				EditorGameObject.Attach(go, part.Name, props);
				list.Add(go);
			}
			if (list.Count < parts.Length)
			{
				foreach (GameObject go in list) { go.SetActive(false); UnityEngine.Object.Destroy(go); }
				return new List<GameObject>();
			}
			UndoRedoManager.Insert(new ObjectVisibilityCommand(list, true));
			return list;
		}
	}
}

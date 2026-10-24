using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>ROADMAP LM13: things to pick up on the floors of houses - the generator's huts and cabins, the world randomizer's hut, the library islands' houses.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIFloorPickups", docs: "Dev, editor: LM13 - the generator's huts and cabins with Raft's land pickups on their floors: each pickup lies on the floor (a foundation under it, within 0.12 m), under the roof, clear of the bed, chest and log; at most two a house and some houses without; the bottoms table matches the measured pieces; the island is the same with and without them but for the pickups (no other draw moved); CreateFile gives the same ones twice; the world randomizer's hut gets them too. Pictures shot_floorpickups_<seed>.png")]
		public static void FloorPickupsCommand(string[] args) { StartTest(FloorPickupsRoutine()); }

		[ConsoleCommand(name: "CIFloorPickupsHere", docs: "Dev, editor: LM13 - every Raft land pickup (Pickup_Landmark_*_Land) on the island in the editor lies on a floor or the ground (within 0.12 m) and inside nothing; with a roof over it counts as inside a house. Pictures of the first [n] (default 2) inside: shot_floorpickups_<island>_<i>.png. CIFloorPickupsHere [n]")]
		public static void FloorPickupsHereCommand(string[] args) { StartTest(FloorPickupsHereRoutine(args != null && args.Length > 0 ? (int)F(args[0]) : 2)); }

		static IEnumerator FloorPickupsRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("CIFloorPickups (in the editor)"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			var hutsSettings = new IslandGenSettings { Seed = 1301, Radius = 120f, Height = 22f, Roughness = 0.25f, Peaks = 1, ObjectDensity = 0.3f, Buildings = true, BuildingKind = GenBuildings.Huts, BuildingCount = 6 };
			yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(hutsSettings));
			Check(ref ok, GenBuildings.NeededNames(hutsSettings).Contains("Pickup_Landmark_Plastic3_Land"), "huts load the floor pickups' scene (NeededNames)");

			// The bottoms used when a pickup's scene isn't loaded (islands made while sailing) match the measured pieces
			foreach (string n in GenBuildings.FloorPickupNames)
			{
				Bounds b;
				bool m = GenBuildings.Measured(n, out b);
				float table = GenBuildings.FloorPickupBottom(n);
				Check(ref ok, m && Mathf.Abs(b.min.y - table) < 0.05f, n + ": measured bottom " + (m ? b.min.y.ToString("F3", CultureInfo.InvariantCulture) : "(not measured)") + ", table " + table.ToString("F3", CultureInfo.InvariantCulture) + ", size " + (m ? b.size.ToString("F2") : "-"));
			}

			// Without the editor: the same file twice, and with and without the pickups the same island but for them
			int filePicks = 0, fileHouses = 0;
			foreach (int seed in new[] { 1301, 1302, 1303 })
			{
				foreach (string kind in new[] { GenBuildings.Huts, GenBuildings.Cabins })
				{
					IslandGenSettings s = hutsSettings.Copy();
					s.Seed = seed; s.BuildingKind = kind;
					IslandFile a = IslandGenerator.CreateFile(s, "cifloorpickups");
					IslandFile a2 = IslandGenerator.CreateFile(s, "cifloorpickups");
					GenBuildings.NoFloorPickups = true;
					IslandFile none;
					try { none = IslandGenerator.CreateFile(s, "cifloorpickups"); }
					finally { GenBuildings.NoFloorPickups = false; }
					Func<IslandFile, bool, List<string>> list = (f, picks) => f.Objects.Where(o => GenBuildings.IsFloorPickup(o.Name) == picks)
						.Select(o => o.Name + "@" + o.Position.x.ToString("F2", CultureInfo.InvariantCulture) + "," + o.Position.y.ToString("F2", CultureInfo.InvariantCulture) + "," + o.Position.z.ToString("F2", CultureInfo.InvariantCulture) + "/" + o.EulerRotation.y.ToString("F0", CultureInfo.InvariantCulture)).ToList();
					List<string> pa = list(a, true), pa2 = list(a2, true), rest = list(a, false), restNone = list(none, false);
					int houses = a.Objects.Count(o => o.Name.StartsWith("Placeable_Bed_"));
					filePicks += pa.Count; fileHouses += houses;
					Check(ref ok, pa.SequenceEqual(pa2), kind + " seed " + seed + ": the same pickups twice (" + pa.Count + " in " + houses + " houses)");
					Check(ref ok, list(none, true).Count == 0 && rest.SequenceEqual(restNone), kind + " seed " + seed + ": the island without pickups is the same but for them (" + rest.Count + " / " + restNone.Count + " other objects)");
					Check(ref ok, pa.Count <= 2 * houses, kind + " seed " + seed + ": at most two a house (" + pa.Count + " in " + houses + ")");
				}
			}
			Check(ref ok, fileHouses > 0 && filePicks > 0 && filePicks <= fileHouses * 1.3f, "light, as Raft's own houses: " + filePicks + " pickups in " + fileHouses + " houses");

			// The world randomizer's hut
			{
				IslandGenSettings s = hutsSettings.Copy();
				s.Buildings = false; s.Seed = 1311;
				int got = 0, tries = 0;
				for (int i = 0; i < 6; i++)
				{
					IslandFile f = IslandGenerator.CreateFile(s, "cifloorpickups");
					var k = new MapKit(f, 50 + i);
					MethodInfo hut = typeof(RandomizerContent).GetMethod("Hut", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(MapKit), typeof(Vector2), typeof(float) }, null);
					if (hut == null) { Check(ref ok, false, "RandomizerContent.Hut found"); break; }
					Vector2 c = k.Mid + new Vector2(i * 7.3f, -i * 4.1f);
					hut.Invoke(null, new object[] { k, c, 0f });
					tries++;
					got += f.Objects.Count(o => GenBuildings.IsFloorPickup(o.Name) && Mathf.Abs(o.Position.x - c.x - 1.5f) < 3f && Mathf.Abs(o.Position.z - c.y - 0.75f) < 2f);
				}
				Check(ref ok, got > 0 && got <= tries * 2, "the world randomizer's hut: " + got + " pickups on the floors of " + tries + " huts");
			}

			// In the editor: each pickup on a floor, under a roof, clear of the furniture; pictures
			foreach (string kind in new[] { GenBuildings.Huts, GenBuildings.Cabins })
			{
				IslandGenSettings s = hutsSettings.Copy();
				s.BuildingKind = kind;
				IslandGenerator.GenerateInEditor(s);
				yield return new WaitForSecondsRealtime(1f);
				List<EditorGameObject> objs = PlacedEditorObjects();
				List<EditorGameObject> picks = objs.Where(e => GenBuildings.IsFloorPickup(e.GameObjectName ?? "")).ToList();
				int houses = objs.Count(e => (e.GameObjectName ?? "").StartsWith("Placeable_Bed_"));
				Check(ref ok, picks.Count > 0, kind + ": " + picks.Count + " pickups in " + houses + " houses (" + IslandGenerator.LastReport.Describe() + ")");
				int inside, bad;
				CheckPickupsLie(ref ok, objs, picks, true, kind, out inside, out bad);
				yield return PickupShots(picks, "floorpickups_" + kind, 2);
			}
			if (ok) Log("PASS: floor pickups"); else Fail("floor pickups");
		}

		static IEnumerator FloorPickupsHereRoutine(int shots)
		{
			if (!DynamicIslands.InEditor()) { Fail("CIFloorPickupsHere (in the editor)"); yield break; }
			bool ok = true;
			List<EditorGameObject> objs = PlacedEditorObjects();
			List<EditorGameObject> picks = objs.Where(e => { string n = e.GameObjectName ?? ""; return n.StartsWith("Pickup_Landmark_") && n.EndsWith("_Land"); }).ToList();
			string island = DynamicIslands.currentIslandName ?? "island";
			int inside, bad;
			CheckPickupsLie(ref ok, objs, picks, false, island, out inside, out bad);
			Log("  " + island + ": " + picks.Count + " land pickups, " + inside + " under a roof, " + bad + " not lying right");
			yield return PickupShots(picks.Where(p => UnderRoof(p) != null).ToList(), "floorpickups_" + RaftIslands.SafeName(island), shots);
			if (ok) Log("PASS: floor pickups here"); else Fail("floor pickups here");
		}

		/// <summary>The piece of a roof (or ceiling) over a pickup within 6 m, or null.</summary>
		static Collider UnderRoof(EditorGameObject p)
		{
			Bounds b = PickupBounds(p);
			// (five rays: one straight up can slip through a seam where a hip roof's pieces meet)
			foreach (Vector2 off in new[] { Vector2.zero, new Vector2(0.3f, 0f), new Vector2(-0.3f, 0f), new Vector2(0f, 0.3f), new Vector2(0f, -0.3f) })
				foreach (RaycastHit h in Physics.RaycastAll(new Vector3(b.center.x + off.x, b.max.y + 0.05f, b.center.z + off.y), Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
					if (h.collider.GetComponentInParent<EditorGameObject>() != p && h.collider.GetComponent<Terrain>() == null) return h.collider;
			return null;
		}

		static Bounds PickupBounds(EditorGameObject p)
		{
			Renderer[] rs = p.GetComponentsInChildren<Renderer>(false).Where(r => r is MeshRenderer && r.enabled && r.name != ContentCatalog.MarkerOnly).ToArray();
			if (rs.Length == 0) return new Bounds(p.transform.position, Vector3.one * 0.2f);
			Bounds b = rs[0].bounds;
			foreach (Renderer r in rs) b.Encapsulate(r.bounds);
			return b;
		}

		/// <summary>Each pickup: what is under it is within 0.12 m of its bottom (a foundation when onFloor), a roof over it
		/// when onFloor, and its box inside no other object's but the floor's.</summary>
		static void CheckPickupsLie(ref bool ok, List<EditorGameObject> objs, List<EditorGameObject> picks, bool onFloor, string what, out int inside, out int bad)
		{
			Physics.SyncTransforms();
			inside = 0; bad = 0;
			var wrong = new List<string>();
			Vector2 mid = EditorLandCentre();
			foreach (EditorGameObject p in picks)
			{
				Bounds b = PickupBounds(p);
				string where = p.GameObjectName + " at " + Num(b.center.x - mid.x) + " " + Num(b.center.z - mid.y);
				RaycastHit under = Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 0.3f, b.center.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore)
					.Where(h => h.collider.GetComponentInParent<EditorGameObject>() != p).OrderBy(h => h.distance).FirstOrDefault();
				EditorGameObject on = under.collider != null ? under.collider.GetComponentInParent<EditorGameObject>() : null;
				string onName = under.collider == null ? "nothing" : on != null ? on.GameObjectName : under.collider.GetComponent<Terrain>() != null ? "the ground" : under.collider.name;
				// (the pickup's lowest point against what is under its middle: a piece lying on a slope or tilted rests lower at one end)
				float gap = under.collider == null ? 99f : b.min.y - under.point.y;
				bool roof = UnderRoof(p) != null;
				if (roof) inside++;
				bool lies = under.collider != null && gap < 0.12f && gap > -0.25f && (!onFloor || (onName ?? "").StartsWith("Block_Foundation"));
				// (inside furniture: its middle within another object's box - walls and the floor aside)
				EditorGameObject inThing = objs.FirstOrDefault(o => o != p && o != on && !(o.GameObjectName ?? "").StartsWith("Block_") && !(o.GameObjectName ?? "").StartsWith("Zone_") && (o.transform.position - b.center).sqrMagnitude < 16f
					&& o.GetComponentsInChildren<Renderer>(false).Any(r => r is MeshRenderer && r.enabled && r.name != ContentCatalog.MarkerOnly && r.bounds.Contains(b.center)));
				if (!lies || (onFloor && !roof) || inThing != null)
				{
					bad++;
					wrong.Add(where + ": on " + onName + " gap " + Num(gap) + (roof ? "" : ", no roof over it") + (inThing != null ? ", inside " + inThing.GameObjectName : ""));
				}
			}
			foreach (string w in wrong.Take(12)) Log("  " + w);
			Check(ref ok, bad == 0, what + ": " + picks.Count + " pickups lie on " + (onFloor ? "the houses' floors, under the roof, clear of the furniture" : "a floor or the ground, inside nothing") + (bad > 0 ? " - " + bad + " not" : ""));
		}

		/// <summary>Pictures of the first n pickups from 2.6 m off, from the side with the least in the way.</summary>
		static IEnumerator PickupShots(List<EditorGameObject> picks, string name, int n)
		{
			if (Camera.main == null) yield break;
			EditorCamera ec = Camera.main.GetComponent<EditorCamera>();
			if (ec != null) ec.enabled = false;
			for (int i = 0; i < Mathf.Min(n, picks.Count); i++)
			{
				Bounds b = PickupBounds(picks[i]);
				float best = -1f, bestYaw = 0f;
				for (int yaw = 0; yaw < 360; yaw += 45)
				{
					Quaternion q = Quaternion.Euler(30f, yaw, 0f);
					Vector3 from = b.center - q * Vector3.forward * 2.6f;
					RaycastHit h;
					float clear = Physics.Raycast(b.center + Vector3.up * 0.2f, (from - b.center).normalized, out h, 2.6f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 2.6f;
					if (clear > best) { best = clear; bestYaw = yaw; }
				}
				Quaternion look = Quaternion.Euler(30f, bestYaw, 0f);
				Camera.main.transform.SetPositionAndRotation(b.center - look * Vector3.forward * Mathf.Min(2.6f, Mathf.Max(1.2f, best - 0.1f)), look);
				yield return new WaitForSecondsRealtime(0.8f);
				Screenshot(new[] { name + "_" + i });
				yield return new WaitForSecondsRealtime(0.4f);
			}
			if (ec != null) ec.enabled = true;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DynamicIslands.Editor;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// What hangs: zipline and wire ends and props that hang from something (IG9, part of CIGroundWorld / CIGroundIslands /
	/// CIFloating's world check). The ground check skips what hangs (Hangs) and lets anything that touches a collider stand -
	/// so a zipline end high over a short post, a wire end in the air, a stone under a crane the flying island left out (it
	/// sank under the sea level, and the check spawns islands at sea level) all read "on the ground" (The Lodestone Array,
	/// Altitude Market, 2026-10-22).
	/// </summary>
	public static partial class DevTests
	{
		static readonly FieldInfo ZipPointA = typeof(MeshPath_Zipline_Landmark).GetField("pointA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		static readonly FieldInfo ZipPointB = typeof(MeshPath_Zipline_Landmark).GetField("pointB", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		/// <summary>Wires and ropes strung between two things (not a zipline - those have their own ends; not a rope fence - it has its own posts).</summary>
		static readonly Regex WireName = new Regex("^(?!.*Fence).*(Rope|Cable|Wire|PowerLine|Chain)", RegexOptions.IgnoreCase);
		/// <summary>Props that hang from something above them.</summary>
		static readonly Regex HangProp = new Regex("Lantern|Lamp|Bucket|Hook|Hanging|Swing|Cage|Pulley|Bell|Chime|Ballast|Net(?!work)", RegexOptions.IgnoreCase);
		/// <summary>How far a wire or zipline end may be from the top of what holds it.</summary>
		const float EndSlack = 0.3f;

		/// <summary>Whether a placed object shows anything (an enabled mesh or skinned renderer).</summary>
		static bool Shows(Transform t)
		{
			return t.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer) && r.name != ContentCatalog.MarkerOnly);
		}

		/// <summary>Whether a collider is something that holds (the land, the island's rock, or a placed object that shows).</summary>
		static bool Holds(Collider c, Transform self, Transform objects)
		{
			if (c == null || c.isTrigger || c.transform.IsChildOf(self)) return false;
			Transform r = PlacedRoot(c.transform, objects);
			return r == null || Shows(r);
		}

		/// <summary>A wire's end from its mesh: the highest vertex within 0.5 m of that end along its long side (where it is across
		/// too: a cable turned off the axis ends off its bounds' middle; a cable tilted up its span has a low end and a high one -
		/// its bounds' top is only the high end's; Cablecar Stacks,
		/// 2026-10-23). NaN when it has no mesh.</summary>
		static Vector3 WireEnd(Renderer[] rs, bool alongX, float at)
		{
			Vector3 best = new Vector3(float.NaN, float.NaN, float.NaN);
			foreach (Renderer r in rs)
			{
				MeshFilter mf = r.GetComponent<MeshFilter>();
				Mesh m = mf != null ? mf.sharedMesh : null;
				if (m == null) continue;
				Matrix4x4 w = r.transform.localToWorldMatrix;
				// (a mesh the game won't let be read: the corners of its own box, turned as it is - a tilted cable's ends are
				// its box's top corners at each end)
				Vector3 c = m.bounds.center, e = m.bounds.extents;
				Vector3[] pts = m.isReadable ? m.vertices : new[] { -1f, 1f }.SelectMany(i => new[] { -1f, 1f }.SelectMany(j => new[] { -1f, 1f }
					.Select(k => c + Vector3.Scale(e, new Vector3(i, j, k))))).ToArray();
				float near = m.isReadable ? 0.5f : 1f;
				foreach (Vector3 v in pts)
				{
					Vector3 p = w.MultiplyPoint3x4(v);
					if (Mathf.Abs((alongX ? p.x : p.z) - at) < near && (float.IsNaN(best.y) || p.y > best.y)) best = p;
				}
			}
			return best;
		}

		/// <summary>Touches, counting only what holds (the land, the rock, placed objects that show).</summary>
		static bool TouchesShown(Transform self, Bounds b, Transform objects)
		{
			var half = new Vector3(Mathf.Max(0.05f, b.extents.x * 0.9f), 0.15f, Mathf.Max(0.05f, b.extents.z * 0.9f));
			if (Physics.OverlapBox(new Vector3(b.center.x, b.min.y, b.center.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => Holds(c, self, objects))) return true;
			return Physics.OverlapBox(b.center, b.extents + Vector3.one * 0.08f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => Holds(c, self, objects) && c.GetComponent<Terrain>() == null);
		}

		/// <summary>The gap from a point down to the first thing that holds (the land, or a placed object that shows); 9999 = nothing.
		/// fromY: where the ray starts (over the piece's own top, so a support it sits a little into is still met - a ray
		/// that starts inside a collider misses it; The Tide Engine's zipline floor on its cab plate, 2026-10-23).</summary>
		static float HoldGapUnder(Transform self, Vector3 p, Terrain land, Transform objects, float fromY = float.NaN)
		{
			float best = float.MaxValue;
			Vector3 start = new Vector3(p.x, (float.IsNaN(fromY) ? p.y : Mathf.Max(p.y, fromY)) + 0.3f, p.z);
			foreach (RaycastHit h in Physics.RaycastAll(start, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
				if (Holds(h.collider, self, objects) && p.y - h.point.y < best) best = Mathf.Max(0f, p.y - h.point.y);
			if (land != null)
			{
				float g = p.y - (land.SampleHeight(p) + land.transform.position.y);
				if (g > -0.3f && g < best) best = Mathf.Max(0f, g);
				if (g <= -0.3f) best = 0f;
			}
			return best == float.MaxValue ? 9999f : best;
		}

		/// <summary>Problems of what hangs on one island: zipline ends, wire ends, hanging props, solid things that show nothing.</summary>
		static List<string> HangProblems(Transform objects, Terrain land, Vector3 mid, float sea)
		{
			var lines = new List<string>();
			Func<Vector3, string> at = p => Num(p.x - mid.x) + " " + Num(p.z - mid.z) + " h=" + Num(p.y - sea);
			foreach (Transform o in objects)
			{
				if (o == null || !o.gameObject.activeInHierarchy) continue;
				string n = o.name.Replace("(Clone)", "").Trim();
				if (ContentCatalog.IsCreature(n) || n.StartsWith("Zone_") || n.StartsWith("Note_") || n.StartsWith("Pickup_")) continue;
				Renderer[] rs = o.GetComponentsInChildren<Renderer>(false).Where(r => r is MeshRenderer && r.enabled && r.name != ContentCatalog.MarkerOnly).ToArray();
				// (nothing to see: invisible helpers, wall colliders - skipped; a piece a flying island leaves out is FlyingLeftOut's)
				if (rs.Length == 0) continue;
				Bounds whole = rs[0].bounds;
				foreach (Renderer r in rs) whole.Encapsulate(r.bounds);
				MeshPath_Zipline_Landmark line = n.StartsWith("ZiplinePath") ? o.GetComponentInChildren<MeshPath_Zipline_Landmark>(true) : null;
				if (line != null)
				{
					// Each end of the line (its connect point): on its own floor, and that floor within 0.3 m of the top of what holds it
					foreach (FieldInfo f in new[] { ZipPointA, ZipPointB })
					{
						Transform end = f != null ? f.GetValue(line) as Transform : null;
						if (end == null) continue;
						Vector3 p = end.position;
						Renderer[] under = rs.Where(r => Mathf.Max(r.bounds.size.x, r.bounds.size.z) < 4f && r.bounds.max.y <= p.y + 0.5f &&
							Mathf.Abs(r.bounds.center.x - p.x) < 2f && Mathf.Abs(r.bounds.center.z - p.z) < 2f).ToArray();
						float baseY = under.Length > 0 ? under.Min(r => r.bounds.min.y) : p.y;
						float top = under.Length > 0 ? under.Max(r => r.bounds.max.y) : p.y;
						float gap = HoldGapUnder(o, new Vector3(p.x, baseY, p.z), land, objects, top);
						if (baseY <= sea + 0.3f) continue;
						if (gap > EndSlack)
							lines.Add("zipline end in the air: " + n + " " + (f == ZipPointA ? "far" : "near") + " end at " + at(p) + (under.Length > 0 ? ", its floor " : ", no floor, ") +
								(gap > 999f ? "over nothing" : Num(gap) + " m over the top of what is under it"));
						else if (p.y - top > 3f)
							lines.Add("zipline end over its floor: " + n + " " + (f == ZipPointA ? "far" : "near") + " end at " + at(p) + ", " + Num(p.y - top) + " m over it");
					}
					continue;
				}
				// A wire or rope strung along: both ends of its long side held (within 0.3 m of something that shows, or the land)
				if (WireName.IsMatch(n) && !n.StartsWith("Zipline", StringComparison.OrdinalIgnoreCase))
				{
					float lx = whole.size.x, lz = whole.size.z;
					if (Mathf.Max(lx, lz) >= 2f && Mathf.Max(lx, lz) > 3f * Mathf.Min(lx, lz) + 0.01f)
					{
						bool alongX = lx >= lz;
						foreach (float s in new[] { 0f, 1f })
						{
							// (the end: the highest point of the wire at that end of its long side)
							float ax = alongX ? Mathf.Lerp(whole.min.x, whole.max.x, s) : whole.center.x, az = alongX ? whole.center.z : Mathf.Lerp(whole.min.z, whole.max.z, s);
							Vector3 end = WireEnd(rs, alongX, alongX ? ax : az);
							float y = end.y;
							if (!float.IsNaN(end.x)) { ax = end.x; az = end.z; }
							if (float.IsNaN(y))
								y = rs.Where(r => alongX ? Mathf.Abs((s == 0 ? r.bounds.min.x : r.bounds.max.x) - ax) < 0.3f : Mathf.Abs((s == 0 ? r.bounds.min.z : r.bounds.max.z) - az) < 0.3f)
									.Select(r => r.bounds.max.y).DefaultIfEmpty(whole.max.y).Max();
							var p = new Vector3(ax, y, az);
							bool held = Physics.OverlapSphere(p, EndSlack + 0.1f, ~0, QueryTriggerInteraction.Ignore).Any(c => Holds(c, o, objects));
							if (!held && land != null && p.y - (land.SampleHeight(p) + land.transform.position.y) < EndSlack + 0.1f) held = true;
							// (or at a shown piece with no collider there - a mast of thick pillars; Cablecar Stacks' summit, 2026-10-23)
							if (!held && objects != null)
								foreach (Transform q in objects)
									if (q != o && !WireName.IsMatch(q.name) && Shows(q) && q.GetComponentsInChildren<Renderer>().Any(r => { var b = r.bounds; b.Expand(2f * (EndSlack + 0.1f)); return b.Contains(p); })) { held = true; break; }
							if (!held && p.y > sea + 0.3f)
							{
								float g = HoldGapUnder(o, p, land, objects);
								lines.Add("wire end in the air: " + n + " at " + at(p) + ", " + (g > 999f ? "nothing under it" : Num(g) + " m over the top of what is under it"));
							}
						}
						continue;
					}
				}
				// A hanging prop over the air: something that shows above it, within 1.5 m (a crane, a beam, a branch, a roof), or a wall
				// or post at its side
				if (HangProp.IsMatch(n))
				{
					float gap = HoldGapUnder(o, new Vector3(whole.center.x, whole.min.y, whole.center.z), land, objects);
					if (gap <= 0.5f || whole.min.y <= sea + 0.3f) continue;
					var half = new Vector3(Mathf.Max(0.3f, whole.extents.x), 0.75f, Mathf.Max(0.3f, whole.extents.z));
					bool above = Physics.OverlapBox(new Vector3(whole.center.x, whole.max.y + 0.75f, whole.center.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => Holds(c, o, objects));
					bool side = Physics.OverlapBox(whole.center, whole.extents + Vector3.one * 0.1f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => Holds(c, o, objects) && c.GetComponent<Terrain>() == null);
					if (!above && !side) lines.Add("hangs from nothing: " + n + " at " + at(new Vector3(whole.center.x, whole.min.y, whole.center.z)) + ", " + (gap > 999f ? "nothing under it" : Num(gap) + " m over the ground") + ", nothing above it within 1.5 m");
				}
			}
			return lines;
		}

		/// <summary>
		/// A flying island leaves out what lies under its own sea level when it spawns (IslandSpawner.Spawned: the "under-water
		/// objects"); the check spawns islands at the sea, so they are there when measured and gone in play. Altitude Market's
		/// ballast crane stood on the cliff edge, its foot under the line, and the stone hung from nothing (2026-10-22).
		/// </summary>
		static List<string> FlyingLeftOut(string islandName)
		{
			var lines = new List<string>();
			IslandFile f;
			try { f = IslandFile.Load(IslandSpawner.PathFor(islandName)); } catch { return lines; }
			if (f == null || f.Elevation <= IslandSpawner.FlyingThreshold) return lines;
			foreach (IslandObject o in f.Objects.Where(o => o.Position.y < f.WaterLevel - 0.5f))
				lines.Add("left out when it flies (under its sea level): " + o.Name + " at " + Num(o.Position.x) + " " + Num(o.Position.z) + " h=" + Num(o.Position.y - f.WaterLevel));
			return lines;
		}
	}
}

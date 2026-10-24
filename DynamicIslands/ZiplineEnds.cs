using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's zipline lines with a far end of the builder's choosing (the user, 2026-10-05: ziplines on the islands). Raft's
	/// line (MeshPath_Zipline_Landmark) runs between two connect points over two small floors: the object stands at one end,
	/// and the setting zip.to ("x y z", the island's own coordinates, like an object's position) is where the other end's
	/// floor stands. In a world its floor and connect point move there and Raft makes the line again (ForceCreatePath);
	/// without the setting the line is Raft's own (Tangaroa's 17.6 m, Caravan Town's 31 m down a 14.5 m drop).
	/// </summary>
	public static class ZiplineEnds
	{
		public const string ZipTo = "zip.to";

		static readonly FieldInfo pointA = typeof(MeshPath_Zipline_Landmark).GetField("pointA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		static readonly FieldInfo pointB = typeof(MeshPath_Zipline_Landmark).GetField("pointB", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		public static bool TryParse(string text, out Vector3 v)
		{
			v = Vector3.zero;
			string[] p = (text ?? "").Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
			float x, y, z;
			if (p.Length != 3 || !float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
				!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
			v = new Vector3(x, y, z);
			return true;
		}

		public static string Text(Vector3 v) { return v.x.ToString("0.##", CultureInfo.InvariantCulture) + " " + v.y.ToString("0.##", CultureInfo.InvariantCulture) + " " + v.z.ToString("0.##", CultureInfo.InvariantCulture); }

		/// <summary>Moves a spawned zipline's far end to its zip.to (island origin: the island's root position). Returns whether it moved.</summary>
		public static bool Apply(GameObject go, IDictionary<string, string> props, Vector3 islandOrigin)
		{
			Vector3 to;
			if (go == null || props == null || !TryParse(ObjectProps.Get(props, ZipTo), out to)) return false;
			MeshPath_Zipline_Landmark line = go.GetComponentInChildren<MeshPath_Zipline_Landmark>(true);
			Transform a = line != null && pointA != null ? pointA.GetValue(line) as Transform : null;
			if (a == null) return false;
			// (everything of the far end goes with it: each ZiplineBase piece nearer the far connect point than the near one -
			// the floor and its stand. Before 2026-10-20 only the nearest piece moved, and not even
			// that when it held the connect point: the Lodestone Array's far-end floor stayed on the slope where Raft's own 31 m line ends, 1.7 m in the air)
			Transform bEnd = pointB != null ? pointB.GetValue(line) as Transform : null;
			Vector3 near = bEnd != null ? bEnd.position : line.transform.position;
			List<Transform> pieces = go.GetComponentsInChildren<Transform>(true).Where(t => t != a && t != go.transform && (bEnd == null || !bEnd.IsChildOf(t)) && !line.transform.IsChildOf(t) &&
				t.name.IndexOf("ZiplineBase", System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			pieces = pieces.Where(t => !pieces.Any(o => o != t && t.IsChildOf(o)) && (t.position - a.position).sqrMagnitude < (t.position - near).sqrMagnitude).ToList();
			Transform floor = pieces.OrderBy(t => (t.position - a.position).sqrMagnitude).FirstOrDefault();
			Vector3 target = islandOrigin + to;
			Vector3 delta = target - (floor != null ? floor.position : a.position);
			foreach (Transform t in pieces) t.position += delta;
			if (!pieces.Any(t => a.IsChildOf(t))) a.position += delta;
			try { line.ForceCreatePath(); }
			catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Zipline end: " + e.Message); return false; }
			// (Raft's own collider follows the made path: re-enable so it is rebuilt where the line now runs)
			foreach (MeshCollider mc in go.GetComponentsInChildren<MeshCollider>(true))
			{
				MeshFilter mf = mc.GetComponent<MeshFilter>();
				if (mf != null && mf.sharedMesh != null) mc.sharedMesh = mf.sharedMesh;
				mc.enabled = false; mc.enabled = true;
			}
			return true;
		}

		/// <summary>The far end's position in the island's coordinates (for the generator and recipes): zip.to as text.</summary>
		public static Vector3? FarEnd(IDictionary<string, string> props) { Vector3 v; return TryParse(ObjectProps.Get(props, ZipTo), out v) ? v : (Vector3?)null; }

		/// <summary>Where the near end's floor stands (world), the end that stays with the object: the ZiplineBase piece
		/// nearest the near connect point (that point itself when there is none). Null when the object has no zipline line.</summary>
		public static Vector3? NearFloor(GameObject go)
		{
			MeshPath_Zipline_Landmark line = go != null ? go.GetComponentInChildren<MeshPath_Zipline_Landmark>(true) : null;
			Transform a = line != null && pointA != null ? pointA.GetValue(line) as Transform : null;
			if (line == null) return null;
			Transform bEnd = pointB != null ? pointB.GetValue(line) as Transform : null;
			Vector3 near = bEnd != null ? bEnd.position : line.transform.position;
			Transform floor = go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform && t.name.IndexOf("ZiplineBase", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
				(a == null || (t.position - near).sqrMagnitude < (t.position - a.position).sqrMagnitude)).OrderBy(t => (t.position - near).sqrMagnitude).FirstOrDefault();
			return floor != null ? floor.position : near;
		}
	}
}

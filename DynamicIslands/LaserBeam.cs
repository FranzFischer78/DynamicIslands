using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// A laser emitter's beam (ROADMAP LM12, 2026-10-09): Raft's laser puzzle scripts don't come with copied objects, so the
	/// beam is the mod's own. It shoots along the emitter's forward; an object marked as a mirror (beh.laser = mirror) sends
	/// it on along the mirror's own forward (turning the mirror a quarter turns the beam); the first other object of the
	/// island it reaches gets the event "laser" (on the host, once each time the beam comes to it). Every player sees the
	/// beam: it follows the mirrors, which move for everyone.
	/// </summary>
	public class LaserBeam : MonoBehaviour
	{
		/// <summary>beh.laser: "beam" = an emitter, "mirror" = sends the beam on.</summary>
		public const string Prop = "beh.laser", Beam = "beam", Mirror = "mirror";
		public const string Event = "laser";
		public const float Range = 60f, Width = 0.05f;
		public const int MaxMirrors = 8;

		/// <summary>The object the beam ends on now (null: it ends in the air), and the mirrors it went by.</summary>
		public IslandObjectRef Hit { get; private set; }
		/// <summary>The collider the beam stopped on (null: it went its full range) - for the tests' log.</summary>
		public Collider HitCollider { get; private set; }
		public readonly List<IslandObjectRef> Mirrors = new List<IslandObjectRef>();
		public readonly List<Vector3> Points = new List<Vector3>();

		LineRenderer line;
		float next;
		IslandObjectRef told;

		public static bool IsMirror(IslandObjectRef r) { return r != null && ObjectProps.Get(r.Props, Prop) == Mirror; }

		/// <summary>Where the beam starts: the middle of the emitter's model.</summary>
		public Vector3 Origin
		{
			get
			{
				Renderer[] rs = GetComponentsInChildren<Renderer>().Where(x => !(x is LineRenderer)).ToArray();
				if (rs.Length == 0) return transform.position;
				Bounds b = rs[0].bounds;
				foreach (Renderer x in rs) b.Encapsulate(x.bounds);
				return b.center;
			}
		}

		void Start()
		{
			var go = new GameObject("LaserBeam_Line");
			go.transform.SetParent(transform, false);
			line = go.AddComponent<LineRenderer>();
			line.useWorldSpace = true;
			line.startWidth = line.endWidth = Width;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			var m = new Material(Shader.Find("Sprites/Default"));
			m.color = new Color(1f, 0.1f, 0.05f, 0.9f);
			line.material = m;
			line.startColor = line.endColor = m.color;
		}

		void Update()
		{
			if (Time.time < next) return;
			next = Time.time + 0.1f;
			Trace();
		}

		/// <summary>Follows the beam from the emitter through the mirrors; tells what it reaches (the host).</summary>
		public void Trace()
		{
			Points.Clear();
			Mirrors.Clear();
			Vector3 pos = Origin, dir = transform.forward;
			Transform skip = transform;
			Points.Add(pos);
			IslandObjectRef hit = null;
			HitCollider = null;
			for (int bounce = 0; ; bounce++)
			{
				RaycastHit? first = null;
				foreach (RaycastHit h in Physics.RaycastAll(pos, dir, Range, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
				{
					if (h.collider.transform.IsChildOf(skip)) continue;
					first = h;
					break;
				}
				if (!first.HasValue) { Points.Add(pos + dir * Range); break; }
				Points.Add(first.Value.point);
				HitCollider = first.Value.collider;
				IslandObjectRef r = first.Value.collider.GetComponentInParent<IslandObjectRef>();
				if (IsMirror(r) && bounce < MaxMirrors && !Mirrors.Contains(r))
				{
					Mirrors.Add(r);
					Vector3 f = r.transform.forward;
					f.y = 0f;
					if (f.sqrMagnitude < 0.01f) f = r.transform.right;
					dir = f.normalized;
					// turn at the mirror's own upright axis, not where the beam first touched its box: a beam that bounced off
					// a box face ran along that face line and only grazed the next mirror (2026-10-18, The Rain Engine in its plan)
					Vector3 c = r.transform.position;
					pos = new Vector3(c.x, first.Value.point.y, c.z);
					Points[Points.Count - 1] = pos;
					skip = r.transform;
					continue;
				}
				hit = r;
				break;
			}
			Hit = hit;
			if (line != null)
			{
				line.positionCount = Points.Count;
				line.SetPositions(Points.ToArray());
			}
			if (hit != told)
			{
				told = hit;
				if (hit != null && Raft_Network.IsHost)
					Behaviours.FireFromHost(ContentState.EntryOf(hit.transform), hit.Index, Event);
			}
		}
	}
}

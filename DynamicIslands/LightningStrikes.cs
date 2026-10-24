using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AzureSky;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Lightning strikes" (WorldOptions.Lightning, TODO 4k): now and then in a storm a bolt of lightning
	/// hits the sea near the raft (30-150 m away) - a jagged bolt, a flash that lights the scene, Raft's own thunder. Very
	/// rarely it hits the raft's tallest Receiver antenna (or wind turbine) instead. LOOKS ONLY: no damage, no fire,
	/// nothing breaks, nothing happens to players or items (no collider, no damage call anywhere).
	///   - A storm: Raft's rain or rough snow weather (Raft has no other storm weather; Storm days and Storm seas use these),
	///     or a storm of Storm days / Storm seas.
	///   - The host decides: when a storm begins it rolls how many bolts this storm gets (none in about 3 storms of 10, else
	///     1-4, fewer more often) and when (the first 40-200 s in, then 60-200 s apart). Each bolt is sent to every player
	///     (IslandNetMessage.Moment, Name = "bolt", Data = Strike.Encode): where it hits, relative to the raft (on the sea:
	///     the offset from the raft; on the raft: the point in the raft's own space), and the seed of its shape - every
	///     machine draws the same bolt at the same place. Nothing is saved.
	/// </summary>
	public static class LightningStrikes
	{
		public const float NoneChance = 0.3f, RodChance = 0.08f, MinDistance = 30f, MaxDistance = 150f;
		public const int MaxPerStorm = 4;
		/// <summary>Raft's blocks a bolt may hit (the tallest one on the raft).</summary>
		public static readonly string[] RodItems = { "Placeable_Reciever_Antenna", "Placeable_WindTurbine", "Placeable_Windturbine" };

		/// <summary>Tests: a storm (true) or calm (false) whatever the weather says; null = the weather.</summary>
		internal static bool? TestStorm;
		/// <summary>Tests: the bolt stays at its brightest until this is false again (for pictures).</summary>
		internal static bool TestHold;
		/// <summary>Bolts shown on this machine since the mod loaded (tests).</summary>
		internal static int Started;
		/// <summary>The last bolt shown (null when it is gone).</summary>
		public static LightningBolt Current { get; private set; }
		/// <summary>The thunder used last: "sky" (Raft's AzureSky thunder: flash + sound), an FMOD event path, or "none".</summary>
		internal static string LastThunder = "";

		static bool inStorm;
		static int left;
		static float nextAt, nextCheck;
		static readonly System.Random rng = new System.Random();

		public static bool On { get { return WorldOptions.On(WorldOptions.Lightning); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [lightning] " + msg); }

		internal static void Reset()
		{
			TestStorm = null; TestHold = false;
			inStorm = false; left = 0; nextAt = 0f; nextCheck = 0f;
			if (Current != null) { try { UnityEngine.Object.Destroy(Current.gameObject); } catch { } }
			Current = null;
		}

		#region When

		static WeatherManager weather;
		static Func<bool> stormSeas;
		static bool stormSeasLooked;

		/// <summary>A storm now: Raft's rain or rough snow, or a Storm days / Storm seas storm.</summary>
		public static bool IsStorm
		{
			get
			{
				if (TestStorm.HasValue) return TestStorm.Value;
				try { if (StormDays.IsStorm) return true; } catch { }
				try { if (StormSeasStorm()) return true; } catch { }
				try
				{
					if (weather == null) weather = UnityEngine.Object.FindObjectOfType<WeatherManager>();
					if (weather == null) return false;
					UniqueWeatherType w = weather.GetCurrentWeatherType();
					return w == UniqueWeatherType.Rain || w == UniqueWeatherType.SnowRough;
				}
				catch { return false; }
			}
		}

		/// <summary>The Storm seas option's own storm (TODO 4e), looked up by name so this file needs nothing of it.</summary>
		static bool StormSeasStorm()
		{
			if (!stormSeasLooked)
			{
				stormSeasLooked = true;
				Type t = typeof(WorldOptions).Assembly.GetType("DynamicIslands.Editor.StormSeas");
				if (t != null)
				{
					foreach (string n in new[] { "IsStorm", "InStorm", "Stormy", "StormNow", "Active" })
					{
						PropertyInfo p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
						if (p != null && p.PropertyType == typeof(bool)) { stormSeas = () => (bool)p.GetValue(null, null); break; }
						FieldInfo f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
						if (f != null && f.FieldType == typeof(bool)) { stormSeas = () => (bool)f.GetValue(null); break; }
					}
				}
			}
			return stormSeas != null && stormSeas();
		}

		/// <summary>How many bolts a storm gets: none in about 3 of 10, else 1 (40 %), 2 (30 %), 3 (20 %) or 4 (10 %).</summary>
		public static int RollCount(System.Random r)
		{
			if (r.NextDouble() < NoneChance) return 0;
			double u = r.NextDouble();
			return u < 0.4 ? 1 : u < 0.7 ? 2 : u < 0.9 ? 3 : 4;
		}

		/// <summary>Every frame (cheap): the host's storms and bolts.</summary>
		internal static void Tick()
		{
			if (Current == null && !ReferenceEquals(Current, null)) Current = null;
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 1f;
			if (!On || !Raft_Network.IsHost || !LoadSceneManager.IsGameSceneLoaded) { inStorm = false; return; }
			bool storm = IsStorm;
			if (storm && !inStorm)
			{
				left = RollCount(rng);
				nextAt = Time.time + 40f + (float)rng.NextDouble() * 160f;
				Log("A storm: " + left + " bolt(s)");
			}
			inStorm = storm;
			if (!storm || left <= 0 || Time.time < nextAt) return;
			left--;
			nextAt = Time.time + 60f + (float)rng.NextDouble() * 140f;
			Send(Roll(rng, rng.NextDouble() < RodChance));
		}

		#endregion

		#region The strike

		/// <summary>One bolt: where it hits (relative to the raft) and the seed of its shape.</summary>
		public class Strike
		{
			/// <summary>On the raft's antenna (Point = in the raft's space) or on the sea (Point = the offset from the raft, y 0).</summary>
			public bool Rod;
			public Vector3 Point;
			public int Shape;

			public string Encode()
			{
				CultureInfo c = CultureInfo.InvariantCulture;
				return (Rod ? "rod" : "sea") + "|" + Point.x.ToString("F2", c) + "|" + Point.y.ToString("F2", c) + "|" + Point.z.ToString("F2", c) + "|" + Shape.ToString(c);
			}

			public static Strike Decode(string data)
			{
				string[] p = (data ?? "").Split('|');
				if (p.Length < 5 || (p[0] != "rod" && p[0] != "sea")) return null;
				float x, y, z; int s;
				NumberStyles n = NumberStyles.Float; CultureInfo c = CultureInfo.InvariantCulture;
				if (!float.TryParse(p[1], n, c, out x) || !float.TryParse(p[2], n, c, out y) || !float.TryParse(p[3], n, c, out z) || !int.TryParse(p[4], NumberStyles.Integer, c, out s)) return null;
				return new Strike { Rod = p[0] == "rod", Point = new Vector3(x, y, z), Shape = s };
			}
		}

		/// <summary>Host: a bolt rolled - on the sea 30-150 m from the raft, or (rod) on the raft's tallest antenna if it has one.</summary>
		public static Strike Roll(System.Random r, bool rod)
		{
			var s = new Strike { Shape = r.Next(1, int.MaxValue) };
			Vector3? top = rod ? RodTop() : null;
			if (top.HasValue) { s.Rod = true; s.Point = top.Value; return s; }
			float a = (float)(r.NextDouble() * Math.PI * 2.0), d = MinDistance + (float)r.NextDouble() * (MaxDistance - MinDistance);
			s.Point = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
			return s;
		}

		static Raft Raft { get { try { return ComponentManager<Raft>.Value ?? UnityEngine.Object.FindObjectOfType<Raft>(); } catch { return null; } } }

		/// <summary>The raft's rod blocks (antennas, wind turbines).</summary>
		internal static List<Block> Rods()
		{
			var list = new List<Block>();
			try
			{
				foreach (Block b in BlockCreator.GetPlacedBlocks())
					if (b != null && b.buildableItem != null && RodItems.Contains(b.buildableItem.UniqueName)) list.Add(b);
			}
			catch { }
			return list;
		}

		/// <summary>The top of the raft's tallest rod block, in the raft's space (null: none).</summary>
		internal static Vector3? RodTop()
		{
			Raft raft = Raft;
			if (raft == null) return null;
			Vector3? best = null;
			foreach (Block b in Rods())
			{
				Renderer[] rs = b.GetComponentsInChildren<Renderer>().Where(x => x != null && x.enabled).ToArray();
				if (rs.Length == 0) continue;
				Bounds bb = rs[0].bounds;
				foreach (Renderer x in rs) bb.Encapsulate(x.bounds);
				Vector3 top = raft.transform.InverseTransformPoint(new Vector3(bb.center.x, bb.max.y, bb.center.z));
				if (!best.HasValue || top.y > best.Value.y) best = top;
			}
			return best;
		}

		/// <summary>Host (and tests): a bolt now, on every machine.</summary>
		public static Strike Send(Strike s)
		{
			try { IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Moment, Name = "bolt", Data = s.Encode() }); }
			catch (Exception e) { Log("not sent: " + e.Message); }
			Show(s);
			return s;
		}

		/// <summary>A player: the host's bolt.</summary>
		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			Strike s = Strike.Decode(msg.Data);
			if (s != null) Show(s);
		}

		/// <summary>The bolt where it hits on this machine.</summary>
		internal static void Show(Strike s)
		{
			Raft raft = Raft;
			if (raft == null) return;
			Vector3 hit;
			if (s.Rod) hit = raft.transform.TransformPoint(s.Point);
			else { Vector3 r = CustomIslandSpawner.RaftPosition ?? raft.transform.position; hit = new Vector3(r.x + s.Point.x, 0f, r.z + s.Point.z); }
			if (Current != null) { try { UnityEngine.Object.Destroy(Current.gameObject); } catch { } }
			var go = new GameObject("CustomIslands_Lightning");
			LightningBolt bolt = go.AddComponent<LightningBolt>();
			bolt.Build(hit, s.Shape, s.Rod);
			Current = bolt;
			Started++;
			Vector3 eye = Camera.main != null ? Camera.main.transform.position : hit;
			Thunder(hit, eye);
			Log("Bolt " + (s.Rod ? "on the raft's antenna" : "on the sea " + new Vector2(s.Point.x, s.Point.z).magnitude.ToString("F0") + " m away") + " (" + s.Encode() + "), thunder: " + LastThunder);
		}

		/// <summary>Raft has no thunder of its own (its sky's thunder list is empty, no FMOD event is called thunder): a clap
		/// and a roll made of Raft's own sounds - the dynamite's bang and the boss rumble - played between the listener and
		/// the bolt, nearer for a near bolt (FMOD fades sounds out long before 150 m).</summary>
		static readonly string[] ThunderEvents = { "event:/weapons/dynamite/explode", "event:/wildlife/boss/boss_rumble" };
		static List<string> fmodThunder;

		/// <summary>Thunder: the sky's own if Raft ever has one (its flash and sound), else the clap and roll above.</summary>
		static void Thunder(Vector3 at, Vector3 eye)
		{
			LastThunder = "none";
			float distance = Vector3.Distance(eye, at);
			try
			{
				AzureSkyController sky = RaftSkySea.Sky ?? ComponentManager<AzureSkyController>.Value ?? UnityEngine.Object.FindObjectOfType<AzureSkyController>();
				var list = sky != null ? Traverse.Create(sky).Field("thunderList").GetValue() as System.Collections.IList : null;
				if (list != null && list.Count > 0)
				{
					int i = Mathf.Clamp((int)(distance / (MaxDistance + 1f) * list.Count), 0, list.Count - 1);
					Traverse.Create(sky).Method("PlayThunderEffect", new[] { typeof(int) }).GetValue(i);
					LastThunder = "sky " + i + " of " + list.Count;
					return;
				}
			}
			catch (Exception e) { Log("sky thunder: " + e.Message); }
			try
			{
				if (fmodThunder == null)
				{
					var known = new HashSet<string>(SoundLibrary.Events);
					fmodThunder = ThunderEvents.Where(known.Contains).ToList();
				}
				if (fmodThunder.Count == 0) return;
				Vector3 dir = at - eye;
				Vector3 where = dir.sqrMagnitude > 0.01f ? eye + dir.normalized * Mathf.Min(distance * 0.15f, 20f) : eye;
				float volume = Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(10f, MaxDistance, distance));
				foreach (string e in fmodThunder) SoundLibrary.PlayOnce(e, where, volume);
				LastThunder = string.Join(" + ", fmodThunder.ToArray());
			}
			catch (Exception e) { Log("thunder: " + e.Message); }
		}

		#endregion
	}

	/// <summary>
	/// One bolt of lightning (LightningStrikes): a jagged white-blue line from high in the sky down to where it hits, a few
	/// forks, a wide faint glow around it and a strong light that flickers - on, off, on again, then fades - all in under a
	/// second. No collider: it touches nothing.
	/// </summary>
	public class LightningBolt : MonoBehaviour
	{
		public const float Life = 0.9f;
		/// <summary>The brightness over the bolt's life: two strokes and a fade.</summary>
		static readonly float[] Flicker = { 1f, 0.15f, 1f, 0.9f, 0.6f, 0.35f, 0.15f, 0.05f, 0f };

		public Vector3 Hit { get; private set; }
		public Vector3 Top { get; private set; }
		/// <summary>Brightness now (0-1); the highest reached (tests).</summary>
		public float Bright { get; private set; }
		public float MaxBright { get; private set; }
		public Light Flash { get; private set; }
		public readonly List<LineRenderer> Lines = new List<LineRenderer>();
		readonly List<Color> colours = new List<Color>();
		float age;
		Light sea;

		static Material material;
		static Material Mat
		{
			get
			{
				if (material != null) return material;
				Shader sh = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
				material = new Material(sh);
				return material;
			}
		}

		internal void Build(Vector3 hit, int shape, bool rod)
		{
			var r = new System.Random(shape);
			Hit = hit;
			float height = rod ? 200f : 230f + (float)r.NextDouble() * 70f;
			float a = (float)(r.NextDouble() * Math.PI * 2.0), slant = 15f + (float)r.NextDouble() * 35f;
			Top = hit + new Vector3(Mathf.Cos(a) * slant, height, Mathf.Sin(a) * slant);
			List<Vector3> main = Jagged(r, Top, hit, 28, 9f);
			Line(main, 1.1f, 0.5f, new Color(0.85f, 0.9f, 1f, 1f));
			Line(main, 4f, 2f, new Color(0.45f, 0.55f, 1f, 0.35f));
			int forks = 2 + r.Next(3);
			for (int f = 0; f < forks; f++)
			{
				int from = 3 + r.Next(main.Count / 2 + 4);
				if (from >= main.Count - 2) from = main.Count / 2;
				Vector3 s = main[from];
				float len = (s.y - hit.y) * (0.25f + (float)r.NextDouble() * 0.35f);
				float fa = (float)(r.NextDouble() * Math.PI * 2.0);
				Vector3 e = s + new Vector3(Mathf.Cos(fa) * len * 0.6f, -len, Mathf.Sin(fa) * len * 0.6f);
				if (e.y < hit.y + 5f) e.y = hit.y + 5f;
				Line(Jagged(r, s, e, 10, 5f), 0.5f, 0.1f, new Color(0.8f, 0.85f, 1f, 0.85f));
			}
			// The flash: a strong light high up the bolt (it lights the sea, the raft and the sky's underside), and a small
			// one where it hits
			Flash = MakeLight("Flash", Vector3.Lerp(hit, Top, 0.25f), 900f, new Color(0.8f, 0.85f, 1f));
			sea = MakeLight("Hit", hit + Vector3.up * 3f, 60f, new Color(0.85f, 0.9f, 1f));
			Apply(1f);
		}

		Light MakeLight(string name, Vector3 at, float range, Color c)
		{
			var go = new GameObject(name);
			go.transform.SetParent(transform, false);
			go.transform.position = at;
			Light l = go.AddComponent<Light>();
			l.type = LightType.Point;
			l.range = range;
			l.color = c;
			l.shadows = LightShadows.None;
			l.renderMode = LightRenderMode.ForcePixel;
			return l;
		}

		static List<Vector3> Jagged(System.Random r, Vector3 a, Vector3 b, int steps, float jag)
		{
			var pts = new List<Vector3> { a };
			Vector3 side = Vector3.zero;
			for (int i = 1; i < steps; i++)
			{
				float t = (float)i / steps;
				// (a random walk to the side, pulled back towards the straight line, smaller near the ends)
				side = side * 0.55f + new Vector3((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1) * 0.3f, (float)(r.NextDouble() * 2 - 1)) * jag;
				pts.Add(Vector3.Lerp(a, b, t) + side * Mathf.Sin(t * Mathf.PI));
			}
			pts.Add(b);
			return pts;
		}

		void Line(List<Vector3> pts, float w0, float w1, Color c)
		{
			var go = new GameObject("Bolt");
			go.transform.SetParent(transform, false);
			LineRenderer l = go.AddComponent<LineRenderer>();
			l.useWorldSpace = true;
			l.positionCount = pts.Count;
			l.SetPositions(pts.ToArray());
			l.startWidth = w0; l.endWidth = w1;
			l.material = Mat;
			l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			l.receiveShadows = false;
			l.numCapVertices = 2;
			Lines.Add(l);
			colours.Add(c);
		}

		void Apply(float b)
		{
			Bright = b;
			if (b > MaxBright) MaxBright = b;
			for (int i = 0; i < Lines.Count; i++)
			{
				if (Lines[i] == null) continue;
				Color c = colours[i];
				c.a *= b;
				Lines[i].startColor = Lines[i].endColor = c;
				Lines[i].enabled = b > 0.02f;
			}
			if (Flash != null) Flash.intensity = 8f * b;
			if (sea != null) sea.intensity = 6f * b;
		}

		void Update()
		{
			if (LightningStrikes.TestHold) { Apply(1f); return; }
			age += Time.deltaTime;
			if (age >= Life) { Destroy(gameObject); return; }
			float f = age / Life * (Flicker.Length - 1);
			int i = Mathf.Min((int)f, Flicker.Length - 2);
			Apply(Mathf.Lerp(Flicker[i], Flicker[i + 1], f - i));
		}
	}
}

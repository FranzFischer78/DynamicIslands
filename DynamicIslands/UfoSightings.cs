using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AzureSky;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "UFO sightings" (WorldOptions.Ufo, TODO 4h): now and then a flying saucer crosses the sky high above
	/// the raft, from horizon to horizon in about half a minute. Looks only: no collider, nothing to catch.
	///   - When: on about two days in five (about three times in a week of Raft days), at an hour of that day; both come from
	///     the world's seed and the day (WorldOptions.Unit), so a loaded world has the same days. Half of them come at night.
	///   - The host decides: at that hour it rolls the saucer's looks and path (colour, glow green or purple, heading, height,
	///     how far to the side, how long) and sends them to every player (IslandNetMessage.Moment, Name = "ufo",
	///     Data = Saucer.Encode). Each machine flies the same path relative to its raft. Nothing is saved.
	///   - The saucer is built from Unity shapes: a flattened metal disc in a random colour, a glass dome, a dark underside,
	///     a rim band and a ring of rim lights; it spins and wobbles a little. At night its rim lights, a soft halo and a
	///     point light under it glow green or purple; by day only the rim lights shine.
	/// </summary>
	public static class UfoSightings
	{
		/// <summary>The share of days with a sighting (from day FirstDay on).</summary>
		public const float Chance = 0.4f;
		public const int FirstDay = 2;

		/// <summary>Tests: the day / hour whatever the world says (null = the world's).</summary>
		internal static int? TestDay;
		internal static float? TestHour;

		/// <summary>Sightings started on this machine since the mod loaded (tests).</summary>
		internal static int Started;

		static int launchedDay = -1;
		static float nextCheck;

		public static bool On { get { return WorldOptions.On(WorldOptions.Ufo); } }

		/// <summary>The saucer in the sky now (null = none).</summary>
		public static UfoSaucer Current { get; private set; }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [ufo] " + msg); }

		internal static void Reset()
		{
			TestDay = null; TestHour = null; launchedDay = -1; nextCheck = 0f;
			if (Current != null) { try { UnityEngine.Object.Destroy(Current.gameObject); } catch { } }
			Current = null;
		}

		static int Today
		{
			get
			{
				if (TestDay.HasValue) return TestDay.Value;
				try { return WorldManager.DayCounter; } catch { return 0; }
			}
		}

		static float Hour
		{
			get
			{
				if (TestHour.HasValue) return TestHour.Value;
				try { AzureSkyController sky = ComponentManager<AzureSkyController>.Value; return sky != null && sky.timeOfDay != null ? sky.timeOfDay.hour : -1f; }
				catch { return -1f; }
			}
		}

		/// <summary>Whether this day has a sighting in this world (the same on every machine).</summary>
		public static bool Rolls(int seed, int day)
		{
			if (day < FirstDay) return false;
			return WorldOptions.Unit(seed, day, 4201) < Chance;
		}

		/// <summary>The hour of the day's sighting: half at night (21-03), half by day (08-18).</summary>
		public static float HourOf(int seed, int day)
		{
			double u = WorldOptions.Unit(seed, day, 4202);
			return u < 0.5 ? (float)((21.0 + u * 2.0 * 6.0) % 24.0) : (float)(8.0 + (u - 0.5) * 2.0 * 10.0);
		}

		/// <summary>Every frame (cheap): the host's schedule, and the saucer gone when the world is.</summary>
		internal static void Tick()
		{
			if (Current == null && !ReferenceEquals(Current, null)) Current = null;
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 1f;
			if (!On || !Raft_Network.IsHost || !LoadSceneManager.IsGameSceneLoaded) return;
			int day = Today, seed = WorldOptions.Seed;
			if (day == launchedDay || !Rolls(seed, day)) return;
			float hour = Hour, at = HourOf(seed, day);
			if (hour < 0f) return;
			// (a half-hour window: a world loaded later in the day, or players who slept through it, skip that day's)
			float late = hour - at;
			if (late < 0f) late += 24f;
			if (late > 0.5f) return;
			launchedDay = day;
			Send(UfoSaucer.Roll(new System.Random(WorldOptions.Mix(seed, day, 4203))));
		}

		/// <summary>Host (and tests): a saucer now, on every machine.</summary>
		public static UfoSaucer.Path Send(UfoSaucer.Path p)
		{
			try { IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Moment, Name = "ufo", Data = p.Encode() }); }
			catch (Exception e) { Log("not sent: " + e.Message); }
			Launch(p);
			return p;
		}

		/// <summary>A player: the host's saucer.</summary>
		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			UfoSaucer.Path p = UfoSaucer.Path.Decode(msg.Data);
			if (p != null) Launch(p);
		}

		static void Launch(UfoSaucer.Path p)
		{
			if (!LoadSceneManager.IsGameSceneLoaded) return;
			if (Current != null) UnityEngine.Object.Destroy(Current.gameObject);
			Current = UfoSaucer.Create(p);
			Started++;
			Log("A saucer crosses the sky: " + p.Encode());
		}
	}

	/// <summary>The flying saucer itself (UfoSightings): built from Unity shapes, flies its path relative to the raft, then is gone.</summary>
	public class UfoSaucer : MonoBehaviour
	{
		/// <summary>One crossing: what every machine needs to show the same saucer.</summary>
		public class Path
		{
			/// <summary>Hue of the hull (0-1); glow: 0 green, 1 purple.</summary>
			public float Hue;
			public int Glow;
			/// <summary>Heading in degrees, metres to the side of the raft, metres above the sea, seconds across.</summary>
			public float Heading, Side, Height, Seconds;
			/// <summary>Spin direction: 1 or -1.</summary>
			public int Spin = 1;

			static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }

			public string Encode() { return F(Hue) + "|" + Glow + "|" + F(Heading) + "|" + F(Side) + "|" + F(Height) + "|" + F(Seconds) + "|" + Spin; }

			public static Path Decode(string s)
			{
				if (string.IsNullOrEmpty(s)) return null;
				string[] a = s.Split('|');
				if (a.Length < 7) return null;
				var p = new Path();
				int g, sp;
				if (!P(a[0], out p.Hue) || !int.TryParse(a[1], out g) || !P(a[2], out p.Heading) || !P(a[3], out p.Side) || !P(a[4], out p.Height) || !P(a[5], out p.Seconds) || !int.TryParse(a[6], out sp)) return null;
				p.Glow = g == 1 ? 1 : 0;
				p.Spin = sp < 0 ? -1 : 1;
				p.Hue = Mathf.Repeat(p.Hue, 1f);
				p.Height = Mathf.Clamp(p.Height, 60f, 400f);
				p.Seconds = Mathf.Clamp(p.Seconds, 5f, 120f);
				p.Side = Mathf.Clamp(p.Side, -400f, 400f);
				return p;
			}

			static bool P(string s, out float f) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f); }
		}

		/// <summary>A random crossing: 25-40 s, 100-150 m up, up to 120 m to the side.</summary>
		public static Path Roll(System.Random r)
		{
			return new Path
			{
				Hue = (float)r.NextDouble(),
				Glow = r.Next(2),
				Heading = (float)(r.NextDouble() * 360.0),
				Side = (float)((r.NextDouble() - 0.5) * 240.0),
				Height = 100f + (float)(r.NextDouble() * 50.0),
				Seconds = 25f + (float)(r.NextDouble() * 15.0),
				Spin = r.Next(2) == 0 ? -1 : 1,
			};
		}

		/// <summary>Half the length of the path: from the haze on one side to the other. Raft's camera draws only 400 m, so the far ends stay just inside it.</summary>
		public const float Reach = 340f;
		public const float Diameter = 24f;

		public Path Route;
		/// <summary>Seconds since it appeared; 0-1 along the path.</summary>
		public float Age;
		public float Along { get { return Route == null ? 1f : Mathf.Clamp01(Age / Route.Seconds); } }
		public Color Hull, GlowColor;

		Vector3 startCenter;
		Transform spinner, halo;
		Light glowLight;
		Material haloMat, lightMat, rimMat;
		readonly List<Renderer> rimLights = new List<Renderer>();

		static Material Lit(Color c, float metallic, float smooth)
		{
			Shader s = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
			var m = new Material(s) { color = c };
			if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
			if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
			return m;
		}

		static Material Unlit(Color c)
		{
			Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
			return new Material(s) { color = c };
		}

		static Texture2D haloTex;

		static Texture2D HaloTexture()
		{
			if (haloTex != null) return haloTex;
			const int S = 64;
			haloTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "DI_UfoHalo", wrapMode = TextureWrapMode.Clamp };
			var px = new Color32[S * S];
			for (int y = 0; y < S; y++)
				for (int x = 0; x < S; x++)
				{
					float d = new Vector2(x + 0.5f - S / 2f, (y + 0.5f - S / 2f) * 2.2f).magnitude / (S / 2f);
					float a = Mathf.Clamp01(1f - d);
					px[y * S + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
				}
			haloTex.SetPixels32(px);
			haloTex.Apply();
			return haloTex;
		}

		static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, string name)
		{
			GameObject g = GameObject.CreatePrimitive(type);
			g.name = name;
			Collider c = g.GetComponent<Collider>();
			if (c != null) UnityEngine.Object.DestroyImmediate(c);
			g.transform.SetParent(parent, false);
			g.transform.localPosition = pos;
			g.transform.localScale = scale;
			Renderer r = g.GetComponent<Renderer>();
			r.sharedMaterial = m;
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			r.receiveShadows = false;
			return g;
		}

		/// <summary>The saucer for one crossing, at its start.</summary>
		public static UfoSaucer Create(Path p)
		{
			var root = new GameObject("CustomIslands_Ufo");
			UfoSaucer u = root.AddComponent<UfoSaucer>();
			u.Route = p;
			u.Hull = Color.HSVToRGB(p.Hue, 0.55f, 0.85f);
			u.GlowColor = p.Glow == 1 ? new Color(0.75f, 0.3f, 1f) : new Color(0.3f, 1f, 0.4f);
			u.startCenter = CustomIslandSpawner.RaftPosition ?? (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
			u.Build();
			u.Place();
			return u;
		}

		void Build()
		{
			float R = Diameter / 2f;
			spinner = new GameObject("Spinner").transform;
			spinner.SetParent(transform, false);
			Material hull = Lit(Hull, 0.8f, 0.75f);
			Material under = Lit(Hull * 0.35f + new Color(0.1f, 0.1f, 0.12f), 0.9f, 0.5f);
			Material dome = Lit(Color.Lerp(new Color(0.75f, 0.9f, 1f), GlowColor, 0.25f), 0.2f, 0.95f);
			rimMat = Lit(Hull * 0.5f, 0.9f, 0.6f);
			lightMat = Unlit(Color.white);
			// The disc, its rim band, the underside and the glass dome
			Part(PrimitiveType.Sphere, spinner, Vector3.zero, new Vector3(Diameter, Diameter * 0.16f, Diameter), hull, "Disc");
			Part(PrimitiveType.Cylinder, spinner, Vector3.zero, new Vector3(Diameter * 1.03f, 0.22f, Diameter * 1.03f), rimMat, "Rim");
			Part(PrimitiveType.Sphere, spinner, new Vector3(0f, -Diameter * 0.06f, 0f), new Vector3(Diameter * 0.42f, Diameter * 0.11f, Diameter * 0.42f), under, "Underside");
			Part(PrimitiveType.Sphere, spinner, new Vector3(0f, Diameter * 0.07f, 0f), new Vector3(Diameter * 0.38f, Diameter * 0.28f, Diameter * 0.38f), dome, "Dome");
			// A ring of rim lights (they show the spin)
			for (int i = 0; i < 12; i++)
			{
				float a = i * Mathf.PI * 2f / 12f;
				GameObject l = Part(PrimitiveType.Sphere, spinner, new Vector3(Mathf.Cos(a) * R * 1.02f, 0.05f, Mathf.Sin(a) * R * 1.02f), Vector3.one * 0.85f, lightMat, "RimLight");
				rimLights.Add(l.GetComponent<Renderer>());
			}
			// The night glow: a soft halo (faces the camera) and a light under the saucer
			haloMat = Unlit(new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f));
			haloMat.mainTexture = HaloTexture();
			halo = Part(PrimitiveType.Quad, transform, Vector3.zero, new Vector3(Diameter * 2.6f, Diameter * 2.6f, 1f), haloMat, "Halo").transform;
			var lg = new GameObject("Glow");
			lg.transform.SetParent(transform, false);
			lg.transform.localPosition = new Vector3(0f, -Diameter * 0.35f, 0f);
			glowLight = lg.AddComponent<Light>();
			glowLight.type = LightType.Point;
			glowLight.color = GlowColor;
			glowLight.range = Diameter * 4f;
			glowLight.shadows = LightShadows.None;
		}

		/// <summary>Night: 1; day: 0 (eases over a few seconds).</summary>
		float night = -1f;

		void Place()
		{
			Vector3 center = CustomIslandSpawner.RaftPosition ?? startCenter;
			float h = Route.Heading * Mathf.Deg2Rad;
			Vector3 dir = new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)), side = new Vector3(dir.z, 0f, -dir.x);
			float t = Along;
			// (a slight bob and sway, and a tilt into the turn of its wobble)
			float bob = Mathf.Sin(Age * 1.7f) * 1.5f, sway = Mathf.Sin(Age * 0.6f) * 6f;
			transform.position = new Vector3(center.x, 0f, center.z) + dir * Mathf.Lerp(-Reach, Reach, t) + side * (Route.Side + sway) + Vector3.up * (Route.Height + bob);
			transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Mathf.Sin(Age * 1.3f) * 4f + 3f, 0f, Mathf.Sin(Age * 0.9f) * 5f);
			spinner.localRotation = Quaternion.Euler(0f, Age * 50f * Route.Spin, 0f);
		}

		void Update()
		{
			Age += Time.deltaTime;
			if (Route == null || Age >= Route.Seconds || !LoadSceneManager.IsGameSceneLoaded) { Destroy(gameObject); return; }
			Place();
			float target = NightDanger.IsNight ? 1f : 0f;
			night = night < 0f ? target : Mathf.MoveTowards(night, target, Time.deltaTime * 0.3f);
			// Rim lights blink round the ring; by day warm white, at night the glow colour
			Color lc = Color.Lerp(new Color(1f, 0.95f, 0.75f), GlowColor, night);
			for (int i = 0; i < rimLights.Count; i++)
			{
				float b = 0.55f + 0.45f * Mathf.Max(0f, Mathf.Sin(Age * 6f - i * 0.9f));
				if (rimLights[i] != null) rimLights[i].material.color = lc * b;
			}
			rimMat.color = Color.Lerp(Hull * 0.5f, GlowColor * 0.8f, night * 0.6f);
			float pulse = 0.85f + 0.15f * Mathf.Sin(Age * 2.5f);
			haloMat.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, Mathf.Lerp(0.0f, 0.55f, night) * pulse);
			glowLight.intensity = Mathf.Lerp(0f, 6f, night) * pulse;
			glowLight.enabled = night > 0.01f;
			Camera cam = Camera.main;
			if (cam != null) halo.rotation = Quaternion.LookRotation(halo.position - cam.transform.position, Vector3.up);
		}

		void OnDestroy()
		{
			foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r != null && r.material != null) Destroy(r.material);
		}
	}
}

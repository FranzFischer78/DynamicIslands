using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AzureSky;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Bioluminescent sea" (WorldOptions.GlowSea): on about one night in four the water around the raft
	/// glows blue. Tiny lights twinkle on the waves and drift under them, soft blue patches light the water in places,
	/// a swimmer stirs up bright sparks, the fish shine, and a hooked fish flashes at the bobber. On those nights fish bite
	/// faster: the wait for a bite with a rod is halved.
	///   - Which nights glow comes from the world's seed and the night (the day it began): every machine knows it with no
	///     message, and Raft keeps the time of day the same for every player. The host's options decide (kind 17).
	///   - Looks only, and nothing is saved: a loaded world glows on the same nights.
	/// </summary>
	public static class GlowSea
	{
		public const float Chance = 0.25f, BiteFactor = 0.5f;
		const int Salt = 5151;

		/// <summary>Tests: glowing (true) or not (false) whatever the night says (null = the roll).</summary>
		internal static bool? TestGlow;
		/// <summary>Tests: the fishing wait Raft asked for and the one it got, at the last bite timer.</summary>
		internal static float LastWaitIn = -1f, LastWaitOut = -1f;

		static float level, nextCheck;
		static bool glowing;
		static int bannerNight = -1;
		static GlowSeaLook look;
		static AzureSkyController sky;

		public static bool On { get { return WorldOptions.On(WorldOptions.GlowSea); } }

		/// <summary>How strongly the sea glows now (0..1, fades in and out over a few seconds).</summary>
		public static float Level { get { return level; } }

		/// <summary>The look's parts (tests), or null.</summary>
		internal static GlowSeaLook Look { get { return look; } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [glowsea] " + msg); }

		internal static void Reset() { TestGlow = null; bannerNight = -1; nextCheck = 0f; }

		/// <summary>Whether this night glows in this world (the same on every machine). The night is the day it began on.</summary>
		public static bool Rolls(int seed, int night)
		{
			if (night < 0) return false; // (night 0 is a new world's first night: it rolls too)
			return WorldOptions.Unit(seed, night, Salt) < Chance;
		}

		static AzureSkyController Sky { get { if (sky == null) sky = RaftSkySea.Sky ?? UnityEngine.Object.FindObjectOfType<AzureSkyController>(); return sky; } }

		/// <summary>This night's number: the day it began on (after midnight Raft's counter is one ahead).</summary>
		public static int Night
		{
			get
			{
				int day;
				try { day = WorldManager.DayCounter; } catch { return 0; }
				AzureSkyController s = Sky;
				return s != null && s.timeOfDay.hour < 12f ? day - 1 : day;
			}
		}

		/// <summary>Tonight glows (with the option on).</summary>
		public static bool Glowing
		{
			get
			{
				if (!On) return false;
				if (TestGlow.HasValue) return TestGlow.Value;
				return NightDanger.IsNight && Rolls(WorldOptions.Seed, Night);
			}
		}

		/// <summary>The fishing wait's factor (1 unless the sea glows).</summary>
		public static float WaitFactor { get { return glowing && On ? BiteFactor : 1f; } }

		/// <summary>Every frame in a world: the fade, the banner and the look.</summary>
		internal static void Tick()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { level = 0f; glowing = false; return; }
			if (Time.time >= nextCheck)
			{
				nextCheck = Time.time + 1f;
				bool now = Glowing;
				if (now != glowing) Log(now ? "The sea glows (night " + Night + ")" : "The glow fades");
				glowing = now;
				if (glowing && bannerNight != Night && !TestGlow.HasValue)
				{
					bannerNight = Night;
					IslandInfo.Show("The sea glows tonight", "", "Tiny lights drift in the water around the raft and the fish shine blue. Fish bite faster tonight.");
				}
			}
			level = Mathf.MoveTowards(level, glowing ? 1f : 0f, Time.deltaTime / 4f);
			if (level > 0f && look == null)
			{
				var go = new GameObject("DI_GlowSea");
				look = go.AddComponent<GlowSeaLook>();
			}
			else if (level <= 0f && look != null)
			{
				UnityEngine.Object.Destroy(look.gameObject);
				look = null;
			}
		}

		[ConsoleCommand(name: "GlowSea", docs: "The world option Bioluminescent sea: GlowSea = tonight and the next glowing nights; GlowSea roll <from> <to> = the glowing nights in a range; GlowSea test on|off|auto = this machine glows or not whatever the night (tests)")]
		public static void GlowSeaCommand(string[] args)
		{
			int seed = WorldOptions.Seed, from, to;
			if (args != null && args.Length > 2 && args[0] == "roll" && int.TryParse(args[1], out from) && int.TryParse(args[2], out to))
			{
				var nights = Enumerable.Range(from, Math.Max(0, Math.Min(to - from + 1, 10000))).Where(n => Rolls(seed, n)).ToArray();
				Debug.Log("[CUSTOM ISLANDS] Glowing nights: " + from + "-" + to + " (seed " + seed + "): " + nights.Length + ": " + string.Join(" ", nights.Select(n => n.ToString()).ToArray()));
				return;
			}
			if (args != null && args.Length > 1 && args[0] == "test") TestGlow = args[1] == "on" ? true : args[1] == "off" ? false : (bool?)null;
			int night = Night, next = Enumerable.Range(night + 1, 1000).FirstOrDefault(n => Rolls(seed, n));
			Debug.Log("[CUSTOM ISLANDS] Bioluminescent sea: " + (On ? "on" : "off") + ", night " + night + (Rolls(seed, night) ? " glows" : " is dark") +
				(NightDanger.IsNight ? " (night now)" : " (day now)") + ", next glowing night " + next + ", glow " + level.ToString("0.00") + ", fishing wait x" + WaitFactor.ToString("0.##") +
				(TestGlow.HasValue ? ", test " + (TestGlow.Value ? "on" : "off") : "") + (look != null ? ", " + look.Describe() : ""));
		}
	}

	/// <summary>Faster bites on a glowing night: Raft's wait for a fish (Bobber.FishOnHook's delay) times GlowSea.WaitFactor.</summary>
	[HarmonyPatch(typeof(Bobber), "FishOnHook")]
	static class GlowSeaBitePatch
	{
		static void Prefix(ref float timeDelay)
		{
			GlowSea.LastWaitIn = timeDelay;
			timeDelay *= GlowSea.WaitFactor;
			GlowSea.LastWaitOut = timeDelay;
		}
	}

	/// <summary>
	/// The glow itself, on this machine, while GlowSea.Level > 0. Every part fades with the level:
	///   sparks on the waves (they follow the waves through a grid of the sea's heights) and some drifting under them;
	///   soft blue patches lying on the water; bright sparks stirred up around a swimmer; halos on Raft's fish;
	///   a ring at a cast bobber and a flash when a fish takes the bait; a blue light low over the water at the raft;
	///   and a little blue in the water's own glow (UltimateWater's emission colour).
	/// </summary>
	public class GlowSeaLook : MonoBehaviour
	{
		const int Sparks = 900, Patches = 70, Halos = 160, Stirred = 160;
		const float Radius = 42f, Grid = 6f;
		const int GridN = 15;

		struct Dot { public Vector3 pos; public Vector3 vel; public float age, life, size, depth; public Color32 col; }

		ParticleSystem sparkPs, patchPs, haloPs, stirPs;
		ParticleSystem.Particle[] buf;
		Dot[] sparks = new Dot[Sparks], patches = new Dot[Patches], stirred = new Dot[Stirred];
		int stirNext;
		Light raftLight, biteLight;
		float biteFlash;
		readonly float[] heights = new float[GridN * GridN];
		Vector3 gridOrigin, centre;
		UltimateWater.Water water;
		bool emissionSet;
		Color emissionBase;
		float nextScan;
		readonly List<ParticleSystem> fishSystems = new List<ParticleSystem>();
		readonly Dictionary<ParticleSystemRenderer, Material> fishMaterials = new Dictionary<ParticleSystemRenderer, Material>();
		readonly List<Bobber> bobbers = new List<Bobber>();
		readonly HashSet<Bobber> hooked = new HashSet<Bobber>();
		ParticleSystem.Particle[] fishBuf = new ParticleSystem.Particle[64];
		int haloCount;
		System.Random rng = new System.Random(12345);

		static Material sparkMaterial, patchMaterial;

		/// <summary>Tests: how many sparks, patches and fish halos show, and the light.</summary>
		internal int SparkCount { get { return sparkPs != null ? sparkPs.particleCount : 0; } }
		internal int PatchCount { get { return patchPs != null ? patchPs.particleCount : 0; } }
		internal int HaloCount { get { return haloCount; } }
		internal int FishSystems { get { return fishSystems.Count; } }
		internal float LightIntensity { get { return raftLight != null && raftLight.enabled ? raftLight.intensity : 0f; } }
		internal bool EmissionSet { get { return emissionSet; } }
		internal Color WaterEmission { get { return water != null && water.Materials != null ? water.Materials.GetParameterValue(UltimateWater.WaterMaterials.ColorParameter.EmissionColor) : Color.clear; } }

		internal string Describe()
		{
			return "sparks " + SparkCount + ", patches " + PatchCount + ", fish halos " + haloCount + " in " + fishSystems.Count + " schools, light " + LightIntensity.ToString("0.0") +
				", water emission " + (emissionSet ? WaterEmission.ToString() : "unchanged");
		}

		float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

		void Awake()
		{
			buf = new ParticleSystem.Particle[Sparks];
			sparkPs = MakeSystem("Sparks", Sparks, SparkMaterial(), ParticleSystemRenderMode.Billboard);
			patchPs = MakeSystem("Patches", Patches, PatchMaterial(), ParticleSystemRenderMode.HorizontalBillboard);
			haloPs = MakeSystem("FishHalos", Halos, PatchMaterial(), ParticleSystemRenderMode.Billboard);
			stirPs = MakeSystem("Stirred", Stirred, SparkMaterial(), ParticleSystemRenderMode.Billboard);
			raftLight = MakeLight("RaftLight", 18f);
			biteLight = MakeLight("BiteLight", 6f);
			for (int i = 0; i < Stirred; i++) stirred[i].life = 0f;
			Refresh();
			for (int i = 0; i < Sparks; i++) { NewSpark(ref sparks[i]); sparks[i].age = Rand(0f, sparks[i].life); }
			for (int i = 0; i < Patches; i++) { NewPatch(ref patches[i]); patches[i].age = Rand(0f, patches[i].life); }
		}

		ParticleSystem MakeSystem(string name, int max, Material mat, ParticleSystemRenderMode mode)
		{
			var go = new GameObject(name);
			go.transform.SetParent(transform, false);
			var ps = go.AddComponent<ParticleSystem>();
			ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			var main = ps.main;
			main.loop = true;
			main.playOnAwake = false;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = max;
			main.startSpeed = 0f;
			var emission = ps.emission;
			emission.enabled = false;
			var pr = go.GetComponent<ParticleSystemRenderer>();
			pr.sharedMaterial = mat;
			pr.renderMode = mode;
			pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			pr.receiveShadows = false;
			pr.maxParticleSize = 4f;
			ps.Play();
			return ps;
		}

		Light MakeLight(string name, float range)
		{
			var go = new GameObject(name);
			go.transform.SetParent(transform, false);
			var l = go.AddComponent<Light>();
			l.type = LightType.Point;
			l.color = new Color(0.25f, 0.65f, 1f);
			l.range = range;
			l.intensity = 0f;
			l.shadows = LightShadows.None;
			l.renderMode = LightRenderMode.ForcePixel;
			return l;
		}

		/// <summary>The centre of the glow: the raft when the player is near it, else the player.</summary>
		Vector3 Centre()
		{
			Network_Player me = null;
			try { me = RAPI.GetLocalPlayer(); } catch { }
			Vector3 p = me != null ? me.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (raft.HasValue && Flat(raft.Value - p).magnitude < 60f) p = raft.Value;
			return new Vector3(p.x, 0f, p.z);
		}

		static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

		/// <summary>The centre, the sea and the fish and bobbers near it again.</summary>
		internal void Refresh()
		{
			centre = Centre();
			if (water == null) water = RaftSkySea.Sea ?? FindObjectOfType<UltimateWater.Water>();
			SampleHeights();
			ScanFish();
		}

		void SampleHeights()
		{
			gridOrigin = centre - new Vector3(Grid * (GridN - 1) / 2f, 0f, Grid * (GridN - 1) / 2f);
			float time = 0f;
			try { if (water != null) time = water.Time; } catch { }
			for (int z = 0; z < GridN; z++)
				for (int x = 0; x < GridN; x++)
				{
					float h = 0f;
					if (water != null)
					{
						try { h = water.GetHeightAt(gridOrigin.x + x * Grid, gridOrigin.z + z * Grid, time); } catch { h = 0f; }
						if (float.IsNaN(h)) h = 0f;
					}
					heights[z * GridN + x] = h;
				}
		}

		/// <summary>The sea's height here, from the grid (the centre's level beyond it).</summary>
		float SeaY(float wx, float wz)
		{
			float fx = Mathf.Clamp((wx - gridOrigin.x) / Grid, 0f, GridN - 1.001f), fz = Mathf.Clamp((wz - gridOrigin.z) / Grid, 0f, GridN - 1.001f);
			int x = (int)fx, z = (int)fz;
			float tx = fx - x, tz = fz - z;
			float a = Mathf.Lerp(heights[z * GridN + x], heights[z * GridN + x + 1], tx), b = Mathf.Lerp(heights[(z + 1) * GridN + x], heights[(z + 1) * GridN + x + 1], tx);
			return Mathf.Lerp(a, b, tz);
		}

		Color32 Blue(float bright)
		{
			// (deep blue to cyan, now and then nearly white: one flat colour looked like a sheet)
			float t = Rand(0f, 1f);
			Color c = t < 0.55f ? Color.Lerp(new Color(0.05f, 0.25f, 1f), new Color(0.1f, 0.55f, 1f), t / 0.55f)
				: t < 0.93f ? Color.Lerp(new Color(0.1f, 0.6f, 1f), new Color(0.3f, 0.95f, 1f), (t - 0.55f) / 0.38f)
				: new Color(0.75f, 0.95f, 1f);
			return (Color32)(c * bright);
		}

		void NewSpark(ref Dot d)
		{
			// (thicker near the centre: the raft sits in its own pool of light)
			float r = Radius * Mathf.Pow(Rand(0f, 1f), 0.75f), a = Rand(0f, Mathf.PI * 2f);
			d.pos = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
			d.vel = new Vector3(Rand(-0.08f, 0.08f), 0f, Rand(-0.08f, 0.08f));
			d.age = 0f;
			d.life = Rand(1.5f, 5f);
			d.size = Rand(0f, 1f) < 0.12f ? Rand(0.25f, 0.45f) : Rand(0.08f, 0.22f);
			// (most ride just above the sampled surface: the waves between the 6 m height samples hid the ones level with it)
			d.depth = Rand(0f, 1f) < 0.15f ? Rand(0.1f, 0.5f) : -0.08f;
			d.col = Blue(Rand(0.7f, 1f));
		}

		void NewPatch(ref Dot d)
		{
			float r = Radius * 0.85f * Mathf.Sqrt(Rand(0f, 1f)), a = Rand(0f, Mathf.PI * 2f);
			d.pos = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
			d.vel = new Vector3(Rand(-0.05f, 0.05f), 0f, Rand(-0.05f, 0.05f));
			d.age = 0f;
			d.life = Rand(6f, 14f);
			d.size = Rand(2.5f, 6f);
			d.depth = -0.1f;
			d.col = Blue(Rand(0.5f, 0.8f));
		}

		void Update()
		{
			float level = GlowSea.Level, dt = Time.deltaTime;
			if (Time.time >= nextScan) { nextScan = Time.time + 1f; centre = Centre(); ScanFish(); ScanBobbers(); }
			else centre = Vector3.Lerp(centre, Centre(), dt);
			SampleHeights();
			Draw(sparkPs, sparks, level, dt, true);
			Draw(patchPs, patches, level, dt, false);
			StirAroundSwimmer(level, dt);
			Draw(stirPs, stirred, level, dt, false, true);
			FishHalos(level);
			Bobbers(level, dt);
			Lights(level);
			WaterGlow(level);
		}

		void Draw(ParticleSystem ps, Dot[] dots, float level, float dt, bool spark, bool once = false)
		{
			if (ps == null) return;
			int n = 0;
			for (int i = 0; i < dots.Length; i++)
			{
				Dot d = dots[i];
				if (once && d.life <= 0f) continue;
				d.age += dt;
				d.pos += d.vel * dt;
				if (d.age >= d.life || (!once && Flat(d.pos - centre).magnitude > Radius * 1.05f))
				{
					if (once) { d.life = 0f; dots[i] = d; continue; }
					if (spark) NewSpark(ref d); else NewPatch(ref d);
				}
				dots[i] = d;
				float t = d.age / d.life;
				// (a twinkle: quick up, slow down; patches swell and fade slowly)
				float a = spark || once ? Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 1.6f) : Mathf.Sin(t * Mathf.PI);
				if (spark) a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 7f + i);
				if (d.depth > 0f) a *= Mathf.Lerp(0.8f, 0.45f, d.depth / 0.5f);
				a *= level * (spark || once ? 1f : 0.35f);
				var p = new ParticleSystem.Particle();
				p.position = new Vector3(d.pos.x, SeaY(d.pos.x, d.pos.z) - d.depth, d.pos.z);
				p.startSize = d.size * (spark ? 1f : (0.8f + 0.2f * t));
				p.startLifetime = 1000f;
				p.remainingLifetime = 999f;
				p.rotation = i * 37f;
				Color32 c = d.col;
				p.startColor = new Color32((byte)(c.r * a), (byte)(c.g * a), (byte)(c.b * a), (byte)(255 * a));
				if (n < buf.Length) buf[n++] = p;
			}
			ps.SetParticles(buf, n);
		}

		/// <summary>Sparks stirred up where the local player swims (the sea lights up where something moves).</summary>
		void StirAroundSwimmer(float level, float dt)
		{
			Network_Player me = null;
			try { me = RAPI.GetLocalPlayer(); } catch { }
			if (me == null || me.PersonController == null || me.PersonController.controllerType != ControllerType.Water) return;
			int count = Mathf.RoundToInt(dt * 70f * level) + (Rand(0f, 1f) < dt * 70f * level % 1f ? 1 : 0);
			Vector3 at = me.transform.position;
			for (int k = 0; k < count; k++) Stir(at, 1.2f, Rand(0.6f, 1.6f));
		}

		void Stir(Vector3 at, float spread, float life)
		{
			Dot d = new Dot();
			float a = Rand(0f, Mathf.PI * 2f), r = Rand(0.1f, spread);
			d.pos = new Vector3(at.x + Mathf.Cos(a) * r, 0f, at.z + Mathf.Sin(a) * r);
			d.vel = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Rand(0.1f, 0.5f);
			d.life = life;
			d.size = Rand(0.06f, 0.2f);
			d.depth = Rand(-0.05f, 0.5f);
			d.col = Blue(1f);
			stirred[stirNext] = d;
			stirNext = (stirNext + 1) % Stirred;
		}

		/// <summary>Raft's fish schools near the glow: their particle systems (each fish is one particle) and a glowing material.</summary>
		void ScanFish()
		{
			fishSystems.RemoveAll(s => s == null);
			foreach (FishSchool school in FindObjectsOfType<FishSchool>())
			{
				if (school == null || Flat(school.transform.position - centre).magnitude > Radius + 30f) continue;
				foreach (ParticleSystem ps in school.GetComponentsInChildren<ParticleSystem>())
				{
					if (fishSystems.Contains(ps)) continue;
					fishSystems.Add(ps);
					ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
					if (pr == null || pr.sharedMaterial == null || fishMaterials.ContainsKey(pr)) continue;
					// (the fish themselves shine when their shader has an emission colour; the halo shows either way)
					if (!pr.sharedMaterial.HasProperty("_EmissionColor")) continue;
					fishMaterials[pr] = pr.sharedMaterial;
					var glow = new Material(pr.sharedMaterial) { name = pr.sharedMaterial.name + " (DI glow)" };
					glow.EnableKeyword("_EMISSION");
					pr.sharedMaterial = glow;
				}
			}
		}

		void FishHalos(float level)
		{
			if (haloPs == null) return;
			int n = 0;
			Color emit = new Color(0.1f, 0.45f, 1f) * (0.9f * level);
			foreach (KeyValuePair<ParticleSystemRenderer, Material> kv in fishMaterials)
				if (kv.Key != null && kv.Key.sharedMaterial != null && kv.Key.sharedMaterial != kv.Value) kv.Key.sharedMaterial.SetColor("_EmissionColor", emit);
			foreach (ParticleSystem ps in fishSystems)
			{
				if (ps == null || n >= Halos) continue;
				int count = ps.particleCount;
				if (count == 0) continue;
				if (fishBuf.Length < count) fishBuf = new ParticleSystem.Particle[count];
				count = ps.GetParticles(fishBuf);
				bool local = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
				for (int i = 0; i < count && n < Halos; i++)
				{
					Vector3 at = local ? ps.transform.TransformPoint(fishBuf[i].position) : fishBuf[i].position;
					var p = new ParticleSystem.Particle();
					p.position = at;
					p.startSize = Mathf.Clamp(fishBuf[i].GetCurrentSize(ps) * 2.2f, 0.5f, 1.6f);
					p.startLifetime = 1000f;
					p.remainingLifetime = 999f;
					float a = level * (0.35f + 0.1f * Mathf.Sin(Time.time * 3f + i));
					p.startColor = new Color(0.15f * a, 0.6f * a, 1f * a, a);
					buf[n++] = p;
					// (a fish leaves a few sparks behind it)
					if (Rand(0f, 1f) < Time.deltaTime * 1.5f * level) Stir(at + Vector3.up * 0.2f, 0.3f, Rand(0.8f, 1.5f));
				}
			}
			haloCount = n;
			haloPs.SetParticles(buf, n);
		}

		void ScanBobbers()
		{
			bobbers.Clear();
			foreach (Bobber b in FindObjectsOfType<Bobber>())
				if (b != null && b.gameObject.activeInHierarchy && Flat(b.transform.position - centre).magnitude < Radius + 40f) bobbers.Add(b);
		}

		/// <summary>A ring of sparks around a cast bobber; a flash and a light when a fish takes the bait.</summary>
		void Bobbers(float level, float dt)
		{
			biteFlash = Mathf.MoveTowards(biteFlash, 0f, dt * 0.8f);
			foreach (Bobber b in bobbers)
			{
				if (b == null || !b.gameObject.activeInHierarchy) continue;
				Vector3 at = b.transform.position;
				if (Rand(0f, 1f) < dt * 6f * level) Stir(at, 0.8f, Rand(0.8f, 1.6f));
				bool on = false;
				try { on = b.FishIsOnHook; } catch { }
				if (on)
				{
					if (!hooked.Contains(b)) { hooked.Add(b); biteFlash = 1f; for (int k = 0; k < 30; k++) Stir(at, 1.4f, Rand(0.8f, 2f)); }
					if (Rand(0f, 1f) < dt * 25f * level) Stir(at, 1f, Rand(0.5f, 1.2f));
					biteLight.transform.position = at + Vector3.up * 0.4f;
					biteFlash = Mathf.Max(biteFlash, 0.5f);
				}
				else hooked.Remove(b);
			}
			biteLight.intensity = biteFlash * 3f * level;
			biteLight.enabled = biteLight.intensity > 0.01f;
		}

		void Lights(float level)
		{
			raftLight.transform.position = new Vector3(centre.x, SeaY(centre.x, centre.z) + 0.6f, centre.z);
			raftLight.intensity = level * (1.4f + 0.15f * Mathf.Sin(Time.time * 0.7f));
			raftLight.enabled = level > 0.01f;
		}

		/// <summary>A little blue in the water's own glow, on top of what it had.</summary>
		void WaterGlow(float level)
		{
			if (water == null || water.Materials == null) return;
			try
			{
				var param = UltimateWater.WaterMaterials.ColorParameter.EmissionColor;
				if (!emissionSet) { emissionBase = water.Materials.GetParameterValue(param); emissionSet = true; }
				Color c = emissionBase + new Color(0.012f, 0.05f, 0.11f, 0f) * level;
				water.Materials.GetParameterOverride(param).Value = new Vector4(c.r, c.g, c.b, emissionBase.a);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [glowsea] The water's glow: " + e.Message); water = null; }
		}

		void OnDestroy()
		{
			try { if (emissionSet && water != null && water.Materials != null) water.Materials.ResetParameter(UltimateWater.WaterMaterials.ColorParameter.EmissionColor); } catch { }
			foreach (KeyValuePair<ParticleSystemRenderer, Material> kv in fishMaterials)
				if (kv.Key != null)
				{
					Material glow = kv.Key.sharedMaterial;
					kv.Key.sharedMaterial = kv.Value;
					if (glow != null && glow != kv.Value) Destroy(glow);
				}
			fishMaterials.Clear();
		}

		/// <summary>A soft round dot, brighter in the middle.</summary>
		static Texture2D DotTexture(int size, float power, string name)
		{
			var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
			var px = new Color32[size * size];
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float dx = (x + 0.5f - size / 2f) / (size / 2f), dy = (y + 0.5f - size / 2f) / (size / 2f);
					float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), power);
					px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
				}
			tex.SetPixels32(px);
			tex.Apply();
			return tex;
		}

		static Material Additive(string name, Texture2D tex)
		{
			Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Mobile/Particles/Additive");
			if (shader == null) shader = AtmosphereZone.ParticleMaterial().shader;
			var m = new Material(shader) { name = name, mainTexture = tex };
			if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", Color.white);
			return m;
		}

		static Material SparkMaterial() { if (sparkMaterial == null) sparkMaterial = Additive("DI_GlowSpark", DotTexture(32, 2.2f, "DI_GlowSpark")); return sparkMaterial; }
		static Material PatchMaterial() { if (patchMaterial == null) patchMaterial = Additive("DI_GlowPatch", DotTexture(64, 1.6f, "DI_GlowPatch")); return patchMaterial; }
	}
}

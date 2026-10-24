using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using HMLLibrary;
using RaftModLoader;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The atmosphere editor's zones: near one, the fog takes the zone's colour and thickness, the ambient light takes
	/// its tint, and its particles (fireflies, mist, snow, embers) drift around. The strength fades in over the outer
	/// third of the radius. The changes are made only while the main camera renders and undone right after, so Raft's
	/// own weather and day/night cycle are never touched. Works in the editor too (fly the camera into the zone).
	/// </summary>
	public class AtmosphereZone : MonoBehaviour
	{
		public float Radius = 20f;
		public Color Fog = Color.white, Light = Color.white;
		public float FogAmount, LightAmount;
		public string Particles = "none";

		static readonly List<AtmosphereZone> zones = new List<AtmosphereZone>();
		static bool hooked;

		void OnEnable() { zones.Add(this); Hook(); }
		void OnDisable() { zones.Remove(this); }

		public static readonly string[] ParticleKinds = { "none", "fireflies", "mist", "snow", "embers", "bubbles" };

		/// <summary>Sets a zone up from its settings (on a spawned island, or live in the editor).</summary>
		public void Configure(IDictionary<string, string> p)
		{
			Radius = ObjectProps.Radius(p);
			Fog = ObjectProps.HasColor(p, ObjectProps.AtmoFog) ? ObjectProps.ColorOf(p, ObjectProps.AtmoFog) : Color.white;
			FogAmount = ObjectProps.HasColor(p, ObjectProps.AtmoFog) ? Mathf.Clamp01(ObjectProps.GetFloat(p, ObjectProps.AtmoFogAmount, 0.6f)) : 0f;
			Light = ObjectProps.HasColor(p, ObjectProps.AtmoLight) ? ObjectProps.ColorOf(p, ObjectProps.AtmoLight) : Color.white;
			LightAmount = ObjectProps.HasColor(p, ObjectProps.AtmoLight) ? Mathf.Clamp01(ObjectProps.GetFloat(p, ObjectProps.AtmoLightAmount, 0.5f)) : 0f;
			string kind = ObjectProps.Get(p, ObjectProps.AtmoParticles, "none");
			if (kind != Particles || transform.Find("CI_Particles") == null || Mathf.Abs(particleRadius - Radius) > 0.01f) MakeParticles(kind);
		}

		float particleRadius;

		void MakeParticles(string kind)
		{
			Transform old = transform.Find("CI_Particles");
			if (old != null) Destroy(old.gameObject);
			Particles = kind;
			particleRadius = Radius;
			if (kind == "none" || !ParticleKinds.Contains(kind)) return;
			var go = new GameObject("CI_Particles");
			go.transform.SetParent(transform, false);
			var ps = go.AddComponent<ParticleSystem>();
			ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			var main = ps.main;
			var emission = ps.emission;
			var shape = ps.shape;
			var noise = ps.noise;
			var col = ps.colorOverLifetime;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.playOnAwake = true;
			main.loop = true;
			float area = Mathf.PI * Radius * Radius;
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = Radius;
			col.enabled = true;
			var fade = new Gradient();
			fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
			col.color = fade;
			switch (kind)
			{
				case "fireflies":
					main.startLifetime = 5f; main.startSpeed = 0.2f; main.startSize = 0.12f; main.maxParticles = 400;
					main.startColor = new Color(1f, 0.95f, 0.45f, 1f);
					emission.rateOverTime = Mathf.Clamp(area * 0.03f, 5f, 80f);
					shape.shapeType = ParticleSystemShapeType.Hemisphere;
					noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.4f;
					break;
				case "mist":
					main.startLifetime = 12f; main.startSpeed = 0.1f; main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f); main.maxParticles = 300;
					main.startColor = new Color(1f, 1f, 1f, 0.12f);
					emission.rateOverTime = Mathf.Clamp(area * 0.01f, 3f, 25f);
					shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(90, 0, 0);
					break;
				case "snow":
					main.startLifetime = 8f; main.startSpeed = 0f; main.startSize = 0.08f; main.maxParticles = 3000; main.gravityModifier = 0.05f;
					main.startColor = new Color(1f, 1f, 1f, 0.9f);
					emission.rateOverTime = Mathf.Clamp(area * 0.12f, 20f, 400f);
					shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(90, 0, 0); shape.position = new Vector3(0, 15f, 0);
					noise.enabled = true; noise.strength = 0.3f; noise.frequency = 0.3f;
					break;
				case "bubbles":
					// (air rising from a spot under water: an air pocket filling, a vent)
					main.startLifetime = 4f; main.startSpeed = 0.5f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f); main.maxParticles = 600; main.gravityModifier = -0.15f;
					main.startColor = new Color(0.85f, 0.95f, 1f, 0.8f);
					emission.rateOverTime = Mathf.Clamp(area * 0.5f, 10f, 120f);
					shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(90, 0, 0); shape.radius = Mathf.Min(Radius, 1.5f);
					noise.enabled = true; noise.strength = 0.25f; noise.frequency = 1.2f;
					break;
				case "embers":
					main.startLifetime = 4f; main.startSpeed = 1f; main.startSize = 0.07f; main.maxParticles = 800; main.gravityModifier = -0.08f;
					main.startColor = new Color(1f, 0.5f, 0.15f, 1f);
					emission.rateOverTime = Mathf.Clamp(area * 0.04f, 10f, 120f);
					shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(90, 0, 0);
					noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.8f;
					break;
			}
			var r = go.GetComponent<ParticleSystemRenderer>();
			r.sharedMaterial = ParticleMaterial();
			r.shadowCastingMode = ShadowCastingMode.Off;
			r.receiveShadows = false;
			ps.Play();
		}

		static Material particleMaterial;

		/// <summary>A soft round dot, drawn with a shader every Unity build has.</summary>
		internal static Material ParticleMaterial()
		{
			if (particleMaterial != null) return particleMaterial;
			const int S = 32;
			var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "CI_ParticleDot", wrapMode = TextureWrapMode.Clamp };
			var px = new Color32[S * S];
			for (int y = 0; y < S; y++)
				for (int x = 0; x < S; x++)
				{
					float d = new Vector2(x + 0.5f - S / 2f, y + 0.5f - S / 2f).magnitude / (S / 2f);
					px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255));
				}
			tex.SetPixels32(px);
			tex.Apply();
			// Plain alpha-blended shaders first: Standard Unlit starts out opaque (the dots came out as white squares)
			Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
			if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
			if (shader == null) shader = Shader.Find("Sprites/Default");
			if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
			particleMaterial = new Material(shader) { name = "CI_Particles", mainTexture = tex };
			if (particleMaterial.HasProperty("_Mode"))
			{
				// Standard (Unlit) particle shaders: switch to "fade" blending
				particleMaterial.SetFloat("_Mode", 2f);
				particleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				particleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				particleMaterial.SetInt("_ZWrite", 0);
				particleMaterial.EnableKeyword("_ALPHABLEND_ON");
				particleMaterial.renderQueue = 3000;
			}
			Debug.Log("[CUSTOM ISLANDS] Particles use shader " + shader.name);
			return particleMaterial;
		}

		#region Applying while the camera renders

		struct Saved
		{
			public bool Fog; public Color FogColor; public float Density, Start, End;
			public SphericalHarmonicsL2 Probe; public Color Ambient;
			public Light Sun; public Color SunColor; public float Reflection; public Color Equator, Ground;
		}

		static Saved saved;
		static bool applied;

		/// <summary>The sun's colour before, and while the camera last drew in a zone with a light tint (the tests read it).</summary>
		public static Color SunBefore { get; private set; }
		public static Color SunWhileDrawn { get; private set; }
		static Image screenTint;
		static Light sunFound;
		static float sunLookedAt = -10f;

		/// <summary>The sun (or the moon at night): the scene's sun light, else its brightest directional light.</summary>
		static Light Sun()
		{
			if (RenderSettings.sun != null) return RenderSettings.sun;
			if ((sunFound == null || !sunFound.isActiveAndEnabled) && Time.unscaledTime - sunLookedAt > 2f)
			{
				sunLookedAt = Time.unscaledTime;
				sunFound = FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
			}
			return sunFound;
		}

		static void Hook()
		{
			if (hooked) return;
			hooked = true;
			Camera.onPreRender += PreRender;
			Camera.onPostRender += PostRender;
		}

		/// <summary>Takes the camera handlers off again (the mod's Unload, AU40).</summary>
		internal static void Unhook()
		{
			if (!hooked) return;
			hooked = false;
			Camera.onPreRender -= PreRender;
			Camera.onPostRender -= PostRender;
		}

		/// <summary>How strongly the zone acts at a point: 1 inside two thirds of its radius, fading to 0 at the edge.</summary>
		public float WeightAt(Vector3 p)
		{
			float d = Vector3.Distance(p, transform.position);
			return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Radius * 0.67f, Radius, d));
		}

		/// <summary>The zone acting most at a point, and how much (tests use it).</summary>
		public static AtmosphereZone Strongest(Vector3 p, out float weight)
		{
			weight = 0f;
			AtmosphereZone best = null;
			foreach (AtmosphereZone z in zones)
			{
				if (z == null || !z.isActiveAndEnabled) continue;
				float w = z.WeightAt(p);
				if (w > weight) { weight = w; best = z; }
			}
			return best;
		}

		static void PreRender(Camera cam)
		{
			if (cam != Camera.main || applied) return;
			float w;
			AtmosphereZone z = Strongest(cam.transform.position, out w);
			SetScreenTint(z, w);
			if (z == null || w <= 0.001f || (z.FogAmount <= 0f && z.LightAmount <= 0f)) return;
			saved = new Saved
			{
				Fog = RenderSettings.fog, FogColor = RenderSettings.fogColor, Density = RenderSettings.fogDensity,
				Start = RenderSettings.fogStartDistance, End = RenderSettings.fogEndDistance, Probe = RenderSettings.ambientProbe, Ambient = RenderSettings.ambientLight,
				Reflection = RenderSettings.reflectionIntensity, Equator = RenderSettings.ambientEquatorColor, Ground = RenderSettings.ambientGroundColor
			};
			applied = true;
			if (z.FogAmount > 0f)
			{
				float f = w * z.FogAmount;
				RenderSettings.fog = true;
				RenderSettings.fogColor = Color.Lerp(saved.FogColor, z.Fog, Mathf.Clamp01(w * Mathf.Min(1f, z.FogAmount * 1.5f)));
				RenderSettings.fogDensity = Mathf.Lerp(saved.Fog ? saved.Density : 0f, 0.06f, f);
				RenderSettings.fogStartDistance = Mathf.Lerp(saved.Fog ? saved.Start : 300f, 0f, f);
				RenderSettings.fogEndDistance = Mathf.Lerp(saved.Fog ? saved.End : 1000f, 40f, f);
			}
			if (z.LightAmount > 0f)
			{
				Color k = Color.Lerp(Color.white, z.Light, w * z.LightAmount);
				SphericalHarmonicsL2 sh = saved.Probe;
				for (int c = 0; c < 9; c++) { sh[0, c] *= k.r; sh[1, c] *= k.g; sh[2, c] *= k.b; }
				RenderSettings.ambientProbe = sh;
				RenderSettings.ambientLight = saved.Ambient * k;
				// (Raft lights with three ambient colours - sky, horizon, ground: all three take the tint, or setting the sky
				// alone rebuilds the ambient from a bright horizon and ground)
				RenderSettings.ambientEquatorColor = saved.Equator * k;
				RenderSettings.ambientGroundColor = saved.Ground * k;
				// (the sun and the sky's reflections take the tint too: Raft's sun lit a cave or a buried room as brightly
				// as the beach, so a dark zone wasn't dark - found building the library's Shelter Atoll)
				saved.Sun = Sun();
				if (saved.Sun != null) { saved.SunColor = saved.Sun.color; saved.Sun.color = saved.SunColor * k; SunBefore = saved.SunColor; SunWhileDrawn = saved.Sun.color; }
				RenderSettings.reflectionIntensity = saved.Reflection * (k.r + k.g + k.b) / 3f;
			}
		}

		static void PostRender(Camera cam)
		{
			if (cam != Camera.main || !applied) return;
			applied = false;
			RenderSettings.fog = saved.Fog;
			RenderSettings.fogColor = saved.FogColor;
			RenderSettings.fogDensity = saved.Density;
			RenderSettings.fogStartDistance = saved.Start;
			RenderSettings.fogEndDistance = saved.End;
			RenderSettings.ambientProbe = saved.Probe;
			RenderSettings.ambientLight = saved.Ambient;
			RenderSettings.ambientEquatorColor = saved.Equator;
			RenderSettings.ambientGroundColor = saved.Ground;
			RenderSettings.reflectionIntensity = saved.Reflection;
			if (saved.Sun != null) { saved.Sun.color = saved.SunColor; saved.Sun = null; }
		}

		/// <summary>
		/// A faint colour over the picture as well, so a zone shows even where Raft draws its own sky fog.
		/// (A canvas behind every other one: the game's HUD stays untinted.)
		/// </summary>
		static void SetScreenTint(AtmosphereZone z, float w)
		{
			float alpha = z == null ? 0f : Mathf.Max(z.FogAmount * 0.18f, z.LightAmount * 0.12f) * w;
			if (screenTint == null)
			{
				if (alpha <= 0.001f) return;
				Canvas canvas = UIKit.CreateCanvas("CustomIslands_AtmosphereTint", -100);
				DontDestroyOnLoad(canvas.gameObject);
				RectTransform r = UIKit.Rect("Tint", canvas.transform);
				UIKit.Stretch(r);
				screenTint = r.gameObject.AddComponent<Image>();
				screenTint.raycastTarget = false;
			}
			Color c = z != null ? (z.FogAmount >= z.LightAmount ? z.Fog : z.Light) : Color.clear;
			screenTint.color = new Color(c.r, c.g, c.b, alpha);
			screenTint.enabled = alpha > 0.001f;
		}

		#endregion
	}

	/// <summary>
	/// The sound editor's zones: Raft's own sounds (FMOD events, e.g. birds, wind, waves, music) play while a player is
	/// inside (a loop that fades with distance and stops outside), or once each time a player walks in. Each machine
	/// plays them for its own player.
	/// </summary>
	public class SoundZone : MonoBehaviour
	{
		public float Radius = 20f, Volume = 0.8f;
		public string Event = "";
		public bool Once;

		FMOD.Studio.EventInstance instance;
		bool playing, inside;
		float nextCheck;

		public void Configure(IDictionary<string, string> p)
		{
			Radius = ObjectProps.Radius(p);
			Volume = Mathf.Clamp01(ObjectProps.GetFloat(p, ObjectProps.SoundVolume, 0.8f));
			Event = ObjectProps.Get(p, ObjectProps.SoundEvent);
			Once = ObjectProps.Get(p, ObjectProps.SoundMode) == "enter";
		}

		/// <summary>Is the sound playing for this player now (tests look at it).</summary>
		public bool Playing { get { return playing; } }

		void Update()
		{
			if (Event.Length == 0 || Time.time < nextCheck) return;
			nextCheck = Time.time + 0.2f;
			Network_Player player = null;
			try { player = RAPI.GetLocalPlayer(); } catch { }
			if (player == null) return;
			float d = Vector3.Distance(player.transform.position, transform.position);
			bool now = d <= Radius;
			if (now && !inside && Once) SoundLibrary.PlayOnce(Event, transform.position, Volume);
			inside = now;
			if (Once) return;
			if (now && !playing) Start3D();
			else if (!now && playing) Stop();
			if (playing)
			{
				// Louder towards the middle
				instance.setVolume(Volume * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Radius * 0.5f, Radius, d))));
				instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform.position));
			}
		}

		void Start3D()
		{
			try
			{
				instance = FMODUnity.RuntimeManager.CreateInstance(Event);
				instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform.position));
				instance.setVolume(0f);
				instance.start();
				playing = true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Sound zone: can't play '" + Event + "': " + e.Message); Event = ""; }
		}

		void Stop()
		{
			if (!playing) return;
			playing = false;
			try { instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); instance.release(); } catch { }
		}

		void OnDisable() { Stop(); inside = false; }
	}

	/// <summary>Raft's sounds (FMOD events) for the sound picker, and playing them.</summary>
	public static class SoundLibrary
	{
		static List<string> events;

		/// <summary>Every event path in Raft's loaded sound banks ("event:/..."), sorted.</summary>
		public static List<string> Events
		{
			get
			{
				if (events != null && events.Count > 0) return events;
				events = new List<string>();
				try
				{
					FMOD.Studio.Bank[] banks;
					FMODUnity.RuntimeManager.StudioSystem.getBankList(out banks);
					foreach (FMOD.Studio.Bank bank in banks)
					{
						FMOD.Studio.EventDescription[] list;
						if (bank.getEventList(out list) != FMOD.RESULT.OK || list == null) continue;
						foreach (FMOD.Studio.EventDescription d in list)
						{
							string path;
							if (d.getPath(out path) == FMOD.RESULT.OK && !string.IsNullOrEmpty(path)) events.Add(path);
						}
					}
					events = events.Distinct().OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's sounds: " + e.Message); }
				return events;
			}
		}

		public static bool IsLooping(string path)
		{
			try
			{
				FMOD.Studio.EventDescription d;
				bool oneshot;
				return FMODUnity.RuntimeManager.StudioSystem.getEvent(path, out d) == FMOD.RESULT.OK && d.isOneshot(out oneshot) == FMOD.RESULT.OK && !oneshot;
			}
			catch { return false; }
		}

		/// <summary>Tests: how many times a sound was asked for here (a zone's "enter" sound, the action sound), and the last one's path.</summary>
		internal static int Played;
		internal static string LastPlayed;

		public static void PlayOnce(string path, Vector3 at, float volume)
		{
			Played++;
			LastPlayed = path;
			try
			{
				FMOD.Studio.EventInstance i = FMODUnity.RuntimeManager.CreateInstance(path);
				i.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(at));
				i.setVolume(volume);
				i.start();
				i.release();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Can't play '" + path + "': " + e.Message); }
		}

		static FMOD.Studio.EventInstance preview;
		static bool previewing;

		/// <summary>Editor: plays a sound at the camera to listen to it (a second call stops it).</summary>
		public static void Preview(string path)
		{
			StopPreview();
			try
			{
				preview = FMODUnity.RuntimeManager.CreateInstance(path);
				Vector3 at = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
				preview.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(at));
				preview.start();
				previewing = true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Can't play '" + path + "': " + e.Message); }
		}

		public static void StopPreview()
		{
			if (!previewing) return;
			previewing = false;
			try { preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); preview.release(); } catch { }
		}
	}
}

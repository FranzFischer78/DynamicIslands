using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Light in the editor (the original roadmap's "good lighting in the editor"): the time of day - Morning, Noon, Evening,
	/// Night, Overcast - sets the sun (the scene's directional light, or one of the mod's own), its colour and shadows, the
	/// sky's light on everything (ambient) and a light haze, so an island can be looked at as it will be in Raft at that
	/// time. The Light button in the top bar goes through them; the choice is kept (Mods\DynamicIslands\editor_light.txt).
	/// </summary>
	public static class EditorLighting
	{
		public static readonly string[] Names = { "Morning", "Noon", "Evening", "Night", "Overcast" };

		class Preset
		{
			public float Height, Turn, Intensity, Shadow, Fog; // (Fog: metres where the haze starts)
			public Color Sun, Sky, Equator, Ground, Haze;
		}

		// (tuned from pictures of a generated island: the haze only far out, night still light enough to work in)
		static readonly Preset[] presets =
		{
			new Preset { Height = 20f, Turn = 80f, Intensity = 0.95f, Shadow = 0.7f, Sun = new Color(1f, 0.86f, 0.7f), Sky = new Color(0.56f, 0.62f, 0.72f), Equator = new Color(0.55f, 0.52f, 0.48f), Ground = new Color(0.28f, 0.26f, 0.24f), Haze = new Color(0.8f, 0.78f, 0.74f), Fog = 900f },
			new Preset { Height = 55f, Turn = 150f, Intensity = 1.0f, Shadow = 0.75f, Sun = new Color(1f, 0.97f, 0.92f), Sky = new Color(0.46f, 0.56f, 0.7f), Equator = new Color(0.46f, 0.5f, 0.52f), Ground = new Color(0.26f, 0.25f, 0.22f), Haze = new Color(0.68f, 0.78f, 0.88f), Fog = 1400f },
			new Preset { Height = 14f, Turn = 250f, Intensity = 0.95f, Shadow = 0.65f, Sun = new Color(1f, 0.76f, 0.56f), Sky = new Color(0.52f, 0.52f, 0.62f), Equator = new Color(0.6f, 0.5f, 0.45f), Ground = new Color(0.25f, 0.21f, 0.2f), Haze = new Color(0.84f, 0.68f, 0.58f), Fog = 900f },
			new Preset { Height = 45f, Turn = 200f, Intensity = 0.55f, Shadow = 0.45f, Sun = new Color(0.7f, 0.78f, 1f), Sky = new Color(0.34f, 0.4f, 0.58f), Equator = new Color(0.28f, 0.32f, 0.44f), Ground = new Color(0.1f, 0.1f, 0.14f), Haze = new Color(0.12f, 0.15f, 0.25f), Fog = 700f },
			new Preset { Height = 75f, Turn = 150f, Intensity = 0.62f, Shadow = 0.3f, Sun = new Color(0.92f, 0.93f, 0.95f), Sky = new Color(0.62f, 0.64f, 0.68f), Equator = new Color(0.56f, 0.57f, 0.6f), Ground = new Color(0.32f, 0.32f, 0.32f), Haze = new Color(0.7f, 0.72f, 0.76f), Fog = 600f },
		};

		/// <summary>The object pictures: rendered in the Noon sky light whatever time of day the island is shown in (they
		/// came out blue at Night). Returns what to put back.</summary>
		public static Action NeutralAmbient()
		{
			AmbientMode mode = RenderSettings.ambientMode;
			Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
			Preset p = presets[1];
			RenderSettings.ambientMode = AmbientMode.Trilight;
			RenderSettings.ambientSkyColor = p.Sky; RenderSettings.ambientEquatorColor = p.Equator; RenderSettings.ambientGroundColor = p.Ground;
			return () => { RenderSettings.ambientMode = mode; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground; };
		}

		/// <summary>The time of day shown now (an index into Names).</summary>
		public static int Current { get; private set; }
		static Light sun;

		static string FilePath { get { return Path.Combine(DynamicIslands.assetpath, "editor_light.txt"); } }

		/// <summary>The editor has opened: the kept time of day (Noon the first time).</summary>
		public static void OnEditorOpened()
		{
			int i = 1;
			try { if (File.Exists(FilePath)) i = Array.FindIndex(Names, n => n.Equals(File.ReadAllText(FilePath).Trim(), StringComparison.OrdinalIgnoreCase)); } catch { }
			Apply(i < 0 ? 1 : i, false);
		}

		/// <summary>The top bar's Light button: the next time of day.</summary>
		public static void Next() { Apply((Current + 1) % Names.Length, true); }

		public static void Apply(int index, bool keep)
		{
			Current = Mathf.Clamp(index, 0, Names.Length - 1);
			// (Raft's sky shown - RaftSkySea: its own sun, sky light and fog at the hour for this time of day)
			if (RaftSkySea.SetTimeOfDay(Current)) { Keep(keep); return; }
			Preset p = presets[Current];
			if (sun == null)
			{
				// (the editor scene's own sun if it has one; else one of the mod's)
				// (not one of the object pictures' own lights - they are off between pictures and only light their layer)
				sun = UnityEngine.Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()
					&& l.GetComponentInParent<ObjectThumbnails>() == null && l.GetComponentInParent<UnityEngine.AzureSky.AzureSkyController>() == null); // (nor Raft's sky's - RaftSkySea)
				if (sun == null)
				{
					var go = new GameObject("CustomIslands_EditorSun");
					sun = go.AddComponent<Light>();
					sun.type = LightType.Directional;
				}
			}
			sun.transform.rotation = Quaternion.Euler(p.Height, p.Turn, 0f);
			sun.color = p.Sun;
			sun.intensity = p.Intensity;
			sun.shadows = LightShadows.Soft;
			sun.shadowStrength = p.Shadow;
			sun.cullingMask = ~(1 << ObjectThumbnails.Layer); // (the object pictures have their own light)
			RenderSettings.sun = sun;
			RenderSettings.ambientMode = AmbientMode.Trilight;
			RenderSettings.ambientSkyColor = p.Sky;
			RenderSettings.ambientEquatorColor = p.Equator;
			RenderSettings.ambientGroundColor = p.Ground;
			RenderSettings.fog = true;
			RenderSettings.fogMode = FogMode.Linear;
			RenderSettings.fogColor = p.Haze;
			RenderSettings.fogStartDistance = p.Fog;
			RenderSettings.fogEndDistance = p.Fog * 4f;
			Camera cam = Camera.main;
			if (cam != null && cam.clearFlags != CameraClearFlags.Skybox) cam.backgroundColor = p.Haze;
			Keep(keep);
		}

		/// <summary>The time of day picked in the Light list: kept for the next time, and said.</summary>
		static void Keep(bool keep)
		{
			if (!keep) return;
			try { SafeFile.WriteAllText(FilePath, Names[Current]); } catch { } // (in one step - AU41)
			DynamicIslands.Notify("Light: " + Names[Current] + " (how the island looks at that time of day; only in the editor)");
		}
	}
}

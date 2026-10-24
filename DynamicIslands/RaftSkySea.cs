using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AzureSky;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Raft's sky and sea" in the editor (ROADMAP E1b): Raft's own AzureSky and UltimateWater ocean instead of the editor's
	/// plain sky and flat blue plane. Raft's main menu has both - its root objects "_SP_Azure[Sky]Controller_Raft_MainMenu"
	/// (AzureSkyController + SkyManager, with the sun, moon and the sun's light), "Water" (UltimateWater.Water) and the
	/// "Camera" that draws them (WaterCamera, WaterCameraIME, AzureSkyFogScattering, VolumetricLightRenderer,
	/// PostProcessingBehaviour). Raft's game scene (MainScene) is not used: loading it starts a world (network, saves,
	/// player). So, as the editor opens from the main menu, those three are kept (DontDestroyOnLoad) while the editor scene
	/// replaces the menu, then moved into the editor's scene, and the menu camera becomes the editor's camera (the editor
	/// scene's own camera is switched off). They are part of the editor scene from then on, so leaving the editor (Main
	/// menu, Test) takes them away with it; the main menu brings its own again.
	/// Off (the default) changes nothing: the editor opens as before. The choice is kept (Mods\DynamicIslands\editor_skysea.txt)
	/// and taken when the editor opens; in the editor it can be switched off and on again (the menu's pieces stay there,
	/// hidden), but switched on in an editor opened without them it shows from the next opening.
	/// </summary>
	public static class RaftSkySea
	{
		/// <summary>The hour of Raft's sky for each of EditorLighting's times of day (Morning, Noon, Evening, Night, Overcast).
		/// (Overcast: Raft's afternoon - the menu's sky has no weather of its own to switch to.)</summary>
		static readonly float[] hours = { 7.5f, 12.5f, 18.25f, 22f, 15f };

		const string MenuScene = "MainMenuScene";

		static string FilePath { get { return Path.Combine(DynamicIslands.assetpath, "editor_skysea.txt"); } }

		static bool? enabled;
		/// <summary>The kept choice: Raft's sky and sea in the editor (off unless chosen).</summary>
		public static bool Enabled
		{
			get
			{
				if (!enabled.HasValue)
				{
					enabled = false;
					try { enabled = File.Exists(FilePath) && File.ReadAllText(FilePath).Trim().Equals("on", StringComparison.OrdinalIgnoreCase); } catch { }
				}
				return enabled.Value;
			}
		}

		// Kept from the main menu while the editor scene loads (not yet in it)
		static readonly List<GameObject> held = new List<GameObject>();
		// In the editor scene
		static GameObject skyRoot, seaRoot, cameraRoot;
		static AzureSkyController sky;
		static UltimateWater.Water sea;
		static Camera camera;
		static readonly List<Behaviour> cameraEffects = new List<Behaviour>();
		static readonly List<Light> editorSuns = new List<Light>();
		static Material editorSkybox;
		static bool editorFog;
		static float hour = 12.5f;

		/// <summary>Raft's sky and sea are in the editor (it was opened with them on), shown or hidden.</summary>
		public static bool Carried { get { return skyRoot != null && seaRoot != null && cameraRoot != null; } }
		/// <summary>Raft's sky and sea are shown now (the editor's sun, haze and blue plane are not).</summary>
		public static bool Showing { get { return Carried && skyRoot.activeSelf && seaRoot.activeSelf; } }
		/// <summary>Tests: the pieces taken from the main menu.</summary>
		internal static GameObject SkyObject { get { return skyRoot; } }
		internal static GameObject SeaObject { get { return seaRoot; } }
		internal static GameObject CameraObject { get { return cameraRoot; } }
		internal static AzureSkyController Sky { get { return sky; } }
		internal static UltimateWater.Water Sea { get { return sea; } }

		static T FindInScene<T>(Scene scene) where T : Component
		{
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				T found = root.GetComponentInChildren<T>(true);
				if (found != null) return found;
			}
			return null;
		}

		/// <summary>
		/// The editor is about to replace the main menu (LoadEditor): with the option on, Raft's sky, sea and the camera that
		/// draws them are kept through the scene change. Nothing happens when it is off or the menu isn't the scene shown.
		/// </summary>
		public static void HoldFromMenu()
		{
			ReleaseHeld();
			if (!Enabled) return;
			Scene menu = SceneManager.GetActiveScene();
			if (menu.name != MenuScene) { Debug.Log("[CUSTOM ISLANDS] Raft's sky and sea: the editor is not opened from the main menu (" + menu.name + "); the editor's own sky and sea are shown"); return; }
			AzureSkyController s = FindInScene<AzureSkyController>(menu);
			UltimateWater.Water w = FindInScene<UltimateWater.Water>(menu);
			UltimateWater.WaterCamera c = FindInScene<UltimateWater.WaterCamera>(menu);
			if (s == null || w == null || c == null || c.GetComponent<Camera>() == null)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Raft's sky and sea: not found in the main menu (sky " + (s != null) + ", sea " + (w != null) + ", water camera " + (c != null) + "); the editor's own sky and sea are shown");
				return;
			}
			// (each is a root object of its own in the menu: the sky with its sun, moon and light; the sea; the camera)
			foreach (GameObject root in new[] { s.transform.root.gameObject, w.transform.root.gameObject, c.transform.root.gameObject }.Distinct())
			{
				UnityEngine.Object.DontDestroyOnLoad(root);
				held.Add(root);
			}
			Debug.Log("[CUSTOM ISLANDS] Raft's sky and sea: kept from the main menu for the editor (" + string.Join(", ", held.Select(g => g.name).ToArray()) + ")");
		}

		/// <summary>What HoldFromMenu kept but the editor didn't take (the editor scene didn't open): removed, so nothing of
		/// the menu stays on in Raft's scenes.</summary>
		public static void ReleaseHeld()
		{
			foreach (GameObject g in held)
				if (g != null)
				{
					Debug.Log("[CUSTOM ISLANDS] Raft's sky and sea: " + g.name + " was not taken into the editor - removed");
					UnityEngine.Object.Destroy(g);
				}
			held.Clear();
		}

		/// <summary>
		/// The editor scene has loaded (before the editor's camera tools are added): what HoldFromMenu kept goes into it, the
		/// menu camera takes the place of the editor scene's camera (its position, view, clip planes and layers), and the
		/// sky is set up for the editor: its clock stopped (the time is EditorLighting's), no rain or snow, and Raft's
		/// SkyManager taken off (it reads Raft's world settings and regions; the editor's light is set in Tick instead).
		/// </summary>
		public static void Adopt(Scene editor)
		{
			skyRoot = seaRoot = cameraRoot = null; sky = null; sea = null; camera = null;
			cameraEffects.Clear(); editorSuns.Clear();
			if (held.Count == 0) return;
			try
			{
				foreach (GameObject g in held) if (g != null) SceneManager.MoveGameObjectToScene(g, editor);
				sky = held.Where(g => g != null).Select(g => g.GetComponentInChildren<AzureSkyController>(true)).FirstOrDefault(x => x != null);
				sea = held.Where(g => g != null).Select(g => g.GetComponentInChildren<UltimateWater.Water>(true)).FirstOrDefault(x => x != null);
				UltimateWater.WaterCamera wc = held.Where(g => g != null).Select(g => g.GetComponentInChildren<UltimateWater.WaterCamera>(true)).FirstOrDefault(x => x != null);
				held.Clear();
				if (sky == null || sea == null || wc == null) throw new InvalidOperationException("a piece went missing on the way");
				skyRoot = sky.transform.root.gameObject; seaRoot = sea.transform.root.gameObject; cameraRoot = wc.transform.root.gameObject;
				camera = wc.GetComponent<Camera>();

				// The editor scene's own camera hands over to the menu's (the editor's tools go on Camera.main next)
				Camera own = editor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(x => x.CompareTag("MainCamera"));
				if (own != null)
				{
					camera.transform.SetPositionAndRotation(own.transform.position, own.transform.rotation);
					camera.fieldOfView = own.fieldOfView;
					camera.nearClipPlane = own.nearClipPlane;
					camera.farClipPlane = own.farClipPlane;
					camera.cullingMask = own.cullingMask;
					camera.depth = own.depth;
					if (own.GetComponent<AudioListener>() != null && camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
					own.gameObject.tag = "Untagged";
					own.gameObject.SetActive(false);
				}
				camera.gameObject.tag = "MainCamera";
				// (what draws Raft's look: switched off with the option - not FMOD's listener)
				foreach (Behaviour b in camera.GetComponents<Behaviour>())
					if (b != null && !(b is Camera) && !(b is AudioListener) && b.GetType().Name != "StudioListener") cameraEffects.Add(b);

				// The sky: Raft's clock stopped, no weather particles (the menu's rain and snow may not have come along)
				sky.options.particlesMode = 0;
				sky.SetParticlesActive(0);
				FieldInfo progression = typeof(AzureSkyController).GetField("m_timeProgression", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (progression != null) progression.SetValue(sky, 0f);
				foreach (SkyManager m in skyRoot.GetComponentsInChildren<SkyManager>(true)) UnityEngine.Object.Destroy(m);

				// The editor's own sun(s) and sky, for when the option is switched off again
				foreach (Light l in editor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)))
					if (l.type == LightType.Directional && l.GetComponentInParent<AzureSkyController>() == null) editorSuns.Add(l);
				editorSkybox = RenderSettings.skybox;
				editorFog = RenderSettings.fog;
				Debug.Log("[CUSTOM ISLANDS] Raft's sky and sea: in the editor (sky " + skyRoot.name + ", sea " + seaRoot.name + ", camera " + cameraRoot.name + ", " + cameraEffects.Count + " camera effects)");
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Raft's sky and sea: could not bring them into the editor (" + e.Message + "); the editor's own sky and sea are shown");
				foreach (GameObject g in held) if (g != null) UnityEngine.Object.Destroy(g);
				held.Clear();
				// (the camera stays if it took over already - the editor needs one; only its effects go off)
				if (skyRoot != null) UnityEngine.Object.Destroy(skyRoot);
				if (seaRoot != null) UnityEngine.Object.Destroy(seaRoot);
				foreach (Behaviour b in cameraEffects) if (b != null) b.enabled = false;
				skyRoot = seaRoot = null; sky = null; sea = null;
			}
		}

		/// <summary>The editor is set up (its sea plane exists): Raft's sky and sea shown as the option says.</summary>
		public static void OnEditorReady()
		{
			if (Carried) Show(Enabled);
		}

		/// <summary>The option's button: on or off, kept for the next time. In an editor opened without them, on only shows
		/// from the next opening.</summary>
		public static void Toggle()
		{
			bool on = !Enabled;
			enabled = on;
			try { SafeFile.WriteAllText(FilePath, on ? "on" : "off"); } catch { }
			if (Carried)
			{
				Show(on);
				DynamicIslands.Notify(on ? "Raft's sky and sea: on (Raft's own sky, sun and ocean; the Light list sets the time of day)" : "Raft's sky and sea: off (the editor's own sky and the blue sea plane)");
			}
			else
				DynamicIslands.Notify(on ? "Raft's sky and sea: on - shown the next time the editor opens from the main menu (they are taken from it)" : "Raft's sky and sea: off");
		}

		/// <summary>Shows Raft's sky and sea (on) or the editor's own sun, sky, haze and blue plane (off).</summary>
		static void Show(bool on)
		{
			if (!Carried) return;
			skyRoot.SetActive(on);
			seaRoot.SetActive(on);
			// (Raft's fog - the camera's AzureSkyFogScattering - is set for playing at sea: from the editor's camera, a few
			// hundred metres out, it hid the island in blue haze; the sky, sun and ocean stay - KeepFogOff)
			foreach (Behaviour b in cameraEffects) if (b != null) b.enabled = on && b.GetType().Name != "AzureSkyFogScattering";
			if (on) KeepFogOff();
			foreach (Light l in editorSuns) if (l != null) l.enabled = !on;
			GameObject plane = DynamicIslands.WaterPlane;
			if (plane != null) foreach (Renderer r in plane.GetComponentsInChildren<Renderer>(true)) r.enabled = !on;
			if (on)
			{
				// (Raft's sky material as the skybox again - the editor scene brought its own - and Raft's fog is the camera's
				// AzureSkyFogScattering, not Unity's)
				sky.ConfigureShaders();
				RenderSettings.fog = false;
				if (sky.m_lightComponent != null) RenderSettings.sun = sky.m_lightComponent;
				Tick();
			}
			else
			{
				RenderSettings.skybox = editorSkybox;
				RenderSettings.fog = editorFog;
			}
			// (the time of day again, in the sky now shown)
			EditorLighting.Apply(EditorLighting.Current, false);
		}

		static readonly FieldInfo waterCameraImageEffects = typeof(UltimateWater.WaterCamera).GetField("_ImageEffects", BindingFlags.Instance | BindingFlags.NonPublic);
		static bool fogWarned;

		/// <summary>
		/// Keeps Raft's fog (the camera's AzureSkyFogScattering) off. Switching it off is not enough: Raft's WaterCamera turns
		/// it on again before every frame whenever the camera is above the water (OnPreCull: fog on unless fully under water)
		/// - and the main menu's fog is a thick sea fog (its sky profile's fog distance is ~220 m), which hid the editor's
		/// island in blue-white haze from a few hundred metres. The WaterCamera does that only while it has a list of image
		/// effects; the menu camera has none (an empty list), so the list is taken away (null) - nothing else uses it.
		/// (WaterCamera.OnEnable makes the list again: so here, every frame.)
		/// </summary>
		internal static void KeepFogOff()
		{
			if (camera == null) return;
			UltimateWater.WaterCamera wc = camera.GetComponent<UltimateWater.WaterCamera>();
			if (wc != null && waterCameraImageEffects != null)
			{
				Array list = waterCameraImageEffects.GetValue(wc) as Array;
				if (list != null && list.Length == 0) waterCameraImageEffects.SetValue(wc, null);
				else if (list != null && !fogWarned) { fogWarned = true; Debug.LogWarning("[CUSTOM ISLANDS] Raft's sky and sea: the water camera has image effects - Raft's fog may show in the editor"); }
			}
			foreach (Behaviour b in cameraEffects) if (b != null && b.enabled && b.GetType().Name == "AzureSkyFogScattering") b.enabled = false;
		}

		/// <summary>Tests: Raft's fog is off on the editor's camera (and the water camera won't turn it on again).</summary>
		internal static bool FogOff
		{
			get
			{
				if (camera == null) return false;
				Behaviour fog = cameraEffects.FirstOrDefault(b => b != null && b.GetType().Name == "AzureSkyFogScattering");
				UltimateWater.WaterCamera wc = camera.GetComponent<UltimateWater.WaterCamera>();
				return (fog == null || !fog.enabled) && (wc == null || waterCameraImageEffects == null || waterCameraImageEffects.GetValue(wc) == null);
			}
		}

		/// <summary>EditorLighting's time of day in Raft's sky; false when Raft's sky isn't shown (EditorLighting lights
		/// the editor itself then).</summary>
		public static bool SetTimeOfDay(int index)
		{
			if (!Showing) return false;
			hour = hours[Mathf.Clamp(index, 0, hours.Length - 1)];
			sky.timeOfDay.hour = hour;
			RenderSettings.fog = false;
			return true;
		}

		/// <summary>Every frame (DynamicIslands.Update): the sea at the editor's sea level (as the blue plane would be), the
		/// sky's clock held at the chosen hour, and the sky's light on everything (what Raft's SkyManager does in a world).</summary>
		public static void Tick()
		{
			if (!Showing) return;
			Vector3 p = seaRoot.transform.position;
			float y = DynamicIslands.EditorSeaInWorld;
			if (!Mathf.Approximately(p.y, y)) seaRoot.transform.position = new Vector3(p.x, y, p.z);
			sky.timeOfDay.hour = hour;
			KeepFogOff();
			RenderSettings.ambientMode = AmbientMode.Trilight;
			RenderSettings.ambientSkyColor = sky.ambientSkyColor;
			RenderSettings.ambientEquatorColor = sky.ambientEquatorColor;
			RenderSettings.ambientGroundColor = sky.ambientGroundColor;
		}
	}
}

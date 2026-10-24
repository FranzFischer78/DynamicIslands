using System.Collections;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AzureSky;
using UnityEngine.SceneManagement;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIEditorSkySea", docs: "Dev, main menu or editor: the editor option \"Raft's sky and sea\" (ROADMAP E1b) - opens the editor with it on (from the main menu), checks Raft's sky, ocean and their camera are in the editor (the camera is the editor's, the blue plane hidden, the sea at the editor's sea level, the Light list sets the sky's hour, off/on in the editor), leaves for the main menu and checks they went with the editor, then opens the editor with it off (the editor's own sky and plane). Puts the option back as it was; shot_skysea_<time>.png")]
		public static void EditorSkySeaCheck()
		{
			StartTest(EditorSkySeaRoutine());
		}

		/// <summary>To Raft's main menu (as the editor's Main menu button goes) and a moment for it to settle.</summary>
		static IEnumerator ToMainMenu()
		{
			if (SceneManager.GetActiveScene().name == "MainMenuScene" && !DynamicIslands.InEditor()) yield break;
			EditorAutosave.WriteNow();
			SceneManager.LoadScene("MainMenuScene", LoadSceneMode.Single);
			for (float until = Time.realtimeSinceStartup + 60f; Time.realtimeSinceStartup < until; )
			{
				yield return new WaitForSecondsRealtime(0.5f);
				Scene s = SceneManager.GetActiveScene();
				if (s.name == "MainMenuScene" && s.isLoaded) break;
			}
			yield return new WaitForSecondsRealtime(2f);
		}

		static IEnumerator EditorSkySeaRoutine()
		{
			if (LoadSceneManager.IsGameSceneLoaded) { Fail("CIEditorSkySea: run at the main menu or in the editor"); yield break; }
			bool ok = true;
			bool wasOn = RaftSkySea.Enabled;
			int exceptions = 0; string firstException = null;
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception) { exceptions++; if (firstException == null) firstException = msg + " " + (trace ?? "").Split('\n').FirstOrDefault(); } };
			Application.logMessageReceived += counter;
			try
			{
				// 1. From the main menu with the option on
				yield return ToMainMenu();
				Check(ref ok, SceneManager.GetActiveScene().name == "MainMenuScene", "at the main menu (" + SceneManager.GetActiveScene().name + ")");
				int menuSkies = Object.FindObjectsOfType<AzureSkyController>().Length, menuSeas = Object.FindObjectsOfType<UltimateWater.Water>().Length;
				Check(ref ok, menuSkies == 1 && menuSeas == 1, "the main menu has Raft's sky and sea (" + menuSkies + " sky, " + menuSeas + " sea)");
				if (!RaftSkySea.Enabled) RaftSkySea.Toggle();
				Check(ref ok, RaftSkySea.Enabled, "the option is on");
				yield return WaitForEditor(false);
				if (!DynamicIslands.InEditor()) { Fail("CIEditorSkySea: the editor did not open"); yield break; }
				yield return new WaitForSecondsRealtime(1f);

				// 2. In the editor
				Scene editor = SceneManager.GetActiveScene();
				GameObject sky = RaftSkySea.SkyObject, sea = RaftSkySea.SeaObject, cam = RaftSkySea.CameraObject;
				Check(ref ok, RaftSkySea.Carried && RaftSkySea.Showing, "Raft's sky and sea are in the editor and shown");
				Check(ref ok, sky != null && sky.activeInHierarchy && sky.scene == editor && RaftSkySea.Sky != null && RaftSkySea.Sky.isActiveAndEnabled, "Raft's sky (" + (sky != null ? sky.name : "none") + ") is in the editor's scene and running");
				Check(ref ok, sea != null && sea.activeInHierarchy && sea.scene == editor && RaftSkySea.Sea != null && RaftSkySea.Sea.isActiveAndEnabled, "Raft's sea (" + (sea != null ? sea.name : "none") + ") is in the editor's scene and running");
				Check(ref ok, sea != null && Mathf.Abs(sea.transform.position.y - DynamicIslands.EditorSeaInWorld) < 0.01f, "the sea is at the editor's sea level (" + (sea != null ? sea.transform.position.y.ToString("F2") : "-") + " / " + DynamicIslands.EditorSeaInWorld.ToString("F2") + ")");
				Camera main = Camera.main;
				UltimateWater.WaterCamera wc = main != null ? main.GetComponent<UltimateWater.WaterCamera>() : null;
				Check(ref ok, main != null && cam != null && main.gameObject == cam && main.GetComponent<terraineditor>() != null && main.GetComponent<EditorCamera>() != null, "the menu's camera is the editor's camera (with the editor's tools on it)");
				Check(ref ok, wc != null && wc.enabled, "the camera draws Raft's water (WaterCamera on)");
				int cameras = Object.FindObjectsOfType<Camera>().Count(c => c.isActiveAndEnabled && c.CompareTag("MainCamera"));
				Check(ref ok, cameras == 1, "one main camera draws the editor (" + cameras + ")");
				GameObject plane = DynamicIslands.WaterPlane;
				Check(ref ok, plane != null && plane.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "the blue sea plane is hidden");
				Check(ref ok, RaftSkySea.Sky != null && (RenderSettings.skybox == RaftSkySea.Sky.skyMaterial || RaftSkySea.Sky.options.shaderMode == 1), "Raft's sky material is the sky");
				Check(ref ok, !RenderSettings.fog, "no Unity haze over Raft's own fog");
				Check(ref ok, RaftSkySea.FogOff, "Raft's sea fog stays off (the water camera doesn't turn it on again: the island isn't hidden in haze)");

				// The Light list: the sky's hour, held (Raft's clock stopped)
				int before = EditorLighting.Current;
				EditorLighting.Apply(0, false);
				yield return new WaitForSecondsRealtime(1f);
				float morning = RaftSkySea.Sky.timeOfDay.hour;
				Color morningLight = RaftSkySea.Sky.lightColor;
				yield return new WaitForSecondsRealtime(1f);
				Check(ref ok, Mathf.Abs(RaftSkySea.Sky.timeOfDay.hour - morning) < 0.001f && morning > 5f && morning < 10f, "Morning: the sky's clock at " + morning.ToString("F2") + " h and held there (" + RaftSkySea.Sky.timeOfDay.hour.ToString("F2") + " a second later)");
				EditorLighting.Apply(3, false);
				yield return new WaitForSecondsRealtime(1f);
				float night = RaftSkySea.Sky.timeOfDay.hour;
				Check(ref ok, night > 20f && RaftSkySea.Sky.lightColor != morningLight, "Night: the sky's clock at " + night.ToString("F2") + " h, its light changed (" + morningLight + " -> " + RaftSkySea.Sky.lightColor + ")");
				EditorLighting.Apply(1, false);
				yield return new WaitForSecondsRealtime(1f);
				Screenshot(new[] { "skysea_noon" });
				yield return new WaitForSecondsRealtime(0.5f);

				// Off and on again in the editor
				RaftSkySea.Toggle();
				yield return null;
				Check(ref ok, !RaftSkySea.Enabled && RaftSkySea.Carried && !RaftSkySea.Showing && !sky.activeSelf && !sea.activeSelf, "off in the editor: Raft's sky and sea hidden");
				Check(ref ok, plane != null && plane.GetComponentsInChildren<Renderer>(true).All(r => r.enabled) && wc != null && !wc.enabled, "off: the blue plane back, the water camera off");
				Check(ref ok, RenderSettings.sun != null && RenderSettings.sun.isActiveAndEnabled && RenderSettings.sun.GetComponentInParent<AzureSkyController>() == null && RenderSettings.fog, "off: the editor's own sun (" + (RenderSettings.sun != null ? RenderSettings.sun.name : "none") + ") and haze");
				yield return new WaitForSecondsRealtime(0.5f);
				Screenshot(new[] { "skysea_off" });
				yield return new WaitForSecondsRealtime(0.5f);
				RaftSkySea.Toggle();
				yield return null;
				Check(ref ok, RaftSkySea.Enabled && RaftSkySea.Showing && wc != null && wc.enabled && plane.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "on again in the editor: Raft's sky and sea shown");
				yield return new WaitForSecondsRealtime(0.5f);
				Check(ref ok, RaftSkySea.FogOff, "on again: Raft's sea fog still off");
				EditorLighting.Apply(before, false);
				Check(ref ok, exceptions == 0, "no errors with Raft's sky and sea in the editor (" + exceptions + (firstException != null ? ", first: " + firstException : "") + ")");

				// 3. Leaving the editor takes them away
				yield return ToMainMenu();
				Check(ref ok, sky == null && sea == null && cam == null, "leaving the editor removed Raft's sky, sea and camera it had (" + (sky == null) + ", " + (sea == null) + ", " + (cam == null) + ")");
				Check(ref ok, !RaftSkySea.Carried, "nothing of them is left");
				int skies = Object.FindObjectsOfType<AzureSkyController>().Length, seas = Object.FindObjectsOfType<UltimateWater.Water>().Length;
				int kept = Object.FindObjectsOfType<GameObject>().Count(g => g.scene.name == "DontDestroyOnLoad" && (g.GetComponentInChildren<AzureSkyController>(true) != null || g.GetComponentInChildren<UltimateWater.Water>(true) != null || g.GetComponentInChildren<UltimateWater.WaterCamera>(true) != null));
				Check(ref ok, skies == menuSkies && seas == menuSeas && kept == 0, "the main menu has only its own sky and sea again (" + skies + " sky, " + seas + " sea, " + kept + " kept over scenes)");

				// 4. Off: the editor as it was
				if (RaftSkySea.Enabled) RaftSkySea.Toggle();
				yield return WaitForEditor(false);
				if (!DynamicIslands.InEditor()) { Fail("CIEditorSkySea: the editor did not open again"); yield break; }
				yield return new WaitForSecondsRealtime(1f);
				Scene again = SceneManager.GetActiveScene();
				bool none = !RaftSkySea.Carried && Object.FindObjectsOfType<AzureSkyController>().Length == 0 && Object.FindObjectsOfType<UltimateWater.Water>().Length == 0;
				Check(ref ok, none, "off: no Raft sky or sea in the editor");
				plane = DynamicIslands.WaterPlane;
				Check(ref ok, plane != null && plane.GetComponentsInChildren<Renderer>(true).All(r => r.enabled) && Camera.main != null && Camera.main.gameObject.scene == again && Camera.main.GetComponent<UltimateWater.WaterCamera>() == null, "off: the editor's own camera and blue sea plane");
			}
			finally
			{
				Application.logMessageReceived -= counter;
				// (the option as it was before the test)
				if (RaftSkySea.Enabled != wasOn) RaftSkySea.Toggle();
			}
			if (ok) Log("PASS: editor sky and sea check"); else Fail("editor sky and sea check");
		}
	
		[ConsoleCommand(name: "CICameraEffects", docs: "Dev, editor: lists the main camera's effects (behaviours) and their state; CICameraEffects <type name> on|off switches one (to find which one does what)")]
		public static void CameraEffectsCommand(string[] args)
		{
			Camera cam = Camera.main;
			if (cam == null) { Fail("camera effects: no main camera"); return; }
			if (args != null && args.Length == 2)
				foreach (Behaviour b in cam.GetComponents<Behaviour>())
					if (b.GetType().Name == args[0]) b.enabled = args[1] == "on";
			Log("EFFECTS " + string.Join(", ", cam.GetComponents<Behaviour>().Select(b => b.GetType().Name + (b.enabled ? " on" : " off")).ToArray()) + "; fog " + RenderSettings.fog + " " + RenderSettings.fogMode + " " + RenderSettings.fogDensity + " " + RenderSettings.fogStartDistance + "-" + RenderSettings.fogEndDistance);
		}
	}
}

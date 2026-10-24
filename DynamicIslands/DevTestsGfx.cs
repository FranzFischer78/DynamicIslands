using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		#region Low graphics for test runs (CIGfx)

		// The user, 2026-10-20: test Rafts at low graphics for shorter tests and less lag (two Rafts share one GPU).
		// Low lives only in this Raft's session: Raft's Settings.rgd keeps the user's values (GfxSavePostfix), "high" and
		// quitting put the user's values back, and tools\gfx.ps1 restores Unity's registry values once the Raft has closed.
		// Pictures (RenderCam, PlayPicture) switch to the user's quality just for the shot.

		class GfxState
		{
			public int quality, textureLimit, shadowCascades, vsync, fps, water = -1;
			public ShadowQuality shadows;
			public ShadowResolution shadowResolution;
			public float shadowDistance, lodBias;
			public bool ao, aa, aoMenu, aaMenu;
			public int width, height, refresh;
			public FullScreenMode mode;
			public Resolution current;
		}

		static bool gfxLow;
		static int gfxFps = 30, gfxShotDepth;
		static GfxState gfxHigh;
		static bool gfxHooked;
		const int GfxWidth = 1280, GfxHeight = 720;

		[ConsoleCommand(name: "CIGfx", docs: "Dev, anywhere: graphics for test runs. CIGfx low [fps]: windowed 1280x720, lowest quality, no shadows, lowest water, no AO/AA/volumetric light, frame cap (default 30, 0 = none) - this session only, Settings.rgd keeps the user's values; CIGfx high: the user's values back; CIGfx status. Pictures (play_*.jpg, recipe and library pictures) are taken at the user's quality anyway; CIShot captures the screen as it is")]
		public static void GfxCommand(string[] args)
		{
			string a = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "status";
			if (a == "low")
			{
				int fps = 30;
				if (args.Length > 1 && !int.TryParse(args[1], out fps)) fps = 30;
				StartTest(GfxLowRoutine(fps));
			}
			else if (a == "high")
			{
				if (gfxLow) GfxRestore(true);
				Log("GFX high: " + GfxStatus());
			}
			else Log("GFX " + (gfxLow ? "low" : "high") + ": " + GfxStatus());
		}

		static GraphicsSettingsBox GfxBox()
		{
			return Resources.FindObjectsOfTypeAll<GraphicsSettingsBox>().FirstOrDefault(b => b != null && b.gameObject.scene.IsValid());
		}

		static IEnumerator GfxLowRoutine(int fps)
		{
			// Raft applies its Settings.rgd (quality, water, resolution) at start: take the user's values only after that
			float end = Time.realtimeSinceStartup + 60f;
			GraphicsSettingsBox box = null;
			while (Time.realtimeSinceStartup < end)
			{
				box = GfxBox();
				if (box != null && Traverse.Create(box).Field("lastLoadedSetting").GetValue() != null) break;
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (box == null) { Fail("graphics low: Raft's graphics settings not found"); yield break; }
			if (!gfxLow) { yield return new WaitForSecondsRealtime(1f); gfxHigh = GfxTake(box); }
			gfxFps = fps;
			gfxLow = true;
			gfxShotDepth = 0;
			GfxHook();
			GfxApplyLow(true);
			yield return null; yield return null; // (Screen.SetResolution takes effect at the end of the frame)
			Log("GFX low: " + GfxStatus());
			Log("PASS: graphics low");
		}

		static GfxState GfxTake(GraphicsSettingsBox box)
		{
			var s = new GfxState
			{
				quality = QualitySettings.GetQualityLevel(), textureLimit = QualitySettings.masterTextureLimit, shadows = QualitySettings.shadows,
				shadowCascades = QualitySettings.shadowCascades, shadowDistance = QualitySettings.shadowDistance, shadowResolution = QualitySettings.shadowResolution,
				lodBias = QualitySettings.lodBias, vsync = QualitySettings.vSyncCount, fps = GameManager.targetFrameRate,
				width = Screen.width, height = Screen.height, mode = Screen.fullScreenMode, current = Screen.currentResolution, refresh = Screen.currentResolution.refreshRate,
			};
			try { s.water = UltimateWater.WaterQualitySettings.Instance.GetQualityLevel(); } catch (Exception e) { Log("(water quality not read: " + e.Message + ")"); }
			try
			{
				s.ao = box.postEffects.ambientOcclusion.enabled; s.aa = box.postEffects.antialiasing.enabled;
				s.aoMenu = box.postEffectsMenu.ambientOcclusion.enabled; s.aaMenu = box.postEffectsMenu.antialiasing.enabled;
			}
			catch (Exception e) { Log("(post effects not read: " + e.Message + ")"); }
			return s;
		}

		/// <summary>Lowest quality; with <paramref name="screen"/> also the small window and the frame cap.</summary>
		static void GfxApplyLow(bool screen)
		{
			QualitySettings.SetQualityLevel(0, screen);
			QualitySettings.shadows = ShadowQuality.Disable;
			QualitySettings.shadowDistance = 0f;
			GfxWater(true, -1);
			GfxPost(false, false, false, false);
			GfxVolumetric(0);
			if (!screen) return;
			QualitySettings.vSyncCount = 0;
			GfxCap();
			Screen.SetResolution(GfxWidth, GfxHeight, FullScreenMode.Windowed);
		}

		/// <summary>The low frame cap. Raft's GameManager.Update puts its own static targetFrameRate (the settings' FPS cap)
		/// into Application.targetFrameRate every frame: that static is set too (10-20: the cap had stayed at 60).</summary>
		static void GfxCap()
		{
			int cap = gfxFps > 0 ? gfxFps : -1;
			GameManager.targetFrameRate = cap;
			Application.targetFrameRate = cap;
		}

		/// <summary>The user's values back; with <paramref name="screen"/> also their window, vsync and frame cap (and low ends).</summary>
		static void GfxRestore(bool screen)
		{
			GfxState s = gfxHigh;
			if (s == null) return;
			QualitySettings.SetQualityLevel(s.quality, screen);
			QualitySettings.masterTextureLimit = s.textureLimit;
			QualitySettings.shadows = s.shadows;
			QualitySettings.shadowCascades = s.shadowCascades;
			QualitySettings.shadowDistance = s.shadowDistance;
			QualitySettings.shadowResolution = s.shadowResolution;
			QualitySettings.lodBias = s.lodBias;
			GfxWater(false, s.water);
			GfxPost(s.ao, s.aa, s.aoMenu, s.aaMenu);
			GfxVolumetric(s.quality);
			if (!screen) return;
			gfxLow = false;
			gfxShotDepth = 0;
			QualitySettings.vSyncCount = s.vsync;
			GameManager.targetFrameRate = s.fps; Application.targetFrameRate = s.fps;
			Screen.SetResolution(s.width, s.height, s.mode, s.refresh);
		}

		static void GfxWater(bool lowest, int level)
		{
			try
			{
				var w = UltimateWater.WaterQualitySettings.Instance;
				if (w == null) return;
				if (lowest)
				{
					// (the lowest by name: index 0 unless the list runs from the best down)
					string[] names = w.Names;
					level = 0;
					if (names != null && names.Length > 1 && System.Text.RegularExpressions.Regex.IsMatch(names[0], "ultra|fantastic|high|beautiful", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) level = names.Length - 1;
				}
				if (level >= 0 && w.GetQualityLevel() != level) w.SetQualityLevel(level);
			}
			catch (Exception e) { Log("(water quality not set: " + e.Message + ")"); }
		}

		static void GfxPost(bool ao, bool aa, bool aoMenu, bool aaMenu)
		{
			try
			{
				GraphicsSettingsBox box = GfxBox();
				if (box == null) return;
				box.postEffects.ambientOcclusion.enabled = ao; box.postEffects.antialiasing.enabled = aa;
				box.postEffectsMenu.ambientOcclusion.enabled = aoMenu; box.postEffectsMenu.antialiasing.enabled = aaMenu;
			}
			catch (Exception e) { Log("(post effects not set: " + e.Message + ")"); }
		}

		static void GfxVolumetric(int quality)
		{
			try
			{
				GraphicsSettingsBox box = GfxBox();
				MethodInfo m = AccessTools.Method(typeof(GraphicsSettingsBox), "ApplyVolumetricLightingProperties");
				if (box != null && m != null) m.Invoke(box, new object[] { quality });
			}
			catch (Exception e) { Log("(volumetric light not set: " + (e.InnerException ?? e).Message + ")"); }
		}

		static string GfxStatus()
		{
			string water = "?";
			try { var w = UltimateWater.WaterQualitySettings.Instance; int l = w.GetQualityLevel(); water = l + (w.Names != null && l >= 0 && l < w.Names.Length ? " " + w.Names[l] : ""); } catch { }
			string[] names = QualitySettings.names;
			int q = QualitySettings.GetQualityLevel();
			return "window " + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode + ", quality " + q + (q < names.Length ? " " + names[q] : "") +
				", shadows " + QualitySettings.shadows + ", water " + water + ", vsync " + QualitySettings.vSyncCount + ", fps cap " + Application.targetFrameRate +
				(gfxHigh != null ? " (user: " + gfxHigh.width + "x" + gfxHigh.height + " " + gfxHigh.mode + ", quality " + gfxHigh.quality + ", water " + gfxHigh.water + ")" : "");
		}

		static void GfxHook()
		{
			if (gfxHooked) return;
			gfxHooked = true;
			// A new scene brings its own camera (volumetric light) and water: low again
			SceneManager.sceneLoaded += (scene, mode) => { if (gfxLow) DynamicIslands.instance.StartCoroutine(GfxAgain()); };
			// Quitting: the user's quality back, so Unity saves theirs (tools\gfx.ps1 restores the registry anyway)
			Application.quitting += () => { if (gfxLow) { try { GfxRestore(true); } catch { } } };
			// Raft saves Settings.rgd from the live values when its settings menu closes: save the user's instead
			try
			{
				new Harmony("ci.gfx").Patch(AccessTools.Method(typeof(GraphicsSettingsBox), "Save", new[] { typeof(RGD_Settings) }),
					postfix: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "GfxSavePostfix")));
			}
			catch (Exception e) { Fail("graphics low: Settings.rgd guard not patched: " + (e.InnerException ?? e).Message); }
		}

		static IEnumerator GfxAgain()
		{
			yield return null; yield return null;
			if (gfxLow && gfxShotDepth == 0) { GfxApplyLow(false); QualitySettings.vSyncCount = 0; GfxCap(); }
		}

		static void GfxSavePostfix(RGD_Settings settings)
		{
			GfxState s = gfxHigh;
			if (!gfxLow || s == null || settings == null || settings.graphics == null) return;
			RGD_Settings_Graphics g = settings.graphics;
			g.overallQuality = s.quality;
			if (s.water >= 0) g.waterQuality = s.water;
			g.resolutionWidth = s.current.width; g.resolutionHeight = s.current.height; g.resolutionRefreshRate = s.current.refreshRate;
			g.vsync = s.vsync;
			g.ambientOcclusion = s.ao; g.antialiasing = s.aa;
			g.textureQuality = s.textureLimit;
			g.shadowType = s.shadows; g.shadowCascades = s.shadowCascades; g.shadowDistance = s.shadowDistance; g.shadowResolution = s.shadowResolution;
		}

		/// <summary>Before a picture: the user's quality (when low is on). Returns whether it switched; pass that to GfxShotEnd.</summary>
		static bool GfxShotBegin()
		{
			if (!gfxLow || gfxHigh == null) return false;
			if (gfxShotDepth++ == 0) GfxRestore(false);
			return true;
		}

		static void GfxShotEnd(bool began)
		{
			if (!began) return;
			if (gfxShotDepth > 0 && --gfxShotDepth == 0 && gfxLow) GfxApplyLow(false);
		}

		#endregion
	}
}

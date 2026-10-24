using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>TODO §5 "Editor camera: modern controls and settings": the camera's settings and Alt + right-drag zoom.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CICameraSettings", docs: "Dev, editor: the editor camera's settings - look sensitivity, drag up looks up or down, move, zoom and pan speed, the wheel's direction - change the camera's moves as set; Alt + right-drag zooms in and out; right mouse + wheel speed is kept; saved in world_rules.txt and read back; the status bar's Camera... button opens the Camera window (every mouse button and key of the editor listed, each on one line, the window fitting 1280 x 720; sliders, switches, Mod's own); Defaults has the same settings; world_rules.txt is put back as it was")]
		public static void CameraSettingsCommand() { StartTest(CameraSettingsRoutine()); }

		static IEnumerator CameraSettingsRoutine()
		{
			yield return WaitForEditor(false);
			EditorCamera c = EditorCamera.Instance;
			if (!DynamicIslands.InEditor() || c == null) { Fail("camera settings: open the editor first"); yield break; }
			bool ok = true;
			string rules = Path.Combine(DynamicIslands.assetpath, WorldRules.DefaultFileName);
			string rulesBefore = File.Exists(rules) ? File.ReadAllText(rules) : null;
			Camera cam = c.GetComponent<Camera>();
			try
			{
				CameraSettings.Reset();
				Terrain terrain = terraineditor.terrain;
				float sea = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
				Vector3 mid = terrain.transform.position + new Vector3(500f, 0f, 500f);

				// 1. Look sensitivity and which way dragging up looks
				Func<Vector2, Vector2> turn = px =>
				{
					cam.transform.SetPositionAndRotation(new Vector3(mid.x, sea + 60f, mid.z - 150f), Quaternion.Euler(20f, 10f, 0f));
					c.Sync();
					c.Look(px);
					return new Vector2(c.transform.eulerAngles.y, c.transform.eulerAngles.x);
				};
				bool ownUp = CameraSettings.DragUpLooksUp; // (the mod's own since 2026-10-21: up)
				CameraSettings.DragUpLooksUp = false;
				yield return null;
				Vector2 own = turn(new Vector2(100f, 50f)); // (Look sets yaw/pitch; Advance shows them next frame)
				yield return null;
				own = new Vector2(cam.transform.eulerAngles.y, cam.transform.eulerAngles.x);
				CameraSettings.Set(CameraSettings.LookNumber, 2f);
				CameraSettings.DragUpLooksUp = true;
				turn(new Vector2(100f, 50f));
				yield return null;
				Vector2 set = new Vector2(cam.transform.eulerAngles.y, cam.transform.eulerAngles.x);
				Check(ref ok, ownUp && Mathf.Abs(Mathf.DeltaAngle(own.x, 28f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(own.y, 29f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(set.x, 46f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(set.y, 2f)) < 0.5f,
					"look: 100 px right, 50 px up turn to " + own.x.ToString("F0") + "° / " + own.y.ToString("F0") + "° with Drag up: look down (28 / 29), the mod's own being look up: " + ownUp + "; at x2 with Drag up: look up to " + set.x.ToString("F0") + "° / " + set.y.ToString("F0") + "° (46 / 2)");
				CameraSettings.Reset();

				// 2. Move speed (high above the island, where the speed is at its top: twice the setting, twice as far)
				Func<float> fly = () =>
				{
					cam.transform.SetPositionAndRotation(new Vector3(mid.x, sea + 400f, mid.z), Quaternion.Euler(30f, 0f, 0f));
					c.Sync();
					Vector3 start = c.Destination;
					for (int i = 0; i < 20; i++) c.Move(new Vector3(1f, 0f, 0f), false, false, 0.02f);
					return Vector3.Distance(c.Destination, start);
				};
				float m1 = fly();
				CameraSettings.Set(CameraSettings.SpeedNumber, 2f);
				float m2 = fly();
				CameraSettings.Reset();
				yield return null;
				Check(ref ok, m1 > 0.5f && Mathf.Abs(m2 / m1 - 2f) < 0.1f, "move speed x2: " + m1.ToString("F1") + " m -> " + m2.ToString("F1") + " m in the same time");

				// 3. Zoom speed (the wheel's step) and Alt + right-drag zoom
				Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
				Func<float> zoomStep = () =>
				{
					cam.transform.SetPositionAndRotation(new Vector3(mid.x, sea + 80f, mid.z - 120f), Quaternion.LookRotation(new Vector3(mid.x, sea + 5f, mid.z) - new Vector3(mid.x, sea + 80f, mid.z - 120f)));
					c.Sync();
					Vector3 start = c.Destination;
					c.ZoomAt(screen, 1f);
					float step = Vector3.Distance(c.Destination, start);
					c.Sync(); // (the zoom not made)
					return step;
				};
				float z1 = zoomStep();
				CameraSettings.Set(CameraSettings.ZoomNumber, 0.5f);
				float zHalf = zoomStep();
				CameraSettings.Reset();
				yield return null;
				cam.transform.SetPositionAndRotation(new Vector3(mid.x, sea + 80f, mid.z - 120f), Quaternion.LookRotation(new Vector3(mid.x, sea + 5f, mid.z) - new Vector3(mid.x, sea + 80f, mid.z - 120f)));
				c.Sync();
				Ray centre = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
				RaycastHit hit;
				bool hitSomething = Physics.Raycast(centre, out hit, 5000f, ~0, QueryTriggerInteraction.Ignore);
				Vector3 aim = hitSomething ? hit.point : centre.GetPoint(100f);
				float d0 = Vector3.Distance(cam.transform.position, aim);
				c.Dolly(new Vector2(60f, 40f));
				yield return null;
				float dIn = Vector3.Distance(cam.transform.position, aim);
				for (int i = 0; i < 50; i++) { c.Dolly(new Vector2(400f, 400f)); yield return null; }
				float dMin = Vector3.Distance(cam.transform.position, aim);
				c.Dolly(new Vector2(-100f, -100f));
				yield return null;
				float dOut = Vector3.Distance(cam.transform.position, aim);
				c.EndDolly();
				Check(ref ok, Mathf.Abs(zHalf / z1 - 0.5f) < 0.05f && dIn < d0 - 1f && (!hitSomething || dMin >= 1.2f) && dOut > dMin + 1f,
					"zoom speed x0.5: a wheel notch " + z1.ToString("F1") + " m -> " + zHalf.ToString("F1") + " m; Alt + right-drag zooms " + d0.ToString("F0") + " -> " + dIn.ToString("F0") + " m, never through it (" + dMin.ToString("F1") + " m), back out " + dOut.ToString("F1") + " m");

				// 4. Pan speed
				Func<float> pan = () =>
				{
					cam.transform.SetPositionAndRotation(new Vector3(mid.x, sea + 80f, mid.z - 120f), Quaternion.Euler(35f, 0f, 0f));
					c.Sync();
					Vector3 start = c.Destination;
					c.BeginPan(screen);
					c.Pan(new Vector2(100f, 0f));
					c.EndPan();
					return Vector3.Distance(c.Destination, start);
				};
				float p1 = pan();
				CameraSettings.Set(CameraSettings.PanNumber, 2f);
				float p2 = pan();
				CameraSettings.Reset();
				yield return null;
				Check(ref ok, p1 > 0.1f && Mathf.Abs(p2 / p1 - 2f) < 0.1f, "pan speed x2: 100 px slide " + p1.ToString("F1") + " m -> " + p2.ToString("F1") + " m");

				// 5. Right mouse + wheel: the flying speed, kept (saved a moment later)
				c.ChangeSpeed(1);
				bool waits = CameraSettings.Unsaved;
				float until = Time.unscaledTime + 3f;
				while (CameraSettings.Unsaved && Time.unscaledTime < until) yield return null;
				bool inFile = File.ReadAllLines(rules).Any(l => l.Trim() == "cameraspeed=1.25");
				Check(ref ok, Mathf.Approximately(EditorCamera.SpeedFactor, 1.25f) && waits && inFile, "right mouse + wheel: speed x" + EditorCamera.SpeedFactor.ToString("0.##") + ", written a moment later: " + inFile);

				// 6. Saved and read back
				CameraSettings.Set(CameraSettings.LookNumber, 1.5f);
				CameraSettings.Set(CameraSettings.ZoomNumber, 99f); // (kept within its range: 4)
				CameraSettings.DragUpLooksUp = false; // (not the mod's own: read back from the file, not the default)
				CameraSettings.WheelReversed = true;
				CameraSettings.Save();
				string[] lines = File.ReadAllLines(rules);
				CameraSettings.Reload();
				Check(ref ok, lines.Contains("cameralook=1.5") && lines.Contains("camerazoom=4") && lines.Contains("cameradragup=down") && lines.Contains("camerawheel=reversed") &&
					Mathf.Approximately(CameraSettings.Look, 1.5f) && Mathf.Approximately(CameraSettings.Zoom, 4f) && !CameraSettings.DragUpLooksUp && CameraSettings.WheelReversed && (rulesBefore == null || rulesBefore.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#") && !l.StartsWith("camera")).All(lines.Contains)),
					"world_rules.txt has cameralook=1.5, camerazoom=4 (kept within 4), cameradragup=down, camerawheel=reversed, its other lines as they were, and they are read back");

				// 7. The status bar's Camera... button: the Camera window
				Button open = EditorUI.Canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Button_CameraSettings");
				if (open != null) open.onClick.Invoke();
				yield return null;
				GameObject w = CameraSettingsWindow.Root;
				Slider look = w != null ? w.GetComponentsInChildren<Slider>(true).FirstOrDefault(s => s.name == "CameraSlider_cameralook") : null;
				Button up = w != null ? w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Camera_DragUp") : null;
				Button wheel = w != null ? w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Camera_Wheel") : null;
				Check(ref ok, open != null && CameraSettingsWindow.IsOpen && EditorInput.WindowOpen && look != null && up != null && wheel != null && w.GetComponentsInChildren<Slider>(true).Length == 4 &&
					Mathf.Abs(Mathf.Exp(look.value) - 1.5f) < 0.02f && UIKit.LabelOf(up).text == "Drag up: look down" && UIKit.LabelOf(wheel).text == "Wheel up: zoom out",
					"Camera... opens the Camera window (" + CameraSettingsWindow.IsOpen + "): 4 sliders, the switches, showing the saved settings");
				// the controls list: every group and row there, each on one line, the window on a small screen
				Canvas.ForceUpdateCanvases();
				var missing = new List<string>();
				var cut = new List<string>();
				foreach (string[] g in CameraSettingsWindow.ControlGroups)
					for (int i = 1; i + 1 < g.Length; i += 2)
					{
						// row names hold '/' ("Q / E"), which Find reads as a path, so match the group's children by name
						Transform groupBox = w != null ? w.transform.Find("Panel/Controls/" + (g[0] == "Camera" ? "Column_Camera" : "Column_Editing") + "/Keys_" + g[0]) : null, row = null;
						if (groupBox != null)
							foreach (Transform child in groupBox)
								if (child.name == "Key_" + g[i]) { row = child; break; }
						Text key = row != null ? row.Find("Key").GetComponent<Text>() : null, does = row != null ? row.Find("Does").GetComponent<Text>() : null;
						if (key == null || does == null || key.text != g[i] || does.text != g[i + 1]) { missing.Add(g[0] + ": " + g[i]); continue; }
						foreach (Text t in new[] { key, does })
							if (t.preferredWidth > t.rectTransform.rect.width + 1f || t.rectTransform.rect.height < t.fontSize) cut.Add("\"" + t.text + "\" (" + Num(t.preferredWidth) + " of " + Num(t.rectTransform.rect.width) + ")");
					}
				string listed = string.Join(" ", CameraSettingsWindow.ControlGroups.SelectMany(g => g).ToArray());
				string[] keys = { "Right mouse", "WASD", "Q / E", "Space / C", "Middle mouse", "Alt + left", "Alt + right", "Wheel", "Shift", "F1 / F2 / F3", "Ctrl+Z", "Ctrl+Y", "Ctrl+S", "Ctrl+O", "Esc",
					"[ / ]", "1 / 2 / 3 / 4", "Delete", "Ctrl+D", "Ctrl+C", "Ctrl+V", "Ctrl+A", "Ctrl + click", "Shift + click" };
				string[] notListed = keys.Where(k => !listed.Contains(k)).ToArray();
				List<string> over = w != null ? UIKit.RowOverflow((RectTransform)w.transform.Find("Panel"), new Vector2(1280, 720)) : new List<string> { "no window" };
				Check(ref ok, missing.Count == 0 && cut.Count == 0 && notListed.Length == 0 && over.Count == 0 && CameraSettingsWindow.ControlGroups.Sum(g => g.Length / 2) >= 30,
					"the window lists every mouse button and key (" + CameraSettingsWindow.ControlGroups.Sum(g => g.Length / 2) + " in " + CameraSettingsWindow.ControlGroups.Length + " groups), each on one line, and fits 1280 x 720" +
					(missing.Count > 0 ? "; missing " + string.Join(", ", missing.ToArray()) : "") + (cut.Count > 0 ? "; cut off " + string.Join(", ", cut.ToArray()) : "") +
					(notListed.Length > 0 ? "; not listed " + string.Join(", ", notListed) : "") + (over.Count > 0 ? "; " + string.Join("; ", over.ToArray()) : ""));
				Screenshot(new[] { "camera_settings" });
				yield return new WaitForSecondsRealtime(0.6f);
				if (look != null) look.value = Mathf.Log(3f); // (as a dragged slider does)
				if (up != null) up.onClick.Invoke();
				yield return null;
				Check(ref ok, Mathf.Abs(CameraSettings.Look - 3f) < 0.02f && CameraSettings.DragUpLooksUp && up != null && UIKit.LabelOf(up).text == "Drag up: look up",
					"its slider sets the look sensitivity (x" + CameraSettings.Format(CameraSettings.Look) + "), its switch the drag up");
				Button modsOwn = w != null ? w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == "Mod's own") : null;
				if (modsOwn != null) modsOwn.onClick.Invoke();
				yield return null;
				Check(ref ok, modsOwn != null && CameraSettings.Numbers.All(n => Mathf.Approximately(CameraSettings.Get(n), 1f)) && !CameraSettings.WheelReversed && CameraSettings.DragUpLooksUp && Mathf.Abs(Mathf.Exp(look.value) - 1f) < 0.02f,
					"Mod's own puts every camera setting back (x1, drag up looks up, wheel up zooms in)");
				CameraSettingsWindow.Close();
				yield return null;
				Check(ref ok, !CameraSettingsWindow.IsOpen && !CameraSettings.Unsaved, "Close closes it, the settings saved");

				// 8. Defaults has the same settings
				CameraSettings.Set(CameraSettings.PanNumber, 2.5f);
				CameraSettings.Save();
				DefaultsWindow.Open();
				yield return null;
				GameObject d = DefaultsWindow.Root;
				Transform group = d != null ? d.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "EditorCamera") : null;
				Slider panSlider = group != null ? group.GetComponentsInChildren<Slider>(true).FirstOrDefault(s => s.name == "CameraSlider_camerapan") : null;
				Check(ref ok, group != null && group.GetComponentsInChildren<Slider>(true).Length == 4 && panSlider != null && Mathf.Abs(Mathf.Exp(panSlider.value) - 2.5f) < 0.02f,
					"Defaults has an Editor camera part with the 4 settings (pan x" + (panSlider != null ? Mathf.Exp(panSlider.value).ToString("0.##") : "?") + ")");
				Screenshot(new[] { "camera_defaults" });
				yield return new WaitForSecondsRealtime(0.6f);
				DefaultsWindow.Close();
			}
			finally
			{
				CameraSettingsWindow.Close();
				DefaultsWindow.Close();
				CameraSettings.Save();
				try { if (rulesBefore != null) File.WriteAllText(rules, rulesBefore); else if (File.Exists(rules)) File.Delete(rules); } catch { }
				CameraSettings.Reload();
			}
			Check(ref ok, rulesBefore == null || File.ReadAllText(rules) == rulesBefore, "world_rules.txt put back as it was");
			if (ok) Log("PASS: camera settings"); else Fail("camera settings");
		}
	}
}

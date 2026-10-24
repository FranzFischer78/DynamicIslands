using System;
using System.Collections;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>The editor camera's test: every control through the same calls the mouse and keys make.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CICameraTest", docs: "Dev, editor: the editor camera - framing the island and a selection (F), high above a deep island and under its sea, never under the ground, zooming to the cursor (never through what is there), looking, orbiting around the selection, panning, moving with the keys, and carrying on after something else moved it")]
		public static void CameraTestCommand()
		{
			StartTest(CameraTestRoutine());
		}

		[ConsoleCommand(name: "CICamera", docs: "Dev, editor: puts the editor camera somewhere: CICamera <x> <y above the sea> <z> <yaw> <pitch>")]
		public static void CameraCommand(string[] args)
		{
			var f = (args ?? new string[0]).Select(a => { float v; return float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f; }).ToArray();
			if (f.Length < 5 || Camera.main == null) { Log("Usage: CICamera <x> <y above the sea> <z> <yaw> <pitch>"); return; }
			Camera.main.transform.SetPositionAndRotation(new Vector3(f[0], DynamicIslands.EditorWaterLevel + f[1], f[2]), Quaternion.Euler(f[4], f[3], 0f));
			Log("Camera put at " + Camera.main.transform.position);
		}

		static IEnumerator WaitSettled(EditorCamera c, float max = 4f)
		{
			float until = Time.unscaledTime + max;
			yield return null;
			while (!c.Settled && Time.unscaledTime < until) yield return null;
		}

		static IEnumerator CameraTestRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			bool ok = true;
			Camera cam = Camera.main;
			EditorCamera c = cam != null ? cam.GetComponent<EditorCamera>() : null;
			bool oldGone = cam != null && cam.GetComponents<MonoBehaviour>().All(m => m == null || m.GetType().Name != "RTSCamera");
			Check(ref ok, c != null && c == EditorCamera.Instance && oldGone, "the editor camera is on the main camera (the 2021 RTS camera is gone: " + oldGone + ")");
			if (c == null) { Fail("editor camera"); yield break; }

			// An island on the deep sea floor to look at
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 88, Radius = 60f, Height = 35f, Trees = 0.3f, Bushes = 0.2f, Rocks = 0.2f, Harvest = 0f, BeachThings = 0f, Water = 0.2f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f });
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			yield return null;
			Terrain terrain = terraineditor.terrain;
			float sea = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
			Func<Vector3, float> ground = p => terrain.SampleHeight(p) + terrain.transform.position.y;

			// 1. F with nothing selected: the whole island in view, from above at an angle
			c.Frame();
			yield return WaitSettled(c);
			Bounds island = EditorCamera.IslandBounds();
			Vector3 vp = cam.WorldToViewportPoint(island.center);
			bool corners = new[] { new Vector3(island.min.x, island.center.y, island.min.z), new Vector3(island.max.x, island.center.y, island.max.z), new Vector3(island.min.x, island.center.y, island.max.z), new Vector3(island.max.x, island.center.y, island.min.z) }
				.All(p => { Vector3 v = cam.WorldToViewportPoint(p); return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f; });
			float pitch = cam.transform.eulerAngles.x;
			Check(ref ok, Mathf.Abs(vp.x - 0.5f) < 0.1f && Mathf.Abs(vp.y - 0.5f) < 0.1f && corners && cam.transform.position.y > sea && pitch >= 19f && pitch <= 61f,
				"F frames the island: its middle at " + vp.x.ToString("F2") + ", " + vp.y.ToString("F2") + " of the view, all of it in view: " + corners + ", " + (cam.transform.position.y - sea).ToString("F0") + " m above the sea, looking down " + pitch.ToString("F0") + "°");
			Screenshot(new[] { "camera_frame" });
			yield return new WaitForSecondsRealtime(0.6f);

			// 2. High above the deep island's sea (the old camera stopped 150 m above the terrain's base, 10 m under this sea)
			cam.transform.position = new Vector3(500f, sea + 300f, 300f);
			yield return null; yield return null;
			float high = cam.transform.position.y - sea;
			// ... and under its sea, above the drop-off
			Vector3 under = new Vector3(500f, sea - 40f, 330f);
			under.y = Mathf.Max(under.y, ground(under) + 5f);
			cam.transform.position = under;
			yield return null; yield return null;
			float below = sea - cam.transform.position.y;
			// ... but flying down never goes into the ground (a place other code chose is left alone: zone tests put it anywhere)
			Vector3 above = terrain.transform.position + new Vector3(500f, 0f, 500f);
			above.y = ground(above) + 3f;
			cam.transform.position = above;
			yield return null;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime) { c.Move(new Vector3(0f, -1f, 0f), true, true, Time.unscaledDeltaTime); yield return null; }
			float clearance = cam.transform.position.y - ground(cam.transform.position);
			Vector3 inside = above; inside.y = ground(inside) - 5f;
			cam.transform.position = inside;
			yield return null; yield return null;
			bool leftAlone = Mathf.Abs(cam.transform.position.y - inside.y) < 0.01f;
			Check(ref ok, high > 290f && below > 4f && clearance >= 0.5f && clearance < 1f && leftAlone, "up to " + high.ToString("F0") + " m above the sea, down to " + below.ToString("F0") + " m under it; flying down stops " + clearance.ToString("F1") + " m above the ground; a place set by code is left alone: " + leftAlone);

			// 3. Zooming towards the cursor: a fifth of the way per notch, never through what is under it
			cam.transform.position = new Vector3(500f, sea + 80f, 380f);
			cam.transform.rotation = Quaternion.LookRotation(new Vector3(terrain.transform.position.x + 500f, sea + 5f, terrain.transform.position.z + 500f) - cam.transform.position);
			yield return null; yield return null;
			Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
			RaycastHit hit;
			Physics.Raycast(cam.ScreenPointToRay(screen), out hit, 5000f);
			float d0 = hit.distance;
			c.ZoomAt(screen, 1f);
			yield return WaitSettled(c);
			float d1 = Vector3.Distance(cam.transform.position, hit.point);
			for (int i = 0; i < 30; i++) c.ZoomAt(screen, 3f);
			yield return WaitSettled(c);
			float dMin = Vector3.Distance(cam.transform.position, hit.point);
			c.ZoomAt(screen, -3f);
			yield return WaitSettled(c);
			float dBack = Vector3.Distance(cam.transform.position, hit.point);
			Check(ref ok, d1 < d0 * 0.85f && d1 > d0 * 0.75f && dMin >= 1.2f && dMin < 5f && dBack > dMin,
				"zoom to the cursor: " + d0.ToString("F0") + " m -> one notch " + d1.ToString("F0") + " m -> many notches " + dMin.ToString("F1") + " m (never through it) -> back " + dBack.ToString("F1") + " m");

			// 4. Looking: the view turns with the mouse
			cam.transform.position = new Vector3(500f, sea + 60f, 350f);
			cam.transform.rotation = Quaternion.Euler(20f, 10f, 0f);
			yield return null;
			c.Look(new Vector2(100f, -50f));
			yield return null;
			Vector3 e = cam.transform.eulerAngles;
			// (dragging down tilts the view up - the user's way, 2026-10-02)
			Check(ref ok, Mathf.Abs(Mathf.DeltaAngle(e.y, 10f + 18f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(e.x, 20f - 9f)) < 0.5f, "looking: 100 px right and 50 px down turn the view to " + e.y.ToString("F0") + "° / " + e.x.ToString("F0") + "° (expected 28° / 11°: dragging down tilts it up)");

			// 5. Orbiting around a selected object: the distance stays, the object stays in the middle
			EditorGameObject tree = GameObject.Find("PlacedObjects").GetComponentsInChildren<EditorGameObject>(false).OrderBy(o => (o.transform.position - (terrain.transform.position + new Vector3(500f, 0f, 500f))).sqrMagnitude).FirstOrDefault();
			if (tree != null)
			{
				DynamicIslands.EditorGizmoHandler.AddTarget(tree.transform, false);
				c.Frame();
				yield return WaitSettled(c);
				Vector3 vpSel = cam.WorldToViewportPoint(tree.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b2) => { a.Encapsulate(b2); return a; }).center);
				c.BeginOrbit();
				float before = Vector3.Distance(cam.transform.position, c.OrbitPivot);
				for (int i = 0; i < 10; i++) { c.Orbit(new Vector2(40f, 5f)); yield return null; }
				c.EndOrbit();
				float after = Vector3.Distance(cam.transform.position, c.OrbitPivot);
				Vector3 vpPivot = cam.WorldToViewportPoint(c.OrbitPivot);
				Check(ref ok, Mathf.Abs(vpSel.x - 0.5f) < 0.1f && Mathf.Abs(vpSel.y - 0.5f) < 0.1f && Mathf.Abs(after - before) < Mathf.Max(0.5f, before * 0.03f) && Mathf.Abs(vpPivot.x - 0.5f) < 0.05f && Mathf.Abs(vpPivot.y - 0.5f) < 0.05f,
					"F frames the selected " + PlaceableCatalog.DisplayName(tree.GameObjectName) + " (in the middle: " + vpSel.x.ToString("F2") + ", " + vpSel.y.ToString("F2") + "); orbiting keeps " + before.ToString("F1") + " -> " + after.ToString("F1") + " m and keeps it in the middle");
				Screenshot(new[] { "camera_orbit" });
				yield return new WaitForSecondsRealtime(0.6f);
				DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			}
			else Check(ref ok, false, "no object to select");

			// 6. Panning: the ground under the cursor follows it
			cam.transform.position = new Vector3(500f, sea + 60f, 380f);
			cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
			yield return null;
			Vector3 p0 = cam.transform.position;
			c.BeginPan(screen);
			c.Pan(new Vector2(200f, 0f));
			c.EndPan();
			yield return null;
			Vector3 moved = cam.transform.position - p0;
			Check(ref ok, moved.x < -5f && Mathf.Abs(moved.z) < 1f, "panning 200 px right slides the view " + (-moved.x).ToString("F1") + " m left (the ground follows the cursor)");

			// 7. Keys: forward at the same height, faster with Shift
			cam.transform.position = new Vector3(500f, sea + 60f, 350f);
			cam.transform.rotation = Quaternion.Euler(30f, 180f, 0f); // (away from the island, over open deep water: the same speed all the way)
			yield return null;
			Vector3 k0 = cam.transform.position;
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { c.Move(new Vector3(0f, 0f, 1f), false, false, Time.unscaledDeltaTime); yield return null; }
			yield return WaitSettled(c, 2f);
			Vector3 walked = cam.transform.position - k0;
			cam.transform.position = k0;
			yield return null;
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { c.Move(new Vector3(0f, 0f, 1f), false, true, Time.unscaledDeltaTime); yield return null; }
			yield return WaitSettled(c, 2f);
			Vector3 ran = cam.transform.position - k0;
			Check(ref ok, -walked.z > 10f && Mathf.Abs(walked.y) < 0.5f && -ran.z > -walked.z * 2f, "W moves forward at the same height (" + (-walked.z).ToString("F0") + " m in a second, " + (-ran.z).ToString("F0") + " m with Shift)");

			// 8. Something else moves the camera (the generator frames its island): it stays there
			var s = IslandGenerator.Last.Copy();
			IslandGenerator.FrameCamera(s);
			Vector3 framed = cam.transform.position;
			for (int i = 0; i < 10; i++) yield return null;
			Check(ref ok, (cam.transform.position - framed).magnitude < 0.01f, "after the generator framed its island the camera stays there (" + (cam.transform.position - framed).magnitude.ToString("F3") + " m)");

			if (ok) Log("PASS: editor camera"); else Fail("editor camera");
		}
	}
}

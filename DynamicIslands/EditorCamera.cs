using UnityEngine;
using UnityEngine.EventSystems;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The editor's camera, handled like Unity's scene view and other modern editors (it replaces the 2021 RTS camera):
	///   Right mouse held   look around (dragging up tilts the view down); WASD flies where you look, Q/E down and up; the wheel sets the flying speed
	///   WASD / arrows      move over the island at the same height (without the right mouse)
	///   Middle mouse       pan: the ground under the cursor sticks to it
	///   Alt + left mouse   orbit around the selected objects, or the ground in the middle of the view
	///   Alt + right mouse  drag to zoom in and out (towards the middle of the view)
	///   Wheel              zoom towards whatever is under the cursor
	///   F                  frame the selected objects, or the whole island when nothing is selected
	///   Shift              three times faster
	/// Moves are smoothed and get faster the further the camera is from the ground. The camera keeps above the ground,
	/// may dive under the sea (islands on a deep sea floor are 160 m deep), and stays near the build area. Other code
	/// (framing a generated island, tests) may move the camera: it carries on from wherever it was put. Each control is
	/// a method (Look, BeginOrbit/Orbit, BeginPan/Pan, ZoomAt, Dolly, Move, Frame) that the mouse and keys - and the tests - use.
	/// The settings (CameraSettings: look sensitivity, drag up looks up or down, move, zoom and pan speed, the wheel's
	/// direction) are this PC's own: the status bar's Camera button, or Defaults > Editor camera.
	/// </summary>
	public class EditorCamera : MonoBehaviour
	{
		public static EditorCamera Instance { get; private set; }

		/// <summary>Multiplier of the flying speed (right mouse + wheel, or the Move speed setting), 0.1 .. 10; saved.</summary>
		public static float SpeedFactor { get { return CameraSettings.Speed; } set { CameraSettings.Set(CameraSettings.SpeedNumber, value); } }
		/// <summary>Degrees the view turns per pixel the mouse moves at the Look sensitivity setting x1.</summary>
		public const float BaseLookSensitivity = 0.18f;
		/// <summary>Degrees the view turns per pixel the mouse moves (with the Look sensitivity setting).</summary>
		public static float LookSensitivity { get { return BaseLookSensitivity * CameraSettings.Look; } }

		/// <summary>True while the camera uses the mouse (looking, panning, orbiting), or Alt is held: the tools leave the mouse alone then.</summary>
		/// <summary>Flying with the right mouse held: Q/E move the camera down/up then, not turn the stamp or the object.</summary>
		public static bool Looking { get { return Instance != null && Instance.isActiveAndEnabled && Instance.looking; } }
		public static bool UsingMouse { get { return Instance != null && Instance.isActiveAndEnabled && (Instance.looking || Instance.panning || Instance.orbiting || Instance.dollying || Alt); } }
		static bool Alt { get { return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt); } }

		/// <summary>The camera's controls, for the help.</summary>
		public const string Help = "Right-drag: look (WASD fly, Q/E down/up, wheel: speed) · WASD: move · Space / C: straight up / down · Middle-drag: pan · Alt+drag: orbit · Alt+right-drag: zoom · Wheel: zoom to the cursor · F: frame · Shift: faster";

		float yaw, pitch;
		Vector3 position, velocity;
		Vector3 lastPosition;
		Quaternion lastRotation;
		bool looking, panning, orbiting, dollying;
		float panDistance;
		Vector3 orbitPivot;
		// Zooming with the wheel: how much of the move is still to come (smoothed over a few frames)
		Vector3 pendingZoom;
		// Framing (F): where it flies to
		bool flying;
		Vector3 flyPosition;
		float flyYaw, flyPitch;

		/// <summary>Where the camera is heading (the tests): the place after the smoothing of zooming and framing.</summary>
		public Vector3 Destination { get { return flying ? flyPosition : position + pendingZoom; } }
		public bool Settled { get { return !flying && pendingZoom.sqrMagnitude < 0.0001f && velocity.sqrMagnitude < 0.0001f; } }
		public Vector3 OrbitPivot { get { return orbitPivot; } }

		Camera Cam { get { return GetComponent<Camera>(); } }

		void Awake()
		{
			Instance = this;
			Sync();
		}

		void OnDisable()
		{
			if (looking || dollying) Cursor.lockState = CursorLockMode.None;
			looking = panning = orbiting = dollying = false;
			CameraSettings.Save();
		}

		/// <summary>Takes over the camera's current place and direction (after something else moved it).</summary>
		public void Sync()
		{
			position = transform.position;
			Vector3 e = transform.eulerAngles;
			yaw = e.y;
			pitch = e.x > 180f ? e.x - 360f : e.x;
			velocity = Vector3.zero;
			pendingZoom = Vector3.zero;
			flying = false;
			lastPosition = transform.position;
			lastRotation = transform.rotation;
		}

		static float SeaY { get { return (terraineditor.terrain != null ? terraineditor.terrain.transform.position.y : 0f) + DynamicIslands.EditorWaterLevel; } }

		/// <summary>Ground height (world y) under a point, or the terrain's base outside it.</summary>
		static float GroundY(Vector3 p)
		{
			Terrain t = terraineditor.terrain;
			return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
		}

		void Update()
		{
			// Moved by something else (a generated island framed, a test): carry on from there
			if ((transform.position - lastPosition).sqrMagnitude > 1e-6f || Quaternion.Angle(transform.rotation, lastRotation) > 0.01f) Sync();
			float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
			bool typing = EditorInput.IsTyping;
			bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
			Vector2 mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; // (about pixels)

			CameraSettings.Tick();

			// Right mouse: look around (the cursor is held still meanwhile); with Alt: drag to zoom (as in Unity and Maya)
			if (Input.GetMouseButtonDown(1) && !overUI && !orbiting)
			{
				if (Alt) dollying = true; else looking = true;
				flying = false;
				Cursor.lockState = CursorLockMode.Locked;
			}
			if ((looking || dollying) && !Input.GetMouseButton(1)) { looking = dollying = false; Cursor.lockState = CursorLockMode.None; }
			if (looking) Look(mouse);
			if (dollying) Dolly(mouse);

			// Alt + left mouse: orbit
			if (Alt && Input.GetMouseButtonDown(0) && !overUI && !looking) BeginOrbit();
			if (orbiting && !Input.GetMouseButton(0)) orbiting = false;
			if (orbiting) Orbit(mouse);

			// Middle mouse: pan
			if (Input.GetMouseButtonDown(2) && !overUI) BeginPan(Input.mousePosition);
			if (panning && !Input.GetMouseButton(2)) panning = false;
			if (panning) Pan(mouse);

			// Keys: WASD / arrows; Space / C straight up and down (also Q/E while looking)
			Vector3 keys = Vector3.zero;
			if (!typing && !EditorInput.Ctrl && !orbiting)
			{
				keys = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
				keys.y = (Input.GetKey(KeyCode.Space) ? 1f : 0f) - (Input.GetKey(KeyCode.C) ? 1f : 0f);
				if (looking) keys.y += (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
				keys.y = Mathf.Clamp(keys.y, -1f, 1f);
			}
			Move(keys, looking, EditorInput.Shift, dt);

			// Wheel: zoom towards what is under the cursor; with the right mouse held: the flying speed
			float wheel = overUI || typing ? 0f : Input.GetAxis("Mouse ScrollWheel") * (CameraSettings.WheelReversed ? -1f : 1f);
			if (Mathf.Abs(wheel) > 0.0001f)
			{
				if (looking) ChangeSpeed(wheel > 0f ? 1 : -1);
				else ZoomAt(Input.mousePosition, wheel * 10f);
			}

			// F: frame the selection, or the whole island
			if (!typing && !EditorInput.Ctrl && !looking && Input.GetKeyDown(KeyCode.F)) Frame();

			Advance(dt);
		}

		/// <summary>Applies the smoothed moves (zoom, framing), keeps the camera in bounds and sets its transform.</summary>
		void Advance(float dt)
		{
			Vector3 zoomNow = pendingZoom * (1f - Mathf.Exp(-dt * 14f));
			position += zoomNow;
			pendingZoom -= zoomNow;
			if (flying)
			{
				float k = 1f - Mathf.Exp(-dt * 7f);
				position = Vector3.Lerp(position, flyPosition, k);
				yaw = Mathf.LerpAngle(yaw, flyYaw, k);
				pitch = Mathf.Lerp(pitch, flyPitch, k);
				if ((position - flyPosition).sqrMagnitude < 0.0004f && Mathf.Abs(Mathf.DeltaAngle(yaw, flyYaw)) < 0.05f && Mathf.Abs(pitch - flyPitch) < 0.05f) { position = flyPosition; flying = false; }
			}
			// (only moves made with the controls are kept in bounds: a place other code chose, a test's or the
			// generator's framing, stays as it is until the camera is moved)
			if ((position - lastPosition).sqrMagnitude > 1e-8f) Keep();
			transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
			lastPosition = transform.position;
			lastRotation = transform.rotation;
		}

		#region The controls

		/// <summary>Turns the view by mouse pixels (right mouse held). Dragging up tilts the view up (the default since 2026-10-21,
		/// the user's wish) - or down, as if the scene were dragged, with the setting Drag up: look down (the default from 2026-10-02).</summary>
		public void Look(Vector2 pixels)
		{
			flying = false;
			float sensitivity = LookSensitivity;
			yaw += pixels.x * sensitivity;
			pitch = Mathf.Clamp(pitch + pixels.y * sensitivity * (CameraSettings.DragUpLooksUp ? -1f : 1f), -89f, 89f);
		}

		/// <summary>Zooms by dragging (Alt + right mouse): right or up goes in, left or down out, towards the middle of the
		/// view - never through what is there.</summary>
		public void Dolly(Vector2 pixels)
		{
			flying = false;
			Camera cam = Cam;
			Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(position, transform.forward);
			float d = DistanceAlong(ray);
			float step = (pixels.x + pixels.y) * Mathf.Max(0.05f, d * 0.004f) * CameraSettings.Zoom;
			if (step > 0f) step = Mathf.Min(step, Mathf.Max(0f, d - 1.5f));
			position += ray.direction * step;
		}

		public void EndDolly() { dollying = false; }

		/// <summary>Starts orbiting around the selected objects, or the ground in the middle of the view (Alt + left mouse).</summary>
		public void BeginOrbit()
		{
			orbiting = true;
			flying = false;
			pendingZoom = Vector3.zero;
			orbitPivot = Pivot();
		}

		/// <summary>Orbits by mouse pixels, keeping the distance to the pivot.</summary>
		public void Orbit(Vector2 pixels)
		{
			float distance = Vector3.Distance(position, orbitPivot);
			yaw += pixels.x * LookSensitivity;
			pitch = Mathf.Clamp(pitch - pixels.y * LookSensitivity, -85f, 85f);
			position = orbitPivot - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
		}

		public void EndOrbit() { orbiting = false; }

		/// <summary>Starts panning (middle mouse): what is under this screen point will stay under the cursor.</summary>
		public void BeginPan(Vector2 screen)
		{
			panning = true;
			flying = false;
			Camera cam = Cam;
			panDistance = Mathf.Max(3f, cam != null ? DistanceAlong(cam.ScreenPointToRay(screen)) : 60f);
		}

		/// <summary>Pans by mouse pixels: the view slides the other way, as if the ground were dragged.</summary>
		public void Pan(Vector2 pixels)
		{
			Camera cam = Cam;
			float perPixel = 2f * panDistance * Mathf.Tan((cam != null ? cam.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height) * CameraSettings.Pan;
			Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
			position -= (r * Vector3.right * pixels.x + r * Vector3.up * pixels.y) * perPixel;
		}

		public void EndPan() { panning = false; }

		/// <summary>
		/// Moves with the keys: x = right, z = forward (-1..1). Flying (right mouse held): along the view, y = up; else
		/// level over the island. Smoothly speeds up and slows down.
		/// </summary>
		public void Move(Vector3 keys, bool fly, bool fast, float dt)
		{
			Vector3 want = fly ? Quaternion.Euler(pitch, yaw, 0f) * new Vector3(keys.x, 0f, keys.z) + Vector3.up * keys.y
				: Quaternion.Euler(0f, yaw, 0f) * new Vector3(keys.x, 0f, keys.z) + Vector3.up * keys.y; // (up and down: straight, whichever way it looks)
			if (want.sqrMagnitude > 1f) want.Normalize();
			want *= Speed() * (fast ? 3f : 1f);
			if (want.sqrMagnitude > 0f) flying = false;
			velocity = Vector3.Lerp(velocity, want, 1f - Mathf.Exp(-dt * 12f));
			if (velocity.sqrMagnitude < 0.0001f && want.sqrMagnitude == 0f) velocity = Vector3.zero;
			position += velocity * dt;
		}

		/// <summary>Zooms towards what is under a screen point: a notch goes a fifth of the way (never through it); negative notches back off.</summary>
		public void ZoomAt(Vector2 screen, float notches)
		{
			flying = false;
			Camera cam = Cam;
			Ray ray = cam != null ? cam.ScreenPointToRay(screen) : new Ray(position, transform.forward);
			float d = DistanceAlong(ray);
			float step = Mathf.Sign(notches) * Mathf.Min(Mathf.Abs(notches), 3f) * Mathf.Max(1.5f, d * 0.2f) * CameraSettings.Zoom;
			if (step > 0f) step = Mathf.Min(step, Mathf.Max(0f, d - 1.5f - Vector3.Dot(pendingZoom, ray.direction)));
			pendingZoom += ray.direction * step;
		}

		/// <summary>Faster (+1) or slower (-1) flying (right mouse + wheel).</summary>
		public void ChangeSpeed(int direction)
		{
			SpeedFactor = Mathf.Clamp(SpeedFactor * (direction > 0 ? 1.25f : 0.8f), 0.1f, 10f);
			EditorUI.Flash("Camera speed x" + SpeedFactor.ToString("0.##") + " (kept: the Camera button at the bottom right has the camera's settings)");
		}

		/// <summary>Flies to where the selection (or, without one, the whole island) fills the view, from the current direction.</summary>
		public void Frame()
		{
			Bounds b;
			bool selection = SelectionBounds(out b);
			if (!selection) b = IslandBounds();
			Camera cam = Cam;
			float fov = (cam != null ? cam.fieldOfView : 60f) * Mathf.Deg2Rad;
			// (a window narrower than it is tall sees less across than up and down: fit the narrower of the two - with the
			// vertical one alone, a 1280 x 1440 window cut the island off at the sides)
			if (cam != null && cam.aspect > 0f && cam.aspect < 1f) fov = 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f) * cam.aspect);
			float radius = Mathf.Max(1f, b.extents.magnitude);
			float distance = radius / Mathf.Sin(fov * 0.5f) * (selection ? 1.2f : 0.9f);
			flyYaw = yaw;
			flyPitch = Mathf.Clamp(pitch, 20f, 60f); // (from above at an angle, never flat or straight down)
			flyPosition = b.center - Quaternion.Euler(flyPitch, flyYaw, 0f) * Vector3.forward * distance;
			flying = true;
			velocity = Vector3.zero;
			pendingZoom = Vector3.zero;
			EditorUI.Flash(selection ? "Framed the selection (F)" : "Framed the island (F; select objects to frame them instead)");
		}

		#endregion

		/// <summary>Flying speed (m/s): faster high above the ground, slow close to it.</summary>
		float Speed()
		{
			float above = Mathf.Abs(position.y - GroundY(position));
			return Mathf.Clamp(above * 0.9f, 6f, 250f) * SpeedFactor;
		}

		/// <summary>Near the build area, above the ground, not absurdly high.</summary>
		void Keep()
		{
			Terrain t = terraineditor.terrain;
			if (t == null) return;
			Vector3 o = t.transform.position, size = t.terrainData.size;
			position.x = Mathf.Clamp(position.x, o.x - 400f, o.x + size.x + 400f);
			position.z = Mathf.Clamp(position.z, o.z - 400f, o.z + size.z + 400f);
			bool overTerrain = position.x >= o.x && position.z >= o.z && position.x <= o.x + size.x && position.z <= o.z + size.z;
			float floor = overTerrain ? GroundY(position) + 0.6f : o.y + 0.6f;
			position.y = Mathf.Clamp(position.y, floor, SeaY + 900f);
		}

		/// <summary>Metres along a ray to the first thing it hits (terrain or an object), the sea, or 60 m.</summary>
		float DistanceAlong(Ray ray)
		{
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 5000f, ~0, QueryTriggerInteraction.Ignore)) return hit.distance;
			if (Mathf.Abs(ray.direction.y) > 0.01f)
			{
				float t = (SeaY - ray.origin.y) / ray.direction.y;
				if (t > 0f) return t;
			}
			return 60f;
		}

		/// <summary>What Alt+drag orbits around: the selected objects, or the ground in the middle of the view.</summary>
		Vector3 Pivot()
		{
			Bounds b;
			if (SelectionBounds(out b)) return b.center;
			Camera cam = Cam;
			Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(position, transform.forward);
			return ray.GetPoint(DistanceAlong(ray));
		}

		static bool SelectionBounds(out Bounds b)
		{
			b = new Bounds();
			var gizmo = DynamicIslands.EditorGizmoHandler;
			if (gizmo == null || gizmo.SelectedRoots == null || gizmo.SelectedRoots.Count == 0) return false;
			bool any = false;
			foreach (Transform t in gizmo.SelectedRoots)
			{
				if (t == null) continue;
				foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
				{
					if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
				}
				if (!any) { b = new Bounds(t.position, Vector3.one * 2f); any = true; } else b.Encapsulate(t.position);
			}
			return any;
		}

		/// <summary>The land of the island being edited (above the sea), or the middle of the build area.</summary>
		public static Bounds IslandBounds()
		{
			Terrain t = terraineditor.terrain;
			if (t == null) return new Bounds(Vector3.zero, Vector3.one * 100f);
			TerrainData d = t.terrainData;
			int res = d.heightmapResolution, step = Mathf.Max(1, res / 128);
			float[,] h = d.GetHeights(0, 0, res, res);
			float sea = DynamicIslands.EditorWaterLevel / d.size.y;
			Vector3 o = t.transform.position;
			var b = new Bounds();
			bool any = false;
			for (int z = 0; z < res; z += step)
				for (int x = 0; x < res; x += step)
				{
					if (h[z, x] <= sea) continue;
					var p = new Vector3(o.x + x * d.size.x / (res - 1), o.y + h[z, x] * d.size.y, o.z + z * d.size.z / (res - 1));
					if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
				}
			if (!any) b = new Bounds(o + new Vector3(d.size.x / 2f, DynamicIslands.EditorWaterLevel, d.size.z / 2f), new Vector3(120f, 10f, 120f));
			return b;
		}
	}
}

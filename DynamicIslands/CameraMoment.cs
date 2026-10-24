using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The when/then action "camera" (ROADMAP RF2): the camera glides to a marker object, looks at a target object for
	/// some seconds, and glides back. The player's input (walking, looking) is held meanwhile; Esc skips it. The host runs
	/// the action (a shared verb) and tells every player (IslandNetMessage.Moment); each machine shows it only if its own
	/// player is within the range of the marker.
	/// Target = the marker's name (empty = the object itself); Arg = "look at|seconds|range" (look at: an object's name,
	/// empty = the marker's own facing; seconds 1-30, default 4; range in metres, default 50).
	/// </summary>
	public class CameraMoment : MonoBehaviour
	{
		public const float Glide = 0.8f, DefaultSeconds = 4f, DefaultRange = 50f;

		static CameraMoment current;
		public static bool Active { get { return current != null; } }
		/// <summary>Tests: moments shown here, skipped with Esc, and where the marker was.</summary>
		public static int Shown, Skipped;
		public static Vector3 LastMarker;
		public static Quaternion LastLook;

		Transform cam;
		Vector3 localPos, fromPos;
		Quaternion localRot, fromRot;
		Vector3 markerPos;
		Quaternion markerRot;
		float seconds, started;
		readonly List<Behaviour> held = new List<Behaviour>();

		public static void Parse(string arg, out string look, out float seconds, out float range)
		{
			string[] p = (arg ?? "").Split('|');
			look = p[0].Trim();
			seconds = DefaultSeconds;
			range = DefaultRange;
			float v;
			if (p.Length > 1 && float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) seconds = Mathf.Clamp(v, 1f, 30f);
			if (p.Length > 2 && float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) range = Mathf.Clamp(v, 5f, 1000f);
		}

		public static string Describe(string marker, string arg)
		{
			string look; float s, r;
			Parse(arg, out look, out s, out r);
			return "show the view from " + (marker.Trim().Length > 0 ? "'" + marker.Trim() + "'" : "itself") + (look.Length > 0 ? " toward '" + look + "'" : "") +
				" for " + s.ToString("0.#", CultureInfo.InvariantCulture) + " s (players within " + r.ToString("0", CultureInfo.InvariantCulture) + " m)";
		}

		/// <summary>Host: the action ran from this object.</summary>
		public static void HostRun(IslandWorldState.Entry e, int index, string marker, string arg)
		{
			if (e == null) return;
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Moment, Ids = new[] { e.Id }, Index = index, Name = "camera", Data = (marker ?? "") + "\n" + (arg ?? "") });
			Here(e, index, marker, arg);
		}

		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost || msg.Ids == null || msg.Ids.Length == 0) return;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == msg.Ids[0]);
			string[] d = (msg.Data ?? "").Split(new[] { '\n' }, 2);
			if (e != null) Here(e, msg.Index, d[0], d.Length > 1 ? d[1] : "");
		}

		static IslandObjectRef Find(IslandWorldState.Entry e, int index, string name)
		{
			name = (name ?? "").Trim();
			if (name.Length == 0 || name.Equals("self", StringComparison.OrdinalIgnoreCase)) return Behaviours.RefsOf(e).FirstOrDefault(r => r.Index == index);
			return Behaviours.RefsOf(e).FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>This machine: shows the moment if its player is within range of the marker.</summary>
		static void Here(IslandWorldState.Entry e, int index, string marker, string arg)
		{
			if (e.Root == null) return;
			string lookName; float seconds, range;
			Parse(arg, out lookName, out seconds, out range);
			IslandObjectRef m = Find(e, index, marker);
			if (m == null) { Debug.LogWarning("[CUSTOM ISLANDS] Camera moment: no marker '" + marker + "' on '" + e.HostName + "'"); return; }
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null) return;
			float far = Vector3.Distance(p.transform.position, m.transform.position);
			if (far > range) { Debug.Log("[CUSTOM ISLANDS] Camera moment at '" + m.Name + "': this player is " + far.ToString("F0") + " m away (range " + range.ToString("F0") + " m)"); return; }
			IslandObjectRef look = lookName.Length > 0 ? Find(e, index, lookName) : null;
			Vector3 at = m.transform.position + Vector3.up * 1.6f;
			Quaternion rot = look != null && (look.transform.position - at).sqrMagnitude > 0.01f ? Quaternion.LookRotation(look.transform.position - at) : Quaternion.Euler(0f, m.transform.eulerAngles.y, 0f);
			Begin(at, rot, seconds);
		}

		/// <summary>Starts a moment here (a new one replaces one that runs).</summary>
		public static void Begin(Vector3 at, Quaternion rot, float seconds)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			Transform cam = p != null && p.Camera != null ? p.Camera.transform : Camera.main != null ? Camera.main.transform : null;
			if (cam == null) return;
			if (current != null) current.End(false);
			var go = new GameObject("CustomIslands_CameraMoment");
			DontDestroyOnLoad(go);
			CameraMoment c = go.AddComponent<CameraMoment>();
			c.cam = cam;
			c.localPos = cam.localPosition;
			c.localRot = cam.localRotation;
			c.fromPos = cam.position;
			c.fromRot = cam.rotation;
			c.markerPos = at;
			c.markerRot = rot;
			c.seconds = seconds;
			c.started = Time.unscaledTime;
			// Hold the player: no walking, no looking around
			if (p != null)
			{
				foreach (MouseLook ml in p.GetComponentsInChildren<MouseLook>(true).Where(x => x.enabled)) { ml.enabled = false; c.held.Add(ml); }
				if (p.PersonController != null && p.PersonController.enabled) { p.PersonController.enabled = false; c.held.Add(p.PersonController); }
			}
			current = c;
			Shown++;
			LastMarker = at;
			LastLook = rot;
			Debug.Log("[CUSTOM ISLANDS] Camera moment: " + seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s at " + at.ToString("F1"));
		}

		/// <summary>Esc (and tests): back to the player at once.</summary>
		public static void Skip()
		{
			if (current == null) return;
			Skipped++;
			current.End(true);
		}

		/// <summary>The player's input is held (tests).</summary>
		public static bool InputHeld
		{
			get
			{
				Network_Player p = RAPI.GetLocalPlayer();
				return p != null && p.PersonController != null && !p.PersonController.enabled;
			}
		}

		/// <summary>Where the camera is now (tests).</summary>
		public static Vector3 CameraNow
		{
			get
			{
				Network_Player p = RAPI.GetLocalPlayer();
				Transform t = p != null && p.Camera != null ? p.Camera.transform : Camera.main != null ? Camera.main.transform : null;
				return t != null ? t.position : Vector3.zero;
			}
		}

		void Pose()
		{
			if (cam == null) { End(false); return; }
			float t = Time.unscaledTime - started;
			Vector3 home = cam.parent != null ? cam.parent.TransformPoint(localPos) : localPos;
			Quaternion homeRot = cam.parent != null ? cam.parent.rotation * localRot : localRot;
			if (t < Glide)
			{
				float k = Mathf.SmoothStep(0f, 1f, t / Glide);
				cam.position = Vector3.Lerp(fromPos, markerPos, k);
				cam.rotation = Quaternion.Slerp(fromRot, markerRot, k);
			}
			else if (t < Glide + seconds) { cam.position = markerPos; cam.rotation = markerRot; }
			else if (t < Glide * 2f + seconds)
			{
				float k = Mathf.SmoothStep(0f, 1f, (t - Glide - seconds) / Glide);
				cam.position = Vector3.Lerp(markerPos, home, k);
				cam.rotation = Quaternion.Slerp(markerRot, homeRot, k);
			}
			else End(false);
		}

		void Update()
		{
			if (current != this) return;
			if (Input.GetKeyDown(KeyCode.Escape) && Time.unscaledTime - started > 0.1f) { EscKey.Swallow(); Skip(); return; }
			if (!LoadSceneManager.IsGameSceneLoaded || RAPI.GetLocalPlayer() == null) { End(false); return; }
			Pose();
		}

		void LateUpdate() { if (current == this) Pose(); }

		void End(bool skipped)
		{
			if (current == this) current = null;
			if (cam != null) { cam.localPosition = localPos; cam.localRotation = localRot; }
			foreach (Behaviour b in held) if (b != null) b.enabled = true;
			held.Clear();
			Debug.Log("[CUSTOM ISLANDS] Camera moment " + (skipped ? "skipped" : "over"));
			// (Esc also opens Raft's pause menu: closed again for a skip)
			if (skipped && DynamicIslands.instance != null) DynamicIslands.instance.StartCoroutine(ClosePause());
			Destroy(gameObject);
		}

		static IEnumerator ClosePause()
		{
			for (int i = 0; i < 3; i++)
			{
				yield return null;
				if (CanvasHelper.ActiveMenu != MenuType.PauseMenu) continue;
				CanvasHelper helper = null;
				try { helper = ComponentManager<CanvasHelper>.Value; } catch { }
				if (helper == null) helper = FindObjectOfType<CanvasHelper>();
				if (helper != null) try { helper.CloseMenu(MenuType.PauseMenu); } catch { }
				yield break;
			}
		}
	}
}

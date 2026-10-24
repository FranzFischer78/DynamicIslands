using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Whale breach" (WorldOptions.WhaleBreach, TODO 4j): now and then, by day, a whale breaches far from the
	/// raft (DistanceMin-DistanceMax m off, side-on to it): it rises steeply, leaps half out of the sea, falls back on its side
	/// with a big splash and spray, dives and is gone. Looks only: no collider, no AI, nothing touches the raft or a player.
	///   - The whale is Raft's own (AI_NetworkBehaviourType.Whale): a local copy of its prefab with every script, collider and
	///     body taken off before it ever wakes (made under an inactive holder), so only its model and Animator are left. The
	///     path is driven here (Catmull-Rom keys in whale lengths). The splash is Raft's own pooled water splash
	///     ("WaterSplash_Player", a player falling in), copied and made bigger; the spray is the whale's own blowhole particle.
	///   - The host decides when (every GapMin-GapMax s, never at night) and where, and sends it to every player
	///     (IslandNetMessage.WhaleBreach: Offsets = x, z from the raft, heading, roll side; Index = the breach's number). Each
	///     machine plays it at the same place relative to its raft. Nothing is saved.
	/// </summary>
	public static class WhaleBreach
	{
		public const float DistanceMin = 170f, DistanceMax = 320f;
		/// <summary>Seconds between breaches (Raft's day and night together last about 20 minutes: one or two a day).</summary>
		public const float GapMin = 480f, GapMax = 900f, FirstMin = 180f, FirstMax = 420f;

		/// <summary>Breaches started and finished (removed) on this machine since the mod loaded (tests).</summary>
		internal static int Started, Finished;
		/// <summary>The breach being shown on this machine (null = none).</summary>
		internal static Show Current;

		static float nextAt = -1f, nextCheck;
		static int number;

		public static bool On { get { return WorldOptions.On(WorldOptions.WhaleBreach); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [whale] " + msg); }

		internal static void Reset()
		{
			nextAt = -1f;
			if (Current != null) Current.End();
		}

		static Vector3? RaftPosition
		{
			get
			{
				try
				{
					Raft raft = ComponentManager<Raft>.Value;
					if (raft != null) return raft.transform.position;
					Network_Player me = RAPI.GetLocalPlayer();
					if (me != null) return me.transform.position;
				}
				catch { }
				return null;
			}
		}

		/// <summary>Every frame (the mod's Update): the host's timer.</summary>
		internal static void Tick()
		{
			if (Time.time < nextCheck) return;
			nextCheck = Time.time + 2f;
			if (!LoadSceneManager.IsGameSceneLoaded) { nextAt = -1f; return; }
			if (!On || !Raft_Network.IsHost) { nextAt = -1f; return; }
			if (nextAt < 0f) { nextAt = Time.time + UnityEngine.Random.Range(FirstMin, FirstMax); return; }
			if (Time.time < nextAt || Current != null) return;
			if (NightDanger.IsNight) { nextAt = Time.time + 30f; return; }
			if (!StartNow(UnityEngine.Random.Range(DistanceMin, DistanceMax))) { nextAt = Time.time + 60f; return; }
			nextAt = Time.time + UnityEngine.Random.Range(GapMin, GapMax);
		}

		/// <summary>Host (and tests): a breach now, about this far from the raft, side-on to it. False if no open sea was found.</summary>
		public static bool StartNow(float distance)
		{
			Vector3? raft = RaftPosition;
			if (!raft.HasValue) return false;
			for (int i = 0; i < 12; i++)
			{
				float a = UnityEngine.Random.Range(0f, 360f), d = distance * UnityEngine.Random.Range(0.92f, 1.08f);
				Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
				Vector3 at = raft.Value + dir * d;
				at.y = 0f;
				if (!OpenSea(at)) continue;
				// (side-on to the raft, so it is seen whole: 90 degrees from the line to the raft, give or take 30)
				float heading = a + (UnityEngine.Random.value < 0.5f ? 90f : -90f) + UnityEngine.Random.Range(-30f, 30f);
				float roll = UnityEngine.Random.value < 0.5f ? 1f : -1f;
				number++;
				var msg = new IslandNetMessage { Kind = IslandNetMessage.WhaleBreach, Index = number, Offsets = new[] { at.x - raft.Value.x, at.z - raft.Value.z, heading, roll } };
				Log("Breach " + number + " at " + d.ToString("F0") + " m from the raft, heading " + heading.ToString("F0"));
				Play(at, heading, roll);
				IslandNetwork.SendToEveryone(msg);
				return true;
			}
			Log("No open sea for a breach near the raft");
			return false;
		}

		/// <summary>Nothing above the sea within 45 m of the spot (islands, rafts, debris).</summary>
		static bool OpenSea(Vector3 at)
		{
			for (int i = 0; i <= 8; i++)
			{
				Vector3 p = i == 0 ? at : at + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 45f;
				RaycastHit hit;
				if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out hit, 320f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > 0.5f) return false;
			}
			return true;
		}

		/// <summary>A player: the host's breach (Offsets = x, z from the raft, heading, roll side).</summary>
		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost || msg.Offsets == null || msg.Offsets.Length < 4) return;
			Vector3? raft = RaftPosition;
			if (!raft.HasValue || !LoadSceneManager.IsGameSceneLoaded) return;
			Log("The host's breach " + msg.Index);
			Play(new Vector3(raft.Value.x + msg.Offsets[0], 0f, raft.Value.z + msg.Offsets[1]), msg.Offsets[2], msg.Offsets[3]);
		}

		/// <summary>This machine: shows a breach at a spot on the sea.</summary>
		internal static void Play(Vector3 at, float heading, float roll)
		{
			if (Current != null) Current.End();
			GameObject whale = MakeWhale();
			if (whale == null) { Log("Raft has no whale to show"); return; }
			var root = new GameObject("CI_WhaleBreach");
			Show s = root.AddComponent<Show>();
			s.Setup(whale, at, heading, roll);
			Current = s;
			Started++;
		}

		#region Raft's whale and splash, copied as looks only

		static readonly Type[] Keep = { typeof(Transform), typeof(Renderer), typeof(MeshFilter), typeof(Animator), typeof(ParticleSystem), typeof(LODGroup) };

		/// <summary>A copy of a prefab made under an inactive holder (none of its scripts wakes), with only looks left.</summary>
		static GameObject LooksOnly(GameObject prefab, Transform holder)
		{
			GameObject go = UnityEngine.Object.Instantiate(prefab, holder, false);
			Strip(go);
			return go;
		}

		static void Strip(GameObject go)
		{
			// (scripts first - they need the colliders and bodies - and a few passes for the ones that need each other)
			for (int pass = 0; pass < 3; pass++)
				foreach (Component c in go.GetComponentsInChildren<Component>(true))
				{
					if (c == null || Keep.Any(k => k.IsInstanceOfType(c))) continue;
					if (pass == 0 && !(c is MonoBehaviour)) continue;
					try { UnityEngine.Object.DestroyImmediate(c); } catch { }
				}
		}

		static ParticleSystem.MinMaxCurve Times(ParticleSystem.MinMaxCurve v, float k)
		{
			switch (v.mode)
			{
				case ParticleSystemCurveMode.Constant: return new ParticleSystem.MinMaxCurve(v.constant * k);
				case ParticleSystemCurveMode.TwoConstants: return new ParticleSystem.MinMaxCurve(v.constantMin * k, v.constantMax * k);
				default: v.curveMultiplier *= k; return v;
			}
		}

		static Transform holder;
		static Transform Holder
		{
			get
			{
				if (holder == null)
				{
					var h = new GameObject("CI_WhaleBreachHolder");
					h.SetActive(false);
					UnityEngine.Object.DontDestroyOnLoad(h);
					holder = h.transform;
				}
				return holder;
			}
		}

		internal static GameObject WhalePrefab()
		{
			try
			{
				Network_Host_Entities host = ComponentManager<Network_Host_Entities>.Value;
				AI_NetworkBehaviour b = host != null && host.AINetworkBehaviourPrefabs != null ? host.AINetworkBehaviourPrefabs.FirstOrDefault(x => x != null && x.behaviourType == AI_NetworkBehaviourType.Whale) : null;
				return b != null ? b.gameObject : null;
			}
			catch { return null; }
		}

		/// <summary>The whale's own blowhole particle, found on the copy before its script goes.</summary>
		internal static ParticleSystem lastBlow;

		static GameObject MakeWhale()
		{
			GameObject prefab = WhalePrefab();
			if (prefab == null) return null;
			GameObject go = UnityEngine.Object.Instantiate(prefab, Holder, false);
			lastBlow = null;
			try { AI_NetworkBehaviour_Whale w = go.GetComponentInChildren<AI_NetworkBehaviour_Whale>(true); if (w != null) lastBlow = Traverse.Create(w).Field("waterParticle").GetValue<ParticleSystem>(); } catch { }
			Strip(go);
			go.name = "Whale (looks only)";
			foreach (Animator an in go.GetComponentsInChildren<Animator>(true)) { an.cullingMode = AnimatorCullingMode.AlwaysAnimate; an.applyRootMotion = false; }
			return go;
		}

		static readonly string[] SplashPools = { "WaterSplash_Player", "WaterSplash_Hook", "WaterSplash_Paddle" };
		static GameObject splashPrefab;

		internal static GameObject SplashPrefab()
		{
			if (splashPrefab != null) return splashPrefab;
			try
			{
				PoolManager pm = ComponentManager<PoolManager>.Value;
				if (pm == null) return null;
				foreach (string n in SplashPools)
				{
					ObjectPool pool = pm.GetPoolByName(n);
					GameObject p = pool != null ? Traverse.Create(pool).Field("objectPrefab").GetValue<GameObject>() : null;
					if (p != null) { splashPrefab = p; Log("Splash: Raft's " + n); break; }
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [whale] Splash: " + e.Message); }
			return splashPrefab;
		}

		/// <summary>Raft's water splash, k times its size, at a spot on the sea; gone by itself after a while.</summary>
		internal static GameObject Splash(Vector3 at, float k)
		{
			GameObject prefab = SplashPrefab();
			if (prefab == null) return null;
			GameObject go = LooksOnly(prefab, Holder);
			go.name = "CI_WhaleSplash";
			foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule m = ps.main;
				m.loop = false;
				m.startSizeMultiplier *= k;
				m.startSpeedMultiplier *= Mathf.Sqrt(k) * 1.4f;
				m.maxParticles = Mathf.Max(m.maxParticles, 400);
				m.simulationSpace = ParticleSystemSimulationSpace.World;
				ParticleSystem.ShapeModule sh = ps.shape;
				if (sh.enabled) { sh.radius *= k; sh.scale = Vector3.Scale(sh.scale, new Vector3(k, 1f, k)); }
				ParticleSystem.EmissionModule em = ps.emission;
				em.rateOverTimeMultiplier *= k;
				var bursts = new ParticleSystem.Burst[em.burstCount];
				em.GetBursts(bursts);
				for (int i = 0; i < bursts.Length; i++) bursts[i].count = Times(bursts[i].count, k);
				if (bursts.Length > 0) em.SetBursts(bursts);
				ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
				if (r != null) r.maxParticleSize = Mathf.Max(r.maxParticleSize, 2f);
			}
			go.transform.SetParent(null, false);
			go.transform.position = at;
			go.transform.rotation = Quaternion.identity;
			go.SetActive(true);
			foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(true); ps.Play(true); }
			UnityEngine.Object.Destroy(go, 12f);
			return go;
		}

		#endregion

		/// <summary>One breach, on this machine: drives the whale along its path, makes the splashes, then removes it all.</summary>
		public class Show : MonoBehaviour
		{
			// Keys: time (s), height and distance along the heading of the body's middle (in whale lengths), pitch (nose up), roll
			static readonly float[] T = { 0f, 1.3f, 2.6f, 3.7f, 5.3f, 7.6f };
			static readonly float[] Y = { -1.6f, -0.65f, 0.12f, -0.18f, -0.6f, -1.7f };
			static readonly float[] X = { -0.55f, -0.35f, -0.12f, 0.18f, 0.55f, 1.0f };
			static readonly float[] Pitch = { 72f, 70f, 60f, 12f, -22f, -38f };
			static readonly float[] Roll = { 0f, 4f, 25f, 80f, 115f, 140f };
			public static float Duration { get { return T[T.Length - 1]; } }

			GameObject whale;
			Transform body;
			Vector3 at, shiftAtStart;
			Quaternion headingRot;
			float rollSide, start, length;
			bool broke, splashed, ended;
			ParticleSystem blow;
			readonly List<GameObject> splashes = new List<GameObject>();

			/// <summary>The highest point of the whale above the sea so far (tests).</summary>
			public float HighestTop = float.MinValue;
			public bool Splashed { get { return splashed; } }
			public float Length { get { return length; } }
			public Vector3 At { get { return Here(); } }
			public float Age { get { return Time.time - start; } }

			internal void Setup(GameObject w, Vector3 spot, float heading, float roll)
			{
				whale = w;
				at = spot;
				shiftAtStart = IslandWorldState.ShiftedBy;
				headingRot = Quaternion.Euler(0f, heading, 0f);
				rollSide = roll >= 0f ? 1f : -1f;
				start = Time.time;
				blow = lastBlow;

				// The model's size and middle, measured as it stands; its long axis turned to point ahead
				whale.transform.SetParent(null, false);
				whale.transform.position = Vector3.zero;
				if (blow != null) try { blow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
				whale.transform.rotation = Quaternion.identity;
				Bounds b = new Bounds();
				bool any = false;
				foreach (Renderer r in whale.GetComponentsInChildren<Renderer>(true))
				{
					if (r is ParticleSystemRenderer) continue;
					if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
				}
				Vector3 size = any ? b.size : new Vector3(4f, 4f, 15f);
				length = Mathf.Max(6f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
				Quaternion fix = size.x > size.z && size.x > size.y ? Quaternion.Euler(0f, -90f, 0f) : (size.y > size.z && size.y > size.x ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
				Vector3 middle = any ? b.center : Vector3.zero;
				body = new GameObject("Body").transform;
				body.SetParent(transform, false);
				whale.transform.SetParent(body, false);
				whale.transform.localRotation = fix;
				whale.transform.localPosition = -(fix * middle);
				Log("Whale " + length.ToString("F1") + " m long (size " + size.ToString("F1") + ", middle " + middle.ToString("F1") + ")");
				Place(0f);
			}

			Vector3 Here() { return at - (IslandWorldState.ShiftedBy - shiftAtStart); }

			static float Cr(float[] v, float[] t, float time)
			{
				int n = t.Length;
				if (time <= t[0]) return v[0];
				if (time >= t[n - 1]) return v[n - 1];
				int i = 0;
				while (i < n - 2 && time > t[i + 1]) i++;
				float u = (time - t[i]) / (t[i + 1] - t[i]);
				float p0 = v[Mathf.Max(0, i - 1)], p1 = v[i], p2 = v[i + 1], p3 = v[Mathf.Min(n - 1, i + 2)];
				return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
			}

			void Place(float time)
			{
				Vector3 fwd = headingRot * Vector3.forward;
				Vector3 c = Here() + fwd * (Cr(X, T, time) * length) + Vector3.up * (Cr(Y, T, time) * length);
				transform.position = c;
				transform.rotation = headingRot * Quaternion.Euler(-Cr(Pitch, T, time), 0f, rollSide * Cr(Roll, T, time));
			}

			Vector3 Nose { get { return transform.position + transform.forward * (length * 0.5f); } }

			void Update()
			{
				if (ended) return;
				float time = Time.time - start;
				if (time >= Duration) { End(); return; }
				Place(time);
				Vector3 nose = Nose, tail = transform.position - transform.forward * (length * 0.5f);
				HighestTop = Mathf.Max(HighestTop, Mathf.Max(nose.y, tail.y));
				// The nose breaks the surface: spray where it comes out
				if (!broke && nose.y > 0f)
				{
					broke = true;
					Hold(Splash(new Vector3(nose.x, 0f, nose.z), 3f));
				}
				// The fall back onto the sea: the big splash along the body, and spray
				if (!splashed && time > T[2] && transform.position.y < -0.05f * length)
				{
					splashed = true;
					Vector3 mid = new Vector3(transform.position.x, 0f, transform.position.z), along = transform.forward; along.y = 0f;
					Hold(Splash(mid, 6f));
					Hold(Splash(mid + along.normalized * length * 0.3f, 4f));
					Hold(Splash(mid - along.normalized * length * 0.3f, 4f));
					if (blow != null)
					{
						try
						{
							blow.transform.SetParent(null, true);
							blow.transform.position = mid;
							blow.transform.rotation = Quaternion.LookRotation(Vector3.up);
							ParticleSystem.MainModule m = blow.main;
							m.startSizeMultiplier *= 2f;
							blow.Play(true);
							Hold(blow.gameObject);
							UnityEngine.Object.Destroy(blow.gameObject, 10f);
						}
						catch { }
					}
					Log("Splash (" + Started + ")");
				}
			}

			void Hold(GameObject g) { if (g != null) splashes.Add(g); }

			/// <summary>The whale gone (it dove away, or the world was left). Splashes finish on their own.</summary>
			internal void End()
			{
				if (ended) return;
				ended = true;
				if (Current == this) Current = null;
				Finished++;
				Log("The whale is gone");
				UnityEngine.Object.Destroy(gameObject);
			}

			void OnDestroy()
			{
				if (!ended) { ended = true; Finished++; if (Current == this) Current = null; }
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The when/then actions "playsound" and "stopsound" (ROADMAP RF1): one of Raft's sound events or music cues, by name,
	/// with a volume, once or looped, near the object or for everyone on the island. The host runs them (they are shared
	/// verbs) and tells every player (IslandNetMessage.Moment); each machine plays its own copy while its player is near the
	/// island, and stops it when the player leaves or the island unloads. A looped sound is kept in the island's state
	/// (KeyBase + object index, Yield = the action's hash), so it is back after a reload and for a player who joins later;
	/// a once sound is not kept.
	/// Arg: "event:/path|volume 0-1|once or loop|near or island" (the parts after the path in any order, all optional).
	/// </summary>
	public static class TriggerSounds
	{
		/// <summary>The island state key of an object's looped sound: KeyBase + (object index &amp; 0xFFFF).</summary>
		public const int KeyBase = 0xD0000;

		public class Spec
		{
			public string Path = "";
			public float Volume = 0.9f;
			public bool Loop, Island;
		}

		public static Spec Parse(string arg)
		{
			var s = new Spec();
			string[] parts = (arg ?? "").Split('|');
			s.Path = parts[0].Trim();
			for (int i = 1; i < parts.Length; i++)
			{
				string p = parts[i].Trim().ToLowerInvariant();
				float v;
				if (p == "loop") s.Loop = true;
				else if (p == "once") s.Loop = false;
				else if (p == "island" || p == "everyone" || p == "all") s.Island = true;
				else if (p == "near") s.Island = false;
				else if (float.TryParse(p.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) s.Volume = Mathf.Clamp01(p.EndsWith("%") || v > 1f ? v / 100f : v);
			}
			return s;
		}

		public static string Join(Spec s)
		{
			return s.Path + "|" + s.Volume.ToString("0.##", CultureInfo.InvariantCulture) + "|" + (s.Loop ? "loop" : "once") + "|" + (s.Island ? "island" : "near");
		}

		/// <summary>A hash of the action's text that stays the same between runs (string.GetHashCode doesn't).</summary>
		public static int Hash(string arg)
		{
			unchecked
			{
				uint h = 2166136261;
				foreach (char c in (arg ?? "").Trim()) { h ^= c; h *= 16777619; }
				return (int)(h & 0x7FFFFFFF);
			}
		}

		public static string Describe(string arg, string where)
		{
			Spec s = Parse(arg);
			string name = s.Path.Length > 0 ? s.Path.Substring(s.Path.LastIndexOf('/') + 1) : "a sound";
			return "play " + name + (s.Loop ? " on a loop" : "") + (s.Island ? " for everyone on the island" : " at " + where) +
				" (" + Mathf.RoundToInt(s.Volume * 100f) + "%)";
		}

		static int KeyOf(int index) { return KeyBase + (index & 0xFFFF); }
		static int IndexOfKey(int key) { int i = key - KeyBase; return i == 0xFFFF ? -1 : i; }
		static bool IsKey(int key) { return key >= KeyBase && key < KeyBase + 0x10000; }
		/// <summary>A looped sound's own entry: the range is shared with Survivors' cages and was with buried treasure, whose
		/// used entries are not Active - the tick below dropped them as "sound gone" (mprf4's treasure came back, 2026-10-18).</summary>
		static bool IsSound(KeyValuePair<int, ObjectState> kv) { return IsKey(kv.Key) && kv.Value.Active; }
		static bool HasSound(IslandWorldState.Entry e, int key) { ObjectState s; return e.State.TryGetValue(key, out s) && s.Active; }

		class Playing
		{
			public int Island, Index;
			public string Arg;
			public Spec Spec;
			public FMOD.Studio.EventInstance I;
			public bool Valid;
		}

		static readonly List<Playing> playing = new List<Playing>();
		static float nextTick;

		/// <summary>Tests: sounds this action started here, and the last path.</summary>
		internal static int Started;
		internal static string LastPath;

		/// <summary>Tests: the sounds playing here on an island (-1 = any island).</summary>
		public static int CountPlaying(int islandId, bool? loop = null)
		{
			return playing.Count(p => (islandId < 0 || p.Island == islandId) && (loop == null || p.Spec.Loop == loop.Value) && IsAlive(p));
		}

		/// <summary>Tests: FMOD says the instance is playing (or starting).</summary>
		public static bool AnyAudible(int islandId)
		{
			foreach (Playing p in playing.Where(x => x.Island == islandId && x.Valid))
			{
				try
				{
					FMOD.Studio.PLAYBACK_STATE st;
					if (p.I.getPlaybackState(out st) == FMOD.RESULT.OK && (st == FMOD.Studio.PLAYBACK_STATE.PLAYING || st == FMOD.Studio.PLAYBACK_STATE.STARTING)) return true;
				}
				catch { }
			}
			return false;
		}

		#region Host and messages

		/// <summary>Host: the action ran (play, or stop) for these objects (-1 = the island).</summary>
		public static void HostRun(IslandWorldState.Entry e, bool play, string arg, List<int> objects)
		{
			if (e == null) return;
			bool all = !play && (arg ?? "").Trim().Equals("all", StringComparison.OrdinalIgnoreCase);
			if (all) objects = new List<int> { -2 };
			foreach (int index in objects)
			{
				Apply(e, play, arg, index);
				IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Moment, Ids = new[] { e.Id }, Index = index, Name = play ? "sound" : "stop", Data = arg ?? "" });
			}
			if (objects.Count == 0) Debug.Log("[CUSTOM ISLANDS] " + (play ? "Play" : "Stop") + " sound: no object found on '" + e.HostName + "'");
		}

		/// <summary>Every machine: index -2 = every sound of the island.</summary>
		static void Apply(IslandWorldState.Entry e, bool play, string arg, int index)
		{
			if (play)
			{
				Spec s = Parse(arg);
				if (s.Path.Length == 0) return;
				if (s.Loop) e.State[KeyOf(index)] = new ObjectState { Active = true, Yield = Hash(arg), Day = Behaviours.Today };
				Stop(e.Id, index); // (one sound per object: a new one replaces it)
				if (e.Root != null && Behaviours.Near(e)) Start(e, index, arg);
				Debug.Log("[CUSTOM ISLANDS] Sound '" + s.Path + "' " + (s.Loop ? "looped" : "once") + " on '" + e.HostName + "' object " + index);
				return;
			}
			if (index == -2) foreach (int k in e.State.Where(IsSound).Select(kv => kv.Key).ToList()) e.State.Remove(k);
			else if (HasSound(e, KeyOf(index))) e.State.Remove(KeyOf(index));
			Stop(e.Id, index);
			Debug.Log("[CUSTOM ISLANDS] Sound stopped on '" + e.HostName + "'" + (index == -2 ? " (all)" : " object " + index));
		}

		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost || msg.Ids == null || msg.Ids.Length == 0) return;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == msg.Ids[0]);
			if (e != null) Apply(e, msg.Name == "sound", msg.Data ?? "", msg.Index);
		}

		#endregion

		#region Playing

		static Vector3 PlaceOf(IslandWorldState.Entry e, int index, Spec s)
		{
			if (s.Island && Camera.main != null) return Camera.main.transform.position;
			IslandObjectRef r = index >= 0 ? Behaviours.RefsOf(e).FirstOrDefault(x => x.Index == index) : null;
			if (r != null) return r.transform.position;
			return e.Root != null ? e.Root.transform.position : e.Position;
		}

		static void Start(IslandWorldState.Entry e, int index, string arg)
		{
			Spec s = Parse(arg);
			var p = new Playing { Island = e.Id, Index = index, Arg = arg, Spec = s };
			Started++;
			LastPath = s.Path;
			try
			{
				p.I = FMODUnity.RuntimeManager.CreateInstance(s.Path);
				p.I.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(PlaceOf(e, index, s)));
				p.I.setVolume(s.Volume);
				p.I.start();
				p.Valid = true;
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Can't play '" + s.Path + "': " + ex.Message); }
			playing.Add(p);
		}

		static bool IsAlive(Playing p)
		{
			if (!p.Valid) return p.Spec.Loop; // (no FMOD here: a loop still counts as playing, a once one is over)
			try
			{
				FMOD.Studio.PLAYBACK_STATE st;
				return p.I.getPlaybackState(out st) != FMOD.RESULT.OK || st != FMOD.Studio.PLAYBACK_STATE.STOPPED;
			}
			catch { return false; }
		}

		static void Kill(Playing p)
		{
			if (!p.Valid) return;
			p.Valid = false;
			try { p.I.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); p.I.release(); } catch { }
		}

		/// <summary>Stops this machine's copy of an object's sound (index -2 = the island's all).</summary>
		static void Stop(int islandId, int index)
		{
			foreach (Playing p in playing.Where(x => x.Island == islandId && (index == -2 || x.Index == index)).ToList()) { Kill(p); playing.Remove(p); }
		}

		public static void StopAll()
		{
			foreach (Playing p in playing) Kill(p);
			playing.Clear();
		}

		/// <summary>The looped action an island's state names: found again by its hash among the island's actions (after a reload).</summary>
		static string FindArg(IslandWorldState.Entry e, int hash)
		{
			IEnumerable<Dictionary<string, string>> all = Behaviours.RefsOf(e).Select(r => r.Props).Concat(new[] { IslandCache.PropsOf(e) });
			foreach (Dictionary<string, string> props in all)
				foreach (KeyValuePair<string, string> kv in props)
				{
					if (!kv.Key.StartsWith(BehaviourProps.EventPrefix) && !kv.Key.StartsWith(BehaviourProps.ElsePrefix)) continue;
					ObjAction a = ObjAction.ParseLines(kv.Value).FirstOrDefault(x => x.Verb == "playsound" && Hash(x.Arg) == hash);
					if (a != null) return a.Arg;
				}
			return null;
		}

		/// <summary>Every frame (every machine): island-wide sounds follow the listener; twice a second the looped sounds of the
		/// islands this player is near start, and the sounds of islands left behind or unloaded stop.</summary>
		public static void Tick()
		{
			if (playing.Count == 0 && Time.unscaledTime < nextTick) return;
			if (!LoadSceneManager.IsGameSceneLoaded) { if (playing.Count > 0) StopAll(); return; }
			if (Camera.main != null)
				foreach (Playing p in playing.Where(x => x.Valid && x.Spec.Island))
					try { p.I.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(Camera.main.transform.position)); } catch { }
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + 0.5f;
			foreach (Playing p in playing.ToList())
			{
				IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == p.Island);
				bool keep = e != null && e.Root != null && Behaviours.Near(e) && IsAlive(p) && (!p.Spec.Loop || HasSound(e, KeyOf(p.Index)));
				if (!keep) { Kill(p); playing.Remove(p); }
			}
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null || !e.State.Any(IsSound) || !Behaviours.Near(e)) continue;
				foreach (KeyValuePair<int, ObjectState> kv in e.State.Where(IsSound).ToList())
				{
					int index = IndexOfKey(kv.Key);
					if (playing.Any(p => p.Island == e.Id && p.Index == index)) continue;
					string arg = FindArg(e, kv.Value.Yield);
					if (arg == null) { e.State.Remove(kv.Key); continue; } // (the island was edited: that sound is gone)
					Start(e, index, arg);
				}
			}
		}

		#endregion
	}
}

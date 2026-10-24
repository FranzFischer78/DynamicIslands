using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Storm seas" (WorldOptions.StormSeas, TODO 4e): storms come much more often, rock the raft harder, and
	/// now and then one breaks a raft floor on the raft's edge.
	///   - More storms: when Raft's weather timer runs out the host picks the next weather (WeatherManager.GetRandomWeather);
	///     with the option on, a calm pick becomes Raft's storm (rain, or rough snow in the snow region) at StormPick, and a
	///     calm spell lasts CalmFactor of its time. Raft's calm-only pool (CalmAndDefault) is left alone. Raft sends the
	///     weather to every player itself (Message_WeatherManager_SetWeather).
	///   - Harder rocking: Raft tilts the raft to the waves under it (Buoyancy.BuoyancyRotation, on every machine); in a
	///     storm the tilt is Rock times as big plus a slow sway, eased in and out over about RampSeconds, never past MaxTilt.
	///   - A broken floor: the host counts each storm that starts; storm n breaks a floor when the world's seed and n roll
	///     under BreakChance (WorldOptions.Unit, the same for a host who takes the world over). Some seconds into the storm
	///     one foundation with an open side (an edge or a corner, never one in the middle) goes the way Raft takes a
	///     destroyed block away (BlockCreator.RemoveBlockNetwork, as Block.SetHealth at 0 health): what stands on it falls,
	///     and every player's raft loses it through Raft's own message; players who join later get the raft as it is.
	///     An edge whose loss keeps the raft in one piece is preferred; a raft of MinFoundations or fewer is never broken.
	///   - Saved in the world file ("@stormseas=storms;breaks;pending"); every player gets a banner (network kind 35).
	/// </summary>
	public static class StormSeas
	{
		public const float StormPick = 0.5f, CalmFactor = 0.5f, BreakChance = 0.25f, Rock = 2.5f, MaxTilt = 12f, RampSeconds = 20f;
		public const int MinFoundations = 4;
		const int SaltBreak = 4401, SaltWhich = 4402, SaltDelay = 4403;

		/// <summary>Storms counted in this world, floors broken, and the storm whose floor is still to break (0 = none).</summary>
		public static int Storms, Breaks, Pending;
		/// <summary>Where the last floor broke (raft space), and whether it had an open side then (tests).</summary>
		public static Vector3? LastBroken;
		/// <summary>Tests: every storm breaks a floor (true), none does (false), or the seed decides (null).</summary>
		internal static bool? TestBreak;
		/// <summary>Tests: seconds into a storm before its floor breaks (null = 15-45 s from the seed).</summary>
		internal static float? TestDelay;

		static float breakAt = -1f, strength, nextCheck;
		static bool seen, wasStorm;
		static Weather lastWeather;

		public static bool On { get { return WorldOptions.On(WorldOptions.StormSeas); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [stormseas] " + msg); }

		internal static void Reset()
		{
			Storms = 0; Breaks = 0; Pending = 0; LastBroken = null; TestBreak = null; TestDelay = null;
			breakAt = -1f; strength = 0f; nextCheck = 0f; seen = false; wasStorm = false; lastWeather = null;
		}

		/// <summary>Whether storm n of this world breaks a floor (the same on every machine).</summary>
		public static bool RollsBreak(int seed, int storm)
		{
			if (TestBreak.HasValue) return TestBreak.Value;
			return WorldOptions.Unit(seed, storm, SaltBreak) < BreakChance;
		}

		public static bool IsStormType(UniqueWeatherType t) { return t == UniqueWeatherType.Rain || t == UniqueWeatherType.SnowRough; }

		static WeatherManager manager;
		static WeatherManager Manager { get { if (manager == null) manager = UnityEngine.Object.FindObjectOfType<WeatherManager>(); return manager; } }

		/// <summary>Raft's weather here is a storm (on every machine: the host's weather).</summary>
		public static bool StormNow
		{
			get
			{
				WeatherManager wm = Manager;
				try { return wm != null && wm.GetCurrentWeather() != null && IsStormType(wm.GetCurrentWeatherType()); } catch { return false; }
			}
		}

		/// <summary>How far the storm's rocking is in (0 calm, 1 full storm).</summary>
		public static float Strength { get { return strength; } }

		#region The weather

		/// <summary>Raft's weather of this kind (its Weather objects in the scene).</summary>
		internal static Weather FindWeather(UniqueWeatherType type)
		{
			foreach (Weather w in Resources.FindObjectsOfTypeAll<Weather>())
			{
				if (w == null || !w.gameObject.scene.IsValid()) continue;
				SO_Weather so = Traverse.Create(w).Field("so_weather").GetValue<SO_Weather>();
				if (so != null && so.uniqueWeatherType == type) return w;
			}
			return null;
		}

		/// <summary>Host: Raft picked its next weather: a calm one becomes the storm at StormPick.</summary>
		internal static Weather Picked(WeatherManager wm, WeatherPoolType pool, Weather picked)
		{
			if (!On || !Raft_Network.IsHost || wm == null || pool == WeatherPoolType.CalmAndDefault) return picked;
			SO_Weather so = picked != null ? Traverse.Create(picked).Field("so_weather").GetValue<SO_Weather>() : null;
			if (so != null && IsStormType(so.uniqueWeatherType)) return picked;
			if (UnityEngine.Random.value >= StormPick) return picked;
			Weather storm = FindWeather(pool == WeatherPoolType.Snow ? UniqueWeatherType.SnowRough : UniqueWeatherType.Rain);
			if (storm == null) return picked;
			Log("Raft picked " + (picked != null ? picked.name : "none") + ": a storm instead (" + storm.name + ")");
			return storm;
		}

		#endregion

		#region Every frame (WeatherManager.Update, every machine)

		internal static void Tick(WeatherManager wm)
		{
			if (!LoadSceneManager.IsGameSceneLoaded) return;
			manager = wm;
			bool storm = On && StormNow;
			// (the rocking eases in and out, so the raft never jumps)
			strength = Mathf.MoveTowards(strength, storm ? 1f : 0f, Time.deltaTime / RampSeconds);
			if (!Raft_Network.IsHost || Time.time < nextCheck) return;
			nextCheck = Time.time + 0.5f;
			if (!On) { seen = false; return; }
			Weather now = wm.GetCurrentWeather();
			if (!seen)
			{
				// (a world just loaded, or the option just on: a storm already blowing was counted before)
				seen = true; wasStorm = storm; lastWeather = now;
				if (Pending > 0) breakAt = Time.time + 20f;
				return;
			}
			if (now != lastWeather)
			{
				lastWeather = now;
				// (a calm spell is shorter: the next storm comes sooner)
				if (!storm && now != null && wm.WeatherTimer > 0f) wm.WeatherTimer *= CalmFactor;
			}
			if (storm && !wasStorm) StormStarts();
			wasStorm = storm;
			if (Pending > 0 && breakAt >= 0f && Time.time >= breakAt) BreakFloor();
		}

		static void StormStarts()
		{
			Storms++;
			bool breaks = RollsBreak(WorldOptions.Seed, Storms);
			Log("Storm " + Storms + " starts" + (breaks ? ": it will break a floor" : ""));
			if (breaks)
			{
				Pending = Storms;
				breakAt = Time.time + (TestDelay.HasValue ? TestDelay.Value : 15f + 30f * (float)WorldOptions.Unit(WorldOptions.Seed, Storms, SaltDelay));
			}
			IslandWorldState.Save();
		}

		#endregion

		#region The broken floor

		internal static List<Block> Foundations()
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			return raft != null ? HeadStart.Foundations(raft) : new List<Block>();
		}

		internal static Vector2Int CellOf(Vector3 local, Vector3 o) { return new Vector2Int(Mathf.RoundToInt((local.x - o.x) / HeadStart.Cell), Mathf.RoundToInt((local.z - o.z) / HeadStart.Cell)); }

		internal static readonly Vector2Int[] Sides = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

		/// <summary>The raft's foundations on its bottom layer, by cell.</summary>
		internal static Dictionary<Vector2Int, Block> Grid(List<Block> all)
		{
			var grid = new Dictionary<Vector2Int, Block>();
			if (all.Count == 0) return grid;
			float low = all.Min(b => b.transform.localPosition.y);
			Vector3 o = all[0].transform.localPosition;
			foreach (Block b in all.OrderBy(b => b.transform.localPosition.x).ThenBy(b => b.transform.localPosition.z))
			{
				if (b.transform.localPosition.y > low + 0.5f) continue;
				Vector2Int c = CellOf(b.transform.localPosition, o);
				if (!grid.ContainsKey(c)) grid[c] = b;
			}
			return grid;
		}

		/// <summary>The foundations that may break: those with at least one open side (edges and corners), in a fixed order.</summary>
		public static List<Block> EdgeFoundations()
		{
			var grid = Grid(Foundations());
			return grid.Where(kv => Sides.Any(s => !grid.ContainsKey(kv.Key + s))).OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).Select(kv => kv.Value).ToList();
		}

		/// <summary>A foundation with no open side (tests).</summary>
		public static List<Block> MiddleFoundations()
		{
			var grid = Grid(Foundations());
			return grid.Where(kv => Sides.All(s => grid.ContainsKey(kv.Key + s))).Select(kv => kv.Value).ToList();
		}

		/// <summary>Whether the raft's other foundations still hang together without this cell.</summary>
		internal static bool KeepsTogether(Dictionary<Vector2Int, Block> grid, Vector2Int gone)
		{
			var rest = new HashSet<Vector2Int>(grid.Keys.Where(c => c != gone));
			if (rest.Count == 0) return true;
			var seenCells = new HashSet<Vector2Int>();
			var todo = new Queue<Vector2Int>();
			Vector2Int first = rest.First();
			todo.Enqueue(first); seenCells.Add(first);
			while (todo.Count > 0)
			{
				Vector2Int c = todo.Dequeue();
				foreach (Vector2Int s in Sides) { Vector2Int n = c + s; if (rest.Contains(n) && seenCells.Add(n)) todo.Enqueue(n); }
			}
			return seenCells.Count == rest.Count;
		}

		/// <summary>Host: the storm's floor breaks now. Returns what happened.</summary>
		public static string BreakFloor()
		{
			int storm = Pending;
			Pending = 0;
			breakAt = -1f;
			if (!Raft_Network.IsHost) return "only the host breaks a floor";
			var grid = Grid(Foundations());
			if (grid.Count <= MinFoundations) { Log("Storm " + storm + ": the raft is too small to lose a floor (" + grid.Count + ")"); IslandWorldState.Save(); return "raft too small"; }
			var edges = grid.Where(kv => Sides.Any(s => !grid.ContainsKey(kv.Key + s))).OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).ToList();
			var safe = edges.Where(kv => KeepsTogether(grid, kv.Key)).ToList();
			var from = safe.Count > 0 ? safe : edges;
			if (from.Count == 0) { IslandWorldState.Save(); return "no edge foundation"; }
			int i = (int)((uint)WorldOptions.Mix(WorldOptions.Seed, storm, SaltWhich) % (uint)from.Count);
			Block b = from[i].Value;
			Vector3 at = b.transform.localPosition;
			string what = b.buildableItem != null ? b.buildableItem.UniqueName : b.name;
			try { BlockCreator.RemoveBlockNetwork(b, null, true); }
			catch (Exception e) { Log("Breaking " + what + " failed: " + e.Message); IslandWorldState.Save(); return "failed: " + e.Message; }
			Breaks++;
			LastBroken = at;
			Log("Storm " + storm + " broke a floor: " + what + " at cell " + from[i].Key + " (" + at.ToString("F1") + "), " + edges.Count + " on the edge, " + grid.Count + " in all");
			IslandWorldState.Save();
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.StormSeas, Name = "broke", Index = storm, Count = Breaks });
			Banner();
			return "broke " + what + " at " + at.ToString("F1");
		}

		static void Banner()
		{
			IslandInfo.Show("The storm broke the raft", "", "A wave tore a floor off the raft's edge - what stood on it went with it. Mend the raft before the next storm.");
		}

		#endregion

		#region Network (kind 35), save

		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			Breaks = msg.Count;
			Log("The host's storm " + msg.Index + " broke a floor");
			if (msg.Name == "broke") Banner();
		}

		internal static bool ReadLine(string key, string value)
		{
			if (key != "stormseas") return false;
			string[] p = (value ?? "").Split(';');
			int v;
			Storms = p.Length > 0 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
			Breaks = p.Length > 1 && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
			Pending = p.Length > 2 && int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
			seen = false;
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Storms == 0 && Breaks == 0 && Pending == 0) yield break;
			yield return "@stormseas=" + Storms.ToString(CultureInfo.InvariantCulture) + ";" + Breaks.ToString(CultureInfo.InvariantCulture) + ";" + Pending.ToString(CultureInfo.InvariantCulture);
		}

		#endregion

		/// <summary>The raft's tilt to the waves (euler angles), rocked harder by the storm's strength.</summary>
		public static Vector3 Rocked(Vector3 euler, float s, float time)
		{
			if (s <= 0f) return euler;
			float k = 1f + (Rock - 1f) * s;
			float x = Mathf.DeltaAngle(0f, euler.x) * k + s * 1.5f * Mathf.Sin(time * 2f * Mathf.PI / 6.5f);
			float z = Mathf.DeltaAngle(0f, euler.z) * k + s * 1.2f * Mathf.Sin(time * 2f * Mathf.PI / 9.1f + 1f);
			return new Vector3(Mathf.Clamp(x, -MaxTilt, MaxTilt), euler.y, Mathf.Clamp(z, -MaxTilt, MaxTilt));
		}

		[ConsoleCommand(name: "StormSeas", docs: "The world option Storm seas: StormSeas = the storm now, storms counted, floors broken, and the edge foundations that may break; StormSeas break = (host) the next storm's floor breaks now (tests)")]
		public static void StormSeasCommand(string[] args)
		{
			if (args != null && args.Length > 0 && args[0] == "break")
			{
				if (!Raft_Network.IsHost) { Debug.Log("[CUSTOM ISLANDS] Only the host breaks a floor"); return; }
				Pending = Math.Max(1, Storms);
				Debug.Log("[CUSTOM ISLANDS] Storm seas: " + BreakFloor());
				return;
			}
			WeatherManager wm = Manager;
			Debug.Log("[CUSTOM ISLANDS] Storm seas: " + (On ? "on" : "off") + ", " + (StormNow ? "a storm now" : "calm") + " (rocking " + strength.ToString("0.00") + ", weather " + (wm != null ? wm.GetCurrentWeatherType() + ", next change in " + Mathf.RoundToInt(wm.WeatherTimer) + " s" : "none") +
				"), storms " + Storms + ", floors broken " + Breaks + (Pending > 0 ? ", storm " + Pending + "'s floor still to break" : "") + ", edge foundations " + EdgeFoundations().Count + " of " + Foundations().Count);
		}
	}

	/// <summary>Every frame on every machine: the storm's rocking, and (host) the storms counted and their floors.</summary>
	[HarmonyPatch(typeof(WeatherManager), "Update")]
	static class StormSeasWeatherUpdate
	{
		static void Postfix(WeatherManager __instance)
		{
			try { StormSeas.Tick(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [stormseas] " + e.Message); }
		}
	}

	/// <summary>Host: Raft picks its next weather: more storms with Storm seas on.</summary>
	[HarmonyPatch(typeof(WeatherManager), "GetRandomWeather")]
	static class StormSeasWeatherPick
	{
		static void Postfix(WeatherManager __instance, WeatherPoolType poolType, ref Weather __result)
		{
			try { __result = StormSeas.Picked(__instance, poolType, __result); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [stormseas] " + e.Message); }
		}
	}

	/// <summary>Raft tilts the raft to the waves: harder in a storm with Storm seas on.</summary>
	[HarmonyPatch(typeof(Buoyancy), "BuoyancyRotation", MethodType.Getter)]
	static class StormSeasRocking
	{
		static void Postfix(ref Vector3 __result)
		{
			float s = StormSeas.Strength;
			if (s <= 0f) return;
			__result = StormSeas.Rocked(__result, s, Time.time);
		}
	}
}

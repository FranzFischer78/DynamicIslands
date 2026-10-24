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
	/// The world option "Rogue shark" (WorldOptions.RogueShark): now and then (a roll per in-game day from the world's seed,
	/// not before day 3) a second shark comes beside Bruce - rust-red. It doesn't leave: it stays until it is killed, and once
	/// killed no other comes for several days (5-8). While it lives, Bruce comes back as usual when killed (Raft only sends a
	/// new shark when the dying one was the last: the rogue isn't counted); the rogue's own body never brings one.
	///   - The host rolls and spawns it with Raft's own CreateAINetworkBehaviour (the other players get it as any shark) and
	///     sends which one it is (network kind 27: Index = its object index, 0 = none; Count = the day before which none comes).
	///   - Raft saves it as one of its sharks; the world file keeps which one ("@rogueshark=index;quietuntil;giant"). If it
	///     can't be found after a load, another shark beyond the first is taken for it, or a new one comes.
	///   - Now and then (GiantChance of the rogues, rolled from the seed and the day) it is a giant: twice as big (its model,
	///     on every machine; message Data "giant"), with more health and harder bites on players (not on the raft).
	/// </summary>
	public static class RogueShark
	{
		public const float Chance = 0.12f;
		public const int FirstDay = 3, QuietMin = 5, QuietMax = 8;
		/// <summary>The share of rogues that are giants, how much bigger, and their health and bite (on players) multipliers.</summary>
		public const float GiantChance = 0.2f, GiantScale = 2f, GiantHealth = 3f, GiantDamage = 1.5f;
		public static readonly Color Colour = new Color(1f, 0.5f, 0.38f);

		/// <summary>The living rogue's object index (0 = none).</summary>
		public static uint Index;
		/// <summary>No new rogue before this day (after a kill).</summary>
		public static int QuietUntil;
		/// <summary>Whether the living rogue is a giant.</summary>
		public static bool Giant;
		// (the body whose stats were set - host - or matched to the host's - another player)
		static int statsId;
		// (each grown model's own scale, so growing it again doesn't grow it twice)
		static readonly Dictionary<int, Vector3> baseScales = new Dictionary<int, Vector3>();
		// (the last one killed: its body still decays a while after the kill is noticed)
		static uint deadIndex;
		static int rolledDay = -1, tintedId;
		static float nextCheck, missingSince;

		/// <summary>Tests: the day in place of Raft's.</summary>
		internal static int? TestDay;

		public static bool On { get { return WorldOptions.On(WorldOptions.RogueShark); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [rogue] " + msg); }

		public static int Today
		{
			get
			{
				if (TestDay.HasValue) return TestDay.Value;
				try { return WorldManager.DayCounter; } catch { return 0; }
			}
		}

		internal static void Reset() { Index = 0; deadIndex = 0; QuietUntil = 0; Giant = false; rolledDay = -1; tintedId = 0; statsId = 0; nextCheck = 0f; missingSince = 0f; TestDay = null; }

		/// <summary>Whether a rogue comes on this day (the same on every machine and every time).</summary>
		public static bool Rolls(int seed, int day)
		{
			if (day < FirstDay) return false;
			return WorldOptions.Unit(seed, day, 41) < Chance;
		}

		/// <summary>Whether the rogue that comes on this day is a giant (the same on every machine and every time).</summary>
		public static bool RollsGiant(int seed, int day) { return WorldOptions.Unit(seed, day, 43) < GiantChance; }

		/// <summary>How many days none comes after one killed on this day.</summary>
		public static int QuietDays(int seed, int day)
		{
			return QuietMin + new System.Random(WorldOptions.Mix(seed, day, 5)).Next(QuietMax - QuietMin + 1);
		}

		public static bool IsRogueIndex(uint objectIndex) { return objectIndex != 0 && (objectIndex == Index || objectIndex == deadIndex); }
		public static bool IsRogue(AI_NetworkBehaviour ai) { return ai != null && IsRogueIndex(ai.ObjectIndex); }

		static bool Dead(AI_NetworkBehaviour ai) { return ai == null || (ai.networkEntity != null && ai.networkEntity.IsDead); }

		public static AI_NetworkBehavior_Shark Find()
		{
			if (Index == 0) return null;
			return UnityEngine.Object.FindObjectsOfType<AI_NetworkBehavior_Shark>().FirstOrDefault(s => s != null && s.ObjectIndex == Index);
		}

		public static bool Alive { get { AI_NetworkBehavior_Shark s = Find(); return s != null && !Dead(s); } }

		public static void Tick()
		{
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 1f;
			if (!LoadSceneManager.IsGameSceneLoaded) return;
			AI_NetworkBehavior_Shark rogue = Find();
			// (every machine: its colour, again for a new body - a load, a player who joins)
			if (rogue != null && !Dead(rogue) && rogue.GetInstanceID() != tintedId) Tint(rogue);
			if (!Raft_Network.IsHost) return;
			if (Index != 0)
			{
				if (rogue != null) { missingSince = 0f; if (Dead(rogue)) Killed(); return; }
				// Not found: after a load Raft's sharks come a moment later; then another shark is taken for it, or a new one comes
				if (missingSince <= 0f) { missingSince = Time.unscaledTime; return; }
				if (Time.unscaledTime - missingSince < 15f) return;
				missingSince = 0f;
				// (switched off meanwhile: let it go, no new one)
				if (!On) { Index = 0; tintedId = 0; Log("The rogue wasn't found and Rogue shark is off: dropped"); Changed("state"); return; }
				List<AI_NetworkBehavior_Shark> sharks = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehavior_Shark>().Where(s => s != null && !Dead(s)).OrderBy(s => s.ObjectIndex).ToList();
				if (sharks.Count >= 2) { Index = sharks.Last().ObjectIndex; tintedId = 0; Log("Shark #" + Index + " taken for the rogue (its own wasn't found)"); Changed("state"); }
				else { Log("The rogue wasn't found: a new one"); Index = 0; Spawn(Giant); }
				return;
			}
			if (!On) return;
			int day = Today;
			if (day == rolledDay) return;
			rolledDay = day;
			if (day >= QuietUntil && Rolls(WorldOptions.Seed, day)) Spawn(RollsGiant(WorldOptions.Seed, day));
		}

		/// <summary>Host: a rogue now, a little way off the raft (a giant one if giant).</summary>
		internal static AI_NetworkBehavior_Shark Spawn(bool giant = false)
		{
			if (!Raft_Network.IsHost) return null;
			Network_Host_Entities host = null;
			try { host = ComponentManager<Network_Host_Entities>.Value; } catch { }
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (host == null || !raft.HasValue) return null;
			Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
			if (dir == Vector2.zero) dir = Vector2.up;
			Vector3 pos = new Vector3(raft.Value.x + dir.x * 45f, -10f, raft.Value.z + dir.y * 45f);
			AI_NetworkBehavior_Shark shark = host.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, pos, null) as AI_NetworkBehavior_Shark;
			if (shark == null) { Log("Raft made no shark"); return null; }
			Index = shark.ObjectIndex;
			Giant = giant;
			tintedId = 0;
			Tint(shark);
			Log("A " + (giant ? "giant " : "") + "rogue shark #" + Index + " on day " + Today);
			Changed("spawn");
			return shark;
		}

		static void Killed()
		{
			Log("The " + (Giant ? "giant " : "") + "rogue #" + Index + " killed on day " + Today);
			deadIndex = Index;
			Index = 0;
			Giant = false;
			QuietUntil = Today + QuietDays(WorldOptions.Seed, Today);
			Changed("dead");
		}

		static void Changed(string what)
		{
			IslandNetwork.SendToEveryone(Message(what));
			IslandWorldState.Save();
			Banner(what);
		}

		static void Banner(string what)
		{
			if (what == "spawn" && Giant) IslandInfo.Show("A giant rogue shark", "", "A second shark, rust-red and twice Bruce's size, has found the raft. It bites hard, takes a beating, and won't leave until it's killed.");
			else if (what == "spawn") IslandInfo.Show("A rogue shark", "", "A second shark, rust-red, has found the raft. It won't leave on its own - only when it's killed.");
			else if (what == "dead") IslandInfo.Show("The rogue shark is dead", "", "No other will come for a few days.");
		}

		static void Tint(AI_NetworkBehaviour ai)
		{
			int n = 0;
			foreach (Renderer r in WorldRandomizer.BodyOf(ai)) n += WorldRandomizer.TintTextures(r, Colour);
			tintedId = ai.GetInstanceID();
			Log("Shark #" + ai.ObjectIndex + " rust-red (" + n + " texture(s))");
			if (Giant) Grow(ai);
		}

		/// <summary>
		/// A giant: its model twice as big - the transform above the shark's renderer, up to (not) the shark's own object
		/// (the shark's body may live apart from it, WorldRandomizer.BodyOf) - and its stats once per body: set by the host,
		/// matched by another player (CreatureSpawner.MatchHealth). Its bites on raft blocks stay Raft's.
		/// </summary>
		static void Grow(AI_NetworkBehaviour ai)
		{
			Renderer body = WorldRandomizer.BodyOf(ai).FirstOrDefault();
			Transform model = body != null ? ModelOf(body.transform, ai.transform) : null;
			if (model != null)
			{
				Vector3 before = body.bounds.size, own;
				int id = model.GetInstanceID();
				if (!baseScales.TryGetValue(id, out own)) baseScales[id] = own = model.localScale;
				model.localScale = own * GiantScale;
				Log("Giant: '" + model.name + "' x" + GiantScale + " (body " + before.ToString("F1") + " -> " + body.bounds.size.ToString("F1") + ")");
			}
			else Log("Giant: no model found to grow");
			if (statsId == ai.GetInstanceID()) return;
			statsId = ai.GetInstanceID();
			if (Raft_Network.IsHost)
				CreatureSpawner.ApplyStats(ai, new Dictionary<string, string>
				{
					{ ObjectProps.CreatureHealth, GiantHealth.ToString(CultureInfo.InvariantCulture) },
					{ ObjectProps.CreatureDamage, GiantDamage.ToString(CultureInfo.InvariantCulture) },
				}, false);
			else CreatureSpawner.MatchHealth(ai, GiantHealth);
		}

		/// <summary>The shark's model: the highest transform above its renderer that is still below the shark's own object
		/// (or, when the renderer isn't under it, below the scene's root).</summary>
		static Transform ModelOf(Transform renderer, Transform shark)
		{
			Transform t = renderer;
			while (t.parent != null && t.parent != shark && t.parent.parent != null) t = t.parent;
			return t;
		}

		/// <summary>The rogue's colour on this body (tests: the textures it has now).</summary>
		internal static bool LooksRogue(AI_NetworkBehaviour ai)
		{
			foreach (Renderer r in WorldRandomizer.BodyOf(ai))
			{
				var block = new MaterialPropertyBlock();
				r.GetPropertyBlock(block);
				Texture t = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Diffuse") ? block.GetTexture("_Diffuse") : block.GetTexture("_MainTex");
				if (t != null && t.name.StartsWith("CI_Tinted_")) return true;
			}
			return false;
		}

		#region Network (kind 27), save

		internal static IslandNetMessage Message(string what)
		{
			return new IslandNetMessage { Kind = IslandNetMessage.RogueShark, Name = what, Index = unchecked((int)Index), Count = QuietUntil, Data = Giant ? "giant" : "" };
		}

		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			uint was = Index;
			Index = unchecked((uint)msg.Index);
			QuietUntil = msg.Count;
			Giant = msg.Data == "giant";
			if (was != 0 && Index != was) deadIndex = was;
			tintedId = 0;
			nextCheck = 0f;
			Log("The host's rogue: " + (Index == 0 ? "none" : (Giant ? "giant " : "") + "#" + Index) + ", none before day " + QuietUntil);
			if (msg.Name != "state") Banner(msg.Name);
		}

		internal static bool ReadLine(string key, string value)
		{
			if (key != "rogueshark") return false;
			string[] p = (value ?? "").Split(';');
			uint i; int q;
			Index = p.Length > 0 && uint.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : 0;
			QuietUntil = p.Length > 1 && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out q) ? q : 0;
			Giant = Index != 0 && p.Length > 2 && p[2] == "1";
			tintedId = 0;
			statsId = 0;
			missingSince = 0f;
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Index != 0 || QuietUntil > 0) yield return "@rogueshark=" + Index.ToString(CultureInfo.InvariantCulture) + ";" + QuietUntil.ToString(CultureInfo.InvariantCulture) + (Giant ? ";1" : "");
		}

		#endregion

		[ConsoleCommand(name: "RogueShark", docs: "The world option Rogue shark: RogueShark = where it is; RogueShark spawn = one now (host); RogueShark giant = a giant one now (host); RogueShark kill = kill it (host, tests)")]
		public static void RogueSharkCommand(string[] args)
		{
			string a = args != null && args.Length > 0 ? args[0] : "";
			if ((a == "spawn" || a == "giant") && Raft_Network.IsHost && Index == 0) Spawn(a == "giant");
			AI_NetworkBehavior_Shark s = Find();
			if (a == "kill" && Raft_Network.IsHost && s != null && s.networkEntity != null) s.networkEntity.Damage(100000f, s.transform.position, Vector3.up, EntityType.Player, true);
			int sharks = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehavior_Shark>().Count(x => x != null && !Dead(x));
			Debug.Log("[CUSTOM ISLANDS] Rogue shark: " + (On ? "on" : "off") + ", " + (Index == 0 ? "none" : (Giant ? "giant " : "") + "#" + Index + (s == null ? " (not here)" : Dead(s) ? " (dead)" : LooksRogue(s) ? " (rust-red)" : " (not coloured yet)")) +
				", none before day " + QuietUntil + " (today " + Today + "), " + sharks + " shark(s) alive");
		}
	}

	/// <summary>
	/// Rogue shark: a dead shark's body decays and Raft sends a new shark only if it was the last one (Network_Host_Entities.SharkCount
	/// 1). The rogue's body brings none; Bruce's does while the rogue lives, as if it weren't there.
	/// </summary>
	[HarmonyPatch(typeof(AI_State_Decay_Shark), "CheckToSpawnOnDecay")]
	static class RogueSharkDecay
	{
		static bool Prefix(AI_State_Decay_Shark __instance, ref bool __result, out int __state)
		{
			__state = 0;
			AI_NetworkBehaviour ai = __instance.GetComponentInParent<AI_NetworkBehaviour>();
			if (RogueShark.IsRogue(ai)) { __result = true; Debug.Log("[CUSTOM ISLANDS] [rogue] Its body decays: no shark comes for it"); return false; }
			if (RogueShark.Index == 0 || !RogueShark.Alive) return true;
			Network_Host_Entities host = null;
			try { host = ComponentManager<Network_Host_Entities>.Value; } catch { }
			if (host != null && host.SharkCount > 1) { host.SharkCount--; __state = 1; Debug.Log("[CUSTOM ISLANDS] [rogue] A shark's body decays while the rogue lives: Raft counts " + host.SharkCount + " shark(s)"); }
			else Debug.Log("[CUSTOM ISLANDS] [rogue] A shark's body decays while the rogue lives: Raft counts " + (host == null ? "? (no host)" : host.SharkCount + " shark(s)") + ", left as it is");
			return true;
		}

		static void Postfix(int __state)
		{
			if (__state == 0) return;
			try { ComponentManager<Network_Host_Entities>.Value.SharkCount += __state; } catch { }
		}
	}
}

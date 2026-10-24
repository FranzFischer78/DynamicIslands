using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// How tough the monsters of a world are, chosen in Raft's New Game box (a slider under Raft's own game mode tabs):
	/// Timid, Normal, Fierce, Savage or Nightmare - x0.75, x1, x1.25, x1.5, x2 health and damage.
	/// Every hit in Raft goes through Network_Host.DamageEntity on the machine where it happens (a monster's bite on the
	/// host, a player's spear on that player's machine), which sends the final damage to everyone: a prefix there
	/// multiplies the damage monsters deal to players by the factor and divides the damage monsters take by it (the
	/// same as health x factor, and it needs no change to the animals themselves, their saves or their health bars).
	/// On top of Raft's game mode (Easy / Normal / Hard) and of the health and damage an island's builder gave its animals.
	/// Left as Raft has them (the user's choice): puffer fish damage (explosion, cloud, poison) and everything about Bruce
	/// and the raft (his bites on its blocks, how often he comes for it, how soon a killed shark comes back).
	/// Saved per world ("@monsters=savage" in the world file) and sent to every player who joins, with the build cost
	/// (WorldRules, network kind 15).
	/// </summary>
	public static class MonsterDifficulty
	{
		public const int Timid = 0, Normal = 1, Fierce = 2, Savage = 3, Nightmare = 4;
		public static readonly string[] Names = { "Timid", "Normal", "Fierce", "Savage", "Nightmare" };
		public static readonly float[] Factors = { 0.75f, 1f, 1.25f, 1.5f, 2f };
		/// <summary>What each level does, shown under the slider.</summary>
		public static readonly string[] Descriptions =
		{
			"Monsters have 25% less health and deal 25% less damage. A gentler trip across the sea.",
			"Monsters are as tough as Raft made them.",
			"Monsters have 25% more health and deal 25% more damage.",
			"Monsters have 50% more health and deal 50% more damage. Keep your spear close.",
			"Monsters have twice the health and deal twice the damage. For seasoned survivors."
		};
		/// <summary>The words the level was first asked for with (Easy, Moderate, Hard, Impossible) work too.</summary>
		static readonly string[][] Aliases = { new[] { "easy" }, new string[0], new[] { "moderate" }, new[] { "hard" }, new[] { "impossible" } };

		/// <summary>Hits this big are meant to kill (Raft's and the tests' "defeat it" values): never changed.</summary>
		public const float KillDamage = 9999f;

		/// <summary>The current world's level (clients get the host's).</summary>
		public static int Current = Normal;
		/// <summary>Chosen in the New Game box for the world being created (null = the last choice, Default).</summary>
		public static int? Pending;

		/// <summary>The last hit that was changed (the tests read it).</summary>
		public static float LastIn, LastOut;
		public static string LastTarget = "";
		public static int ChangedHits;

		static readonly Dictionary<int, bool> monsterCache = new Dictionary<int, bool>();

		public static float Factor { get { return Factors[Clamp(Current)]; } }
		public static string Name(int level) { return Names[Clamp(level)]; }
		public static int Clamp(int level) { return Mathf.Clamp(level, 0, Names.Length - 1); }
		public static string FactorText(int level) { return "\u00D7" + Factors[Clamp(level)].ToString("0.##", CultureInfo.InvariantCulture); }
		public static string Describe(int level) { return Name(level) + " (" + FactorText(level) + " health and damage)"; }

		/// <summary>A level by name (any case, also Easy / Moderate / Hard / Impossible) or number 0-4; -1 if unknown.</summary>
		public static int Parse(string text)
		{
			string t = (text ?? "").Trim().ToLowerInvariant();
			for (int i = 0; i < Names.Length; i++)
				if (Names[i].ToLowerInvariant() == t || Aliases[i].Contains(t)) return i;
			int n;
			return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 0 && n < Names.Length ? n : -1;
		}

		/// <summary>The "?" next to the slider.</summary>
		public static string HelpText
		{
			get
			{
				return "How tough the monsters of this world are: Bruce and the other sharks, warthogs, bears, screechers, puffer fish, " +
					"rats, hyenas, bees, the bosses, and the animals of custom islands.\n" +
					string.Join("\n", Enumerable.Range(0, Names.Length).Select(i => Names[i] + " (" + FactorText(i) + "): " + Short(i)).ToArray()) +
					"\nBruce and your raft stay as in Raft: his bites on it, how often he comes for it and how soon he is back after you kill him. " +
					"Puffer fish: only their health changes, their explosion and poison hurt as in Raft.\n" +
					"It comes on top of the game mode: Easy's monsters hit softer, Hard's harder (and in Hard your hits do 20% less). " +
					"In Peaceful monsters leave you alone (only Raft's Varuna Point and Utopia bosses still fight), so mostly their health changes. " +
					"In Creative nothing can hurt you, and no screechers or puffer fish come (a quest step that needs them can't be done there). " +
					"In every mode your hits count, so kill quests and lairs can be done.\n" +
					"Tame animals (llamas, goats, chickens) and seagulls don't change. " +
					"The host can change it later with the console command Monsters (F10).";
			}
		}

		static string Short(int level)
		{
			switch (level)
			{
				case Timid: return "25% less health and damage.";
				case Normal: return "as in Raft.";
				case Fierce: return "25% more health and damage.";
				case Savage: return "50% more health and damage.";
				default: return "twice the health and damage.";
			}
		}

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [monsters] " + msg); }

		#region The last choice (for the New Game box)

		/// <summary>The level last chosen in the New Game box (Normal until one was; kept in WorldRules' file).</summary>
		public static int Default
		{
			get { int l = Parse(WorldRules.ReadDefault("monsters") ?? ""); return l >= 0 ? l : Normal; }
		}

		public static void SaveDefault(int level) { WorldRules.SaveDefault("monsters", Name(level).ToLowerInvariant()); }

		#endregion

		#region The world file

		/// <summary>Before a world's island list is read (WorldRules): a brand-new world (host) gets the level chosen for it, any other Normal until its line is read.</summary>
		internal static void Reset(bool newWorld)
		{
			monsterCache.Clear();
			if (newWorld)
			{
				Current = Clamp(Pending ?? Default);
				Pending = null;
				Log("New world '" + SaveAndLoad.CurrentGameFileName + "': monsters " + Describe(Current));
				if (Current != Normal) DynamicIslands.Notify("Monsters in this world: " + Describe(Current));
			}
			else Current = Normal;
		}

		/// <summary>A "@key=value" line of the world file; false if it isn't ours.</summary>
		internal static bool ReadLine(string key, string value)
		{
			if (!key.Equals("monsters", StringComparison.OrdinalIgnoreCase)) return false;
			int l = Parse(value);
			if (l < 0) { Debug.LogWarning("[CUSTOM ISLANDS] Unknown monster difficulty '" + value + "' in the world file: Normal"); l = Normal; }
			Current = l;
			Log("This world's monsters: " + Describe(Current));
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Current != Normal) yield return "@monsters=" + Name(Current).ToLowerInvariant();
		}

		/// <summary>True when the world file is needed for the monster difficulty alone.</summary>
		internal static bool HasState { get { return Current != Normal; } }

		/// <summary>Host: changes the current world's level (the Monsters command, tests) and tells everyone.</summary>
		public static void Set(int level)
		{
			Current = Clamp(level);
			Log("Monsters in this world: " + Describe(Current));
			WorldRules.Broadcast();
			// (saved with the world at once, like the other settings: the copy players keep for host swap follows)
			IslandWorldState.Save();
		}

		#endregion

		#region Network

		/// <summary>Client: the host's level (WorldRules' message).</summary>
		internal static void FromHost(int level)
		{
			int before = Current;
			Current = Clamp(level);
			monsterCache.Clear();
			Log("The host's monsters: " + Describe(Current));
			if (Current != before && Current != Normal) DynamicIslands.Notify("Monsters in this world: " + Describe(Current));
		}

		/// <summary>Client: a host's world arrived; Normal until that host says otherwise.</summary>
		internal static void OnWorldReceived()
		{
			Current = Normal;
			monsterCache.Clear();
		}

		#endregion

		#region Damage

		/// <summary>The animals that fight players - the same ones that give EXP (LevelRules.IsMonster: one list, the two
		/// had drifted apart - pigs and roaches gave EXP but weren't scaled, Utopia's harmless butler bots the reverse).
		/// The ones to catch, the sea life that only swims by and people aren't monsters.</summary>
		public static bool IsMonsterType(AI_NetworkBehaviourType t) { return LevelRules.IsMonster(t); }

		/// <summary>An animal of one of the monster kinds, or one of Raft's other enemies (its bosses without an animal brain); not seagulls.</summary>
		public static bool IsMonster(Network_Entity e)
		{
			if (e == null || e.entityType == EntityType.Player) return false;
			int id = e.GetInstanceID();
			bool m;
			if (monsterCache.TryGetValue(id, out m)) return m;
			AI_NetworkBehaviour ai = e.GetComponentInParent<AI_NetworkBehaviour>();
			if (ai == null) ai = e.GetComponentInChildren<AI_NetworkBehaviour>();
			if (ai != null) m = IsMonsterType(ai.behaviourType);
			else m = e.entityType == EntityType.Enemy && e.GetComponentInParent<Seagull>() == null;
			if (monsterCache.Count > 4000) monsterCache.Clear();
			monsterCache[id] = m;
			return m;
		}

		/// <summary>
		/// Set while a puffer fish explodes or its cloud hurts (PufferFishDamagePatch): their damage stays Raft's. The
		/// poison it leaves hits with no inflictor, which is never changed either.
		/// </summary>
		internal static bool PufferFishHurting;

		/// <summary>
		/// The damage a hit really does in this world: a monster's hit on a player times the factor (not a puffer fish's),
		/// any hit on a monster divided by it. Other hits (players on players, falling, fire, poison, the kill values) stay
		/// as they are.
		/// </summary>
		public static float Scale(Network_Entity target, float damage, EntityType inflictor)
		{
			// (with Night is dangerous on top: NightDanger)
			float f = Factor * NightDanger.MonsterFactor, result;
			if (Mathf.Approximately(f, 1f) || target == null || damage <= 0f || damage >= KillDamage) return damage;
			if (target.entityType == EntityType.Player)
			{
				if (inflictor != EntityType.Enemy || PufferFishHurting) return damage;
				result = damage * f;
			}
			else if (IsMonster(target)) result = damage / f;
			else return damage;
			LastIn = damage;
			LastOut = result;
			LastTarget = target.name;
			ChangedHits++;
			return result;
		}

		#endregion

		[ConsoleCommand(name: "Monsters", docs: "Monster difficulty: Monsters = this world's; Monsters timid|normal|fierce|savage|nightmare = change it for this world (host), or at the main menu for the next new world. Health and damage of hostile animals x0.75 / x1 / x1.25 / x1.5 / x2")]
		public static void MonstersCommand(string[] args)
		{
			string arg = args != null ? string.Join(" ", args).Trim() : "";
			bool inGame = LoadSceneManager.IsGameSceneLoaded;
			if (arg.Length > 0)
			{
				int l = Parse(arg);
				if (l < 0) { DynamicIslands.Notify("Monsters: unknown '" + arg + "'. Use " + string.Join(", ", Names.Select(n => n.ToLowerInvariant()).ToArray()), true); return; }
				if (!inGame)
				{
					NewWorldRulesBox.MonsterLevel = l;
					SaveDefault(l);
					DynamicIslands.Notify("Monsters in the next new world: " + Describe(l));
					return;
				}
				if (!Raft_Network.IsHost) { DynamicIslands.Notify("Only the host can change the monsters", true); return; }
				Set(l);
				DynamicIslands.Notify("Monsters in this world: " + Describe(Current));
				return;
			}
			if (inGame) Log("Monsters in this world: " + Describe(Current) + "; " + ChangedHits + " hit(s) changed so far");
			else Log("Monsters in the next new world: " + Describe(NewWorldRulesBox.MonsterLevel));
		}
	}

	/// <summary>Every hit in Raft (a bite, a charge, a spear, an arrow) comes through here on the machine where it happens.</summary>
	[HarmonyPatch(typeof(Network_Host), "DamageEntity")]
	static class MonsterDamagePatch
	{
		static bool warned;

		static void Prefix(Network_Entity entity, ref float damage, EntityType damageInflictorEntityType)
		{
			try { damage = MonsterDifficulty.Scale(entity, damage, damageInflictorEntityType); }
			catch (Exception e) { if (!warned) { warned = true; Debug.LogWarning("[CUSTOM ISLANDS] [monsters] Changing a hit failed: " + e); } }
		}
	}

	/// <summary>A puffer fish's explosion and its poison cloud (host): their hits on players stay Raft's at every level.</summary>
	[HarmonyPatch]
	static class PufferFishDamagePatch
	{
		static IEnumerable<System.Reflection.MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(AI_State_PufferFish_Explode), "DamageNearbyPlayers");
			yield return AccessTools.Method(typeof(PufferFishParticleCloud), "Tick");
		}

		static void Prefix() { MonsterDifficulty.PufferFishHurting = true; }

		static Exception Finalizer(Exception __exception) { MonsterDifficulty.PufferFishHurting = false; return __exception; }
	}
}

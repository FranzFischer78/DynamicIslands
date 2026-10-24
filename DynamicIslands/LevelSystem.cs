using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The level up system's numbers: what a level costs, what a monster is worth, and the nine stats a player puts
	/// points into. Each point is +1% (Hunger, Thirst and Oxygen: the stat lasts 1% longer), at most 15 points per
	/// stat, 2 points per level. Once every stat is full (135 points, level 69) players still level up, without points.
	/// </summary>
	public static class LevelRules
	{
		public const int Walk = 0, Run = 1, Swim = 2, Jump = 3, Damage = 4, Health = 5, Hunger = 6, Thirst = 7, Oxygen = 8, StatCount = 9;
		public const int MaxPoints = 15, PointsPerLevel = 2; // (15 since 2026-10-06, the user's wish; was 10)
		public const float PerPoint = 0.01f;

		/// <summary>Every point there is (all stats full).</summary>
		public const int AllPoints = StatCount * MaxPoints;

		public static readonly string[] StatNames = { "Walk speed", "Run speed", "Swim speed", "Jump height", "Damage", "Health", "Hunger", "Thirst", "Oxygen" };
		public static readonly string[] StatHints =
		{
			"How fast you walk on land",
			"How fast you run (sprint) on land",
			"How fast you swim, on the surface and diving",
			"How high you jump (on land and out of the water)",
			"The damage you do to monsters with every weapon",
			"Your maximum health",
			"Hunger drains slower: you can go longer without eating",
			"Thirst drains slower: you can go longer without drinking",
			"You use your breath slower: you last longer under water",
		};

		/// <summary>Stat points a player has had at this level: 2 a level, until every stat could be full.</summary>
		public static int PointsAt(int level) { return Mathf.Min(AllPoints, Mathf.Max(0, level - 1) * PointsPerLevel); }

		/// <summary>EXP of the monster every level is measured in: Bruce, Raft's shark (before GainMultiplier).</summary>
		public const int ReferenceXp = 20;

		/// <summary>EXP gained is nine times what the levels are measured in (the user: doubled 2026-10-02, tripled again
		/// 2026-10-04, +50 % 2026-10-09) - Bruce gives 180 - so a level takes a ninth of the kills; the EXP each level needs
		/// stays as it was (100, 200, 400...).</summary>
		public const float GainMultiplier = 9f;

		/// <summary>What Bruce gives (ReferenceXp x GainMultiplier).</summary>
		public static int BruceXp { get { return Mathf.RoundToInt(ReferenceXp * GainMultiplier); } }

		/// <summary>Monster kills worth ReferenceXp each from this level to the next: 5, 10, 20, 30, 40, ... (a sixth as many of
		/// Bruce, GainMultiplier).</summary>
		public static int KillsFor(int level) { return level <= 1 ? 5 : 10 * (level - 1); }

		/// <summary>EXP from this level to the next: 100, 200, 400, 600, 800, ...</summary>
		public static int XpFor(int level) { return ReferenceXp * KillsFor(level); }

		/// <summary>Total EXP at which this level starts (level 1 at 0, level 2 at 100, level 3 at 300, level 4 at 700, level 5 at 1300).</summary>
		public static int TotalFor(int level)
		{
			int total = 0;
			for (int l = 1; l < level; l++) total += XpFor(l);
			return total;
		}

		public static int LevelOf(int xp)
		{
			int level = 1; long total = 0; // (long: a total past int.MaxValue would wrap)
			while (level < 9999 && xp >= total + XpFor(level)) { total += XpFor(level); level++; }
			return level;
		}

		/// <summary>How much a stat is multiplied by with this many points in it (Hunger and Oxygen: how much longer they last).</summary>
		public static float Factor(int points) { return 1f + PerPoint * Mathf.Clamp(points, 0, MaxPoints); }

		/// <summary>Animals that aren't monsters: no EXP for them (the ones to catch, the harmless sea life, people).</summary>
		static readonly HashSet<string> Harmless = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{ "None", "TEST", "Llama", "Goat", "Chicken", "ButlerBot", "Puffin", "Dolphin", "Whale", "BirdPack", "Turtle", "Stingray" };

		public static bool IsMonster(AI_NetworkBehaviourType type)
		{
			string n = type.ToString();
			return !Harmless.Contains(n) && !n.StartsWith("NPC_");
		}

		// Bruce, measured in Raft (CILevelTable, 2026-09-27: health 150, bite 30): the numbers every monster is compared with
		public const float SharkHealth = 150f, SharkDamage = 30f;

		/// <summary>
		/// A monster's EXP from its health and damage (as they are on this animal, so the editor's Easy/Hard/Boss
		/// settings count): half from how tough it is, half from how hard it hits, both compared with Bruce, who is
		/// worth BruceXp. At least 1.
		/// </summary>
		public static int XpOf(float health, float damage)
		{
			float x = GainMultiplier * ReferenceXp * (0.5f * health / SharkHealth + 0.5f * damage / SharkDamage);
			return Mathf.Max(1, Mathf.RoundToInt(x));
		}
	}

	/// <summary>One player's EXP, the points they put into each stat, and how many monsters they defeated.</summary>
	public class LevelRecord
	{
		public int Xp, Kills;
		public int[] Points = new int[LevelRules.StatCount];

		public int Level { get { return LevelRules.LevelOf(Xp); } }
		public int Spent { get { return Points.Sum(); } }
		public int Unspent { get { return Mathf.Max(0, LevelRules.PointsAt(Level) - Spent); } }
		/// <summary>Every stat is full: levels still come, without points.</summary>
		public bool AllFull { get { return Spent >= LevelRules.AllPoints; } }
		public float Factor(int stat) { return LevelRules.Factor(Points[stat]); }

		/// <summary>"xp|p0,p1,...,p8|kills"</summary>
		public string Encode() { return Xp.ToString(CultureInfo.InvariantCulture) + "|" + string.Join(",", Points.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray()) + "|" + Kills.ToString(CultureInfo.InvariantCulture); }

		public static LevelRecord Decode(string text)
		{
			var r = new LevelRecord();
			if (string.IsNullOrEmpty(text)) return r;
			string[] p = text.Split('|');
			int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Xp);
			r.Xp = Mathf.Max(0, r.Xp);
			if (p.Length > 1)
			{
				string[] pts = p[1].Split(',');
				for (int i = 0; i < pts.Length && i < LevelRules.StatCount; i++)
				{
					int v;
					if (int.TryParse(pts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) r.Points[i] = Mathf.Clamp(v, 0, LevelRules.MaxPoints);
				}
			}
			if (p.Length > 2) { int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Kills); r.Kills = Mathf.Max(0, r.Kills); }
			// (never more points spent than the level gives: a hand-edited file)
			for (int i = LevelRules.StatCount - 1; i >= 0 && r.Spent > LevelRules.PointsAt(r.Level); i--)
				while (r.Points[i] > 0 && r.Spent > LevelRules.PointsAt(r.Level)) r.Points[i]--;
			return r;
		}

		public LevelRecord Copy() { return new LevelRecord { Xp = Xp, Kills = Kills, Points = (int[])Points.Clone() }; }
	}

	/// <summary>
	/// The level up system in a world: a world option, chosen in the New Game box's World settings (or switched with the
	/// Levels command); islands and plans don't turn it on. While it is on, every player earns EXP by hitting monsters
	/// anywhere (Bruce, the island animals, Raft's own). The EXP of a hit is the monster's EXP times the share of its
	/// health the hit took, so killing it alone gives exactly its EXP; the number floats up over the monster (+5).
	/// Each level gives 2 stat points to spend on the stats page (K).
	///
	/// The host keeps everyone's record in the world's island file (@levels=on, @level=steamid|xp|points) and gives a
	/// joining player theirs; players earn EXP and spend points on their own machine and send their record to the host.
	/// </summary>
	public static class PlayerLevels
	{
		/// <summary>K unless changed in Defaults (Keys, ModKeys).</summary>
		public static KeyCode Key { get { return ModKeys.Stats; } }

		static readonly Dictionary<ulong, LevelRecord> records = new Dictionary<ulong, LevelRecord>();
		static LevelRecord mine;
		static bool wasInGame, dirty, stateSeen;
		static float nextSend, nextApply;

		/// <summary>Whether the level up system is on in this world.</summary>
		public static bool On { get; private set; }

		/// <summary>This player's record (null while the system is off).</summary>
		public static LevelRecord Mine
		{
			get
			{
				if (!On) return null;
				if (mine == null)
				{
					ulong id = LocalId;
					if (Raft_Network.IsHost && id != 0 && !records.TryGetValue(id, out mine)) records[id] = mine = new LevelRecord();
					else if (!Raft_Network.IsHost) mine = new LevelRecord();
				}
				return mine;
			}
		}

		public static bool HasState { get { return On || OffByHost; } }

		/// <summary>
		/// The host switched the system off in this world (the Levels command): everyone's levels are kept for when it is
		/// switched on again.
		/// </summary>
		public static bool OffByHost { get; private set; }

		/// <summary>Chosen in the New Game box's World settings for the world being created (null = the last choice).</summary>
		public static bool? Pending;
		/// <summary>The last choice (world_rules.txt "levels=on").</summary>
		public static bool Default { get { return (WorldRules.ReadDefault("levels") ?? "").Trim().Equals("on", StringComparison.OrdinalIgnoreCase); } }
		public static void SaveDefault(bool on) { WorldRules.SaveDefault("levels", on ? "on" : "off"); }
		/// <summary>What the World settings window shows for the next world.</summary>
		public static bool Chosen { get { if (!Pending.HasValue) Pending = Default; return Pending.Value; } set { Pending = value; } }

		/// <summary>A brand-new world (host): on when it was chosen in World settings.</summary>
		public static void OnNewWorld()
		{
			bool on = Chosen;
			Pending = null;
			if (on) TurnOn(false);
		}

		/// <summary>
		/// Host: switches the system on or off in this world for every player (the Levels command). Off keeps everyone's
		/// levels (saved with the world) and takes their stat bonuses away until it is on again.
		/// </summary>
		public static void SetEnabled(bool on)
		{
			if (!Raft_Network.IsHost) return;
			if (on)
			{
				OffByHost = false;
				TurnOn(true);
				// Every player gets their own record back: switched off, a player's machine dropped it, and on again it
				// started them at level 1 - the host left their "older" record out until it passed the old one, then the
				// old one was gone
				foreach (Network_Player p in UnityEngine.Object.FindObjectsOfType<Network_Player>())
				{
					ulong id;
					try { id = p != null ? p.steamID.Id : 0UL; } catch { id = 0UL; }
					if (id == 0UL || id == LocalId) continue;
					IslandNetMessage state = StateFor(id);
					if (state != null) IslandNetwork.SendLevels(state, new Network_UserId(id));
				}
				IslandWorldState.Save();
				return;
			}
			OffByHost = true;
			if (On)
			{
				On = false;
				mine = null;
				StatApply.ResetHealth();
				IslandNetwork.SendLevels(new IslandNetMessage { Name = "off" }, null);
				Debug.Log("[CUSTOM ISLANDS] The level up system is off in this world (levels kept)");
				Raise();
			}
			IslandWorldState.Save();
		}

		/// <summary>Raised whenever this player's EXP or points change (the stats page and the HUD follow).</summary>
		public static event Action Changed;

		public static ulong LocalPlayerId { get { return LocalId; } }

		static ulong LocalId
		{
			get
			{
				try { Network_Player p = RAPI.GetLocalPlayer(); return p != null ? p.steamID.Id : 0UL; }
				catch { return 0UL; }
			}
		}

		public static void Reset()
		{
			records.Clear();
			mine = null;
			On = false;
			OffByHost = false;
			dirty = false;
			stateSeen = false;
			Fraction.Clear();
			xpCache.Clear();
			shownLevels.Clear();
			shownHealth.Clear();
			levelsDirty = false;
			StatApply.ResetHealth();
			Raise();
		}

		static void Raise() { if (Changed != null) try { Changed(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Levels: " + e.Message); } }

		/// <summary>Every frame from the mod.</summary>
		public static void Tick()
		{
			bool inGame = LoadSceneManager.IsGameSceneLoaded && !DynamicIslands.InEditor();
			if (!inGame)
			{
				// Left the world: the next one has its own levels (a saved world reads them, a joining player gets theirs)
				if (wasInGame) { wasInGame = false; Reset(); }
				return;
			}
			wasInGame = true;
			LevelTags.Tick();
			if (!On) return;
			LevelHud.Ensure();
			if (Time.unscaledTime >= nextApply) { nextApply = Time.unscaledTime + 0.25f; StatApply.Tick(); }
			if (dirty && !Raft_Network.IsHost && Time.unscaledTime >= nextSend)
			{
				dirty = false;
				nextSend = Time.unscaledTime + 1f;
				IslandNetwork.SendLevels(new IslandNetMessage { Name = "mine", Data = Mine.Encode() }, null);
			}
			// Host: everyone's level to everyone, for the name tags (after a level up, or someone joining)
			if (levelsDirty && Raft_Network.IsHost && Time.unscaledTime >= nextLevels)
			{
				levelsDirty = false;
				nextLevels = Time.unscaledTime + 1f;
				LevelRecord own = Mine; // (makes the host's own record, so it is in the list)
				IslandNetwork.SendLevels(new IslandNetMessage { Name = "levels", Data = LevelList() }, null);
			}
		}

		/// <summary>Turns the system on in this world (host: tells everyone; a client does it when the host says so).</summary>
		public static void TurnOn(bool announce)
		{
			if (On) return;
			On = true;
			Debug.Log("[CUSTOM ISLANDS] The level up system is on in this world");
			if (Raft_Network.IsHost) IslandNetwork.SendLevels(new IslandNetMessage { Name = "on" }, null);
			if (announce) LevelHud.Announce("LEVEL UP SYSTEM", "Hit monsters to earn EXP. Every level gives " + LevelRules.PointsPerLevel + " stat points: press " + Key + " to spend them.");
			Raise();
		}

		/// <summary>Turns it off again, for everyone (tests).</summary>
		public static void TurnOff()
		{
			if (On && Raft_Network.IsHost) IslandNetwork.SendLevels(new IslandNetMessage { Name = "off" }, null);
			Reset();
		}

		#region EXP

		/// <summary>Per monster and player: the share of the monster's EXP that player has had (0..1), so a monster
		/// never gives more than its EXP, and two players on one monster each get their own share.</summary>
		static readonly Dictionary<Network_Entity, Dictionary<ulong, float>> Fraction = new Dictionary<Network_Entity, Dictionary<ulong, float>>();

		/// <summary>The last EXP given by a hit, and where (the tests look at it).</summary>
		public static int LastGain { get; private set; }
		public static Vector3 LastGainAt { get; private set; }

		/// <summary>
		/// A hit this machine's player made (Network_Host.DamageEntity, with Raft's multipliers and the Damage stat).
		/// Only the host works out EXP: it has every monster as its builder made it (the editor's health and damage,
		/// the randomizer's alphas are set on the host only - a player's copy of the animal keeps Raft's numbers), so
		/// a monster is worth the same to every player. A player's hit reaches the host as Raft's damage message
		/// (OnRemoteHit) and the EXP comes back ("gain"). Returns the EXP given here.
		/// </summary>
		public static int OnHit(Network_Entity entity, float damage)
		{
			if (Raft_Network.IsHost) CountDailyKill(entity, damage);
			if (!On || !Raft_Network.IsHost) return 0;
			return HostHit(entity, damage, LocalId);
		}

		/// <summary>Host: another player's hit, as Raft's damage message from them arrives (before it is applied).</summary>
		public static int OnRemoteHit(Network_Entity entity, float damage, ulong player)
		{
			if (Raft_Network.IsHost && player != LocalId) CountDailyKill(entity, damage);
			if (!On || !Raft_Network.IsHost || player == LocalId) return 0;
			return HostHit(entity, damage, player);
		}

		/// <summary>Host: a hit that defeats a monster counts for the Daily quest (also with the level up system off).</summary>
		static void CountDailyKill(Network_Entity entity, float damage)
		{
			try
			{
				if (!DailyQuest.On || entity == null || damage <= 0f) return;
				AI_NetworkBehaviour ai = AiOf(entity);
				Stat_Health h = entity.stat_health;
				if (ai == null || !LevelRules.IsMonster(ai.behaviourType) || h == null || h.Value <= 0f) return;
				if (damage >= h.Value - 0.001f) DailyQuest.Add(DailyQuest.Monsters, "", 1);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [daily] " + e.Message); }
		}

		/// <summary>Host: the EXP a hit on a monster is worth to a player, from the monster's health before the hit.</summary>
		static int HostHit(Network_Entity entity, float damage, ulong player)
		{
			if (entity == null || damage <= 0f || player == 0UL) return 0;
			AI_NetworkBehaviour ai = AiOf(entity);
			if (ai == null || !LevelRules.IsMonster(ai.behaviourType)) return 0;
			Stat_Health h = entity.stat_health;
			if (h == null || h.Max <= 0f || h.Value <= 0f) return 0;
			int worth = MonsterXp(ai);
			float share = Mathf.Min(damage, h.Value) / h.Max;
			bool kills = damage >= h.Value - 0.001f;
			Dictionary<ulong, float> byPlayer;
			if (!Fraction.TryGetValue(entity, out byPlayer)) Fraction[entity] = byPlayer = new Dictionary<ulong, float>();
			float before = 0f;
			byPlayer.TryGetValue(player, out before);
			// (a kill alone rounds up to all of it; with others fighting it too each keeps the share they did)
			float after = Mathf.Min(1f, before + share);
			if (kills && after > 0.98f) after = 1f;
			byPlayer[player] = after;
			int gain = Mathf.RoundToInt(after * worth) - Mathf.RoundToInt(before * worth);
			// (forget monsters that are gone)
			if (Fraction.Count > 64) foreach (Network_Entity gone in Fraction.Keys.Where(k => k == null || k.IsDead).ToList()) Fraction.Remove(gone);
			bool kill = kills && before < 1f;
			if (kill) ShareKill(entity, ai, worth, player, byPlayer);
			if (player != LocalId)
			{
				// (the host's record of them grows with it: only their own "mine" moved it before, and what a player earned in
				// the second before leaving was never saved; a "mine" sent before this gain reached them is older and left out,
				// the next one has it)
				LevelRecord rec;
				// (CB2: a record made for a player the host has none of yet - the host's credit is what a player's record is checked against)
				if ((gain > 0 || kill) && !records.TryGetValue(player, out rec)) records[player] = rec = new LevelRecord();
				if ((gain > 0 || kill) && records.TryGetValue(player, out rec))
				{
					int levelBefore = rec.Level;
					rec.Xp += Mathf.Max(0, gain);
					if (kill) rec.Kills++;
					if (rec.Level != levelBefore) levelsDirty = true;
				}
				if (gain > 0 || kill)
					IslandNetwork.SendLevels(new IslandNetMessage { Name = "gain", Count = Mathf.Max(0, gain), Index = unchecked((int)entity.ObjectIndex), FullList = kill }, new Network_UserId(player));
				LastRemote = player + " +" + Mathf.Max(0, gain) + (kill ? " kill" : "");
				return Mathf.Max(0, gain);
			}
			return Gained(ai, Mathf.Max(0, gain), kill);
		}

		/// <summary>The world option Shared EXP: players this near a kill share in it, each this much of the monster's EXP.</summary>
		public const float SharedXpRange = 50f, SharedXpShare = 0.6f;

		/// <summary>
		/// Host, a monster killed with Shared EXP on: every other player within SharedXpRange of it gets SharedXpShare of its EXP,
		/// less what their own hits on it already earned (a helper never ends up with less than the share, nor gets it twice).
		/// Not a kill for them. The same message as a hit's EXP.
		/// </summary>
		static void ShareKill(Network_Entity entity, AI_NetworkBehaviour ai, int worth, ulong killer, Dictionary<ulong, float> byPlayer)
		{
			if (!WorldOptions.On(WorldOptions.SharedXp) || worth <= 0) return;
			Vector3 at = entity.transform.position;
			foreach (Network_Player p in Players.All)
			{
				if (p == null || p.steamID.Id == killer || p.steamID.Id == 0UL) continue;
				Vector3 d = p.transform.position - at;
				if (d.magnitude > SharedXpRange) continue;
				ulong id = p.steamID.Id;
				float had;
				byPlayer.TryGetValue(id, out had);
				int give = Mathf.RoundToInt(SharedXpShare * worth) - Mathf.RoundToInt(had * worth);
				if (give <= 0) continue;
				byPlayer[id] = Mathf.Max(had, SharedXpShare);
				LastShared = id + " +" + give;
				Debug.Log("[CUSTOM ISLANDS] Shared EXP: +" + give + " to " + id + " (" + d.magnitude.ToString("F0") + " m from the kill)");
				if (id == LocalId) { Gained(ai, give, false); continue; }
				LevelRecord rec;
				if (!records.TryGetValue(id, out rec)) records[id] = rec = new LevelRecord(); // (CB2: as for a hit)
				{ int levelBefore = rec.Level; rec.Xp += give; if (rec.Level != levelBefore) levelsDirty = true; }
				IslandNetwork.SendLevels(new IslandNetMessage { Name = "gain", Count = give, Index = unchecked((int)entity.ObjectIndex), FullList = false }, new Network_UserId(id));
			}
		}

		/// <summary>The host's last shared EXP ("id +n", tests).</summary>
		public static string LastShared { get; internal set; }

		/// <summary>The host's last EXP for another player's hit ("id +n [kill]", tests).</summary>
		public static string LastRemote { get; private set; }

		/// <summary>EXP (and maybe a kill) for this player's hit on this monster: counted, floated over it.</summary>
		static int Gained(AI_NetworkBehaviour ai, int gain, bool kill)
		{
			if (Mine == null) return 0;
			if (kill)
			{
				Mine.Kills++;
				Debug.Log("[CUSTOM ISLANDS] Defeated a " + (ai != null ? ai.behaviourType.ToString() : "monster") + " (" + Mine.Kills + " monster(s) in all)");
				if (gain <= 0) MarkChanged();
			}
			if (gain <= 0) return 0;
			GiveXp(gain, ai != null ? TopOf(ai) : (Vector3?)null);
			return gain;
		}

		/// <summary>Adds EXP to this player (a hit, or the tests), floating "+n" at the place.</summary>
		public static void GiveXp(int amount, Vector3? at)
		{
			if (!On || amount <= 0 || Mine == null) return;
			int levelBefore = Mine.Level;
			Mine.Xp = (int)Math.Min(int.MaxValue, (long)Mine.Xp + amount);
			LastGain = amount;
			if (at.HasValue) { LastGainAt = at.Value; LevelHud.Float("+" + amount, at.Value); }
			LevelHud.ShowBar();
			int levelAfter = Mine.Level;
			if (levelAfter > levelBefore)
			{
				// (once every stat could be full, levels still come but give no points)
				int points = LevelRules.PointsAt(levelAfter) - LevelRules.PointsAt(levelBefore);
				Debug.Log("[CUSTOM ISLANDS] Level up: level " + levelAfter + " (" + Mine.Xp + " EXP), +" + points + " point(s), " + Mine.Unspent + " stat point(s) to spend");
				LevelHud.Announce("LEVEL " + levelAfter + "!", points > 0 ? "+" + points + " stat points. Click here, or press " + Key + ", to spend them" :
					Mine.Unspent > 0 ? "Every stat can be full now: " + Mine.Unspent + " point(s) left to spend (" + Key + ")" : "Every stat is full: no more points, but the levels go on", true);
				levelsDirty = true;
			}
			MarkChanged();
		}

		static void MarkChanged() { dirty = true; if (Raft_Network.IsHost) levelsDirty = true; Raise(); }

		// Other players' levels

		/// <summary>Every player's level as this machine knows it (the host: from the records; a player: from the host), for their name tags.</summary>
		static readonly Dictionary<ulong, int> shownLevels = new Dictionary<ulong, int>();
		static bool levelsDirty;
		static float nextLevels;

		/// <summary>A player's level (0 = the system is off; a player without a record yet is level 1, the same on every machine).</summary>
		public static int LevelOf(ulong id)
		{
			if (!On) return 0;
			LevelRecord r;
			if (Raft_Network.IsHost) return records.TryGetValue(id, out r) ? r.Level : 1;
			if (id == LocalId && mine != null) return mine.Level;
			int level;
			return shownLevels.TryGetValue(id, out level) ? level : 1;
		}

		/// <summary>A player's points in Health (for their maximum health on every machine; 0 = not known).</summary>
		public static int HealthPointsOf(ulong id)
		{
			if (!On) return 0;
			LevelRecord r;
			if (Raft_Network.IsHost) return records.TryGetValue(id, out r) ? r.Points[LevelRules.Health] : 0;
			if (id == LocalId && mine != null) return mine.Points[LevelRules.Health];
			int points;
			return shownHealth.TryGetValue(id, out points) ? points : 0;
		}
		static readonly Dictionary<ulong, int> shownHealth = new Dictionary<ulong, int>();

		/// <summary>Host: "id=level:health points;..." of every player with a record.</summary>
		static string LevelList()
		{
			return string.Join(";", records.Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture) + "=" + kv.Value.Level.ToString(CultureInfo.InvariantCulture) +
				":" + kv.Value.Points[LevelRules.Health].ToString(CultureInfo.InvariantCulture)).ToArray());
		}

		static void ReadLevelList(string data)
		{
			shownLevels.Clear();
			shownHealth.Clear();
			foreach (string part in (data ?? "").Split(';'))
			{
				int eq = part.IndexOf('=');
				if (eq <= 0) continue;
				string[] v = part.Substring(eq + 1).Split(':');
				ulong id; int level, health;
				if (!ulong.TryParse(part.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out id) ||
					!int.TryParse(v[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level)) continue;
				shownLevels[id] = level;
				if (v.Length > 1 && int.TryParse(v[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out health)) shownHealth[id] = Mathf.Clamp(health, 0, LevelRules.MaxPoints);
			}
		}

		/// <summary>
		/// The EXP a monster is worth, cached per animal together with the max health it was worked out from: a
		/// randomizer turned on mid-game makes an animal that was already hit an alpha (x3 health, x1.6 damage), and
		/// the old worth would stick.
		/// </summary>
		public static int MonsterXp(AI_NetworkBehaviour ai)
		{
			float hp = ai.networkEntity != null && ai.networkEntity.stat_health != null ? ai.networkEntity.stat_health.Max : 0f;
			KeyValuePair<float, int> cached;
			if (xpCache.TryGetValue(ai, out cached) && Mathf.Abs(cached.Key - hp) < 0.01f) return cached.Value;
			int xp = LevelRules.XpOf(hp, DamageOf(ai.gameObject, ai.behaviourType));
			if (xpCache.Count > 256) foreach (AI_NetworkBehaviour gone in xpCache.Keys.Where(k => k == null).ToList()) xpCache.Remove(gone);
			xpCache[ai] = new KeyValuePair<float, int>(hp, xp);
			return xp;
		}
		static readonly Dictionary<AI_NetworkBehaviour, KeyValuePair<float, int>> xpCache = new Dictionary<AI_NetworkBehaviour, KeyValuePair<float, int>>();

		static readonly string[] NotDamage = { "taken", "threshold", "treshold", "treshhold", "recieved", "received", "range", "radius", "frequency", "time", "particle", "state", "reached", "sound", "box", "event", "cooldown", "delay", "multiplier", "chance", "speed", "distance" };

		/// <summary>
		/// How hard a monster hits: the biggest damage number on its attack states and damage boxes (Bruce's bite is
		/// AI_State_Attack_Entity_Shark._attackPlayerDamage, a warthog's charge AI_State_MeleeAttack's damage...), as
		/// set on this animal (the editor's damage multiplier changes those same fields). Raft's own number when none
		/// is found.
		/// </summary>
		public static float DamageOf(GameObject root, AI_NetworkBehaviourType type)
		{
			float best = 0f;
			bool found = false;
			foreach (MonoBehaviour c in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (c == null) continue;
				for (Type t = c.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
				{
					foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					{
						string n = f.Name.ToLowerInvariant();
						if (!n.Contains("damage") || NotDamage.Any(n.Contains)) continue;
						float v;
						if (f.FieldType == typeof(float)) v = (float)f.GetValue(c);
						else if (f.FieldType == typeof(int)) v = (int)f.GetValue(c);
						else continue;
						found = true;
						if (v > best && v < 10000f) best = v;
					}
				}
			}
			// (fields all 0: an animal made harmless in the editor)
			return found ? best : FallbackDamage(type);
		}

		/// <summary>Damage when no field was found (an attack that isn't a damage field: bees, puffer fish).</summary>
		static float FallbackDamage(AI_NetworkBehaviourType type)
		{
			switch (type.ToString())
			{
				case "BugSwarm_Bee": return 5f;
				case "PufferFish": return 20f;
				case "Boss_Varuna": case "HyenaBoss": case "MamaBear": return 40f;
				default: return LevelRules.SharkDamage * 0.5f;
			}
		}

		public static AI_NetworkBehaviour AiOf(Network_Entity entity)
		{
			if (entity == null) return null;
			AI_NetworkBehaviour ai = entity.GetComponent<AI_NetworkBehaviour>();
			if (ai == null) ai = entity.GetComponentInParent<AI_NetworkBehaviour>();
			if (ai == null) ai = entity.GetComponentInChildren<AI_NetworkBehaviour>();
			return ai;
		}

		/// <summary>Just above the monster's head, where its "+5" floats up.</summary>
		static Vector3 TopOf(AI_NetworkBehaviour ai)
		{
			Bounds b = new Bounds(ai.transform.position, Vector3.zero);
			bool any = false;
			foreach (Collider c in ai.GetComponentsInChildren<Collider>())
			{
				if (c == null || !c.enabled || c.isTrigger) continue;
				if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
			}
			if (!any) foreach (Renderer r in ai.GetComponentsInChildren<Renderer>())
			{
				if (r == null) continue;
				if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
			}
			return new Vector3(b.center.x, (any ? b.max.y : ai.transform.position.y + 1f) + 0.3f, b.center.z);
		}

		#endregion

		#region Stat points

		/// <summary>Puts a point into a stat (or takes one back with -1). False if there is no point to spend or the stat is full.</summary>
		public static bool Spend(int stat, int delta = 1)
		{
			LevelRecord r = Mine;
			if (r == null || stat < 0 || stat >= LevelRules.StatCount) return false;
			if (delta > 0 && (r.Unspent <= 0 || r.Points[stat] >= LevelRules.MaxPoints)) return false;
			if (delta < 0 && r.Points[stat] <= 0) return false;
			r.Points[stat] += delta > 0 ? 1 : -1;
			Debug.Log("[CUSTOM ISLANDS] Stat point " + (delta > 0 ? "into " : "taken back from ") + LevelRules.StatNames[stat] + ": " + r.Points[stat] + "/" + LevelRules.MaxPoints + " (+" + r.Points[stat] + "%), " + r.Unspent + " left");
			MarkChanged();
			StatApply.Tick();
			return true;
		}

		/// <summary>How much a stat of this player is multiplied by now (1 when the system is off).</summary>
		public static float Factor(int stat)
		{
			LevelRecord r = On ? mine : null;
			return r != null ? r.Factor(stat) : 1f;
		}

		/// <summary>Sets this player's record (tests).</summary>
		public static void SetMine(LevelRecord r)
		{
			if (!On) return;
			Mine.Xp = r.Xp;
			Mine.Points = (int[])r.Points.Clone();
			Mine.Kills = r.Kills;
			MarkChanged();
			StatApply.Tick();
		}

		#endregion

		#region Saving and the network

		/// <summary>The world file's lines: "@levels=on" and "@level=steamid|xp|points" per player.</summary>
		public static IEnumerable<string> WriteLines()
		{
			if (!On && !OffByHost) yield break;
			yield return "@levels=" + (On ? "on" : "off");
			foreach (var kv in records) yield return "@level=" + kv.Key.ToString(CultureInfo.InvariantCulture) + "|" + kv.Value.Encode();
		}

		public static bool ReadLine(string key, string value)
		{
			if (key.Equals("levels", StringComparison.OrdinalIgnoreCase))
			{
				On = value.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
				OffByHost = value.Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
				mine = null;
				return true;
			}
			if (!key.Equals("level", StringComparison.OrdinalIgnoreCase)) return false;
			int bar = value.IndexOf('|');
			ulong id;
			if (bar > 0 && ulong.TryParse(value.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) records[id] = LevelRecord.Decode(value.Substring(bar + 1));
			mine = null;
			return true;
		}

		/// <summary>Host -> a player who joined: whether the system is on, and their record.</summary>
		public static IslandNetMessage StateFor(ulong id)
		{
			if (!On) return null;
			LevelRecord r;
			levelsDirty = true; // (and everyone's levels, the new player's name tag with them)
			return new IslandNetMessage { Name = "state", Count = 1, Data = records.TryGetValue(id, out r) ? r.Encode() : "" };
		}

		public static void OnMessage(IslandNetMessage msg, Network_UserId from)
		{
			switch (msg.Name ?? "")
			{
				case "on": // host -> everyone
					if (!Raft_Network.IsHost) TurnOn(true);
					break;
				case "off": // host -> everyone: switched off (the host keeps the levels)
					if (!Raft_Network.IsHost) Reset();
					break;
				case "state": // host -> a joining player: their record
					if (Raft_Network.IsHost) break;
					// (CB2: Count 2 = the host put this player's record right - more EXP than it credited: taken as it is)
					if (msg.Count == 2 && On) { mine = LevelRecord.Decode(msg.Data); stateSeen = true; dirty = false; Debug.Log("[CUSTOM ISLANDS] Levels: the host put this player's record right (" + mine.Xp + " EXP)"); Raise(); StatApply.Tick(); break; }
					if (msg.Count != 1) break;
					{
						bool was = On;
						On = true;
						LevelRecord theirs = LevelRecord.Decode(msg.Data);
						// EXP only grows: a reply to an earlier ask (the list is asked for again until it comes) or one that
						// crossed EXP earned meanwhile must not take progress away. The host's is taken when it has more EXP,
						// or on the first reply; with the same EXP after that, what this player did since (points) stays.
						if (mine != null && mine.Xp > theirs.Xp) dirty = true; // (the host learns ours)
						else if (mine == null || !stateSeen || theirs.Xp > mine.Xp) mine = theirs;
						stateSeen = true;
						if (!was) Debug.Log("[CUSTOM ISLANDS] The level up system is on in this world (level " + mine.Level + ")");
						Raise();
						StatApply.Tick();
					}
					break;
				case "mine": // a player -> host: their record now
					if (!Raft_Network.IsHost) break;
					// (CB2: only the host switches the system on - a player's record no longer does; a record sent just before the
					// host switched it off is left out too)
					if (!On) { Debug.Log("[CUSTOM ISLANDS] Levels: a record from " + from.Id + " left out (the system is off here)"); break; }
					LevelRecord old;
					int levelBefore = records.TryGetValue(from.Id, out old) ? old.Level : 0;
					LevelRecord sent = LevelRecord.Decode(msg.Data);
					// (a message older than what the host has - EXP only grows - is left out)
					if (old != null && sent.Xp < old.Xp) { Debug.Log("[CUSTOM ISLANDS] Levels: an older record from " + from.Id + " left out (" + sent.Xp + " < " + old.Xp + " EXP)"); break; }
					// (CB2: the host credits every player's EXP - their hits, shared kills, the daily quest - so a record with more
					// EXP or kills than that is put right: the host's EXP and kills, the player's points as far as that level
					// gives, and the player is told)
					int creditedXp = old != null ? old.Xp : 0, creditedKills = old != null ? old.Kills : 0;
					if (!PlayerTrust.ForTests && (sent.Xp > creditedXp || sent.Kills > creditedKills))
					{
						Debug.Log("[CUSTOM ISLANDS] Levels: a record from " + from.Id + " with more than the host credited (" + sent.Xp + " EXP, " + sent.Kills + " kills; credited " + creditedXp + ", " + creditedKills + "): put right");
						sent.Xp = Mathf.Min(sent.Xp, creditedXp);
						sent.Kills = Mathf.Min(sent.Kills, creditedKills);
						sent = LevelRecord.Decode(sent.Encode()); // (the points as far as the level gives)
						Corrected++;
						IslandNetwork.SendLevels(new IslandNetMessage { Name = "state", Count = 2, Data = sent.Encode() }, from);
					}
					records[from.Id] = sent;
					if (records[from.Id].Level != levelBefore || old == null || records[from.Id].Points[LevelRules.Health] != old.Points[LevelRules.Health]) levelsDirty = true;
					break;
				case "levels": // host -> everyone: every player's level
					if (!Raft_Network.IsHost) ReadLevelList(msg.Data);
					break;
				case "gain": // host -> the player who hit: the EXP their hit was worth (Index = the monster, FullList = it was the kill)
					if (Raft_Network.IsHost || !On) break;
					Network_Entity hit = null;
					try { hit = NetworkIDManager.GetNetworkIDFromObjectIndex<Network_Entity>(unchecked((uint)msg.Index)); } catch { }
					Gained(AiOf(hit), Mathf.Max(0, msg.Count), msg.FullList);
					break;
			}
		}

		/// <summary>Host: how many players' records it put right (CB2, tests).</summary>
		public static int Corrected { get; private set; }

		/// <summary>
		/// Host, CB2: EXP for another player that the host credits itself (the daily quest's reward, which their machine adds
		/// on its side): their record grows with it, so their next record isn't put right.
		/// </summary>
		public static void CreditXp(ulong player, int amount)
		{
			if (!Raft_Network.IsHost || !On || amount <= 0 || player == 0UL || player == LocalId) return;
			LevelRecord rec;
			if (!records.TryGetValue(player, out rec)) records[player] = rec = new LevelRecord();
			int levelBefore = rec.Level;
			rec.Xp = (int)Math.Min(int.MaxValue, (long)rec.Xp + amount);
			if (rec.Level != levelBefore) levelsDirty = true;
		}

		/// <summary>Host: the record of a player (tests).</summary>
		public static LevelRecord RecordOf(ulong id) { LevelRecord r; return records.TryGetValue(id, out r) ? r : null; }

		#endregion
	}

	/// <summary>
	/// Puts the stat points to work on this player: speeds and the jump while Raft moves the player (the fields are
	/// multiplied only for that moment, so Raft's flippers and other changes stay as they are), damage on every hit
	/// that goes to a monster, maximum health, and how fast hunger and breath run out.
	/// </summary>
	public static class StatApply
	{
		/// <summary>Raft's maximum health of a player before the Health stat, and what the stat last set it to.</summary>
		class BaseMax { public float Base, Set; }
		static readonly Dictionary<Stat_Health, BaseMax> health = new Dictionary<Stat_Health, BaseMax>();

		static Network_Player Local
		{
			get { try { return RAPI.GetLocalPlayer(); } catch { return null; } }
		}

		public static bool IsLocal(PersonController pc)
		{
			if (pc == null || !PlayerLevels.On) return false;
			Network_Player p = Local;
			return p != null && p.PersonController == pc;
		}

		/// <summary>
		/// Maximum health: Raft's own times the Health stat (checked a few times a second and after a point) - for every
		/// player on every machine, so the host's and the other players' copy of a player has the same maximum as that
		/// player's own (the host applies monsters' bites to its copy; its health must not stop at Raft's 100).
		/// </summary>
		public static void Tick()
		{
			Network_Player local = Local;
			foreach (Network_Player p in Players.All)
			{
				Stat_Health h = p != null && p.Stats != null ? p.Stats.stat_health : null;
				if (h == null) continue;
				int points = p == local ? (PlayerLevels.Mine != null ? PlayerLevels.Mine.Points[LevelRules.Health] : 0) : PlayerLevels.HealthPointsOf(p.steamID.Id);
				Apply(h, LevelRules.Factor(points));
			}
			foreach (Stat_Health gone in health.Keys.Where(k => k == null).ToList()) health.Remove(gone);
		}

		static void Apply(Stat_Health h, float factor)
		{
			BaseMax b;
			if (!health.TryGetValue(h, out b)) health[h] = b = new BaseMax { Base = h.Max, Set = h.Max };
			// (Raft set its own maximum again - a respawn: that is the base now)
			else if (!Mathf.Approximately(h.Max, b.Set)) { b.Base = h.Max; b.Set = h.Max; }
			float want = b.Base * factor;
			if (Mathf.Approximately(h.Max, want)) { b.Set = want; return; }
			float gain = want - h.Max;
			h.SetMaxValue(want);
			b.Set = want;
			// (more health: the new part is there at once; less (a point taken back): no more than the new maximum)
			if (gain > 0f && h.Value > 0f) h.Value = Mathf.Min(want, h.Value + gain);
			else if (h.Value > want) h.Value = want;
		}

		/// <summary>The level system went off (left the world, tests): Raft's own maximum health again, for every player.</summary>
		public static void ResetHealth()
		{
			foreach (var kv in health.ToList())
			{
				Stat_Health h = kv.Key;
				if (h == null || !Mathf.Approximately(h.Max, kv.Value.Set) || kv.Value.Base <= 0f) continue;
				h.SetMaxValue(kv.Value.Base);
				if (h.Value > kv.Value.Base) h.Value = kv.Value.Base;
			}
			health.Clear();
		}

		/// <summary>Raft's maximum health of this machine's player before the Health stat (tests).</summary>
		public static float HealthBaseOf(Network_Player p)
		{
			BaseMax b;
			return p != null && p.Stats != null && p.Stats.stat_health != null && health.TryGetValue(p.Stats.stat_health, out b) ? b.Base : 0f;
		}
		public static float HealthBase { get { return HealthBaseOf(Local); } }

		public struct Saved { public float Walk, Run, Swim, Jump; public bool Set; }

		public static Saved Boost(PersonController pc)
		{
			var s = new Saved();
			if (!IsLocal(pc)) return s;
			s = new Saved { Walk = pc.normalSpeed, Run = pc.sprintSpeed, Swim = pc.swimSpeed, Jump = pc.jumpSpeed, Set = true };
			try
			{
				pc.normalSpeed *= PlayerLevels.Factor(LevelRules.Walk);
				pc.sprintSpeed *= PlayerLevels.Factor(LevelRules.Run);
				pc.swimSpeed *= PlayerLevels.Factor(LevelRules.Swim);
				// (a jump this much higher: the height goes with the take-off speed squared)
				pc.jumpSpeed *= Mathf.Sqrt(PlayerLevels.Factor(LevelRules.Jump));
			}
			catch (Exception e)
			{
				// (part way through: the speeds as they were - AU41)
				Restore(pc, s);
				Debug.LogWarning("[CUSTOM ISLANDS] Levels: speed boost: " + e.Message);
				return new Saved();
			}
			return s;
		}

		public static void Restore(PersonController pc, Saved s)
		{
			if (!s.Set || pc == null) return;
			pc.normalSpeed = s.Walk; pc.sprintSpeed = s.Run; pc.swimSpeed = s.Swim; pc.jumpSpeed = s.Jump;
		}

		/// <summary>Which of this player's stats a consumable drains: LevelRules.Hunger, LevelRules.Thirst, or -1 (another player's, or none).</summary>
		public static int ConsumableStat(Stat_Consumable c)
		{
			if (c == null || !PlayerLevels.On) return -1;
			Network_Player p = Local;
			if (p == null || p.Stats == null) return -1;
			Stat_Bonus_Consumable hunger = p.Stats.stat_hunger, thirst = p.Stats.stat_thirst;
			if (hunger != null && (hunger.normalConsumable == c || hunger.bonusConsumable == c)) return LevelRules.Hunger;
			if (thirst != null && (thirst.normalConsumable == c || thirst.bonusConsumable == c)) return LevelRules.Thirst;
			return -1;
		}

		public static bool IsLocalOxygen(Stat_Oxygen o)
		{
			if (o == null || !PlayerLevels.On) return false;
			Network_Player p = Local;
			return p != null && p.Stats != null && p.Stats.stat_oxygen == o;
		}
	}

	// (the speeds go back in a Finalizer, not a Postfix: it runs also when Raft's method throws - a Postfix was skipped then,
	// and the boost stayed on and grew at every frame - AU41)
	[HarmonyPatch(typeof(PersonController), "GroundControll")]
	static class LevelGroundPatch
	{
		static void Prefix(PersonController __instance, out StatApply.Saved __state) { __state = StatApply.Boost(__instance); }
		static Exception Finalizer(Exception __exception, PersonController __instance, StatApply.Saved __state) { StatApply.Restore(__instance, __state); return __exception; }
	}

	[HarmonyPatch(typeof(PersonController), "WaterControll")]
	static class LevelWaterPatch
	{
		static void Prefix(PersonController __instance, out StatApply.Saved __state) { __state = StatApply.Boost(__instance); }
		static Exception Finalizer(Exception __exception, PersonController __instance, StatApply.Saved __state) { StatApply.Restore(__instance, __state); return __exception; }
	}

	/// <summary>Hunger and thirst drain slower with the Hunger and Thirst stats (each of Raft's is two consumables: normal and bonus).</summary>
	[HarmonyPatch(typeof(Stat_Consumable), "LostPerSecond", MethodType.Getter)]
	static class LevelHungerPatch
	{
		static void Postfix(Stat_Consumable __instance, ref float __result)
		{
			int stat = StatApply.ConsumableStat(__instance);
			if (stat >= 0) __result /= PlayerLevels.Factor(stat);
		}
	}

	/// <summary>Breath runs out slower with the Oxygen stat (only for that frame: Raft's air bottle sets the same field).</summary>
	[HarmonyPatch(typeof(Stat_Oxygen), "Update")]
	static class LevelOxygenPatch
	{
		// (a reference to the field, not FieldInfo: GetValue and SetValue boxed the float twice a frame)
		static readonly AccessTools.FieldRef<Stat_Oxygen, float> LostPerSecond = OxygenField();

		static AccessTools.FieldRef<Stat_Oxygen, float> OxygenField()
		{
			try { return AccessTools.FieldRefAccess<Stat_Oxygen, float>("oxygenLostPerSecond"); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Levels: oxygen: " + e.Message); return null; }
		}

		static void Prefix(Stat_Oxygen __instance, out float __state)
		{
			__state = -1f;
			if (LostPerSecond == null || !StatApply.IsLocalOxygen(__instance)) return;
			__state = LostPerSecond(__instance);
			LostPerSecond(__instance) = __state / PlayerLevels.Factor(LevelRules.Oxygen);
		}

		// (a Finalizer: put back also when Raft's Update throws - AU41)
		static Exception Finalizer(Exception __exception, Stat_Oxygen __instance, float __state)
		{
			try { if (__state >= 0f && LostPerSecond != null) LostPerSecond(__instance) = __state; }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Levels: oxygen: " + e.Message); }
			return __exception;
		}
	}

	/// <summary>
	/// Host: another player's hit arrives as Raft's damage message (Network_Host_Entities.Deserialize, with who sent it
	/// and the final damage - their Damage stat, Raft's multipliers and the world's monster difficulty already in it)
	/// and is applied straight to the monster. Just before that, the host works out the EXP it is worth to that player.
	/// </summary>
	[HarmonyPatch(typeof(Network_Host_Entities), "Deserialize")]
	static class LevelRemoteHitPatch
	{
		static void Prefix(Message_NetworkBehaviour msg, Network_UserId remoteID)
		{
			try
			{
				if (!(PlayerLevels.On || DailyQuest.On) || !Raft_Network.IsHost) return; // (also for the Daily quest's kills with levels off)
				Message_NetworkEntity_Damage d = msg as Message_NetworkEntity_Damage;
				if (d == null || d.damageInflictorEntityType != EntityType.Player) return;
				Network_Entity entity = NetworkIDManager.GetNetworkIDFromObjectIndex<Network_Entity>(d.entityObjectIndex);
				if (entity == null || entity.entityType != EntityType.Enemy || entity.IsInvurnerable) return;
				PlayerLevels.OnRemoteHit(entity, d.damage, remoteID.Id);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Levels (a player's hit): " + e.Message); }
		}
	}

	/// <summary>
	/// Every hit a player makes goes through Network_Host.DamageEntity on that player's own machine (melee weapons,
	/// arrows, spears, stones): the Damage stat makes it bigger, and on the host a hit on a monster gives EXP (a
	/// player's hit gets its EXP from the host: LevelRemoteHitPatch).
	/// </summary>
	[HarmonyPatch(typeof(Network_Host), "DamageEntity")]
	static class LevelDamagePatch
	{
		// (last: other changes to the damage - a monster difficulty - come first, so the EXP follows the damage really done)
		[HarmonyPriority(Priority.Last)]
		static void Prefix(Network_Entity entity, ref float damage, EntityType damageInflictorEntityType)
		{
			try
			{
				if (!(PlayerLevels.On || DailyQuest.On) || entity == null || entity.entityType != EntityType.Enemy) return; // (Factor is 1 with levels off)
				if (damageInflictorEntityType == EntityType.Environment && LevelFireworkPatch.By != 0UL)
				{
					// a firework's hit (Raft counts it as the world's): EXP for the player who lit it, its damage as Raft's
					if (entity.IsInvurnerable) return;
					if (LevelFireworkPatch.By == PlayerLevels.LocalPlayerId) PlayerLevels.OnHit(entity, damage);
					else PlayerLevels.OnRemoteHit(entity, damage, LevelFireworkPatch.By);
					return;
				}
				if (damageInflictorEntityType != EntityType.Player) return;
				damage *= PlayerLevels.Factor(LevelRules.Damage);
				// What Raft does to it next (DamageEntity): a mode that takes players' damage away makes it 0 (none of Raft's own
				// does; never on an island creature - IslandCreatureHitPatch), the mode's PvE multiplier scales it (Hard x0.8)
				SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
				float dealt = damage;
				if (mode != null && mode.playerSpecificVariables != null)
				{
					if (mode.playerSpecificVariables.negateOutgoingPlayerDamage) dealt = 0f;
					else dealt *= mode.playerSpecificVariables.outgoingDamageMultiplierPVE;
				}
				if (!entity.IsInvurnerable) PlayerLevels.OnHit(entity, dealt);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Levels (hit): " + e.Message); }
		}
	}

	/// <summary>
	/// Raft's fireworks hurt as the world (EntityType.Environment) and keep no owner, so a monster hit by one gave no
	/// EXP. Every machine launches the firework from the player's hand (Firework_Hand.LaunchFirework, with the
	/// firework's network index): that player is kept per firework, and while the host works out its hit (only the
	/// host does: Firework.TryToDamageEntity) LevelDamagePatch gives that player the EXP.
	/// </summary>
	[HarmonyPatch(typeof(Firework_Hand), "LaunchFirework", new Type[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(bool), typeof(int), typeof(uint) })]
	static class LevelFireworkLaunchPatch
	{
		static void Postfix(Firework_Hand __instance, uint objectIndex)
		{
			try
			{
				Network_Player p = Traverse.Create(__instance).Field("player").GetValue<Network_Player>();
				if (p != null) LevelFireworkPatch.Lit(objectIndex, p.steamID.Id);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Levels (firework): " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(Firework), "TryToDamageEntity")]
	static class LevelFireworkPatch
	{
		/// <summary>The player whose firework is hitting something now (0: none).</summary>
		public static ulong By;
		static readonly Dictionary<uint, ulong> Owner = new Dictionary<uint, ulong>();

		public static void Lit(uint objectIndex, ulong player)
		{
			if (Owner.Count > 200) Owner.Clear();   // fireworks live seconds; never let it grow
			Owner[objectIndex] = player;
		}

		static void Prefix(Firework __instance)
		{
			ulong id;
			By = __instance != null && Owner.TryGetValue(__instance.ObjectIndex, out id) ? id : 0UL;
		}

		static void Postfix() { By = 0UL; }
	}
}

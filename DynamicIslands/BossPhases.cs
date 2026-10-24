using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Boss phases (ROADMAP RF5): a creature spot with boss.phase set gets a second phase in a fight. When one of its
	/// animals falls to that share of its health (default half), the host - once per fight - calls more creatures
	/// (boss.summon of boss.summonKind, the spot's own kind by default; they stand around the boss and aren't counted for
	/// the spot's "defeat"), speeds the spot's animals up (boss.speed times), sends a signal (boss.signal, "phase" by
	/// default) for when/then rules and runs the spot's "phase" event. The fight is remembered in the island's state
	/// (KeyBase + the spot's object index, saved with the world): after a reload mid-fight the boss comes back in its
	/// second phase (faster, at that share of its health) without calling again or sending the signal again; the spot's
	/// "defeat" clears it, so the next fight (after the regrow days) has its phase again.
	/// Health goes by the share Raft shows (Value / Max): the monster difficulty divides damage, not health, so under
	/// Nightmare the phase comes at the same share after more hits. A guest's hits change the host's health too
	/// (Raft's Network_Host.DamageEntity), so they count; the summons are Raft's networked animals, seen by everyone.
	/// </summary>
	public static class BossPhases
	{
		public const int KeyBase = 0xE0000;
		public const string Share = "boss.phase", Summon = "boss.summon", SummonKind = "boss.summonKind", Speed = "boss.speed", Signal = "boss.signal";
		public const string Event = "phase", DefaultSignal = "phase";
		public const int MaxSummon = 6;
		public const float MinShare = 0.05f, MaxShare = 0.95f, MinSpeed = 0.5f, MaxSpeed = 2.5f;

		/// <summary>Phases started on this machine since start-up (the tests count them).</summary>
		public static int Started;

		/// <summary>The health share the phase starts at (0: no phase).</summary>
		public static float ShareOf(IDictionary<string, string> p)
		{
			float s = ObjectProps.GetFloat(p, Share, 0f);
			return s <= 0f ? 0f : Mathf.Clamp(s, MinShare, MaxShare);
		}

		public static bool Has(IDictionary<string, string> p) { return ShareOf(p) > 0f; }
		public static int SummonCount(IDictionary<string, string> p) { return Mathf.Clamp(ObjectProps.GetInt(p, Summon, 0), 0, MaxSummon); }
		public static float SpeedOf(IDictionary<string, string> p) { return Mathf.Clamp(ObjectProps.GetFloat(p, Speed, 1f), MinSpeed, MaxSpeed); }
		public static string SignalOf(IDictionary<string, string> p) { string s = ObjectProps.Get(p, Signal).Trim(); return s.Length > 0 ? s : DefaultSignal; }

		/// <summary>The kind the phase calls: boss.summonKind (a creature's object name), else the spot's own.</summary>
		public static ContentCatalog.CreatureKind SummonKindOf(CreatureSpawnPoint p)
		{
			string name = ObjectProps.Get(p.Props, SummonKind).Trim();
			if (name.Length == 0) return p.Kind;
			// (the object name, Creature_Bear; or the type or the label, as builders may write it in a recipe)
			ContentCatalog.CreatureKind k = ContentCatalog.CreatureOf(name) ?? ContentCatalog.CreatureOf("Creature_" + name) ??
				ContentCatalog.Creatures.FirstOrDefault(c => string.Equals(c.Label, name, StringComparison.OrdinalIgnoreCase));
			return k ?? p.Kind;
		}

		/// <summary>What the inspector and the plan check say about a spot's phase.</summary>
		public static string Describe(IDictionary<string, string> p)
		{
			if (!Has(p)) return "no phase";
			var parts = new List<string> { "at " + Mathf.RoundToInt(ShareOf(p) * 100f) + "% health" };
			if (SummonCount(p) > 0) parts.Add("calls " + SummonCount(p));
			if (!Mathf.Approximately(SpeedOf(p), 1f)) parts.Add("speed x" + SpeedOf(p).ToString("0.##", CultureInfo.InvariantCulture));
			parts.Add("signal '" + SignalOf(p) + "'");
			return string.Join(", ", parts.ToArray());
		}

		public static int KeyOf(CreatureSpawnPoint p)
		{
			IslandObjectRef r = p != null ? p.GetComponent<IslandObjectRef>() : null;
			return r != null ? KeyBase + (r.Index & 0xFFFF) : -1;
		}

		/// <summary>Whether this spot's fight is in its second phase (host; clients get the key too).</summary>
		public static bool InPhase(IslandWorldState.Entry e, CreatureSpawnPoint p)
		{
			int key = KeyOf(p);
			return e != null && key >= 0 && e.State.ContainsKey(key);
		}

		/// <summary>Host, once a second and after each hit: starts the phase when an animal of the spot is low enough.</summary>
		internal static void Check(IslandWorldState.Entry e, CreatureSpawnPoint p)
		{
			if (e == null || p == null || p.Kind == null || !Raft_Network.IsHost || !Has(p.Props) || InPhase(e, p)) return;
			float share = ShareOf(p.Props);
			AI_NetworkBehaviour low = p.Spawned.FirstOrDefault(ai => ai != null && ai.networkEntity != null && ai.networkEntity.stat_health != null &&
				ai.networkEntity.stat_health.Max > 0f && ai.networkEntity.stat_health.Value > 0f && ai.networkEntity.stat_health.Value <= share * ai.networkEntity.stat_health.Max);
			if (low != null) Start(e, p, low);
		}

		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		static void Start(IslandWorldState.Entry e, CreatureSpawnPoint p, AI_NetworkBehaviour boss)
		{
			int key = KeyOf(p);
			if (key < 0) return;
			e.State[key] = new ObjectState { Active = false, Day = Today };
			IslandNetwork.SendUsed(e.Id, key, Today);
			Started++;
			float mult = SpeedOf(p.Props);
			if (!Mathf.Approximately(mult, 1f)) foreach (AI_NetworkBehaviour ai in p.Spawned) CreatureSpawner.MultiplySpeed(ai, mult);
			int called = CreatureSpawner.Summon(e, p, SummonKindOf(p), SummonCount(p.Props), boss.transform.position);
			Behaviours.SendSignal(e, SignalOf(p.Props));
			IslandObjectRef r = p.GetComponent<IslandObjectRef>();
			Debug.Log("[CUSTOM ISLANDS] Boss phase on '" + e.HostName + "': the " + p.Kind.Label + " at " + Mathf.RoundToInt(boss.networkEntity.stat_health.Value / boss.networkEntity.stat_health.Max * 100f) +
				"% health called " + called + ", speed x" + mult.ToString("0.##", CultureInfo.InvariantCulture) + ", signal '" + SignalOf(p.Props) + "'");
			if (r != null) Behaviours.FireFromHost(e, r.Index, Event);
		}

		/// <summary>Host: an animal of a spot in its second phase was spawned again (a reload mid-fight): its speed, and
		/// its health at the phase's share (no new summons, no signal).</summary>
		internal static void Resume(IslandWorldState.Entry e, CreatureSpawnPoint p, AI_NetworkBehaviour ai)
		{
			if (ai == null || !Has(p.Props) || !InPhase(e, p)) return;
			float mult = SpeedOf(p.Props);
			if (!Mathf.Approximately(mult, 1f)) CreatureSpawner.MultiplySpeed(ai, mult);
			DynamicIslands.instance.StartCoroutine(HealthAfterStart(ai, ShareOf(p.Props)));
			Debug.Log("[CUSTOM ISLANDS] Boss phase on '" + e.HostName + "' goes on after a reload: the " + p.Kind.Label + " is back in its second phase");
		}

		static IEnumerator HealthAfterStart(AI_NetworkBehaviour ai, float share)
		{
			// (after ApplyHealth, which sets the builder's health over the animal's first 3 frames)
			for (int i = 0; i < 6; i++) yield return null;
			if (ai == null || ai.networkEntity == null || ai.networkEntity.stat_health == null) yield break;
			Stat_Health h = ai.networkEntity.stat_health;
			if (h.Max > 0f && h.Value > share * h.Max) h.Value = share * h.Max;
		}

		/// <summary>Host: the spot's animals were all defeated - the fight is over; the next one has its phase again.</summary>
		internal static void OnDefeat(IslandWorldState.Entry e, CreatureSpawnPoint p)
		{
			int key = KeyOf(p);
			if (e != null && key >= 0) e.State.Remove(key);
		}
	}
}

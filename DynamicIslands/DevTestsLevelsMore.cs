using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>More tests for the level up system (LevelSystem.cs): which hits give no EXP, the Swim stat with Raft's flippers,
	/// the editor's toughness presets and the randomizer's alphas, very high levels, and a kill of every monster kind.</summary>
	public static partial class DevTests
	{
		#region IL3 - hits that give no EXP

		[ConsoleCommand(name: "CILevelNoXpHits", docs: "Dev, in game (host): no EXP from what isn't a player's hit (IL3) - on level island warthogs every other EntityType (None, Enemy, FallDamage, Environment: poison and other ticks without a hit transform, fire, falls, the world) takes health maybe but gives no EXP and no kill, also a hit that kills; a real poison buff, a poison tick, a bite, a fall and fire on the player give none; one hit with EntityType.Player is the control and does give EXP. The record is put back after")]
		public static void LevelNoXpHitsCommand() { StartTest(LevelNoXpHitsRoutine()); }

		static IEnumerator LevelNoXpHitsRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			if (!raftPos.HasValue || !Raft_Network.IsHost || player == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 5, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!PlayerLevels.On || PlayerLevels.Mine == null || host == null) { Fail("level no-EXP hits: the level system is not on"); LevelCleanup(entry); yield break; }
			try
			{
				PlayerLevels.SetMine(new LevelRecord());
				EntityType[] others = Enum.GetValues(typeof(EntityType)).Cast<EntityType>().Where(t => t != EntityType.Player).ToArray();
				Check(ref ok, others.Length >= 3, "Raft's non-player EntityTypes: " + string.Join(", ", others.Select(t => t.ToString()).ToArray()));

				// Small hits from every non-player kind (with a hit transform, and as a tick without one: how Raft's buffs hit)
				Network_Entity chip = boars[0].networkEntity;
				float max = chip.stat_health.Max;
				foreach (EntityType t in others)
					foreach (bool tick in new[] { false, true })
					{
						int xp = PlayerLevels.Mine.Xp, kills = PlayerLevels.Mine.Kills;
						float h0 = chip.stat_health.Value;
						host.DamageEntity(chip, tick ? null : chip.transform, max * 0.04f, chip.transform.position + Vector3.up, Vector3.up, t, null);
						yield return null;
						Check(ref ok, PlayerLevels.Mine.Xp == xp && PlayerLevels.Mine.Kills == kills && !chip.IsDead,
							t + (tick ? " tick" : " hit") + " on a warthog: health " + h0.ToString("F1") + " -> " + chip.stat_health.Value.ToString("F1") + ", EXP " + xp + " -> " + PlayerLevels.Mine.Xp + ", kills " + kills + " -> " + PlayerLevels.Mine.Kills);
					}

				// A hit that kills, from the world / poison: the monster may die, nobody gets EXP or a kill
				EntityType[] killers = { EntityType.None, EntityType.Environment, EntityType.FallDamage };
				for (int i = 0; i < killers.Length; i++)
				{
					Network_Entity victim = boars[i + 1].networkEntity;
					if (victim == null) break;
					int xp = PlayerLevels.Mine.Xp, kills = PlayerLevels.Mine.Kills;
					host.DamageEntity(victim, victim.transform, victim.stat_health.Max * 5f, victim.transform.position + Vector3.up, Vector3.up, killers[i], null);
					yield return new WaitForSeconds(0.2f);
					Check(ref ok, PlayerLevels.Mine.Xp == xp && PlayerLevels.Mine.Kills == kills, "a killing " + killers[i] + " hit (warthog " + (victim.IsDead ? "dead" : "alive") + "): EXP " + xp + " -> " + PlayerLevels.Mine.Xp + ", kills " + kills + " -> " + PlayerLevels.Mine.Kills);
				}

				// Being hurt yourself: nothing, whatever the kind (a real poison buff, ticks, a bite, a fall, fire)
				PlayerStats ps = player.Stats;
				int xp0 = PlayerLevels.Mine.Xp, kills0 = PlayerLevels.Mine.Kills;
				SO_Buff poison = (BuffManager.allBuffAssets ?? new SO_Buff[0]).Concat(Resources.FindObjectsOfTypeAll<SO_Buff>()).FirstOrDefault(b => b != null && b.BuffType == BuffType.Poison);
				if (poison != null)
				{
					ps.buffManager.ClearBuffs();
					ps.buffManager.AddBuff(poison);
					float hp = ps.stat_health.Value;
					yield return new WaitForSeconds(2.5f);
					Log("  (the real poison buff for 2.5 s: health " + hp.ToString("F1") + " -> " + ps.stat_health.Value.ToString("F1") + ")");
					ps.buffManager.ClearBuffs();
					KeepAlive(player);
				}
				else Log("  (no poison buff found: only the ticks below)");
				foreach (EntityType t in new[] { EntityType.None, EntityType.Enemy, EntityType.FallDamage, EntityType.Environment })
					foreach (bool tick in new[] { false, true })
						HitAndHeal(host, ps, 5f, t, tick);
				Check(ref ok, PlayerLevels.Mine.Xp == xp0 && PlayerLevels.Mine.Kills == kills0, "the player poisoned, bitten, falling, burning: EXP " + xp0 + " -> " + PlayerLevels.Mine.Xp + ", kills " + kills0 + " -> " + PlayerLevels.Mine.Kills);

				// The control: a player's hit on the last warthog gives its share
				AI_NetworkBehaviour control = boars[boars.Count - 1];
				if (control.networkEntity.IsDead) Check(ref ok, false, "the control warthog is already dead");
				else
				{
					int worth = PlayerLevels.MonsterXp(control);
					float cmax = control.networkEntity.stat_health.Max;
					int xp = PlayerLevels.Mine.Xp;
					host.DamageEntity(control.networkEntity, control.transform, cmax * 0.3f, control.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
					yield return new WaitForSeconds(0.2f);
					int gain = PlayerLevels.Mine.Xp - xp;
					Check(ref ok, gain > 0 && gain <= worth, "the control, EntityType.Player: a hit of 30% gives EXP +" + gain + " (of " + worth + ")");
				}
			}
			finally { PlayerLevels.SetMine(new LevelRecord()); if (player.Stats != null && player.Stats.buffManager != null) player.Stats.buffManager.ClearBuffs(); }
			KeepAlive(player);
			LevelCleanup(entry);
			if (ok) Log("PASS: level no-EXP hits"); else Fail("level no-EXP hits");
		}

		#endregion

		#region IL7 - Swim and Raft's flippers

		static void FlipperTry(ref bool ok, Action a, string what)
		{
			try { a(); }
			catch (Exception e) { Check(ref ok, false, what + " threw: " + (e.InnerException ?? e).Message); }
		}

		[ConsoleCommand(name: "CILevelFlippers", docs: "Dev, in game: the Swim speed stat with Raft's flippers (IL7) - 10 points in Swim multiply the player's swim speed by 1.1 without flippers and Raft's flipper speed with them (Raft's Equipment_Flipper.Equip multiplies PersonController.swimSpeed, the stat multiplies that again for the frame), Raft's own number is back after the stat and after taking the flippers off, and after 20 flippers on/off with the stat in between there is no drift. The record, the flippers and the speed are put back after")]
		public static void LevelFlippersCommand() { StartTest(LevelFlippersRoutine()); }

		static IEnumerator LevelFlippersRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (!LoadSceneManager.IsGameSceneLoaded || player == null || player.PersonController == null) { Fail("level flippers: run in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PersonController pc = player.PersonController;
			PlayerEquipment pe = player.GetComponentInChildren<PlayerEquipment>(true);
			Equipment[] eq = pe != null ? Traverse.Create(pe).Field("equipment").GetValue<Equipment[]>() : null;
			// (Raft's own flippers: the one with the smallest multiplier - the Extra upgrades' swift flippers have a copy of their own)
			Equipment_Flipper flip = eq == null ? null : eq.OfType<Equipment_Flipper>().OrderBy(f => Traverse.Create(f).Field("swimSpeedMultiplier").GetValue<float>()).FirstOrDefault();
			bool wasOn = PlayerLevels.On;
			LevelRecord kept = wasOn && PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : null;
			bool wasEquipped = flip != null && flip.Equipped;
			Slot_Equip slot0 = flip != null ? flip.equippedSlot : null;
			float swim0 = pc.swimSpeed;
			try
			{
				if (!wasOn) PlayerLevels.TurnOn(false);
				if (wasEquipped) FlipperTry(ref ok, () => flip.UnEquip(), "taking Raft's flippers off");
				float baseSwim = pc.swimSpeed;
				var r = new LevelRecord { Xp = LevelRules.TotalFor(40) };
				r.Points[LevelRules.Swim] = 10;
				PlayerLevels.SetMine(r);
				Check(ref ok, Mathf.Abs(PlayerLevels.Factor(LevelRules.Swim) - 1.1f) < 0.0001f && baseSwim > 0f, "10 points in Swim: x" + PlayerLevels.Factor(LevelRules.Swim).ToString("F2") + " on Raft's swim speed " + baseSwim.ToString("F3"));

				StatApply.Saved sv = StatApply.Boost(pc);
				Check(ref ok, sv.Set && Near(pc.swimSpeed, baseSwim * 1.1f), "no flippers: swim speed " + baseSwim.ToString("F3") + " -> " + pc.swimSpeed.ToString("F3") + " while Raft moves the player");
				StatApply.Restore(pc, sv);
				Check(ref ok, pc.swimSpeed == baseSwim, "and Raft's own number after (" + pc.swimSpeed.ToString("F3") + ")");

				if (flip == null) Log("  (no Equipment_Flipper on this player: the flipper checks are skipped)");
				else
				{
					float mult = Traverse.Create(flip).Field("swimSpeedMultiplier").GetValue<float>();
					if (!Traverse.Create(flip).Field("isInitialized").GetValue<bool>()) FlipperTry(ref ok, () => flip.Initialize(player), "initializing the flippers");
					FlipperTry(ref ok, () => flip.Equip(null), "putting Raft's flippers on");
					float with = pc.swimSpeed;
					Check(ref ok, mult > 1f && Near(with, baseSwim * mult), "Raft's flippers on: x" + mult.ToString("F2") + " -> swim speed " + with.ToString("F3") + " (want " + (baseSwim * mult).ToString("F3") + ")");
					sv = StatApply.Boost(pc);
					Check(ref ok, Near(pc.swimSpeed, with * 1.1f) && Near(pc.swimSpeed, baseSwim * mult * 1.1f), "flippers and 10 points: " + with.ToString("F3") + " -> " + pc.swimSpeed.ToString("F3") + " (Raft's flipper speed x1.1 = " + (with * 1.1f).ToString("F3") + ")");
					StatApply.Restore(pc, sv);
					Check(ref ok, pc.swimSpeed == with, "and Raft's flipper number after the stat (" + pc.swimSpeed.ToString("F3") + ")");
					FlipperTry(ref ok, () => flip.UnEquip(), "taking Raft's flippers off");
					Check(ref ok, Near(pc.swimSpeed, baseSwim), "flippers off: Raft's own number back (" + pc.swimSpeed.ToString("F4") + " vs " + baseSwim.ToString("F4") + ")");

					for (int i = 0; i < 20; i++)
					{
						FlipperTry(ref ok, () => flip.Equip(null), "flippers on (round " + i + ")");
						StatApply.Saved s2 = StatApply.Boost(pc);
						StatApply.Restore(pc, s2);
						FlipperTry(ref ok, () => flip.UnEquip(), "flippers off (round " + i + ")");
					}
					Check(ref ok, Mathf.Abs(pc.swimSpeed - baseSwim) <= 0.0001f * baseSwim, "after 20 flippers on/off with the stat in between: " + pc.swimSpeed.ToString("F6") + " vs " + baseSwim.ToString("F6") + " (no drift)");
				}
			}
			finally
			{
				try
				{
					if (flip != null && flip.Equipped) flip.UnEquip();
					if (flip != null && wasEquipped) flip.Equip(slot0);
				}
				catch (Exception e) { Log("  (putting the flippers back: " + e.Message + ")"); }
				pc.swimSpeed = swim0;
				if (kept != null) PlayerLevels.SetMine(kept); else if (!wasOn) PlayerLevels.TurnOff();
			}
			if (ok) Log("PASS: level flippers"); else Fail("level flippers");
		}

		#endregion

		#region IL13 - presets and alphas

		[ConsoleCommand(name: "CILevelPresetsXp", docs: "Dev, in game (host): the editor's toughness presets (Easy / Normal / Hard / Boss, ObjectProps.Presets) and the randomizer's alphas are worth more or less EXP from their own health and damage (IL13) - LevelRules.XpOf of Raft's warthog numbers times each preset rises Easy < Normal < Hard < Boss and an alpha (x3 health, x1.6 damage) and Big Bruce (x2.5 / x1.6) beat the plain animal; then real warthogs given each preset's health (CreatureSpawner.ApplyStats) and an alpha (WorldRandomizer.ApplyAlphaForTest) have health x the preset and PlayerLevels.MonsterXp from it, rising in the same order")]
		public static void LevelPresetsXpCommand() { StartTest(LevelPresetsXpRoutine()); }

		static IEnumerator LevelPresetsXpRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Host_Entities ents = ComponentManager<Network_Host_Entities>.Value;
			if (!raftPos.HasValue || !Raft_Network.IsHost || ents == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;

			// The numbers: Raft's warthog from its prefab, each preset on it
			AI_NetworkBehaviour prefab = (ents.AINetworkBehaviourPrefabs ?? new AI_NetworkBehaviour[0]).FirstOrDefault(a => a != null && a.behaviourType == AI_NetworkBehaviourType.Boar);
			float hp0 = 0f, dmg0 = PlayerLevels.DamageOf(prefab != null ? prefab.gameObject : null, AI_NetworkBehaviourType.Boar);
			if (prefab != null)
			{
				Network_Entity pne = prefab.networkEntity != null ? prefab.networkEntity : prefab.GetComponentInChildren<Network_Entity>(true);
				if (pne != null && pne.stat_health != null)
				{
					hp0 = pne.stat_health.Max;
					if (hp0 <= 0f) { FieldInfo mv = AccessTools.Field(typeof(Stat), "maxValue"); if (mv != null) hp0 = (float)mv.GetValue(pne.stat_health); }
				}
			}
			if (hp0 <= 0f) hp0 = 180f;
			if (dmg0 <= 0f) dmg0 = 15f;
			string[] wantNames = { "Easy", "Normal", "Hard", "Boss" };
			Check(ref ok, ObjectProps.Presets.Select(p => p.Key).SequenceEqual(wantNames), "the editor's presets: " + string.Join(", ", ObjectProps.Presets.Select(p => p.Key + " x" + p.Value[0].ToString("0.##") + "/x" + p.Value[1].ToString("0.##")).ToArray()));
			int[] presetXp = ObjectProps.Presets.Select(p => LevelRules.XpOf(hp0 * p.Value[0], dmg0 * p.Value[1])).ToArray();
			Check(ref ok, presetXp.Length == 4 && presetXp[0] < presetXp[1] && presetXp[1] < presetXp[2] && presetXp[2] < presetXp[3] && presetXp[0] >= 1,
				"a warthog (health " + hp0.ToString("F0") + ", damage " + dmg0.ToString("F0") + ") is worth " + string.Join(" < ", presetXp.Select(x => x.ToString()).ToArray()) + " EXP at Easy < Normal < Hard < Boss");
			Check(ref ok, presetXp[1] == LevelRules.XpOf(hp0, dmg0), "Normal is Raft's own animal's EXP");
			int plain = LevelRules.XpOf(hp0, dmg0), alpha = LevelRules.XpOf(hp0 * 3f, dmg0 * 1.6f);
			Check(ref ok, alpha > plain, "an alpha warthog (x3 health, x1.6 damage) is worth " + alpha + " EXP, a plain one " + plain);
			int bruce = LevelRules.XpOf(LevelRules.SharkHealth, LevelRules.SharkDamage), bigBruce = LevelRules.XpOf(LevelRules.SharkHealth * 2.5f, LevelRules.SharkDamage * 1.6f);
			Check(ref ok, bruce == LevelRules.BruceXp && bigBruce > bruce, "Big Bruce (x2.5 health, x1.6 bite) is worth " + bigBruce + " EXP, Bruce " + bruce);
			// (only health or only damage also counts)
			Check(ref ok, LevelRules.XpOf(hp0 * 4f, dmg0) > plain && LevelRules.XpOf(hp0, dmg0 * 2.5f) > plain && LevelRules.XpOf(hp0 * 0.5f, dmg0) < plain && LevelRules.XpOf(hp0, dmg0 * 0.5f) < plain, "health alone and damage alone move the EXP, up and down");

			// Real warthogs: Easy, Normal, Hard, Boss presets and an alpha
			PlayerLevels.TurnOff();
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 5, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			try
			{
				float[] baseMax = boars.Select(b => b.networkEntity.stat_health.Max).ToArray();
				for (int i = 0; i < 4; i++)
				{
					var props = new Dictionary<string, string> { { ObjectProps.CreatureHealth, ObjectProps.Presets[i].Value[0].ToString(CultureInfo.InvariantCulture) }, { ObjectProps.CreatureDamage, ObjectProps.Presets[i].Value[1].ToString(CultureInfo.InvariantCulture) } };
					CreatureSpawner.ApplyStats(boars[i], props, false);
				}
				WorldRandomizer.ApplyAlphaForTest(boars[4]);
				yield return new WaitForSeconds(0.6f);
				var real = new int[5];
				for (int i = 0; i < 5; i++)
				{
					Stat_Health h = boars[i].networkEntity.stat_health;
					float want = baseMax[i] * (i < 4 ? ObjectProps.Presets[i].Value[0] : 3f);
					real[i] = PlayerLevels.MonsterXp(boars[i]);
					float dmg = PlayerLevels.DamageOf(boars[i].gameObject, boars[i].behaviourType);
					string name = i < 4 ? ObjectProps.Presets[i].Key : "alpha";
					Check(ref ok, Mathf.Abs(h.Max - want) <= 0.5f + want * 0.01f && real[i] == LevelRules.XpOf(h.Max, dmg),
						name + " warthog: health " + baseMax[i].ToString("F0") + " -> " + h.Max.ToString("F0") + " (want " + want.ToString("F0") + "), damage " + dmg.ToString("F1") + ", worth " + real[i] + " EXP (from its own numbers: " + LevelRules.XpOf(h.Max, dmg) + ")");
				}
				Check(ref ok, real[0] < real[1] && real[1] < real[2] && real[2] < real[3], "real warthogs: Easy " + real[0] + " < Normal " + real[1] + " < Hard " + real[2] + " < Boss " + real[3] + " EXP");
				Check(ref ok, real[4] > real[1], "the alpha is worth " + real[4] + " EXP, a plain warthog " + real[1]);
			}
			finally { KeepAlive(RAPI.GetLocalPlayer()); }
			LevelCleanup(entry);
			if (ok) Log("PASS: level presets and alphas EXP"); else Fail("level presets and alphas EXP");
		}

		#endregion

		#region IL22 - high levels

		[ConsoleCommand(name: "CILevelHigh", docs: "Dev, anywhere (the page check only in a world, as the host): high levels (IL22) - a record at level 200 (3 940 300 EXP) and at 20 000 000 EXP reads, writes and reads back whole (EXP, kills, points), its level is exact (TotalFor / LevelOf agree at the edges), the points stop at all stats full (135), the level loop takes under 50 ms; in a world the stats page of such a record says 'Every stat is full' past the last point (level 69 and up), and shows the points left at level 200 with nothing spent")]
		public static void LevelHighCommand() { StartTest(LevelHighRoutine()); }

		static IEnumerator LevelHighRoutine()
		{
			bool ok = true;
			// Level 200
			int t200 = LevelRules.TotalFor(200);
			Check(ref ok, t200 == 100 + 100 * 199 * 198, "level 200 starts at " + t200 + " EXP (100 + 100 x 199 x 198)");
			Check(ref ok, LevelRules.LevelOf(t200) == 200 && LevelRules.LevelOf(t200 - 1) == 199 && LevelRules.LevelOf(t200 + LevelRules.XpFor(200) - 1) == 200 && LevelRules.LevelOf(t200 + LevelRules.XpFor(200)) == 201,
				"levels 199 / 200 / 201 start exactly where TotalFor says");
			Check(ref ok, LevelRules.PointsAt(200) == LevelRules.AllPoints && LevelRules.PointsAt(69) == LevelRules.AllPoints && LevelRules.PointsAt(68) == 134, "the points stop at " + LevelRules.AllPoints + " (level 69 and up)");
			var full = new LevelRecord { Xp = t200, Kills = 123456 };
			for (int i = 0; i < LevelRules.StatCount; i++) full.Points[i] = LevelRules.MaxPoints;
			LevelRecord back = LevelRecord.Decode(full.Encode());
			Check(ref ok, back.Xp == t200 && back.Kills == 123456 && back.Points.SequenceEqual(full.Points) && back.Level == 200 && back.Unspent == 0 && back.AllFull && back.Spent == LevelRules.AllPoints,
				"level 200, every stat full, written and read back (" + full.Encode() + ": level " + back.Level + ", " + back.Unspent + " point left)");
			var none = new LevelRecord { Xp = t200 };
			LevelRecord backNone = LevelRecord.Decode(none.Encode());
			Check(ref ok, backNone.Level == 200 && backNone.Unspent == LevelRules.AllPoints && !backNone.AllFull && backNone.Spent == 0, "level 200 with nothing spent: " + backNone.Unspent + " points to spend (not " + (199 * LevelRules.PointsPerLevel) + ")");

			// 20 000 000 EXP
			const int big = 20000000;
			int bigLevel = LevelRules.LevelOf(big);
			Check(ref ok, bigLevel > 400 && LevelRules.TotalFor(bigLevel) <= big && big < LevelRules.TotalFor(bigLevel + 1), "20 000 000 EXP is level " + bigLevel + " (it starts at " + LevelRules.TotalFor(bigLevel) + ", the next at " + LevelRules.TotalFor(bigLevel + 1) + ")");
			var huge = new LevelRecord { Xp = big, Kills = 9999999 };
			huge.Points[LevelRules.Walk] = LevelRules.MaxPoints; huge.Points[LevelRules.Oxygen] = 3;
			string line = huge.Encode();
			LevelRecord hb = LevelRecord.Decode(line);
			Check(ref ok, line.StartsWith("20000000|") && hb.Xp == big && hb.Kills == 9999999 && hb.Points.SequenceEqual(huge.Points) && hb.Level == bigLevel && hb.Unspent == LevelRules.AllPoints - 18 && !hb.AllFull,
				"a record at 20 000 000 EXP written (" + line + ") and read back whole, level " + hb.Level + ", " + hb.Unspent + " points to spend");
			Check(ref ok, LevelRecord.Decode("20000000|15,15,15,15,15,15,15,15,15|1").AllFull && Mathf.Approximately(LevelRules.Factor(LevelRules.MaxPoints * 4), 1f + LevelRules.PerPoint * LevelRules.MaxPoints), "all stats full at that level, and a stat never goes past +" + (LevelRules.MaxPoints * 100f * LevelRules.PerPoint).ToString("0") + "%");

			// The level loop stays fast
			var sw = Stopwatch.StartNew();
			int sink = 0;
			for (int i = 0; i < 1000; i++) sink += LevelRules.LevelOf(big + i);
			sw.Stop();
			double each = sw.Elapsed.TotalMilliseconds / 1000.0;
			Check(ref ok, sink > 0 && each < 50.0, "LevelOf at 20 000 000 EXP takes " + each.ToString("F4") + " ms a call (1000 calls " + sw.ElapsedMilliseconds + " ms)");
			sw.Reset(); sw.Start();
			int worst = LevelRules.LevelOf(int.MaxValue);
			sw.Stop();
			Check(ref ok, sw.Elapsed.TotalMilliseconds < 50.0, "LevelOf at the largest EXP (" + int.MaxValue + ") takes " + sw.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
			int near = LevelRules.LevelOf(2000000000);
			Check(ref ok, near > 4000 && near < 5000 && LevelRules.TotalFor(near) <= 2000000000, "2 000 000 000 EXP is level " + near);
			// (the loop adds up in an int: past about 2 146 000 000 EXP it could wrap; noted, not a failure)
			if (worst < 4000 || worst >= 9999) Log("  NOTE: LevelOf(int.MaxValue) = " + worst + " - the level loop's int total wraps past ~2.146 billion EXP (a monster is worth ~180 EXP: not reachable in play)");
			else Log("  (LevelOf(int.MaxValue) = " + worst + ")");

			// The stats page, in a world as the host
			if (LoadSceneManager.IsGameSceneLoaded && Raft_Network.IsHost)
			{
				bool wasOn = PlayerLevels.On;
				LevelRecord kept = wasOn && PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : null;
				try
				{
					if (!wasOn) PlayerLevels.TurnOn(false);
					PlayerLevels.SetMine(new LevelRecord { Xp = big, Kills = 9999999, Points = Enumerable.Repeat(LevelRules.MaxPoints, LevelRules.StatCount).ToArray() });
					LevelWindow.Open();
					yield return null;
					string shown = LevelWindow.Shown;
					Check(ref ok, shown != null && shown.StartsWith("LEVEL " + bigLevel + " | Every stat is full") && shown.Contains("Monsters defeated: 9999999"), "the stats page past the last point: " + shown);
					LevelWindow.Close();
					PlayerLevels.SetMine(new LevelRecord { Xp = t200, Kills = 5 });
					LevelWindow.Open();
					yield return null;
					shown = LevelWindow.Shown;
					Check(ref ok, shown != null && shown.StartsWith("LEVEL 200 |") && shown.Contains(LevelRules.AllPoints + "</color> stat point") && !shown.Contains("Every stat is full"), "level 200 with nothing spent: " + shown);
					LevelWindow.Close();
				}
				finally
				{
					LevelWindow.Close();
					if (kept != null) PlayerLevels.SetMine(kept); else if (!wasOn) PlayerLevels.TurnOff();
				}
			}
			else Log("  (not in a world as the host: the stats page check skipped)");
			if (ok) Log("PASS: level high levels"); else Fail("level high levels");
		}

		#endregion

		#region IL30 - a kill of every monster kind

		static readonly AI_NetworkBehaviourType[] SeaMonsters = { AI_NetworkBehaviourType.Shark, AI_NetworkBehaviourType.PufferFish, AI_NetworkBehaviourType.AnglerFish, AI_NetworkBehaviourType.Boss_Varuna };

		[ConsoleCommand(name: "CILevelKillsAll", docs: "Dev, in game (host): a kill of every monster kind Raft has (IL30) - each kind in CILevelTable that LevelRules calls a monster is created with Raft's own CreateAINetworkBehaviour (sea kinds in the sea 45 m from the raft, land kinds over the level island) and killed with one EntityType.Player hit: one kill and exactly PlayerLevels.MonsterXp of EXP each (kinds that can't be made or hit are logged and skipped); a puffer fish that explodes on its own (Raft's Explode) and one killed by EntityType.None give no kill and no EXP. The record, difficulty and animals are put back / removed after")]
		public static void LevelKillsAllCommand() { StartTest(LevelKillsAllRoutine()); }

		static IEnumerator LevelKillsAllRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host_Entities ents = ComponentManager<Network_Host_Entities>.Value;
			if (!raftPos.HasValue || !Raft_Network.IsHost || player == null || ents == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			int difficulty0 = MonsterDifficulty.Current;
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 1, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!PlayerLevels.On || PlayerLevels.Mine == null || host == null) { Fail("level kills: the level system is not on"); LevelCleanup(entry); yield break; }
			Vector3 land = boars[0].transform.position;
			var kinds = Enum.GetValues(typeof(AI_NetworkBehaviourType)).Cast<AI_NetworkBehaviourType>()
				.Where(t => LevelRules.IsMonster(t) && (ents.AINetworkBehaviourPrefabs ?? new AI_NetworkBehaviour[0]).Any(a => a != null && a.behaviourType == t)).ToList();
			Log("monster kinds in Raft's prefabs: " + string.Join(", ", kinds.Select(k => k.ToString()).ToArray()));
			var spawned = new List<AI_NetworkBehaviour>();
			var done = new List<string>(); var skipped = new List<string>();
			try
			{
				MonsterDifficulty.Current = MonsterDifficulty.Normal;
				PlayerLevels.SetMine(new LevelRecord());
				int n = 0;
				foreach (AI_NetworkBehaviourType kind in kinds)
				{
					bool sea = SeaMonsters.Contains(kind);
					float angle = (n * 53f) * Mathf.Deg2Rad;
					Vector3 pos = sea ? raftPos.Value + new Vector3(Mathf.Cos(angle) * 45f, -5f, Mathf.Sin(angle) * 45f) : land + new Vector3(2f + (n % 3) * 2f, 1.5f, 2f + (n / 3) * 2f);
					n++;
					AI_NetworkBehaviour ai = null;
					try { ai = ents.CreateAINetworkBehaviour(kind, pos, null); }
					catch (Exception e) { skipped.Add(kind + " (" + (e.InnerException ?? e).Message + ")"); continue; }
					if (ai == null) { skipped.Add(kind + " (Raft made none)"); continue; }
					spawned.Add(ai);
					Network_Entity ne = ai.networkEntity != null ? ai.networkEntity : ai.GetComponentInChildren<Network_Entity>(true);
					if (ne == null || ne.stat_health == null || ne.stat_health.Max <= 0f) { skipped.Add(kind + " (no entity to hit)"); continue; }
					if (ne.IsInvurnerable) { skipped.Add(kind + " (invulnerable)"); continue; }
					int worth = PlayerLevels.MonsterXp(ai);
					int xp0 = PlayerLevels.Mine.Xp, kills0 = PlayerLevels.Mine.Kills;
					float hp = ne.stat_health.Value;
					host.DamageEntity(ne, ne.transform, ne.stat_health.Max * 10f, ne.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
					yield return null;
					Check(ref ok, ne.IsDead && PlayerLevels.Mine.Kills == kills0 + 1 && PlayerLevels.Mine.Xp - xp0 == worth && worth >= 1,
						kind + ": health " + hp.ToString("F0") + ", one hit kills it, kills " + kills0 + " -> " + PlayerLevels.Mine.Kills + ", EXP +" + (PlayerLevels.Mine.Xp - xp0) + " (want " + worth + ")");
					done.Add(kind.ToString());
					KeepAlive(player);
				}
				Log("kinds killed: " + string.Join(", ", done.ToArray()) + (skipped.Count > 0 ? "; skipped: " + string.Join("; ", skipped.ToArray()) : ""));
				Check(ref ok, done.Count >= 3, done.Count + " monster kinds could be killed (at least 3 wanted)");

				// A puffer fish: killed by something that isn't a player, and exploding on its own
				if (kinds.Contains(AI_NetworkBehaviourType.PufferFish))
				{
					Vector3 sea = raftPos.Value + new Vector3(-45f, -5f, 20f);
					AI_NetworkBehaviour fish = null;
					try { fish = ents.CreateAINetworkBehaviour(AI_NetworkBehaviourType.PufferFish, sea, null); } catch (Exception e) { Log("  (puffer fish: " + (e.InnerException ?? e).Message + ")"); }
					if (fish != null && fish.networkEntity != null && fish.networkEntity.stat_health != null)
					{
						spawned.Add(fish);
						int xp = PlayerLevels.Mine.Xp, kills = PlayerLevels.Mine.Kills;
						host.DamageEntity(fish.networkEntity, fish.transform, fish.networkEntity.stat_health.Max * 10f, fish.transform.position, Vector3.up, EntityType.None, null);
						yield return new WaitForSeconds(0.3f);
						Check(ref ok, PlayerLevels.Mine.Xp == xp && PlayerLevels.Mine.Kills == kills, "a puffer fish killed by EntityType.None (" + (fish.networkEntity.IsDead ? "dead" : "alive") + "): no kill, no EXP");
					}
					AI_NetworkBehaviour boom = null;
					try { boom = ents.CreateAINetworkBehaviour(AI_NetworkBehaviourType.PufferFish, raftPos.Value + new Vector3(45f, -5f, -20f), null); } catch (Exception e) { Log("  (puffer fish: " + (e.InnerException ?? e).Message + ")"); }
					AI_State_PufferFish_Explode explode = boom != null ? boom.GetComponentInChildren<AI_State_PufferFish_Explode>(true) : null;
					if (boom == null || explode == null) Check(ref ok, false, "a puffer fish with Raft's explosion state (" + (boom != null) + " / " + (explode != null) + ")");
					else
					{
						spawned.Add(boom);
						int xp = PlayerLevels.Mine.Xp, kills = PlayerLevels.Mine.Kills;
						explode.Explode(boom.transform.position);
						float t0 = Time.realtimeSinceStartup;
						while (boom != null && Time.realtimeSinceStartup - t0 < 8f) yield return new WaitForSeconds(0.25f);
						Check(ref ok, PlayerLevels.Mine.Xp == xp && PlayerLevels.Mine.Kills == kills, "a puffer fish exploding on its own (" + (boom == null ? "gone after " + (Time.realtimeSinceStartup - t0).ToString("F1") + " s" : "still there") + "): no kill, no EXP");
						if (boom != null) Log("  (the fish did not blow up within 8 s)");
					}
				}
				else skipped.Add("PufferFish (no prefab)");
			}
			finally
			{
				MonsterDifficulty.Current = difficulty0;
				PlayerLevels.SetMine(new LevelRecord());
				foreach (AI_NetworkBehaviour a in spawned) if (a != null) { try { UnityEngine.Object.Destroy(a.gameObject); } catch { } }
			}
			KeepAlive(player);
			yield return null;
			LevelCleanup(entry);
			if (ok) Log("PASS: level kills of every monster kind"); else Fail("level kills of every monster kind");
		}

		#endregion
	}
}

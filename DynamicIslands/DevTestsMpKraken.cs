using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Two-player test of the world option Kraken arm (tools\mpkraken.ps1): the host's arm reaches player 2 the same,
	/// a rejoin gets it back, player 2's hits make it let go, and a tear takes the same floors on both machines.</summary>
	public static partial class DevTests
	{
		static HashSet<string> mpkBefore;
		static readonly List<Block> mpkMade = new List<Block>();
		static int mpkTorn = -1;

		[ConsoleCommand(name: "CIMpKraken", docs: "Dev, two players (tools\\mpkraken.ps1): the Kraken arm in multiplayer - CIMpKraken bring (host: the option on, the raft grown to 5x5, an arm out for 150 s; MPKRAKEN SIG), check (player 2: the same arm on the same floor with the same extra arms; MPKRAKEN SIG), rejoin (player 2: its arm dropped, the join reply asked again: the arm back as it was), hit (player 2: 10 hits sent to the host make it let go), tear (host: it was beaten; a new arm for 20 s; MPKRAKEN VICTIMS), torn (player 2: the same floors gone; MPKRAKEN VICTIMS + FLOORS), hosttorn (host: torn; MPKRAKEN FLOORS), done (host: options back, grown floors taken down)")]
		public static void MpKrakenCommand(string[] args)
		{
			string step = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			switch (step)
			{
				case "bring": StartTest(MpkBring()); break;
				case "check": StartTest(MpkCheck()); break;
				case "rejoin": StartTest(MpkRejoin()); break;
				case "hit": StartTest(MpkHit()); break;
				case "tear": StartTest(MpkTear()); break;
				case "torn": StartTest(MpkTorn()); break;
				case "hosttorn": StartTest(MpkHostTorn()); break;
				case "done": StartTest(MpkDone()); break;
				default: Fail("CIMpKraken bring|check|rejoin|hit|tear|torn|hosttorn|done"); break;
			}
		}

		static string MpkSig(Kraken.Arm a)
		{
			if (a == null) return "MPKRAKEN SIG none";
			return "MPKRAKEN SIG arm " + a.Number + " floor " + a.BlockIndex + " side " + a.SideIndex + " seed " + a.Seed + " need " + a.Need + " extras " + a.Extras + " stay " + Mathf.RoundToInt(a.Stay);
		}

		static string MpkVictims(Kraken.Arm a)
		{
			List<Block> v = Kraken.Victims(a.Block, a.Seed);
			return "MPKRAKEN VICTIMS " + string.Join(",", v.Select(b => b.ObjectIndex).OrderBy(x => x).Select(x => x.ToString()).ToArray());
		}

		static IEnumerator MpkWaitArm(float seconds, Func<Kraken.Arm, bool> want)
		{
			for (float t = Time.time + seconds; Time.time < t && !(Kraken.Current != null && want(Kraken.Current));) yield return null;
		}

		static IEnumerator MpkBring()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("mp kraken: bring on the host, in a world"); yield break; }
			if (mpkBefore == null) mpkBefore = new HashSet<string>(WorldOptions.Current);
			WorldOptions.Set(new HashSet<string>(WorldOptions.Current) { WorldOptions.Kraken });
			if (StormSeas.Foundations().Count < Kraken.MinFloors && !KrakenGrowRaft(RAPI.GetLocalPlayer(), mpkMade)) { Fail("mp kraken: no raft"); yield break; }
			yield return new WaitForSeconds(2f);
			Kraken.Reset();
			Kraken.TestStay = 150f;
			string said = Kraken.Bring();
			Log("mp kraken: " + said);
			if (Kraken.Current == null) { Fail("mp kraken: no arm (" + said + ")"); yield break; }
			Log(MpkSig(Kraken.Current));
			Log("PASS: mp kraken brought");
		}

		static IEnumerator MpkCheck()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("mp kraken: check on player 2"); yield break; }
			yield return MpkWaitArm(20f, a => a.Phase >= 1);
			bool ok = true;
			Kraken.Arm arm = Kraken.Current;
			Check(ref ok, WorldOptions.On(WorldOptions.Kraken), "player 2 has the option on");
			Check(ref ok, arm != null && arm.Block != null && arm.Phase == 1, "player 2 sees the arm holding a floor");
			Check(ref ok, arm != null && arm.Extras >= 1 && arm.Others.Count == arm.Extras, "with its extra arms: " + (arm != null ? arm.Extras : 0));
			Log(MpkSig(arm));
			if (ok) Log("PASS: mp kraken seen"); else Fail("mp kraken seen");
		}

		static IEnumerator MpkRejoin()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("mp kraken: rejoin on player 2"); yield break; }
			string was = MpkSig(Kraken.Current);
			Kraken.Reset();
			bool gone = Kraken.Current == null;
			IslandNetwork.Resync();
			yield return MpkWaitArm(30f, a => a.Phase >= 1);
			bool ok = true;
			Check(ref ok, gone && Kraken.Current != null && Kraken.Current.Phase == 1, "after the join reply player 2 has the arm again, holding on");
			Check(ref ok, MpkSig(Kraken.Current) == was, "the same arm: " + MpkSig(Kraken.Current));
			if (ok) Log("PASS: mp kraken rejoined"); else Fail("mp kraken rejoined");
		}

		static IEnumerator MpkHit()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("mp kraken: hit on player 2"); yield break; }
			Kraken.Arm arm = Kraken.Current;
			if (arm == null || arm.Phase != 1) { Fail("mp kraken: no arm holding on for player 2"); yield break; }
			Transform pivot = arm.transform.parent;
			Vector2Int sv = StormSeas.Sides[arm.SideIndex];
			Vector3 u = pivot.TransformDirection(new Vector3(sv.x, 0f, sv.y));
			int sent = 0;
			for (float t = Time.time + 25f; Time.time < t && arm != null && arm.Phase == 1;)
			{
				Vector3 target = pivot.TransformPoint(arm.Main.Pts[(int)(arm.Main.N * 0.62f)]);
				Vector3 from = target - u * 2.5f + Vector3.up * 0.3f;
				if (Kraken.TryHit(from, target - from, 3f)) sent++;
				yield return new WaitForSeconds(0.4f);
			}
			bool ok = true;
			Check(ref ok, arm == null || arm.Released, "player 2's hits made the arm let go (" + sent + " hits sent, " + (arm != null ? arm.Hits : -1) + " counted by the host)");
			if (ok) Log("PASS: mp kraken beaten by player 2"); else Fail("mp kraken beaten by player 2");
		}

		static IEnumerator MpkTear()
		{
			if (!Raft_Network.IsHost) { Fail("mp kraken: tear on the host"); yield break; }
			bool ok = true;
			Check(ref ok, Kraken.Beaten >= 1, "the host counted the arm beaten off by player 2 (" + Kraken.Beaten + ")");
			yield return new WaitForSeconds(Kraken.SinkSeconds + 0.5f);
			Kraken.TestStay = 20f;
			mpkTorn = Kraken.Torn;
			string said = Kraken.Bring();
			Log("mp kraken: " + said);
			Check(ref ok, Kraken.Current != null, "a new arm");
			if (Kraken.Current != null) { Log(MpkSig(Kraken.Current)); Log(MpkVictims(Kraken.Current)); }
			if (ok) Log("PASS: mp kraken tear coming"); else Fail("mp kraken tear coming");
		}

		static IEnumerator MpkTorn()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("mp kraken: torn on player 2"); yield break; }
			yield return MpkWaitArm(15f, a => a.Phase >= 1);
			Kraken.Arm arm = Kraken.Current;
			if (arm == null) { Fail("mp kraken: player 2 has no arm before the tear"); yield break; }
			Log(MpkSig(arm));
			List<Block> victims = Kraken.Victims(arm.Block, arm.Seed);
			Log(MpkVictims(arm));
			int torn = Kraken.Torn, before = StormSeas.Foundations().Count;
			var keep = StormSeas.Foundations().Except(victims).ToList();
			for (float t = Time.time + 30f; Time.time < t && Kraken.Torn == torn;) yield return null;
			for (float t = Time.time + 5f; Time.time < t && victims.Any(b => b != null);) yield return null;
			bool ok = true;
			int after = StormSeas.Foundations().Count;
			Check(ref ok, Kraken.Torn == torn + 1 && Kraken.LastTornCount == victims.Count, "player 2 heard of the tear: " + Kraken.LastTornCount + " floors (it worked out " + victims.Count + " from the seed)");
			Check(ref ok, victims.All(b => b == null) && keep.All(b => b != null), "the same floors are gone on player 2's raft, the others stay");
			Check(ref ok, after == before - victims.Count, "player 2's floors " + before + " -> " + after);
			Log("MPKRAKEN FLOORS " + after);
			if (ok) Log("PASS: mp kraken torn on player 2"); else Fail("mp kraken torn on player 2");
		}

		static IEnumerator MpkHostTorn()
		{
			if (!Raft_Network.IsHost) { Fail("mp kraken: hosttorn on the host"); yield break; }
			for (float t = Time.time + 20f; Time.time < t && Kraken.Torn == mpkTorn;) yield return null;
			bool ok = true;
			Check(ref ok, mpkTorn >= 0 && Kraken.Torn == mpkTorn + 1, "the host's arm tore (" + Kraken.LastTornCount + " floors)");
			Log("MPKRAKEN FLOORS " + StormSeas.Foundations().Count);
			if (ok) Log("PASS: mp kraken torn on the host"); else Fail("mp kraken torn on the host");
		}

		static IEnumerator MpkDone()
		{
			if (!Raft_Network.IsHost) { Fail("mp kraken: done on the host"); yield break; }
			if (mpkBefore != null) { WorldOptions.Set(mpkBefore); mpkBefore = null; }
			Kraken.TestStay = null;
			yield return new WaitForSeconds(1f);
			foreach (Block b in mpkMade) { try { if (b != null) BlockCreator.RemoveBlockNetwork(b, null, true); } catch { } }
			mpkMade.Clear();
			mpkTorn = -1;
			Log("PASS: mp kraken done (" + WorldOptions.Describe() + ")");
		}
	}
}

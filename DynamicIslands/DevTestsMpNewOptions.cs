using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Gap check 2026-10-20: the seven world options of 2026-10-20 (UFO sightings, Lightning strikes, Whale breach, Storm seas,
	/// Bioluminescent sea, Fog banks, Ghost ship) with two players. Their own tests (CIUfo, CILightning, ...) run on the host
	/// alone and catch the host's messages before they leave (IslandNetwork.Loopback), so nothing showed that player 2 gets
	/// them. Driven by tools\mpgap.ps1 -parts options: CIMpNewOptions mark (player 2), host (the host: the options on, a saucer, a
	/// bolt, a breach, a storm's broken floor, a ghost ship), check (player 2: each seen, the same options and seed, the
	/// same fog and glow nights), done (the host: the options as before, the ship gone).
	/// </summary>
	public static partial class DevTests
	{
		static int mpoUfo, mpoBolt, mpoWhale, mpoBreaks = -1;
		static HashSet<string> mpoBefore;
		static readonly string[] MpoOptions = { WorldOptions.Ufo, WorldOptions.Lightning, WorldOptions.WhaleBreach, WorldOptions.StormSeas, WorldOptions.GlowSea, WorldOptions.Fog, WorldOptions.GhostShip };

		[ConsoleCommand(name: "CIMpNewOptions", docs: "Dev, two players (tools\\mpgap.ps1 -parts options):the 10-20 world options reach player 2 - CIMpNewOptions mark (player 2: its counts now), host (the host: UFO, lightning, whale breach, storm seas, glow sea, fog banks and ghost ship on; a saucer, a bolt, a breach, a storm's broken floor and a ghost ship sent), check [seconds] (player 2: each one seen, the options and seed the host's, its ghost ship loaded and glowing), done (the host: the options as before, the ship gone)")]
		public static void MpNewOptionsCommand(string[] args)
		{
			string step = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			float seconds;
			if (args == null || args.Length < 2 || !float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds)) seconds = 45f;
			if (step == "mark") StartTest(MpoMark());
			else if (step == "host") StartTest(MpoHost());
			else if (step == "check") StartTest(MpoCheck(seconds));
			else if (step == "done") StartTest(MpoDone());
			else Fail("CIMpNewOptions mark|host|check [seconds]|done");
		}

		/// <summary>Both machines: the seed, the options on, and the fog, glow and UFO days the seed gives (the same on both
		/// machines when the host's seed arrived - the fog and the glow are worked out on each machine, never sent).</summary>
		static string MpoSig()
		{
			int seed = WorldOptions.Seed;
			var sb = new StringBuilder();
			for (int d = 0; d < 40; d++) sb.Append(FogBanks.Rolls(seed, d) ? '1' : '0');
			sb.Append('/');
			for (int d = 0; d < 40; d++) sb.Append(GlowSea.Rolls(seed, d) ? '1' : '0');
			sb.Append('/');
			for (int d = 0; d < 40; d++) sb.Append(UfoSightings.Rolls(seed, d) ? '1' : '0');
			return "MPOPT SIG seed " + seed + " options " + string.Join(",", WorldOptions.Current.OrderBy(o => o).ToArray()) + " days " + sb;
		}

		static IEnumerator MpoMark()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("mp options: mark on player 2, in the host's world"); yield break; }
			mpoUfo = UfoSightings.Started; mpoBolt = LightningStrikes.Started; mpoWhale = WhaleBreach.Started; mpoBreaks = StormSeas.Breaks;
			Log("PASS: mp options marked (saucers " + mpoUfo + ", bolts " + mpoBolt + ", breaches " + mpoWhale + ", broken floors " + mpoBreaks + ")");
		}

		static IEnumerator MpoHost()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !CustomIslandSpawner.RaftPosition.HasValue) { Fail("mp options: run in a world, as the host"); yield break; }
			bool ok = true;
			if (mpoBefore == null) mpoBefore = new HashSet<string>(WorldOptions.Current);
			var on = new HashSet<string>(WorldOptions.Current);
			foreach (string o in MpoOptions) on.Add(o);
			WorldOptions.Set(on);
			yield return new WaitForSeconds(1f);
			Log(MpoSig());

			// A saucer and a bolt now, sent as the host's own schedule sends them (not caught on the way)
			int ufo = UfoSightings.Started, bolt = LightningStrikes.Started;
			UfoSightings.Send(new UfoSaucer.Path { Hue = 0.02f, Glow = 0, Heading = 90f, Side = 120f, Height = 150f, Seconds = 20f, Spin = 1 });
			LightningStrikes.Send(new LightningStrikes.Strike { Point = new Vector3(55f, 0f, 70f), Shape = 1234 });
			Check(ref ok, UfoSightings.Started == ufo + 1 && LightningStrikes.Started == bolt + 1, "the host shows a saucer and a bolt");

			// A whale breach (kind 36)
			bool whale = WhaleBreach.StartNow(200f) || WhaleBreach.StartNow(300f);
			Check(ref ok, whale, "a whale breach started");

			// A storm's broken floor (kind 35): a real one when the raft has floors to spare, else the message alone (the
			// floor itself goes through Raft's own block sync, tested by CIStormSeas)
			int floors = StormSeas.Foundations().Count;
			string broke;
			if (floors > StormSeas.MinFoundations) broke = StormSeas.BreakFloor();
			else
			{
				StormSeas.Breaks++;
				IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.StormSeas, Name = "broke", Index = StormSeas.Storms, Count = StormSeas.Breaks });
				broke = "the raft has " + floors + " floors: the message alone";
			}
			Log("mp options: storm seas - " + broke);

			// A ghost ship: a nightfall forced to bring one (a generated island: it goes out with the island messages)
			yield return GhostNightfall(true);
			float until = Time.time + 40f;
			while (Time.time < until && (GhostShip.Ship == null || GhostShip.Ship.Root == null)) yield return new WaitForSeconds(0.5f);
			IslandWorldState.Entry ship = GhostShip.Ship;
			Check(ref ok, ship != null && ship.Root != null, "a ghost ship beside the raft: " + (ship != null ? ship.HostName : "none") + " (" + GhostShip.LastDecision + ")");
			Log("MPOPT HOST breaks " + StormSeas.Breaks + " floors " + StormSeas.Foundations().Count + " ship " + (ship != null ? ship.HostName : "none"));
			if (ok) Log("PASS: mp options sent"); else Fail("mp options sent");
		}

		static IEnumerator MpoCheck(float seconds)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("mp options: check on player 2, in the host's world"); yield break; }
			if (mpoBreaks < 0) { Fail("mp options: CIMpNewOptions mark first (player 2, before the host's step)"); yield break; }
			float until = Time.time + seconds;
			while (Time.time < until && !MpoAllSeen()) yield return new WaitForSeconds(0.5f);
			bool ok = true;
			Check(ref ok, MpoOptions.All(WorldOptions.On), "player 2 has the seven options on: " + WorldOptions.Describe());
			Check(ref ok, UfoSightings.Started > mpoUfo, "player 2 saw the host's saucer (" + (UfoSightings.Started - mpoUfo) + ")");
			Check(ref ok, LightningStrikes.Started > mpoBolt, "player 2 saw the host's bolt (" + (LightningStrikes.Started - mpoBolt) + ")");
			Check(ref ok, WhaleBreach.Started > mpoWhale, "player 2 saw the host's whale breach (" + (WhaleBreach.Started - mpoWhale) + ")");
			Check(ref ok, StormSeas.Breaks > mpoBreaks, "player 2 got the storm's broken floor: " + mpoBreaks + " -> " + StormSeas.Breaks);
			IslandWorldState.Entry e = GhostShip.Ship;
			Check(ref ok, e != null && e.Root != null, "player 2 has the ghost ship: " + (e == null ? "none" : e.HostName + (e.Root != null ? " (loaded)" : " (not loaded)")));
			Check(ref ok, e != null && e.Root != null && e.Root.transform.Find("GhostShipGlow") != null, "its glow on player 2's machine");
			Log("MPOPT CLIENT breaks " + StormSeas.Breaks + " floors " + StormSeas.Foundations().Count + " ship " + (e != null ? e.HostName : "none"));
			Log(MpoSig());
			mpoBreaks = -1;
			if (ok) Log("PASS: mp options seen"); else Fail("mp options seen");
		}

		static bool MpoAllSeen()
		{
			IslandWorldState.Entry e = GhostShip.Ship;
			return MpoOptions.All(WorldOptions.On) && UfoSightings.Started > mpoUfo && LightningStrikes.Started > mpoBolt && WhaleBreach.Started > mpoWhale &&
				StormSeas.Breaks > mpoBreaks && e != null && e.Root != null && e.Root.transform.Find("GhostShipGlow") != null;
		}

		static IEnumerator MpoDone()
		{
			if (!Raft_Network.IsHost) { Fail("mp options: done on the host"); yield break; }
			if (mpoBefore != null) { WorldOptions.Set(mpoBefore); mpoBefore = null; }
			GhostShip.TestForce = false;
			GhostShip.TestNight = null;
			// (the option off: the host takes the ship away at its next look)
			float until = Time.time + 10f;
			while (Time.time < until && GhostShip.Ship != null && !WorldOptions.On(WorldOptions.GhostShip)) yield return new WaitForSeconds(0.5f);
			IslandWorldState.Entry e = GhostShip.Ship;
			if (e != null && !WorldOptions.On(WorldOptions.GhostShip)) GhostShip.Remove(e, "the test is done");
			Log("PASS: mp options done (" + WorldOptions.Describe() + ")");
		}
	}
}

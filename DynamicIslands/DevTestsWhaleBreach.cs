using System.Collections;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// The world option Whale breach (WhaleBreach): CIWhaleBreach starts a breach now, takes pictures mid-breach and of the
	/// splash (play_whale_*.jpg), and checks the whale rose above the sea, fell back with a splash, and is gone afterwards.
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIWhaleBreach", docs: "Dev, in game (host): the world option Whale breach. 'CIWhaleBreach [distance m, default 200]' starts a breach now (with the option on or off), takes pictures mid-breach and of the splash (play_whale_*.jpg, from the raft and from 70 m), and checks the whale rose above the sea, splashed, and was removed afterwards")]
		public static void WhaleBreachCommand(string[] args)
		{
			float distance;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out distance)) distance = 200f;
			StartTest(WhaleBreachRoutine(distance));
		}

		static IEnumerator WhaleBreachRoutine(float distance)
		{
			Network_Player me = RAPI.GetLocalPlayer();
			if (me == null || ComponentManager<Raft>.Value == null) { Fail("whale breach: needs a world with a raft"); yield break; }
			if (!Raft_Network.IsHost) { Fail("whale breach: run it on the host"); yield break; }
			int finishedBefore = WhaleBreach.Finished;
			if (!WhaleBreach.StartNow(distance)) { Fail("whale breach: no breach started (no open sea near the raft?)"); yield break; }
			WhaleBreach.Show show = WhaleBreach.Current;
			if (show == null) { Fail("whale breach: started but no whale"); yield break; }
			Log("whale breach: whale " + show.Length.ToString("F1") + " m long, " + Vector3.Distance(Flat(me.transform.position), Flat(show.At)).ToString("F0") + " m from the player, option " + (WhaleBreach.On ? "on" : "off"));

			// Mid-breach: the whale high out of the water
			while (show != null && show.Age < 2.35f) yield return null;
			if (show == null) { Fail("whale breach: the whale was gone before the middle of the breach"); yield break; }
			Vector3 eye = me.transform.position + Vector3.up * 1.7f, at = show.At, look = at + Vector3.up * show.Length * 0.25f;
			Vector3 side = Flat(eye - at).normalized;
			yield return PlayPicture("whale_mid_raft", eye, look);
			yield return PlayPicture("whale_mid", at + side * 70f + Vector3.up * 7f, look);

			// The fall back: the splash
			while (show != null && !show.Splashed && show.Age < WhaleBreach.Show.Duration) yield return null;
			bool splashed = show != null && show.Splashed;
			float top = show != null ? show.HighestTop : float.MinValue, length = show != null ? show.Length : 0f;
			if (splashed)
			{
				float t0 = Time.time;
				while (Time.time - t0 < 0.45f) yield return null;
				yield return PlayPicture("whale_splash", at + side * 70f + Vector3.up * 7f, at + Vector3.up * 3f);
				yield return PlayPicture("whale_splash_raft", eye, at + Vector3.up * 3f);
			}

			// Afterwards: gone
			float until = Time.time + WhaleBreach.Show.Duration + 3f;
			while (WhaleBreach.Current != null && Time.time < until) yield return null;
			if (show != null) top = Mathf.Max(top, show.HighestTop);
			yield return null;
			bool gone = WhaleBreach.Current == null && GameObject.Find("CI_WhaleBreach") == null;

			Log("whale breach: highest point " + top.ToString("F1") + " m above the sea, splash " + splashed + ", removed " + gone + ", finished " + finishedBefore + " -> " + WhaleBreach.Finished);
			if (top < Mathf.Max(3f, length * 0.25f)) Fail("whale breach: the whale rose only " + top.ToString("F1") + " m above the sea");
			else if (!splashed) Fail("whale breach: no splash when it fell back");
			else if (!gone) Fail("whale breach: the whale is still there after the breach");
			else if (WhaleBreach.Finished != finishedBefore + 1) Fail("whale breach: the breach was not counted as finished");
			else Log("PASS: whale breach rose " + top.ToString("F1") + " m above the sea, splashed and was removed");
		}

	}
}

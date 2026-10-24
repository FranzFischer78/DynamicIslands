using System;
using System.Collections;
using System.IO;
using System.Linq;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CISaveSoon", docs: "Dev test, host in a world: a quest/story step's save request saves Raft's world a few seconds later (its save files newer), and a second request within a minute waits")]
		public static void SaveSoonCommand()
		{
			StartTest(SaveSoonTest());
		}

		/// <summary>Newest write time of any file in the world's save folder (UTC), or MinValue.</summary>
		static DateTime NewestWorldFile()
		{
			try
			{
				string dir = Path.Combine(SaveAndLoad.WorldPath, SaveAndLoad.CurrentGameFileName);
				if (!Directory.Exists(dir)) return DateTime.MinValue;
				return new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Select(f => f.LastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
			}
			catch { return DateTime.MinValue; }
		}

		static IEnumerator SaveSoonTest()
		{
			if (!Raft_Network.IsHost || Raft_Network.InMenuScene) { Fail("save soon: host in a world needed"); yield break; }
			DateTime before = NewestWorldFile();
			float asked = Time.realtimeSinceStartup;
			SaveSoon.ResetForTest();
			SaveSoon.Request("test");
			if (!SaveSoon.Pending) { Fail("save soon: request not taken (quick-start game or loading?)"); yield break; }
			float end = asked + 15f;
			while (SaveSoon.LastSaved < asked && Time.realtimeSinceStartup < end) yield return new WaitForSecondsRealtime(0.25f);
			yield return new WaitForSecondsRealtime(0.5f);
			DateTime after = NewestWorldFile();
			bool saved = SaveSoon.LastSaved >= asked, newer = after > before;
			Log("  saved after " + Took(asked) + "; world files " + before.ToString("HH:mm:ss") + " -> " + after.ToString("HH:mm:ss"));
			// (a second request within MinGap waits: nothing saved in the next 5 s)
			float second = SaveSoon.LastSaved;
			SaveSoon.Request("test 2");
			yield return new WaitForSecondsRealtime(5f);
			bool waited = SaveSoon.Pending && SaveSoon.LastSaved == second;
			SaveSoon.ResetForTest();
			if (saved && newer && waited) Log("PASS: save soon (saved, files newer, second request waits)");
			else Fail("save soon: saved=" + saved + " filesNewer=" + newer + " secondWaits=" + waited);
		}
	}
}

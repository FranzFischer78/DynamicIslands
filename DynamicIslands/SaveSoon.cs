using System;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Raft's world save at the right times (GUIDE 13, "Raft crashed in a world"): Raft saves only now and then, so a host who closed Raft with
	/// Alt+F4 or the window's X lost everything since. Now (host, in a world):
	/// - closing Raft saves the world first (Application.wantsToQuit; Raft's SaveGame is synchronous);
	/// - a quest step, a quest done or a story step asks for a save: a few seconds later, at most one per MinGap seconds,
	///   never while Raft saves or loads.
	/// The mod's own state goes with each save (SaveWorldPatch writes it after Raft's SaveWorld).
	/// </summary>
	public static class SaveSoon
	{
		/// <summary>Seconds between two saves asked for by the mod.</summary>
		public const float MinGap = 60f;
		/// <summary>Seconds from a request to its save (the step's other work done first).</summary>
		const float Delay = 3f;
		/// <summary>Seconds after a world loaded in which requests are ignored (steps marked while the world is read).</summary>
		const float LoadQuiet = 20f;

		static bool pending, hooked, quitting;
		static float dueAt, lastSave = -1000f, loadedAt = -1000f;

		/// <summary>Realtime of the last save this class made (tests).</summary>
		public static float LastSaved { get { return lastSave; } }
		public static bool Pending { get { return pending; } }

		public static void Hook()
		{
			if (hooked) return;
			Application.wantsToQuit += OnWantsToQuit;
			Editor.QuestTracker.AdvancedAny += OnQuestAdvanced;
			SaveAndLoad.LoadComplete += OnLoaded;
			hooked = true;
		}

		public static void Unhook()
		{
			if (!hooked) return;
			Application.wantsToQuit -= OnWantsToQuit;
			Editor.QuestTracker.AdvancedAny -= OnQuestAdvanced;
			SaveAndLoad.LoadComplete -= OnLoaded;
			hooked = false;
		}

		static void OnLoaded() { loadedAt = Time.realtimeSinceStartup; pending = false; }
		static void OnQuestAdvanced(int island, int n, int step) { Request("quest step"); }

		/// <summary>Asks for a world save soon (host only; others ignore it).</summary>
		public static void Request(string why)
		{
			try
			{
				if (!CanSave() || Time.realtimeSinceStartup - loadedAt < LoadQuiet) return;
				float at = Math.Max(Time.realtimeSinceStartup + Delay, lastSave + MinGap);
				if (!pending || at < dueAt) dueAt = at;
				if (!pending) Debug.Log("[CUSTOM ISLANDS] World save asked for (" + why + ") in " + Mathf.RoundToInt(dueAt - Time.realtimeSinceStartup) + " s");
				pending = true;
			}
			catch { }
		}

		/// <summary>From DynamicIslands.Update.</summary>
		public static void Tick()
		{
			if (!pending || Time.realtimeSinceStartup < dueAt) return;
			if (!CanSave()) { if (!Raft_Network.IsHost || Raft_Network.InMenuScene) pending = false; return; }
			if (SaveAndLoad.IsPerformingASave) { dueAt = Time.realtimeSinceStartup + 2f; return; }
			pending = false;
			SaveNow("asked for");
		}

		/// <summary>Host in a world Raft would autosave (not the main menu, not loading, not a quick-start game).</summary>
		static bool CanSave()
		{
			return Raft_Network.IsHost && !Raft_Network.InMenuScene && !SaveAndLoad.IsGameLoading && !GameManager.QuickStartGame
				&& ComponentManager<SaveAndLoad>.Value != null;
		}

		/// <summary>Raft's own save (as its autosave: world, users, clients told to save theirs). False if it threw.</summary>
		static bool SaveNow(string why)
		{
			lastSave = Time.realtimeSinceStartup;
			try
			{
				ComponentManager<SaveAndLoad>.Value.SaveGame(true);
				Debug.Log("[CUSTOM ISLANDS] World saved (" + why + ")");
				return true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] World save (" + why + ") failed: " + e.Message); return false; }
		}

		/// <summary>
		/// Raft is closing (Alt+F4, the window's X, Application.Quit): the host saves first. Raft's save is synchronous,
		/// so the quit goes on in the same call. Once per quit attempt: another handler that holds the quit (and a
		/// second close soon after) does not save again within 10 s.
		/// </summary>
		static bool OnWantsToQuit()
		{
			if (quitting) return true;
			try
			{
				if (!CanSave() || SaveAndLoad.IsPerformingASave || Time.realtimeSinceStartup - lastSave < 10f) return true;
				quitting = true;
				pending = false;
				SaveNow("Raft closing");
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Save on quit skipped: " + e.Message); }
			finally { quitting = false; }
			return true;
		}

		/// <summary>Tests: forget the last save time, so the next request saves after Delay.</summary>
		internal static void ResetForTest() { lastSave = -1000f; loadedAt = -1000f; pending = false; }
	}
}

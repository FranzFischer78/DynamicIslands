using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A world plan's own islands in Raft's story - the Receiver chain (NextTask_StoryChain.md):
	///   - Raft's story is a chain: each story island has a note that unlocks the next one's frequency on the Receiver
	///     (NoteBook.UnlockFrequency), and the host's Receiver, tuned to an unlocked frequency, brings that island
	///     (Reciever.Update -> ChunkManager.AddChunkPointForcibly). A plan can change the chain: Raft's story off (only the
	///     plan's own islands, a new adventure), story islands left out (the note before leads to the one after), and its
	///     own islands put into it - first, after an island, or in place of one. Each is "done" when its quest is (or
	///     step N, a zone, a signal, players reaching it), and that unlocks the next step.
	///   - A plan island can get its own frequency on the Receiver (where = receiver): 4 digits made for the world, an
	///     extra entry in RecieverFrequency.AllFrequencies with a mod-only island type (100 + n). Any player dials it (Raft
	///     sends the dials to the host); the host's Receiver asks Raft for an island of that type, and the mod brings the
	///     plan's island instead. Or it comes by chance while sailing (where = sailing), or ahead/near as other rules.
	///   - Raft's unlocks go through the chain on the host (the notes found are the same on every machine: Raft sends
	///     them). The host keeps the chain and where it stands in the world file (so it moves with the world to another
	///     host) and sends it to every player (network kind 19); each machine sets its notebook's unlocked list and its
	///     frequencies from it.
	/// Steps are "raft:Balboa" (StoryOrder.Key) or "rule:camp" (a plan rule's id).
	/// </summary>
	public static class StoryChain
	{
		/// <summary>Island types from here on are the mod's own frequencies (Raft's go up to 14).</summary>
		public const int ModTypeBase = 100;
		const float RetrySeconds = 20f;

		static bool active;
		/// <summary>The world's chain differs from Raft's (a plan changed it): the chain decides what the notes unlock.</summary>
		public static bool Active { get { return active; } }
		/// <summary>This world has taken its chain and rules from its plan (host), or the host's came (client).</summary>
		public static bool HasSnapshot { get; private set; }
		/// <summary>The chain, in order.</summary>
		public static readonly List<string> Steps = new List<string>();
		/// <summary>The plan's rules StoryChain handles (in the chain, on the Receiver, by chance) - kept with the world (host).</summary>
		public static readonly List<IntroRule> Rules = new List<IntroRule>();
		/// <summary>The mod's frequencies: rule id and its 4 digits; the n-th has island type ModTypeBase + n.</summary>
		public static readonly List<KeyValuePair<string, int[]>> Frequencies = new List<KeyValuePair<string, int[]>>();
		/// <summary>Steps unlocked / done (step keys); rules whose moment came / whose island is in the world (ids).</summary>
		public static readonly HashSet<string> Unlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Done = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			Fired = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Brought = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>The plan's last page of the main story (Raft's notebook), kept with the world and sent to every player.</summary>
		public static string StoryEnding = "";
		/// <summary>The plan's ending (RF2): the credits' title (the plan's name if it gives none) and its makers' names; the
		/// credits were shown in this world (the host's, kept with the world: once).</summary>
		public static string EndingTitle = "", Credits = "";
		public static bool CreditsShown;
		/// <summary>Raised on every machine when the chain, its rules or what is done changed (QuestBook listens).</summary>
		public static event Action ChainChanged;
		/// <summary>Rules by chance: the km sailed (metres) at which it comes up.</summary>
		static readonly Dictionary<string, float> due = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, float> retryAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		static readonly HashSet<string> warned = new HashSet<string>();
		static bool rebuilding, dirty;

		/// <summary>Our own call of Raft's UnlockFrequency: let it through.</summary>
		internal static bool Bypass;
		/// <summary>The last banner of the chain on this machine (tests look at it).</summary>
		public static string LastBanner { get; private set; }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [story chain] " + msg); }

		public static string RaftKey(ChunkPointType t) { return "raft:" + StoryOrder.Key(t); }
		public static string RuleKey(string id) { return "rule:" + id; }
		public static bool IsRaft(string step) { return step.StartsWith("raft:", StringComparison.OrdinalIgnoreCase); }
		public static ChunkPointType TypeOfStep(string step) { return IsRaft(step) ? StoryOrder.Parse(step.Substring(5)) : ChunkPointType.None; }
		public static string RuleIdOf(string step) { return IsRaft(step) ? null : step.Substring(5); }
		public static IntroRule RuleOf(string id) { return Rules.FirstOrDefault(r => r.Id.Equals(id ?? "", StringComparison.OrdinalIgnoreCase)); }

		/// <summary>A player's copy of the main story rules (the host sends them with the chain: the book needs their tabs).</summary>
		static readonly List<IntroRule> clientBook = new List<IntroRule>();
		/// <summary>The main story islands' rules (in the chain: their tabs in Raft's notebook) - on every machine.</summary>
		public static List<IntroRule> BookRules { get { return Raft_Network.IsHost ? Rules.Where(r => r.MainStory).Concat(beside).ToList() : clientBook.ToList(); } }
		/// <summary>Main story islands beside Raft's story (not in the chain, brought by WorldDirector): the book needs them too (host).</summary>
		static readonly List<IntroRule> beside = new List<IntroRule>();

		#region The chain

		/// <summary>
		/// The steps of a chain: Raft's story islands in Raft's order (unless the story is off or they are left out), the
		/// plan's islands in their places - "first" before everything, "instead:X" in X's place, "after:X" after X (a story
		/// island, or another rule of the chain; after one that isn't in the chain: at the end).
		/// </summary>
		public static List<string> BuildSteps(bool raftStory, ICollection<string> leaveOut, IEnumerable<IntroRule> rules)
		{
			List<IntroRule> story = rules.Where(r => r.InStory).ToList();
			var steps = new List<string>();
			steps.AddRange(story.Where(r => r.StoryPlace == "first").Select(r => RuleKey(r.Id)));
			foreach (ChunkPointType t in StoryOrder.Chain)
			{
				string key = StoryOrder.Key(t);
				List<IntroRule> instead = story.Where(r => r.StoryPlace.Equals("instead:" + key, StringComparison.OrdinalIgnoreCase)).ToList();
				if (instead.Count > 0) steps.AddRange(instead.Select(r => RuleKey(r.Id)));
				else if (raftStory && !leaveOut.Contains(key)) steps.Add(RaftKey(t));
				steps.AddRange(story.Where(r => r.StoryPlace.Equals("after:" + key, StringComparison.OrdinalIgnoreCase)).Select(r => RuleKey(r.Id)));
			}
			// After another of the plan's islands (in the plan's order; one after the other when several follow the same)
			List<IntroRule> pending = story.Where(r => r.StoryPlace.StartsWith("after:") && StoryOrder.Parse(r.StoryPlace.Substring(6)) == ChunkPointType.None).ToList();
			var placedAfter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			bool moved = true;
			while (pending.Count > 0 && moved)
			{
				moved = false;
				foreach (IntroRule r in pending.ToList())
				{
					string anchor = r.StoryPlace.Substring(6);
					int at = steps.FindIndex(s => s.Equals(RuleKey(anchor), StringComparison.OrdinalIgnoreCase));
					if (at < 0) continue;
					int n;
					placedAfter.TryGetValue(anchor, out n);
					steps.Insert(Mathf.Min(steps.Count, at + 1 + n), RuleKey(r.Id));
					placedAfter[anchor] = n + 1;
					pending.Remove(r);
					moved = true;
				}
			}
			steps.AddRange(pending.Select(r => RuleKey(r.Id)));
			return steps;
		}

		/// <summary>Whether Raft's island t is in the plan's story as Raft's own last island: Utopia, which never counts as
		/// done (no note comes after it) - an island placed after it never unlocks.</summary>
		public static bool EndsStory(WorldPlan plan, ChunkPointType t)
		{
			if (plan == null || t != ChunkPointType.Landmark_Utopia || !plan.RaftStory) return false;
			string key = StoryOrder.Key(t);
			return !plan.LeaveOut.Contains(key) && !plan.Rules.Any(r => r.InStory && r.StoryPlace.Equals("instead:" + key, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Host: the world takes its chain and rules from a plan (a new world, or WorldPlan &lt;name&gt;). What is done stays.</summary>
		public static void FromPlan(WorldPlan plan)
		{
			if (plan == null || !Raft_Network.IsHost) return;
			Rules.Clear();
			Rules.AddRange(plan.Rules.Where(r => r.Special).Select(r => r.Clone()));
			beside.Clear();
			beside.AddRange(plan.Rules.Where(r => r.Beside && !r.Special).Select(r => r.Clone()));
			active = plan.ChangesStory;
			Steps.Clear();
			Steps.AddRange(BuildSteps(plan.RaftStory, plan.LeaveOut, Rules));
			StoryEnding = plan.StoryEnding ?? "";
			EndingTitle = (plan.EndingTitle ?? "").Trim().Length > 0 ? plan.EndingTitle.Trim() : plan.Name ?? "";
			Credits = plan.Credits ?? "";
			HasSnapshot = true;
			AssignFrequencies();
			if (active || Rules.Count > 0) Log("From plan '" + plan.Name + "': " + Describe().Replace("\n", " | "));
			NoteMissingIslands();
			ResumeAfterChange();
			Changed();
		}

		/// <summary>
		/// After the chain changed under a world that is under way (the plan edited mid-game): the step after the last one
		/// done is unlocked. A step put in after a done one waited for a "done" that had already come, and taking out the
		/// unlocked step stalled the chain for good.
		/// </summary>
		static void ResumeAfterChange()
		{
			int last = -1;
			for (int k = 0; k < Steps.Count; k++) if (Done.Contains(Steps[k])) last = k;
			if (last < 0 || last + 1 >= Steps.Count) return;
			string next = Steps[last + 1];
			if (Unlocked.Contains(next) || Done.Contains(next)) return;
			Log("The chain changed: the step after " + StepName(Steps[last]) + " unlocks");
			Unlock(next, true);
		}

		/// <summary>
		/// A plan's story isn't the world's (the plan's Raft story on/off or left-out islands changed - its rules are compared
		/// on their own): the world file keeps the chain it was made with, not those plan settings.
		/// </summary>
		public static bool DiffersFrom(WorldPlan plan)
		{
			// (a world without a chain yet takes the plan's anyway when it loads: not a change)
			if (plan == null || !HasSnapshot) return false;
			return plan.ChangesStory != active || !BuildSteps(plan.RaftStory, plan.LeaveOut, plan.Rules.Where(r => r.Special)).SequenceEqual(Steps, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>Host: 4 digits for each Receiver island (from the world's id and the rule's, never one of Raft's or another's).</summary>
		static void AssignFrequencies()
		{
			var kept = Frequencies.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
			Frequencies.Clear();
			var taken = new List<int[]>();
			if (RecieverFrequency.AllFrequencies != null)
				taken.AddRange(RecieverFrequency.AllFrequencies.Where(f => f != null && (int)f.chunkPointType < ModTypeBase && f.numbers != null).Select(f => f.numbers));
			foreach (IntroRule r in Rules.Where(r => r.Where == "receiver"))
			{
				int[] digits;
				if (!kept.TryGetValue(r.Id, out digits) || digits == null || digits.Length != 4 || taken.Any(t => t.SequenceEqual(digits)))
				{
					var rnd = new System.Random(StableHash(SaveAndLoad.WorldGuid.ToString() + "/" + r.Id));
					do digits = new[] { rnd.Next(10), rnd.Next(10), rnd.Next(10), rnd.Next(10) };
					while (taken.Any(t => t.SequenceEqual(digits)));
				}
				taken.Add(digits);
				Frequencies.Add(new KeyValuePair<string, int[]>(r.Id, digits));
			}
		}

		static int StableHash(string s)
		{
			unchecked
			{
				int h = 23;
				foreach (char c in s ?? "") h = h * 31 + c;
				return h & 0x7fffffff;
			}
		}

		public static int TypeOfRule(string id)
		{
			int i = Frequencies.FindIndex(kv => kv.Key.Equals(id ?? "", StringComparison.OrdinalIgnoreCase));
			return i < 0 ? -1 : ModTypeBase + i;
		}

		public static string RuleOfType(int type)
		{
			int i = type - ModTypeBase;
			return i >= 0 && i < Frequencies.Count ? Frequencies[i].Key : null;
		}

		/// <summary>"#1234" for a Receiver island, or null.</summary>
		public static string FrequencyOf(string ruleId)
		{
			int i = Frequencies.FindIndex(kv => kv.Key.Equals(ruleId ?? "", StringComparison.OrdinalIgnoreCase));
			return i < 0 ? null : "#" + string.Concat(Frequencies[i].Value.Select(d => d.ToString(CultureInfo.InvariantCulture)).ToArray());
		}

		/// <summary>The step after this one, or null.</summary>
		public static string NextAfter(string step)
		{
			int k = Steps.FindIndex(s => s.Equals(step, StringComparison.OrdinalIgnoreCase));
			return k >= 0 && k + 1 < Steps.Count ? Steps[k + 1] : null;
		}

		/// <summary>
		/// The frequency a note of Raft's shows: the note that names Chain[i] lies on Chain[i-1] (the first is the
		/// Receiver's own), so it leads to the step after Chain[i-1] in this world's chain. "#----" when that step has
		/// no frequency (it comes another way) or there is none.
		/// </summary>
		public static string FrequencyTextForNote(ChunkPointType original)
		{
			int i = Array.IndexOf(StoryOrder.Chain, original);
			if (i < 0) return null;
			string next = i == 0 ? Steps.FirstOrDefault() : NextAfter(RaftKey(StoryOrder.Chain[i - 1]));
			if (next == null) return "#----";
			if (IsRaft(next))
			{
				ChunkPointType t = TypeOfStep(next);
				RecieverFrequency f = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.FirstOrDefault(x => x != null && x.chunkPointType == t) : null;
				return f != null ? f.ToString() : null;
			}
			return FrequencyOf(RuleIdOf(next)) ?? "#----";
		}

		/// <summary>The island a note of Raft's now leads to (ROADMAP LM1): the step after the note's island in this chain, by
		/// its name (Raft's island, or a plan island's label), or null.</summary>
		public static string NextNameForNote(ChunkPointType original)
		{
			int i = Array.IndexOf(StoryOrder.Chain, original);
			if (i < 0) return null;
			string next = i == 0 ? Steps.FirstOrDefault() : NextAfter(RaftKey(StoryOrder.Chain[i - 1]));
			if (next == null) return null;
			if (IsRaft(next)) return StoryOrder.Name(TypeOfStep(next));
			IntroRule r = RuleOf(RuleIdOf(next));
			return r != null && r.Label.Length > 0 ? r.Label : RuleIdOf(next);
		}

		#endregion

		#region Leading on (host)

		/// <summary>Host: Raft unlocked a story island's frequency - a note was found (on every machine; or a save's notes read again).</summary>
		public static void OnRaftUnlock(ChunkPointType t)
		{
			int i = Array.IndexOf(StoryOrder.Chain, t);
			if (i < 0) return;
			// (the note naming Chain[i] lies on Chain[i-1]: that island is done; the first is the Receiver's own - the start)
			if (i == 0) { if (Steps.Count > 0 && IsRaft(Steps[0])) Unlock(Steps[0], false); }
			else MarkDone(RaftKey(StoryOrder.Chain[i - 1]));
		}

		public static void MarkDone(string step)
		{
			if (!Steps.Any(s => s.Equals(step, StringComparison.OrdinalIgnoreCase)) || !Done.Add(step)) return;
			Log("Done: " + StepName(step));
			string next = NextAfter(step);
			// (a Raft island unlocked by one of the plan's islands: no note told the players, so a banner does)
			if (next != null) Unlock(next, !IsRaft(step));
			Changed();
			SaveSoon.Request("story step");
		}

		static void Unlock(string step, bool announce)
		{
			if (!Unlocked.Add(step)) return;
			Log("Unlocked: " + StepName(step));
			if (IsRaft(step) && !rebuilding)
			{
				ChunkPointType t = TypeOfStep(step);
				try { Bypass = true; NoteBook.UnlockFrequency(t); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] Unlocking " + t + ": " + e.Message); }
				finally { Bypass = false; }
				if (announce)
				{
					string text = "The Receiver picks up a new frequency: " + (FrequencyTextForRaft(t) ?? "?");
					Banner("A new signal", text);
					// (no note of Raft's gives this number - the one before now leads to the plan's island: the journal keeps it)
					try { StoryBook.AddPage("storyfreq:" + step, "A new signal", text, ""); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e.Message); }
				}
			}
			Changed();
		}

		static string FrequencyTextForRaft(ChunkPointType t)
		{
			RecieverFrequency f = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.FirstOrDefault(x => x != null && x.chunkPointType == t) : null;
			return f != null ? f.ToString() : null;
		}

		public static string StepName(string step)
		{
			if (IsRaft(step)) return StoryOrder.Name(TypeOfStep(step));
			IntroRule r = RuleOf(RuleIdOf(step));
			return "'" + RuleIdOf(step) + "'" + (r != null && r.Label.Length > 0 ? " (" + r.Label + ")" : "");
		}

		/// <summary>Every second on the host (WorldDirector.Tick).</summary>
		public static void Tick()
		{
			if (!HasSnapshot || !Raft_Network.IsHost) return;
			// (the plan's island first in the chain: unlocked from the start)
			if (active && Steps.Count > 0 && !IsRaft(Steps[0])) Unlock(Steps[0], false);
			foreach (IntroRule r in Rules.ToList())
			{
				string key = RuleKey(r.Id);
				if (r.InStory && !(active && Unlocked.Contains(key))) continue;
				if (!Fired.Contains(r.Id))
				{
					IslandWorldState.Entry at;
					if (WorldDirector.Met(r, null, out at)) Fire(r);
					continue;
				}
				if (!Brought.Contains(r.Id))
				{
					if (IslandWorldState.Islands.Any(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase))) { Brought.Add(r.Id); dirty = true; }
					else if (r.Where == "sailing") { float d; if (!due.TryGetValue(r.Id, out d) || WorldDirector.Sailed >= d) TryBring(r); }
					else if (r.Where != "receiver") TryBring(r);
					continue;
				}
				if (r.InStory && !Done.Contains(key) && IsDone(r)) MarkDone(key);
			}
			if (dirty) { dirty = false; IslandWorldState.Save(); }
		}

		/// <summary>The rule's moment came: its frequency is unlocked, or it comes up by chance from now on, or it is brought.</summary>
		static void Fire(IntroRule r)
		{
			Fired.Add(r.Id);
			Log("Rule '" + r.Id + "': " + r.Describe());
			if (r.Where == "receiver")
			{
				string title = r.Label.Length > 0 ? r.Label : "A new signal";
				string text = (r.Message.Length > 0 ? r.Message + "\n" : "") + "Tune the Receiver to " + FrequencyOf(r.Id);
				Banner(title, text);
				try { StoryBook.AddPage("storyfreq:" + r.Id, title, text.Replace("\n", " "), ""); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e.Message); }
			}
			else if (r.Where == "sailing") DueFromNow(r);
			Changed();
		}

		/// <summary>A rule by chance comes up 0.3 to 1.8 km of sailing from now (the same distance for the rule in this world).</summary>
		static void DueFromNow(IntroRule r)
		{
			due[r.Id] = WorldDirector.Sailed + 300f + (float)new System.Random(StableHash(r.Id + SaveAndLoad.WorldGuid)).NextDouble() * 1500f;
		}

		/// <summary>
		/// Host: islands were taken out of the world (RemoveIsland, IslandWorldState.RemoveIds). A chain step's island that
		/// is gone while its step isn't done was never brought again - Brought stayed set, so the rule skipped it and the step
		/// could never be done: the story stopped for good (AU50). Such a rule is "not brought" again and brings its island
		/// once more when its moment comes: at once (ahead / near), on the Receiver when it is tuned to the frequency again,
		/// or by chance after some more sailing. A rule with another island of its own still in the world, or whose step is
		/// done, keeps it as it is.
		/// </summary>
		internal static void OnIslandsRemoved(IEnumerable<IslandWorldState.Entry> gone)
		{
			if (!Raft_Network.IsHost || !HasSnapshot || gone == null) return;
			var ids = new HashSet<string>(gone.Where(e => e != null && !string.IsNullOrEmpty(e.Rule)).Select(e => e.Rule), StringComparer.OrdinalIgnoreCase);
			if (ids.Count == 0) return;
			bool changed = false;
			foreach (IntroRule r in Rules.Where(x => x.InStory && ids.Contains(x.Id) && Brought.Contains(x.Id)).ToList())
			{
				if (Done.Contains(RuleKey(r.Id))) continue;
				if (IslandWorldState.Islands.Any(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase))) continue;
				Brought.Remove(r.Id);
				retryAt.Remove(r.Id);
				if (r.Where == "sailing" && Fired.Contains(r.Id)) DueFromNow(r);
				Log("The island of '" + r.Id + "' was removed from the world before its step was done: it comes again " +
					(r.Where == "receiver" ? "when the Receiver is tuned to " + FrequencyOf(r.Id) : r.Where == "sailing" ? "by chance while sailing" : "now"));
				changed = true;
			}
			if (changed) Changed();
		}

		static void TryBring(IntroRule r)
		{
			float t;
			if (retryAt.TryGetValue(r.Id, out t) && Time.unscaledTime < t) return;
			string why = WorldDirector.Bring(r, null, null);
			if (why == null) { Brought.Add(r.Id); retryAt.Remove(r.Id); Changed(); return; }
			retryAt[r.Id] = Time.unscaledTime + RetrySeconds;
			if (warned.Add(r.Id + why)) Log("Rule '" + r.Id + "' waits: " + why);
			// (the host is told when the island isn't on this PC: the Receiver gave nothing and the story stopped, silently)
			WorldDirector.NoteMissingIf(why, r, null);
			if (WorldDirector.IsMissing(why)) GoOnWithout(r);
		}

		/// <summary>
		/// Host: a story island whose file isn't on this PC (missing, or removed since) when its moment comes: the story goes
		/// on without it - Raft's own island comes back in its place when it took one's place, else its step counts as
		/// done and the next one unlocks. It stopped the story for good.
		/// </summary>
		static void GoOnWithout(IntroRule r)
		{
			if (!active || !r.InStory) return;
			string key = RuleKey(r.Id);
			int at = Steps.FindIndex(s => s.Equals(key, StringComparison.OrdinalIgnoreCase));
			if (at < 0 || Done.Contains(key)) return;
			string name = r.Label.Length > 0 ? r.Label : r.Id;
			ChunkPointType raft = r.StoryPlace.StartsWith("instead:", StringComparison.OrdinalIgnoreCase) ? StoryOrder.Parse(r.StoryPlace.Substring(8)) : ChunkPointType.None;
			if (raft != ChunkPointType.None && !Steps.Contains(RaftKey(raft)))
			{
				Steps[at] = RaftKey(raft);
				Log("The island of '" + r.Id + "' isn't on this PC: Raft's " + StoryOrder.Name(raft) + " comes in its place");
				Banner("A new signal", "The island '" + name + "' can't come (it isn't on the host's PC): Raft's " + StoryOrder.Name(raft) + " comes in its place.");
				Unlock(Steps[at], true);
			}
			else
			{
				Log("The island of '" + r.Id + "' isn't on this PC: the story goes on without it");
				Banner("The story goes on", "The island '" + name + "' can't come (it isn't on the host's PC): the story goes on without it.");
				MarkDone(key);
			}
			Changed();
		}

		/// <summary>Host: Raft's Receiver, tuned to one of the mod's frequencies, asks for its island.</summary>
		public static void OnTuned(int type)
		{
			string id = RuleOfType(type);
			IntroRule r = RuleOf(id);
			if (r == null || !Fired.Contains(r.Id) || Brought.Contains(r.Id)) return;
			if (IslandWorldState.Islands.Any(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase))) { Brought.Add(r.Id); return; }
			TryBring(r);
		}

		/// <summary>The rule's island counts as done (its quest, step N, a zone, a signal, players at it).</summary>
		public static bool IsDone(IntroRule r)
		{
			List<IslandWorldState.Entry> entries = WorldDirector.Refs(r.Id, null);
			if (entries.Count == 0) return false;
			// (an island whose file this PC can't read has no settings: its quest looked empty, a visit counted as done and
			// the next island was unlocked - AU21. It isn't done until the file is there.)
			// (failed but there: an error after its land and objects were made - it plays, so it counts - review 2026-10-06)
			if (entries.All(e => e.Failed && e.Root == null || !System.IO.File.Exists(IslandSpawner.PathFor(e.Name)))) return false;
			string kind = r.StoryDone.Split(':')[0], arg = r.StoryDone.Contains(":") ? r.StoryDone.Substring(r.StoryDone.IndexOf(':') + 1) : "";
			if (kind.Length == 0) kind = IslandQuest.From(IslandCache.PropsOf(entries[0])).Steps.Count > 0 ? "quest" : "visit";
			if (kind == "note") return entries.Any(e => StoryBook.Pages.Any(p => p.Key.Equals("note:" + StoryBook.PageIsland(e) + ":" + arg.Trim(), StringComparison.OrdinalIgnoreCase)));
			IslandWorldState.Entry at;
			return WorldDirector.Met(new IntroRule { When = kind, WhenRef = r.Id, WhenArg = arg }, null, out at);
		}

		#endregion

		#region Every machine: the notebook and the frequencies

		/// <summary>The mod's frequencies in Raft's list (after Raft makes or restores its own, and when they change).</summary>
		public static void InstallFrequencies()
		{
			RecieverFrequency[] all = RecieverFrequency.AllFrequencies;
			if (all == null) return;
			List<RecieverFrequency> list = all.Where(f => f != null && (int)f.chunkPointType < ModTypeBase).ToList();
			for (int i = 0; i < Frequencies.Count; i++)
				list.Add(new RecieverFrequency((ChunkPointType)(ModTypeBase + i), false) { numbers = (int[])Frequencies[i].Value.Clone() });
			if (list.Count != all.Length || list.Where((f, i) => !ReferenceEquals(f, all[i])).Any()) RecieverFrequency.AllFrequencies = list.ToArray();
		}

		/// <summary>The notebook's unlocked islands from the chain (host: first the notes found, again through the chain).</summary>
		public static void Rebuild()
		{
			if (rebuilding) return;
			rebuilding = true;
			try
			{
				InstallFrequencies();
				List<ChunkPointType> unlocked = NoteBook.unlockedChunkPointType;
				if (unlocked == null) return;
				if (active && Raft_Network.IsHost) ReplayNotes();
				unlocked.RemoveAll(t => (int)t >= ModTypeBase);
				if (active)
				{
					unlocked.RemoveAll(t => StoryOrder.Chain.Contains(t));
					foreach (string s in Steps.Where(s => IsRaft(s) && Unlocked.Contains(s))) { ChunkPointType t = TypeOfStep(s); if (!unlocked.Contains(t)) unlocked.Add(t); }
				}
				for (int i = 0; i < Frequencies.Count; i++)
					if (Fired.Contains(Frequencies[i].Key)) unlocked.Add((ChunkPointType)(ModTypeBase + i));
				StoryOrder.RelabelAll();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e); }
			finally { rebuilding = false; }
		}

		/// <summary>Host: every frequency note found (a save's too) through the chain again - nothing twice.</summary>
		static void ReplayNotes()
		{
			if (NoteBook.unlockedNoteBookIndexes == null) return;
			Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
			foreach (int idx in NoteBook.unlockedNoteBookIndexes.ToList())
			{
				ChunkPointType t;
				if (notes.TryGetValue(idx, out t)) OnRaftUnlock(t);
			}
		}

		/// <summary>Something changed (host): every machine's notebook and frequencies follow, and it is saved.</summary>
		static void Changed()
		{
			dirty = true;
			if (rebuilding) return;
			Rebuild();
			Broadcast(null);
			RaiseChanged();
		}

		// (the main story's tabs travel after the chain's parts: "\u001d" ending "\u001d" rule lines joined by "\u001e")
		const char BookSep = '\u001d', RuleSep = '\u001e';

		/// <summary>Host: a banner on every machine (the quests' milestones, QuestMilestones).</summary>
		internal static void Announce(string title, string text) { Banner(title, text); }

		/// <summary>A banner on every machine (and the host's own).</summary>
		static void Banner(string title, string text)
		{
			Show(title, text);
			if (Raft_Network.IsHost) IslandNetwork.SendToEveryone(Message(title + "\n" + text));
		}

		static void Show(string title, string text)
		{
			LastBanner = title + " | " + text.Replace("\n", " | ");
			try { IslandInfo.Show(title, "", text); } catch { }
			Debug.Log("[CUSTOM ISLANDS] [story chain] Banner: " + LastBanner);
		}

		#endregion

		#region World file and network (kind 19)

		static bool wasInGame;

		/// <summary>
		/// Every frame: leaving a world puts its chain away at once. It was only replaced when the next world's island list
		/// was read - after Raft had restored that world's notebook through the old chain: a plain world loaded after a
		/// chain world showed the old frequencies, and its own notes didn't unlock.
		/// </summary>
		internal static void WatchWorld()
		{
			bool inGame = LoadSceneManager.IsGameSceneLoaded;
			if (wasInGame && !inGame && (HasSnapshot || active || Steps.Count > 0 || Frequencies.Count > 0))
			{
				Reset();
				try { InstallFrequencies(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e.Message); }
				Log("Left the world: its story chain is put away");
			}
			wasInGame = inGame;
		}

		internal static void Reset()
		{
			active = false;
			HasSnapshot = false;
			Steps.Clear();
			Rules.Clear();
			Frequencies.Clear();
			Unlocked.Clear(); Done.Clear(); Fired.Clear(); Brought.Clear();
			due.Clear(); retryAt.Clear(); warned.Clear();
			StoryEnding = "";
			EndingTitle = ""; Credits = ""; CreditsShown = false;
			clientBook.Clear();
			beside.Clear();
			LastBanner = null;
			dirty = false;
			RaiseChanged();
		}

		static void RaiseChanged()
		{
			if (ChainChanged == null) return;
			try { ChainChanged(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e); }
		}

		static string Freqs() { return string.Join(",", Frequencies.Select(kv => kv.Key.Replace(",", "").Replace(":", "") + ":" + string.Concat(kv.Value.Select(d => d.ToString(CultureInfo.InvariantCulture)).ToArray())).ToArray()); }

		static void ReadFreqs(string value)
		{
			Frequencies.Clear();
			foreach (string p in (value ?? "").Split(','))
			{
				int c = p.LastIndexOf(':');
				if (c <= 0 || p.Length - c - 1 != 4) continue;
				int[] d = p.Substring(c + 1).Select(ch => ch - '0').ToArray();
				if (d.All(x => x >= 0 && x <= 9)) Frequencies.Add(new KeyValuePair<string, int[]>(p.Substring(0, c).Trim(), d));
			}
		}

		static void ReadSet(HashSet<string> set, string value) { set.Clear(); foreach (string s in (value ?? "").Split(',')) if (s.Trim().Length > 0) set.Add(s.Trim()); }

		internal static bool ReadLine(string key, string value)
		{
			switch (key)
			{
				case "storychain": HasSnapshot = true; active = value.StartsWith("on;"); Steps.Clear(); Steps.AddRange(value.Substring(value.IndexOf(';') + 1).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)); return true;
				case "storyrule": IntroRule r = IntroRule.Parse(value); if (r != null) { HasSnapshot = true; Rules.Add(r); } return true;
				case "storyending": StoryEnding = IntroRule.UnMulti(value); return true;
				case "storyendtitle": EndingTitle = IntroRule.UnMulti(value); return true;
				case "storycredits": Credits = IntroRule.UnMulti(value); return true;
				case "storycreditsshown": CreditsShown = value.Trim() == "1"; return true;
				case "storybeside": IntroRule b = IntroRule.Parse(value); if (b != null) { HasSnapshot = true; beside.Add(b); } return true;
				case "storyfreq": ReadFreqs(value); return true;
				case "storyunlocked": ReadSet(Unlocked, value); return true;
				case "storydone": ReadSet(Done, value); return true;
				case "storyfired": ReadSet(Fired, value); return true;
				case "storybrought": ReadSet(Brought, value); return true;
				case "storydue":
					foreach (string p in value.Split(','))
					{
						int c = p.LastIndexOf(':');
						float d;
						if (c > 0 && float.TryParse(p.Substring(c + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) due[p.Substring(0, c)] = d;
					}
					return true;
			}
			return false;
		}

		internal static bool HasState { get { return HasSnapshot && (active || Rules.Count > 0 || beside.Count > 0); } }

		internal static IEnumerable<string> WriteLines()
		{
			if (!HasState) yield break;
			yield return "@storychain=" + (active ? "on" : "off") + ";" + string.Join(",", Steps.ToArray());
			foreach (IntroRule r in Rules) yield return "@storyrule=" + r.ToLine();
			if (StoryEnding.Length > 0) yield return "@storyending=" + IntroRule.Multi(StoryEnding);
			if (EndingTitle.Length > 0) yield return "@storyendtitle=" + IntroRule.Multi(EndingTitle);
			if (Credits.Length > 0) yield return "@storycredits=" + IntroRule.Multi(Credits);
			if (CreditsShown) yield return "@storycreditsshown=1";
			foreach (IntroRule r in beside) yield return "@storybeside=" + r.ToLine();
			if (Frequencies.Count > 0) yield return "@storyfreq=" + Freqs();
			if (Unlocked.Count > 0) yield return "@storyunlocked=" + string.Join(",", Unlocked.ToArray());
			if (Done.Count > 0) yield return "@storydone=" + string.Join(",", Done.ToArray());
			if (Fired.Count > 0) yield return "@storyfired=" + string.Join(",", Fired.ToArray());
			if (Brought.Count > 0) yield return "@storybrought=" + string.Join(",", Brought.ToArray());
			if (due.Count > 0) yield return "@storydue=" + string.Join(",", due.Select(kv => kv.Key + ":" + kv.Value.ToString("F0", CultureInfo.InvariantCulture)).ToArray());
		}

		/// <summary>After the world file was read (host): the notebook and the frequencies follow it.</summary>
		/// <summary>Host: the chain's islands that aren't on this PC are said at once (it was said only when someone tuned to one).</summary>
		static void NoteMissingIslands()
		{
			if (!Raft_Network.IsHost) return;
			foreach (IntroRule r in Rules.Where(x => !Brought.Contains(x.Id)))
				try { WorldDirector.NoteIfMissing(r); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e.Message); }
		}

		internal static void OnWorldRead()
		{
			if (HasSnapshot) { AssignFrequencies(); Rebuild(); }
			NoteMissingIslands();
		}

		/// <summary>The state every player needs: "on;steps|freqs|unlocked|fired|done|brought" (Name: a banner "title\ntext").</summary>
		internal static IslandNetMessage Message(string banner = null)
		{
			string data = (active ? "on" : "off") + ";" + string.Join(",", Steps.ToArray()) + "|" + Freqs() + "|" + string.Join(",", Unlocked.ToArray()) + "|" + string.Join(",", Fired.ToArray()) +
				// (done and brought since 2026-09-29: a player's world window and StoryChain never showed a step done)
				"|" + string.Join(",", Done.ToArray()) + "|" + string.Join(",", Brought.ToArray()) +
				// (the main story's rules and ending since 2026-10-04: every player's notebook shows their tabs)
				BookSep + IntroRule.Multi(StoryEnding) + BookSep + string.Join(RuleSep.ToString(), BookRules.Select(r => r.ToLine()).ToArray());
			return new IslandNetMessage { Kind = IslandNetMessage.StoryChain, Data = HasState ? data : "", Name = banner };
		}

		/// <summary>Host: the credits were shown (StoryCredits): kept with the world.</summary>
		internal static void MarkCreditsShown() { CreditsShown = true; dirty = true; }

		static void Broadcast(string banner)
		{
			if (Raft_Network.IsHost) IslandNetwork.SendToEveryone(Message(banner));
		}

		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			string data = msg.Data ?? "";
			if (data.Length == 0) { Reset(); }
			else
			{
				string[] book = data.Split(BookSep);
				data = book[0];
				StoryEnding = book.Length > 1 ? IntroRule.UnMulti(book[1]) : "";
				clientBook.Clear();
				if (book.Length > 2) foreach (string line in book[2].Split(RuleSep)) { IntroRule r = IntroRule.Parse(line); if (r != null) clientBook.Add(r); }
				string[] p = data.Split('|');
				string chain = p[0];
				active = chain.StartsWith("on;");
				HasSnapshot = true;
				Steps.Clear();
				Steps.AddRange(chain.Substring(chain.IndexOf(';') + 1).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
				ReadFreqs(p.Length > 1 ? p[1] : "");
				ReadSet(Unlocked, p.Length > 2 ? p[2] : "");
				ReadSet(Fired, p.Length > 3 ? p[3] : "");
				ReadSet(Done, p.Length > 4 ? p[4] : "");
				ReadSet(Brought, p.Length > 5 ? p[5] : "");
			}
			Rebuild();
			RaiseChanged();
			if (!string.IsNullOrEmpty(msg.Name))
			{
				int nl = msg.Name.IndexOf('\n');
				Show(nl < 0 ? msg.Name : msg.Name.Substring(0, nl), nl < 0 ? "" : msg.Name.Substring(nl + 1));
			}
		}

		/// <summary>Client: a host's world arrived: nothing of the chain until the host's comes.</summary>
		internal static void OnWorldReceived() { Reset(); }

		#endregion

		/// <summary>Blueprints the story needs (ScrambledBlueprints.Keep) that lie on this story island ("Balboa").</summary>
		public static List<string> NeededBlueprintsOn(string key)
		{
			ScrambledBlueprints.Read();
			ChunkPointType t = StoryOrder.Parse(key);
			return ScrambledBlueprints.OnIslands.Where(kv => ScrambledBlueprints.Keep.Contains(kv.Key) && kv.Value.Any(i => StoryOrder.Parse(i) == t))
				.Select(kv => kv.Key.Replace("Blueprint_", "")).ToList();
		}

		public static string Describe()
		{
			var lines = new List<string>();
			lines.Add("Story chain: " + (active ? string.Join(" > ", Steps.Select(s => StepName(s) + (Done.Contains(s) ? " (done)" : Unlocked.Contains(s) ? " (unlocked)" : "")).ToArray()) : "Raft's own"));
			foreach (IntroRule r in Rules)
				lines.Add("  " + r.Id + ": " + r.Describe() + (FrequencyOf(r.Id) != null ? " [" + FrequencyOf(r.Id) + "]" : "") +
					(Brought.Contains(r.Id) ? " - in the world" : Fired.Contains(r.Id) ? " - " + (r.Where == "receiver" ? "frequency unlocked" : "on its way") : ""));
			return string.Join("\n", lines.ToArray());
		}

		[ConsoleCommand(name: "StoryChain", docs: "The world's story chain (a world plan's own islands in Raft's story, and islands on the Receiver): what comes in which order and where it stands")]
		public static void StoryChainCommand()
		{
			foreach (string line in Describe().Split('\n')) Debug.Log("[CUSTOM ISLANDS] " + line);
		}
	}

	/// <summary>Raft unlocks a story island's frequency (a note found): through the chain when a plan changed it.</summary>
	[HarmonyPatch(typeof(NoteBook), "UnlockFrequency")]
	static class StoryChainUnlock
	{
		static bool Prefix(ChunkPointType chunkPointType)
		{
			if (!StoryChain.Active || StoryChain.Bypass || (int)chunkPointType >= StoryChain.ModTypeBase || Array.IndexOf(StoryOrder.Chain, chunkPointType) < 0) return true;
			// (the host leads on through the chain and sends every player the result; a player's own unlock waits for it)
			try { if (Raft_Network.IsHost) StoryChain.OnRaftUnlock(chunkPointType); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] Unlocking " + chunkPointType + ": " + e); }
			return false;
		}
	}

	/// <summary>The host's Receiver, tuned to one of the mod's frequencies, asks Raft for its island: the mod brings the plan's.
	/// (Raft's only caller, Reciever.Update, drops the returned point - checked in its IL, CA25 - so null is safe.)</summary>
	[HarmonyPatch(typeof(ChunkManager), "AddChunkPointForcibly")]
	static class StoryChainReceiver
	{
		static bool Prefix(ChunkPointType pointType, ref ChunkPoint __result)
		{
			if ((int)pointType < StoryChain.ModTypeBase) return true;
			__result = null;
			try { if (Raft_Network.IsHost) StoryChain.OnTuned((int)pointType); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e.Message); }
			return false;
		}
	}

	/// <summary>Raft made or restored its frequencies: the mod's go back in.</summary>
	[HarmonyPatch(typeof(RecieverFrequency), "InitializeAllFrequencies")]
	static class StoryChainFrequenciesMade
	{
		static void Postfix()
		{
			try { StoryChain.InstallFrequencies(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e); }
		}
	}

	[HarmonyPatch(typeof(RGD_RecieverFrequencies), "RestoreFrequencies")]
	static class StoryChainFrequenciesRestored
	{
		static void Postfix()
		{
			try { StoryChain.InstallFrequencies(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] " + e); }
		}
	}
}

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>What happened to one harvestable tree or pickup on a custom island.</summary>
	public class ObjectState
	{
		/// <summary>False once picked up (or a tree that hides when chopped down).</summary>
		public bool Active;
		/// <summary>Harvests left on a tree (its yield), or -1 for objects without a yield.</summary>
		public int Yield;
		/// <summary>In-game day it last changed, for regrowing.</summary>
		public int Day;
	}

	/// <summary>
	/// Remembers harvested trees and picked-up items per island, so they stay gone when the island is unloaded and
	/// loaded again, when the world is saved and loaded, and for players who join later. Objects are identified
	/// by their place in the island (the low bits of the network index IslandSpawner.RegisterNetworkIds gives them).
	/// Raft itself has no timed regrowth (its islands reset when they respawn); ours grow back after
	/// CustomIslandSpawner.RegrowDays in-game days, checked whenever the island is loaded.
	/// </summary>
	public static class IslandObjectState
	{
		static int Ordinal(PickupItem_Networked pn) { return (int)(pn.ObjectIndex & 0xFFFF); }

		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		static YieldHandler YieldOf(PickupItem_Networked pn)
		{
			PickupItem pi = pn.GetComponent<PickupItem>();
			return pi != null ? pi.yieldHandler : null;
		}

		static int FullYield(YieldHandler yh)
		{
			return yh != null && yh.yieldAsset != null && yh.yieldAsset.yieldAssets != null ? yh.yieldAsset.yieldAssets.Count : 0;
		}

		/// <summary>Records the state of a loaded island's objects into its entry (only objects that were used).</summary>
		public static void Capture(IslandWorldState.Entry e)
		{
			if (e.Root == null) return;
			int today = Today;
			foreach (PickupItem_Networked pn in e.Root.GetComponentsInChildren<PickupItem_Networked>(true))
			{
				int ord = Ordinal(pn);
				if (ord == 0) continue; // not registered
				// (a client: the host said it has grown back; it still looks used here until the island loads again)
				if (e.Regrown.Contains(ord)) { e.State.Remove(ord); continue; }
				YieldHandler yh = YieldOf(pn);
				int full = FullYield(yh);
				int left = yh != null && yh.Yield != null ? yh.Yield.Count : 0;
				bool active = pn.gameObject.activeSelf;
				if (active && (full == 0 || left >= full)) { e.State.Remove(ord); continue; }
				int yield = full > 0 ? left : -1;
				ObjectState old;
				int day = e.State.TryGetValue(ord, out old) && old.Active == active && old.Yield == yield ? old.Day : today;
				e.State[ord] = new ObjectState { Active = active, Yield = yield, Day = day };
			}
			BuriedTreasure.Capture(e, today);
		}

		/// <summary>
		/// Puts a freshly spawned island's objects into the remembered state, without effects. Only the host decides what has
		/// grown back (its regrow days, when the island loads on the host) and tells every player; a player who joined never
		/// decides it alone - their own regrow days, or the island loading on their machine and not the host's, would show them
		/// a tree the host still has cut down.
		/// </summary>
		public static void Apply(IslandWorldState.Entry e, int regrowDays)
		{
			if (e.Root == null) return;
			e.Regrown.Clear(); // (a client: what the host said has grown back shows now)
			if (e.State.Count == 0) return;
			if (Raft_Network.IsHost)
				foreach (int ord in DropRegrownKeys(e.State, regrowDays)) IslandNetwork.SendUsed(e.Id, ord, -1);
			int applied = 0;
			foreach (PickupItem_Networked pn in e.Root.GetComponentsInChildren<PickupItem_Networked>(true))
			{
				ObjectState s;
				if (!e.State.TryGetValue(Ordinal(pn), out s)) continue;
				YieldHandler yh = YieldOf(pn);
				if (s.Yield >= 0 && yh != null && yh.Yield != null)
					while (yh.Yield.Count > s.Yield) yh.Yield.RemoveAt(yh.Yield.Count - 1); // what Raft's own restore does
				pn.gameObject.SetActive(s.Active);
				pn.OnRestored();
				applied++;
			}
			if (applied > 0) Debug.Log("[CUSTOM ISLANDS] '" + e.HostName + "': " + applied + " harvested/picked-up object(s) restored");
		}

		/// <summary>Forgets objects used at least regrowDays in-game days ago (0 = never), so they come back. Creatures (CreatureSpawner) decide for themselves. Returns how many.</summary>
		public static int DropRegrown(Dictionary<int, ObjectState> state, int regrowDays) { return DropRegrownKeys(state, regrowDays).Count; }

		/// <summary>DropRegrown, returning which objects came back.</summary>
		public static List<int> DropRegrownKeys(Dictionary<int, ObjectState> state, int regrowDays)
		{
			if (regrowDays <= 0) return new List<int>();
			int today = Today;
			List<int> old = state.Where(s => s.Key < CreatureSpawner.StateKeyBase && today - s.Value.Day >= regrowDays).Select(s => s.Key).ToList();
			foreach (int ord in old) state.Remove(ord);
			return old;
		}

		#region Changes as they happen (AU63)

		// Raft finds a tree a player chops, or an item picked up, by its index (RegisterNetworkIds) - on machines where the
		// island is loaded. A copy that isn't loaded missed it, and the host's copy too: the host's state then didn't have
		// it, and the players who came later found the tree whole. Every machine watches its loaded copies and tells the
		// others what was used (IslandNetMessage.ObjectHarvest): the host keeps it in the island's state for a copy it
		// hasn't loaded and passes it on, a player keeps the host's word for a copy they haven't loaded - applied when the
		// copy loads (Apply). A loaded copy that Raft's own message still hasn't reached a few seconds later is brought up
		// to it then (only ever further used: not at once, as Raft's message on its way would chop the tree twice).

		const float WatchSeconds = 1f, CatchUpSeconds = 3f;
		static float nextWatch;

		class Seen
		{
			public GameObject Root;
			public PickupItem_Networked[] Objects;
			public Dictionary<int, ObjectState> Last;
		}

		class Behind
		{
			public IslandWorldState.Entry Entry;
			public GameObject Root;
			public int Ord;
			public ObjectState State;
			public float At;
		}

		static readonly Dictionary<IslandWorldState.Entry, Seen> seen = new Dictionary<IslandWorldState.Entry, Seen>();
		static readonly List<Behind> behind = new List<Behind>();

		/// <summary>The used objects of a loaded copy now (as Capture records them; ones the host said have grown back left out).</summary>
		static Dictionary<int, ObjectState> Look(IslandWorldState.Entry e, Seen s)
		{
			var now = new Dictionary<int, ObjectState>();
			foreach (PickupItem_Networked pn in s.Objects)
			{
				if (pn == null) continue;
				int ord = Ordinal(pn);
				if (ord == 0 || e.Regrown.Contains(ord)) continue;
				ObjectState u = UsedState(pn);
				if (u != null) now[ord] = u;
			}
			return now;
		}

		/// <summary>An object's state if it was used (picked up, or harvests taken), else null.</summary>
		static ObjectState UsedState(PickupItem_Networked pn)
		{
			YieldHandler yh = YieldOf(pn);
			int full = FullYield(yh);
			int left = yh != null && yh.Yield != null ? yh.Yield.Count : 0;
			bool active = pn.gameObject.activeSelf;
			if (active && (full == 0 || left >= full)) return null;
			return new ObjectState { Active = active, Yield = full > 0 ? left : -1 };
		}

		/// <summary>Whether b is used further than a (a is null: not used).</summary>
		static bool Further(ObjectState a, ObjectState b)
		{
			if (b == null) return false;
			if (a == null) return true;
			return (a.Active && !b.Active) || (b.Yield >= 0 && (a.Yield < 0 || b.Yield < a.Yield));
		}

		/// <summary>Every second: what was used on this machine's loaded copies goes to the others; loaded copies still behind catch up.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextWatch && Time.unscaledTime >= nextWatch - WatchSeconds) return;
			nextWatch = Time.unscaledTime + WatchSeconds;
			// (not with the tests' loopback: it collects what is sent)
			if (!IslandNetwork.InGame || IslandNetwork.Loopback != null) { seen.Clear(); behind.Clear(); return; }
			CatchUp();
			foreach (IslandWorldState.Entry gone in seen.Keys.Where(x => x.Root == null || !IslandWorldState.Islands.Contains(x)).ToList()) seen.Remove(gone);
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) continue;
				Seen s;
				if (!seen.TryGetValue(e, out s) || s.Root != e.Root)
				{
					// (first look at a copy that just loaded: what it shows came from the saved state)
					s = new Seen { Root = e.Root, Objects = e.Root.GetComponentsInChildren<PickupItem_Networked>(true) };
					s.Last = Look(e, s);
					seen[e] = s;
					continue;
				}
				Dictionary<int, ObjectState> now = Look(e, s);
				foreach (var kv in now)
				{
					ObjectState was;
					s.Last.TryGetValue(kv.Key, out was);
					if (Further(was, kv.Value)) IslandNetwork.SendHarvest(e.Id, kv.Key, kv.Value.Active, kv.Value.Yield, ContentState.Today);
				}
				s.Last = now;
			}
		}

		/// <summary>
		/// From the network: a tree or pickup was used on another machine's copy (host: from a player; a player: from the
		/// host). Not loaded here: kept in the island's state (the host keeps the further used of what it had and what came),
		/// applied when it loads. Loaded: caught up later if Raft's own message doesn't bring it. True when the host should
		/// pass it on (something here was behind it).
		/// </summary>
		internal static bool OnHarvest(int islandId, int ord, bool active, int yield, int day)
		{
			if (ord <= 0 || ord > 0xFFFF) return false;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e == null) return false;
			var came = new ObjectState { Active = active, Yield = yield < 0 ? -1 : yield, Day = day };
			// (a player: the host said it had grown back, and it was used again - this copy still shows it as before)
			if (!Raft_Network.IsHost && e.Regrown.Remove(ord)) { e.State[ord] = came; return false; }
			if (e.Root != null)
			{
				PickupItem_Networked pn = ObjectOf(e, ord);
				if (pn == null || !Further(UsedState(pn), came)) return false;
				behind.RemoveAll(b => b.Entry == e && b.Ord == ord);
				behind.Add(new Behind { Entry = e, Root = e.Root, Ord = ord, State = came, At = Time.unscaledTime });
				return true;
			}
			ObjectState had;
			e.State.TryGetValue(ord, out had);
			if (!Raft_Network.IsHost) { e.State[ord] = came; return false; } // (the host's word)
			if (!Further(had, came)) return false;
			if (had != null) came = new ObjectState { Active = had.Active && came.Active, Yield = had.Yield >= 0 && came.Yield >= 0 ? Mathf.Min(had.Yield, came.Yield) : came.Yield, Day = day };
			e.State[ord] = came;
			return true;
		}

		static PickupItem_Networked ObjectOf(IslandWorldState.Entry e, int ord)
		{
			Seen s;
			PickupItem_Networked[] all = seen.TryGetValue(e, out s) && s.Root == e.Root ? s.Objects : e.Root.GetComponentsInChildren<PickupItem_Networked>(true);
			return all.FirstOrDefault(p => p != null && Ordinal(p) == ord);
		}

		/// <summary>Loaded copies that Raft's own message didn't bring up to what happened: brought up to it now, as Apply does.</summary>
		static void CatchUp()
		{
			foreach (Behind b in behind.Where(x => Time.unscaledTime - x.At >= CatchUpSeconds || Time.unscaledTime < x.At).ToList())
			{
				behind.Remove(b);
				if (b.Entry.Root == null || b.Entry.Root != b.Root) continue; // (unloaded meanwhile: Capture kept what it showed)
				PickupItem_Networked pn = ObjectOf(b.Entry, b.Ord);
				if (pn == null || !Further(UsedState(pn), b.State)) continue;
				YieldHandler yh = YieldOf(pn);
				if (b.State.Yield >= 0 && yh != null && yh.Yield != null)
					while (yh.Yield.Count > b.State.Yield) yh.Yield.RemoveAt(yh.Yield.Count - 1);
				if (!b.State.Active) pn.gameObject.SetActive(false);
				pn.OnRestored();
				// (seen as it is now: not told to the others again)
				Seen s;
				if (seen.TryGetValue(b.Entry, out s) && s.Root == b.Root) { ObjectState u = UsedState(pn); if (u != null) s.Last[b.Ord] = u; }
				Debug.Log("[CUSTOM ISLANDS] '" + b.Entry.HostName + "': object " + b.Ord + " brought up to what another player did (Raft's own message didn't reach this copy)");
			}
		}

		#endregion

		/// <summary>Client: the host says a tree or pickup has grown back (IslandNetwork, ObjectUsed with a day below 0).</summary>
		internal static void OnRegrownFromHost(IslandWorldState.Entry e, int ord)
		{
			e.State.Remove(ord);
			if (e.Root != null && !Raft_Network.IsHost) e.Regrown.Add(ord);
		}

		/// <summary>"ordinal,active,yield,day;..." for the world file and network messages.</summary>
		public static string Encode(Dictionary<int, ObjectState> state)
		{
			var sb = new StringBuilder();
			foreach (var kv in state.OrderBy(k => k.Key))
			{
				if (sb.Length > 0) sb.Append(';');
				sb.Append(kv.Key).Append(',').Append(kv.Value.Active ? 1 : 0).Append(',').Append(kv.Value.Yield).Append(',').Append(kv.Value.Day);
			}
			return sb.ToString();
		}

		public static Dictionary<int, ObjectState> Decode(string text)
		{
			var state = new Dictionary<int, ObjectState>();
			if (string.IsNullOrEmpty(text)) return state;
			foreach (string item in text.Split(';'))
			{
				string[] p = item.Split(',');
				int ord, active, yield, day;
				if (p.Length == 4 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out ord) &&
					int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out active) &&
					int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out yield) &&
					int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out day))
					state[ord] = new ObjectState { Active = active != 0, Yield = yield, Day = day };
			}
			return state;
		}
	}
}

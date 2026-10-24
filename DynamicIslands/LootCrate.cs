using System;
using System.Collections.Generic;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Things on an island that players use up and that must stay used for everyone: looted chests (and later
	/// triggers). They live in the island's object state (saved with the world, sent to players who join) under
	/// keys above the 16 bits Raft's pickups use, and a change travels as one IslandNetMessage.ObjectUsed.
	/// </summary>
	public static class ContentState
	{
		public const int LootKeyBase = 0x20000;

		internal static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		/// <summary>The world's entry of the island this object is part of (null in the editor).</summary>
		public static IslandWorldState.Entry EntryOf(Transform t)
		{
			if (t == null) return null;
			GameObject root = t.root.gameObject;
			return IslandWorldState.Islands.FirstOrDefault(e => e.Root == root);
		}

		public static bool IsUsed(IslandWorldState.Entry e, int key)
		{
			ObjectState s;
			return e != null && e.State.TryGetValue(key, out s) && !s.Active;
		}

		/// <summary>This player used it: remember it and tell the others.</summary>
		public static void MarkUsed(Transform t, int key)
		{
			IslandWorldState.Entry e = EntryOf(t);
			if (e == null) return;
			int day = Today;
			e.State[key] = new ObjectState { Active = false, Yield = 0, Day = day };
			IslandNetwork.SendUsed(e.Id, key, day);
			AfterChange(e, key, day);
		}

		static bool IsZoneKey(int key) { return key >= TriggerZone.KeyBase && key < TriggerZone.KeyBase + 0x10000; }

		/// <summary>Host: a zone that fired wakes up the creatures waiting for it.</summary>
		static void AfterChange(IslandWorldState.Entry e, int key, int day)
		{
			if (day >= 0 && IsZoneKey(key) && Raft_Network.IsHost) CreatureSpawner.OnIslandReady(e);
		}

		/// <summary>From the network: day &lt; 0 = available again (refilled).</summary>
		public static void ApplyUsed(int islandId, int key, int day)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e == null) return;
			if (day < 0 && key < CreatureSpawner.StateKeyBase) IslandObjectState.OnRegrownFromHost(e, key); // a tree or pickup (the host decides)
			else if (day < 0) e.State.Remove(key);
			else e.State[key] = new ObjectState { Active = false, Yield = 0, Day = day };
			if (day >= 0) { if (usedByOthersAt.Count > 256) usedByOthersAt.Clear(); usedByOthersAt[((long)islandId << 32) | (uint)key] = Time.time; }
			AfterChange(e, key, day);
		}

		/// <summary>When another player's "used" of each thing came (Time.time), for "someone else got here first".</summary>
		static readonly Dictionary<long, float> usedByOthersAt = new Dictionary<long, float>();

		/// <summary>Another player used it within the last seconds: a player arriving now lost the race to it (SC50 - the
		/// host stepping into a once-zone a moment after player 2 set it off was told nothing).</summary>
		public static bool UsedByOtherLately(IslandWorldState.Entry e, int key, float seconds)
		{
			float at;
			return e != null && usedByOthersAt.TryGetValue(((long)e.Id << 32) | (uint)key, out at) && Time.time - at <= seconds;
		}

		/// <summary>
		/// Host: an island loaded; chests that were looted long enough ago fill up again (unless the builder said never),
		/// and zones re-arm. A zone with guards (a lair's, a den's: creatures waiting for it) re-arms with them: its guards
		/// that come back are back in full with it, and a zone whose guards are all gone for good (the builder said they
		/// never come back) stays as it is - re-armed, it woke nothing and said its message again (AU70).
		/// </summary>
		public static void OnIslandReady(IslandWorldState.Entry e)
		{
			if (e == null || e.Root == null || !Raft_Network.IsHost) return;
			int days = IslandRules.RegrowDays(e);
			if (days <= 0) return;
			foreach (LootCrate crate in e.Root.GetComponentsInChildren<LootCrate>(true).Where(c => c.Refills)) Refill(e, crate.StateKey, days);
			CreatureSpawnPoint[] spots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true);
			foreach (TriggerZone zone in e.Root.GetComponentsInChildren<TriggerZone>(true).Where(z => !z.OnceEver))
			{
				if (!IsDue(e, zone.StateKey, days)) continue;
				string id = zone.Id;
				List<CreatureSpawnPoint> guards = id.Length == 0 ? new List<CreatureSpawnPoint>() :
					spots.Where(p => p.Kind != null && ObjectProps.Get(p.Props, ObjectProps.CreatureZone) == id).ToList();
				if (guards.Count > 0 && guards.All(p => GoneForGood(e, p)))
				{
					Debug.Log("[CUSTOM ISLANDS] '" + e.HostName + "': zone '" + id + "' stays spent - its guards don't come back");
					continue;
				}
				// (the guards that come back come back with it: they may have been killed days after it fired)
				foreach (CreatureSpawnPoint guard in guards.Where(p => ObjectProps.Respawns(p.Props))) e.State.Remove(guard.StateKey);
				Refill(e, zone.StateKey, days);
			}
		}

		static bool IsDue(IslandWorldState.Entry e, int key, int days)
		{
			ObjectState s;
			return e.State.TryGetValue(key, out s) && Today - s.Day >= days;
		}

		/// <summary>Used long enough ago: available again, for everyone.</summary>
		static void Refill(IslandWorldState.Entry e, int key, int days)
		{
			if (!IsDue(e, key, days)) return;
			e.State.Remove(key);
			IslandNetwork.SendUsed(e.Id, key, -1);
		}

		/// <summary>A creature spot whose animals are all gone (killed or caught) and never come back (the builder's "respawns" off).</summary>
		static bool GoneForGood(IslandWorldState.Entry e, CreatureSpawnPoint p)
		{
			ObjectState s;
			return !ObjectProps.Respawns(p.Props) && e.State.TryGetValue(p.StateKey, out s) && s.Yield <= 0;
		}
	}

	/// <summary>
	/// A container on a custom island with the builder's loot. Looking at it shows Raft's "[E] Open ..." hint; opening
	/// gives the items to the player's inventory (what doesn't fit is dropped in front of them, as Raft does) and
	/// leaves it empty for everyone until it fills up again after CustomIslandSpawner.RegrowDays in-game days.
	/// </summary>
	public class LootCrate : MonoBehaviour, IRaycastable
	{
		public List<KeyValuePair<string, int>> Items = new List<KeyValuePair<string, int>>();
		public bool Refills = true;
		public string Label = "chest";
		/// <summary>Place among the island's loot containers (file order: the same on every machine).</summary>
		public int Ordinal;
		public int StateKey { get { return ContentState.LootKeyBase + Ordinal; } }

		public static LootCrate Attach(GameObject go, string name, IDictionary<string, string> props, int ordinal)
		{
			GameObject holder = CustomNote.InteractHolder(go);
			LootCrate c = holder.AddComponent<LootCrate>();
			c.Items = ObjectProps.Loot(props);
			// (a chest with story items never fills up again: the story's key or log came twice, and counts and "uses up"
			// checks went wrong)
			c.Refills = ObjectProps.LootRefills(props) && !c.Items.Any(l => StoryItems.IsStory(l.Key));
			c.Ordinal = ordinal;
			c.Label = PlaceableCatalog.DisplayName(name).ToLowerInvariant();
			return c;
		}

		public bool Looted { get { return ContentState.IsUsed(ContentState.EntryOf(transform), StateKey); } }

		void IRaycastable.OnIsRayed()
		{
			if (NoteReader.IsOpen) return;
			DisplayTextManager hints = CustomNote.Hints;
			bool looted = Looted || Items.Count == 0;
			CustomNote note = GetComponent<CustomNote>();
			if (hints != null)
			{
				if (!looted) hints.ShowText("Open the " + Label, CustomNote.InteractKey, 0, 0, true);
				else if (note != null) hints.ShowText("Read " + (note.Title.Length > 0 ? "\"" + note.Title + "\"" : "the note") + " (the " + Label + " is empty)", CustomNote.InteractKey, 0, 0, true);
				else hints.ShowText("The " + Label + " is empty", 0, true, 0);
			}
			if (!looted && CustomNote.InteractPressed())
			{
				if (hints != null) hints.HideDisplayTexts();
				Open();
			}
		}

		void IRaycastable.OnRayEnter() { }

		void IRaycastable.OnRayExit()
		{
			DisplayTextManager hints = CustomNote.Hints;
			if (hints != null) hints.HideDisplayTexts();
		}

		/// <summary>Gives the loot to the local player and marks the container empty. Returns what was given ("Plank x12").</summary>
		public List<string> Open()
		{
			if (Looted) return new List<string>();
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || player.Inventory == null) return new List<string>();
			// A locked chest the player can't open yet says so before it is claimed (claimed, it was held from the others)
			IslandObjectRef locked = GetComponentInParent<IslandObjectRef>();
			if (locked != null && !Behaviours.WouldAllow(ContentState.EntryOf(transform), locked.Index, "open"))
			{
				Behaviours.Allows(ContentState.EntryOf(transform), locked.Index, "open"); // (its "otherwise": why not)
				return new List<string>();
			}
			// One player gets the loot: a client asks the host first and opens when it says yes (Claims)
			if (!Claims.May(ContentState.EntryOf(transform), StateKey, yes => { if (this == null) return; if (yes) Open(); else Beaten(); }))
			{
				if (Raft_Network.IsHost) Beaten();
				return new List<string>();
			}
			// A locked chest: its "open" checks first (a key...); when they fail it says so and keeps its loot
			IslandObjectRef r = GetComponentInParent<IslandObjectRef>();
			if (r != null && !Behaviours.Allows(ContentState.EntryOf(transform), r.Index, "open")) return new List<string>();
			// Marked first, so a second press in the same moment can't give it twice
			ContentState.MarkUsed(transform, StateKey);
			List<string> given = TriggerZone.Give(Items); // a full inventory: the rest lands in front of the player
			LastGiven = given;
			Debug.Log("[CUSTOM ISLANDS] Opened a " + Label + ": " + string.Join(", ", given.ToArray()));
			// A note inside: show it
			CustomNote note = GetComponent<CustomNote>();
			QuestTracker.Event(ContentState.EntryOf(transform), "open", note != null ? note.Title : "");
			if (r != null) Behaviours.Fire(ContentState.EntryOf(transform), r.Index, "open", true, true); // (checked above)
			if (note != null) NoteReader.Open(note);
			return given;
		}

		/// <summary>What the last opening here gave this player (tests: a client's opening waits for the host's yes).</summary>
		public List<string> LastGiven = new List<string>();

		void Beaten()
		{
			Debug.Log("[CUSTOM ISLANDS] The " + Label + " went to another player");
			IslandInfo.ShowMessage("Someone else got to the " + Label + " first");
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A trigger zone of a custom island in a world (invisible). Every machine watches its own player: walking in shows
	/// the builder's message, gives the zone's items, and (through the host) wakes up the creatures linked to the zone.
	/// A zone fires once per world (for everyone, until it resets after the island's regrow time) or every time a
	/// player walks in (then at most every half minute per player). Its "has fired" state is shared like a looted chest.
	/// </summary>
	public class TriggerZone : MonoBehaviour
	{
		public const int KeyBase = 0x30000;
		const float RepeatCooldown = 30f;

		public string Id = "", Message = "";
		public float Radius = 6f;
		public bool Repeats;
		/// <summary>An air pocket: a player inside breathes (ObjectProps.ZoneAir) - while the zone is shown.</summary>
		public bool Air;
		/// <summary>A hazard zone (Hazards: acid, shock, darkness...): hurts the local player inside unless protected.</summary>
		public string Hazard = "";
		public Dictionary<string, string> Props = new Dictionary<string, string>();
		float nextHazard, toldHazardAt = -100f;
		/// <summary>Fires once ever: never ready again after the regrow days (a story's once-zone fired again: a double
		/// ambush, a bridge flipped back).</summary>
		public bool OnceEver;
		public List<KeyValuePair<string, int>> Items = new List<KeyValuePair<string, int>>();
		/// <summary>Place among the island's zones (file order: the same on every machine).</summary>
		public int Ordinal;
		public int StateKey { get { return KeyBase + Ordinal; } }

		/// <summary>A once-zone another player had first is tried again this often while the local player stays inside
		/// (the host holds it for them about as long: their "only if" may fail, or they walk off without setting it off).</summary>
		const float RetrySeconds = 6f;

		bool inside, toldRefused;
		/// <summary>The quest step "go to this zone" was counted for this arrival (once per walk-in).</summary>
		bool reachCounted;
		// (spent before this player came near - an earlier session's, or before the island loaded: old news, said nothing;
		// spent meanwhile by someone else: "someone else got here first" - SC50, the host losing the race was told nothing)
		bool? spentWhenSeen;
		bool firedHere;
		float nextCheck, cooldownUntil, retryAt;

		/// <summary>Raised on every machine when the local player sets a zone off (tests and quests listen).</summary>
		public static event Action<TriggerZone> Fired;

		public static TriggerZone Create(Transform parent, IslandObject o, int ordinal)
		{
			var go = new GameObject("TriggerZone_" + ordinal);
			go.transform.SetParent(parent, false);
			go.transform.position = parent.position + o.Position;
			var z = go.AddComponent<TriggerZone>();
			z.Id = ObjectProps.Get(o.Props, ObjectProps.ZoneId);
			z.Message = ObjectProps.Get(o.Props, ObjectProps.ZoneMessage);
			z.Radius = ObjectProps.Radius(o.Props);
			z.Repeats = ObjectProps.Repeats(o.Props);
			z.OnceEver = ObjectProps.Get(o.Props, ObjectProps.ZoneRepeat) == ObjectProps.ZoneOnceEver;
			z.Items = ObjectProps.Loot(o.Props);
			z.Air = ObjectProps.GetBool(o.Props, ObjectProps.ZoneAir, false);
			z.Ordinal = ordinal;
			z.Props = o.Props != null ? new Dictionary<string, string>(o.Props) : new Dictionary<string, string>();
			z.Hazard = Hazards.KindOf(z.Props);
			return z;
		}

		public bool HasFired { get { return ContentState.IsUsed(ContentState.EntryOf(transform), StateKey); } }

		void Update()
		{
			if (Time.time < nextCheck) return;
			nextCheck = Time.time + 0.25f;
			if (spentWhenSeen == null) spentWhenSeen = HasFired; // (as the zone first came: SC50)
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) return;
			bool now = (player.transform.position - transform.position).sqrMagnitude <= Radius * Radius;
			if (now && !inside) Enter();
			// (refused by the host - another player had it: tried again while the player stays inside - AU70)
			else if (now && retryAt > 0f && Time.time >= retryAt) { retryAt = 0f; Enter(false, true); }
			if (!now) { retryAt = 0f; toldRefused = false; }
			inside = now;
			if (now && Air) Breathe(player);
			if (now && Hazard.Length > 0) HazardTick(player);
		}

		/// <summary>The local player stands in a hazard zone (every quarter second): unprotected, it hurts - damage every
		/// second, breath drained in deep water, the screen dark in a dark zone - and says why at most every 6 s.</summary>
		void HazardTick(Network_Player player)
		{
			bool safe;
			try { safe = Hazards.Protected(this, player); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Hazard zone '" + Id + "': " + e.Message); safe = false; }
			if (Hazard == Hazards.Dark) { DarknessOverlay.Mark(safe); if (!safe) Tell(); return; }
			if (safe) return;
			float dmg = Hazards.DamageOf(Props);
			if (Hazard == Hazards.Deep)
			{
				try { if (player.Stats != null && player.Stats.stat_oxygen != null) player.Stats.stat_oxygen.Value = Mathf.Max(0f, player.Stats.stat_oxygen.Value - dmg * 0.25f); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Deep water: " + e.Message); }
				Tell();
				return;
			}
			if (Time.time < nextHazard || dmg <= 0f) return;
			nextHazard = Time.time + 1f;
			try { player.Stats.Damage(dmg, player.transform.position + Vector3.up, Vector3.up, EntityType.Environment, true, null); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Hazard damage: " + e.Message); }
			Tell();
		}

		void Tell()
		{
			if (Time.time - toldHazardAt < 6f) return;
			toldHazardAt = Time.time;
			string m = Hazards.MessageOf(Props);
			if (m.Length > 0) IslandInfo.ShowMessage(m);
			Debug.Log("[CUSTOM ISLANDS] Hazard zone '" + Id + "' (" + Hazard + ") hurts the player");
		}

		/// <summary>A player in an air pocket breathes: breath back to full.</summary>
		public static void Breathe(Network_Player player)
		{
			try { if (player.Stats != null && player.Stats.stat_oxygen != null) player.Stats.stat_oxygen.Value = player.Stats.stat_oxygen.Max; }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Breathing in an air pocket: " + e.Message); }
		}

		/// <summary>The local player walked in (tests call it directly). granted: again, now that the host said yes;
		/// again: tried again while the player stayed inside after a refusal (not a new arrival).</summary>
		public void Enter() { reachCounted = false; Enter(false, false); }

		void Enter(bool granted, bool again)
		{
			// A quest step "go to this zone" counts on every arrival (even when the zone itself has fired already), but
			// only once the zone's own "only if" passes (before, it counted as the player walked in: a "go to" step was
			// done at a zone that refused the player - AU41)
			bool fired = HasFired;
			if (spentWhenSeen == null) spentWhenSeen = fired;
			if (fired && !Repeats)
			{
				if ((spentWhenSeen == false || ContentState.UsedByOtherLately(ContentState.EntryOf(transform), StateKey, 30f)) && !firedHere && (Items.Count > 0 || Message.Length > 0) && !toldRefused) { toldRefused = true; IslandInfo.ShowMessage("Someone else got here first"); }
				Reached();
				return;
			}
			if (!Repeats && !granted && !again) Debug.Log("[CUSTOM ISLANDS] Trigger zone '" + Id + "' entered");
			if (Repeats && Time.time < cooldownUntil) { Reached(); return; }
			// (a zone the player can't set off yet says why before it is claimed: claimed, it was held from the others)
			IslandObjectRef self = GetComponent<IslandObjectRef>();
			if (!fired && !Repeats && !granted && self != null && !Behaviours.WouldAllow(ContentState.EntryOf(transform), self.Index, "enter"))
			{
				Behaviours.Allows(ContentState.EntryOf(transform), self.Index, "enter");
				return;
			}
			// A zone that fires once fires for one player: a client asks the host first (Claims). Refused (another player
			// has it), the player is told so and it is tried again while they stay inside (before: nothing was said, and
			// it never went off for them until they walked out and in again - AU70)
			if (!fired && !Repeats && !Claims.May(ContentState.EntryOf(transform), StateKey, yes => { if (this == null) return; if (yes) Enter(true, false); else Refused(); }))
			{
				if (Raft_Network.IsHost) Refused(); // (the host is answered at once: another player holds it)
				return;
			}
			// Its "only if" checks first: when they fail the player is told why and the zone stays ready - not marked
			// as fired, no cooldown. (Before, a failed check still used the zone up: a treasure map's X crossed
			// without the map did nothing when the player came back with it within half a minute, and a zone that
			// fires once was spent for good. Found in the two-player test.)
			IslandObjectRef r = GetComponent<IslandObjectRef>();
			IslandWorldState.Entry entry = ContentState.EntryOf(transform);
			if (r != null && !Behaviours.Allows(entry, r.Index, "enter")) return;
			cooldownUntil = Time.time + RepeatCooldown;
			if (!reachCounted) { reachCounted = true; QuestTracker.Event(entry, "reach", Id); } // (passed just above)

			if (Message.Length > 0) IslandInfo.ShowMessage(Message);
			if (Items.Count > 0) Give(Items);
			if (!fired) { firedHere = true; ContentState.MarkUsed(transform, StateKey); } // the host wakes up the linked creatures
			Debug.Log("[CUSTOM ISLANDS] Trigger zone '" + Id + "' set off" + (Message.Length > 0 ? ": " + Message : ""));
			if (r != null) Behaviours.Fire(entry, r.Index, "enter", true, true); // (checked above)
			if (Fired != null) try { Fired(this); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Zone listener: " + e.Message); }
		}

		/// <summary>Counts the quest step "go to this zone" for this arrival when the zone's "only if" passes - checked
		/// quietly (nothing taken, no "otherwise"): for a zone that doesn't fire now (fired already, cooling down, or another
		/// player's).</summary>
		void Reached()
		{
			if (reachCounted) return;
			IslandWorldState.Entry entry = ContentState.EntryOf(transform);
			IslandObjectRef r = GetComponent<IslandObjectRef>();
			if (r != null && !Behaviours.WouldAllow(entry, r.Index, "enter")) return;
			reachCounted = true;
			QuestTracker.Event(entry, "reach", Id);
		}

		/// <summary>The host gave this once-zone to another player: the local player is told once while inside, and it is
		/// tried again in a few seconds (when the other player set it off, it has fired by then and nothing happens).</summary>
		void Refused()
		{
			Debug.Log("[CUSTOM ISLANDS] Trigger zone '" + Id + "': another player has it");
			Reached(); // (the player got there: the "go to" step counts even though the zone is another player's)
			if (!toldRefused) { toldRefused = true; IslandInfo.ShowMessage("Someone else got here first"); }
			retryAt = Time.time + RetrySeconds;
		}

		/// <summary>Puts items in the local player's inventory; what doesn't fit is dropped in front of them. Story items go to the crew's journal.</summary>
		public static List<string> Give(IEnumerable<KeyValuePair<string, int>> items)
		{
			var given = new List<string>();
			Network_Player player = RAPI.GetLocalPlayer();
			PlayerInventory inv = player != null ? player.Inventory : null;
			if (inv == null) return given;
			foreach (KeyValuePair<string, int> l in items)
			{
				if (StoryItems.IsStory(l.Key)) { StoryBook.Give(l.Key, l.Value); given.Add(StoryItems.Label(l.Key) + (l.Value > 1 ? " \u00D7" + l.Value : "")); continue; }
				Item_Base item = ItemManager.GetItemByName(l.Key);
				if (item == null) { Debug.LogWarning("[CUSTOM ISLANDS] Raft has no item '" + l.Key + "'"); continue; }
				try
				{
					int before = inv.GetItemCount(l.Key);
					inv.AddItem(l.Key, l.Value);
					int left = l.Value - (inv.GetItemCount(l.Key) - before);
					if (left > 0) inv.DropItem(item, left);
					given.Add(ContentCatalog.ItemLabel(l.Key) + " \u00D7" + l.Value + (left > 0 ? " (" + left + " dropped)" : ""));
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Giving " + l.Key + " failed: " + e.Message); }
			}
			return given;
		}
	}
}

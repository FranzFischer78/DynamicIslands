using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The upgrade research table (the user 2026-10-09): the extra upgrades (ExtraUpgrades) are learned at a table of their
	/// own, not with their base item. It is a tinted copy of Raft's research table at four times its cost, learned from the start
	/// while the world has the option "upgrades" on (ExtraUpgrades.Upgrade.Table). It works as Raft's: put an item in, press
	/// Research; an upgrade can be learned once one item of each of its cost lines is researched.
	///
	/// How: Raft has one research menu (Inventory_ResearchTable) for every table. Opening the upgrade table (SetTable) turns
	/// it into the upgrade menu: Raft's own entries hidden, the upgrades' shown (Raft hides those for its own tables). The
	/// two have separate pools: an item researched at one is not researched at the other. Raft's research reaches every
	/// menu entry (ResearchMenuItem.Research): UpgradeResearchEntryPatch keeps it off the upgrades' entries, and the
	/// upgrades' research off Raft's. The research and learn buttons of the upgrade menu don't send Raft's messages: they
	/// go to the host as the mod's own (network kind 29), the host takes the item from the table, keeps the pools and
	/// sends them to everyone (and to each player who joins); the world file keeps them ("@upgraderesearch=",
	/// "@upgradeslearned="). An upgrade is craftable once learned here (ExtraUpgrades.Craftable).
	/// A world saved before the table (no "@upgradeslearned=" line, the option on) keeps what it had: the upgrades whose
	/// base item is learned there are learned (once, on the host, a few seconds after it loads).
	/// </summary>
	public static class UpgradeTable
	{
		/// <summary>Item indexes researched at the upgrade table, and the upgrades (item indexes) learned there.</summary>
		static readonly HashSet<int> researched = new HashSet<int>(), learned = new HashSet<int>();
		/// <summary>The world file had the option on but no line of the table: a world from before it (migrated once).</summary>
		static bool migrate, hasLines;
		static float migrateAt = -1f;

		public static IEnumerable<int> Researched { get { return researched; } }
		public static IEnumerable<int> Learned { get { return learned; } }
		public static bool IsLearned(ExtraUpgrades.Upgrade u) { return u != null && learned.Contains(u.Index); }
		public static bool IsResearched(Item_Base item) { return item != null && researched.Contains(item.UniqueIndex); }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [upgrade table] " + msg); }

		/// <summary>The upgrades the table teaches (every registered upgrade but the table itself).</summary>
		public static IEnumerable<ExtraUpgrades.Upgrade> Teaches { get { return ExtraUpgrades.Registered.Where(u => !u.Table); } }

		/// <summary>The item of each of an upgrade's cost lines that the research counts (Raft's research menu shows and ticks
		/// the first item of a line only - ResearchMenuItem.SetItem).</summary>
		public static IEnumerable<Item_Base> ResearchItems(ExtraUpgrades.Upgrade u)
		{
			return (u.Item.settings_recipe.NewCost ?? new CostMultiple[0]).Where(c => c.items != null && c.items.Length > 0 && c.items[0] != null).Select(c => c.items[0]);
		}

		/// <summary>The items that can be researched at the upgrade table: those in the upgrades' costs.</summary>
		public static HashSet<Item_Base> Researchable()
		{
			var set = new HashSet<Item_Base>();
			foreach (ExtraUpgrades.Upgrade u in Teaches)
				foreach (Item_Base i in ResearchItems(u)) set.Add(i);
			return set;
		}

		public static bool CanResearch(Item_Base item) { return item != null && !IsResearched(item) && Researchable().Contains(item); }

		/// <summary>Every cost line of the upgrade has one of its items researched here.</summary>
		public static bool CanLearn(ExtraUpgrades.Upgrade u)
		{
			if (u == null || u.Item == null || u.Table) return false;
			return ResearchItems(u).All(IsResearched);
		}

		#region World file, reset

		internal static void Reset() { researched.Clear(); learned.Clear(); migrate = false; hasLines = false; migrateAt = -1f; UpgradeTableMenu.Refresh(); }

		/// <summary>The world file's "@options=" line was read (WorldOptions.ReadLine): with upgrades on and no lines of the
		/// table after it, the world is from before the table.</summary>
		internal static void OptionsRead(bool upgradesOn) { migrate = upgradesOn && !hasLines; }

		internal static bool ReadLine(string key, string value)
		{
			HashSet<int> into = key == "upgraderesearch" ? researched : key == "upgradeslearned" ? learned : null;
			if (into == null) return false;
			hasLines = true;
			migrate = false;
			into.Clear();
			// (an index that is no upgrade / no researchable item - a hand edit, or an upgrade a later version dropped - is left
			// out; while nothing is registered yet everything is kept, so a world read early loses nothing)
			bool known = Teaches.Any();
			HashSet<int> ok = !known ? null : into == learned ? new HashSet<int>(Teaches.Select(u => u.Index)) : new HashSet<int>(Researchable().Select(it => it.UniqueIndex));
			foreach (int i in Ints(value)) if (ok == null || ok.Contains(i)) into.Add(i);
			UpgradeTableMenu.Refresh();
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (!WorldOptions.On(WorldOptions.Upgrades) && researched.Count == 0 && learned.Count == 0) yield break;
			yield return "@upgraderesearch=" + Join(researched);
			yield return "@upgradeslearned=" + Join(learned);
		}

		static string Join(IEnumerable<int> set) { return string.Join(",", set.OrderBy(i => i).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray()); }

		static IEnumerable<int> Ints(string s)
		{
			foreach (string p in (s ?? "").Split(','))
			{
				int i;
				if (int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) yield return i;
			}
		}

		/// <summary>Every frame in a world (ExtraUpgrades.Tick): the host migrates a world from before the table once Raft has
		/// put its learned recipes back.</summary>
		internal static void Tick()
		{
			if (!migrate || !Raft_Network.IsHost) return;
			if (migrateAt < 0f) { migrateAt = Time.time + 5f; return; }
			if (Time.time < migrateAt || ComponentManager<Network_Player>.Value == null) return;
			migrate = false;
			var kept = Teaches.Where(u => u.Base != null && u.Base.settings_recipe.Learned).ToList();
			foreach (ExtraUpgrades.Upgrade u in kept) learned.Add(u.Index);
			Log("a world from before the table: " + kept.Count + " upgrades kept learned (their base item is) - " + string.Join(", ", kept.Select(u => u.Display).ToArray()));
			Changed();
		}

		#endregion

		#region Research and learn (network kind 29)

		/// <summary>The upgrade menu's Research button: the item in the open upgrade table's slot is used up (the slot is the
		/// researching player's own until the table closes, as with Raft's research) and the host told.</summary>
		internal static bool AskResearch(Slot slot)
		{
			Item_Base item = slot != null && !slot.IsEmpty ? slot.itemInstance.baseItem : null;
			if (!CanResearch(item)) return false;
			slot.RemoveItem(1);
			UpgradeTableMenu.ResearchSound();
			if (Raft_Network.IsHost) ResearchOnHost(item.UniqueIndex, "host");
			else Send(new IslandNetMessage { Name = "research", Index = item.UniqueIndex }, null);
			return true;
		}

		/// <summary>The upgrade menu's Learn button.</summary>
		internal static void AskLearn(ExtraUpgrades.Upgrade u)
		{
			if (u == null || IsLearned(u) || !CanLearn(u)) return;
			Network_Player me = ComponentManager<Network_Player>.Value;
			if (Raft_Network.IsHost) LearnOnHost(u.Index, me != null ? me.steamID.Id : 0UL);
			else Send(new IslandNetMessage { Name = "learn", Index = u.Index }, null);
		}

		/// <summary>False when the item can't be researched (already is: another player was first).</summary>
		internal static bool ResearchOnHost(int itemIndex, string by)
		{
			Item_Base item = ItemManager.GetItemByIndex(itemIndex);
			if (!CanResearch(item)) return false;
			researched.Add(itemIndex);
			Log("researched " + item.UniqueName + " (by " + by + ")");
			Changed();
			return true;
		}

		static string NameOf(Network_UserId id)
		{
			return "player " + id.Id.ToString(CultureInfo.InvariantCulture);
		}

		internal static void LearnOnHost(int upgradeIndex, ulong by)
		{
			ExtraUpgrades.Upgrade u = ExtraUpgrades.Find(upgradeIndex);
			if (u == null || learned.Contains(upgradeIndex) || !CanLearn(u)) return;
			learned.Add(upgradeIndex);
			Log("learned " + u.Display);
			Changed();
			Send(new IslandNetMessage { Name = "learned", Index = upgradeIndex, Data = by.ToString(CultureInfo.InvariantCulture) }, null);
			UpgradeTableMenu.Learned(u, by);
		}

		/// <summary>The host's pools changed: to everyone, and saved.</summary>
		static void Changed()
		{
			UpgradeTableMenu.Refresh();
			if (Raft_Network.IsHost) { Send(Message(), null); IslandWorldState.Save(); }
		}

		internal static IslandNetMessage Message()
		{
			return new IslandNetMessage { Kind = IslandNetMessage.UpgradeTable, Name = "state", Data = State() };
		}

		static void Send(IslandNetMessage msg, Network_UserId? to) { IslandNetwork.SendUpgradeTable(msg, to); }

		internal static void OnMessage(IslandNetMessage msg, Network_UserId from)
		{
			if (Raft_Network.IsHost)
			{
				// (two players researching the same item at once: the second one's item, already taken from its slot, goes back)
				if (msg.Name == "research" && !ResearchOnHost(msg.Index, NameOf(from))) { Log("research of item " + msg.Index + " by " + NameOf(from) + " refused, item given back"); Send(new IslandNetMessage { Name = "refund", Index = msg.Index }, from); }
				else if (msg.Name == "learn") LearnOnHost(msg.Index, from.Id);
				return;
			}
			if (msg.Name == "learned")
			{
				ulong by;
				ulong.TryParse(msg.Data ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out by);
				UpgradeTableMenu.Learned(ExtraUpgrades.Find(msg.Index), by);
				return;
			}
			if (msg.Name == "refund")
			{
				Item_Base item = ItemManager.GetItemByIndex(msg.Index);
				Network_Player me = ComponentManager<Network_Player>.Value;
				if (item != null && me != null) { me.Inventory.AddItem(item.UniqueName, 1); Log("research refused (already researched): " + item.UniqueName + " given back"); }
				return;
			}
			if (msg.Name != "state") return;
			SetState(msg.Data);
			migrate = false;
		}

		/// <summary>The pools as "researched|learned" (the host's "state" message; tests keep them with it).</summary>
		internal static string State() { return Join(researched) + "|" + Join(learned); }

		/// <summary>The pools from State() (not sent, not saved).</summary>
		internal static void SetState(string data)
		{
			string[] p = (data ?? "").Split('|');
			researched.Clear(); learned.Clear();
			foreach (int i in Ints(p[0])) researched.Add(i);
			if (p.Length > 1) foreach (int i in Ints(p[1])) learned.Add(i);
			UpgradeTableMenu.Refresh();
		}

		/// <summary>Tests: an upgrade learned at the table or not (not sent, not saved).</summary>
		internal static void SetLearned(ExtraUpgrades.Upgrade u, bool on)
		{
			if (u == null) return;
			if (on) learned.Add(u.Index); else learned.Remove(u.Index);
			UpgradeTableMenu.Refresh();
		}

		/// <summary>Tests: a world from before the table is read (the migration runs a few seconds later on the host).</summary>
		internal static void MigrateForTest() { migrate = true; hasLines = false; migrateAt = -1f; }

		#endregion

		[ConsoleCommand(name: "UpgradeTable", docs: "The upgrade research table: UpgradeTable = what is researched and learned there in this world")]
		public static void UpgradeTableCommand(string[] args)
		{
			Debug.Log("[CUSTOM ISLANDS] Upgrade table: researched " + string.Join(", ", researched.Select(i => { Item_Base it = ItemManager.GetItemByIndex(i); return it != null ? it.UniqueName : i.ToString(); }).ToArray())
				+ "; learned " + string.Join(", ", learned.Select(i => { ExtraUpgrades.Upgrade u = ExtraUpgrades.Find(i); return u != null ? u.Display : i.ToString(); }).ToArray()));
		}
	}

	/// <summary>
	/// Raft's research menu as the upgrade table's menu (UpgradeTable). Raft has one menu for every research table: opening
	/// a table (SetTable) shows the upgrades' entries and their research items here, Raft's own parked out of sight under an
	/// inactive holder (they stay active themselves: Raft's blueprint code reads that), and the other way round for Raft's
	/// tables. The upgrades' entries never take Raft's research (UpgradeResearchEntryPatch): their ticks and learned state
	/// are set from the upgrade table's pools (Apply).
	/// </summary>
	public static class UpgradeTableMenu
	{
		/// <summary>The menu shows the upgrade table's entries now.</summary>
		public static bool Showing { get; private set; }
		/// <summary>The upgrades' entries are being set from the pools: their Learn may run.</summary>
		internal static bool Applying;
		static GameObject entryHolder, iconHolder;
		/// <summary>Research item icons under Raft's list that aren't in its dictionary (no item): parked while the upgrade menu is up.</summary>
		static readonly List<Transform> strays = new List<Transform>();

		static Inventory_ResearchTable Menu { get { return ComponentManager<Inventory_ResearchTable>.Value; } }

		public static bool IsUpgradeTable(ResearchTable t)
		{
			if (t == null) return false;
			Block b = t.GetComponent<Block>();
			return b != null && b.buildableItem != null && b.buildableItem.UniqueIndex == ExtraUpgrades.TableIndex;
		}

		/// <summary>An entry of the mod's items (an upgrade, or the table: that one stays hidden).</summary>
		public static bool IsModEntry(ResearchMenuItem m)
		{
			Item_Base i = m != null ? m.GetItem() : null;
			return i != null && ExtraUpgrades.Find(i.UniqueIndex) != null;
		}

		/// <summary>The upgrade an entry teaches, or null (Raft's entries, the table's).</summary>
		public static ExtraUpgrades.Upgrade UpgradeOf(ResearchMenuItem m)
		{
			Item_Base i = m != null ? m.GetItem() : null;
			ExtraUpgrades.Upgrade u = i != null ? ExtraUpgrades.Find(i.UniqueIndex) : null;
			return u != null && !u.Table ? u : null;
		}

		internal static Dictionary<Item_Base, AvaialableResearchItem> Icons(Inventory_ResearchTable inv)
		{
			return Traverse.Create(inv).Field("availableResearchItems").GetValue<Dictionary<Item_Base, AvaialableResearchItem>>() ?? new Dictionary<Item_Base, AvaialableResearchItem>();
		}

		internal static List<BingoMenuItem> Ticks(ResearchMenuItem m)
		{
			return Traverse.Create(m).Field("bingoMenuItems").GetValue<List<BingoMenuItem>>() ?? new List<BingoMenuItem>();
		}

		static void Park(Transform t, Transform under) { if (t.parent != under) t.SetParent(under, false); }

		/// <summary>Turns the menu into the upgrade table's (true) or Raft's (false).</summary>
		public static void Show(bool upgrades)
		{
			Inventory_ResearchTable inv = Menu;
			if (inv == null) return;
			Transform content = Traverse.Create(inv).Field("content").GetValue<RectTransform>();
			Transform icons = Traverse.Create(inv).Field("researchItemContent").GetValue<RectTransform>();
			if (content == null || icons == null) return;
			if (entryHolder == null || entryHolder.transform.parent != content.parent)
			{
				if (entryHolder != null) UnityEngine.Object.Destroy(entryHolder);
				if (iconHolder != null) UnityEngine.Object.Destroy(iconHolder);
				entryHolder = new GameObject("DI_ParkedResearchEntries");
				entryHolder.SetActive(false);
				entryHolder.transform.SetParent(content.parent, false);
				iconHolder = new GameObject("DI_ParkedResearchItems");
				iconHolder.SetActive(false);
				iconHolder.transform.SetParent(icons.parent, false);
			}
			Showing = upgrades;
			var raftItems = new HashSet<Item_Base>();
			foreach (ResearchMenuItem m in inv.GetMenuItems())
			{
				if (m == null) continue;
				if (!IsModEntry(m))
				{
					Park(m.transform, upgrades ? entryHolder.transform : content);
					foreach (CostMultiple c in m.GetItem().settings_recipe.NewCost ?? new CostMultiple[0])
						foreach (Item_Base i in c.items ?? new Item_Base[0]) if (i != null) raftItems.Add(i);
					continue;
				}
				if (UpgradeOf(m) == null) continue;
				// (Raft hides them - HiddenInResearchTable: shown, they live under the holder while Raft's menu is up)
				if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
				Park(m.transform, upgrades ? content : entryHolder.transform);
			}
			HashSet<Item_Base> ours = UpgradeTable.Researchable();
			int n = 0;
			foreach (var kv in Icons(inv))
			{
				if (kv.Value == null) continue;
				bool show = upgrades ? ours.Contains(kv.Key) : raftItems.Contains(kv.Key) || !ours.Contains(kv.Key);
				Park(kv.Value.transform, show ? icons : iconHolder.transform);
				if (show) kv.Value.transform.SetSiblingIndex(n++);
			}
			// (CIUpgradeTable found one icon with no item under the list, not one of the dictionary's: not the upgrades')
			var known = new HashSet<AvaialableResearchItem>(Icons(inv).Values.Where(v => v != null));
			if (upgrades)
			{
				foreach (AvaialableResearchItem a in icons.GetComponentsInChildren<AvaialableResearchItem>(true).ToList())
					if (a.transform.parent == icons && !known.Contains(a)) { Park(a.transform, iconHolder.transform); if (!strays.Contains(a.transform)) strays.Add(a.transform); }
			}
			else
			{
				foreach (Transform t in strays) if (t != null) Park(t, icons);
				strays.Clear();
			}
			SyncIcons(inv);
			if (upgrades) Apply(inv);
			inv.SortMenuItems();
		}

		/// <summary>The research items' dimmed state from the pool the menu shows.</summary>
		internal static void SyncIcons(Inventory_ResearchTable inv)
		{
			List<Item_Base> raft = inv.GetResearchedItems();
			foreach (var kv in Icons(inv))
			{
				if (kv.Value == null) continue;
				bool r = Showing ? UpgradeTable.IsResearched(kv.Key) : raft.Contains(kv.Key);
				if (kv.Value.Researched != r) kv.Value.SetResearchedState(r);
			}
		}

		/// <summary>The upgrades' entries from the upgrade table's pools: ticks, the learn button, learned.</summary>
		static void Apply(Inventory_ResearchTable inv)
		{
			Applying = true;
			try
			{
				foreach (ResearchMenuItem m in inv.GetMenuItems())
				{
					ExtraUpgrades.Upgrade u = UpgradeOf(m);
					if (u == null) continue;
					List<BingoMenuItem> ticks = Ticks(m);
					foreach (BingoMenuItem b in ticks)
					{
						bool r = UpgradeTable.IsResearched(b.BingoItem);
						if (b != null && b.BingoState != r) b.SetBingoState(r);
					}
					if (UpgradeTable.IsLearned(u)) { if (!m.Learned) m.Learn(); }
					else if (ticks.All(b => b == null || b.BingoState)) m.Bingo();
				}
			}
			finally { Applying = false; }
		}

		/// <summary>The pools changed (or a world loaded): the menu again, if it shows the upgrades.</summary>
		internal static void Refresh()
		{
			if (!Showing || Menu == null) return;
			try { Show(true); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrade table] menu: " + e.Message); }
		}

		internal static void Forget() { Showing = false; }

		internal static void ResearchSound()
		{
			Inventory_ResearchTable inv = Menu;
			if (inv != null) FMODUnity.RuntimeManager.PlayOneShot(inv.er_research, default(Vector3));
		}

		/// <summary>An upgrade was learned at the table (by the player with that Steam id): Raft's "learned" notice.</summary>
		internal static void Learned(ExtraUpgrades.Upgrade u, ulong by)
		{
			if (u == null || u.Item == null) return;
			Inventory_ResearchTable inv = Menu;
			if (inv != null) FMODUnity.RuntimeManager.PlayOneShot(inv.er_learn, default(Vector3));
			NotificationManager nm = ComponentManager<NotificationManager>.Value;
			Notification_Research n = nm != null ? nm.ShowNotification("Research") as Notification_Research : null;
			if (n != null) n.researchInfoQue.Enqueue(new Notification_Research_Info(u.Display, new Network_UserId(by), u.Item.settings_Inventory.Sprite));
			Refresh();
		}
	}

	/// <summary>Opening a research table turns the one menu into its kind's.</summary>
	[HarmonyPatch(typeof(Inventory_ResearchTable), "SetTable")]
	static class UpgradeTableSetPatch
	{
		static void Postfix(ResearchTable table)
		{
			if (table == null) return;
			try { UpgradeTableMenu.Show(UpgradeTableMenu.IsUpgradeTable(table)); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrade table] menu: " + e.Message); }
		}
	}

	/// <summary>
	/// An upgrade table taken away (picked up, broken) while this player has its menu open: what lies in its slot goes
	/// back to this player (Raft destroys the slot with the table, and the items in it were gone).
	/// </summary>
	[HarmonyPatch(typeof(ResearchTable), "OnDestroy")]
	static class UpgradeTableGoneGiveBackPatch
	{
		static void Prefix(ResearchTable __instance)
		{
			try
			{
				if (__instance == null || !UpgradeTableMenu.IsUpgradeTable(__instance)) return;
				Slot slot = __instance.slot;
				if (slot == null || slot.IsEmpty || slot.itemInstance == null || slot.itemInstance.baseItem == null) return;
				var t = HarmonyLib.Traverse.Create(__instance);
				Network_Player local = t.Field("localPlayer").GetValue<Network_Player>() ?? RAPI.GetLocalPlayer();
				if (local == null || local.Inventory == null || t.Field("occupyingPlayerID").GetValue<Network_UserId>() != local.steamID) return;
				string name = slot.itemInstance.UniqueName;
				int amount = slot.itemInstance.Amount;
				slot.RemoveItem(amount);
				local.Inventory.AddItem(name, amount);
				Debug.Log("[CUSTOM ISLANDS] [upgrade table] the table went with " + amount + " " + name + " in its slot: given back");
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrade table] give back: " + e.Message); }
		}
	}

	/// <summary>The upgrade table's Research button and its "researched" / "can't be researched" texts, from its own pool
	/// (Raft's Update sets them from Raft's first).</summary>
	[HarmonyPatch(typeof(Inventory_ResearchTable), "Update")]
	static class UpgradeTableUpdatePatch
	{
		static void Postfix(Inventory_ResearchTable __instance)
		{
			ResearchTable t = __instance.activeTable;
			if (!UpgradeTableMenu.IsUpgradeTable(t)) return;
			if (!UpgradeTableMenu.Showing) UpgradeTableMenu.Show(true);
			UpgradeTableMenu.SyncIcons(__instance);
			if (!t.IsOccupied || t.slot == null || t.slot.IsEmpty) return;
			Traverse tr = Traverse.Create(__instance);
			UnityEngine.UI.Button button = tr.Field("researchButton").GetValue<UnityEngine.UI.Button>();
			UnityEngine.UI.Text done = tr.Field("researchedText").GetValue<UnityEngine.UI.Text>(), cant = tr.Field("cantBeResearchedText").GetValue<UnityEngine.UI.Text>();
			if (button == null || done == null || cant == null) return;
			bool pad = CustomInputConfig.Instance != null && CustomInputConfig.Instance.Gamepad;
			Item_Base item = t.slot.itemInstance.baseItem;
			if (UpgradeTable.IsResearched(item))
			{
				done.gameObject.SetActive(true);
				if (!pad) button.gameObject.SetActive(false);
				cant.gameObject.SetActive(false);
				Inventory_ResearchTable.ResearchItem = null;
				return;
			}
			button.interactable = UpgradeTable.CanResearch(item);
			button.gameObject.SetActive(!pad);
			cant.gameObject.SetActive(!button.interactable);
			done.gameObject.SetActive(false);
			Inventory_ResearchTable.ResearchItem = button.interactable ? item : null;
		}
	}

	/// <summary>The upgrade table's Research button: the mod's research, not Raft's message.</summary>
	[HarmonyPatch(typeof(Inventory_ResearchTable), "ResearchButton")]
	static class UpgradeTableResearchPatch
	{
		static bool Prefix(Inventory_ResearchTable __instance)
		{
			if (!UpgradeTableMenu.IsUpgradeTable(__instance.activeTable)) return true;
			UpgradeTable.AskResearch(__instance.activeTable.slot);
			return false;
		}
	}

	/// <summary>Raft's tables research only what Raft's own entries use (not an item only the upgrades cost).</summary>
	[HarmonyPatch(typeof(Inventory_ResearchTable), "CanResearchItem")]
	static class UpgradeTableCanResearchPatch
	{
		static void Postfix(Inventory_ResearchTable __instance, Item_Base item, ref bool __result)
		{
			if (!__result || item == null || item.settings_recipe.IsBlueprint) return;
			if (!__instance.GetMenuItems().Any(m => m != null && !UpgradeTableMenu.IsModEntry(m) && m.ContainsItem(item))) __result = false;
		}
	}

	/// <summary>The upgrades' entries: Raft's research and learning never reach them (UpgradeTableMenu.Apply sets them),
	/// and their Learn button asks the upgrade table.</summary>
	[HarmonyPatch]
	static class UpgradeResearchEntryPatch
	{
		[HarmonyPatch(typeof(ResearchMenuItem), "Research")]
		[HarmonyPrefix]
		static bool Research(ResearchMenuItem __instance) { return !UpgradeTableMenu.IsModEntry(__instance); }

		[HarmonyPatch(typeof(ResearchMenuItem), "LearnInstantly")]
		[HarmonyPrefix]
		static bool LearnInstantly(ResearchMenuItem __instance) { return !UpgradeTableMenu.IsModEntry(__instance); }

		[HarmonyPatch(typeof(ResearchMenuItem), "Learn")]
		[HarmonyPrefix]
		static bool Learn(ResearchMenuItem __instance) { return UpgradeTableMenu.Applying || !UpgradeTableMenu.IsModEntry(__instance); }

		[HarmonyPatch(typeof(ResearchMenuItem), "LearnButton")]
		[HarmonyPrefix]
		static bool LearnButton(ResearchMenuItem __instance)
		{
			if (!UpgradeTableMenu.IsModEntry(__instance)) return true;
			UpgradeTable.AskLearn(UpgradeTableMenu.UpgradeOf(__instance));
			return false;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The extra option "upgrades" (WorldOptions.Upgrades, the user 2026-10-09): better versions of Raft's own things to
	/// craft - a large battery, a titanium grill, a blast furnace, a reinforced storage, a wide net, large fuel and water
	/// tanks, a greenhouse plot, a turbo engine, a large wind turbine, a comfy bed, a magnet hook, a titanium rod, swift
	/// flippers, a large air tank, a rapid charger, a fast recycler, a bright lantern, a telescope, a titanium greatsword,
	/// a titanium spear, a long paddle and a floodlight (the list: Defs). Every one is tinted: blocks and hand models
	/// themselves, worn ones the model they share with their base while they are worn (UpgradeWornPatch).
	///
	/// How: each is a copy of one of Raft's items (Item_Base) with its own fixed index (Upgrade.Index - saves and messages
	/// name items by index, so it must never change), registered with RAPI.RegisterItem when the mod starts, before a world
	/// builds its crafting menu (CraftingMenu.InitializeRecipes reads ItemManager.GetAllItems once). Its recipe costs the
	/// base item's recipe times CostFactor (plus an optional extra), has no entry in Raft's research tables and is
	/// craftable while the world has the option on and it is learned at the upgrade research table (UpgradeTable, an
	/// upgrade itself, learned from the start - Tick), so it appears next to the base item.
	/// A buildable one gets copies of the base item's block prefabs (kept inactive under one holder, so they don't wake),
	/// each pointing back at the new item (Block.buildableItem / itemToReturnOnDestroy, so saves store the new index) and
	/// carrying an UpgradeBlock: when a block of it is placed or loaded, UpgradeBlock tints it and sets its better stats
	/// (Upgrade.Setup) before Raft's own Start code reads them. Raft hands back a block's contents (storage, grill) by its
	/// item index (RemovePlaceables.ReturnItemsFromBlock): UpgradeReturnItemsPatch lets it see the base item there.
	/// A hand-held one (hook, rod) gets a copy of the base item's
	/// hand model in every player's UseItemController (Upgrade.Held sets its stats); a worn one (flippers, air tank) gets a
	/// copy of the base item's Equipment in every player's PlayerEquipment (Upgrade.Worn sets its stats).
	/// Every player needs the mod (the mod is needed to join anyway); without it the upgrades are missing from a world.
	/// </summary>
	public static class ExtraUpgrades
	{
		/// <summary>One better version of a Raft item.</summary>
		public class Upgrade
		{
			/// <summary>The item index: fixed for ever (worlds save it). Below RAPI's limit of 32767, far from Raft's own.</summary>
			public int Index;
			public string Name, BaseName, Display, Description;
			public Color Tint;
			/// <summary>A pale metal look: the model's textures lightened (dark parts lifted) in its pale tint, not made Vivid.</summary>
			public bool Whiten;
			/// <summary>How many times the base item's recipe it costs.</summary>
			public int CostFactor = 2;
			/// <summary>An extra cost item (Raft's unique name) and amount, or null.</summary>
			public string ExtraCostItem;
			public int ExtraCostAmount;
			/// <summary>How many times the base item's uses (a battery's charge) it has.</summary>
			public int UsesFactor = 1;
			/// <summary>Sets a placed block's better stats (its root object), before Raft's Start code reads them.</summary>
			public Action<GameObject> Setup;
			/// <summary>Runs every 2 s on a placed block (its root object), or null.</summary>
			public Action<GameObject> Every;
			/// <summary>Sets a hand-held upgrade's stats on its copy of the base item's hand model, or null.</summary>
			public Action<GameObject> Held;
			/// <summary>Sets a worn upgrade's stats on its copy of the base item's Equipment, or null.</summary>
			public Action<Equipment> Worn;
			/// <summary>Runs when a worn upgrade is put on (true) or taken off (false), or null: for stats on the shared model.</summary>
			public Action<Equipment, bool> Wearing;
			public Item_Base Base, Item;
			/// <summary>The upgrade research table itself (UpgradeTable): learned from the start while the option is on.</summary>
			public bool Table;
			public override string ToString() { return Display + " (" + Name + ", " + Index + ")"; }
		}

		public const int LargeBatteryIndex = 29411;
		/// <summary>The upgrade research table (UpgradeTable): a tinted copy of Raft's research table at four times its cost.</summary>
		public const int TableIndex = 29443;
		public const string TableName = "DI_UpgradeResearchTable";
		public static readonly Color TableTint = new Color(0.75f, 0.55f, 1f);
		public const string LargeBatteryName = "DI_LargeBattery";
		/// <summary>How many times Raft's battery the large one holds and costs.</summary>
		public const int LargeBatteryFactor = 2;
		public static readonly Color LargeBatteryTint = new Color(0.45f, 0.65f, 1f);

		/// <summary>Cooking time of the titanium grill and blast furnace, times Raft's.</summary>
		public const float FastCookFactor = 0.5f;
		/// <summary>Reinforced storage: slots times the large storage's (rounded up to full rows).</summary>
		public const float StorageSlotsFactor = 1.5f;
		/// <summary>Turbo engine: pushing strength and speed times the engine's, fuel time times the engine's.</summary>
		public const int TurboStrengthFactor = 2;
		public const float TurboSpeedFactor = 1.5f, TurboFuelTimeFactor = 0.6f;
		/// <summary>Comfy bed: respawn health/food/water in % (Raft's bed: 50), sleep healing times, hunger/thirst times.</summary>
		public const float BedRespawnPercent = 75f, BedRegenFactor = 2f, BedDecayFactor = 0.5f;
		/// <summary>Magnet hook: pulling speed and gathering time times the titanium hook's.</summary>
		public const float HookPullFactor = 1.5f, HookGatherFactor = 0.5f;
		/// <summary>Titanium rod: waiting time for a bite times the metal rod's.</summary>
		public const float RodBiteFactor = 0.5f;
		/// <summary>Swift flippers: the flippers' extra swimming speed times this (Raft's 1.4 becomes 1.8).</summary>
		public const float FlipperBoostFactor = 2f;
		/// <summary>Large air tank: the oxygen bottle's oxygen loss multiplier times this.</summary>
		public const float AirLossFactor = 0.5f;
		/// <summary>Rapid charger: charge per fuel tick times the charger's. Fast recycler: time per cycle times the recycler's.</summary>
		public const int ChargerFactor = 2;
		public const float RecyclerTimeFactor = 0.5f;
		/// <summary>Bright lantern and floodlight: light range and brightness times their base's.</summary>
		public const float LightRangeFactor = 2f, LightIntensityFactor = 1.5f;
		/// <summary>Telescope: the binoculars' narrowest view (most zoom) times this.</summary>
		public const float TelescopeFovFactor = 0.5f;
		/// <summary>Titanium greatsword and spear: damage times their base's. Long paddle: push times the paddle's.</summary>
		public const float WeaponDamageFactor = 1.5f, PaddleFactor = 2f;
		/// <summary>Steel pot and fast juicer: cooking time is this many times shorter.</summary>
		public const float TableSpeedFactor = 2f;
		/// <summary>Master hammer: repairs this many times as much per swing.</summary>
		public const int HammerRepairFactor = 2;
		/// <summary>Longbow: draws and shoots this many times as hard and fast.</summary>
		public const float BowFactor = 1.5f;
		/// <summary>Lumber axe: takes building pieces apart in this part of the time.</summary>
		public const float AxeTimeFactor = 0.5f;

		static List<Upgrade> all;
		/// <summary>The upgrades (made once; Item set for the registered ones).</summary>
		public static List<Upgrade> All { get { if (all == null) { all = Defs(); foreach (Upgrade u in all) if (!u.Whiten) u.Tint = Vivid(u.Tint); } return all; } }
		public static IEnumerable<Upgrade> Registered { get { return All.Where(u => u.Item != null); } }
		public static Upgrade Find(int index) { return all == null ? null : all.FirstOrDefault(u => u.Item != null && u.Index == index); }
		public static Upgrade Find(string name) { return All.FirstOrDefault(u => u.Name == name); }

		public static Upgrade TableUpgrade { get { return Find(TableName); } }
		public static Item_Base TableItem { get { Upgrade u = Find(TableName); return u != null ? u.Item : null; } }
		public static Item_Base LargeBattery { get { Upgrade u = Find(LargeBatteryName); return u != null ? u.Item : null; } }

		static GameObject holder;

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [upgrades] " + msg); }
		static void Warn(string msg) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] " + msg); }

		static List<Upgrade> Defs()
		{
			return new List<Upgrade>
			{
				new Upgrade { Index = LargeBatteryIndex, Name = LargeBatteryName, BaseName = "Battery", Display = "Large battery", Tint = LargeBatteryTint, UsesFactor = LargeBatteryFactor,
					Description = "Holds twice the charge of a battery: machines run twice as long on it. Charged in the battery charger like a battery (twice as long, twice the fuel)." },
				new Upgrade { Index = 29412, Name = "DI_TitaniumGrill", BaseName = "Placeable_CookingStand_Food_Two", Display = "Titanium grill", Tint = new Color(0.75f, 0.82f, 0.95f),
					ExtraCostItem = "TitaniumIngot", ExtraCostAmount = 2, Setup = FastCooking,
					Description = "An advanced grill that cooks twice as fast." },
				new Upgrade { Index = 29413, Name = "DI_BlastFurnace", BaseName = "Placeable_CookingStand_Smelter", Display = "Blast furnace", Tint = new Color(1f, 0.6f, 0.45f),
					Setup = FastCooking,
					Description = "A smelter that smelts twice as fast." },
				new Upgrade { Index = 29414, Name = "DI_ReinforcedStorage", BaseName = "Placeable_Storage_Large", Display = "Reinforced storage", Tint = new Color(0.75f, 0.6f, 0.45f),
					Setup = BigStorage,
					Description = "A large storage with half as many slots again." },
				new Upgrade { Index = 29415, Name = "DI_WideNet", BaseName = "Placeable_CollectionNet_Advanced", Display = "Wide net", Tint = new Color(0.55f, 0.95f, 0.65f),
					Setup = WideNet,
					Description = "An advanced collection net that catches from twice as wide an area and holds twice as many items." },
				new Upgrade { Index = 29416, Name = "DI_LargeFuelTank", BaseName = "Placeable_FuelTank", Display = "Large fuel tank", Tint = new Color(1f, 0.75f, 0.4f),
					Setup = BigTanks,
					Description = "A fuel tank that holds twice as much fuel." },
				new Upgrade { Index = 29417, Name = "DI_GreenhousePlot", BaseName = "Placeable_Cropplot_Large", Display = "Greenhouse plot", Tint = new Color(0.6f, 1f, 0.6f),
					Every = WaterPlot,
					Description = "A large crop plot that waters itself: its plants never go dry." },
				new Upgrade { Index = 29418, Name = "DI_TurboEngine", BaseName = "Placeable_MotorWheel", Display = "Turbo engine", Tint = new Color(1f, 0.5f, 0.45f),
					Setup = Turbo,
					Description = "An engine that pushes twice the weight and drives the raft half again as fast, but burns fuel faster." },
				new Upgrade { Index = 29419, Name = "DI_LargeWindTurbine", BaseName = "Placeable_WindTurbine", Display = "Large wind turbine", Tint = new Color(0.6f, 0.8f, 1f),
					Setup = StrongTurbine,
					Description = "A wind turbine that charges batteries twice as fast." },
				new Upgrade { Index = 29420, Name = "DI_ComfyBed", BaseName = "Placeable_Bed_Wood", Display = "Comfy bed", Tint = new Color(1f, 0.78f, 0.85f),
					Setup = ComfyBed,
					Description = "Sleeping in it heals twice as fast and makes you half as hungry and thirsty; you wake up from death with 75% health, food and water instead of 50%." },
				new Upgrade { Index = 29421, Name = "DI_LargeWaterTank", BaseName = "Placeable_WaterTank", Display = "Large water tank", Tint = new Color(0.5f, 0.75f, 1f),
					Setup = BigTanks,
					Description = "A water tank that holds twice as much water." },
				new Upgrade { Index = 29422, Name = "DI_MagnetHook", BaseName = "Hook_Titanium", Display = "Magnet hook", Tint = new Color(1f, 0.55f, 0.55f), UsesFactor = 2,
					Held = MagnetHook,
					Description = "A titanium hook with a magnet: pulls in half again as fast, picks things up twice as fast and lasts twice as long." },
				new Upgrade { Index = 29423, Name = "DI_TitaniumRod", BaseName = "FishingRod_Metal", Display = "Titanium rod", Tint = new Color(0.75f, 0.82f, 0.95f), UsesFactor = 2,
					ExtraCostItem = "TitaniumIngot", ExtraCostAmount = 2, Held = TitaniumRod,
					Description = "A metal fishing rod made of titanium: fish bite twice as fast, and it lasts twice as long." },
				new Upgrade { Index = 29424, Name = "DI_SwiftFlippers", BaseName = "Flipper", Display = "Swift flippers", Tint = new Color(0.45f, 0.9f, 1f), UsesFactor = 2,
					Worn = SwiftFlippers,
					Description = "Flippers that add twice the swimming speed the flippers add, and last twice as long." },
				new Upgrade { Index = 29425, Name = "DI_LargeAirTank", BaseName = "OxygenBottle", Display = "Large air tank", Tint = new Color(0.6f, 0.75f, 1f), UsesFactor = 2,
					Worn = LargeAirTank,
					Description = "An oxygen bottle that makes you lose air under water half as fast as the bottle does, and lasts twice as long." },
				new Upgrade { Index = 29426, Name = "DI_RapidCharger", BaseName = "Placeable_BatteryCharger", Display = "Rapid charger", Tint = new Color(1f, 0.9f, 0.4f),
					Setup = RapidCharger,
					Description = "A battery charger that charges twice as much per fuel." },
				new Upgrade { Index = 29427, Name = "DI_FastRecycler", BaseName = "Placeable_Recycler", Display = "Fast recycler", Tint = new Color(0.55f, 1f, 0.75f),
					Setup = FastRecycler,
					Description = "A recycler that works twice as fast." },
				new Upgrade { Index = 29428, Name = "DI_BrightLantern", BaseName = "Placeable_Lantern_Metal", Display = "Bright lantern", Tint = new Color(1f, 0.85f, 0.5f),
					Setup = BrightLight,
					Description = "A metal lantern that lights twice as far and half again as bright." },
				new Upgrade { Index = 29429, Name = "DI_Telescope", BaseName = "Binoculars", Display = "Telescope", Tint = new Color(0.85f, 0.7f, 0.45f),
					Held = Telescope,
					Description = "Binoculars that zoom in twice as far." },
				new Upgrade { Index = 29430, Name = "DI_TitaniumGreatsword", BaseName = "Sword_Titanium", Display = "Titanium greatsword", Tint = new Color(0.7f, 0.8f, 1f), UsesFactor = 2,
					Held = StrongWeapon,
					Description = "A titanium sword that hits half again as hard and lasts twice as long." },
				new Upgrade { Index = 29431, Name = "DI_TitaniumSpear", BaseName = "Spear_Scrap", Display = "Titanium spear", Tint = new Color(0.75f, 0.82f, 0.95f), Whiten = true, UsesFactor = 2,
					ExtraCostItem = "TitaniumIngot", ExtraCostAmount = 2, Held = StrongWeapon,
					Description = "A metal spear with a titanium tip: hits half again as hard and lasts twice as long." },
				new Upgrade { Index = 29432, Name = "DI_LongPaddle", BaseName = "Paddle", Display = "Long paddle", Tint = new Color(0.8f, 0.6f, 0.4f), UsesFactor = 2,
					Held = LongPaddle,
					Description = "A paddle that pushes the raft twice as hard and lasts twice as long." },
				new Upgrade { Index = 29433, Name = "DI_Floodlight", BaseName = "HeadLight_Advanced", Display = "Floodlight", Tint = new Color(1f, 0.95f, 0.55f), UsesFactor = 2,
					Wearing = Floodlight,
					Description = "An advanced head light that lights twice as far and half again as bright, and lasts twice as long." },
				new Upgrade { Index = 29434, Name = "DI_SteelPot", BaseName = "Placeable_CookingPot", Display = "Steel pot", Tint = new Color(0.7f, 0.75f, 0.8f),
					Setup = QuickTable,
					Description = "A cooking pot that cooks twice as fast." },
				new Upgrade { Index = 29435, Name = "DI_FastJuicer", BaseName = "Placeable_Juicer", Display = "Fast juicer", Tint = new Color(1f, 0.7f, 0.35f),
					Setup = QuickTable,
					Description = "A juicer that makes juice twice as fast." },
				new Upgrade { Index = 29436, Name = "DI_TitaniumMachete", BaseName = "Machete", Display = "Titanium machete", Tint = new Color(0.75f, 0.82f, 0.95f), UsesFactor = 2,
					ExtraCostItem = "TitaniumIngot", ExtraCostAmount = 2, Held = StrongWeapon,
					Description = "A machete with a titanium blade: hits half again as hard and lasts twice as long." },
				new Upgrade { Index = 29437, Name = "DI_SturdyScarecrow", BaseName = "Placeable_Scarecrow_Advanced", Display = "Sturdy scarecrow", Tint = new Color(0.95f, 0.8f, 0.4f),
					Setup = SturdyScarecrow,
					Description = "An advanced scarecrow that never wears out." },
				new Upgrade { Index = 29438, Name = "DI_MasterHammer", BaseName = "Hammer", Display = "Master hammer", Tint = new Color(0.85f, 0.55f, 0.35f), UsesFactor = 2,
					Held = MasterHammer,
					Description = "A building hammer that repairs twice as much per swing and lasts twice as long." },
				new Upgrade { Index = 29439, Name = "DI_Longbow", BaseName = "Bow", Display = "Longbow", Tint = new Color(0.55f, 0.8f, 0.45f), UsesFactor = 2,
					Held = Longbow,
					Description = "A bow that draws half again as fast, shoots half again as far and lasts twice as long." },
				new Upgrade { Index = 29440, Name = "DI_BrightHeadLight", BaseName = "HeadLight", Display = "Bright head light", Tint = new Color(1f, 0.85f, 0.6f), UsesFactor = 2,
					Wearing = Floodlight,
					Description = "A head light that lights twice as far and half again as bright, and lasts twice as long." },
				new Upgrade { Index = 29441, Name = "DI_BrightLamp", BaseName = "Placeable_Lantern_Basic", Display = "Bright lamp", Tint = new Color(1f, 0.7f, 0.7f),
					Setup = BrightLight,
					Description = "A lantern that lights twice as far and half again as bright." },
				new Upgrade { Index = 29442, Name = "DI_LumberAxe", BaseName = "Axe_Titanium", Display = "Lumber axe", Tint = new Color(0.9f, 0.45f, 0.4f), UsesFactor = 2,
					Held = LumberAxe,
					Description = "A titanium axe that takes building pieces apart twice as fast and lasts twice as long." },
				new Upgrade { Index = TableIndex, Name = TableName, BaseName = "Placeable_ResearchTable", Display = "Upgrade research table", Tint = TableTint, Table = true, CostFactor = 4,
					Description = "A research table of its own for the upgrades: research items here to learn better versions of Raft's things. What is researched at Raft's table doesn't count here." },
			};
		}

		/// <summary>Makes and registers the upgrades (the mod's Start). Safe to call again.</summary>
		public static void Register()
		{
			if (holder == null)
			{
				holder = new GameObject("DI_UpgradePrefabs");
				holder.SetActive(false);
				UnityEngine.Object.DontDestroyOnLoad(holder);
			}
			int made = 0;
			foreach (Upgrade u in All)
			{
				if (u.Item != null) continue;
				try { if (Make(u)) made++; }
				catch (Exception e) { Warn(u + ": " + e.Message); }
			}
			// BlockCreator fills its static list of buildable items once; a world opened before this needs them added.
			var buildables = Traverse.Create(typeof(BlockCreator)).Field("buildableItems").GetValue<List<Item_Base>>();
			if (buildables != null && buildables.Count > 0)
				foreach (Upgrade u in Registered.Where(u => u.Item.settings_buildable != null && u.Item.settings_buildable.GetBlockPrefabs() != null && !buildables.Contains(u.Item)))
					buildables.Add(u.Item);
			Log(made + " upgrades registered");
		}

		static bool Make(Upgrade u)
		{
			Item_Base baseItem = ItemManager.GetItemByName(u.BaseName);
			if (baseItem == null) { Warn("Raft has no item '" + u.BaseName + "': no " + u.Display); return false; }
			Item_Base existing = ItemManager.GetItemByIndex(u.Index);
			if (existing != null) { Warn("Item index " + u.Index + " is taken by '" + existing.UniqueName + "' (another mod): no " + u.Display); return false; }

			Item_Base item = ScriptableObject.Instantiate(baseItem);
			item.name = u.Name;
			item.Initialize(u.Index, u.Name, baseItem.MaxUses * u.UsesFactor);

			ItemInstance_Inventory inv = baseItem.settings_Inventory.Clone();
			inv.LocalizationTerm = "";
			inv.DisplayName = u.Display;
			inv.Description = u.Description;
			inv.Sprite = TintedSprite(baseItem.settings_Inventory.Sprite, u.Tint, u.Name) ?? baseItem.settings_Inventory.Sprite;
			item.settings_Inventory = inv;

			ItemInstance_Recipe recipe = baseItem.settings_recipe.Clone();
			var cost = baseItem.settings_recipe.NewCost.Select(c => new CostMultiple(c.items, c.amount * u.CostFactor)).ToList();
			if (u.ExtraCostItem != null)
			{
				Item_Base extra = ItemManager.GetItemByName(u.ExtraCostItem);
				if (extra != null) cost.Add(new CostMultiple(new[] { extra }, u.ExtraCostAmount));
			}
			recipe.NewCost = cost.ToArray();
			Traverse t = Traverse.Create(recipe);
			// (the table is learned from the start - Tick, while the option is on; the rest at the table: Raft's never shows them)
			t.Field("_hiddenInResearchTable").SetValue(true);
			t.Field("learnedFromBeginning").SetValue(false);
			t.Field("blueprintItem").SetValue(null);
			t.Field("extraBlueprintItems").SetValue(null);
			// a skin (Raft's wooden bed) has no crafting tab of its own: take its base item's tab, so the menu lists it
			if (recipe.CraftingCategory == CraftingCategory.Skin)
			{
				Item_Base skinOf = t.Field("baseSkinItem").GetValue<Item_Base>();
				ItemInstance_Recipe from = skinOf != null && skinOf.settings_recipe != null && skinOf.settings_recipe.CraftingCategory != CraftingCategory.Skin ? skinOf.settings_recipe : null;
				t.Field("craftingCategory").SetValue(from != null ? from.CraftingCategory : CraftingCategory.Other);
				if (from != null)
				{
					t.Field("categoryOrder").SetValue(Traverse.Create(from).Field("categoryOrder").GetValue<int>());
					t.Field("subCategory").SetValue(from.SubCategory);
					t.Field("subCatergoryOrder").SetValue(from.SubCategoryOrder);
				}
			}
			t.Field("baseSkinItem").SetValue(null);
			t.Field("skins").SetValue(new Item_Base[0]);
			t.Field("selectedSkin").SetValue(null);
			recipe.Learned = false;
			item.settings_recipe = recipe;

			Block[] prefabs = baseItem.settings_buildable != null ? baseItem.settings_buildable.GetBlockPrefabs() : null;
			if (prefabs != null && prefabs.Length > 0 && prefabs.All(p => p != null))
			{
				ItemInstance_Buildable b = baseItem.settings_buildable.Clone();
				Traverse.Create(b).Field("blockPrefabs").SetValue(prefabs.Select(p => ClonePrefab(p, baseItem, item)).ToArray());
				Traverse.Create(b).Field("mirroredVersion").SetValue(null);
				item.settings_buildable = b;
			}

			RAPI.RegisterItem(item);
			u.Item = ItemManager.GetItemByIndex(u.Index);
			if (u.Item == null) { Warn(u.Display + " couldn't be registered"); return false; }
			u.Base = baseItem;
			Log(u + " registered from " + u.BaseName + ", cost " + CostText(recipe.NewCost) + (u.UsesFactor != 1 ? ", uses " + u.Item.MaxUses : ""));
			return true;
		}

		static Block ClonePrefab(Block prefab, Item_Base baseItem, Item_Base item)
		{
			Block c = UnityEngine.Object.Instantiate(prefab, holder.transform);
			c.name = prefab.name + "_" + item.UniqueName;
			c.buildableItem = item;
			if (c.itemToReturnOnDestroy == null || c.itemToReturnOnDestroy == baseItem) c.itemToReturnOnDestroy = item;
			if (c.GetComponent<UpgradeBlock>() == null) c.gameObject.AddComponent<UpgradeBlock>();
			return c;
		}

		/// <summary>The mod's unload: the items off again.</summary>
		public static void Unregister()
		{
			foreach (Upgrade u in Registered.ToList())
			{
				try { RAPI.UnregisterItem(u.Item); } catch (Exception e) { Warn(u + ": " + e.Message); }
				var buildables = Traverse.Create(typeof(BlockCreator)).Field("buildableItems").GetValue<List<Item_Base>>();
				if (buildables != null) buildables.Remove(u.Item);
				u.Item = null;
			}
			if (holder != null) UnityEngine.Object.Destroy(holder);
			holder = null;
		}

		public static string CostText(CostMultiple[] cost)
		{
			return cost == null ? "-" : string.Join(", ", cost.Select(c => c.amount + " " + string.Join("/", (c.items ?? new Item_Base[0]).Where(i => i != null).Select(i => i.UniqueName).ToArray())).ToArray());
		}

		/// <summary>Whether an upgrade can be crafted in this world now: the option on and the upgrade learned at the upgrade
		/// research table (the table itself: from the start).</summary>
		public static bool Craftable(Upgrade u)
		{
			return u != null && u.Item != null && u.Base != null && WorldOptions.On(WorldOptions.Upgrades) && (u.Table || UpgradeTable.IsLearned(u));
		}

		public static bool LargeBatteryCraftable { get { return Craftable(Find(LargeBatteryName)); } }

		static float baseMaxSpeed = -1f;
		static Raft speedRaft;

		/// <summary>Every frame in a world: each upgrade's recipe learned exactly while Craftable (a world whose option is
		/// switched off hides them again; Raft's research table never shows them), and the raft's top speed raised by the
		/// running turbo engines' extra speed.</summary>
		public static void Tick()
		{
			if (all == null || !LoadSceneManager.IsGameSceneLoaded) return;
			foreach (Upgrade u in Registered)
			{
				bool on = Craftable(u);
				if (u.Item.settings_recipe.Learned != on) { u.Item.settings_recipe.Learned = on; Log(u.Display + (on ? " craftable" : " hidden")); }
			}
			UpgradeTable.Tick();
			TurboTopSpeed();
		}

		#region Battery slots

		/// <summary>Lets a machine's battery slot take the large battery: a connection for it, with a blue copy of the
		/// battery's model beside Raft's one. Returns false when the slot has no battery model to copy.</summary>
		public static bool AddToSlot(Battery slot)
		{
			Item_Base large = LargeBattery;
			if (large == null || slot == null || slot.itemEnabler == null) return false;
			ItemObjectEnabler en = slot.itemEnabler;
			ItemModelConnection[] cons = en.GetObjectConnections() ?? new ItemModelConnection[0];
			if (cons.Any(c => c != null && c.item != null && c.item.UniqueIndex == LargeBatteryIndex)) return true;
			ItemModelConnection basic = cons.FirstOrDefault(c => c != null && c.item != null && c.model != null && c.item.UniqueName == "Battery");
			if (basic == null) return false;
			GameObject model = UnityEngine.Object.Instantiate(basic.model, basic.model.transform.parent);
			model.name = basic.model.name + "_Large";
			model.SetActive(false);
			TintAll(model, Vivid(LargeBatteryTint));
			var con = new ItemModelConnection { item = large, model = model };
			Traverse.Create(en).Field("itemConnections").SetValue(cons.Concat(new[] { con }).ToArray());
			return true;
		}

		#endregion

		#region Better stats (Upgrade.Setup / Every)

		static void FastCooking(GameObject go)
		{
			foreach (CookingSlot s in go.GetComponentsInChildren<CookingSlot>(true))
			{
				Traverse f = Traverse.Create(s).Field("cookTimeMultiplier");
				f.SetValue(f.GetValue<float>() * FastCookFactor);
			}
		}

		static readonly Dictionary<Inventory, Inventory> bigInventories = new Dictionary<Inventory, Inventory>();

		static void BigStorage(GameObject go)
		{
			foreach (Storage_Small s in go.GetComponentsInChildren<Storage_Small>(true))
				if (s.inventoryPrefab != null) s.inventoryPrefab = BigInventory(s.inventoryPrefab);
		}

		/// <summary>A copy of a storage's inventory prefab with StorageSlotsFactor times its slots (full rows), made once.</summary>
		public static Inventory BigInventory(Inventory prefab)
		{
			Inventory big;
			if (bigInventories.TryGetValue(prefab, out big) && big != null) return big;
			if (bigInventories.ContainsValue(prefab)) return prefab;
			big = UnityEngine.Object.Instantiate(prefab, holder.transform);
			big.name = prefab.name + "_Reinforced";
			Slot[] slots = big.GetComponentsInChildren<Slot>(true);
			if (slots.Length > 0)
			{
				Slot last = slots[slots.Length - 1];
				GridLayoutGroupInfo grid = GridLayoutGroupInfo.Of(big.gridLayoutGroup, slots.Length);
				int want = Mathf.CeilToInt(slots.Length * StorageSlotsFactor / grid.columns) * grid.columns;
				for (int i = slots.Length; i < want; i++)
				{
					GameObject extra = UnityEngine.Object.Instantiate(last.gameObject, last.transform.parent);
					extra.name = last.gameObject.name + "_" + i;
				}
				int addedRows = (want - slots.Length) / grid.columns;
				float grow = addedRows * grid.rowHeight;
				// Taller panels: the grid's own rect and the inventory's root rect, so the new rows show inside the frame.
				foreach (RectTransform rt in new[] { big.gridLayoutGroup != null ? big.gridLayoutGroup.transform as RectTransform : null, big.transform as RectTransform }.Distinct())
					if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y + grow);
				Log("Reinforced storage: " + slots.Length + " -> " + want + " slots (" + grid.columns + " columns, " + addedRows + " more rows)");
			}
			bigInventories[prefab] = big;
			return big;
		}

		/// <summary>Column count and row height of an inventory grid.</summary>
		struct GridLayoutGroupInfo
		{
			public int columns; public float rowHeight;
			public static GridLayoutGroupInfo Of(UnityEngine.UI.GridLayoutGroup g, int slots)
			{
				var info = new GridLayoutGroupInfo { columns = 5, rowHeight = 60f };
				if (g == null) return info;
				info.rowHeight = g.cellSize.y + g.spacing.y;
				if (g.constraint == UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount) info.columns = Mathf.Max(1, g.constraintCount);
				else if (g.constraint == UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount) info.columns = Mathf.Max(1, Mathf.CeilToInt(slots / (float)Mathf.Max(1, g.constraintCount)));
				else
				{
					var rt = g.transform as RectTransform;
					float w = rt != null ? rt.rect.width - g.padding.horizontal : 0f;
					if (w > 0f) info.columns = Mathf.Max(1, Mathf.FloorToInt((w + g.spacing.x) / (g.cellSize.x + g.spacing.x)));
				}
				return info;
			}
		}

		static void WideNet(GameObject go)
		{
			foreach (ItemCollector c in go.GetComponentsInChildren<ItemCollector>(true))
			{
				Traverse t = Traverse.Create(c);
				var box = t.Field("collectorCollider").GetValue<Collider>() as BoxCollider;
				if (box != null)
				{
					// Twice as wide along the net's long side (the largest of the box's sides).
					Vector3 s = box.size;
					if (s.x >= s.y && s.x >= s.z) s.x *= 2f; else if (s.z >= s.y) s.z *= 2f; else s.y *= 2f;
					box.size = s;
				}
				Traverse max = t.Field("maxNumberOfItems");
				max.SetValue(max.GetValue<int>() * 2);
			}
		}

		static void BigTanks(GameObject go)
		{
			foreach (Tank tank in go.GetComponentsInChildren<Tank>(true)) tank.maxCapacity *= 2f;
		}

		static void WaterPlot(GameObject go)
		{
			if (!Raft_Network.IsHost) return;
			foreach (Cropplot plot in go.GetComponentsInChildren<Cropplot>(true))
				if (plot.SlotsNeedWater()) plot.AddWater(false);
		}

		static void Turbo(GameObject go)
		{
			foreach (MotorWheel m in go.GetComponentsInChildren<MotorWheel>(true))
			{
				Traverse t = Traverse.Create(m);
				t.Field("_motorStrenght").SetValue(t.Field("_motorStrenght").GetValue<int>() * TurboStrengthFactor);
				t.Field("_extraMotorStrength").SetValue(t.Field("_extraMotorStrength").GetValue<int>() * TurboStrengthFactor);
				t.Field("_raftSpeed").SetValue(t.Field("_raftSpeed").GetValue<float>() * TurboSpeedFactor);
				t.Field("timePerFuel").SetValue(t.Field("timePerFuel").GetValue<float>() * TurboFuelTimeFactor);
			}
		}

		/// <summary>Raft caps the raft's speed at Raft.maxSpeed (Raft.FixedUpdate), which a turbo engine would reach at
		/// once: the cap is raised by what the running turbo engines add over plain engines.</summary>
		static void TurboTopSpeed()
		{
			Raft raft = ComponentManager<Raft>.Value;
			if (raft == null) return;
			if (raft != speedRaft) { speedRaft = raft; baseMaxSpeed = raft.maxSpeed; }
			Upgrade turbo = Find("DI_TurboEngine");
			float extra = 0f;
			if (turbo != null && turbo.Item != null && RaftVelocityManager.Motors != null)
				foreach (MotorWheel m in RaftVelocityManager.Motors)
				{
					if (m == null || !m.MotorState) continue;
					Block b = m.GetComponentInParent<Block>();
					if (b != null && b.buildableItem == turbo.Item) extra += m.RaftSpeed - m.RaftSpeed / TurboSpeedFactor;
				}
			float want = baseMaxSpeed + extra;
			if (Mathf.Abs(raft.maxSpeed - want) > 0.0001f) raft.maxSpeed = want;
		}

		static void StrongTurbine(GameObject go)
		{
			foreach (WindTurbine w in go.GetComponentsInChildren<WindTurbine>(true))
			{
				Traverse f = Traverse.Create(w).Field("batteryChargesPerTick");
				f.SetValue(f.GetValue<int>() * 2);
			}
		}

		static void ComfyBed(GameObject go)
		{
			foreach (Bed bed in go.GetComponentsInChildren<Bed>(true))
			{
				Traverse t = Traverse.Create(bed);
				foreach (string f in new[] { "healthPercentage", "hungerPercentage", "thirstPercentage" })
					t.Field(f).SetValue(Mathf.Max(t.Field(f).GetValue<float>(), BedRespawnPercent));
				t.Field("healthRegen").SetValue(t.Field("healthRegen").GetValue<float>() * BedRegenFactor);
				t.Field("hungerDecay").SetValue(t.Field("hungerDecay").GetValue<float>() * BedDecayFactor);
				t.Field("thirstDecay").SetValue(t.Field("thirstDecay").GetValue<float>() * BedDecayFactor);
			}
		}

		static void MagnetHook(GameObject go)
		{
			foreach (Hook h in go.GetComponentsInChildren<Hook>(true))
			{
				h.pullSpeed *= HookPullFactor;
				h.gatherTime *= HookGatherFactor;
			}
		}

		static void TitaniumRod(GameObject go)
		{
			foreach (FishingRod r in go.GetComponentsInChildren<FishingRod>(true))
			{
				if (r.bobber == null || !r.bobber.transform.IsChildOf(go.transform)) { Warn("Titanium rod: its bobber isn't part of its model; fish bite as on the metal rod"); continue; }
				Interval_Float w = Traverse.Create(r.bobber).Field("waitTime").GetValue<Interval_Float>();
				if (w == null) continue;
				w.minValue *= RodBiteFactor;
				w.maxValue *= RodBiteFactor;
			}
		}

		static void SwiftFlippers(Equipment e)
		{
			Traverse f = Traverse.Create(e).Field("swimSpeedMultiplier");
			f.SetValue(1f + (f.GetValue<float>() - 1f) * FlipperBoostFactor);
		}

		static void LargeAirTank(Equipment e)
		{
			Traverse f = Traverse.Create(e).Field("oxygenLostMultiplier");
			f.SetValue(f.GetValue<float>() * AirLossFactor);
		}

		static void RapidCharger(GameObject go)
		{
			foreach (BatteryCharger c in go.GetComponentsInChildren<BatteryCharger>(true))
				c.chargePerFuelTick *= ChargerFactor;
		}

		static void FastRecycler(GameObject go)
		{
			foreach (Placeable_Extractor x in go.GetComponentsInChildren<Placeable_Extractor>(true))
			{
				Traverse f = Traverse.Create(x).Field("processCooldown");
				f.SetValue(f.GetValue<float>() * RecyclerTimeFactor);
			}
		}

		static void BrightLight(GameObject go)
		{
			foreach (LightSingularity s in go.GetComponentsInChildren<LightSingularity>(true))
			{
				s.maxRange *= LightRangeFactor;
				s.maxIntensity *= LightIntensityFactor;
			}
			foreach (Light l in go.GetComponentsInChildren<Light>(true))
			{
				l.range *= LightRangeFactor;
				l.intensity *= LightIntensityFactor;
			}
			// (the wooden lantern has no light of its own: Raft's light manager lights it from these numbers when placed)
			foreach (LightSingularityExternal x in go.GetComponentsInChildren<LightSingularityExternal>(true))
			{
				x.lightRange *= LightRangeFactor;
				x.intensity *= LightIntensityFactor;
			}
		}

		static void Telescope(GameObject go)
		{
			foreach (Binoculars b in go.GetComponentsInChildren<Binoculars>(true))
			{
				Traverse f = Traverse.Create(b).Field("minFOV");
				f.SetValue(f.GetValue<float>() * TelescopeFovFactor);
			}
		}

		static void StrongWeapon(GameObject go)
		{
			foreach (MeleeWeapon w in go.GetComponentsInChildren<MeleeWeapon>(true))
			{
				Traverse f = Traverse.Create(w).Field("damage");
				f.SetValue(Mathf.RoundToInt(f.GetValue<int>() * WeaponDamageFactor));
			}
		}

		static void LongPaddle(GameObject go)
		{
			foreach (Paddle p in go.GetComponentsInChildren<Paddle>(true))
			{
				Traverse f = Traverse.Create(p).Field("paddleForce");
				f.SetValue(f.GetValue<float>() * PaddleFactor);
			}
		}

		static void QuickTable(GameObject go)
		{
			foreach (CookingTable t in go.GetComponentsInChildren<CookingTable>(true))
				if (t.GetComponent<QuickCooking>() == null) t.gameObject.AddComponent<QuickCooking>();
		}

		static void SturdyScarecrow(GameObject go)
		{
			foreach (Scarecrow s in go.GetComponentsInChildren<Scarecrow>(true))
				s.invurnerable = true;
		}

		static void MasterHammer(GameObject go)
		{
			foreach (Hammer h in go.GetComponentsInChildren<Hammer>(true))
			{
				Traverse f = Traverse.Create(h).Field("blockRepairAmount");
				f.SetValue(f.GetValue<int>() * HammerRepairFactor);
			}
		}

		static void Longbow(GameObject go)
		{
			// (the bow shoots with ThrowableComponent_Bow; its force is ThrowableComponent.throwForce)
			foreach (ThrowableComponent t in go.GetComponentsInChildren<ThrowableComponent>(true))
			{
				Traverse f = Traverse.Create(t).Field("throwForce");
				f.SetValue(f.GetValue<Vector3>() * BowFactor);
			}
			foreach (Throwable t in go.GetComponentsInChildren<Throwable>(true))
				t.throwForceMultiplier *= BowFactor;
			foreach (ChargeMeter c in go.GetComponentsInChildren<ChargeMeter>(true))
			{
				Traverse f = Traverse.Create(c).Field("chargeSpeed");
				f.SetValue(f.GetValue<float>() * BowFactor);
			}
		}

		static void LumberAxe(GameObject go)
		{
			foreach (Axe a in go.GetComponentsInChildren<Axe>(true))
				a.chopBlockTime *= AxeTimeFactor;
		}

		/// <summary>The head light's lamp sits on the model it shares with the advanced head light: brighter only while worn.</summary>
		static readonly Dictionary<Light, Vector2> lightBase = new Dictionary<Light, Vector2>();

		static void Floodlight(Equipment e, bool on)
		{
			Light l = Traverse.Create(e).Field("lightSourceLight").GetValue<Light>();
			if (l == null)
			{
				// (the upgrade's own copy may not have found its lamp yet: it is under the model it shares with its base)
				Transform m = Traverse.Create(e).Field("localModel").GetValue<Transform>() ?? Traverse.Create(e).Field("remoteModel").GetValue<Transform>();
				l = m != null ? m.GetComponentInChildren<Light>(true) : null;
			}
			if (l == null) return;
			Vector2 b;
			if (!lightBase.TryGetValue(l, out b)) lightBase[l] = b = new Vector2(l.range, l.intensity);
			l.range = on ? b.x * LightRangeFactor : b.x;
			l.intensity = on ? b.y * LightIntensityFactor : b.y;
		}

		#endregion

		#region Hand-held and worn upgrades

		/// <summary>Equipment_Model.Equip / UnEquip: worn upgrades share their base's model, so it gets the upgrade's tint
		/// while one is worn and its own colours back when it comes off or the base is put on; then Wearing runs.</summary>
		public static void WornChanged(Equipment e, bool on)
		{
			if (all == null || e == null || e.equipableItem == null) return;
			Upgrade u = Find(e.equipableItem.UniqueIndex);
			if (u == null && !on) return;
			Traverse t = Traverse.Create(e);
			Color c = u != null && on ? u.Tint : Color.white;
			foreach (string f in new[] { "localModel", "remoteModel" })
			{
				Transform m = t.Field(f).GetValue<Transform>();
				if (m == null) continue;
					if (u != null && on) { Untint(m.gameObject); TintAll(m.gameObject, c, u.Whiten); Shine(m.gameObject, c); }
					else Untint(m.gameObject);
			}
			if (u != null && u.Wearing != null) u.Wearing(e, on);
		}

		/// <summary>In UseItemController.Awake, before it builds its dictionary: each hand-held upgrade gets a copy of its base
		/// item's hand model (tinted, its stats set by Held) and a connection of its own.</summary>
		public static void AddHeldItems(UseItemController c)
		{
			if (all == null || c == null) return;
			var list = Traverse.Create(c).Field("allConnections").GetValue<List<ItemConnection>>();
			if (list == null) return;
			foreach (Upgrade u in Registered.Where(x => x.Held != null).ToList())
			{
				if (list.Any(k => k != null && k.inventoryItem == u.Item)) continue;
				ItemConnection b = list.FirstOrDefault(k => k != null && k.inventoryItem != null && k.obj != null && k.inventoryItem.UniqueIndex == u.Base.UniqueIndex);
				if (b == null) { Warn(u + ": the player has no hand model of " + u.BaseName); continue; }
				GameObject o = UnityEngine.Object.Instantiate(b.obj, b.obj.transform.parent, false);
				o.name = b.obj.name + "_" + u.Name;
				try { TintAll(o, u.Tint, u.Whiten); o.transform.localScale *= HeldGrow; Shine(o, u.Tint); u.Held(o); }
				catch (Exception e) { Warn(u + ": " + e.Message); }
				list.Add(new ItemConnection { name = u.Name, inventoryItem = u.Item, obj = o, objs = b.objs });
			}
		}

		/// <summary>In PlayerEquipment.Awake, before it gathers its Equipment: each worn upgrade gets a copy of its base item's
		/// Equipment component (all its fields, so it shows the same model) on an object of its own, its stats set by Worn.</summary>
		public static void AddEquipment(PlayerEquipment pe)
		{
			if (all == null || pe == null) return;
			Equipment[] have = pe.GetComponentsInChildren<Equipment>(true);
			foreach (Upgrade u in Registered.Where(x => x.Worn != null || x.Wearing != null).ToList())
			{
				if (have.Any(e => e.equipableItem == u.Item)) continue;
				Equipment b = have.FirstOrDefault(e => e.equipableItem != null && e.equipableItem.UniqueIndex == u.Base.UniqueIndex);
				if (b == null) { Warn(u + ": the player has no equipment for " + u.BaseName); continue; }
				var go = new GameObject(b.name + "_" + u.Name);
				go.SetActive(false);
				go.transform.SetParent(b.transform.parent != null ? b.transform.parent : pe.transform, false);
				var c = (Equipment)go.AddComponent(b.GetType());
				for (Type t = b.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
					foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
						f.SetValue(c, f.GetValue(b));
				c.equipableItem = u.Item;
				try { if (u.Worn != null) u.Worn(c); }
				catch (Exception e) { Warn(u + ": " + e.Message); }
				go.SetActive(b.gameObject.activeSelf);
			}
		}

		#endregion

		#region Look

		/// <summary>How much bigger a hand-held upgrade's model is than its base item's. (Placed ones keep their size: a bigger
		/// collider would stop pieces fitting on the grid next to them.)</summary>
		public const float HeldGrow = 1.1f;
		/// <summary>The glints round a placed or hand-held upgrade (a child of its model).</summary>
		public const string ShineName = "DI_UpgradeShine";

		/// <summary>A tint made strong enough to see on Raft's textures (its hue kept): a pale one hardly showed (2026-10-14 pictures).</summary>
		public static Color Vivid(Color c)
		{
			float h, sat, v;
			Color.RGBToHSV(c, out h, out sat, out v);
			return Color.HSVToRGB(h, Mathf.Clamp(Mathf.Max(sat * 1.7f, 0.5f), 0f, 0.85f), 1f);
		}

		/// <summary>Tints every material under root; a material with no colour of its own (Raft's texture-only shaders) through its
		/// textures, as the randomizer's animals. Returns how many materials.</summary>
		public static int TintAll(GameObject root, Color tint, bool whiten = false)
		{
			int n = 0;
			if (root == null) return 0;
			foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
			{
				if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r.GetComponent<TextMesh>() != null) continue;
				// Whiten: a colour over 1 makes TintTextures lift the dark parts (the titanium spear looked like the scrap one)
				int w = whiten ? WorldRandomizer.TintTextures(r, tint * WhitenLift) : 0;
				if (w > 0) { n += w; continue; }
				int k = ObjectProps.TintRenderer(r, whiten ? tint * WhitenLift : tint);
				n += k > 0 ? k : WorldRandomizer.TintTextures(r, tint);
			}
			return n;
		}

		/// <summary>How much lighter than its tint a Whiten upgrade is drawn (over 1: TintTextures lifts by 0.6 of the excess).</summary>
		public const float WhitenLift = 1.9f;

		/// <summary>Takes TintAll and Shine off again (a worn upgrade taken off: the model is shared with its base item).</summary>
		public static void Untint(GameObject root)
		{
			if (root == null) return;
			Transform glints = root.transform.Find(ShineName);
			if (glints != null) { glints.SetParent(null, false); UnityEngine.Object.Destroy(glints.gameObject); }
			foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
			{
				if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r.GetComponent<TextMesh>() != null) continue;
				if (ObjectProps.TintRenderer(r, Color.white) == 0) r.SetPropertyBlock(new MaterialPropertyBlock());
			}
		}

		/// <summary>A few slow glints in the upgrade's colour round its model: something no Raft item has, so an upgrade is
		/// told apart at a glance even where its tint is close to the base item's colours. Once per model.</summary>
		public static void Shine(GameObject root, Color tint)
		{
			if (root == null || root.transform.Find(ShineName) != null) return;
			Bounds b = new Bounds();
			bool any = false;
			foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
			{
				if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
				Bounds rb;
				if (!MeshBounds(r, out rb)) continue;
				if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
			}
			if (!any || b.size.sqrMagnitude < 1e-6f) return;
			try
			{
				var go = new GameObject(ShineName) { layer = root.layer };
				go.transform.SetParent(root.transform, false);
				go.transform.position = b.center;
				go.transform.rotation = Quaternion.identity;
				Vector3 ls = go.transform.lossyScale;
				var ps = go.AddComponent<ParticleSystem>();
				ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				var main = ps.main;
				main.loop = true;
				main.playOnAwake = true;
				main.simulationSpace = ParticleSystemSimulationSpace.Local;
				main.scalingMode = ParticleSystemScalingMode.Shape;
				main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
				main.startSpeed = 0f;
				// (sized and counted by the model: 2-4 small stars on a lantern hardly showed, 2026-10-14 diagnosis)
				float big = Mathf.Max(b.size.x, b.size.y, b.size.z), star = Mathf.Clamp(big * 0.08f, 0.14f, 0.4f);
				main.startSize = new ParticleSystem.MinMaxCurve(star * 0.6f, star);
				main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI / 4f);
				main.maxParticles = 20;
				main.startColor = Color.Lerp(tint, Color.white, 0.5f);
				var shape = ps.shape;
				shape.shapeType = ParticleSystemShapeType.Box;
				shape.scale = new Vector3(b.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), b.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), b.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
				var emission = ps.emission;
				emission.rateOverTime = Mathf.Clamp(big * 3f, 3f, 8f);
				var col = ps.colorOverLifetime;
				col.enabled = true;
				var fade = new Gradient();
				fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
					new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
				col.color = fade;
				var grow = ps.sizeOverLifetime;
				grow.enabled = true;
				grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.4f)));
				var pr = go.GetComponent<ParticleSystemRenderer>();
				pr.sharedMaterial = GlintMaterial();
				pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				pr.receiveShadows = false;
				ps.Play();
			}
			catch (Exception e) { Warn("Glints of " + root.name + ": " + e.Message); }
		}

		static Material glintMaterial;

		/// <summary>A four-pointed star that adds light (a soft dot hardly showed by day, 2026-10-14 close pictures).</summary>
		/// <summary>A renderer's world bounds; from its mesh when Raft has it hidden (a worn model of the local player has empty
		/// bounds then: the head lights got no glints).</summary>
		static bool MeshBounds(Renderer r, out Bounds b)
		{
			b = r.bounds;
			if (b.size.sqrMagnitude > 1e-8f) return true;
			SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
			MeshFilter mf = sk == null ? r.GetComponent<MeshFilter>() : null;
			Bounds local;
			if (sk != null && sk.sharedMesh != null) local = sk.localBounds.size.sqrMagnitude > 1e-8f ? sk.localBounds : sk.sharedMesh.bounds;
			else if (mf != null && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
			else return false;
			Transform t = sk != null && sk.rootBone != null && sk.localBounds.size.sqrMagnitude > 1e-8f ? sk.rootBone : r.transform;
			b = new Bounds(t.TransformPoint(local.center), Vector3.zero);
			for (int i = 0; i < 8; i++)
				b.Encapsulate(t.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
			return b.size.sqrMagnitude > 1e-8f;
		}

		static Material GlintMaterial()
		{
			if (glintMaterial != null) return glintMaterial;
			Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Mobile/Particles/Additive");
			// (Raft's build may not have them: then the alpha-blended one the atmosphere zones use, with this star)
			if (shader == null) shader = AtmosphereZone.ParticleMaterial().shader;
			Log("Upgrade glints use shader " + shader.name);
			const int S = 64;
			var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "DI_Glint", wrapMode = TextureWrapMode.Clamp };
			var px = new Color32[S * S];
			for (int y = 0; y < S; y++)
				for (int x = 0; x < S; x++)
				{
					float dx = Mathf.Abs(x + 0.5f - S / 2f) / (S / 2f), dy = Mathf.Abs(y + 0.5f - S / 2f) / (S / 2f);
					float d = Mathf.Sqrt(dx * dx + dy * dy);
					float core = Mathf.Pow(Mathf.Clamp01(1f - d * 1.6f), 2f);
					float arms = Mathf.Clamp01(1f - dy * 14f) * Mathf.Clamp01(1f - dx) + Mathf.Clamp01(1f - dx * 14f) * Mathf.Clamp01(1f - dy);
					px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(core + arms * 0.9f) * 255));
				}
			tex.SetPixels32(px);
			tex.Apply();
			glintMaterial = new Material(shader) { name = "DI_Glint", mainTexture = tex };
			// (Raft's particle shaders tint by a mid grey by default: half as bright)
			if (glintMaterial.HasProperty("_TintColor")) glintMaterial.SetColor("_TintColor", Color.white);
			return glintMaterial;
		}

		#endregion

		/// <summary>A copy of a (possibly atlased, unreadable) sprite multiplied by tint.</summary>
		static Sprite TintedSprite(Sprite s, Color tint, string name)
		{
			if (s == null) return null;
			try
			{
				Texture2D src = s.texture;
				Rect r = s.textureRect;
				var mat = new Material(Shader.Find("Sprites/Default")) { color = tint };
				var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
				Graphics.Blit(src, rt, mat);
				RenderTexture previous = RenderTexture.active;
				RenderTexture.active = rt;
				var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { name = name + "_Icon" };
				tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
				tex.Apply(false, true);
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(rt);
				UnityEngine.Object.Destroy(mat);
				return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
			}
			catch (Exception e) { Warn("Tinting the icon of " + name + " failed: " + e.Message); return null; }
		}
	}

	/// <summary>On every placed or loaded block of an upgrade (its prefab copies carry it): tints it and sets its better
	/// stats in Awake, before Raft's own Start code reads them, then runs the upgrade's Every every 2 s. The upgrade is found
	/// by the block's item, so the component needs no saved fields.</summary>
	/// <summary>On a steel pot or fast juicer: while it cooks, the host adds the extra time to its cook timer
	/// (the recipe asset is shared, so its cook time stays as it is).</summary>
	public class QuickCooking : MonoBehaviour
	{
		static readonly MethodInfo isCooking = AccessTools.Method(typeof(CookingTable), "IsCooking");
		CookingTable table;
		Traverse timer;

		void Awake()
		{
			table = GetComponent<CookingTable>();
			timer = Traverse.Create(table).Field("cookTimer");
		}

		void Update()
		{
			if (table == null || !Raft_Network.IsHost || isCooking == null || !(bool)isCooking.Invoke(table, null)) return;
			timer.SetValue(timer.GetValue<float>() + Time.deltaTime * (ExtraUpgrades.TableSpeedFactor - 1f));
		}
	}

	public class UpgradeBlock : MonoBehaviour
	{
		ExtraUpgrades.Upgrade upgrade;
		bool done;
		float next;

		public ExtraUpgrades.Upgrade Upgrade { get { return upgrade; } }

		void Awake() { Apply(); }

		public void Apply()
		{
			if (done) return;
			Block block = GetComponent<Block>();
			upgrade = block != null && block.buildableItem != null ? ExtraUpgrades.Find(block.buildableItem.UniqueIndex) : null;
			if (upgrade == null) return;
			done = true;
			try
			{
				ExtraUpgrades.TintAll(gameObject, upgrade.Tint);
				ExtraUpgrades.Shine(gameObject, upgrade.Tint);
				if (upgrade.Setup != null) upgrade.Setup(gameObject);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] " + upgrade.Display + ": " + e.Message); }
		}

		void Update()
		{
			if (upgrade == null || upgrade.Every == null || Time.time < next) return;
			next = Time.time + 2f;
			try { upgrade.Every(gameObject); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] " + upgrade.Display + ": " + e.Message); next = Time.time + 30f; }
		}
	}

	/// <summary>Every machine's battery slot (grill, smelter, purifier, juicer, recycler, Receiver, sprinkler, charger) takes
	/// the large battery too. Awake runs before a loaded world puts its batteries back (RGD_Battery), so a saved large
	/// battery finds its slot ready.</summary>
	[HarmonyPatch(typeof(Battery), "Awake")]
	static class UpgradeBatterySlotPatch
	{
		static void Postfix(Battery __instance)
		{
			try { ExtraUpgrades.AddToSlot(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] A battery slot: " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(UseItemController), "Awake")]
	static class UpgradeHeldItemsPatch
	{
		static void Prefix(UseItemController __instance)
		{
			try { ExtraUpgrades.AddHeldItems(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] Hand-held upgrades: " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(PlayerEquipment), "Awake")]
	static class UpgradeEquipmentPatch
	{
		static void Prefix(PlayerEquipment __instance)
		{
			try { ExtraUpgrades.AddEquipment(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] Worn upgrades: " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(Equipment_Model), "Equip")]
	static class UpgradeWornPatch
	{
		static void Postfix(Equipment_Model __instance)
		{
			try { ExtraUpgrades.WornChanged(__instance, true); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] Putting on: " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(Equipment_Model), "UnEquip")]
	static class UpgradeUnwornPatch
	{
		static void Postfix(Equipment_Model __instance)
		{
			try { ExtraUpgrades.WornChanged(__instance, false); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] Taking off: " + e.Message); }
		}
	}

	/// <summary>A safety net: an upgrade block whose UpgradeBlock didn't come along with the prefab copy gets one when placed.</summary>
	[HarmonyPatch(typeof(Block), "OnFinishedPlacement")]
	static class UpgradeBlockPlacedPatch
	{
		static void Postfix(Block __instance)
		{
			try
			{
				if (__instance == null || __instance.buildableItem == null || ExtraUpgrades.Find(__instance.buildableItem.UniqueIndex) == null) return;
				UpgradeBlock ub = __instance.GetComponent<UpgradeBlock>() ?? __instance.gameObject.AddComponent<UpgradeBlock>();
				ub.Apply();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [upgrades] A placed block: " + e.Message); }
		}
	}

	/// <summary>Raft hands back what a removed block holds (a storage's items, a grill's food) by the block's item index
	/// (RemovePlaceables.ReturnItemsFromBlock switches on it): for an upgrade's block it sees the base item's index there.</summary>
	[HarmonyPatch(typeof(RemovePlaceables), "ReturnItemsFromBlock")]
	static class UpgradeReturnItemsPatch
	{
		static void Prefix(Block block, out Item_Base __state)
		{
			__state = null;
			if (block == null || block.buildableItem == null) return;
			ExtraUpgrades.Upgrade u = ExtraUpgrades.Find(block.buildableItem.UniqueIndex);
			if (u == null || u.Base == null) return;
			__state = block.buildableItem;
			block.buildableItem = u.Base;
		}

		static void Postfix(Block block, Item_Base __state)
		{
			if (__state != null && block != null) block.buildableItem = __state;
		}
	}

	/// <summary>The crafting menu's recipe list: Raft makes it as wide as a row's whole recipe list, hidden recipes too - an
	/// upgrade not learned yet shares its base item's row, and the Weapons list grew over the inventory with two recipes
	/// shown (2026-10-10). Sized by the recipes shown instead. And a new category closes the recipe box of the last one
	/// (Raft left the stone axe open over Food/Water); opening the menu again on the same category keeps it.</summary>
	[HarmonyPatch(typeof(CraftingMenu), "SelectCraftingCategory")]
	static class CraftingListWidthPatch
	{
		static void Prefix(CraftingMenu __instance, out object __state)
		{
			__state = Traverse.Create(__instance).Field("selectedCategory").GetValue();
		}

		static void Postfix(CraftingMenu __instance, CraftingCategory category, object __state)
		{
			try
			{
				var all = Traverse.Create(__instance).Field("allRecipes").GetValue() as System.Collections.IDictionary;
				var rows = all != null ? all[category] as System.Collections.IEnumerable : null;
				if (rows != null && __instance.recipeMenuItemParent != null)
				{
					int cols = 1;
					foreach (object row in rows)
					{
						var recipes = Traverse.Create(row).Field("recipes").GetValue<List<Item_Base>>();
						if (recipes != null) cols = Mathf.Max(cols, recipes.Count(i => i != null && i.settings_recipe != null && i.settings_recipe.CanCraft));
					}
					float x = 315f + (cols > 3 ? 55f * (cols - 3) : 0f); // (Raft's own sizes)
					Vector2 size = __instance.recipeMenuItemParent.sizeDelta;
					if (x < size.x)
					{
						__instance.recipeMenuItemParent.sizeDelta = new Vector2(x, size.y);
						if (__instance.scrollViewRect != null) __instance.scrollViewRect.sizeDelta = new Vector2(x, __instance.scrollViewRect.sizeDelta.y);
					}
				}
				if (__state != null && !Equals(__state, category) && __instance.selectedRecipeBox != null && __instance.selectedRecipeBox.selectedRecipeItem != null)
					__instance.selectedRecipeBox.DisplayRecipe(null);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Crafting menu sizes: " + e.Message); }
		}
	}
}

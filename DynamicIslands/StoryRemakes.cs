using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// ROADMAP CW1: Raft's other story islands rebuilt "in a new way" from their own pieces (the user, 2026-10-03), two
	/// designs each, picked in the generator's Randomize existing tab (Rebuild it) when that island is chosen:
	/// Balboa (a logging camp, a relay station), Caravan Town (a caravan circle, a market on scaffold decks), Tangaroa (a
	/// seaside café, the founder's garden), Varuna Point (a building site, a half-built tower), Temperance (an igloo
	/// village, a weather outpost), Utopia (a water station, a market yard) and the Vasagatan (the lounge on the beach, the
	/// engine yard). Each stands on a levelled pad on the island's land, turned by the seed, with a container of loot.
	/// The pieces' sizes were measured with CIKitInfo (their mesh boxes from their pivots): Put places a piece by the middle
	/// of its box, its bottom on the ground.
	/// </summary>
	public static partial class Remakes
	{
		static IEnumerable<Design> StoryDesigns()
		{
			yield return new Design { Id = "balboa.camp", Scene = "Landmark_BalboaIsland", Label = "A logging camp", Build = LoggingCamp, Kit = BalboaKit,
				Hint = "Balboa's shack in a clearing, a fence of its planks behind, tents, a table with a lantern, the generator and its barrels, bear signs and a locker" };
			yield return new Design { Id = "balboa.relay", Scene = "Landmark_BalboaIsland", Label = "A relay station", Build = RelayStation, Kit = BalboaKit,
				Hint = "A new relay hut of Balboa's relay walls, decks and roof: the radio racks inside, a porch with a railing and an antenna mast" };
			yield return new Design { Id = "caravan.circle", Scene = "Landmark_CaravanIsland", Label = "A caravan circle", Build = CaravanCircle, Kit = CaravanKit,
				Hint = "Caravans in a ring round the town's well and its raft monument, benches, a workbench with tools, tables and the mayor's chest" };
			yield return new Design { Id = "caravan.decks", Scene = "Landmark_CaravanIsland", Label = "A market on scaffold decks", Build = CaravanDecks, Kit = CaravanKit,
				Hint = "Caravans on the town's scaffold decks with steps up, a workshop deck with the workbench, crates, tyres and cable rolls" };
			yield return new Design { Id = "tangaroa.cafe", Scene = "Landmark_Tangaroa", Label = "A seaside café", Build = TangaroaCafe, Kit = TangaroaKit,
				Hint = "Tangaroa's sunshades over its outdoor tables, a kitchen counter, the burger sign, a vending machine, plants and the generator" };
			yield return new Design { Id = "tangaroa.garden", Scene = "Landmark_Tangaroa", Label = "The founder's garden", Build = TangaroaGarden, Kit = TangaroaKit,
				Hint = "The founder's statue on a lawn of benches and plants, the grand piano under a sunshade, the board room's table set out for a sales pitch" };
			yield return new Design { Id = "varuna.site", Scene = "Landmark_VarunaPoint", Label = "A building site", Build = VarunaSite, Kit = VarunaKit,
				Hint = "Varuna Point's break room on its stilts, the excavator and forklift, dumpsters, rubble, steel beams, concrete pipes and the skylift" };
			yield return new Design { Id = "varuna.tower", Scene = "Landmark_VarunaPoint", Label = "A half-built tower", Build = VarunaTower, Kit = VarunaKit,
				Hint = "A lift shaft rising 22 m out of a building site, garage door frames round it, steel beams, the skylift and rubble" };
			yield return new Design { Id = "temperance.igloos", Scene = "Landmark_Temperance", Label = "An igloo village", Build = IglooVillage, Kit = TemperanceKit,
				Hint = "Temperance's domes - a big one and two small - the snowmobile shed, snow vehicles, tarp crates, barrels and ice pillars" };
			yield return new Design { Id = "temperance.outpost", Scene = "Landmark_Temperance", Label = "A weather outpost", Build = WeatherOutpost, Kit = TemperanceKit,
				Hint = "The electrical building under the telephone antenna, the telescope, mirror housings, the snowmobile shed and crates" };
			yield return new Design { Id = "utopia.water", Scene = "Landmark_Utopia", Label = "A water station", Build = UtopiaWater, Kit = UtopiaKit,
				Hint = "Utopia's water tanks on their bamboo stands, pumps, the crane, covered crates and baskets" };
			yield return new Design { Id = "utopia.market", Scene = "Landmark_Utopia", Label = "A market yard", Build = UtopiaMarket, Kit = UtopiaKit,
				Hint = "Market baskets and crates round silver outdoor tables, covered crates, a dog cage, a water tank and the harpoon platform" };
			yield return new Design { Id = "vasagatan.lounge", Scene = "Landmark_Vasagatan", Label = "The lounge on the beach", Build = VasagatanLounge, Kit = VasagatanKit,
				Hint = "The liner's lounge set out on the land: its stage and bars, sofas, carpets, lamps and the DJ's table" };
			yield return new Design { Id = "vasagatan.engine", Scene = "Landmark_Vasagatan", Label = "The engine yard", Build = VasagatanEngine, Kit = VasagatanKit,
				Hint = "The liner's engines and control board on the land, lockers, bunk beds and a garbage station: the crew's salvage camp" };
		}

		#region Kits

		static readonly string[] BalboaKit = { "Balboa_Shack", "Balboa_DecorationPrefabBase_Fence_Mid", "Balboa_DecorationPrefabBase_SimpleTent", "Balboa_DecorationPrefabBase_Table",
			"Balboa_DecorationPrefabBase_Chair", "Balboa_DecorationPrefabBase_Lantern", "Balboa_DecorationPrefabBase_Generator", "Balboa_DecorationPrefabBase_ToxicBarrel",
			"Reef_Wheelbarrow", "BearSign1", "Balboa_DirectionSign", "Balboa_DecorationPrefabBase_SmallLocker", "Obstacle_Moving_Base_PlasticBox_Blue",
			"Obstacle_Moving_Base_PlasticContainer_Red", "Firewood_1", "Balboa_DecorationPrefabBase_Trashcan", "Relay_Floor_deck", "Relay_Wall", "Relay_Wall_door",
			"Relay_Wall_Windows", "Relay_Wall_Window", "Relay_Roof_Mid", "Relay_Roof_End", "Relay_Fence_pole", "Relay_Fence_wood", "Antenna_pole", "Antenna_dish",
			"Antenna_cone", "Balboa_DecorationPrefabBase_BigElectronicRack", "Balboa_DecorationPrefabBase_Desk", "Balboa_DecorationPrefabBase_Desk_Chair",
			"Balboa_DecorationPrefabBase_RadioDevice", "Balboa_DecorationPrefabBase_CRTScreen", "Balboa_DecorationPrefabBase_ElectronicCloset 1", "EmergencyGenerator" };
		static readonly string[] CaravanKit = { "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01", "Well", "RaftMonument", "Bench_01", "BenchTable_01",
			"CaravanWorkbench", "CaravanWorkbench_Tools", "Crate_Big_01", "Crate_Small_02", "Tire_02", "Tire_03", "Cableroll_01", "MetalTable_01", "PlasticChair_01",
			"MayorsChest", "Scaffolding_6x4m", "Stairs", "Pallet", "Candle_01", "DeadTree1" };
		static readonly string[] TangaroaKit = { "RestaSunshadeGround", "Tangaroa_OutdoorFurniture_Table", "Tangaroa_OutdoorFurniture_Chair", "Tangaroa_OutdoorFurniture_Bench",
			"TangaroaKitchen_Bench5_Sink", "TangaroaKitchen_Fridge", "TangaroaStoreSign_ChisBurgers", "VendingMachine_w_Animations", "LargePlant1", "LargePlant2",
			"Generator", "Trashcan_01", "RestaFence", "TangaroaFounderStatue", "TangaroaBench", "Tangaroa_BallroomKit_GrandPiano", "Tangaroa_BoardRoomKit_Table",
			"Tangaroa_BoardRoomKit_ChartBoard", "Dinnerchair_01", "Tangaroa_BirthdayKit_PresentPile1", "Cabinet_01" };
		static readonly string[] VarunaKit = { "Varuna BreakRoomBuilding", "VP_Excavator", "VP_Forklift", "VP_Dumpster01", "VP_Dumpster02", "VP_RubblePile_Medium01",
			"VP_RubblePile_Small01", "VP_RubblePile_Small02", "VP_SteelBeam02", "VP_ConcretePipe02", "VP_NailPole_01", "VP_Skylift_Extended", "VP_ElevatorShaft",
			"VP_GarageDoor_Frame" };
		static readonly string[] TemperanceKit = { "TP_Igloo_Medium_1", "TP_Igloo_Small", "TP_Igloo_Small_2", "SnowmobileShedMesh", "TP_SnowVehicle01", "TP_SnowVehicle02",
			"TP_Moontown_TarpCrate02_Clean", "TP_Moontown_TarpCrate03_Clean", "TP_Moontown_Barrel02", "TP_Moontown_SealedCrate02_Clean", "TP_IcePillar01", "TP_IcePillar02",
			"TP_ElectricalBuilding", "TP_TelephoneAntenna_NoQuestItem", "TP_Telescope", "TP_MirrorHousing_Rotating", "TP_FoldingTable_Large", "TP_FoldingStool",
			"TP_SnowShovel02", "TP_ServerCrate01" };
		static readonly string[] UtopiaKit = { "UT_WaterTank", "UT_WaterPump", "UT_ElectricityIsland_Crane01", "UT_CoveredCrate01", "UT_CoveredCrate02", "UT_CoveredCrate03",
			"UT_MarketBasket01", "UT_MarketBasket02", "UT_MarketBasket03", "UT_MarketCrateSmall01", "UT_MarketCrateSmall02", "UT_EndRoom_Pot01", "UT_DogCage_Medium02",
			"Tangaroa_OutdoorFurnitureSilver_Table", "Tangaroa_OutdoorFurnitureSilver_Chair", "TangaroaBenchSilver", "UT_HarpoonTower_Platform", "Tire_01" };
		static readonly string[] VasagatanKit = { "VG_LoungeStage", "VG_LoungeBar1", "VG_LoungeBar2", "VG_DecorationPrefabBase_SofaU_Leather Variant",
			"VG_DecorationPrefabBase_SofaU_Fabric Variant", "VG_DecorationPrefabBase_Carpet_Rectangle Variant", "VG_DecorationPrefabBase_Carpet_Circle Variant",
			"VG_DecorationPrefabBase_FloorLamp Variant", "VG_DecorationPrefabBase_DJ_MixTable Variant", "VG_DecorationPrefabBase_Armchair Variant",
			"VG_DecorationPrefabBase_DinnerTable Variant", "VG_DecorationPrefabBase_DinnerChair Variant", "VG_LoungeChair", "VG_ControlBoard",
			"VG_DecorationPrefabBase_Engine Variant", "VG_DecorationPrefabBase_Engine_EndPart Variant", "VG_DecorationPrefabBase_Lockers_Closed Variant",
			"VG_DecorationPrefabBase_Lockers_Open Variant", "VG_DecorationPrefabBase_BunkBed Variant", "VG_DecorationPrefabBase_Garbagestation_4 Variant",
			"VG_DecorationPrefabBase_BenchWithDrawers Variant", "VG_DecorationPrefabBase_EmergencyGenerator Variant", "VG_DecorationPrefabBase_Mattress_Regular Variant" };

		#endregion

		#region A skyscraper (ROADMAP LM10)

		/// <summary>
		/// A skyscraper of the Radio Tower's walls and floors (the generator's landmarks, ROADMAP LM10): 12 x 9 m, 6 to 10
		/// storeys of windows on levelled ground at c, a door on the ground floor, a ladder up its front to a railed roof with
		/// a lamp, the dish and a chest.
		/// </summary>
		internal static void Skyscraper(MapKit k, Vector2 c, System.Random r)
		{
			float yaw = Turn(r);
			var a = new Frame(c, yaw, 0f);
			float y0 = Mathf.Max(TopUnder(k, a), TopUnder(k, new Frame(a.At(6f, 0f), yaw, 0f))) + 0.05f;
			a = new Frame(c, yaw, y0);
			var b = new Frame(a.At(6f, 0f), yaw, y0);
			Level(k, a, -7f, 7f, -5.5f, 5.5f, y0 - 0.05f, 6f);
			k.Clear(c, 12f);
			int storeys = 6 + r.Next(5);
			for (int i = 0; i < storeys; i++)
			{
				int floor = i;
				// (open inside: no walls between the halves)
				Storey(k, a.Up(3f * i), sl => sl.StartsWith("r") ? null : floor == 0 && sl == "f1" ? "RT_WallDoor1" : Wall(r, 8));
				Storey(k, b.Up(3f * i), sl => sl.StartsWith("l") ? null : Wall(r, 8));
			}
			Frame ra = a.Up(3f * storeys), rb = b.Up(3f * storeys);
			P(k, ra, "RT_Floor", 0f, 4.5f);
			P(k, rb, "RT_Floor", 0f, 4.5f);
			Railing(k, ra, 0f, right: false);
			Railing(k, rb, 0f, left: false, gap: -1.5f);
			Ladders(k, b, -1.5f, y0, rb.Y);
			P(k, ra, "RT_RoofLamp", -3f, 0f, 0.05f);
			Sit(k, rb, "RT_SatteliteDisc", -4.5f, 2.5f, 0f, 200f);
			Sit(k, ra, "Locker", -5.4f, 3.4f, 0f, 90f, LootOf("Rooftop locker", "Treasure"));
		}

		#endregion

		#region Helpers

		/// <summary>
		/// A pad for a design: a spot on the land (at least 2 m over the sea, gently sloped) whose square half x half reaches
		/// no water, levelled to its average height and cleared of nature, turned by the seed. Null if the land has none.
		/// </summary>
		static Frame? Pad(MapKit k, IslandGenSettings s, float half)
		{
			for (int pass = 0; pass < 2; pass++)
			{
				float reach = s.Radius * (pass == 0 ? 0.55f : 0.8f), spread = pass == 0 ? 3.5f : 5.5f;
				for (int tries = 0; tries < 60; tries++)
				{
					Vector2? c = k.Find(k.Mid, reach, (above, slope) => above > 2f && slope < (pass == 0 ? 16f : 24f), 0f);
					if (!c.HasValue) continue;
					var hs = new List<float>();
					for (float x = -half; x <= half + 0.01f; x += half / 2f)
						for (float z = -half; z <= half + 0.01f; z += half / 2f) hs.Add(k.Ground(c.Value + new Vector2(x, z)));
					if (hs.Min() < k.Sea + 0.8f || hs.Max() - hs.Min() > spread) continue;
					float y = Mathf.Max(hs.Average(), k.Sea + 1.2f);
					var f = new Frame(c.Value, Turn(k.Rnd), y);
					// (a wide blend: a narrow one left sheer banks round a pad cut into a slope)
					Level(k, f, -half, half, -half, half, y, 11f);
					k.Clear(c.Value, half * 1.45f);
					return f;
				}
			}
			// (Raft's built places - Varuna Point, Utopia, the Vasagatan - measure as little or steep land: the pad is made,
			// raised out of the sea or cut into the slope near the middle, and blended into what is round it)
			Vector2 m = k.Highest(k.Mid, Mathf.Max(10f, s.Radius * 0.3f));
			var around = new List<float>();
			for (float x = -half; x <= half + 0.01f; x += half / 2f)
				for (float z = -half; z <= half + 0.01f; z += half / 2f) around.Add(k.Ground(m + new Vector2(x, z)));
			// (its own height: capped low, it cut a pit with sheer banks into a mountain)
			float h = Mathf.Max(around.Average(), k.Sea + 1.5f);
			var made = new Frame(m, Turn(k.Rnd), h);
			Level(k, made, -half - 2f, half + 2f, -half - 2f, half + 2f, h, 12f);
			k.Clear(m, half * 1.45f);
			return made;
		}

		/// <summary>
		/// A piece put by the middle of its mesh box at (cx, cz) of the frame, turned yaw more, its bottom on the ground (the
		/// lowest ground under its box; dy over that for something raised), or at the frame's floor plus dy when onPad.
		/// </summary>
		static IslandObject Put(MapKit k, Frame f, string name, float cx, float cz, float yaw = 0f, float dy = 0f, Dictionary<string, string> props = null, bool onPad = false)
		{
			Bounds b;
			if (!GenBuildings.Measured(name, out b)) b = new Bounds(Vector3.zero, Vector3.one);
			Quaternion q = Quaternion.Euler(0f, yaw, 0f);
			Vector3 off = q * new Vector3(b.center.x, 0f, b.center.z);
			float px = cx - off.x, pz = cz - off.z;
			float ground;
			if (onPad) ground = f.Y;
			else
			{
				ground = float.MaxValue;
				Vector3 ex = q * new Vector3(b.extents.x * 0.8f, 0f, 0f), ez = q * new Vector3(0f, 0f, b.extents.z * 0.8f);
				foreach (Vector3 d in new[] { Vector3.zero, ex + ez, ex - ez, -ex + ez, -ex - ez })
					ground = Mathf.Min(ground, k.Ground(f.At(cx + d.x, cz + d.z)));
			}
			Vector2 w = f.At(px, pz);
			return k.Add(name, new Vector3(w.x, ground + dy - b.min.y, w.y), f.Yaw + yaw, props, 0f);
		}

		/// <summary>A piece whose pivot is its foot (part of its mesh is meant to be under the ground: an antenna's roots, a pump's pipe).</summary>
		static IslandObject Foot(MapKit k, Frame f, string name, float x, float z, float yaw = 0f)
		{
			Vector2 w = f.At(x, z);
			return k.Add(name, new Vector3(w.x, Mathf.Min(k.Ground(w), f.Y), w.y), f.Yaw + yaw, null, 0f);
		}

		static Dictionary<string, string> LootOf(string title, string preset)
		{
			return new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot(preset) }, { ObjectProps.NoteTitle, title } };
		}

		static string Pick(System.Random r, params string[] names) { return names[r.Next(names.Length)]; }

		static float Jit(System.Random r, float amount) { return ((float)r.NextDouble() * 2f - 1f) * amount; }

		#endregion

		#region Balboa

		static string LoggingCamp(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 14f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "Balboa_Shack", 0f, 5f, 0f, 0f, null, true);
			// The fence behind and along the sides (its planks: 3 m each)
			for (float x = -10.5f; x <= 10.6f; x += 3f) Put(k, f, "Balboa_DecorationPrefabBase_Fence_Mid", x, 12.6f, 0f);
			for (float z = -4.5f; z <= 10.6f; z += 3f) { Put(k, f, "Balboa_DecorationPrefabBase_Fence_Mid", -12.6f, z, 90f); Put(k, f, "Balboa_DecorationPrefabBase_Fence_Mid", 12.6f, z, 270f); }
			Put(k, f, "Balboa_DecorationPrefabBase_SimpleTent", -8.5f, -5f, 20f + Jit(r, 10f));
			Put(k, f, "Balboa_DecorationPrefabBase_SimpleTent", -4.5f, -9f, 5f + Jit(r, 10f));
			// The table with a lantern and chairs
			Put(k, f, "Balboa_DecorationPrefabBase_Table", 5f, -5f, 90f, 0f, null, true);
			Bounds tb;
			float top = GenBuildings.Measured("Balboa_DecorationPrefabBase_Table", out tb) ? tb.size.y : 0.92f;
			Put(k, f, "Balboa_DecorationPrefabBase_Lantern", 5.3f, -5f, 0f, top, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_Chair", 5f, -3.8f, 180f, 0f, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_Chair", 5f, -6.2f, 0f, 0f, null, true);
			// The firewood by the tents, the generator and its barrels by the shack
			for (int i = 0; i < 3; i++) Put(k, f, "Firewood_1", -1.5f + i * 0.6f, -6f + Jit(r, 0.3f), Jit(r, 30f), 0f, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_Generator", 9f, 1f, 90f, 0f, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_ToxicBarrel", 10.2f, 2.6f);
			Put(k, f, "Balboa_DecorationPrefabBase_ToxicBarrel", 9.4f, 3.4f);
			Put(k, f, "Obstacle_Moving_Base_PlasticBox_Blue", -9f, 2f, 15f);
			Put(k, f, "Obstacle_Moving_Base_PlasticContainer_Red", -9.5f, -0.5f, 70f);
			Put(k, f, "Reef_Wheelbarrow", -1.5f, -10.5f, 35f);
			Put(k, f, "Balboa_DecorationPrefabBase_Trashcan", 8.5f, -2f);
			Put(k, f, "Balboa_DecorationPrefabBase_SmallLocker", 8f, -8.5f, 180f, 0f, LootOf("Logger's locker", "Basics"), true);
			Put(k, f, "BearSign1", -11.5f, -12f, 200f);
			Put(k, f, "Balboa_DirectionSign", 11.5f, -11.5f, 160f);
			return "a logging camp round Balboa's shack";
		}

		static string RelayStation(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 11f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			const float deckTop = 0.3f;
			// The deck: four of the relay's 5.23 m decks, 10.46 m square
			foreach (float x in new[] { -2.615f, 2.615f }) foreach (float z in new[] { -2.615f, 2.615f }) Put(k, f, "Relay_Floor_deck", x, z, 0f, 0f, null, true);
			// The hut on the west part: 7 x 10.5 m (two walls by three), the door to the porch on the east
			float hx = -1.745f, w = 3.49f, d = 5.235f;
			foreach (float x in new[] { hx - w / 2f, hx + w / 2f })
			{
				Put(k, f, Pick(r, "Relay_Wall", "Relay_Wall_Windows", "Relay_Wall_Window"), x, d, 0f, deckTop, null, true);
				Put(k, f, Pick(r, "Relay_Wall", "Relay_Wall_Windows"), x, -d, 180f, deckTop, null, true);
			}
			foreach (float z in new[] { -w, 0f, w })
			{
				Put(k, f, Pick(r, "Relay_Wall", "Relay_Wall_Window"), hx - w, z, 90f, deckTop, null, true);
				Put(k, f, z == 0f ? "Relay_Wall_door" : "Relay_Wall_Windows", hx + w, z, 270f, deckTop, null, true);
			}
			// The roof: three slices of 3.6 m along the hut, the ends turned out
			float roofY = deckTop + 4.49f - 0.05f;
			Put(k, f, "Relay_Roof_End", hx, -3.6f, 180f, roofY, null, true);
			Put(k, f, "Relay_Roof_Mid", hx, 0f, 0f, roofY, null, true);
			Put(k, f, "Relay_Roof_End", hx, 3.6f, 0f, roofY, null, true);
			// Inside: the racks along the back, the radio desk, a closet with the station's spares
			Put(k, f, "Balboa_DecorationPrefabBase_BigElectronicRack", hx - w + 0.75f, 1.5f, 90f, deckTop, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_Desk", hx, -3.6f, 0f, deckTop, null, true);
			Bounds db;
			float desk = deckTop + (GenBuildings.Measured("Balboa_DecorationPrefabBase_Desk", out db) ? db.size.y : 0.94f);
			Put(k, f, "Balboa_DecorationPrefabBase_RadioDevice", hx - 0.4f, -3.9f, 0f, desk, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_CRTScreen", hx + 0.7f, -3.8f, 180f, desk, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_Desk_Chair", hx, -2.4f, 180f, deckTop, null, true);
			Put(k, f, "Balboa_DecorationPrefabBase_ElectronicCloset 1", hx - w + 0.7f, -3.8f, 90f, deckTop, LootOf("Relay spares", "Metal"), true);
			// The porch: a railing on its edge, the mast with the dish and cone, the generator
			for (float z = -4.6f; z <= 4.61f; z += 1.84f) Put(k, f, "Relay_Fence_pole", 5.05f, z, 0f, deckTop, null, true);
			for (float z = -3.68f; z <= 3.69f; z += 1.84f) Put(k, f, "Relay_Fence_wood", 5.05f, z, 90f, deckTop + 0.75f, null, true);
			Put(k, f, "Antenna_pole", 4.3f, 4.3f, 0f, deckTop, null, true);
			Put(k, f, "Antenna_pole", 4.3f, 4.3f, 0f, deckTop + 6.3f, null, true);
			Put(k, f, "Antenna_dish", 4.3f, 4.05f, 180f, deckTop + 11.6f, null, true);
			Put(k, f, "Antenna_cone", 4.3f, 4.55f, 0f, deckTop + 10.2f, null, true);
			Put(k, f, "EmergencyGenerator", 3.6f, -4f, 0f, deckTop, null, true);
			return "a relay station of Balboa's relay walls";
		}

		#endregion

		#region Caravan Town

		static readonly string[] Caravans = { "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01" };

		static string CaravanCircle(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 14f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "Well", 0f, 0f, 0f, 0f, null, true);
			Put(k, f, "RaftMonument", 0f, -6f, 180f, 0f, null, true);
			int n = 6 + r.Next(2);
			for (int i = 0; i < n; i++)
			{
				float a = (i + 0.5f) * 360f / n + Jit(r, 6f);
				Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 10.5f);
				// (side on to the well)
				Put(k, f, Caravans[r.Next(Caravans.Length)], p.x, p.z, a + 90f + Jit(r, 8f));
			}
			for (int i = 0; i < 4; i++)
			{
				float a = i * 90f + 45f;
				Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 4f);
				Put(k, f, "Bench_01", p.x, p.z, a, 0f, null, true);
			}
			Put(k, f, "BenchTable_01", 5.5f, 3f, 20f, 0f, null, true);
			Put(k, f, "CaravanWorkbench", -6f, 3.5f, 90f, 0f, null, true);
			Bounds wb;
			Put(k, f, "CaravanWorkbench_Tools", -6f, 3.5f, 90f, GenBuildings.Measured("CaravanWorkbench", out wb) ? wb.size.y : 0.97f, null, true);
			Put(k, f, "MetalTable_01", 5.5f, -3.5f, 0f, 0f, null, true);
			Put(k, f, "PlasticChair_01", 4.5f, -3.5f, 90f, 0f, null, true);
			Put(k, f, "PlasticChair_01", 6.5f, -3.5f, 270f, 0f, null, true);
			Put(k, f, "Candle_01", 5.5f, -3.2f, 0f, 0.7f, null, true);
			Put(k, f, "MayorsChest", -4f, -5f, 30f, 0f, LootOf("The mayor's chest", "Treasure"), true);
			for (int i = 0; i < 4; i++) Put(k, f, Pick(r, "Crate_Big_01", "Crate_Small_02", "Tire_02", "Tire_03", "Cableroll_01"), -7.5f + Jit(r, 1.5f), -2f + Jit(r, 2f), Jit(r, 90f), 0f, null, true);
			Put(k, f, "DeadTree1", 13f, -12f, Jit(r, 180f));
			return "a caravan circle round the well";
		}

		static string CaravanDecks(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 12f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			// Three decks of 6 x 4 m side by side (1 m up), steps to each, a fourth for the workshop
			float deck = 1f;
			var decks = new[] { new Vector2(-6.5f, 3f), new Vector2(0f, 3f), new Vector2(6.5f, 3f), new Vector2(3.25f, -4.5f) };
			for (int i = 0; i < decks.Length; i++)
			{
				Vector2 d = decks[i];
				Put(k, f, "Scaffolding_6x4m", d.x, d.y, 0f, 0f, null, true);
				Put(k, f, "Stairs", d.x, d.y + (i < 3 ? -3.3f : 3.3f), i < 3 ? 0f : 180f, 0f, null, true);
				if (i < 3) Put(k, f, Caravans[r.Next(Caravans.Length)], d.x, d.y + 0.2f, 180f + Jit(r, 3f), deck, null, true);
			}
			// The workshop deck
			Vector2 w = decks[3];
			Put(k, f, "CaravanWorkbench", w.x - 1.5f, w.y - 1f, 180f, deck, null, true);
			Bounds wb;
			Put(k, f, "CaravanWorkbench_Tools", w.x - 1.5f, w.y - 1f, 180f, deck + (GenBuildings.Measured("CaravanWorkbench", out wb) ? wb.size.y : 0.97f), null, true);
			Put(k, f, "MayorsChest", w.x + 1.8f, w.y - 0.8f, 180f, deck, LootOf("Market chest", "Metal"), true);
			Put(k, f, "Cableroll_01", w.x + 0.5f, w.y + 0.8f, 0f, deck, null, true);
			// Below and round them: pallets, crates, tyres
			Put(k, f, "Pallet", -6f, -5f, 10f, 0f, null, true);
			for (int i = 0; i < 6; i++) Put(k, f, Pick(r, "Crate_Big_01", "Crate_Small_02", "Tire_02", "Tire_03", "Cableroll_01"), -9f + Jit(r, 3f), -5f + Jit(r, 3f), Jit(r, 180f), 0f, null, true);
			Put(k, f, "Bench_01", 9f, -4f, 270f, 0f, null, true);
			Put(k, f, "BenchTable_01", 9f, -7f, 0f, 0f, null, true);
			return "a market of caravans on scaffold decks";
		}

		#endregion

		#region Tangaroa

		static string TangaroaCafe(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 11f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			// Four sunshades, each over a table with chairs
			foreach (Vector2 c in new[] { new Vector2(-5f, -2f), new Vector2(0f, -4f), new Vector2(5f, -2f), new Vector2(0f, 2.5f) })
			{
				Put(k, f, "RestaSunshadeGround", c.x, c.y, 0f, 0f, null, true);
				Put(k, f, "Tangaroa_OutdoorFurniture_Table", c.x + 1.3f, c.y, Jit(r, 20f), 0f, null, true);
				Put(k, f, "Tangaroa_OutdoorFurniture_Chair", c.x + 1.3f, c.y + 1.1f, 180f + Jit(r, 15f), 0f, null, true);
				Put(k, f, "Tangaroa_OutdoorFurniture_Chair", c.x + 1.3f, c.y - 1.1f, Jit(r, 15f), 0f, null, true);
			}
			// The counter along the back, the fridge, the sign over it all, the vending machine, plants on the edge
			Put(k, f, "TangaroaKitchen_Bench5_Sink", -2f, 8f, 180f, 0f, null, true);
			Put(k, f, "TangaroaKitchen_Fridge", 0.6f, 8.2f, 180f, 0f, LootOf("Café fridge", "Food"), true);
			Put(k, f, "TangaroaStoreSign_ChisBurgers", -2f, 9.5f, 180f, 0f, null, true);
			Put(k, f, "VendingMachine_w_Animations", 6.5f, 7.5f, 180f, 0f, null, true);
			Put(k, f, "Generator", -8f, 7.5f, 0f, 0f, null, true);
			Put(k, f, "Trashcan_01", 3f, 8f, 0f, 0f, null, true);
			for (float x = -9f; x <= 9.1f; x += 4.5f) Put(k, f, Pick(r, "LargePlant1", "LargePlant2"), x, -9f, Jit(r, 180f), 0f, null, true);
			for (float x = -7.5f; x <= 7.6f; x += 1.25f) if (Mathf.Abs(x) > 1.3f) Put(k, f, "RestaFence", x, -7.5f, 0f, 0f, null, true);
			return "a seaside café of Tangaroa's furniture";
		}

		static string TangaroaGarden(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 11f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "TangaroaFounderStatue", 0f, 0f, 180f, 0f, null, true);
			for (int i = 0; i < 6; i++)
			{
				float a = i * 60f;
				Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 5.5f);
				if (i % 2 == 0) Put(k, f, "TangaroaBench", p.x, p.z, a + 180f, 0f, null, true);
				else Put(k, f, Pick(r, "LargePlant1", "LargePlant2"), p.x, p.z, Jit(r, 180f), 0f, null, true);
			}
			// The piano under a sunshade, the sales pitch at the board room's table, the presents for the buyers
			Put(k, f, "RestaSunshadeGround", -7f, 6f, 0f, 0f, null, true);
			Put(k, f, "Tangaroa_BallroomKit_GrandPiano", -7f, 7.5f, 200f, 0f, null, true);
			Put(k, f, "Tangaroa_BoardRoomKit_Table", 7f, 5.5f, 90f, 0f, null, true);
			for (int i = 0; i < 3; i++)
			{
				Put(k, f, "Dinnerchair_01", 5.5f + i * 1.5f, 4.4f, 0f, 0f, null, true);
				Put(k, f, "Dinnerchair_01", 5.5f + i * 1.5f, 6.6f, 180f, 0f, null, true);
			}
			Put(k, f, "Tangaroa_BoardRoomKit_ChartBoard", 10f, 5.5f, 270f, 0f, null, true);
			Put(k, f, "Tangaroa_BirthdayKit_PresentPile1", -3f, -7f, Jit(r, 40f), 0f, null, true);
			Put(k, f, "Cabinet_01", 3.5f, -8f, 0f, 0f, LootOf("Sales office cabinet", "Treasure"), true);
			return "the founder's garden";
		}

		#endregion

		#region Varuna Point

		static string VarunaSite(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 14f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "Varuna BreakRoomBuilding", -4f, 6.5f, 0f, 0f, null, true);
			Put(k, f, "VP_Excavator", 8f, -5f, 200f + Jit(r, 20f), 0f, null, true);
			Put(k, f, "VP_Forklift", -8f, -6f, 60f + Jit(r, 20f), 0f, null, true);
			Put(k, f, "VP_Dumpster01", 9.5f, 6f, 90f, 0f, LootOf("Site dumpster", "Metal"), true);
			Put(k, f, "VP_Dumpster02", 9.5f, 10.5f, 90f, 0f, null, true);
			Put(k, f, "VP_RubblePile_Medium01", 2f, -9f, Jit(r, 180f), 0f, null, true);
			Put(k, f, "VP_RubblePile_Small01", -10f, 0f, Jit(r, 180f), 0f, null, true);
			for (int i = 0; i < 4; i++) Put(k, f, "VP_SteelBeam02", -1f, -1.5f + i * 0.75f, 0f, (i % 2) * 0.64f, null, true);
			Put(k, f, "VP_ConcretePipe02", 4f, 1f, 90f, 0f, null, true);
			Put(k, f, "VP_ConcretePipe02", 4f, 3.6f, 90f, 0f, null, true);
			Put(k, f, "VP_NailPole_01", -11f, -11f, Jit(r, 180f));
			Put(k, f, "VP_NailPole_01", 11f, -11.5f, Jit(r, 180f));
			Put(k, f, "VP_Skylift_Extended", -11.5f, 6f, 90f, 0f, null, true);
			return "a building site round Varuna Point's break room";
		}

		static string VarunaTower(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 12f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			// (the shaft's foot is a metre under its pivot: it stands on the pad)
			Put(k, f, "VP_ElevatorShaft", 0f, 0f, 0f, 1.09f, null, true);
			Put(k, f, "VP_GarageDoor_Frame", 0f, -6.5f, 0f, 0f, null, true);
			Put(k, f, "VP_GarageDoor_Frame", 6.5f, 0f, 90f, 0f, null, true);
			Put(k, f, "VP_Skylift_Extended", -5.5f, 0f, 90f, 0f, null, true);
			for (int i = 0; i < 5; i++) Put(k, f, "VP_SteelBeam02", -7f, -7f + i * 0.75f, 90f, (i % 2) * 0.64f, null, true);
			Put(k, f, "VP_RubblePile_Small02", 7f, 8f, Jit(r, 180f), 0f, null, true);
			Put(k, f, "VP_RubblePile_Medium01", -6f, 8f, Jit(r, 180f), 0f, null, true);
			Put(k, f, "VP_Dumpster01", 8.5f, -7f, 0f, 0f, LootOf("Site dumpster", "Metal"), true);
			Put(k, f, "VP_Forklift", 9f, 4f, 250f, 0f, null, true);
			Put(k, f, "VP_ConcretePipe02", -2f, 9f, 0f, 0f, null, true);
			return "a half-built tower 22 m high";
		}

		#endregion

		#region Temperance

		static string IglooVillage(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 17f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "TP_Igloo_Medium_1", 0f, 4f, 0f, 0f, null, true);
			Put(k, f, "TP_Igloo_Small", -11f, -5f, 60f, 0f, null, true);
			Put(k, f, "TP_Igloo_Small_2", 10f, -6f, 300f, 0f, null, true);
			Put(k, f, "SnowmobileShedMesh", 12f, 9f, 270f, 0f, null, true);
			Put(k, f, "TP_SnowVehicle01", 7f, 12f, 200f + Jit(r, 15f), 0f, null, true);
			Put(k, f, "TP_SnowVehicle02", -3f, -10f, 30f + Jit(r, 15f), 0f, null, true);
			Put(k, f, "TP_Moontown_TarpCrate03_Clean", -12f, 8f, 90f, 0f, null, true);
			Put(k, f, "TP_Moontown_TarpCrate02_Clean", -12f, 12f, 80f, 0f, null, true);
			for (int i = 0; i < 3; i++) Put(k, f, "TP_Moontown_Barrel02", -8.5f + i * 0.8f, 10.5f + Jit(r, 0.3f), Jit(r, 180f), 0f, null, true);
			Put(k, f, "TP_Moontown_SealedCrate02_Clean", 3f, -7f, 20f, 0f, LootOf("Village stores", "Food"), true);
			Put(k, f, "TP_SnowShovel02", 4.2f, -7.4f, 0f, 0f, null, true);
			Put(k, f, "TP_IcePillar01", -15f, -14f, Jit(r, 180f));
			Put(k, f, "TP_IcePillar02", 15f, -14f, Jit(r, 180f));
			return "an igloo village";
		}

		static string WeatherOutpost(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 13f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			// (the antenna's legs reach 4 m under its pivot: its foot on the pad)
			Foot(k, f, "TP_TelephoneAntenna_NoQuestItem", 0f, 3f, 0f);
			Put(k, f, "TP_ElectricalBuilding", -7f, -4f, 90f, 0f, null, true);
			Put(k, f, "TP_Telescope", 7f, -6f, 200f, 0f, null, true);
			Put(k, f, "TP_MirrorHousing_Rotating", 9f, 0f, 0f, 0f, null, true);
			Put(k, f, "TP_MirrorHousing_Rotating", 9f, 4f, 0f, 0f, null, true);
			Put(k, f, "SnowmobileShedMesh", -8f, 9f, 180f, 0f, null, true);
			Put(k, f, "TP_SnowVehicle01", -2f, 11f, 160f, 0f, null, true);
			Put(k, f, "TP_FoldingTable_Large", 3f, -9f, 0f, 0f, null, true);
			Put(k, f, "TP_FoldingStool", 3f, -8f, Jit(r, 30f), 0f, null, true);
			Put(k, f, "TP_ServerCrate01", -2f, -9f, 0f, 0f, LootOf("Outpost crate", "Metal"), true);
			Put(k, f, "TP_Moontown_TarpCrate02_Clean", -11f, 2f, 90f, 0f, null, true);
			return "a weather outpost under the antenna";
		}

		#endregion

		#region Utopia

		static string UtopiaWater(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 13f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			// The crane lengthwise along the pad's west side (its frame is 10 x 20 m: turned across, it stood out over the
			// pad's edge and past the island - the user, 2026-10-06), the tanks and pumps beside it
			Put(k, f, "UT_ElectricityIsland_Crane01", -7f, 0f, 0f, 0f, null, true);
			Put(k, f, "UT_WaterTank", 4f, 6f, 0f, 0f, null, true);
			Put(k, f, "UT_WaterTank", 4f, -1f, 0f, 0f, null, true);
			Foot(k, f, "UT_WaterPump", 10.5f, 6f, 270f);
			Foot(k, f, "UT_WaterPump", 10.5f, -1f, 270f);
			Put(k, f, "UT_CoveredCrate03", 2f, -8.5f, 15f, 0f, LootOf("Station stores", "Basics"), true);
			Put(k, f, "UT_CoveredCrate02", 6.5f, -9f, 80f, 0f, null, true);
			Put(k, f, "UT_CoveredCrate01", 9.5f, -8f, 30f, 0f, null, true);
			for (int i = 0; i < 3; i++) Put(k, f, Pick(r, "UT_MarketBasket01", "UT_MarketBasket02", "UT_MarketBasket03"), 0.5f + i * 1.3f, 10.5f + Jit(r, 0.4f), Jit(r, 180f), 0f, null, true);
			Put(k, f, "Tire_01", -1f, -10.5f, Jit(r, 180f), 0f, null, true);
			return "a water station of Utopia's tanks";
		}

		static string UtopiaMarket(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 12f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			foreach (Vector2 c in new[] { new Vector2(-3f, 0f), new Vector2(3f, 0f), new Vector2(0f, -4.5f) })
			{
				Put(k, f, "Tangaroa_OutdoorFurnitureSilver_Table", c.x, c.y, Jit(r, 30f), 0f, null, true);
				Put(k, f, "Tangaroa_OutdoorFurnitureSilver_Chair", c.x + 1f, c.y, 270f, 0f, null, true);
				Put(k, f, "Tangaroa_OutdoorFurnitureSilver_Chair", c.x - 1f, c.y, 90f, 0f, null, true);
			}
			// The stalls: baskets and small crates along two sides
			for (float x = -9f; x <= 9.1f; x += 1.5f) Put(k, f, Pick(r, "UT_MarketBasket01", "UT_MarketBasket02", "UT_MarketBasket03", "UT_MarketCrateSmall01", "UT_MarketCrateSmall02"), x, 7.5f + Jit(r, 0.4f), Jit(r, 180f), 0f, null, true);
			for (float z = -6f; z <= 6.1f; z += 1.5f) Put(k, f, Pick(r, "UT_MarketBasket01", "UT_MarketBasket02", "UT_MarketCrateSmall01"), -9.5f + Jit(r, 0.4f), z, Jit(r, 180f), 0f, null, true);
			Put(k, f, "TangaroaBenchSilver", 7f, -6f, 0f, 0f, null, true);
			Put(k, f, "UT_DogCage_Medium02", 9f, 1f, 270f, 0f, null, true);
			Put(k, f, "UT_CoveredCrate02", 8f, -9f, 20f, 0f, LootOf("Market crate", "Food"), true);
			Put(k, f, "UT_EndRoom_Pot01", -7f, -9f, 0f, 0f, null, true);
			Put(k, f, "UT_EndRoom_Pot01", 3f, 9.5f, 0f, 0f, null, true);
			Put(k, f, "UT_HarpoonTower_Platform", -6f, 11f, 180f);
			return "a market yard";
		}

		#endregion

		#region Vasagatan

		static string VasagatanLounge(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 11f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "VG_LoungeStage", 0f, 7f, 180f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_DJ_MixTable Variant", 0f, 7.2f, 180f, 0.51f, null, true);
			Put(k, f, "VG_LoungeBar1", -3f, -7f, 0f, 0f, null, true);
			Put(k, f, "VG_LoungeBar2", 6f, -7f, 0f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_Carpet_Rectangle Variant", -4f, 1f, 0f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_SofaU_Leather Variant", -4f, 1.6f, 180f, 0.03f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_Carpet_Circle Variant", 4f, 1f, 0f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_SofaU_Fabric Variant", 4f, 1.6f, 180f, 0.03f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_DinnerTable Variant", 0f, -2.5f, 0f, 0f, null, true);
			for (int i = 0; i < 4; i++)
			{
				float a = i * 90f;
				Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 1.3f);
				Put(k, f, "VG_DecorationPrefabBase_DinnerChair Variant", p.x, -2.5f + p.z, a + 180f, 0f, null, true);
			}
			foreach (float x in new[] { -9f, 9f }) Put(k, f, "VG_DecorationPrefabBase_FloorLamp Variant", x, 6f, 0f, 0f, null, true);
			for (int i = 0; i < 3; i++) Put(k, f, "VG_LoungeChair", -8f + i * 2.2f, -10f, 180f + Jit(r, 10f));
			Put(k, f, "VG_DecorationPrefabBase_Armchair Variant", 9f, -2f, 270f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_BenchWithDrawers Variant", 9.5f, 1.5f, 270f, 0f, LootOf("Lounge cabinet", "Treasure"), true);
			return "the Vasagatan's lounge on the land";
		}

		static string VasagatanEngine(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Frame? pad = Pad(k, s, 11f);
			if (!pad.HasValue) return null;
			Frame f = pad.Value;
			Put(k, f, "VG_ControlBoard", 0f, 6f, 180f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_Engine Variant", -4f, -1f, 90f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_Engine_EndPart Variant", -2.2f, -1f, 90f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_Engine Variant", 3f, -1f, 270f + Jit(r, 10f), 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_EmergencyGenerator Variant", 6f, -4f, 0f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_BunkBed Variant", -8f, -6f, 90f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_BunkBed Variant", -8f, -9f, 90f, 0f, null, true);
			Put(k, f, "VG_DecorationPrefabBase_Mattress_Regular Variant", -5f, -9f, Jit(r, 30f), 0f, null, true);
			for (int i = 0; i < 3; i++) Put(k, f, i == 1 ? "VG_DecorationPrefabBase_Lockers_Open Variant" : "VG_DecorationPrefabBase_Lockers_Closed Variant", 9f, -1f + i * 1f, 270f, 0f, i == 0 ? LootOf("Crew locker", "Metal") : null, true);
			Put(k, f, "VG_DecorationPrefabBase_Garbagestation_4 Variant", 7f, -9f, 0f, 0f, null, true);
			return "the Vasagatan's engine yard";
		}

		#endregion
	}
}

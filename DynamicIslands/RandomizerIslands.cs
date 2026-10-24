using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A look borrowed from one of Raft's quest islands, for dressing a spot of a randomized island: a centrepiece, props
	/// around it and small things between (all measured in raft_props.txt), a container and a note.
	/// </summary>
	public class Theme
	{
		public string Name, Label;
		/// <summary>Island styles it suits (TerrainPainter styles).</summary>
		public int[] Styles;
		public string[] Anchors, Medium, Small;
		public string Loot, Container, NoteKind, NoteTitle;
		public string[] Notes;
		/// <summary>Animals that fit it (a spot near the scene now and then), or null.</summary>
		public string Creature;
	}

	/// <summary>
	/// The world randomizer's bigger pieces: themed scenes from the quest islands' props, caves (Raft's own cave pieces:
	/// set into a hill on generated islands, or under a mound of rocks on Raft's islands, whose ground can't be dug), and
	/// the large islands made like Raft's big ones.
	/// </summary>
	public static class RandomizerIslands
	{
		const int Tropical = TerrainPainter.Tropical, Snowy = TerrainPainter.Snowy, Desert = TerrainPainter.Desert, Forest = TerrainPainter.Forest, Volcanic = TerrainPainter.Volcanic;

		public static readonly Theme[] Themes =
		{
			new Theme { Name = "scrapyard", Label = "Scrapyard", Styles = new[] { Tropical, Desert, Forest, Volcanic },
				Anchors = new[] { "VP_Excavator", "VP_Forklift", "VP_Dumpster01" },
				Medium = new[] { "VP_ConcretePipe01", "VP_ConcretePipe02", "VP_Dumpster02", "VP_BrickStack01", "VP_BrickStack02", "VP_BrickStack03", "VP_ExplosiveBarrel_Pile", "Pallet" },
				Small = new[] { "VP_MetalCrate01", "VP_MetalCrate02", "VP_MetalCrate03", "VP_Cable_Roll01", "Tire_02", "Tire_03", "VP_FoldingLadder_Tall" },
				Loot = "Metal", Container = "Loot_Crate", NoteKind = "Note_Papers", NoteTitle = "Site log",
				Notes = new[] { "Work stopped when the water came. The machines stayed.", "Whoever finds the key to the digger: it doesn't matter any more. Take the scrap.", "Shift report: flooded. Next shift: cancelled." } },
			new Theme { Name = "market", Label = "Old market", Styles = new[] { Tropical, Desert },
				Anchors = new[] { "RestaSunshadeGround", "Tangaroa_OutdoorFurnitureSilver_Table" },
				Medium = new[] { "UT_MarketStackBox01", "Tangaroa_OutdoorFurnitureSilver_Bench", "UT_CoveredCrate01", "UT_CoveredCrate02", "UT_Generator01" },
				Small = new[] { "UT_MarketBasket01", "UT_MarketBasket02", "UT_MarketBasket03", "UT_MarketCrateSmall01", "UT_MarketCrateSmall02", "Tangaroa_OutdoorFurnitureSilver_Chair" },
				Loot = "Food", Container = "Loot_Box", NoteKind = "Note_Sign", NoteTitle = "Market sign",
				Notes = new[] { "Fresh fruit! Fair prices! (Closed until further notice.)", "Trade here: fish for rope, rope for planks.", "The market moves with the tide. Back next season." } },
			new Theme { Name = "caravan", Label = "Caravan outpost", Styles = new[] { Tropical, Desert, Volcanic },
				Anchors = new[] { "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01", "Scaffolding_6x4m" },
				Medium = new[] { "BenchTable_01", "Crate_Big_01", "Crate_Big_02", "FishingNets_02", "MetalSheet_2", "MetalSheet_3", "Bench_01" },
				Small = new[] { "PlasticChair_01", "Cableroll_01", "Cableroll_02", "Crate_Small_02", "Tire_02", "Candle_01", "RopeFence_Short", "MetalTable_01" },
				Loot = "Basics", Container = "Loot_Barrel", NoteKind = "Note_Board", NoteTitle = "Outpost notice",
				Notes = new[] { "Caravan three went on to the east. Water in the barrel, use it well.", "Outpost rules: share the catch, mend the nets, don't feed the gulls.", "We'll be back when the storms settle." } },
			new Theme { Name = "bear", Label = "Bear country", Styles = new[] { Forest, Tropical, Snowy },
				Anchors = new[] { "Balboa_DecorationPrefabBase_SimpleTent", "Balboa_DecorationPrefabBase_OldCouch" },
				Medium = new[] { "Balboa_DecorationPrefabBase_Fence_Mid", "Balboa_DecorationPrefabBase_Fence_EndL", "Balboa_DecorationPrefabBase_Fence_EndBroken", "Balboa_DecorationPrefabBase_Old Stove",
					"Balboa_DecorationPrefabBase_Wooden Spikes", "Balboa_DecorationPrefabBase_Generator", "Antenna_dish", "Balboa_DecorationPrefabBase_Table" },
				Small = new[] { "BearSign1", "BearSign2 Variant", "Firewood_1", "Balboa_DecorationPrefabBase_Lantern", "Balboa_DecorationPrefabBase_Trashcan", "Balboa_DecorationPrefabBase_ToxicBarrel", "Balboa_DecorationPrefabBase_Chair", "Balboa_DirectionSign" },
				Loot = "Food", Container = "Loot_Chest", NoteKind = "Note_Sign", NoteTitle = "Warning", Creature = "Bear",
				Notes = new[] { "BEWARE OF THE BEAR. Seriously.", "Do not leave food out. It will find it. It always finds it.", "Camp moved inland. The bear did not." } },
			new Theme { Name = "frozen", Label = "Frozen camp", Styles = new[] { Snowy },
				Anchors = new[] { "TP_Igloo_Small", "SnowmobileShedMesh" },
				Medium = new[] { "TP_FoldingTable_Large", "TP_IcePillar01", "TP_IcePillar02" },
				Small = new[] { "TP_FoldingStool", "TP_Moontown_Barrel02", "TP_Moontown_SealedCrate02_Clean", "TP_Moontown_TarpCrate02_Clean" },
				Loot = "Treasure", Container = "Loot_Crate", NoteKind = "Note_Paper", NoteTitle = "Expedition note",
				Notes = new[] { "Day 9 of the survey. Minus twenty. The igloos hold.", "Supplies in the crate. Keep it closed, the foxes are clever.", "We are going back. Tell Olof we tried." } },
			new Theme { Name = "garden", Label = "Hotel garden", Styles = new[] { Tropical, Forest },
				Anchors = new[] { "RestaSunshadeGround", "TangaroaFounderStatue" },
				Medium = new[] { "LargePlant1", "LargePlant2", "Table_Common_01", "RestaFence" },
				Small = new[] { "MediumPlant1", "MediumPlant2", "SmallPlant1", "Flowerpot_01", "Flowerpot_02", "Flowerpot_03", "Dinnerchair_01" },
				Loot = "Treasure", Container = "Loot_ChestSmall", NoteKind = "Note_Sign", NoteTitle = "Reserved",
				Notes = new[] { "Reserved for guests of the Tangaroa. Please do not feed the sharks.", "Garden party postponed (sea level).", "Complimentary drinks at sunset. Bring your own cup." } },
			new Theme { Name = "radio", Label = "Radio outpost", Styles = new[] { Tropical, Snowy, Forest, Volcanic, Desert },
				Anchors = new[] { "RT_WindMill", "RT_SatteliteDisc" },
				Medium = new[] { "RT_SharkCage", "RT_PowerBox", "RT_Fence" },
				Small = new[] { "RT_Floodlight_WithoutLightSource", "RT_PlasticBoat", "Tire_02" },
				Loot = "Metal", Container = "Loot_Box", NoteKind = "Note_Papers", NoteTitle = "Radio log",
				Notes = new[] { "...repeat, is anyone receiving? Over.", "Signal strongest from the north-east. Follow it.", "Battery low. Leaving the spare circuit boards in the box." } },
			new Theme { Name = "raftcamp", Label = "Castaways' camp", Styles = new[] { Tropical, Forest, Desert },
				Anchors = new[] { "campfire_1" },
				Medium = new[] { "Bed_Simple", "Scarecrow", "Placeable_Lantern_FireBasket" },
				Small = new[] { "Bird_Nest", "buoy", "Firewood_1" },
				Loot = "Basics", Container = "Loot_Barrel", NoteKind = "Note_Paper", NoteTitle = "Castaway's note",
				Notes = new[] { "We pulled our raft up here to fix it. It didn't want to be fixed.", "The scarecrow keeps the gulls off. Mostly.", "Two weeks on this beach. Tomorrow we sail." } },
		};

		public static Theme ThemeOf(string name) { return Themes.FirstOrDefault(t => t.Name == name); }

		/// <summary>The themes that suit a style and whose props this Raft has (all of them when nothing was measured).</summary>
		public static List<Theme> ThemesFor(int style)
		{
			return Themes.Where(t => t.Styles.Contains(style) && t.Anchors.Any(RaftProps.Standable)).ToList();
		}

		static T One<T>(System.Random r, IList<T> list) { return list[r.Next(list.Count)]; }
		static float Yaw(System.Random r) { return (float)r.NextDouble() * 360f; }
		static Vector2 Around(Vector2 c, float yaw, float x, float z) { Vector3 v = Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0f, z); return c + new Vector2(v.x, v.z); }

		/// <summary>
		/// The lowest ground under a prop's footprint (its middle and near its corners, turned by yaw) with its pivot at p:
		/// standing it there, a big prop on uneven ground sinks in on the high side instead of floating on the low side.
		/// ground(x, z) gives the height, or NaN where there is none.
		/// </summary>
		internal static float LowestUnder(Func<float, float, float> ground, string name, Vector2 p, float yaw)
		{
			PropInfo info = RaftProps.Get(name);
			float low = ground(p.x, p.y);
			if (info == null) return low;
			Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
			// (the part it stands on where that is smaller: the excavator's tracks, not its boom)
			bool part = info.StandSize.x > 0f;
			Vector2 mid = part ? info.StandCentre : new Vector2(info.Centre.x, info.Centre.z), size = part ? info.StandSize : new Vector2(info.Size.x, info.Size.z);
			foreach (Vector2 c in new[] { Vector2.zero, new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
			{
				Vector3 w = rot * new Vector3(mid.x + c.x * size.x * 0.45f, 0f, mid.y + c.y * size.y * 0.45f);
				float g = ground(p.x + w.x, p.y + w.z);
				if (float.IsNaN(g)) return float.NaN;
				low = Mathf.Min(low, g);
			}
			return low;
		}

		/// <summary>How far a prop's body reaches from its middle on the ground, roughly (the inner part of its footprint).</summary>
		static float Footprint(string name)
		{
			PropInfo p = RaftProps.Get(name);
			// (plus how far its body sits off its pivot: a fence panel's pivot is at its end - IG7, a fence through a monument 2026-10-18)
			return p == null ? 0.6f : Mathf.Max(0.4f, Mathf.Min(p.Size.x, p.Size.z) * 0.45f + new Vector2(p.Centre.x, p.Centre.z).magnitude);
		}

		/// <summary>How far a set piece's body reaches from its pivot on the ground: its longest side, all of it (a monument, a statue).</summary>
		static float Reach(string name)
		{
			PropInfo p = RaftProps.Get(name);
			return p == null ? 0.6f : Mathf.Max(0.4f, Mathf.Max(p.Size.x, p.Size.z) * 0.5f + new Vector2(p.Centre.x, p.Centre.z).magnitude);
		}

		/// <summary>The setting a themed scene's pieces carry (the theme's name), the chest and the note too: the overlap check
		/// (CIOverlap*) measures them against each other.</summary>
		internal const string SceneProp = "set.scene";

		/// <summary>How far apart a scene's footprints stay (metres): their measured boxes, so a little room for a tilted flat prop.</summary>
		const float FootGap = 0.05f;

		/// <summary>Room kept for a scene's chest and note (half a side, metres), whatever way they turn.</summary>
		const float ChestRoom = 0.85f, NoteRoom = 0.8f;

		/// <summary>Containers raft_props doesn't measure, with their meshes' box (CIPropColliders 2026-10-22: Loot_Box is
		/// 2.6 x 1 x 2.2 m - taken as a 1.2 m cube, a radio scene's power box stood 0.3 m inside it).</summary>
		static readonly Dictionary<string, Vector3> ContainerSizes = new Dictionary<string, Vector3>(StringComparer.Ordinal) { { "Loot_Box", new Vector3(2.59f, 1f, 2.18f) } };

		/// <summary>Room kept for a chest of this kind, whatever way it turns: half its box's diagonal, at least ChestRoom.</summary>
		static float ChestRoomOf(string kind)
		{
			Vector3 size;
			return ContainerSizes.TryGetValue(kind ?? "", out size) ? Mathf.Max(ChestRoom, new Vector2(size.x, size.z).magnitude * 0.5f + FootGap) : ChestRoom;
		}

		/// <summary>A prop's footprint on the ground: a turned rectangle - its middle and its two half sides.</summary>
		internal struct Foot { public Vector2 Mid, X, Z; }

		/// <summary>
		/// The ground a measured prop covers with its pivot at p, turned by euler: its measured box turned (a tilted flat prop's
		/// box laid flat), as a rectangle along its turn. Balboa's dish also covers its pole's foot (it hangs on it).
		/// </summary>
		internal static Foot FootOf(string name, Vector2 p, Vector3 euler)
		{
			PropInfo info = RaftProps.Get(name);
			Vector3 size = info != null ? info.Size : new Vector3(1.2f, 1.2f, 1.2f), centre = info != null ? info.Centre : Vector3.zero, known;
			if (info == null && name != null && ContainerSizes.TryGetValue(name, out known)) { size = known; centre = new Vector3(0f, known.y * 0.5f, 0f); }
			Quaternion turn = Quaternion.Euler(0f, euler.y, 0f), full = Quaternion.Euler(euler);
			Vector3 ax = turn * Vector3.right, az = turn * Vector3.forward;
			float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
			for (int i = 0; i < 8; i++)
			{
				Vector3 v = full * (centre + Vector3.Scale(size * 0.5f, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
				float x = Vector3.Dot(v, ax), z = Vector3.Dot(v, az);
				x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); z0 = Mathf.Min(z0, z); z1 = Mathf.Max(z1, z);
			}
			if (name == "Antenna_dish") { x0 = Mathf.Min(x0, -0.4f); x1 = Mathf.Max(x1, 0.4f); z0 = Mathf.Min(z0, -0.4f); z1 = Mathf.Max(z1, 0.4f); }
			Vector3 mid = ax * ((x0 + x1) * 0.5f) + az * ((z0 + z1) * 0.5f);
			return new Foot { Mid = p + new Vector2(mid.x, mid.z), X = new Vector2(ax.x, ax.z) * ((x1 - x0) * 0.5f), Z = new Vector2(az.x, az.z) * ((z1 - z0) * 0.5f) };
		}

		/// <summary>A square of half side r round p: room for something whose turn isn't drawn yet (a chest, a note).</summary>
		static Foot Spot(Vector2 p, float r) { return new Foot { Mid = p, X = new Vector2(r, 0f), Z = new Vector2(0f, r) }; }

		/// <summary>How far two footprints pass into each other (separating axes): above 0 they overlap; 0 or less, they are
		/// at least that far apart.</summary>
		internal static float Into(Foot a, Foot b)
		{
			float depth = float.MaxValue;
			Vector2 d = b.Mid - a.Mid;
			foreach (Vector2 axis in new[] { a.X, a.Z, b.X, b.Z })
			{
				float len = axis.magnitude;
				if (len < 1e-4f) continue;
				Vector2 n = axis / len;
				depth = Mathf.Min(depth, Mathf.Abs(Vector2.Dot(a.X, n)) + Mathf.Abs(Vector2.Dot(a.Z, n)) + Mathf.Abs(Vector2.Dot(b.X, n)) + Mathf.Abs(Vector2.Dot(b.Z, n)) - Mathf.Abs(Vector2.Dot(d, n)));
			}
			return depth;
		}

		/// <summary>Whether a footprint keeps <see cref="FootGap"/> from all of these.</summary>
		static bool Clear(IEnumerable<Foot> feet, Foot f) { return feet.All(q => Into(q, f) <= -FootGap); }

		/// <summary>A chest's or note's spot moved out from the scene's middle c until its room is clear of these (a wide centrepiece).</summary>
		static Vector2 OutOf(Vector2 c, Vector2 p, float room, IList<Foot> feet)
		{
			Vector2 dir = (p - c).sqrMagnitude > 1e-6f ? (p - c).normalized : Vector2.up;
			for (int i = 0; i < 12 && !Clear(feet, Spot(p, room)); i++) p += dir * 0.25f;
			return p;
		}

		/// <summary>A set piece's settings: kept when the land is cleared; in a themed scene, the scene's name too.</summary>
		static Dictionary<string, string> Tags(string scene)
		{
			var d = new Dictionary<string, string> { { "set.piece", "1" } };
			if (scene != null) d[SceneProp] = scene;
			return d;
		}

		/// <summary>Balboa's dish never stands on the ground by itself: it goes up on its antenna pole, the pole on the ground where
		/// the dish was (dishes lay on the grass or hung over their spot, the ground check 2026-10-19). <paramref name="add"/> adds
		/// the pole (name, position, yaw).</summary>
		static void DishOnPole(IslandObject dish, float yaw, float sink, Func<string, Vector3, float, IslandObject> add)
		{
			if (dish == null || dish.Name != "Antenna_dish") return;
			float baseY = dish.Position.y - RaftProps.Lift("Antenna_dish") + sink;
			Vector3 at = dish.Position;
			add("Antenna_pole", new Vector3(at.x, baseY - 0.1f, at.z), yaw);
			// (its mount on the pole's top, as the hand-built islands stand it - pivot on the pole, 6.0-6.2 m up; the pole is 6.3 m.
			// Set 0.25 m out and 5.3 m up, its back hung 0.1 m off the pole, held by nothing: CIGroundTypes gen-shack-296323)
			dish.Position = new Vector3(at.x, baseY - 0.1f + 6f, at.z);
		}

		/// <summary>Stands a measured prop on generated ground (sunk a little), protected from clearing like a set piece.</summary>
		static IslandObject Prop(MapKit k, string name, Vector2 p, float yaw, float sink = 0.05f, string scene = null)
		{
			float ground = LowestUnder((x, z) => k.Ground(new Vector2(x, z)), name, p, yaw);
			PropInfo fi = RaftProps.Get(name);
			if (fi != null && (fi.Size.y < 0.3f || fi.Size.y < 0.25f * Mathf.Min(fi.Size.x, fi.Size.z)))
			{
				// (a sheet, firewood, a tyre, a pallet - anything flat: lies along the slope - set on the lowest ground and sunk,
				// it was under the ground at its middle and uphill side, the ground check 2026-10-12; a 4 m pallet, IG6)
				Quaternion q = Quaternion.Euler(0f, yaw, 0f);
				Vector3 ax = q * new Vector3(fi.Size.x * 0.45f, 0f, 0f), az = q * new Vector3(0f, 0f, fi.Size.z * 0.45f);
				Func<Vector3, float> g = v => k.Ground(new Vector2(p.x + v.x, p.y + v.z));
				float tz = Mathf.Atan2(g(ax) - g(-ax), 2f * ax.magnitude) * Mathf.Rad2Deg, tx = -Mathf.Atan2(g(az) - g(-az), 2f * az.magnitude) * Mathf.Rad2Deg;
				IslandObject flat = k.Add(name, new Vector3(p.x, k.Ground(p) + RaftProps.Lift(name) - Mathf.Min(sink, fi.Size.y * 0.3f) + 0.02f, p.y), yaw, Tags(scene), 0f);
				flat.EulerRotation = new Vector3(tx, yaw, tz);
				return flat;
			}
			// (never more than half its height under the ground at its middle, as a set piece)
			if (fi != null) ground = Mathf.Max(ground, k.Ground(p) - fi.Size.y * 0.5f);
			return k.Add(name, new Vector3(p.x, ground + RaftProps.Lift(name) - sink, p.y), yaw, Tags(scene), 0f);
		}

		#region Themed scenes

		/// <summary>
		/// A themed scene on generated land around c: the centrepiece, props around it facing in, small things between,
		/// the theme's container and note. Nature is cleared from the spot. Returns false if the theme has no props here.
		/// </summary>
		public static bool Dress(MapKit k, Theme t, Vector2 c, float radius, bool withNote = true)
		{
			System.Random r = k.Rnd;
			string[] anchors = t.Anchors.Where(RaftProps.Standable).ToArray(), medium = t.Medium.Where(RaftProps.Standable).ToArray(), small = t.Small.Where(RaftProps.Standable).ToArray();
			if (anchors.Length == 0) return false;
			float yaw = Yaw(r);
			string anchor = One(r, anchors);
			float anchorSize = Mathf.Max(2f, RaftProps.Get(anchor).Size.x * 0.5f, RaftProps.Get(anchor).Size.z * 0.5f);
			// (a big centrepiece - an igloo, a shed - gets its own room plus the scene's around it)
			radius = Mathf.Max(radius, anchorSize + 5f);
			k.Clear(c, radius + 1f);
			RemoveContentNear(k, c, radius + 1f);
			IslandObject centre = Prop(k, anchor, c, yaw, 0.05f, t.Name);
			// (each prop's footprint kept clear of the others': brick stacks and baskets stood inside each other)
			var placed = new List<KeyValuePair<Vector2, float>> { new KeyValuePair<Vector2, float>(c, Mathf.Max(anchorSize * 0.8f, Reach(anchor))) };
			Func<string, Vector2, bool> free = (name, p) => { float fr = Footprint(name); return placed.All(q => (q.Key - p).magnitude >= q.Value + fr); };
			// (and their turned boxes, the chest's and the note's room too: two metal crates 2.4 m apart, each turned its own way,
			// stood 0.35 m into each other - the round footprints don't turn; gen-large-37435, 2026-10-21. A prop whose box
			// doesn't fit gets another try instead, up to four more, so a scene keeps its props)
			var feet = new List<Foot> { FootOf(anchor, c, centre.EulerRotation) };
			Vector2 box = OutOf(c, Around(c, yaw + 180f, 0f, anchorSize + 1.5f), ChestRoomOf(t.Container), feet);
			feet.Add(Spot(box, ChestRoomOf(t.Container)));
			Vector2 notePos = OutOf(c, Around(c, yaw + 150f, 0f, anchorSize + 2.5f), NoteRoom, feet);
			if (withNote) feet.Add(Spot(notePos, NoteRoom));
			int nm = medium.Length == 0 ? 0 : 2 + r.Next(3);
			for (int i = 0; i < nm; i++)
			{
				string name = One(r, medium);
				for (int attempt = 0, bumps = 0; attempt < 4 + bumps; attempt++)
				{
					float a = yaw + 60f + i * (240f / Mathf.Max(1, nm)) + (float)r.NextDouble() * 30f;
					Vector2 p = Around(c, a, 0f, anchorSize + 2f + (float)r.NextDouble() * Mathf.Max(1f, radius - anchorSize - 3f));
					if (k.Slope(p) > 22f || !free(name, p)) continue;
					Vector2 toC = c - p;
					float my = Mathf.Atan2(toC.x, toC.y) * Mathf.Rad2Deg + (float)r.NextDouble() * 30f - 15f;
					if (!Clear(feet, FootOf(name, p, new Vector3(0f, my, 0f)))) { bumps = Mathf.Min(4, bumps + 1); continue; }
					IslandObject o = Prop(k, name, p, my, 0.05f, t.Name);
					feet.Add(FootOf(name, p, o.EulerRotation));
					DishOnPole(o, my, 0.05f, (pn, pp, py) => k.Add(pn, pp, py, Tags(t.Name), 0f));
					placed.Add(new KeyValuePair<Vector2, float>(p, Footprint(name)));
					break;
				}
			}
			int ns = small.Length == 0 ? 0 : 3 + r.Next(4);
			for (int i = 0; i < ns; i++)
			{
				string name = One(r, small);
				for (int attempt = 0, bumps = 0; attempt < 4 + bumps; attempt++)
				{
					Vector2 p = Around(c, Yaw(r), 0f, anchorSize + 1f + (float)r.NextDouble() * Mathf.Max(1f, radius - anchorSize - 1f));
					if (k.Slope(p) > 25f || !free(name, p)) continue;
					float sy = Yaw(r);
					if (!Clear(feet, FootOf(name, p, new Vector3(0f, sy, 0f)))) { bumps = Mathf.Min(4, bumps + 1); continue; }
					IslandObject o = Prop(k, name, p, sy, 0.05f, t.Name);
					feet.Add(FootOf(name, p, o.EulerRotation));
					placed.Add(new KeyValuePair<Vector2, float>(p, Footprint(name)));
					break;
				}
			}
			k.Chest(t.Container, box, t.Label, MapKit.Loot(t.Loot)).Props[SceneProp] = t.Name;
			if (withNote) k.Note(t.NoteKind, notePos, t.NoteTitle, One(r, t.Notes)).Props[SceneProp] = t.Name;
			if (t.Creature != null && r.NextDouble() < 0.5)
			{
				Vector2? den = k.Find(c, radius * 2.5f, MapKit.Dry, 6f);
				if (den.HasValue) k.Creature(t.Creature, den.Value, 1);
			}
			return true;
		}

		/// <summary>A few of a theme's props scattered between inner and outer metres around c (no centrepiece, chest or note): a touch of it.
		/// <paramref name="force"/> (tests only, CIShackDish): the first prop placed is this one instead of a random pick; null = as always.</summary>
		public static int Sprinkle(MapKit k, Theme t, Vector2 c, float inner, float outer, int count, string force = null)
		{
			System.Random r = k.Rnd;
			string[] props = t.Medium.Concat(t.Small).Where(RaftProps.Standable).ToArray();
			int placed = 0;
			// (clear of the set pieces already there and of each other, by their footprints)
			var near = k.File.Objects.Where(o => o.Props != null && o.Props.ContainsKey("set.piece") && new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < outer + 25f).ToList();
			var taken = near.Select(o => new KeyValuePair<Vector2, float>(new Vector2(o.Position.x, o.Position.z), Reach(o.Name))).ToList();
			// (and by their turned boxes, as Dress's)
			var feet = near.Where(o => !o.Props.ContainsKey("cave")).Select(o => FootOf(o.Name, new Vector2(o.Position.x, o.Position.z), o.EulerRotation)).ToList();
			for (int i = 0, bumps = 0; i < count * 4 + bumps && placed < count && props.Length > 0; i++)
			{
				Vector2 p = Around(c, Yaw(r), 0f, inner + (float)r.NextDouble() * (outer - inner));
				if (k.Ground(p) - k.Sea < 0.8f || k.Slope(p) > 22f) continue;
				string name = placed == 0 && force != null ? force : One(r, props);
				if (taken.Any(q => (q.Key - p).magnitude < q.Value + Footprint(name))) continue;
				float sy = Yaw(r);
				if (!Clear(feet, FootOf(name, p, new Vector3(0f, sy, 0f)))) { bumps = Mathf.Min(count * 2, bumps + 1); continue; }
				k.Clear(p, 1.5f);
				IslandObject o = Prop(k, name, p, sy, 0.05f, t.Name);
				feet.Add(FootOf(name, p, o.EulerRotation));
				DishOnPole(o, sy, 0.05f, (pn, pp, py) => k.Add(pn, pp, py, Tags(t.Name), 0f));
				taken.Add(new KeyValuePair<Vector2, float>(p, Footprint(name)));
				placed++;
			}
			return placed;
		}

		/// <summary>The theme that goes with each oddity's set piece.</summary>
		public static readonly Dictionary<string, string> OddityTheme = new Dictionary<string, string>
		{
			{ "van", "scrapyard" }, { "caravan", "caravan" }, { "planecrash", "radio" }, { "boatwreck", "raftcamp" }, { "shack", "bear" }, { "statue", "garden" }, { "rocket", "scrapyard" }, { "hut", "raftcamp" },
		};

		/// <summary>The same on one of Raft's islands (the randomizer's extras): props stood on Raft's ground found by rays.</summary>
		public static bool DressWorld(WorldKit k, LandGround g, Theme t, Vector3 c, float radius, System.Random r)
		{
			string[] anchors = t.Anchors.Where(RaftProps.Standable).ToArray(), medium = t.Medium.Where(RaftProps.Standable).ToArray(), small = t.Small.Where(RaftProps.Standable).ToArray();
			if (anchors.Length == 0) return false;
			Func<float, float, float> ground = (x, z) => { Vector3 gh, gn; return g.Hit(x, z, out gh, out gn) ? gh.y : float.NaN; };
			// (the scene's turned boxes kept apart, as Dress's: bumped = the last one didn't fit them, worth another try)
			var feet = new List<Foot>();
			bool bumped = false;
			Func<Vector3, float, float, string, IslandObject> put = (at, yaw, sink, name) =>
			{
				Vector3 hit, n;
				if (!g.Hit(at.x, at.z, out hit, out n) || hit.y < 0.8f || Vector3.Angle(n, Vector3.up) > 25f) return null;
				// (clear of Raft's trees, rocks and pickups, and of what was put here already: by the prop's own footprint)
				PropInfo pi = RaftProps.Get(name);
				float clear = pi != null ? Mathf.Max(1.5f, Mathf.Max(pi.Size.x, pi.Size.z) * 0.4f + 0.5f) : 1.5f;
				if (k.Taken.Any(x => new Vector2(x.x - at.x, x.z - at.z).sqrMagnitude < clear * clear)) return null;
				Foot f = FootOf(name, new Vector2(at.x, at.z), new Vector3(0f, yaw, 0f));
				if (!Clear(feet, f)) { bumped = true; return null; }
				float low = LowestUnder(ground, name, new Vector2(at.x, at.z), yaw);
				if (float.IsNaN(low) || low < 0.6f) return null;
				feet.Add(f);
				return k.Add(name, new Vector3(at.x, low + RaftProps.Lift(name) - sink, at.z), yaw, Tags(t.Name));
			};
			float y0 = Yaw(r);
			string anchor = One(r, anchors);
			if (put(c, y0, 0.05f, anchor) == null) return false;
			float anchorSize = Mathf.Max(2f, RaftProps.Get(anchor).Size.x * 0.5f, RaftProps.Get(anchor).Size.z * 0.5f);
			// (the chest's and the note's room kept first)
			Vector2 c2 = new Vector2(c.x, c.z);
			Vector3 box = c + Quaternion.Euler(0f, y0 + 180f, 0f) * Vector3.forward * (anchorSize + 1.5f), bh, bn;
			Vector2 b2 = OutOf(c2, new Vector2(box.x, box.z), ChestRoomOf(t.Container), feet);
			feet.Add(Spot(b2, ChestRoomOf(t.Container)));
			Vector3 note = c + Quaternion.Euler(0f, y0 + 150f, 0f) * Vector3.forward * (anchorSize + 2.5f), nh, nn;
			Vector2 n2 = OutOf(c2, new Vector2(note.x, note.z), NoteRoom, feet);
			feet.Add(Spot(n2, NoteRoom));
			int nm = medium.Length == 0 ? 0 : 2 + r.Next(3);
			for (int i = 0; i < nm; i++)
			{
				float a0 = y0 + 60f + i * (240f / Mathf.Max(1, nm));
				for (int attempt = 0; attempt < 4; attempt++)
				{
					float a = attempt == 0 ? a0 : a0 + (float)r.NextDouble() * 30f - 15f;
					Vector3 p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (anchorSize + 2f + (float)r.NextDouble() * Mathf.Max(1f, radius - anchorSize - 3f));
					bumped = false;
					IslandObject o = put(p, a + 180f, 0.05f, One(r, medium));
					DishOnPole(o, a + 180f, 0.05f, (pn, pp, py) => k.Add(pn, pp, py, Tags(t.Name)));
					if (o != null || !bumped) break;
				}
			}
			int ns = small.Length == 0 ? 0 : 3 + r.Next(3);
			for (int i = 0; i < ns; i++)
				for (int attempt = 0; attempt < 4; attempt++)
				{
					Vector3 p = c + Quaternion.Euler(0f, Yaw(r), 0f) * Vector3.forward * (anchorSize + 1f + (float)r.NextDouble() * Mathf.Max(1f, radius - anchorSize - 1f));
					bumped = false;
					if (put(p, Yaw(r), 0.05f, One(r, small)) != null || !bumped) break;
				}
			if (g.Hit(b2.x, b2.y, out bh, out bn)) k.Chest(t.Container, bh, Yaw(r), t.Label, MapKit.Loot(t.Loot)).Props[SceneProp] = t.Name;
			if (g.Hit(n2.x, n2.y, out nh, out nn)) k.Note(t.NoteKind, nh, Yaw(r), t.NoteTitle, One(r, t.Notes)).Props[SceneProp] = t.Name;
			return true;
		}

		#endregion

		#region Caves

		/// <summary>
		/// A cave: one of Balboa's dens (a rock outcrop with a den inside and one mouth), placed so its floor meets the
		/// ground at the mouth and the mouth faces open ground. Positions are in the kit's space (terrain-local for a
		/// generated island, world for Raft's islands).
		/// </summary>
		public class Cave
		{
			public string Name;
			public PropInfo Info;
			public Vector3 Pivot;
			public float Yaw;
			/// <summary>The middle of the mouth and the back wall, on the floor; Out = the way out (flat, normalised).</summary>
			public Vector3 Mouth, Back;
			public Vector2 Out;
			public float Floor, Width, Headroom;
			public float Depth { get { return Vector2.Distance(new Vector2(Mouth.x, Mouth.z), new Vector2(Back.x, Back.z)); } }
			/// <summary>A point on the floor t metres in from the mouth, s metres to the side.</summary>
			public Vector3 At(float t, float s = 0f) { return new Vector3(Mouth.x - Out.x * t - Out.y * s, Floor, Mouth.z - Out.y * t + Out.x * s); }
			/// <summary>How far from the middle the den's outcrop reaches (for finding room for it).</summary>
			public float Reach { get { return Mathf.Max(Info.Size.x, Info.Size.z) * 0.5f + 1f; } }
		}

		/// <summary>Raft's dens: whole caves with one mouth (Balboa's bear cave and dead end), as measured in raft_props.txt.</summary>
		public static readonly string[] Dens = { "BalboaCave_Bear", "BalboaCave_DeadEnd" };

		static PropInfo Den(string name)
		{
			PropInfo p = RaftProps.Get(name);
			return p != null && p.IsCave && p.OpenPlus != p.OpenMinus && p.Width > 3f && p.Headroom > 2.5f ? p : null;
		}

		/// <summary>Whether this Raft has measured dens to make caves with.</summary>
		public static bool CanBuildCaves { get { return Dens.Any(n => Den(n) != null); } }

		/// <summary>
		/// A den placed with its inside point (the measured spot in its passage) at inside, its mouth facing outward, its
		/// floor at floorY.
		/// </summary>
		public static Cave PlaceDen(string name, Vector2 inside, Vector2 outward, float floorY)
		{
			PropInfo p = Den(name);
			if (p == null) return null;
			outward = outward.normalized;
			Vector3 axis = p.Axis == 0 ? Vector3.right : Vector3.forward;
			Vector3 localOut = axis * (p.OpenPlus ? 1f : -1f);
			float yaw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg - Mathf.Atan2(localOut.x, localOut.z) * Mathf.Rad2Deg;
			Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
			Vector3 o = rot * p.Inside;
			var cave = new Cave { Name = name, Info = p, Yaw = yaw, Out = outward, Floor = floorY, Width = p.Width, Headroom = p.Headroom };
			cave.Pivot = new Vector3(inside.x - o.x, floorY - p.Floor, inside.y - o.z);
			// The mouth: where the outcrop ends on the open side; the back: the closed side's wall
			float insideAlong = p.Axis == 0 ? p.Inside.x : p.Inside.z, centreAlong = p.Axis == 0 ? p.Centre.x : p.Centre.z, half = (p.Axis == 0 ? p.Size.x : p.Size.z) / 2f;
			float toMouth = Mathf.Max(1f, p.OpenPlus ? centreAlong + half - insideAlong : insideAlong - (centreAlong - half));
			float toBack = Mathf.Max(2f, (p.OpenPlus ? p.ToMinus : p.ToPlus) - 0.4f);
			cave.Mouth = new Vector3(inside.x + outward.x * toMouth, floorY, inside.y + outward.y * toMouth);
			cave.Back = new Vector3(inside.x - outward.x * toBack, floorY, inside.y - outward.y * toBack);
			return cave;
		}

		/// <summary>What is inside a cave: a hoard at the back, a lantern by it, dark air, and a guard that wakes when players come in.</summary>
		static void FurnishCave(object kit, Cave cave, string guard, string lootPreset, string title)
		{
			MapKit mk = kit as MapKit;
			WorldKit wk = kit as WorldKit;
			float depth = cave.Depth, inward = Mathf.Atan2(-cave.Out.x, -cave.Out.y) * Mathf.Rad2Deg;
			Vector3 hoard = cave.At(depth - 2f), lamp = cave.At(depth - 2.5f, cave.Width * 0.3f), guardAt = cave.At(depth * 0.7f), zoneAt = cave.At(Mathf.Min(6f, depth * 0.3f));
			var chest = new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot(lootPreset) + ";" + MapKit.Loot("Metal").Split(';').First() }, { ObjectProps.NoteTitle, title } };
			var zone = new Dictionary<string, string> { { ObjectProps.ZoneId, "cave" }, { ObjectProps.ZoneRadius, Mathf.Clamp(cave.Width * 0.45f, 2.5f, 6f).ToString("0.#", CultureInfo.InvariantCulture) }, { ObjectProps.ZoneMessage, "Something moves in the dark..." } };
			var atmo = new Dictionary<string, string>
			{
				{ ObjectProps.ZoneRadius, Mathf.Max(6f, depth * 0.5f).ToString("0.#", CultureInfo.InvariantCulture) }, { ObjectProps.AtmoFog, "#0E0C0A" }, { ObjectProps.AtmoFogAmount, "0.5" },
				{ ObjectProps.AtmoLight, "#FFB060" }, { ObjectProps.AtmoLightAmount, "0.2" }, { ObjectProps.AtmoParticles, "none" },
			};
			Dictionary<string, string> creature = null;
			if (guard != null)
			{
				creature = new Dictionary<string, string> { { ObjectProps.CreatureCount, guard == "Rat" ? "3" : "1" }, { ObjectProps.CreatureZone, "cave" }, { ObjectProps.CreatureRespawn, "0" } };
				if (guard != "Rat") { creature[ObjectProps.CreatureHealth] = "2"; creature[ObjectProps.CreatureDamage] = "1.5"; creature[ObjectProps.CreatureSize] = "1.1"; }
			}
			Action<string, Vector3, float, Dictionary<string, string>> add = (n, at, yaw, props) =>
			{
				if (mk != null) mk.Add(n, at, yaw, props, 0f); else wk.Add(n, at, yaw, props);
			};
			add(cave.Name, cave.Pivot, cave.Yaw, new Dictionary<string, string> { { "set.piece", "1" }, { "cave", "1" } });
			add("Loot_Chest", hoard, inward + 180f, chest);
			add("Placeable_Lantern_Basic", lamp, 0f, null);
			add(ContentCatalog.TriggerZone, zoneAt, 0f, zone);
			add(ContentCatalog.AtmosphereZoneName, cave.At(depth * 0.5f), 0f, atmo);
			if (creature != null) add("Creature_" + guard, guardAt, inward + 180f, creature);
		}

		static string DenFor(System.Random r) { string[] dens = Dens.Where(n => Den(n) != null).ToArray(); return dens.Length > 0 ? dens[r.Next(dens.Length)] : null; }

		/// <summary>
		/// A den on a generated island: on fairly level land near the coast, its mouth towards the sea. The ground under
		/// it is levelled to the den's floor and blended into the land around, nature is cleared from it and in front of
		/// the mouth. Returns false if no spot fits.
		/// </summary>
		public static bool EmbeddedCave(MapKit k, IslandGenSettings s, string guard, string lootPreset, string title)
		{
			System.Random r = k.Rnd;
			string name = DenFor(r);
			if (name == null) return false;
			PropInfo p = Den(name);
			float reach = Mathf.Max(p.Size.x, p.Size.z) * 0.5f;
			for (int attempt = 0; attempt < 300; attempt++)
			{
				float a = (float)r.NextDouble() * Mathf.PI * 2f, d = (0.25f + 0.55f * (float)r.NextDouble()) * s.Radius;
				Vector2 c = k.Mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
				float floor = k.Ground(c);
				if (floor - k.Sea < 2f || floor - k.Sea > 25f) continue;
				// (clear of the scenes already on the island)
				if (k.File.Objects.Any(o => o.Props != null && o.Props.ContainsKey("set.piece") && new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < reach + 8f)) continue;
				// All of the outcrop over land, not much lower than the floor (it reaches 6-11 m under it)
				bool ok = true;
				for (int i = 0; i < 16 && ok; i++)
				{
					float b = i * Mathf.PI / 8f;
					Vector2 q = c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * reach;
					float g = k.Ground(q);
					if (g - k.Sea < 0.5f || g < floor - 4f) ok = false;
				}
				if (!ok) continue;
				// Open ground in front of the mouth: facing the sea best, else turned up to 80° either way
				Vector2 away = (c - k.Mid).normalized, outward = Vector2.zero;
				foreach (float turn in new[] { 0f, 40f, -40f, 80f, -80f })
				{
					Vector2 dir = Quaternion.Euler(0f, 0f, turn) * away;
					bool open = true;
					for (float t = reach; t <= reach + 10f && open; t += 2f) { float g = k.Ground(c + dir * t); if (g - k.Sea < 0.4f || Mathf.Abs(g - floor) > 3f) open = false; }
					if (open) { outward = dir; break; }
				}
				if (outward == Vector2.zero) continue;
				Cave cave = PlaceDen(name, c, outward, floor);
				RemoveContentNear(k, c, reach + 4f);
				Level(k, c, reach, floor, outward, cave);
				k.Clear(c, reach + 3f);
				for (float t = reach; t <= reach + 10f; t += 2f) k.Clear(c + outward * t, 4f);
				FurnishCave(k, cave, guard, lootPreset, title);
				return true;
			}
			return false;
		}

		/// <summary>
		/// The ground round a den: the passage (mouth to back and a metre and a half past it) level at its floor, a mound
		/// round the rest of the outcrop that buries its rim (Raft's dens are shells made to sit in a hillside: on level
		/// ground their rim floated over it and you saw into them - AU83), blended into the land beyond; in front of the
		/// mouth no mound, and a level path out of it.
		/// </summary>
		static void Level(MapKit k, Vector2 c, float reach, float floor, Vector2 outward, Cave cave)
		{
			// (the den's passage, mouth to back and a metre past it: its floor all the way - the passage reaches past the
			// outcrop's middle, and the hill's blend rose into the back of the den, a lantern 3 m under the ground there)
			Vector2 back = new Vector2(cave.Back.x, cave.Back.z) - outward * 1.5f, mouth = new Vector2(cave.Mouth.x, cave.Mouth.z);
			float half = cave.Width * 0.5f + 1.5f, mound = MoundHeight(cave);
			IslandFile f = k.File;
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1), blend = 10f, reachAll = reach + blend + 18f;
			// The outcrop's footprint: an ellipse round its middle, in its own turn
			Quaternion toDen = Quaternion.Inverse(Quaternion.Euler(0f, cave.Yaw, 0f));
			Vector3 mid3 = cave.Pivot + Quaternion.Euler(0f, cave.Yaw, 0f) * cave.Info.Centre;
			Vector2 mid = new Vector2(mid3.x, mid3.z);
			float ax = cave.Info.Size.x * 0.5f, az = cave.Info.Size.z * 0.5f;
			// What stands around keeps its height above the ground as the ground changes (trees in the blend don't float)
			var around = f.Objects.Where(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < reachAll).Select(o => new KeyValuePair<IslandObject, float>(o, k.Ground(new Vector2(o.Position.x, o.Position.z)))).ToList();
			float[,] was = (float[,])f.Heights.Clone();
			Func<Vector2, float> aroundGround = q0 => IslandGenerator.SampleHeights(was, res, step, q0.x, q0.y) * f.TerrainSize.y;
			int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - reachAll) / step)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.x + reachAll) / step));
			int z0 = Mathf.Max(0, Mathf.FloorToInt((c.y - reachAll) / step)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.y + reachAll) / step));
			Vector2 side = new Vector2(-outward.y, outward.x), seg = mouth - back;
			for (int z = z0; z <= z1; z++)
				for (int x = x0; x <= x1; x++)
				{
					Vector2 q = new Vector2(x * step, z * step);
					float h = f.Heights[z, x] * f.TerrainSize.y;
					// How far out of the passage, and past the mouth
					float u = Mathf.Clamp01(Vector2.Dot(q - back, seg) / Mathf.Max(0.01f, seg.sqrMagnitude));
					float fromPassage = Mathf.Max(0f, (q - (back + seg * u)).magnitude - half), pastMouth = Vector2.Dot(q - mouth, outward);
					// How far out of the footprint (0 inside it)
					Vector3 l = toDen * new Vector3(q.x - mid.x, 0f, q.y - mid.y);
					float e = Mathf.Sqrt(l.x * l.x / (ax * ax) + l.z * l.z / (az * az)), r = new Vector2(l.x, l.z).magnitude;
					float outside = e <= 1f ? 0f : r * (1f - 1f / e);
					// The mound: its full height a few metres out of the passage, none in front of the mouth
					float rise = Smooth((fromPassage - 0.5f) / 3f) * (1f - Smooth(pastMouth / 5f));
					float target = floor - 0.15f + (mound + 0.15f) * rise, w = 1f - Smooth(outside / blend);
					float g = w <= 0f ? h : Mathf.Lerp(h, target, w);
					// (beyond the footprint it only raises the land: higher land behind it stays)
					if (outside > 0f) g = Mathf.Max(h, g);
					// A path out of the mouth: level 5 m wide for 12 m past the outcrop, blended at its sides and end
					float along = Vector2.Dot(q - c, outward), across = Mathf.Abs(Vector2.Dot(q - c, side));
					if (along > 0f)
					{
						float wSide = across < 2.5f ? 1f : across < 6f ? 1f - (across - 2.5f) / 3.5f : 0f;
						float wEnd = along < reach + 12f ? 1f : along < reach + 18f ? 1f - (along - reach - 12f) / 6f : 0f;
						g = Mathf.Lerp(g, floor - 0.15f, Smooth(wSide * wEnd));
					}
					// The passage itself: its floor
					if (fromPassage <= 0f) g = floor - 0.15f;
					if (Mathf.Abs(g - h) > 0.001f) f.Heights[z, x] = Mathf.Clamp01(g / f.TerrainSize.y);
				}
			// (nature moves with the ground at its lowest change under it: by its pivot's alone, grass on the mound's steep
			// blend stood 1.1-1.4 m over the ground lowered more beside it, CIGroundIslands gen-large-315592)
			// (and where the ground changed unevenly under it - the mound's edge rose 1.9 m under its middle and hardly at all a
			// metre and a half off - it can't stand on both: taken away, else it stood under the raised ground at its middle and
			// over the drop beside it, CIGroundIslands gen-large-416923)
			foreach (var kv in around)
			{
				Vector2 p = new Vector2(kv.Key.Position.x, kv.Key.Position.z);
				float d = k.Ground(p) - kv.Value;
				if (kv.Key.Props == null || kv.Key.Props.Count == 0)
				{
					float most = d;
					foreach (Vector2 o in new[] { new Vector2(1.5f, 0f), new Vector2(-1.5f, 0f), new Vector2(0f, 1.5f), new Vector2(0f, -1.5f) })
					{
						float ch = k.Ground(p + o) - aroundGround(p + o);
						d = Mathf.Min(d, ch);
						most = Mathf.Max(most, ch);
					}
					if (most - d > 1f) { f.Objects.Remove(kv.Key); continue; }
				}
				kv.Key.Position.y += d;
			}
		}

		static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

		/// <summary>How high the mound round a den rises over its floor: enough to bury its rim, under its roof.</summary>
		public static float MoundHeight(Cave cave) { return Mathf.Min(5f, cave.Headroom * 0.6f); }

		/// <summary>
		/// Whether the ground under a den's footprint (behind its mouth) falls more than a metre or so under its floor, where
		/// its rim would float over it. ground(x, z) gives the ground's height there.
		/// </summary>
		public static bool RimFloats(Cave cave, Func<float, float, float> ground, float drop = 1.5f)
		{
			Quaternion turn = Quaternion.Euler(0f, cave.Yaw, 0f);
			Vector3 mid = cave.Pivot + turn * cave.Info.Centre;
			foreach (float e in new[] { 0.7f, 1f })
				for (int i = 0; i < 16; i++)
				{
					float b = i * Mathf.PI / 8f;
					Vector3 q = mid + turn * new Vector3(Mathf.Cos(b) * cave.Info.Size.x * 0.5f * e, 0f, Mathf.Sin(b) * cave.Info.Size.z * 0.5f * e);
					if (Vector2.Dot(new Vector2(q.x - cave.Mouth.x, q.z - cave.Mouth.z), cave.Out) > -1f) continue;
					if (ground(q.x, q.z) < cave.Floor - (e < 1f ? drop * 2f / 3f : drop)) return true;
				}
			return false;
		}

		/// <summary>Takes the generator's animal spots and loot boxes off a spot that content is going on (a den, a scene).</summary>
		static void RemoveContentNear(MapKit k, Vector2 c, float radius)
		{
			k.File.Objects.RemoveAll(o => (o.Name.StartsWith("Creature_") || ContentCatalog.IsLootObject(o.Name)) && new Vector2(o.Position.x - c.x, o.Position.z - c.y).sqrMagnitude < radius * radius);
		}

		/// <summary>
		/// A den on one of Raft's islands, whose ground can't be changed: a spot where the den's inside is clear of Raft's
		/// trees and rocks and its floor meets the ground, with walkable ground out of the mouth. Returns a short
		/// description, or null if nothing fits.
		/// </summary>
		public static string Grotto(WorldKit k, LandGround g, System.Random r, string guard, string lootPreset, IList<Vector3> blockers)
		{
			string name = DenFor(r);
			if (name == null) return null;
			PropInfo p = Den(name);
			float reach = Mathf.Max(p.Size.x, p.Size.z) * 0.5f;
			int noSpot = 0, bumpy = 0, raftThings = 0, overSea = 0, rim = 0, mouth = 0;
			for (int attempt = 0; attempt < 160; attempt++)
			{
				Vector3? spot = g.Find(r, (h, slope) => h > 2f && h < 30f && slope < 14f, null, 0f, 40);
				if (!spot.HasValue) { noSpot++; continue; }
				Vector2 c = new Vector2(spot.Value.x, spot.Value.z), away = (c - new Vector2(g.Centre.x, g.Centre.z)).normalized;
				if (away.sqrMagnitude < 0.5f) continue;
				// Out towards the coast (away from the island's middle) best, else turned up to 90° either way
				foreach (float turn in new[] { (float)r.NextDouble() * 30f - 15f, 45f, -45f, 90f, -90f })
				{
					Vector2 outward = (Vector2)(Quaternion.Euler(0f, 0f, turn) * away);
					Cave cave = PlaceDen(name, c, outward, spot.Value.y);
					// Inside the den: Raft's ground can't be changed, so the floor goes on its highest point there (ground a
					// little under the floor is hidden by it); too bumpy if it drops more than 3 m (the den has no floor of its own: where Raft's
					// ground fell away under it you saw out under its walls - AU83; CaveMountain's skirt now closes that down to the ground,
					// but a deeper drop makes a wall of rock - at 1.5 m no den fitted on any of Raft's big islands), nothing of Raft's in it
					float top = float.MinValue, low = float.MaxValue;
					bool ok = true;
					for (float t = 1f; t <= cave.Depth && ok; t += 2f)
						foreach (float s in new[] { -cave.Width * 0.35f, 0f, cave.Width * 0.35f })
						{
							Vector3 q = cave.At(t, s), hit, n;
							if (!g.Hit(q.x, q.z, out hit, out n)) { ok = false; break; }
							top = Mathf.Max(top, hit.y); low = Mathf.Min(low, hit.y);
							if (blockers.Any(x => (new Vector2(x.x - q.x, x.z - q.z)).sqrMagnitude < 6.25f)) { ok = false; raftThings++; break; }
						}
					if (!ok) continue;
					// (nor under its outcrop round the passage: its rock buried Raft's crates and palms by the passage - CA23. The
					// ellipse its measured footprint holds, a metre around)
					Quaternion toDen = Quaternion.Inverse(Quaternion.Euler(0f, cave.Yaw, 0f));
					float ax = cave.Info.Size.x * 0.5f + 1f, az = cave.Info.Size.z * 0.5f + 1f;
					if (blockers.Any(x =>
					{
						Vector3 d = toDen * new Vector3(x.x - cave.Pivot.x, 0f, x.z - cave.Pivot.z) - new Vector3(cave.Info.Centre.x, 0f, cave.Info.Centre.z);
						return d.x * d.x / (ax * ax) + d.z * d.z / (az * az) < 1f;
					})) { raftThings++; continue; }
					if (top - low > 3f) { bumpy++; continue; }
					if (top - 0.3f > cave.Floor + 0.01f || top - 0.3f < cave.Floor - 0.01f) cave = PlaceDen(name, c, outward, top - 0.3f);
					// Around it: land under the whole outcrop (not hanging over the sea)
					for (int i = 0; i < 12 && ok; i++)
					{
						float b = i * Mathf.PI / 6f;
						Vector3 q = new Vector3(c.x + Mathf.Cos(b) * reach, 0f, c.y + Mathf.Sin(b) * reach), hit, n;
						if (!g.Hit(q.x, q.z, out hit, out n) || hit.y < 0.5f) ok = false;
					}
					if (!ok) { overSea++; continue; }
					// and its rim on the ground all round (not floating over ground that falls away - AU83): Raft's ground can't be
					// raised round it as a generated island's is, so it goes only where the ground is as high as its floor or higher
					// under its footprint, but in front of its mouth
					if (RimFloats(cave, (x, z) => { Vector3 hit, n; return g.Hit(x, z, out hit, out n) ? hit.y : float.MinValue; }, 3f)) { rim++; continue; }
					if (!ok) { overSea++; continue; }
					// Out of the mouth: walkable ground that meets the floor
					for (float t = 1f; t <= 8f && ok; t += 2f)
					{
						Vector3 q = cave.At(-t), hit, n;
						if (!g.Hit(q.x, q.z, out hit, out n) || hit.y > cave.Floor + 1.2f || hit.y < cave.Floor - 1.5f - t * 0.25f || Vector3.Angle(n, Vector3.up) > 32f) ok = false;
					}
					if (!ok) { mouth++; continue; }
					FurnishCave(k, cave, guard, lootPreset, "Den hoard");
					return "a den (" + name + ", " + cave.Depth.ToString("F0", CultureInfo.InvariantCulture) + " m deep" + (guard != null ? ", a " + guard.ToLowerInvariant() : "") + ")";
				}
			}
			Debug.Log("[CUSTOM ISLANDS] [randomizer] no den fits: " + noSpot + " no level spot, " + bumpy + " too bumpy, " + raftThings + " Raft's things in the way, " + overSea + " over the sea, " + rim + " ground falling away under its rim, " + mouth + " no way out of the mouth");
			return null;
		}

		#endregion

		#region Large islands

		static readonly string[] NameFirst = { "Gull", "Palm", "Driftwood", "Coral", "Turtle", "Crab", "Storm", "Lantern", "Old Man's", "Whisper", "Sunset", "Thunder", "Mango", "Anchor", "Fisher's", "Kelp" };
		static readonly string[] NameLast = { "Isle", "Rock", "Key", "Haven", "Reach", "Point", "Island", "Hollow", "Bluff", "Cay" };

		/// <summary>A name for a large island, from its seed.</summary>
		public static string IslandName(System.Random r) { return One(r, NameFirst) + " " + One(r, NameLast); }

		/// <summary>Settings of a large island: as big and tall as Raft's big islands, objects as dense as theirs.</summary>
		public static IslandGenSettings LargeSettings(System.Random rnd)
		{
			double roll = rnd.NextDouble();
			int style = roll < 0.55 ? Tropical : roll < 0.7 ? Forest : roll < 0.8 ? Desert : roll < 0.9 ? Snowy : Volcanic;
			int[] shapes = { IslandShapes.Round, IslandShapes.Round, IslandShapes.TwinPeaks, IslandShapes.TwinPeaks };
			var s = new IslandGenSettings
			{
				Seed = rnd.Next(1, 999999), Style = style, Shape = shapes[rnd.Next(shapes.Length)],
				Radius = 78f + (float)rnd.NextDouble() * 30f, Height = 36f + (float)rnd.NextDouble() * 22f,
				Roughness = 0.45f + (float)rnd.NextDouble() * 0.2f, Peaks = 2 + rnd.Next(3), ObjectDensity = 0.6f,
			};
			LikeRaft(s);
			// Like Raft's big islands: warthogs, animals to catch for the raft, puffer fish around (content), loot boxes
			s.Hostiles = 3;
			s.HostileKinds = style == Tropical ? "Boar" : "";
			s.Friendly = 4;
			s.Loot = 4; s.LootMin = 2; s.LootMax = 4; s.LootHidden = true;
			return s;
		}

		/// <summary>Every land object slider "like Raft" (the generator's measured densities at Raft's own amount).</summary>
		public static void LikeRaft(IslandGenSettings s)
		{
			s.Trees = s.Bushes = 0.33f;
			s.Rocks = s.Harvest = 0.33f;
			s.BeachThings = 0.3f;
		}

		static readonly System.Text.RegularExpressions.Regex CutTree = new System.Text.RegularExpressions.Regex(@"^Pickup_Landmark_(Tree_(Palm \d+|Pine|Birch|Mango|Banana)|MangoTree|Palmtree \d+)$");

		/// <summary>
		/// Trees to cut as on Raft's own islands: its small tropical islands have 2-6 palms to cut, its big ones about one
		/// per 1000 m² (palms, mango trees; pines and birches on Balboa). The generator leaves few on low islands (Raft's
		/// palms to cut grow 4 m and more above the sea there) and mostly bamboo: some of the island's own bamboo and big
		/// palms (forest: pines and birches) become trees to cut, where they stand - nothing new is put anywhere.
		/// </summary>
		public static void TreesToCut(MapKit k, IslandGenSettings s, bool small)
		{
			// (Raft's snowy islands have none to cut; volcanic is the mod's own style)
			if (s.Style != Tropical && s.Style != Forest && s.Style != Desert) return;
			IslandFile f = k.File;
			System.Random r = new System.Random(s.Seed * 31 + 7);
			// (the desert: Raft's small Caravan islands have 2-5 mango trees, its big one ten acacias - not in the catalog - on its land)
			string[] kinds = (s.Style == Tropical ? new[] { "Pickup_Landmark_Tree_Palm 1", "Pickup_Landmark_Tree_Palm 2", "Pickup_Landmark_Tree_Palm 3", "Pickup_Landmark_Tree_Palm 4" }
				: s.Style == Forest ? new[] { "Pickup_Landmark_Tree_Pine", "Pickup_Landmark_Tree_Birch" } : new[] { "Pickup_Landmark_Tree_Mango" }).Where(PlaceableCatalog.IsLoaded).ToArray();
			if (kinds.Length == 0) return;
			var land = f.Objects.Where(o => o.Position.y > f.WaterLevel + 0.2f && (o.Props == null || o.Props.Count == 0)).ToList();
			int have = land.Count(o => CutTree.IsMatch(o.Name));
			int want = small ? 2 + r.Next(s.Style == Desert ? 4 : 5) : Mathf.RoundToInt(LandArea(f) * (s.Style == Desert ? 0.5f : 0.7f + 0.4f * (float)r.NextDouble()) / 1000f);
			if (have >= want) return;
			// (look-alikes first: big palms / Balboa's pines and birches / the desert's bushy trees; then bamboo, bushes, ferns)
			string alike = s.Style == Tropical ? @"^BigPalm\d+$" : s.Style == Forest ? @"^(PineTree|BirchTree)_\w+$" : @"^SmallBushyTree_\d+$";
			string rest = s.Style == Tropical ? @"^Bamboo_\d+$" : s.Style == Forest ? @"^(Balboa_(Big)?Bush_\d+ Variant)$" : @"^DesertFern_\d+$";
			var first = land.Where(o => System.Text.RegularExpressions.Regex.IsMatch(o.Name, alike)).ToList();
			var then = land.Where(o => System.Text.RegularExpressions.Regex.IsMatch(o.Name, rest)).ToList();
			Shuffle(first, r); Shuffle(then, r);
			foreach (IslandObject o in first.Concat(then))
			{
				if (have >= want) break;
				// (spread out: not next to another tree to cut)
				if (f.Objects.Any(x => CutTree.IsMatch(x.Name) && (x.Position - o.Position).sqrMagnitude < 36f)) continue;
				o.Name = kinds[r.Next(kinds.Length)];
				o.EulerRotation = new Vector3(0f, o.EulerRotation.y, 0f);
				o.Scale = Vector3.one;
				have++;
			}
		}

		static void Shuffle<T>(List<T> list, System.Random r)
		{
			for (int i = list.Count - 1; i > 0; i--) { int j = r.Next(i + 1); T t = list[i]; list[i] = list[j]; list[j] = t; }
		}

		/// <summary>An island file's land above the sea, m².</summary>
		public static float LandArea(IslandFile f)
		{
			int res = f.HeightmapResolution, cells = 0;
			float step = f.TerrainSize.x / (res - 1);
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) if (f.Heights[z, x] * f.TerrainSize.y > f.WaterLevel + 0.1f) cells++;
			return cells * step * step;
		}

		/// <summary>The land object sliders of Raft's own small islands, which are much denser than its big ones (0.33 without raft_islands.txt).</summary>
		public static void LikeRaftSmall(IslandGenSettings s)
		{
			if (!RaftIslands.DensitiesLikeRaft(s, true)) LikeRaft(s);
		}

		/// <summary>
		/// A large island's content, like Raft's big islands and more: puffer fish around it, a screecher, one or two themed
		/// scenes from the quest islands, and a cave with a guard and a hoard.
		/// </summary>
		public static void Large(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			k.File.Props[IslandProps.Title] = IslandName(r);
			// Puffer fish around, under water (Raft's big islands have about six)
			for (int i = 0; i < 5; i++)
			{
				Vector2? p = k.Find(k.Mid, s.Radius * 1.4f, (above, slope) => above < -4f && above > -14f && slope < 40f, 12f);
				if (p.HasValue) k.Creature("PufferFish", p.Value, 1, "Normal", 1f, null, true, 2f);
			}
			if (s.Style == Tropical)
			{
				Vector2 high = k.Highest(k.Mid, s.Radius * 0.6f);
				k.Creature("StoneBird", high, 1, "Normal", 1f, null, true, 2f);
			}
			// A cave with a guard and a hoard first (it needs the most room: an outcrop over land, open ground at its mouth)
			string guard = s.Style == Snowy ? "PolarBear" : s.Style == Forest ? "Bear" : s.Style == Desert ? "Hyena" : r.NextDouble() < 0.5 ? "Boar" : "Rat";
			EmbeddedCave(k, s, guard, "Treasure", "Cave hoard");
			// Themed scenes on open, flat ground
			List<Theme> themes = ThemesFor(s.Style);
			int scenes = themes.Count == 0 ? 0 : 1 + r.Next(2);
			var used = new List<Vector2>();
			for (int i = 0; i < scenes; i++)
			{
				Theme t = themes[r.Next(themes.Count)];
				themes.Remove(t);
				Vector2? spot = k.Find(k.Mid, s.Radius * 0.75f, (above, slope) => above > 2.5f && above < 25f && slope < 10f, 30f);
				if (!spot.HasValue || used.Any(u => (u - spot.Value).magnitude < 35f)) continue;
				// (not over the den, its guard or the ground in front of its mouth)
				Vector2 sp = spot.Value;
				if (k.File.Objects.Any(o => o.Props != null && o.Props.ContainsKey("cave") && new Vector2(o.Position.x - sp.x, o.Position.z - sp.y).magnitude < 38f)) continue;
				if (Dress(k, t, spot.Value, 9f)) used.Add(spot.Value);
				if (themes.Count == 0) break;
			}
			// Up to two of the generator's landmarks (ROADMAP LM10): a wreck, a lighthouse, a jetty, a skyscraper... - away
			// from the scenes and the cave
			var spots = new List<Vector2>(used);
			spots.AddRange(k.File.Objects.Where(o => o.Props != null && o.Props.ContainsKey("cave")).Select(o => new Vector2(o.Position.x, o.Position.z)));
			for (int i = r.Next(3); i > 0; i--) GenBuildings.Landmark(k, s, spots);
		}

		#endregion
	}
}

using System.Collections.Generic;
using System.Linq;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's story-island doors, hatches, crank wheels and levers as ready pieces (TODO "Raft's quest items used in the\n	/// plans" / ROADMAP LM12): placed from the object list they already work in a world, with Raft's own quest item as the
	/// key where Raft used one - a keycard door opens (it goes) for a player whose crew holds Raft's keycard (the story
	/// item raft-&lt;type&gt; a quest item pickup gives, kept for later doors: a "has" check, not "take"); a hatch opens when
	/// used; a crank wheel or a lever sends the signal "crank" or "lever" that the island's other objects, its quest and
	/// the world plan can wait for. Every setting can be changed in the behaviour window like any other.
	/// More of Raft's story machinery (LM12, 2026-10-06): lifts that carry the player up and down when used (Tangaroa's
	/// elevator, Varuna Point's skylifts), cages cut open with Raft's bolt cutters, security cameras that sweep, generators
	/// that start with Raft's generator part and send "power", radios that work with power (a started generator or Raft's
	/// battery charger part) and send "radio", Vasagatan's engine that starts with Raft's gas tank and sends "engine", and
	/// Temperance's turning mirrors (a puzzle piece: each use turns it a quarter, the next one back, sending "mirror").
	/// </summary>
	public static class ReadyPieces
	{
		public class Piece
		{
			public string Name, Kind, Item, Use, Locked;
		}

		public const string Door = "door", Hatch = "hatch", Crank = "crank", Lever = "lever";
		public const string Lift = "lift", Cage = "cage", Camera = "camera", Generator = "generator", Radio = "radio", Engine = "engine", Mirror = "mirror";
		public const string Wheel = "wheel", Pipe = "pipe", Wire = "wire", Claw = "claw", Scales = "scales";
		public const string Laser = "laser", FixedMirror = "fixed mirror", LaserTarget = "laser target";
		/// <summary>Gap features (2026-10-24): a socket an item goes into (G2), an explosive barrel and a pillar that hurt a
		/// boss (G4), a spotlight that keeps creatures back (G12), a broken-down vehicle repaired with bolts (G3).</summary>
		public const string Socket = "socket", Barrel = "barrel", Pillar = "pillar", Spotlight = "spotlight", Vehicle = "vehicle";
		public const string SocketSignal = "socket", BlastSignal = "blast", CollapseSignal = "collapse", VehicleSignal = "vehicle";
		/// <summary>The generator parts a generator placed from the editor wants (G7); old islands and recipes keep 1.</summary>
		public const int DefaultGeneratorParts = 3;
		/// <summary>The signals the new kinds send (a quest, the world plan or other objects can wait for them).</summary>
		public const string PowerSignal = "power", RadioSignal = "radio", EngineSignal = "engine", MirrorSignal = "mirror", CageSignal = "cage";
		public const string LaserSignal = "laser";
		public const string WheelSignal = "wheel", PipeSignal = "pipe", WireSignal = "wire", ClawSignal = "claw", ScalesSignal = "scales";

		public static readonly Piece[] All =
		{
			new Piece { Name = "PlantationDoor", Kind = Door, Item = "Tangaroa_KeyCard", Use = "Swipe the keycard", Locked = "A keycard reader blinks red. It wants a Tangaroa keycard." },
			new Piece { Name = "TP_Selene_Int_DoorFrame_WithDoorReactorKey", Kind = Door, Item = "Temperance_ReactorKey", Use = "Turn the reactor key", Locked = "The door needs the reactor key." },
			new Piece { Name = "UT_MainEntranceDoor", Kind = Door, Item = "Utopia_KeyEntrance", Use = "Unlock the entrance", Locked = "The entrance is locked. Somewhere there is a key for it." },
			new Piece { Name = "UT_Hut_LockedDoor_DoorL", Kind = Door, Item = "Utopia_KeyPhase1", Use = "Unlock the door", Locked = "Locked. It needs a key." },
			new Piece { Name = "UT_Hut_LockedDoor_DoorR", Kind = Door, Item = "Utopia_KeyPhase1", Use = "Unlock the door", Locked = "Locked. It needs a key." },
			new Piece { Name = "UT_Skyscraber01PadlockDoor", Kind = Door, Item = "Vasagatan_BoltCutter", Use = "Cut the padlock", Locked = "A heavy padlock. Bolt cutters would get through it." },
			new Piece { Name = "UT_DettoDoor", Kind = Door, Item = "Utopia_DettoCode", Use = "Enter the code", Locked = "A code lock. Someone must have written the code down." },
			new Piece { Name = "VP_GarageDoor", Kind = Door, Item = "Varuna_MotherlodeKey", Use = "Turn the key", Locked = "The garage door's keyhole is empty. It needs a key." },
			new Piece { Name = "TangaroaHatchRoom_Hatch", Kind = Hatch, Use = "Open the hatch" },
			new Piece { Name = "Hatch", Kind = Hatch, Use = "Open the hatch" },
			new Piece { Name = "TP_CoolingStation_Monitor_Hatch01", Kind = Hatch, Use = "Open the hatch" },
			new Piece { Name = "TP_CoolingStation_Monitor_Hatch02", Kind = Hatch, Use = "Open the hatch" },
			new Piece { Name = "TP_Selene_DoorCrankWheel", Kind = Crank, Use = "Turn the crank wheel" },
			new Piece { Name = "UT_DrawBridgeMechanism_Cogwheel01", Kind = Crank, Use = "Turn the cogwheel" },
			new Piece { Name = "TP_CoolingStation_Monitor_Lever", Kind = Lever, Use = "Pull the lever" },
			new Piece { Name = "TP_LaserControlPanel_lever", Kind = Lever, Use = "Pull the lever" },
			new Piece { Name = "TradingPost_Register_Lever", Kind = Lever, Use = "Pull the lever" },
			// (LM12, 2026-10-06)
			new Piece { Name = "Elevator", Kind = Lift, Use = "Ride the lift" },
			new Piece { Name = "VP_Skylift", Kind = Lift, Use = "Ride the lift" },
			new Piece { Name = "VP_Skylift_Extended", Kind = Lift, Use = "Ride the lift" },
			new Piece { Name = "UT_DogCage_Medium02", Kind = Cage, Item = "Vasagatan_BoltCutter", Use = "Cut the cage open", Locked = "The cage is chained shut. Bolt cutters would get through the chain." },
			new Piece { Name = "RT_SharkCage", Kind = Cage, Item = "Vasagatan_BoltCutter", Use = "Cut the cage open", Locked = "The cage is chained shut. Bolt cutters would get through the chain." },
			new Piece { Name = "RT_Camera", Kind = Camera },
			new Piece { Name = "EmergencyGenerator", Kind = Generator, Item = "Tangaroa_GeneratorPart", Use = "Start the generator", Locked = "It won't start. A part is missing - a generator part." },
			new Piece { Name = "Generator", Kind = Generator, Item = "Tangaroa_GeneratorPart", Use = "Start the generator", Locked = "It won't start. A part is missing - a generator part." },
			new Piece { Name = "UT_Generator01", Kind = Generator, Item = "Tangaroa_GeneratorPart", Use = "Start the generator", Locked = "It won't start. A part is missing - a generator part." },
			new Piece { Name = "VG_DecorationPrefabBase_EmergencyGenerator Variant", Kind = Generator, Item = "Tangaroa_GeneratorPart", Use = "Start the generator", Locked = "It won't start. A part is missing - a generator part." },
			new Piece { Name = "RT_CommRadio", Kind = Radio, Item = "Caravan_BatteryChargerPart", Use = "Use the radio", Locked = "The radio is dead. It needs power - a running generator, or a battery." },
			new Piece { Name = "Model_Radio", Kind = Radio, Item = "Caravan_BatteryChargerPart", Use = "Use the radio", Locked = "The radio is dead. It needs power - a running generator, or a battery." },
			new Piece { Name = "Balboa_DecorationPrefabBase_RadioDevice", Kind = Radio, Item = "Caravan_BatteryChargerPart", Use = "Use the radio", Locked = "The radio is dead. It needs power - a running generator, or a battery." },
			new Piece { Name = "VG_DecorationPrefabBase_Engine Variant", Kind = Engine, Item = "Vasagatan_GasTank", Use = "Start the engine", Locked = "The tank is dry. It needs fuel - a gas tank." },
			new Piece { Name = "TP_MirrorHousing_Rotating", Kind = Mirror, Use = "Turn the mirror" },
			new Piece { Name = "TP_MirrorHousing_InteractableMirror", Kind = Mirror, Use = "Turn the mirror" },
			// (Raft's puzzle mini-games as the mod's own pieces, 2026-10-09: Raft's puzzle scripts don't come with copied objects)
			new Piece { Name = "UT_WaterChallengeWheel1", Kind = Wheel, Use = "Turn the wheel" },
			new Piece { Name = "UT_WaterChallengeWheel2", Kind = Wheel, Use = "Turn the wheel" },
			new Piece { Name = "UT_WaterChallengeWheel3", Kind = Wheel, Use = "Turn the wheel" },
			new Piece { Name = "WaterPipe_Short_1", Kind = Pipe, Use = "Turn the pipe" },
			new Piece { Name = "WaterPipe_BigCurve_1", Kind = Pipe, Use = "Turn the pipe" },
			new Piece { Name = "RT_PipeBendL", Kind = Pipe, Use = "Turn the pipe" },
			new Piece { Name = "UT_ElectricCableHub01", Kind = Wire, Item = "Temperance_ElectricalCable", Use = "Connect the cable", Locked = "No cable runs to this connector. It needs an electrical cable." },
			new Piece { Name = "UT_ElectricCableHub02", Kind = Wire, Item = "Temperance_ElectricalCable", Use = "Connect the cable", Locked = "No cable runs to this connector. It needs an electrical cable." },
			new Piece { Name = "UT_ElectricCableHub03", Kind = Wire, Item = "Temperance_ElectricalCable", Use = "Connect the cable", Locked = "No cable runs to this connector. It needs an electrical cable." },
			new Piece { Name = "Claw_ControlBoard", Kind = Claw, Use = "Work the claw" },
			new Piece { Name = "TangaroaScaleModel", Kind = Scales, Use = "Tip the scales" },
			// (Temperance's laser puzzle: the beam is the mod's own, LaserBeam.cs)
			new Piece { Name = "TP_LaserEmitter", Kind = Laser },
			new Piece { Name = "TP_MirrorHousing_StaticMirror", Kind = FixedMirror },
			new Piece { Name = "TP_LaserDoorPanel", Kind = LaserTarget },
			new Piece { Name = "TP_CoolingStation_Keysocket", Kind = Socket, Item = "MetalIngot", Use = "Put it in the socket", Locked = "An empty socket. Something fits in here." },
			new Piece { Name = "VP_ExplosiveBarrel_Pile", Kind = Barrel, Use = "Light the fuse" },
			new Piece { Name = "VP_MotherlodePillar1_Stage1", Kind = Pillar, Use = "Knock the pillar down" },
			new Piece { Name = "VP_MotherlodePillar2_Stage1", Kind = Pillar, Use = "Knock the pillar down" },
			new Piece { Name = "VP_MotherlodePillar3_Stage1", Kind = Pillar, Use = "Knock the pillar down" },
			new Piece { Name = "VP_Spotlight", Kind = Spotlight },
			new Piece { Name = "RT_Spotlight", Kind = Spotlight },
			new Piece { Name = "TP_SnowVehicle01", Kind = Vehicle, Item = "Bolt", Use = "Start the vehicle", Locked = "It's broken down. 3 bolts would get it running." },
			new Piece { Name = "TP_SnowVehicle02", Kind = Vehicle, Item = "Bolt", Use = "Start the vehicle", Locked = "It's broken down. 3 bolts would get it running." },
		};

		/// <summary>The pieces added for LM12's rest (2026-10-06): lifts, cages, cameras, generators, radios, the engine, mirrors;
		/// (2026-10-09) the puzzle pieces: water wheels, pipes, wire connectors, the claw console, the scales.</summary>
		public static IEnumerable<Piece> Machinery { get { return All.Where(p => p.Kind != Door && p.Kind != Hatch && p.Kind != Crank && p.Kind != Lever); } }

		/// <summary>Kinds that turn a quarter (open = turned) each use and send their signal: a puzzle is a check on their states.</summary>
		public static bool Turns(string kind) { return kind == Mirror || kind == Wheel || kind == Pipe; }


		public static Piece Of(string name) { return All.FirstOrDefault(p => p.Name == name); }

		/// <summary>The piece's working settings (ObjectProps.Defaults).</summary>
		public static void Defaults(string name, Dictionary<string, string> props)
		{
			Piece p = Of(name);
			if (p == null) return;
			if (p.Use != null) props[BehaviourProps.Use] = p.Use;
			string item = p.Item != null ? StoryItems.Prefix + QuestItemPickups.IdPrefix + p.Item : null;
			switch (p.Kind)
			{
				case Socket:
					// (an item goes in once for good: open = filled - a puzzle checks the sockets' states; the item is a
					// Raft item here, changed in the behaviour window)
					props[BehaviourProps.CheckKey("use")] = ObjCheck.AnyLine + "\nstate||open\ntake|" + p.Item + "|1";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					props[BehaviourProps.EventKey("use")] = "open|" + "\n" + "message||It clicks into place." + "\n" + "signal||" + SocketSignal;
					break;
				case Barrel:
				case Pillar:
					// (the creature spot named "boss" loses a third of its health: three barrels or pillars bring it down)
					props[BehaviourProps.EventKey("use")] = (p.Kind == Barrel ? "message||The fuse fizzes... BOOM!" : "message||The pillar cracks and comes crashing down!") + "\n" +
						"hit|boss|34" + "\n" + "hide|" + "\n" + "signal||" + (p.Kind == Barrel ? BlastSignal : CollapseSignal);
					break;
				case Spotlight:
					props[GapFeatures.Repel] = "8";
					break;
				case Vehicle:
					// (not drivable - Raft's snowmobile is Temperance's own networked vehicle: repaired, it carries the
					// players standing on it 24 m along and back on the next use)
					props[BehaviourProps.Move] = "0,0,24";
					props[BehaviourProps.MoveTime] = "10";
					props[BehaviourProps.Carry] = "1";
					props[BehaviourProps.CheckKey("use")] = ObjCheck.AnyLine + "\nsignal|" + VehicleSignal + "|\ntake|" + p.Item + "|3";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					props[BehaviourProps.EventKey("use")] = "message||The engine catches. Hold on!" + "\n" + "signal||" + VehicleSignal + "\n" + "switch|";
					break;
				case Lift:
					// (used, it goes up 6 m and down again on the next use, carrying the player standing on it)
					props[BehaviourProps.Move] = "0,6,0";
					props[BehaviourProps.MoveTime] = "5";
					props[BehaviourProps.Carry] = "1";
					break;
				case Cage:
					props[BehaviourProps.CheckKey("use")] = "has|" + item + "|1";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					props[BehaviourProps.EventKey("use")] = "message||The chain snaps and the cage swings open." + "\n" + "hide|" + "\n" + "signal||" + CageSignal;
					break;
				case Camera:
					// (sweeps 70 degrees and back, for ever)
					props[BehaviourProps.Turn] = "70";
					props[BehaviourProps.MoveMode] = "loop";
					props[BehaviourProps.MoveTime] = "4";
					break;
				case Generator:
				case Engine:
					// (started once it runs for good: a later use finds the signal and runs again without wanting the part)
					string sig = p.Kind == Generator ? PowerSignal : EngineSignal;
					props[BehaviourProps.CheckKey("use")] = ObjCheck.AnyLine + "\nsignal|" + sig + "|\ntake|" + item + "|1";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					props[BehaviourProps.EventKey("use")] = "message||" + (p.Kind == Generator ? "The generator coughs, then hums. The power is on." : "The engine sputters, then roars.") + "\n" + "signal||" + sig;
					break;
				case Radio:
					props[BehaviourProps.CheckKey("use")] = ObjCheck.AnyLine + "\nsignal|" + PowerSignal + "|\nhas|" + item + "|1";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					props[BehaviourProps.EventKey("use")] = "message||Static crackles... then a voice, far away, repeating a frequency." + "\n" + "signal||" + RadioSignal;
					break;
				case Laser:
					props[LaserBeam.Prop] = LaserBeam.Beam;
					break;
				case FixedMirror:
					props[LaserBeam.Prop] = LaserBeam.Mirror;
					props[BehaviourProps.Collision] = "solid";
					break;
				case LaserTarget:
					// (the beam has to stop on it: a box if it has no collider)
					props[BehaviourProps.Collision] = "solid";
					props[BehaviourProps.EventKey(LaserBeam.Event)] = "message||The panel flashes green. Something unlocks." + "\n" + "signal||" + LaserSignal;
					break;
				case Mirror:
				case Wheel:
				case Pipe:
					if (p.Kind == Mirror)
					{
						// (Temperance's mirrors send a laser beam on, along their front: turned, the beam turns)
						props[LaserBeam.Prop] = LaserBeam.Mirror;
						props[BehaviourProps.Collision] = "solid";
					}
					props[BehaviourProps.Turn] = "90";
					props[BehaviourProps.MoveTime] = p.Kind == Wheel ? "1.5" : "0.8";
					props[BehaviourProps.EventKey("use")] = "switch|" + "\n" + "signal||" + p.Kind;
					break;
				case Wire:
					// (connected once for good: open = connected; a later use finds it open and doesn't want another cable)
					props[BehaviourProps.CheckKey("use")] = ObjCheck.AnyLine + "\nstate||open\ntake|" + item + "|1";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					props[BehaviourProps.EventKey("use")] = "open|" + "\n" + "message||The cable clicks in. Current hums along the wire." + "\n" + "signal||" + WireSignal;
					break;
				case Claw:
					props[BehaviourProps.EventKey("use")] = "message||The claw whirs down, closes... and rises." + "\n" + "signal||" + ClawSignal;
					break;
				case Scales:
					props[BehaviourProps.EventKey("use")] = "message||The scales tip, sway, and settle." + "\n" + "signal||" + ScalesSignal;
					break;
				case Door:
					// (it goes when opened: Raft's doors swing on hinges this piece doesn't have)
					props[BehaviourProps.CheckKey("use")] = "has|" + StoryItems.Prefix + QuestItemPickups.IdPrefix + p.Item + "|1";
					props[BehaviourProps.ElseKey("use")] = "message||" + p.Locked;
					// (a card reader beeps its card through: Raft's own sound, RF4)
					props[BehaviourProps.EventKey("use")] = (KeycardDoor.IsCardReader(name) ? "sound||" + KeycardDoor.CardSound + "\n" : "") + "hide|";
					break;
				case Hatch:
					props[BehaviourProps.EventKey("use")] = "hide|";
					break;
				case Crank:
					props[BehaviourProps.EventKey("use")] = "message||The wheel turns with a long groan." + "\n" + "signal||crank";
					break;
				case Lever:
					props[BehaviourProps.EventKey("use")] = "message||Clunk. Something moved." + "\n" + "signal||lever";
					break;
			}
		}

		public static bool IsGenerator(string name) { Piece p = Of(name); return p != null && p.Kind == Generator; }

		/// <summary>How many generator parts a generator wants (its "use" check's take line), 0 = not a generator's check.</summary>
		public static int PartsOf(Dictionary<string, string> props)
		{
			foreach (string l in ObjectProps.Get(props, BehaviourProps.CheckKey("use")).Split('\n'))
			{
				ObjCheck c = ObjCheck.Parse(l.Trim());
				if (c != null && c.Kind == "take" && c.Target.EndsWith("Tangaroa_GeneratorPart")) return c.Count;
			}
			return 0;
		}

		/// <summary>A generator wants n generator parts (G7): every one is used up when it starts.</summary>
		public static void SetParts(string name, Dictionary<string, string> props, int n)
		{
			Piece p = Of(name);
			if (p == null || p.Kind != Generator || props == null) return;
			n = n < 1 ? 1 : n > 9 ? 9 : n;
			string item = StoryItems.Prefix + QuestItemPickups.IdPrefix + p.Item;
			props[BehaviourProps.CheckKey("use")] = ObjCheck.AnyLine + "\nsignal|" + PowerSignal + "|\ntake|" + item + "|" + n;
			props[BehaviourProps.ElseKey("use")] = "message||" + (n == 1 ? p.Locked : "It won't start. It needs " + n + " generator parts.");
		}

		/// <summary>The story item a piece wants (raft-&lt;type&gt;), or null.</summary>
		public static string ItemOf(string name) { Piece p = Of(name); return p != null && p.Item != null ? QuestItemPickups.IdPrefix + p.Item : null; }
	}
}

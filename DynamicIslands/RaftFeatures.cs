using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's features on islands and the tools players need for them, in one place (the user, 2026-10-16): vines want the
	/// machete, buried treasure the metal detector and the shovel, dirt spots the shovel, Raft's zipline lines the zipline
	/// tool. The plan Check and its Progression panel walk a plan in play order with it (is the tool given before the island
	/// that wants it?), and the generator's explorer's chest (GenFeatures) holds what its features want.
	/// Since 2026-10-24 (G5, G6, G14, G15) also the headlight (dark places), binoculars, oxygen bottle and flippers (air
	/// zones, deep dives: advice), the axe and the metal hook, and the bolt cutters (cages): a story item, shown as a tool.
	/// Other story-item locks (a keycard door, a generator and its part) are not here: they want a story item, ReadyPieces
	/// says which, and Check follows them as story items.
	/// </summary>
	public static class RaftFeatures
	{
		public const string MetalDetectorItem = "MetalDetector", ShovelItem = "Shovel", HeadLightItem = "HeadLight", BinocularsItem = "Binoculars",
			OxygenItem = "OxygenBottle", FlipperItem = "Flipper", AxeItem = "Axe", HookItem = "Hook_Scrap", BoltCutterItem = "Vasagatan_BoltCutter";
		/// <summary>An object a step wants this far below the sea (metres) is a deep dive: oxygen or flippers (advice).</summary>
		public const float DeepMetres = 15f;
		/// <summary>An atmosphere zone whose light tint leaves this little brightness (0..1) is dark: the headlight (advice).</summary>
		public const float DarkLight = 0.7f;

		/// <summary>One of Raft's tools a feature wants.</summary>
		public class Tool
		{
			/// <summary>Raft's item (its unique name) and what players call it.</summary>
			public string Item, Label;
			/// <summary>Its blueprint: on one of Raft's story islands (raft_blueprints.txt), or given by an island.</summary>
			public string Blueprint;
			/// <summary>Learned at Raft's research table, in every world: no island has to give it. (No tool is, since
			/// 2026-10-24 - G6: the shovel and the zipline tool are given by a plan's island or one of Raft's story islands too.)</summary>
			public bool Researched;
			/// <summary>Other items that count as it (the advanced headlight, the electric zipline tool, the titanium axe) and
			/// other blueprints that give it.</summary>
			public string[] Also = new string[0], Blueprints = new string[0];
			/// <summary>A story item that is this tool (raft-Vasagatan_BoltCutter): given and wanted as a story item - Check
			/// follows it with the story items - and shown as a tool.</summary>
			public string Story;
			public IEnumerable<string> Names { get { return new[] { Item }.Concat(Also); } }
			public IEnumerable<string> AllBlueprints { get { return (Blueprint != null ? new[] { Blueprint } : new string[0]).Concat(Blueprints); } }
		}

		/// <summary>A feature: a placed object that wants tools.</summary>
		public class Feature
		{
			public string Object, Label;
			public string[] Tools;
			/// <summary>Always an optional find (Raft's treasure loot, dirt): no quest step can point at it.</summary>
			public bool Optional;
			/// <summary>Any one of the tools is enough (oxygen bottle or flippers): kept as one need, "OxygenBottle/Flipper".</summary>
			public bool Any;
			/// <summary>Players can do it without the tool, but hardly (a dark cave, a long dive): Check's warning is advice.</summary>
			public bool Advisory;
			/// <summary>Only objects whose settings pass (a dark atmosphere zone, an air zone); null: every object of that name.</summary>
			public Func<IDictionary<string, string>, bool> When;
			/// <summary>A zone: the main quest wants it when a step wants something inside its radius.</summary>
			public bool Zone;
			/// <summary>The needs' keys in Facts.NeedsTools: the tools, or the tools joined by "/" (Any).</summary>
			public string[] Needs { get { return Any ? new[] { string.Join("/", Tools) } : Tools; } }
		}

		public static readonly Tool[] Tools =
		{
			new Tool { Item = ContentCatalog.MacheteItem, Label = "machete", Blueprint = "Blueprint_Machete" },
			new Tool { Item = MetalDetectorItem, Label = "metal detector", Blueprint = "Blueprint_MetalDetector" },
			new Tool { Item = ShovelItem, Label = "shovel" },
			new Tool { Item = ContentCatalog.ZiplineItem, Label = "zipline tool", Blueprint = "Blueprint_ZiplineTool", Also = new[] { "ZiplineTool_Electric" }, Blueprints = new[] { "Blueprint_ZiplineToolElectric" } },
			new Tool { Item = HeadLightItem, Label = "headlight", Blueprint = "Blueprint_HeadLight", Also = new[] { "HeadLight_Advanced" }, Blueprints = new[] { "Blueprint_HeadLight_Advanced" } },
			new Tool { Item = BinocularsItem, Label = "binoculars" },
			new Tool { Item = OxygenItem, Label = "oxygen bottle" },
			new Tool { Item = FlipperItem, Label = "flippers" },
			new Tool { Item = AxeItem, Label = "axe", Also = new[] { "Axe_Stone", "Axe_Titanium" } },
			new Tool { Item = HookItem, Label = "metal hook", Also = new[] { "Hook_Titanium" } },
			// (a story item: Raft's bolt cutters, given by a plan's island as story:raft-Vasagatan_BoltCutter - its pickup)
			new Tool { Item = BoltCutterItem, Label = "bolt cutters", Story = QuestItemPickups.IdPrefix + BoltCutterItem },
		};

		/// <summary>An atmosphere zone dark enough for the headlight: its light tint, at its strength, leaves little brightness.</summary>
		static bool Dark(IDictionary<string, string> p)
		{
			if (!ObjectProps.HasColor(p, ObjectProps.AtmoLight)) return false;
			UnityEngine.Color c = UnityEngine.Color.Lerp(UnityEngine.Color.white, ObjectProps.ColorOf(p, ObjectProps.AtmoLight), UnityEngine.Mathf.Clamp01(ObjectProps.GetFloat(p, ObjectProps.AtmoLightAmount, 0.5f)));
			return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b <= DarkLight;
		}

		static bool Air(IDictionary<string, string> p) { return ObjectProps.Get(p, ObjectProps.ZoneAir).Trim() == "1"; }

		/// <summary>A sea vine with a behaviour of its own (a vine wall cut to pass); the generator's reef decoration has none.</summary>
		static bool Used(IDictionary<string, string> p) { return p.Keys.Any(k => k.StartsWith(BehaviourProps.CheckPrefix) || k.StartsWith(BehaviourProps.EventPrefix)); }

		public static readonly Feature[] Features =
		{
			// (the vines' own check, has|Machete - ObjectProps.Defaults - keeps the machete for the next vines)
			new Feature { Object = ContentCatalog.MacheteVines, Label = "vines", Tools = new[] { ContentCatalog.MacheteItem } },
			new Feature { Object = ContentCatalog.BuriedTreasure, Label = "buried treasure", Tools = new[] { MetalDetectorItem, ShovelItem }, Optional = true },
			new Feature { Object = "Pickup_Landmark_DirtPickup", Label = "dirt spots", Tools = new[] { ShovelItem }, Optional = true },
			new Feature { Object = "ZiplinePath_Landmark", Label = "a zipline", Tools = new[] { ContentCatalog.ZiplineItem } },
			new Feature { Object = "ZiplinePath", Label = "a zipline", Tools = new[] { ContentCatalog.ZiplineItem } },
			// (G5: Raft's other zipline lines, and sea vines - only when they do something: the generator's reef decoration doesn't)
			new Feature { Object = "ZiplinePath_Caravan", Label = "a zipline", Tools = new[] { ContentCatalog.ZiplineItem } },
			new Feature { Object = "ZiplinePath_JumpChallenge", Label = "a zipline", Tools = new[] { ContentCatalog.ZiplineItem } },
			new Feature { Object = "ZiplinePath_Utopia_ZipineHook", Label = "a zipline", Tools = new[] { ContentCatalog.ZiplineItem } },
			new Feature { Object = "SeaVine3", Label = "sea vines", Tools = new[] { ContentCatalog.MacheteItem }, When = Used },
			new Feature { Object = "seavine_tongue", Label = "sea vines", Tools = new[] { ContentCatalog.MacheteItem }, When = Used },
			new Feature { Object = "Seavine_tongue", Label = "sea vines", Tools = new[] { ContentCatalog.MacheteItem }, When = Used },
			// (G15: cages and the padlocked door want the bolt cutters; ReadyPieces gives them the check)
			new Feature { Object = "UT_DogCage_Medium02", Label = "a cage", Tools = new[] { BoltCutterItem } },
			new Feature { Object = "RT_SharkCage", Label = "a cage", Tools = new[] { BoltCutterItem } },
			new Feature { Object = "UT_Skyscraber01PadlockDoor", Label = "a padlocked door", Tools = new[] { BoltCutterItem } },
			// (G14 and dark places: advice - players manage without, hardly)
			new Feature { Object = ContentCatalog.TriggerZone, Label = "an underwater air zone", Tools = new[] { OxygenItem, FlipperItem }, Any = true, Advisory = true, Zone = true, When = Air },
			new Feature { Object = ContentCatalog.AtmosphereZoneName, Label = "a dark place", Tools = new[] { HeadLightItem }, Advisory = true, Zone = true, When = Dark },
		};

		/// <summary>
		/// Raft's story-island blueprints that build on others (TODO 4c "every blueprint and key item, in order"): the engine
		/// controls steer the motor wheels and the steering wheel, the fuel pipes feed from a fuel tank, the advanced biofuel
		/// extractor replaces the plain one, the water pipes run from a water tank. Raft's own order gives the bases first
		/// (Vasagatan before Caravan Town, Balboa before Temperance); a plan that gives one before its bases gets a Check
		/// warning (PlanChecker.CheckBlueprintOrder): players find a blueprint they can't use yet.
		/// </summary>
		public static readonly Dictionary<string, string[]> BuildsOn = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			{ "Blueprint_EngineControls", new[] { "Blueprint_MotorWheel", "Blueprint_SteeringWheel" } },
			{ "Blueprint_Pipe_Fuel", new[] { "Blueprint_Fueltank" } },
			{ "Blueprint_BiofuelExtractorAdvanced", new[] { "Blueprint_BiofuelExtractor" } },
			{ "Blueprint_Pipe_Water", new[] { "Blueprint_WaterTank" } },
			{ "Blueprint_HeadLight_Advanced", new[] { "Blueprint_HeadLight" } },
			{ "Blueprint_ZiplineToolElectric", new[] { "Blueprint_ZiplineTool" } },
		};

		/// <summary>The feature an object is, or null.</summary>
		public static Feature Of(string objectName) { return Features.FirstOrDefault(f => f.Object == objectName && f.When == null); }

		/// <summary>The feature a placed object is, with its settings (a dark atmosphere zone, an air zone), or null.</summary>
		public static Feature Of(string objectName, IDictionary<string, string> props)
		{
			props = props ?? new Dictionary<string, string>();
			return Features.FirstOrDefault(f => f.Object == objectName && (f.When == null || f.When(props)));
		}

		/// <summary>The tools an object wants as a feature (none: not a feature).</summary>
		public static string[] ToolsOf(string objectName) { Feature f = Of(objectName); return f != null ? f.Tools : new string[0]; }

		/// <summary>A tool by its item name, or null.</summary>
		public static Tool ToolOf(string item) { return Tools.FirstOrDefault(t => t.Names.Any(n => n.Equals(item ?? "", StringComparison.OrdinalIgnoreCase))); }

		/// <summary>The tool a story item is (raft-Vasagatan_BoltCutter: the bolt cutters), or null.</summary>
		public static Tool ToolOfStory(string id) { return Tools.FirstOrDefault(t => t.Story != null && t.Story.Equals(id ?? "", StringComparison.OrdinalIgnoreCase)); }

		/// <summary>A need's label: the tool, or "oxygen bottle or flippers" (Feature.Any).</summary>
		public static string LabelOf(string item)
		{
			if ((item ?? "").Contains("/")) return string.Join(" or ", item.Split('/').Select(LabelOf).ToArray());
			Tool t = ToolOf(item); return t != null ? t.Label : ContentCatalog.ItemLabel(item);
		}

		/// <summary>Do the tools held cover a need (one of them, for "OxygenBottle/Flipper")?</summary>
		public static bool Has(ICollection<string> held, string need) { return (need ?? "").Split('/').Any(held.Contains); }

		static Regex given;

		/// <summary>The tools an island's settings give (an item list - loot, a give action, a quest reward - with the tool or
		/// its blueprint: "Machete*1", "Blueprint_Machete*1").</summary>
		public static IEnumerable<string> GivenIn(string text)
		{
			if (given == null)
			{
				// (every item and blueprint name of the tools, longest first; a story tool comes with the story items)
				string[] names = Tools.Where(t => t.Story == null).SelectMany(t => t.Names.Concat(t.AllBlueprints.Select(b => b.Substring("Blueprint_".Length))))
					.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(n => n.Length).ToArray();
				given = new Regex(@"(?<![A-Za-z0-9_:-])(Blueprint_)?(" + string.Join("|", names.Select(Regex.Escape).ToArray()) + @")\s*\*", RegexOptions.IgnoreCase);
			}
			var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match m in given.Matches(text ?? ""))
			{
				string name = m.Groups[2].Value;
				Tool t = (m.Groups[1].Success ? Tools.FirstOrDefault(x => x.AllBlueprints.Any(b => b.Equals("Blueprint_" + name, StringComparison.OrdinalIgnoreCase))) : null) ?? ToolOf(name);
				if (t != null && t.Story == null) found.Add(t.Item);
			}
			return found;
		}

		/// <summary>The tools a list of blueprints gives (one of Raft's story islands).</summary>
		public static IEnumerable<string> FromBlueprints(IEnumerable<string> blueprints)
		{
			var b = new HashSet<string>(blueprints, StringComparer.OrdinalIgnoreCase);
			return Tools.Where(t => t.AllBlueprints.Any(b.Contains)).Select(t => t.Item);
		}
	}
}

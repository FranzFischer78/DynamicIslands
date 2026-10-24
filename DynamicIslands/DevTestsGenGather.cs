using System;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>The generator's things to gather (ROADMAP LM9): off by default (the island is unchanged), a little and
		/// much on the land by style, Raft's sea finds in the shallows; all where they belong. Kinds switched off (GatherOff)
		/// stay away, How far out (ShallowsDepth) sets the depth; the randomizer's islands and generated ones get amounts.</summary>
		[ConsoleCommand(name: "CIGenGather", docs: "Dev, anywhere: the generator's Things to gather and Finds in the shallows - off changes nothing, more gives more, land things above the sea, sea finds 0.6-6 m down; kinds switched off, How far out, the randomizer's and generated islands' amounts")]
		public static void GenGatherCommand(string[] args)
		{
			bool ok = true;
			foreach (int style in new[] { TerrainPainter.Tropical, TerrainPainter.Snowy, TerrainPainter.Desert, TerrainPainter.Forest, TerrainPainter.Volcanic })
			{
				Func<float, float, IslandFile> make = (g, w) => IslandGenerator.CreateFile(new IslandGenSettings { Seed = 7300 + style, Style = style, Shape = IslandShapes.Round, Radius = 70f, Height = 16f, ObjectDensity = 0.5f, Gather = g, Shallows = w }, "cigengather");
				IslandFile off = make(0f, 0f), little = make(0.3f, 0.3f), much = make(1f, 1f);
				Func<IslandFile, IslandFile, List<IslandObject>> added = (f, b) => f.Objects.Skip(b.Objects.Count).ToList();
				Check(ref ok, off.Objects.Count == IslandGenerator.CreateFile(new IslandGenSettings { Seed = 7300 + style, Style = style, Shape = IslandShapes.Round, Radius = 70f, Height = 16f, ObjectDensity = 0.5f }, "cigengather").Objects.Count,
					"style " + style + ": off changes nothing (" + off.Objects.Count + " objects)");
				List<IslandObject> a1 = added(little, off), a2 = added(much, off);
				Func<IslandObject, bool> wet = o => ScatterTool.OnlyUnderWater(o.Name) || o.Name.Contains("Sand") || o.Name.Contains("Clay") || o.Name.Contains("Rock ") || o.Name.Contains("Iron") || o.Name.Contains("Copper");
				int l1 = a1.Count(o => !wet(o)), l2 = a2.Count(o => !wet(o)), w1 = a1.Count(wet), w2 = a2.Count(wet);
				Check(ref ok, l1 > 0 && l2 > l1 * 1.5f, "style " + style + ": things to gather on land, a little " + l1 + ", much " + l2 + " (" + string.Join(", ", a2.Where(o => !wet(o)).Select(o => o.Name.Replace("Pickup_Landmark_", "")).Distinct().ToArray()) + ")");
				Check(ref ok, w1 > 0 && w2 > w1 * 1.5f && w2 >= 10, "style " + style + ": finds in the shallows, a little " + w1 + ", much " + w2);
				if (style != TerrainPainter.Snowy && style != TerrainPainter.Desert)
					Check(ref ok, a2.Any(o => o.Name == ContentCatalog.WildHive && ObjectProps.Get(o.Props, ObjectProps.LootItems).Contains("HoneyComb")) && a2.Any(o => o.Name == "Pickup_Landmark_DirtPickup"),
						"style " + style + ": dirt spots and a wild beehive with honeycomb among them");
				float sea = much.WaterLevel;
				Check(ref ok, a2.Where(o => !wet(o)).All(o => o.Position.y > sea + 0.3f), "style " + style + ": land things above the sea");
				Check(ref ok, a2.Where(wet).All(o => o.Position.y < sea - 0.5f && o.Position.y > sea - 6.1f), "style " + style + ": sea finds 0.6-6 m down");
			}
			// Which kinds (GatherOff) and how far out (ShallowsDepth): ROADMAP LM9's rest
			{
				Func<string, float, IslandFile> make = (off, depth) => IslandGenerator.CreateFile(new IslandGenSettings { Seed = 7311, Style = TerrainPainter.Tropical, Shape = IslandShapes.Round, Radius = 70f, Height = 16f, ObjectDensity = 0.5f, Gather = 1f, Shallows = 1f, GatherOff = off, ShallowsDepth = depth }, "cigengather");
				int baseCount = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 7311, Style = TerrainPainter.Tropical, Shape = IslandShapes.Round, Radius = 70f, Height = 16f, ObjectDensity = 0.5f }, "cigengather").Objects.Count;
				Func<IslandFile, List<IslandObject>> added = f => f.Objects.Skip(baseCount).ToList();
				Func<IslandObject, bool> sea = o => GenGather.SeaKeys.Contains(GenGather.KeyOf(o.Name));
				// (old settings and recipes: no GatherOff / ShallowsDepth lines = every kind on, 6 m)
				IslandGenSettings old = IslandGenSettings.FromText("Gather=1\nShallows=1\n");
				Check(ref ok, old.GatherOff == "" && Mathf.Approximately(old.ShallowsDepth, 6f), "older settings: every kind on, finds down to 6 m");
				IslandGenSettings read = IslandGenSettings.FromText(new IslandGenSettings { GatherOff = "pine,flower,clam", ShallowsDepth = 12f }.ToText());
				Check(ref ok, read.GatherOff == "pine,flower,clam" && Mathf.Approximately(read.ShallowsDepth, 12f), "GatherOff and ShallowsDepth saved and read back (" + read.GatherOff + ", " + read.ShallowsDepth + " m)");
				Check(ref ok, IslandGenSettings.FromText("GatherOff=nonsense,Palm\nShallowsDepth=99").GatherOff == "palm" && IslandGenSettings.FromText("ShallowsDepth=99").ShallowsDepth == GenGather.MaxDepth,
					"unknown kinds dropped, the depth kept within " + GenGather.MinDepth + "-" + GenGather.MaxDepth + " m");
				// Only flowers on the land, only sand in the shallows
				string onlyFlowersSand = string.Join(",", GenGather.LandKeys.Concat(GenGather.SeaKeys).Where(k => k != "flower" && k != "sand").ToArray());
				List<IslandObject> picked = added(make(onlyFlowersSand, GenGather.DefaultDepth));
				Check(ref ok, picked.Count(o => !sea(o)) > 0 && picked.Where(o => !sea(o)).All(o => GenGather.KeyOf(o.Name) == "flower"),
					"kinds switched off: only flowers on the land (" + string.Join(", ", picked.Where(o => !sea(o)).Select(o => o.Name.Replace("Pickup_Landmark_", "")).Distinct().ToArray()) + ")");
				Check(ref ok, picked.Count(sea) > 0 && picked.Where(sea).All(o => GenGather.KeyOf(o.Name) == "sand"),
					"kinds switched off: only sand in the shallows (" + string.Join(", ", picked.Where(sea).Select(o => o.Name.Replace("Pickup_Landmark_", "")).Distinct().ToArray()) + ")");
				List<IslandObject> none = added(make(string.Join(",", GenGather.LandKeys.Concat(GenGather.SeaKeys).ToArray()), GenGather.DefaultDepth));
				Check(ref ok, none.Count == 0, "every kind off: nothing added (" + none.Count + ")");
				Check(ref ok, GenGather.LandKeysOf(TerrainPainter.Snowy).SequenceEqual(new[] { "pine", "berry", "flower" }) && GenGather.LandKeysOf(TerrainPainter.Tropical).Contains("hive") && !GenGather.LandKeysOf(TerrainPainter.Desert).Contains("hive"),
					"the pick list offers the style's own kinds (snowy: " + string.Join(", ", GenGather.LandKeysOf(TerrainPainter.Snowy).ToArray()) + ")");
				// How far out: 3 m keeps them at the shore, 16 m takes some well past 6 m
				IslandFile nearF = make("", 3f), farF = make("", 16f);
				float level = nearF.WaterLevel;
				List<IslandObject> near = added(nearF).Where(sea).ToList(), far = added(farF).Where(sea).ToList();
				Check(ref ok, near.Count > 0 && near.All(o => o.Position.y > level - 3.1f && o.Position.y < level - 0.5f), "How far out 3 m: " + near.Count + " finds, all 0.6-3 m down");
				Check(ref ok, far.Count > 0 && far.All(o => o.Position.y > level - 16.1f) && far.Any(o => o.Position.y < level - 6.5f), "How far out 16 m: " + far.Count + " finds, " + far.Count(o => o.Position.y < level - 6.5f) + " deeper than 6 m");
			}
			// The randomizer's own islands get things to gather by its level; other map types and worlds without it don't
			{
				Func<int, string, IslandGenSettings> rolled = (level, type) => { var gs = new IslandGenSettings(); WorldRandomizer.GatherFor(gs, type, new RandomizerSettings { Level = level }); return gs; };
				IslandGenSettings off = rolled(RandomizerSettings.Off, "large"), light = rolled(RandomizerSettings.Light, "oddity"), wild = rolled(RandomizerSettings.Wild, "lair"), other = rolled(RandomizerSettings.Wild, "atoll");
				Check(ref ok, off.Gather == 0f && other.Gather == 0f && light.Gather > 0f && light.Shallows > 0f && wild.Gather > light.Gather && wild.Shallows > light.Shallows,
					"randomizer islands: off " + off.Gather + ", light " + light.Gather + "/" + light.Shallows + ", wild " + wild.Gather + "/" + wild.Shallows + ", other types " + other.Gather);
				var own = new IslandGenSettings { Gather = 0.1f };
				WorldRandomizer.GatherFor(own, "large", new RandomizerSettings { Level = RandomizerSettings.Wild });
				Check(ref ok, own.Gather == 0.1f && own.Shallows > 0f, "randomizer islands: a type's own amount kept");
			}
			// Generated islands while sailing (spawnpool.txt, the Defaults window): the keys are known and kept in range
			Check(ref ok, CustomIslandSpawner.Clamped("generatedgather", 3f) == 1f && CustomIslandSpawner.Clamped("generatedshallowsdepth", 1f) == GenGather.MinDepth &&
				CustomIslandSpawner.NumberKeys.Contains("generatedShallows") && !float.IsNaN(CustomIslandSpawner.DefaultOf("generatedShallowsDepth")),
				"spawnpool.txt: generatedGather, generatedShallows, generatedShallowsDepth known and kept in range");
			if (ok) Log("PASS: generator things to gather"); else Fail("generator things to gather");
		}
	}
}

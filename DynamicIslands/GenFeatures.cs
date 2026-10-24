using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Raft's features" (ROADMAP LM12, generator options): what Raft's story islands have the player do, put on a generated
	/// island - a cache behind vines for the machete, treasure buried for the metal detector and the shovel, a zipline from
	/// the island's top down to its beach, a strongbox behind a code panel (the code on a note elsewhere), a grove of wild
	/// beehives and patches of dirt for the shovel. How many (0-6), different ones first; an explorer's chest by the beach
	/// holds the tools they need (machete, detector, shovel, zipline tool: RaftFeatures), as players may not have them yet.
	/// Past six, Raft's story machinery (LM12, 2026-10-06; ReadyPieces): a generator to start with a part from a toolbox,
	/// then a radio that works with its power and shows a hidden cache; an engine to start with fuel from a crate, showing
	/// another; a cage cut open with Raft's bolt cutters (in the explorer's chest) with a chest inside; a lift up a cliff.
	/// Islands with six or fewer are as before (the extra kinds only come in past six).
	/// </summary>
	public static class GenFeatures
	{
		public const int Max = 10;
		public static readonly string[] Kinds = { "vines", "treasure", "zipline", "code", "hives", "dirt" };
		/// <summary>Raft's story machinery (LM12), placed when more than Kinds.Length features are asked for.</summary>
		public static readonly string[] MoreKinds = { "power", "engine", "cage", "lift" };
		const string GeneratorPiece = "VG_DecorationPrefabBase_EmergencyGenerator Variant", RadioPiece = "RT_CommRadio", EnginePiece = "VG_DecorationPrefabBase_Engine Variant",
			CagePiece = "RT_SharkCage", LiftPiece = "VP_Skylift";
		public static readonly string[] MoreNames = { GeneratorPiece, RadioPiece, EnginePiece, CagePiece, LiftPiece };

		/// <summary>The objects the features need loaded (the machinery's only past six).</summary>
		public static IEnumerable<string> NamesFor(IslandGenSettings s) { return s.Features > Kinds.Length ? Names.Concat(MoreNames) : Names; }

		public static readonly string[] Names = { ContentCatalog.MacheteVines, ContentCatalog.BuriedTreasure, "ZiplinePath_Landmark", ContentCatalog.WildHive,
			"Pickup_Landmark_DirtPickup", "Loot_Chest", "Loot_ChestLarge", "RT_PowerBox", "Note_Papers" };

		/// <summary>Puts s.Features features on the island; what was put (for the report).</summary>
		public static void Make(MapKit k, IslandGenSettings s, List<string> done)
		{
			int want = Mathf.Clamp(s.Features, 0, Max);
			if (want == 0) return;
			System.Random r = k.Rnd;
			List<string> order = Kinds.OrderBy(x => r.Next()).ToList();
			// (the machinery after the first six; the random numbers drawn for six or fewer stay as they were)
			if (want > Kinds.Length) order.AddRange(MoreKinds.OrderBy(x => r.Next()));
			var made = new List<string>();
			var tools = new List<string>();
			for (int i = 0; i < want; i++)
			{
				string kind = order[i % order.Count];
				string what = One(k, s, kind, i, tools);
				if (what != null) made.Add(what);
			}
			// The tools, in a chest near the shore (where players land)
			if (tools.Count > 0)
			{
				Vector2? p = k.Find(k.Mid, s.Radius * 0.9f, (above, slope) => above > 0.6f && above < 4f && slope < 15f, 6f);
				// (no gentle shore - a steep island: anywhere dry, else its top; the tools open what was placed - the cage's bolt cutters are nowhere else)
				if (!p.HasValue) p = k.Find(k.Mid, s.Radius, MapKit.Dry, 0f) ?? k.Highest(k.Mid, s.Radius * 0.5f);
				if (p.HasValue) k.Chest("Loot_Chest", p.Value, "Explorer's chest", string.Join(";", tools.Distinct().Select(t => t + "*1").ToArray()) + ";Rope*3",
					"", 0f);
			}
			if (made.Count > 0) done.Add(string.Join(", ", made.ToArray()) + (made.Count < want ? " (" + (want - made.Count) + " more found no place)" : ""));
		}

		static string One(MapKit k, IslandGenSettings s, string kind, int n, List<string> tools)
		{
			System.Random r = k.Rnd;
			Vector2? dry = k.Find(k.Mid, s.Radius * 0.7f, MapKit.Dry, 10f);
			switch (kind)
			{
				case "vines":
				{
					if (!dry.HasValue) return null;
					string chest = "vinecache" + n;
					var cp = new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Treasure") }, { ObjectProps.NoteTitle, "Behind the vines" }, { BehaviourProps.Name, chest }, { BehaviourProps.Hidden, "1" } };
					k.Add("Loot_Chest", k.At(dry.Value), (float)r.NextDouble() * 360f, cp, 3f);
					Dictionary<string, string> vp = ObjectProps.Defaults(ContentCatalog.MacheteVines);
					vp[BehaviourProps.EventKey("use")] = "hide|\nshow|" + chest + "\nmessage||The machete bites through the vines. Something was hidden behind them.";
					k.Add(ContentCatalog.MacheteVines, k.At(dry.Value + new Vector2(1.6f, 0f)), (float)r.NextDouble() * 360f, vp, 0f);
					tools.AddRange(RaftFeatures.ToolsOf(ContentCatalog.MacheteVines));
					return "a cache behind vines";
				}
				case "treasure":
				{
					int put = 0;
					for (int i = 0; i < 2; i++)
					{
						Vector2? p = k.Find(k.Mid, s.Radius * 0.75f, MapKit.Dry, 8f);
						if (!p.HasValue) continue;
						k.Add(ContentCatalog.BuriedTreasure, k.At(p.Value), 0f, new Dictionary<string, string> { { ObjectProps.TreasureKind, "0" } }, 0f);
						put++;
					}
					if (put == 0) return null;
					tools.AddRange(RaftFeatures.ToolsOf(ContentCatalog.BuriedTreasure));
					return put + " buried treasure" + (put == 1 ? "" : "s");
				}
				case "zipline":
				{
					// (from the top down to the beach: at least 25 m long, falling at least 6 m)
					Vector2 top = k.Highest(k.Mid, s.Radius * 0.5f);
					float topY = k.Ground(top);
					Vector2? beach = null;
					for (int i = 0; i < 40 && beach == null; i++)
					{
						Vector2? p = k.Find(top, s.Radius * 1.1f, (above, slope) => above > 0.4f && above < 2.5f && slope < 18f, 0f);
						if (p.HasValue && (p.Value - top).magnitude > 25f && (p.Value - top).magnitude < 90f && topY - k.Ground(p.Value) > 6f) beach = p;
					}
					if (!beach.HasValue) return null;
					Vector3 far = k.At(beach.Value, 0.2f);
					var zp = new Dictionary<string, string> { { ZiplineEnds.ZipTo, ZiplineEnds.Text(far) }, { BehaviourProps.Name, "zipline" + n } };
					k.Add("ZiplinePath_Landmark", k.At(top, 3f), 0f, zp, 4f);
					tools.AddRange(RaftFeatures.ToolsOf("ZiplinePath_Landmark"));
					return "a zipline down to the beach";
				}
				case "code":
				{
					Vector2? other = k.Find(k.Mid, s.Radius * 0.8f, MapKit.Dry, 15f);
					if (!dry.HasValue || !other.HasValue) return null;
					string code = (1000 + r.Next(9000)).ToString();
					string box = "strongbox" + n;
					var bp = new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Treasure") + ";" + MapKit.Loot("Metal").Split(';').First() }, { ObjectProps.NoteTitle, "Strongbox" }, { BehaviourProps.Name, box }, { BehaviourProps.Hidden, "1" } };
					k.Add("Loot_ChestLarge", k.At(dry.Value), (float)r.NextDouble() * 360f, bp, 3f);
					var pp = new Dictionary<string, string>
					{
						{ BehaviourProps.Use, "Enter the code" }, { CodeLock.Code, code },
						{ BehaviourProps.EventKey("use"), "show|" + box + "\nmessage||The panel beeps and a hatch in the ground slides open." },
					};
					k.Add("RT_PowerBox", k.At(dry.Value + new Vector2(0f, 1.8f)), (float)r.NextDouble() * 360f, pp, 0f);
					k.Note("Note_Papers", other.Value, "Scribbled numbers", "Don't forget it this time: " + code + ". The panel by the strongbox.");
					return "a strongbox behind a code panel (" + code + ")";
				}
				case "power":
				{
					// A generator, a radio beside it that works once it runs, a cache the radio's voice leads to (shown), and
					// the generator's missing part in a toolbox elsewhere
					Vector2? other = k.Find(k.Mid, s.Radius * 0.8f, MapKit.Dry, 15f);
					if (!dry.HasValue || !other.HasValue) return null;
					string cache = "radiocache" + n;
					k.Add("Loot_Chest", k.At(dry.Value + new Vector2(-2.5f, 1.5f)), (float)r.NextDouble() * 360f,
						new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Treasure") }, { ObjectProps.NoteTitle, "Supply drop" }, { BehaviourProps.Name, cache }, { BehaviourProps.Hidden, "1" } }, 3f);
					k.Add(GeneratorPiece, k.At(dry.Value), (float)r.NextDouble() * 360f, ObjectProps.Defaults(GeneratorPiece), 3f);
					Dictionary<string, string> rp = ObjectProps.Defaults(RadioPiece);
					rp[BehaviourProps.EventKey("use")] = ObjectProps.Get(rp, BehaviourProps.EventKey("use")) + "\nshow|" + cache + "\nmessage||The voice gives a place on this island. A supply drop!";
					k.Add(RadioPiece, k.At(dry.Value + new Vector2(2.2f, 0f)), (float)r.NextDouble() * 360f, rp, 0f);
					k.Chest("Loot_Chest", other.Value, "Mechanic's toolbox", StoryItems.Ref(ReadyPieces.ItemOf(GeneratorPiece)) + "*1;" + MapKit.Loot("Metal").Split(';').First(), "A spare part for the generator.");
					return "a generator and a radio (the part in a toolbox)";
				}
				case "engine":
				{
					Vector2? other = k.Find(k.Mid, s.Radius * 0.8f, MapKit.Dry, 15f);
					if (!dry.HasValue || !other.HasValue) return null;
					string cache = "enginecache" + n;
					k.Add("Loot_ChestLarge", k.At(dry.Value + new Vector2(0f, -2.6f)), (float)r.NextDouble() * 360f,
						new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Metal") }, { ObjectProps.NoteTitle, "Engine room locker" }, { BehaviourProps.Name, cache }, { BehaviourProps.Hidden, "1" } }, 3f);
					Dictionary<string, string> ep = ObjectProps.Defaults(EnginePiece);
					ep[BehaviourProps.EventKey("use")] = ObjectProps.Get(ep, BehaviourProps.EventKey("use")) + "\nshow|" + cache;
					k.Add(EnginePiece, k.At(dry.Value), (float)r.NextDouble() * 360f, ep, 0f);
					k.Chest("Loot_Chest", other.Value, "Fuel crate", StoryItems.Ref(ReadyPieces.ItemOf(EnginePiece)) + "*1", "Fuel for the engine.");
					return "an engine to start (the fuel in a crate)";
				}
				case "cage":
				{
					if (!dry.HasValue) return null;
					string chest = "cagechest" + n;
					k.Add("Loot_Chest", k.At(dry.Value), (float)r.NextDouble() * 360f,
						new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Treasure") }, { ObjectProps.NoteTitle, "Caged chest" }, { BehaviourProps.Name, chest }, { BehaviourProps.Hidden, "1" } }, 3f);
					Dictionary<string, string> cp = ObjectProps.Defaults(CagePiece);
					cp[BehaviourProps.EventKey("use")] = ObjectProps.Get(cp, BehaviourProps.EventKey("use")) + "\nshow|" + chest;
					k.Add(CagePiece, k.At(dry.Value), (float)r.NextDouble() * 360f, cp, 0f);
					tools.Add(StoryItems.Ref(ReadyPieces.ItemOf(CagePiece)));
					return "a chest in a cage (bolt cutters)";
				}
				case "lift":
				{
					// At the foot of a cliff: ground 3.5 m away is 4-14 m higher and level enough to step off onto
					for (int i = 0; i < 60; i++)
					{
						Vector2? p = k.Find(k.Mid, s.Radius * 0.85f, (above, slope) => above > 0.6f && slope < 22f, 6f);
						if (!p.HasValue) break;
						// (3.5 m out first, then a little further: a generated island's slopes are rarely sheer - 3 m up is a lift's worth)
						for (int d = 0; d < 32; d++)
						{
							float a = (d % 8) * Mathf.PI / 4f, reach = 3.5f + (d / 8) * 1.5f;
							Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
							Vector2 q = p.Value + dir * reach;
							// (the lift stands 3.5 m before the high ground, so its top is beside it: step off there)
							Vector2 foot = q - dir * 3.5f;
							float rise = k.Ground(q) - k.Ground(foot);
							if (rise < (d < 8 ? 4f : 3f) || rise > 14f || k.Slope(q) > 25f || k.Slope(foot) > 30f) continue;
							Dictionary<string, string> lp = ObjectProps.Defaults(LiftPiece);
							lp[BehaviourProps.Move] = BehaviourProps.OffsetText(new Vector3(0f, rise + 0.3f, 0f));
							lp[BehaviourProps.MoveTime] = Mathf.Clamp(rise * 0.6f, 3f, 9f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
							k.Add(LiftPiece, k.At(foot), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, lp, 2f); // (facing the high ground: a yaw faces (sin, cos), as the jetty's)
							return "a lift up a cliff (" + rise.ToString("F0") + " m)";
						}
					}
					// (no cliff: the steepest rise found, at least 2.5 m - a lift up a slope players could also climb)
					float best = 2.5f; Vector2 bestFoot = Vector2.zero; float bestAngle = 0f; bool found = false; float seen = 0f; int tried = 0;
					for (int i = 0; i < 200; i++)
					{
						Vector2? p = k.Find(k.Mid, s.Radius * 0.85f, (above, slope) => above > 0.6f && slope < 30f, 2f);
						if (!p.HasValue) break;
						for (int d = 0; d < 8; d++)
						{
							float a = d * Mathf.PI / 4f;
							Vector2 q = p.Value + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f;
							float rise = k.Ground(q) - k.Ground(p.Value);
							seen = Mathf.Max(seen, rise); tried++;
							// (the top's slope looked at a little past the edge: where players step off)
							Vector2 off = q + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.5f;
							if (rise > best && rise <= 14f && k.Ground(off) >= k.Ground(q) - 0.5f) { best = rise; bestFoot = p.Value; bestAngle = a; found = true; }
						}
					}
					if (!found) { Debug.Log("[CUSTOM ISLANDS] [gen] No place for a lift: the steepest rise in 3.5 m was " + seen.ToString("F1") + " m (" + tried + " tries)"); return null; }
					Dictionary<string, string> fp = ObjectProps.Defaults(LiftPiece);
					fp[BehaviourProps.Move] = BehaviourProps.OffsetText(new Vector3(0f, best + 0.3f, 0f));
					fp[BehaviourProps.MoveTime] = Mathf.Clamp(best * 0.6f, 3f, 9f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
					k.Add(LiftPiece, k.At(bestFoot), 90f - bestAngle * Mathf.Rad2Deg, fp, 2f); // (facing the rise: bestAngle is from x towards z)
					return "a lift up a slope (" + best.ToString("F0") + " m)";
				}
				case "hives":
				{
					int put = 0;
					Vector2 grove = dry ?? k.Mid;
					for (int i = 0; i < 3; i++)
					{
						Vector2? p = k.Find(grove, 12f, MapKit.Dry, 3f);
						if (!p.HasValue) continue;
						var hp = new Dictionary<string, string> { { ObjectProps.LootItems, ContentCatalog.PresetLoot(new[] { ContentCatalog.WildHiveLoot }) }, { ObjectProps.NoteTitle, "Wild beehive" } };
						k.Add(ContentCatalog.WildHive, k.At(p.Value), (float)r.NextDouble() * 360f, hp, 2f);
						put++;
					}
					return put > 0 ? "a grove of " + put + " wild beehives" : null;
				}
				default:
				{
					int put = 0;
					for (int i = 0; i < 5; i++)
					{
						Vector2? p = k.Find(dry ?? k.Mid, 14f, MapKit.Dry, 2.5f);
						if (!p.HasValue) continue;
						k.Add("Pickup_Landmark_DirtPickup", k.At(p.Value), (float)r.NextDouble() * 360f, null, 1f);
						put++;
					}
					if (put == 0) return null;
					tools.AddRange(RaftFeatures.ToolsOf("Pickup_Landmark_DirtPickup"));
					return put + " dirt spots for the shovel";
				}
			}
		}
	}
}

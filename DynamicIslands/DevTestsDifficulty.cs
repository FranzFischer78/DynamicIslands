using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Tests for the monster difficulty (MonsterDifficulty): its rules, the New Game box, a world, two players.</summary>
	public static partial class DevTests
	{
		#region Rules (anywhere)

		[ConsoleCommand(name: "CIMonsterUnit", docs: "Dev, anywhere: the monster difficulty's rules - the five levels, names, texts, which animals are monsters, how each kind of hit changes, the world file line, the last choice")]
		public static void MonsterUnit()
		{
			bool ok = true;
			int before = MonsterDifficulty.Current;
			try
			{
				string[] names = MonsterDifficulty.Names;
				Check(ref ok, names.SequenceEqual(new[] { "Timid", "Normal", "Fierce", "Savage", "Nightmare" }), "five levels: " + string.Join(", ", names));
				Check(ref ok, MonsterDifficulty.Factors.SequenceEqual(new[] { 0.75f, 1f, 1.25f, 1.5f, 2f }), "health and damage x" + string.Join(" / x", MonsterDifficulty.Factors.Select(f => f.ToString(CultureInfo.InvariantCulture)).ToArray()));
				string[] want = { "25% less health and deal 25% less damage", "as Raft made them", "25% more health and deal 25% more damage", "50% more health and deal 50% more damage", "twice the health and deal twice the damage" };
				for (int i = 0; i < names.Length; i++)
					Check(ref ok, MonsterDifficulty.Descriptions[i].Contains(want[i]), names[i] + " says what it does: \"" + MonsterDifficulty.Descriptions[i] + "\"");
				string help = MonsterDifficulty.HelpText;
				Check(ref ok, names.All(n => help.Contains(n + " (")) && help.Contains("Bruce") && help.Contains("game mode") && help.Contains("Puffer fish") && help.Contains("your raft stay as in Raft"),
					"the ? lists every level, the monsters, the game mode, and that puffer fish damage and Bruce and the raft stay Raft's (" + help.Length + " characters)");

				// Every Harmony patch is in place (RML patched them when the mod loaded)
				var patched = new Dictionary<System.Reflection.MethodBase, string>
				{
					{ HarmonyLib.AccessTools.Method(typeof(Network_Host), "DamageEntity"), "MonsterDamagePatch" },
					{ HarmonyLib.AccessTools.Method(typeof(AI_State_PufferFish_Explode), "DamageNearbyPlayers"), "PufferFishDamagePatch" },
					{ HarmonyLib.AccessTools.Method(typeof(PufferFishParticleCloud), "Tick"), "PufferFishDamagePatch" },
					{ HarmonyLib.AccessTools.Method(typeof(NewGameBox), "Open"), "NewWorldRulesBox" },
					{ HarmonyLib.AccessTools.Method(typeof(NewGameBox), "Button_CreateNewGame"), "NewWorldRulesChoice" }
				};
				List<string> missing = patched.Where(kv =>
				{
					HarmonyLib.Patches info = kv.Key != null ? HarmonyLib.Harmony.GetPatchInfo(kv.Key) : null;
					return info == null || !info.Prefixes.Concat(info.Postfixes).Any(p => p.PatchMethod.DeclaringType.Name == kv.Value);
				}).Select(kv => kv.Value).ToList();
				Check(ref ok, missing.Count == 0, "the " + patched.Count + " Harmony patches are in place (hits, puffer fish, the New Game box)" + (missing.Count > 0 ? ": MISSING " + string.Join(", ", missing.ToArray()) : ""));
				// ... and none on Bruce and the raft: his bites on it, how often he comes for it, how soon the next shark comes (the user wants Raft's)
				var untouched = new[] { HarmonyLib.AccessTools.Method(typeof(AI_State_Attack_Block_Shark), "DealDamageToBlock"),
					HarmonyLib.AccessTools.PropertyGetter(typeof(AI_StateMachine_Shark), "SearchBlockInterval"), HarmonyLib.AccessTools.Method(typeof(AI_State_Decay_Shark), "CheckToSpawnOnDecay") };
				List<string> ours = untouched.Where(m => m != null && HarmonyLib.Harmony.GetPatchInfo(m) != null &&
					HarmonyLib.Harmony.GetPatchInfo(m).Prefixes.Concat(HarmonyLib.Harmony.GetPatchInfo(m).Postfixes).Any(p => p.PatchMethod.DeclaringType.Namespace == typeof(MonsterDifficulty).Namespace && p.PatchMethod.DeclaringType.Name.StartsWith("Monster")))
					.Select(m => m.Name).ToList();
				Check(ref ok, ours.Count == 0, "Bruce's raft bites, how often he comes for the raft and his respawn are Raft's (no patch of the difficulty on them)" + (ours.Count > 0 ? ": PATCHED " + string.Join(", ", ours.ToArray()) : ""));

				// Names, the words first asked for, numbers
				var parse = new Dictionary<string, int> { { "timid", 0 }, { "NORMAL", 1 }, { "Fierce", 2 }, { "savage", 3 }, { "nightmare", 4 }, { "easy", 0 }, { "Moderate", 2 }, { "hard", 3 }, { "Impossible", 4 }, { "3", 3 }, { "7", -1 }, { "brutal", -1 }, { "", -1 } };
				List<string> wrong = parse.Where(kv => MonsterDifficulty.Parse(kv.Key) != kv.Value).Select(kv => "'" + kv.Key + "' -> " + MonsterDifficulty.Parse(kv.Key)).ToList();
				Check(ref ok, wrong.Count == 0, "names, Easy/Moderate/Hard/Impossible and numbers are understood" + (wrong.Count > 0 ? ": " + string.Join(", ", wrong.ToArray()) : ""));

				// Which of Raft's animals are monsters - the same ones that give EXP (AU69: one list; Raft's pig charges at
				// players and the Tangaroa roach attacks, Utopia's butler bots don't)
				var monsters = new[] { AI_NetworkBehaviourType.Shark, AI_NetworkBehaviourType.Boar, AI_NetworkBehaviourType.Pig, AI_NetworkBehaviourType.Bear, AI_NetworkBehaviourType.MamaBear, AI_NetworkBehaviourType.StoneBird,
					AI_NetworkBehaviourType.StoneBird_Caravan, AI_NetworkBehaviourType.PufferFish, AI_NetworkBehaviourType.Rat, AI_NetworkBehaviourType.Rat_Tangaroa, AI_NetworkBehaviourType.Roach, AI_NetworkBehaviourType.BugSwarm_Bee,
					AI_NetworkBehaviourType.Boss_Varuna, AI_NetworkBehaviourType.AnglerFish, AI_NetworkBehaviourType.PolarBear, AI_NetworkBehaviourType.Hyena, AI_NetworkBehaviourType.HyenaBoss };
				var all = Enum.GetValues(typeof(AI_NetworkBehaviourType)).Cast<AI_NetworkBehaviourType>().ToList();
				List<string> mixed = all.Where(t => MonsterDifficulty.IsMonsterType(t) != monsters.Contains(t)).Select(t => t.ToString()).ToList();
				Check(ref ok, mixed.Count == 0, monsters.Length + " kinds are monsters, the other " + (all.Count - monsters.Length) + " (llama, goat, chicken, butler bots, sea life, people) aren't" + (mixed.Count > 0 ? ": wrong " + string.Join(", ", mixed.ToArray()) : ""));

				// Every kind of hit, at every level (stand-in entities: a player, a monster of Raft's without an animal brain, a thing)
				var holder = new GameObject("CIMonsterUnit");
				holder.SetActive(false); // (so Raft's components added below never start)
				try
				{
					Func<string, EntityType, Network_Entity> make = (n, type) =>
					{
						var go = new GameObject(n);
						go.transform.SetParent(holder.transform, false);
						Network_Entity ne = go.AddComponent<Network_Entity>();
						ne.entityType = type;
						return ne;
					};
					Network_Entity player = make("player", EntityType.Player), enemy = make("enemy", EntityType.Enemy), thing = make("thing", EntityType.None);
					Check(ref ok, MonsterDifficulty.IsMonster(enemy) && !MonsterDifficulty.IsMonster(player) && !MonsterDifficulty.IsMonster(thing), "an enemy is a monster, a player and a thing aren't");
					for (int l = 0; l < names.Length; l++)
					{
						MonsterDifficulty.Current = l;
						float f = MonsterDifficulty.Factors[l];
						var cases = new[]
						{
							new { what = "a monster's bite on a player", e = player, t = EntityType.Enemy, d = 20f, want = 20f * f },
							new { what = "a fall", e = player, t = EntityType.FallDamage, d = 20f, want = 20f },
							new { what = "fire and the like", e = player, t = EntityType.Environment, d = 20f, want = 20f },
							new { what = "another player's hit", e = player, t = EntityType.Player, d = 20f, want = 20f },
							new { what = "a player's spear on a monster", e = enemy, t = EntityType.Player, d = 20f, want = 20f / f },
							new { what = "anything else on a monster", e = enemy, t = EntityType.Environment, d = 20f, want = 20f / f },
							new { what = "a hit on a thing", e = thing, t = EntityType.Player, d = 20f, want = 20f },
							new { what = "poison and other damage over time (no inflictor) on a player", e = player, t = EntityType.None, d = 20f, want = 20f },
							new { what = "a kill hit on a monster", e = enemy, t = EntityType.Player, d = MonsterDifficulty.KillDamage, want = MonsterDifficulty.KillDamage },
							new { what = "a kill hit on a player", e = player, t = EntityType.Enemy, d = 99999f, want = 99999f },
							new { what = "nothing", e = enemy, t = EntityType.Player, d = 0f, want = 0f }
						};
						List<string> bad = cases.Where(c => Mathf.Abs(MonsterDifficulty.Scale(c.e, c.d, c.t) - c.want) > 0.001f)
							.Select(c => c.what + " " + c.d + " -> " + MonsterDifficulty.Scale(c.e, c.d, c.t) + " (want " + c.want + ")").ToList();
						// (a puffer fish's explosion and cloud hit as an Enemy too, but keep Raft's damage)
						MonsterDifficulty.PufferFishHurting = true;
						float puffer = MonsterDifficulty.Scale(player, 20f, EntityType.Enemy);
						MonsterDifficulty.PufferFishHurting = false;
						if (Mathf.Abs(puffer - 20f) > 0.001f) bad.Add("a puffer fish's explosion 20 -> " + puffer + " (want 20)");
						Check(ref ok, bad.Count == 0, names[l] + ": bites on players x" + f + ", hits on monsters /" + f + ", puffer fish, poison and the rest unchanged" + (bad.Count > 0 ? ": " + string.Join("; ", bad.ToArray()) : ""));
					}
				}
				finally { UnityEngine.Object.Destroy(holder); }

				// The world file
				MonsterDifficulty.Current = MonsterDifficulty.Normal;
				Check(ref ok, !MonsterDifficulty.WriteLines().Any() && !MonsterDifficulty.HasState, "Normal writes nothing to the world file");
				MonsterDifficulty.Current = MonsterDifficulty.Savage;
				string line = MonsterDifficulty.WriteLines().FirstOrDefault();
				Check(ref ok, line == "@monsters=savage" && MonsterDifficulty.HasState, "Savage is kept as " + line);
				MonsterDifficulty.Current = MonsterDifficulty.Normal;
				bool read = MonsterDifficulty.ReadLine("monsters", "savage");
				Check(ref ok, read && MonsterDifficulty.Current == MonsterDifficulty.Savage, "and read back: " + MonsterDifficulty.Name(MonsterDifficulty.Current));
				Check(ref ok, MonsterDifficulty.ReadLine("Monsters", "impossible") && MonsterDifficulty.Current == MonsterDifficulty.Nightmare, "'impossible' in a hand-edited file is Nightmare");
				Check(ref ok, !MonsterDifficulty.ReadLine("plan", "x") && MonsterDifficulty.Current == MonsterDifficulty.Nightmare, "other lines aren't read as the difficulty");
				IslandNetMessage msg = WorldRules.Message();
				Check(ref ok, msg.Kind == IslandNetMessage.WorldRules && msg.Kind == 15 && msg.Index == MonsterDifficulty.Nightmare && msg.Count == BuildCost.Current,
					"the message to other players (and each who joins): kind " + msg.Kind + ", level " + msg.Index + ", build cost " + msg.Count);

				// The last choice in the New Game box
				string path = Path.Combine(DynamicIslands.assetpath, WorldRules.DefaultFileName);
				string saved = File.Exists(path) ? File.ReadAllText(path) : null;
				try
				{
					MonsterDifficulty.SaveDefault(MonsterDifficulty.Fierce);
					Check(ref ok, MonsterDifficulty.Default == MonsterDifficulty.Fierce, "the last choice is remembered (" + WorldRules.DefaultFileName + ": " + string.Join(" | ", File.ReadAllLines(path).Skip(1).ToArray()) + ")");
					File.Delete(path);
					Check(ref ok, MonsterDifficulty.Default == MonsterDifficulty.Normal, "without one it is Normal");
				}
				finally { if (saved != null) File.WriteAllText(path, saved); else if (File.Exists(path)) File.Delete(path); }
			}
			catch (Exception e) { Check(ref ok, false, "no exception: " + e); }
			finally { MonsterDifficulty.Current = before; }
			if (ok) Log("PASS: monster difficulty rules"); else Fail("monster difficulty rules");
		}

		#endregion

		#region The build cost's rules (anywhere)

		[ConsoleCommand(name: "CIBuildCostUnit", docs: "Dev, anywhere: the build cost's rules - amounts rounded to the nearest (never below Raft's) at every percent, set from Raft's own numbers (never on top of an earlier change), back to Raft's at 0, the texts, the world file line, the last choice")]
		public static void BuildCostUnit()
		{
			bool ok = true;
			int before = BuildCost.Current, monstersBefore = MonsterDifficulty.Current;
			try
			{
				// Rounded to the nearest (a half up), never below Raft's own, at every percent and amount (AU41: rounded up, +5 %
				// doubled every 1-item cost)
				var table = new[] { new[] { 1, 50, 2 }, new[] { 2, 50, 3 }, new[] { 3, 50, 5 }, new[] { 4, 50, 6 }, new[] { 5, 50, 8 }, new[] { 1, 5, 1 }, new[] { 1, 45, 1 }, new[] { 10, 5, 11 },
					new[] { 20, 5, 21 }, new[] { 7, 100, 14 }, new[] { 3, 0, 3 }, new[] { 0, 50, 0 }, new[] { 6, 50, 9 }, new[] { 10, 25, 13 }, new[] { 3, 10, 3 }, new[] { 2, 25, 3 } };
				List<string> wrong = table.Where(r => BuildCost.Cost(r[0], r[1]) != r[2]).Select(r => r[0] + " at " + r[1] + "% -> " + BuildCost.Cost(r[0], r[1]) + " (want " + r[2] + ")").ToList();
				for (int a = 1; a <= 40; a++)
					for (int p = 0; p <= BuildCost.Max; p += BuildCost.Step)
					{
						float exact = a * (100 + p) / 100f;
						int want = Mathf.Max(a, Mathf.FloorToInt(exact + 0.5f + 0.0001f));
						int c = BuildCost.Cost(a, p);
						if (c != want) wrong.Add(a + " at " + p + "% -> " + c + " (exactly " + exact + ", want " + want + ")");
					}
				Check(ref ok, wrong.Count == 0, "amounts at every percent are rounded to the nearest, never below Raft's (1 plank at 5% is 1, at 50% 2; 2 are 3, 3 are 5)" + (wrong.Count > 0 ? ": " + string.Join("; ", wrong.Take(8).ToArray()) : ""));

				// Set from Raft's own numbers: never on top of an earlier change, and back at 0
				var entries = new List<CostMultiple> { new CostMultiple { items = new Item_Base[0], amount = 1 }, new CostMultiple { items = new Item_Base[0], amount = 2 }, new CostMultiple { items = new Item_Base[0], amount = 3 } };
				Func<string> amounts = () => string.Join(" ", entries.Select(e => e.amount.ToString()).ToArray());
				BuildCost.ApplyTo(entries, 50);
				string once = amounts();
				BuildCost.ApplyTo(entries, 50);
				BuildCost.ApplyTo(entries, 50);
				string thrice = amounts();
				BuildCost.ApplyTo(entries, 100);
				string full = amounts();
				BuildCost.ApplyTo(entries, 0);
				string back = amounts();
				Check(ref ok, once == "2 3 5" && thrice == "2 3 5" && full == "2 4 6" && back == "1 2 3" && entries.All(e => BuildCost.OriginalOf(e) == e.amount),
					"1 2 3 at 50%: " + once + ", three times: " + thrice + " (never on top), at 100%: " + full + ", at 0%: " + back + " (Raft's own)");

				// The texts
				Check(ref ok, BuildCost.DescriptionFor(0).Contains("Raft") && BuildCost.DescriptionFor(50).Contains("50% more") && BuildCost.DescriptionFor(50).Contains("1 plank becomes 2") &&
					BuildCost.DescriptionFor(50).Contains("half of what it cost"), "what 0% and 50% say: \"" + BuildCost.DescriptionFor(0) + "\" / \"" + BuildCost.DescriptionFor(50) + "\"");
				string help = BuildCost.HelpText;
				Check(ref ok, help.Contains("rounded") && help.Contains("crafting menu") && help.Contains("join") && help.Contains("repairing") && help.Contains("half"),
					"the ? says rounded, half back when removing, repairs, the crafting menu stays, players who join get it (" + help.Length + " characters)");

				// The world file
				BuildCost.Current = 0;
				Check(ref ok, !BuildCost.WriteLines().Any() && !BuildCost.HasState, "0% writes nothing to the world file");
				BuildCost.Current = 40;
				string line = BuildCost.WriteLines().FirstOrDefault();
				Check(ref ok, line == "@buildcost=40" && BuildCost.HasState, "40% is kept as " + line);
				BuildCost.Current = 0;
				Check(ref ok, BuildCost.ReadLine("buildcost", "40") && BuildCost.Current == 40, "and read back: " + BuildCost.Current + "%");
				Check(ref ok, BuildCost.ReadLine("BuildCost", "150") && BuildCost.Current == 100, "150 in a hand-edited file is 100");
				Check(ref ok, !BuildCost.ReadLine("monsters", "savage") && BuildCost.Current == 100, "other lines aren't read as the build cost");
				// (after them: the cost blocks were placed at, from 25 (AU33), and the last world's regrow days, kept until the next world loads)
				Check(ref ok, WorldRules.ReadLine("buildcost", "25") && WorldRules.ReadLine("monsters", "fierce") && BuildCost.Current == 25 && MonsterDifficulty.Current == MonsterDifficulty.Fierce &&
					WorldRules.WriteLines().Take(2).SequenceEqual(new[] { "@monsters=fierce", "@buildcost=25" }) && WorldRules.WriteLines().Skip(2).All(l => l.StartsWith("@builtat=25|") || l.StartsWith("@regrow=")),
					"the world rules read and write both: " + string.Join(", ", WorldRules.WriteLines().ToArray()));

				// The last choice in the New Game box (next to the monster difficulty's)
				string path = Path.Combine(DynamicIslands.assetpath, WorldRules.DefaultFileName);
				string saved = File.Exists(path) ? File.ReadAllText(path) : null;
				try
				{
					MonsterDifficulty.SaveDefault(MonsterDifficulty.Savage);
					BuildCost.SaveDefault(35);
					Check(ref ok, BuildCost.Default == 35 && MonsterDifficulty.Default == MonsterDifficulty.Savage, "the last choices are remembered together (" + string.Join(" | ", File.ReadAllLines(path).Skip(1).ToArray()) + ")");
					File.Delete(path);
					Check(ref ok, BuildCost.Default == 0, "without one it is Raft's own");
				}
				finally { if (saved != null) File.WriteAllText(path, saved); else if (File.Exists(path)) File.Delete(path); }
			}
			catch (Exception e) { Check(ref ok, false, "no exception: " + e); }
			finally
			{
				BuildCost.Current = before;
				MonsterDifficulty.Current = monstersBefore;
				BuildCost.Refresh();
			}
			if (ok) Log("PASS: build cost rules"); else Fail("build cost rules");
		}

		#endregion

		#region The New Game box (main menu)

		[ConsoleCommand(name: "CIWorldRulesBox", docs: "Dev, main menu: the world rules in Raft's New Game box - the monster difficulty's five levels and texts, the build cost's 0-100 % in steps of 5 and its text, both ? helps, the layout (in the box, nothing on top, on the screen); screenshot. CIWorldRulesBox <level> <percent> leaves them chosen (for CINewWorld)")]
		public static void WorldRulesBox(string[] args)
		{
			StartTest(WorldRulesBoxRoutine(args != null && args.Length > 0 ? args[0] : null, args != null && args.Length > 1 ? args[1] : null));
		}

		static IEnumerator WorldRulesBoxRoutine(string chooseLevel, string choosePercent)
		{
			bool ok = true;
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(x => x.gameObject.scene.IsValid());
			if (box == null) { Fail("no New Game box (go to the main menu first)"); yield break; }
			int level = chooseLevel != null ? MonsterDifficulty.Parse(chooseLevel) : -1, percent = -1;
			if (chooseLevel != null && level < 0) { Fail("unknown level '" + chooseLevel + "'"); yield break; }
			if (choosePercent != null && (!int.TryParse(choosePercent.TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent) || percent < 0 || percent > BuildCost.Max)) { Fail("unknown build cost '" + choosePercent + "'"); yield break; }
			int levelBefore = NewWorldRulesBox.MonsterLevel, percentBefore = NewWorldRulesBox.BuildPercent;
			// (Raft's Normal tab: the last world's Peaceful or Creative would add their note to the texts)
			GameMode modeBefore = GameManager.GameMode;
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			GameModeValueManager.SelectCurrentGameMode(GameMode.Normal);
			yield return new WaitForSecondsRealtime(0.5f);
			// (in the World settings window, opened from the box's button as a player does)
			var rt = (RectTransform)box.transform;
			Check(ref ok, rt.Find(NewWorldRulesBox.PanelName) == null, "the New Game box is Raft's own: no world rules in it");
			if (WorldSettingsWindow.OpenButton != null) WorldSettingsWindow.OpenButton.onClick.Invoke();
			yield return null; yield return null;
			var panel = WorldSettingsWindow.Window != null ? WorldSettingsWindow.Window.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == NewWorldRulesBox.PanelName) : null;
			Check(ref ok, panel != null && panel.gameObject.activeInHierarchy, "the World settings window has the world rules");
			if (panel == null) { Fail("world rules in the New Game box"); yield break; }

			// The monster difficulty: five steps named under it, each level as a player drags the slider to it
			Slider s = NewWorldRulesBox.MonsterSlider;
			Check(ref ok, s != null && s.minValue == 0f && s.maxValue == 4f && s.wholeNumbers && s.interactable, "a monster slider with five steps");
			List<string> tickNames = panel.GetComponentsInChildren<Text>().Where(t => t.name.StartsWith("Tick_")).Select(t => t.text).ToList();
			Check(ref ok, tickNames.SequenceEqual(MonsterDifficulty.Names), "the five names under its steps: " + string.Join(", ", tickNames.ToArray()));
			for (int i = MonsterDifficulty.Names.Length - 1; i >= 0; i--)
			{
				s.value = i;
				yield return null;
				Text value = s.transform.parent.Find("Top/Value").GetComponent<Text>();
				Text tick = panel.GetComponentsInChildren<Text>().First(t => t.name == "Tick_" + MonsterDifficulty.Names[i]);
				Check(ref ok, NewWorldRulesBox.MonsterLevel == i && value.text.StartsWith(MonsterDifficulty.Names[i].ToUpperInvariant()) && NewWorldRulesBox.MonsterText == MonsterDifficulty.Descriptions[i] && tick.color == UIKit.Accent,
					MonsterDifficulty.Names[i] + ": " + value.text + " - \"" + NewWorldRulesBox.MonsterText + "\"");
			}
			// (Peaceful: a note - monsters can't hurt players there)
			s.value = MonsterDifficulty.Savage;
			GameModeValueManager.SelectCurrentGameMode(GameMode.Peaceful);
			yield return null; yield return null;
			string peaceful = NewWorldRulesBox.MonsterText;
			GameModeValueManager.SelectCurrentGameMode(GameMode.Normal);
			yield return null; yield return null;
			string normal = NewWorldRulesBox.MonsterText;
			Check(ref ok, peaceful.Contains("Peaceful") && peaceful.Contains("only their health") && normal == MonsterDifficulty.Descriptions[MonsterDifficulty.Savage], "Peaceful adds a note: \"" + peaceful + "\"");

			// The build cost: 0-100 % in steps of 5, each step as a player drags the slider to it
			Slider b = NewWorldRulesBox.BuildSlider;
			Check(ref ok, b != null && b.minValue == 0f && b.maxValue == BuildCost.Max / BuildCost.Step && b.wholeNumbers && b.interactable, "a build cost slider from 0 to 100% in steps of 5");
			var badSteps = new List<string>();
			for (int step = BuildCost.Max / BuildCost.Step; step >= 0; step--)
			{
				b.value = step;
				yield return null;
				int p = step * BuildCost.Step;
				Text value = b.transform.parent.Find("Top/Value").GetComponent<Text>();
				string want = p == 0 ? "RAFT'S OWN" : "+" + p + "%";
				if (NewWorldRulesBox.BuildPercent != p || value.text != want || NewWorldRulesBox.BuildText != BuildCost.DescriptionFor(p)) badSteps.Add(p + "%: " + value.text + " / " + NewWorldRulesBox.BuildPercent + " / \"" + NewWorldRulesBox.BuildText + "\"");
			}
			b.value = 10;
			yield return null;
			Check(ref ok, badSteps.Count == 0, "every step shows its percent and what it does (at 50%: \"" + NewWorldRulesBox.BuildText + "\")" + (badSteps.Count > 0 ? ": WRONG " + string.Join("; ", badSteps.ToArray()) : ""));

			// The two "?": every level; rounded and what players who join get
			List<UIKit.HelpMark> marks = panel.GetComponentsInChildren<UIKit.HelpMark>().ToList();
			var shown = new List<string>();
			foreach (UIKit.HelpMark m in marks)
			{
				m.Toggle();
				yield return null;
				shown.Add(UIKit.ShownHelp ?? "");
				if (UIKit.ShownHelp != null) m.Toggle();
			}
			Check(ref ok, marks.Count == 2 && MonsterDifficulty.Names.All(n => shown[0].Contains(n)) && shown[1].Contains("rounded") && shown[1].Contains("join"), "two ?: every monster level, and the build cost (rounded, players who join get it)");

			// The layout: in the World settings window, inside its frame, nothing else of it drawn on top; Raft's box its own
			Canvas.ForceUpdateCanvases();
			var background = rt.Find("BrownBackground") as RectTransform;
			RectTransform frame = WorldSettingsWindow.Window.Find("Panel") as RectTransform;
			Rect pr = NewWorldRulesBox.LocalRect(frame, panel), fr = frame.rect;
			Check(ref ok, fr.xMin <= pr.xMin + 0.5f && fr.xMax >= pr.xMax - 0.5f && fr.yMin <= pr.yMin + 0.5f && fr.yMax >= pr.yMax - 0.5f,
				"the panel is inside the window (" + pr.size.ToString("F0") + " in " + fr.size.ToString("F0") + ")");
			var overlaps = new List<string>();
			foreach (Graphic g in frame.GetComponentsInChildren<Graphic>(false))
			{
				// (its own parts, and what it sits in - the window's frame and column)
				if (!g.enabled || g.color.a < 0.05f || g.transform.IsChildOf(panel) || panel.IsChildOf(g.transform)) continue;
				LayoutElement deco = g.GetComponent<LayoutElement>();
				if (deco != null && deco.ignoreLayout) continue; // (decoration layers: the frame's grunge and border, a group's background)
				if (g is Text && ((Text)g).text.Trim().Length == 0) continue;
				Rect r = NewWorldRulesBox.LocalRect(frame, g.rectTransform);
				if (g is Text) { float h = Mathf.Min(r.height, ((Text)g).preferredHeight); if (((Text)g).alignment.ToString().StartsWith("Upper")) r.yMin = r.yMax - h; else if (((Text)g).alignment.ToString().StartsWith("Lower")) r.yMax = r.yMin + h; }
				if (r.xMin < pr.xMax - 1f && r.xMax > pr.xMin + 1f && r.yMin < pr.yMax - 1f && r.yMax > pr.yMin + 1f) overlaps.Add(g.name + " (" + g.transform.parent.name + ")");
			}
			Check(ref ok, overlaps.Count == 0, "nothing else is drawn on it" + (overlaps.Count > 0 ? ": " + string.Join(", ", overlaps.Distinct().ToArray()) : ""));
			// (Raft's box: everything in it on its background - the plan and the World settings button too)
			var outside = new List<string>();
			Rect bg = background != null ? NewWorldRulesBox.LocalRect(rt, background) : new Rect();
			foreach (Graphic g in rt.GetComponentsInChildren<Graphic>(false))
			{
				if (!g.enabled || g.color.a < 0.05f || (background != null && g.transform.IsChildOf(background))) continue;
				Rect r = NewWorldRulesBox.LocalRect(rt, g.rectTransform);
				if (background != null && (r.yMin < bg.yMin - 1f || r.xMin < bg.xMin - 1f || r.xMax > bg.xMax + 1f) && g.transform.parent != rt && !g.name.StartsWith("Gamepad")) outside.Add(g.name);
			}
			Check(ref ok, outside.Count == 0, "everything in the box is on its background" + (outside.Count > 0 ? ": " + string.Join(", ", outside.Distinct().ToArray()) : ""));
			// (and the panel's own texts fit their lines: none cut off)
			List<string> cut = panel.GetComponentsInChildren<Text>().Where(t => t.text.Length > 0 && t.verticalOverflow == VerticalWrapMode.Truncate && t.preferredHeight > t.rectTransform.rect.height + 1f)
				.Select(t => t.name + " (" + t.preferredHeight.ToString("F0") + " in " + t.rectTransform.rect.height.ToString("F0") + ")").ToList();
			Check(ref ok, cut.Count == 0, "the panel's texts fit" + (cut.Count > 0 ? ": CUT OFF " + string.Join(", ", cut.ToArray()) : ""));
			if (background != null)
			{
				var corners = new Vector3[4];
				background.GetWorldCorners(corners);
				Canvas canvas = background.GetComponentInParent<Canvas>();
				Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
				Vector2 lo = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), hi = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
				Check(ref ok, lo.x >= 0f && lo.y >= 0f && hi.x <= Screen.width && hi.y <= Screen.height, "the whole box is on the screen (" + lo.ToString("F0") + " - " + hi.ToString("F0") + " of " + Screen.width + " x " + Screen.height + ")");
			}
			Screenshot(new[] { "world_rules_box" });
			yield return new WaitForSecondsRealtime(1f);
			GameModeValueManager.SelectCurrentGameMode(modeBefore);

			if (chooseLevel != null)
			{
				NewWorldRulesBox.MonsterLevel = level;
				NewWorldRulesBox.BuildPercent = percent >= 0 ? percent : 0;
				Log("World rules for the next new world: monsters " + MonsterDifficulty.Describe(level) + ", build cost " + BuildCost.Describe(NewWorldRulesBox.BuildPercent));
			}
			else
			{
				NewWorldRulesBox.MonsterLevel = levelBefore;
				NewWorldRulesBox.BuildPercent = percentBefore;
				box.Button_Close();
			}
			WorldSettingsWindow.Close();
			if (ok) Log("PASS: world rules in the New Game box"); else Fail("world rules in the New Game box");
		}

		#endregion

		#region A world

		static string WorldFilePath { get { return Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), SaveAndLoad.WorldGuid + ".txt"); } }

		/// <summary>Build menu amounts that aren't what this percent of Raft's own gives (empty: all right).</summary>
		static List<string> WrongAmounts(int percent)
		{
			return BuildCost.Entries(BuildCost.Items).Where(c => c.amount != BuildCost.Cost(BuildCost.OriginalOf(c), percent))
				.Select(c => (c.items != null && c.items.Length > 0 && c.items[0] != null ? c.items[0].UniqueName : "?") + " " + c.amount + " (Raft " + BuildCost.OriginalOf(c) + ")").ToList();
		}

		[ConsoleCommand(name: "CIRulesCheck", docs: "Dev, either player: in a world, its rules are <level> and <percent> (player 2: as the host sent them) and every build menu amount is that percent of Raft's own, once; CIRulesCheck <level> <percent> file also checks the world file (host, after a save); CIRulesCheck menu: at the main menu every amount is Raft's own")]
		public static void RulesCheck(string[] args)
		{
			bool ok = true;
			string who = Raft_Network.IsHost ? "host" : "player 2";
			if (args != null && args.Length > 0 && args[0] == "menu")
			{
				if (LoadSceneManager.IsGameSceneLoaded) { Fail("run at the main menu"); return; }
				List<string> changed = WrongAmounts(0);
				Check(ref ok, BuildCost.Applied == 0 && changed.Count == 0, "at the main menu every build menu amount is Raft's own (" + BuildCost.Entries(BuildCost.Items).Count + " amounts" +
					(changed.Count > 0 ? "; CHANGED " + string.Join(", ", changed.Take(6).ToArray()) : "") + ")");
				if (ok) Log("PASS: world rules of the world"); else Fail("world rules of the world");
				return;
			}
			int level = args != null && args.Length > 0 ? MonsterDifficulty.Parse(args[0]) : -1, percent = -1;
			if (level < 0 || args.Length < 2 || !int.TryParse(args[1].TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)) { Fail("usage: CIRulesCheck <level> <percent> [file] | CIRulesCheck menu"); return; }
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world"); return; }
			Check(ref ok, MonsterDifficulty.Current == level, "this world's monsters: " + MonsterDifficulty.Describe(MonsterDifficulty.Current) + " (" + who + ")");
			List<string> wrong = WrongAmounts(percent);
			Check(ref ok, BuildCost.Current == percent && BuildCost.Applied == percent && BuildCost.Items.Count > 0 && wrong.Count == 0,
				"this world's build cost: " + BuildCost.Describe(BuildCost.Current) + ", on Raft's numbers " + BuildCost.Describe(BuildCost.Applied) + " (" + BuildCost.Items.Count + " build menu items, " +
				BuildCost.Entries(BuildCost.Items).Count + " amounts, each once" + (wrong.Count > 0 ? "; WRONG " + string.Join(", ", wrong.Take(6).ToArray()) : "") + ")");
			if (args.Length > 2 && args[2] == "file")
			{
				string[] lines = File.Exists(WorldFilePath) ? File.ReadAllLines(WorldFilePath) : new string[0];
				string m = lines.FirstOrDefault(l => l.StartsWith("@monsters=")), c = lines.FirstOrDefault(l => l.StartsWith("@buildcost="));
				Check(ref ok, (level == MonsterDifficulty.Normal ? m == null : m == "@monsters=" + MonsterDifficulty.Name(level).ToLowerInvariant()) && (percent == 0 ? c == null : c == "@buildcost=" + percent),
					"the world file says " + (m ?? "nothing about monsters (Normal)") + ", " + (c ?? "nothing about the build cost (Raft's own)"));
			}
			if (ok) Log("PASS: world rules of the world"); else Fail("world rules of the world");
		}

		/// <summary>What a hit through Network_Host.DamageEntity takes off, worked out the way Raft does it (the game mode, armour), with the difficulty on top.</summary>
		static float ExpectedDrop(Network_Entity e, float damage, EntityType inflictor, bool monster)
		{
			SO_GameModeValue gm = GameModeValueManager.GetCurrentGameModeValue();
			PlayerSpecificVariables pv = gm.playerSpecificVariables;
			float f = MonsterDifficulty.Factor, d = damage;
			if (damage < MonsterDifficulty.KillDamage)
			{
				if (e.entityType == EntityType.Player && inflictor == EntityType.Enemy) d *= f;
				else if (monster) d /= f;
			}
			if (e.entityType == EntityType.Enemy && pv.negateOutgoingPlayerDamage) d = 0f;
			if (e.entityType == EntityType.Player && inflictor == EntityType.Enemy) d *= pv.damageTakenMultiplier;
			else if (e.entityType == EntityType.Enemy && inflictor == EntityType.Player) d *= pv.outgoingDamageMultiplierPVE;
			var ps = e as PlayerStats;
			if (ps != null && (inflictor == EntityType.Enemy || inflictor == EntityType.Player) && ps.armorHandler != null && ps.armorHandler.DamageReduction != 0f)
				d = Mathf.FloorToInt(d * (1f - ps.armorHandler.DamageReduction));
			return d;
		}

		/// <summary>
		/// A hit through Raft's Network_Host.DamageEntity (as a weapon or a bite does it; tick: as Raft's buffs do it, without a
		/// hit transform); returns the health it took, and puts the health back.
		/// </summary>
		static float HitAndHeal(Network_Host net, Network_Entity e, float damage, EntityType inflictor, bool tick = false)
		{
			var ps = e as PlayerStats;
			float bonus = ps != null ? ps.stat_BonusHealth.statTarget.Value : 0f;
			if (ps != null) ps.stat_BonusHealth.statTarget.Value = 0f; // (bonus health would take the hit first)
			float h0 = e.stat_health.Value;
			net.DamageEntity(e, tick ? null : e.transform, damage, e.transform.position + Vector3.up, Vector3.up, inflictor, null);
			float drop = h0 - e.stat_health.Value;
			e.stat_health.Value = h0;
			if (ps != null) ps.stat_BonusHealth.statTarget.Value = bonus;
			return drop;
		}

		[ConsoleCommand(name: "CIMonsterWorld", docs: "Dev, in game (host): the monster difficulty in a world - which of Raft's animals are monsters, at every level a spear on warthogs, Bruce, a puffer fish and a chicken, a bite and a fall on the player, Bruce's raft bites and search time (Raft's), a poison tick (Raft's); a puffer fish explosion (Raft's); a real warthog (or shark) bite at Savage. CIMonsterWorld [keep]")]
		public static void MonsterWorld(string[] args)
		{
			StartTest(MonsterWorldRoutine(args != null && args.Contains("keep")));
		}

		static IEnumerator MonsterWorldRoutine(bool keep)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			Network_Host net = ComponentManager<Network_Host>.Value;
			Network_Host_Entities ents = ComponentManager<Network_Host_Entities>.Value;
			Network_Player player = RAPI.GetLocalPlayer();
			if (net == null || ents == null || player == null || player.Stats == null) { Fail("no Network_Host / player"); yield break; }
			bool ok = true;
			int levelBefore = MonsterDifficulty.Current;
			int exceptions = 0; string firstException = null;
			// (Unity's own "Screen position out of view frustum" is a camera drawing while Raft is minimised, as the tests run it - not the mod's)
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception || (type == LogType.Error && !msg.StartsWith("[CITEST]") && !msg.StartsWith("Screen position out of view frustum"))) { exceptions++; if (firstException == null) firstException = msg + " " + trace.Split('\n').FirstOrDefault(); } };
			Application.logMessageReceived += counter;

			// Raft's animals, by kind
			var prefabs = ents.AINetworkBehaviourPrefabs.Where(a => a != null).ToList();
			foreach (AI_NetworkBehaviour a in prefabs)
				Log("  " + a.behaviourType + ": " + (a.networkEntity != null ? a.networkEntity.entityType.ToString() : "no entity") + (MonsterDifficulty.IsMonsterType(a.behaviourType) ? ", a monster" : ""));
			var kinds = new HashSet<AI_NetworkBehaviourType>(prefabs.Select(a => a.behaviourType));
			Check(ref ok, new[] { AI_NetworkBehaviourType.Shark, AI_NetworkBehaviourType.Boar, AI_NetworkBehaviourType.Bear, AI_NetworkBehaviourType.StoneBird }.All(kinds.Contains) &&
				prefabs.Count(a => MonsterDifficulty.IsMonsterType(a.behaviourType)) >= 10 && prefabs.Any(a => a.behaviourType == AI_NetworkBehaviourType.Llama && !MonsterDifficulty.IsMonsterType(a.behaviourType)),
				prefabs.Count(a => MonsterDifficulty.IsMonsterType(a.behaviourType)) + " of Raft's " + prefabs.Count + " animal kinds are monsters (Bruce, warthogs, bears, screechers...), llamas aren't");

			// The creature island: two warthogs (x2 health from the builder), a chicken, a puffer fish
			string error;
			if (!MakeCreatureIsland(out error)) { Application.logMessageReceived -= counter; Fail(error); yield break; }
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(CreatureIsland), 390f);
			if (!spot.HasValue) { Application.logMessageReceived -= counter; Fail("no open sea near the raft for the test island"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile(CreatureIsland, spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Application.logMessageReceived -= counter; Fail("the creature island did not spawn"); yield break; }
			List<CreatureSpawnPoint> points = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).ToList();
			float t0 = Time.realtimeSinceStartup;
			while (points.Sum(p => p.Spawned.Count(a => a != null)) < 4 && Time.realtimeSinceStartup - t0 < 60f) yield return new WaitForSeconds(0.5f);
			yield return new WaitForSeconds(1f);
			Func<AI_NetworkBehaviourType, AI_NetworkBehaviour> first = t => points.Where(p => p.Kind.Type == t).SelectMany(p => p.Spawned).FirstOrDefault(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead);
			AI_NetworkBehaviour boar = first(AI_NetworkBehaviourType.Boar), chicken = first(AI_NetworkBehaviourType.Chicken), fish = first(AI_NetworkBehaviourType.PufferFish);
			AI_NetworkBehaviour shark = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a.behaviourType == AI_NetworkBehaviourType.Shark && a.networkEntity != null && !a.networkEntity.IsDead)
				.OrderBy(a => a.ObjectIndex).FirstOrDefault();
			Check(ref ok, boar != null && chicken != null && fish != null, "a warthog, a chicken and a puffer fish (" + (Time.realtimeSinceStartup - t0).ToString("F1") + " s); Bruce " + (shark != null ? "is here" : "isn't around"));
			var targets = new List<KeyValuePair<string, AI_NetworkBehaviour>>();
			if (boar != null) targets.Add(new KeyValuePair<string, AI_NetworkBehaviour>("the warthog", boar));
			if (fish != null) targets.Add(new KeyValuePair<string, AI_NetworkBehaviour>("the puffer fish", fish));
			if (shark != null) targets.Add(new KeyValuePair<string, AI_NetworkBehaviour>("Bruce", shark));
			Check(ref ok, targets.All(kv => MonsterDifficulty.IsMonster(kv.Value.networkEntity)) && (chicken == null || !MonsterDifficulty.IsMonster(chicken.networkEntity)) && !MonsterDifficulty.IsMonster(player.Stats),
				string.Join(", ", targets.Select(kv => kv.Key).ToArray()) + " are monsters; the chicken and the player aren't");

			// Bruce and the raft: his brain (how long he circles before he looks for a block to bite), his raft bite and a block
			// he could bite (not reinforced, not shark bait, healthy enough for four bites); Raft's poison (puffer fish)
			AI_StateMachine_Shark sm = shark != null ? shark.GetComponentInChildren<AI_StateMachine_Shark>(true) : null;
			AI_State_Attack_Block_Shark biteState = sm != null ? sm.biteRaftState : null;
			MonsterDifficulty.Set(MonsterDifficulty.Normal);
			float searchRaw = sm != null ? sm.SearchBlockInterval : -1f;
			Block block = null;
			if (biteState != null)
			{
				List<Block> near = null;
				try { near = biteState.GetAttackableBlocks(); } catch { }
				// (any block: its health is lifted for the four test bites and put back)
				block = (near ?? new List<Block>()).Concat(UnityEngine.Object.FindObjectsOfType<Block>())
					.FirstOrDefault(b => b != null && !(b is SharkBait) && !b.Reinforced && b.Health > 0);
			}
			SO_Buff poison = (BuffManager.allBuffAssets ?? new SO_Buff[0]).Concat(Resources.FindObjectsOfTypeAll<SO_Buff>()).FirstOrDefault(b => b != null && b.BuffType == BuffType.Poison);
			AI_State_Decay_Shark decay = shark != null ? shark.GetComponentInChildren<AI_State_Decay_Shark>(true) : null;
			Check(ref ok, sm != null && biteState != null && block != null && poison != null, "Bruce's brain (looks for the raft every " + searchRaw.ToString("0.#") + " s; the next shark " +
				(decay != null ? decay.decayRespawnTime.ToString("0") + " s after he has decayed" : "?") + ") and raft bite (" +
				(biteState != null ? biteState.attackBlockDamage.ToString() : "none") + "), a raft block he can bite (" + (block != null ? block.name + ", health " + block.Health : "none") + "), Raft's poison (" + (poison != null ? poison.name : "none") + ")");

			// At every level: a player's spear on each of them (12, or half its health when it has little: never a kill), a
			// monster's bite (10) and a fall (10) on the player; Bruce's search time and four of his bites on the raft block;
			// a tick of poison and one of something else
			KeepAlive(player);
			if (chicken != null) targets.Add(new KeyValuePair<string, AI_NetworkBehaviour>("the chicken", chicken));
			for (int l = 0; l < MonsterDifficulty.Names.Length; l++)
			{
				MonsterDifficulty.Set(l);
				var bad = new List<string>();
				var said = new List<string>();
				foreach (KeyValuePair<string, AI_NetworkBehaviour> kv in targets)
				{
					Network_Entity e = kv.Value.networkEntity;
					if (e == null || e.IsDead || e.IsInvurnerable) continue;
					bool monster = kv.Value != chicken;
					float hit = Mathf.Min(12f, e.stat_health.Value * 0.5f);
					float drop = HitAndHeal(net, e, hit, EntityType.Player), want = ExpectedDrop(e, hit, EntityType.Player, monster);
					said.Add(kv.Key + " " + hit.ToString("0.##") + " -> " + drop.ToString("0.##"));
					if (Mathf.Abs(drop - want) > 0.01f) bad.Add(kv.Key + " lost " + drop + " (want " + want + ")");
				}
				KeepAlive(player);
				float bite = HitAndHeal(net, player.Stats, 10f, EntityType.Enemy), biteWant = ExpectedDrop(player.Stats, 10f, EntityType.Enemy, false);
				float fall = HitAndHeal(net, player.Stats, 10f, EntityType.FallDamage), fallWant = ExpectedDrop(player.Stats, 10f, EntityType.FallDamage, false);
				said.Add("a bite on the player " + bite.ToString("0.##") + ", a fall " + fall.ToString("0.##"));
				if (Mathf.Abs(bite - biteWant) > 0.01f) bad.Add("the bite took " + bite + " (want " + biteWant + ")");
				if (Mathf.Abs(fall - fallWant) > 0.01f) bad.Add("the fall took " + fall + " (want " + fallWant + ")");
				if (sm != null)
				{
					// (as often as in Raft at every level)
					float search = sm.SearchBlockInterval;
					said.Add("Bruce looks for the raft every " + search.ToString("0.#") + " s");
					if (Mathf.Abs(search - searchRaw) > 0.01f) bad.Add("he looks every " + search + " s (want Raft's " + searchRaw + ")");
				}
				if (biteState != null && block != null)
				{
					bool baitBefore = biteState.isTargetBlockSharkBait;
					biteState.isTargetBlockSharkBait = false;
					// (Raft's bites at every level)
					int raw = biteState.attackBlockDamage, h0 = block.Health;
					block.SetHealth(1000);
					for (int b = 0; b < 4; b++) biteState.DealDamageToBlock(block);
					int lost = 1000 - block.Health;
					block.SetHealth(h0);
					biteState.isTargetBlockSharkBait = baitBefore;
					said.Add("4 raft bites " + lost);
					if (lost != 4 * raw || biteState.attackBlockDamage != raw)
						bad.Add("4 raft bites took " + lost + " (want Raft's " + (4 * raw) + "), the bite is " + biteState.attackBlockDamage + " afterwards (want " + raw + ")");
				}
				if (poison != null)
				{
					// (a tick of Raft's poison, as its buffs hit: no inflictor, no hit transform - Raft's 2 at every level)
					player.Stats.buffManager.ClearBuffs();
					player.Stats.buffManager.AddBuff(poison);
					float tick = HitAndHeal(net, player.Stats, 2f, EntityType.None, true);
					player.Stats.buffManager.ClearBuffs();
					said.Add("a tick of poison " + tick.ToString("0.##"));
					if (Mathf.Abs(tick - 2f) > 0.01f) bad.Add("poison took " + tick + " (want Raft's 2)");
				}
				Check(ref ok, bad.Count == 0 && said.Count > 1, MonsterDifficulty.Names[l] + ": hits take " + string.Join(", ", said.ToArray()) + (bad.Count > 0 ? " - WRONG: " + string.Join("; ", bad.ToArray()) : ""));
				yield return null;
			}
			KeepAlive(player);

			// A puffer fish's explosion (Raft's own DamageNearbyPlayers, the player in the water beside it) hurts the same at
			// Normal and at Nightmare
			AI_State_PufferFish_Explode explode = fish != null ? fish.GetComponentInChildren<AI_State_PufferFish_Explode>(true) : null;
			if (explode != null)
			{
				var blast = new List<float>();
				foreach (int l in new[] { MonsterDifficulty.Normal, MonsterDifficulty.Nightmare })
				{
					MonsterDifficulty.Set(l);
					CharacterController cc = player.PersonController.controller;
					cc.enabled = false;
					player.transform.position = fish.transform.position + new Vector3(1f, 0f, 0f);
					player.PersonController.SwitchControllerType(ControllerType.Water);
					cc.enabled = true;
					KeepAlive(player);
					player.Stats.stat_BonusHealth.statTarget.Value = 0f;
					float h0 = player.Stats.stat_health.Value;
					explode.DamageNearbyPlayers();
					blast.Add(h0 - player.Stats.stat_health.Value);
					player.Stats.stat_health.Value = h0;
					player.Stats.buffManager.ClearBuffs();
				}
				Check(ref ok, blast[0] > 0f && Mathf.Abs(blast[0] - blast[1]) < 0.01f, "a puffer fish's explosion takes " + blast[0].ToString("0.##") + " at Normal and " + blast[1].ToString("0.##") + " at Nightmare (Raft's " + explode.explosionDamage + ")");
				OnRaftCommand();
				yield return new WaitForSeconds(1f);
				KeepAlive(player);
			}
			else Check(ref ok, false, "the puffer fish's explosion (no puffer fish)");

			// A real bite at Savage: a warthog (the island's, x1.5 damage from its builder) or else Bruce
			MonsterDifficulty.Set(MonsterDifficulty.Savage);
			float taken = -1f, sentIn = 0f, sentOut = 0f;
			string by = null;
			// (only a hit the difficulty changed: a puffer fish's, left as Raft's, doesn't count)
			int changed = MonsterDifficulty.ChangedHits;
			Network_Entity.OnDamageTaken seen = (d, point, normal, from, local) =>
			{
				if (MonsterDifficulty.ChangedHits == changed) return;
				changed = MonsterDifficulty.ChangedHits;
				if (taken >= 0f || from != EntityType.Enemy) return;
				taken = d; sentIn = MonsterDifficulty.LastIn; sentOut = MonsterDifficulty.LastOut;
			};
			player.Stats.OnDamageTakenEvent += seen;
			player.Stats.stat_BonusHealth.statTarget.Value = 0f;
			float armour = player.Stats.armorHandler != null ? player.Stats.armorHandler.DamageReduction : 0f;
			if (boar != null && !boar.networkEntity.IsDead)
			{
				by = "a warthog";
				for (t0 = Time.realtimeSinceStartup; taken < 0f && Time.realtimeSinceStartup - t0 < 40f; )
				{
					PutPlayerNear(boar.transform, 1.2f);
					yield return new WaitForSeconds(2f);
				}
			}
			if (taken < 0f && shark != null && !shark.networkEntity.IsDead)
			{
				// (Bruce bites swimmers: in the water beside him)
				by = "Bruce";
				for (t0 = Time.realtimeSinceStartup; taken < 0f && Time.realtimeSinceStartup - t0 < 50f; )
				{
					CharacterController cc = player.PersonController.controller;
					cc.enabled = false;
					player.transform.position = new Vector3(shark.transform.position.x + 2f, Mathf.Min(shark.transform.position.y + 1f, -0.5f), shark.transform.position.z);
					player.PersonController.SwitchControllerType(ControllerType.Water);
					cc.enabled = true;
					KeepAlive(player);
					yield return new WaitForSeconds(3f);
				}
			}
			player.Stats.OnDamageTakenEvent -= seen;
			float wantTaken = armour != 0f ? Mathf.FloorToInt(sentIn * 1.5f * GameModeValueManager.GetCurrentGameModeValue().playerSpecificVariables.damageTakenMultiplier * (1f - armour))
				: sentIn * 1.5f * GameModeValueManager.GetCurrentGameModeValue().playerSpecificVariables.damageTakenMultiplier;
			Check(ref ok, taken > 0f && Mathf.Abs(sentOut - sentIn * 1.5f) < 0.01f && Mathf.Abs(taken - wantTaken) < 0.01f,
				"a real bite by " + (by ?? "nothing") + " at Savage: " + (taken > 0f ? "its " + sentIn.ToString("0.##") + " became " + sentOut.ToString("0.##") + ", the player lost " + taken.ToString("0.##") + " (want " + wantTaken.ToString("0.##") + ")" : "no bite came"));
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			KeepAlive(player);

			MonsterDifficulty.Set(levelBefore);
			Application.logMessageReceived -= counter;
			Check(ref ok, exceptions == 0, "no errors (" + exceptions + (firstException != null ? ", first: " + firstException : "") + ")");
			if (!keep)
			{
				IslandWorldState.RemoveIds(new[] { entry.Id }, true);
				File.Delete(IslandSpawner.PathFor(CreatureIsland));
			}
			if (ok) Log("PASS: monster difficulty in a world"); else Fail("monster difficulty in a world");
		}

		[ConsoleCommand(name: "CIModesDifficulty", docs: "Dev, in game (host): each of Raft's game modes (Normal, Hardcore, Easy, Peaceful, Creative) with each monster level - a monster's bite on the player through Raft's DamageEntity is the mode's damage taken times the level's factor (they stack), and where a mode takes no damage no level makes the bite hurt (Raft's Peaceful keeps damage taken: its monsters just don't attack). The mode and level are put back after (IW5)")]
		public static void ModesDifficultyCommand() { StartTest(ModesDifficultyRoutine()); }

		static IEnumerator ModesDifficultyRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host net = ComponentManager<Network_Host>.Value;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || player == null || net == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			GameMode modeBefore = GameManager.GameMode;
			int levelBefore = MonsterDifficulty.Current;
			int[] levels = { MonsterDifficulty.Timid, MonsterDifficulty.Normal, MonsterDifficulty.Nightmare };
			var normalBite = new Dictionary<int, float>();
			try
			{
				foreach (GameMode m in Enum.GetValues(typeof(GameMode)).Cast<GameMode>())
				{
					GameModeValueManager.SelectCurrentGameMode(m);
					yield return null;
					SO_GameModeValue gm = GameModeValueManager.GetCurrentGameModeValue();
					if (gm == null || gm.playerSpecificVariables == null) { Log("  (" + m + ": no game mode values)"); continue; }
					float taken = gm.playerSpecificVariables.damageTakenMultiplier;
					var said = new List<string>();
					foreach (int l in levels)
					{
						MonsterDifficulty.Current = l;
						KeepAlive(player);
						float bite = HitAndHeal(net, player.Stats, 10f, EntityType.Enemy), want = ExpectedDrop(player.Stats, 10f, EntityType.Enemy, false);
						said.Add(MonsterDifficulty.Name(l) + " " + bite.ToString("0.##"));
						if (Mathf.Abs(bite - want) > 0.01f) Check(ref ok, false, m + " at " + MonsterDifficulty.Name(l) + ": a bite of 10 took " + bite.ToString("0.##") + " (want " + want.ToString("0.##") + " = 10 x" + taken.ToString("0.##") + " x" + MonsterDifficulty.Factor.ToString("0.##") + ")");
						if (taken <= 0f && bite > 0f) Check(ref ok, false, m + " takes no damage, but at " + MonsterDifficulty.Name(l) + " a bite took " + bite.ToString("0.##"));
						if (l == MonsterDifficulty.Normal) normalBite[(int)m] = bite;
					}
					Log("  " + m + " (damage taken x" + taken.ToString("0.##") + "): " + string.Join(", ", said.ToArray()));
				}
				var modes = normalBite.Keys.Select(k => (GameMode)k).ToList();
				Check(ref ok, normalBite.Values.Distinct().Count() > 1, "the modes' bites differ at Normal (" + string.Join(", ", modes.Select(k => k + " " + normalBite[(int)k].ToString("0.##")).ToArray()) + ") - the mode really changed");
				GameMode peaceful;
				// (Peaceful: Raft keeps its damage taken - its monsters just don't attack - so a bite there follows the same rule as the others)
				if (Enum.TryParse("Peaceful", out peaceful) && normalBite.ContainsKey((int)peaceful)) Log("  Peaceful at Normal: a bite of 10 takes " + normalBite[(int)peaceful].ToString("0.##") + " (Raft's Peaceful has no attacks, not less damage)");
			}
			finally
			{
				GameModeValueManager.SelectCurrentGameMode(modeBefore);
				MonsterDifficulty.Current = levelBefore;
			}
			KeepAlive(player);
			Check(ref ok, GameManager.GameMode == modeBefore && MonsterDifficulty.Current == levelBefore, "the mode (" + GameManager.GameMode + ") and the level (" + MonsterDifficulty.Name(MonsterDifficulty.Current) + ") are put back");
			if (ok) Log("PASS: modes difficulty"); else Fail("modes difficulty");
		}

		#endregion

		#region Build cost in a world

		/// <summary>A block of the build menu to place in tests: not placed from the inventory, one of Raft's plain blocks, every cost with a material.</summary>
		static Item_Base TestBlockItem()
		{
			return BuildCost.Items.Where(i => i != null && i.settings_buildable != null && !i.settings_buildable.Placeable && i.settings_buildable.GetBlockPrefab(DPS.Default) != null &&
					i.settings_recipe != null && i.settings_recipe.NewCost != null && i.settings_recipe.NewCost.Length > 0 &&
					i.settings_recipe.NewCost.All(c => c != null && c.items != null && c.items.Length > 0 && c.items[0] != null))
				.OrderBy(i => i.UniqueName.Contains("Foundation") ? 0 : i.UniqueName.Contains("Floor") ? 1 : 2).FirstOrDefault();
		}

		static Dictionary<string, int> Counts(Network_Player player, IEnumerable<CostMultiple> cost)
		{
			var d = new Dictionary<string, int>();
			foreach (CostMultiple c in cost) { string n = c.items[0].UniqueName; d[n] = player.Inventory.GetItemCount(n); }
			return d;
		}

		/// <summary>Each material's amount in the cost, and what taking it (Raft's RemoveCostMultiple) or giving back half (Raft's refund) should change.</summary>
		static string CostText(IEnumerable<CostMultiple> cost) { return string.Join(" + ", cost.Select(c => c.amount + " " + c.items[0].UniqueName + " (Raft " + BuildCost.OriginalOf(c) + ")").ToArray()); }

		[ConsoleCommand(name: "CIBuildCostWorld", docs: "Dev, in game (host): the build cost in a world - Raft's build menu items, every amount at 0 / 50 / 100 / 35 %, never on top of an earlier change; a real block placed through Raft's BlockCreator takes that many % more materials, rounded, and removing it gives half of that back as Raft does; Raft's numbers again at 0")]
		public static void BuildCostWorld()
		{
			StartTest(BuildCostWorldRoutine());
		}

		static IEnumerator BuildCostWorldRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || player.BlockCreator == null) { Fail("no player / BlockCreator"); yield break; }
			bool ok = true;
			int before = BuildCost.Current;
			int exceptions = 0; string firstException = null;
			// (Unity's own "Screen position out of view frustum" is a camera drawing while Raft is minimised, as the tests run it - not the mod's)
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception || (type == LogType.Error && !msg.StartsWith("[CITEST]") && !msg.StartsWith("Screen position out of view frustum"))) { exceptions++; if (firstException == null) firstException = msg + " " + trace.Split('\n').FirstOrDefault(); } };
			Application.logMessageReceived += counter;

			// Raft's build menu (found once the percent is more than 0)
			BuildCost.Set(50);
			for (float t0 = Time.realtimeSinceStartup; BuildCost.Applied != 50 && Time.realtimeSinceStartup - t0 < 10f; ) yield return new WaitForSeconds(0.25f);
			IList<Item_Base> items = BuildCost.Items;
			List<CostMultiple> entries = BuildCost.Entries(items);
			Check(ref ok, items.Count >= 20 && entries.Count >= 20 && items.Any(i => i.UniqueName.Contains("Foundation")),
				items.Count + " build menu items (" + string.Join(", ", items.Take(10).Select(i => i.UniqueName).ToArray()) + "...), " + entries.Count + " amounts");

			// Every percent, twice in a row too: always from Raft's own numbers
			foreach (int p in new[] { 0, 50, 50, 100, 35, 35, 0 })
			{
				BuildCost.Set(p);
				List<string> wrong = WrongAmounts(p);
				Check(ref ok, BuildCost.Applied == p && wrong.Count == 0, BuildCost.Describe(p) + ": every amount is " + (p == 0 ? "Raft's own" : "Raft's + " + p + "%, rounded") +
					(wrong.Count > 0 ? " - WRONG " + string.Join(", ", wrong.Take(6).ToArray()) : ""));
				yield return null;
			}

			// Only the build menu: every other recipe (the crafting menu, the research table's) keeps Raft's numbers at +100 %
			var buildEntries = new HashSet<CostMultiple>(BuildCost.Entries(BuildCost.Items));
			var others = new List<KeyValuePair<CostMultiple, int>>();
			string otherName = null;
			foreach (Item_Base it in ItemManager.GetAllItems())
			{
				CostMultiple[] cost = it != null && it.settings_recipe != null ? it.settings_recipe.NewCost : null;
				if (cost == null) continue;
				foreach (CostMultiple c in cost)
					if (c != null && !buildEntries.Contains(c)) { others.Add(new KeyValuePair<CostMultiple, int>(c, c.amount)); if (otherName == null) otherName = it.UniqueName; }
			}
			BuildCost.Set(100);
			List<string> changed = others.Where(kv => kv.Key.amount != kv.Value).Select(kv => (kv.Key.items != null && kv.Key.items.Length > 0 && kv.Key.items[0] != null ? kv.Key.items[0].UniqueName : "?") +
				" " + kv.Value + "->" + kv.Key.amount).ToList();
			Check(ref ok, others.Count >= 50 && changed.Count == 0, "the crafting menu stays Raft's at +100%: " + others.Count + " amounts of other recipes (e.g. " + otherName + ") unchanged" +
				(changed.Count > 0 ? " - CHANGED " + string.Join(", ", changed.Take(6).ToArray()) : ""));
			BuildCost.Set(0);

			// A real block at 0 % and at 50 %: Raft's BlockCreator takes the materials, Raft's refund gives half back
			Item_Base item = TestBlockItem();
			Check(ref ok, item != null, "a block to place: " + (item != null ? item.UniqueName : "none"));
			if (item != null)
				foreach (int p in new[] { 0, 50 })
				{
					BuildCost.Set(p);
					CostMultiple[] cost = item.settings_recipe.NewCost;
					foreach (CostMultiple c in cost) player.Inventory.AddItem(c.items[0].UniqueName, c.amount * 3);
					Dictionary<string, int> c0 = Counts(player, cost);
					Block made = player.BlockCreator.CreateBlockCheat(item, new Vector3(0f, 25f, 0f), Vector3.zero, DPS.Default, 0);
					Dictionary<string, int> c1 = Counts(player, cost);
					List<string> paid = cost.Where(c => c0[c.items[0].UniqueName] - c1[c.items[0].UniqueName] != c.amount)
						.Select(c => c.items[0].UniqueName + " took " + (c0[c.items[0].UniqueName] - c1[c.items[0].UniqueName]) + " (want " + c.amount + ")").ToList();
					Check(ref ok, made != null && paid.Count == 0, BuildCost.Describe(p) + ": placing a " + item.UniqueName + " took " + CostText(cost) + (made == null ? " - NO BLOCK" : "") + (paid.Count > 0 ? " - WRONG " + string.Join(", ", paid.ToArray()) : ""));
					if (made == null) continue;
					RemovePlaceables.ReturnItemsFromBlock(made, player, true);
					Dictionary<string, int> c2 = Counts(player, cost);
					List<string> back = cost.Where(c => c2[c.items[0].UniqueName] - c1[c.items[0].UniqueName] != Mathf.CeilToInt(c.amount * 0.5f))
						.Select(c => c.items[0].UniqueName + " gave back " + (c2[c.items[0].UniqueName] - c1[c.items[0].UniqueName]) + " (want " + Mathf.CeilToInt(c.amount * 0.5f) + ")").ToList();
					Check(ref ok, back.Count == 0, BuildCost.Describe(p) + ": removing it gives half back as Raft does: " + string.Join(" + ", cost.Select(c => Mathf.CeilToInt(c.amount * 0.5f) + " " + c.items[0].UniqueName).ToArray()) +
						(back.Count > 0 ? " - WRONG " + string.Join(", ", back.ToArray()) : ""));
					BlockCreator.RemoveBlockNetwork(made, null, true);
					yield return new WaitForSeconds(0.5f);
				}

			// AU33: a block gives back by the cost it was placed at, whatever the cost is when it is taken down
			if (item != null)
				foreach (int[] pair in new[] { new[] { 0, 100 }, new[] { 100, 0 }, new[] { 50, 100 } })
				{
					BuildCost.Set(pair[0]);
					CostMultiple[] cost = item.settings_recipe.NewCost;
					foreach (CostMultiple c in cost) player.Inventory.AddItem(c.items[0].UniqueName, BuildCost.Cost(BuildCost.OriginalOf(c), 100) * 3);
					Block made = player.BlockCreator.CreateBlockCheat(item, new Vector3(0f, 25f, 0f), Vector3.zero, DPS.Default, 0);
					Check(ref ok, made != null && BuildCostRefund.PercentOf(made.ObjectIndex) == pair[0], "placed at " + BuildCost.Describe(pair[0]) + ": noted as " + (made != null ? BuildCost.Describe(BuildCostRefund.PercentOf(made.ObjectIndex)) : "NO BLOCK"));
					if (made == null) continue;
					BuildCost.Set(pair[1]);
					string kept = BuildCostRefund.Encode();
					Dictionary<string, int> c1 = Counts(player, cost);
					RemovePlaceables.ReturnItemsFromBlock(made, player, true);
					Dictionary<string, int> c2 = Counts(player, cost);
					List<string> back = cost.Where(c => c2[c.items[0].UniqueName] - c1[c.items[0].UniqueName] != Mathf.CeilToInt(BuildCost.Cost(BuildCost.OriginalOf(c), pair[0]) * 0.5f))
						.Select(c => c.items[0].UniqueName + " gave back " + (c2[c.items[0].UniqueName] - c1[c.items[0].UniqueName]) + " (want " + Mathf.CeilToInt(BuildCost.Cost(BuildCost.OriginalOf(c), pair[0]) * 0.5f) + ")").ToList();
					List<string> after = cost.Where(c => c.amount != BuildCost.Cost(BuildCost.OriginalOf(c), pair[1])).Select(c => c.items[0].UniqueName + " " + c.amount).ToList();
					Check(ref ok, back.Count == 0 && after.Count == 0, "built at " + BuildCost.Describe(pair[0]) + ", taken down at " + BuildCost.Describe(pair[1]) + ": half of the placing cost back (" + kept + ")" +
						(back.Count > 0 ? " - WRONG " + string.Join(", ", back.ToArray()) : "") + (after.Count > 0 ? " - the build menu's cost not put back: " + string.Join(", ", after.ToArray()) : ""));
					BlockCreator.RemoveBlockNetwork(made, null, true);
					yield return new WaitForSeconds(0.5f);
				}
			// The world file's line and an older world (no line: its blocks were built at its cost)
			{
				string keep = BuildCostRefund.Encode();
				BuildCostRefund.Decode("40|0:7,8;100:9");
				bool round = BuildCostRefund.Base == 40 && BuildCostRefund.PercentOf(7) == 0 && BuildCostRefund.PercentOf(9) == 100 && BuildCostRefund.PercentOf(5) == 40 && BuildCostRefund.Encode() == "40|0:7,8;100:9";
				BuildCostRefund.Reset(0);
				BuildCostRefund.OnCostRead(60);
				bool older = BuildCostRefund.PercentOf(123) == 60;
				BuildCostRefund.Decode(keep);
				Check(ref ok, round && older, "@builtat read and written back (" + (round ? "same" : "DIFFERENT") + "); an older world's blocks give back by its cost (" + (older ? "yes" : "NO") + ")");
			}

			BuildCost.Set(before);
			Application.logMessageReceived -= counter;
			Check(ref ok, exceptions == 0, "no errors (" + exceptions + (firstException != null ? ", first: " + firstException : "") + ")");
			if (ok) Log("PASS: build cost in a world"); else Fail("build cost in a world");
		}

		[ConsoleCommand(name: "CIBuildCostPay", docs: "Dev, in game (either player, e.g. player 2): pays for a build menu block as Raft's BlockCreator does on the builder's machine (RemoveCostMultiple) and checks it took this world's build cost")]
		public static void BuildCostPay()
		{
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			if (!LoadSceneManager.IsGameSceneLoaded || player == null) { Fail("run in a world"); return; }
			Item_Base item = TestBlockItem();
			if (item == null) { Fail("no build menu block to pay for (build menu not found?)"); return; }
			CostMultiple[] cost = item.settings_recipe.NewCost;
			foreach (CostMultiple c in cost) player.Inventory.AddItem(c.items[0].UniqueName, c.amount * 3);
			Dictionary<string, int> c0 = Counts(player, cost);
			player.Inventory.RemoveCostMultiple(cost, false);
			Dictionary<string, int> c1 = Counts(player, cost);
			List<string> wrong = cost.Where(c => c0[c.items[0].UniqueName] - c1[c.items[0].UniqueName] != BuildCost.Cost(BuildCost.OriginalOf(c), BuildCost.Current))
				.Select(c => c.items[0].UniqueName + " took " + (c0[c.items[0].UniqueName] - c1[c.items[0].UniqueName]) + " (want " + BuildCost.Cost(BuildCost.OriginalOf(c), BuildCost.Current) + ")").ToList();
			Check(ref ok, BuildCost.Applied == BuildCost.Current && wrong.Count == 0, (Raft_Network.IsHost ? "host" : "player 2") + ", build cost " + BuildCost.Describe(BuildCost.Current) + ": a " + item.UniqueName + " took " + CostText(cost) +
				(wrong.Count > 0 ? " - WRONG " + string.Join(", ", wrong.ToArray()) : ""));
			if (ok) Log("PASS: build cost paid"); else Fail("build cost paid");
		}

		[ConsoleCommand(name: "CIBuildCostRepair", docs: "Dev, in game (host): the hammer's repair and reinforce follow the build cost - their cost lists are Raft's at 0 % and Raft's + 50 %, rounded, at 50 %; a damaged foundation repaired the way Raft's hammer does and one reinforced through Raft's Hammer.ReinforceBlock take that from a real inventory")]
		public static void BuildCostRepair()
		{
			StartTest(BuildCostRepairRoutine());
		}

		static IEnumerator BuildCostRepairRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			Network_Player player = RAPI.GetLocalPlayer();
			Hammer hammer = player != null ? player.GetComponentsInChildren<Hammer>(true).FirstOrDefault() : null;
			if (hammer == null) hammer = Resources.FindObjectsOfTypeAll<Hammer>().FirstOrDefault(h => h.gameObject.scene.IsValid());
			if (player == null || player.BlockCreator == null || hammer == null) { Fail("no player / BlockCreator / hammer"); yield break; }
			bool ok = true;
			int before = BuildCost.Current;
			var hammerT = HarmonyLib.Traverse.Create(hammer);
			Item_Base repair = hammerT.Field("repairItem").GetValue<Item_Base>(), reinforce = hammerT.Field("reinforceItem").GetValue<Item_Base>();
			int repairAmount = hammerT.Field("blockRepairAmount").GetValue<int>();
			Check(ref ok, repair != null && reinforce != null && repair.settings_recipe != null && reinforce.settings_recipe != null,
				"the hammer's repair item " + (repair != null ? repair.UniqueName : "NONE") + " and reinforce item " + (reinforce != null ? reinforce.UniqueName : "NONE") + ", repairs " + repairAmount + " health a hit");
			if (!ok) { Fail("build cost repair"); yield break; }
			BuildCost.Set(50);
			Check(ref ok, BuildCost.Items.Contains(repair) && BuildCost.Items.Contains(reinforce), "both are among the build menu items the build cost changes");

			Item_Base foundation = ItemManager.GetItemByName("Block_Foundation");
			Check(ref ok, foundation != null, "a foundation to damage and reinforce");
			foreach (int p in new[] { 0, 50 })
			{
				BuildCost.Set(p);
				foreach (Item_Base it in new[] { repair, reinforce })
				{
					List<string> wrong = it.settings_recipe.NewCost.Where(c => c != null && c.amount != BuildCost.Cost(BuildCost.OriginalOf(c), p)).Select(c => c.items[0].UniqueName + " " + c.amount).ToList();
					Check(ref ok, wrong.Count == 0, BuildCost.Describe(p) + ": " + it.UniqueName + " costs " + CostText(it.settings_recipe.NewCost) + (wrong.Count > 0 ? " - WRONG " + string.Join(", ", wrong.ToArray()) : ""));
				}
				if (foundation == null) continue;
				Block made = player.BlockCreator.CreateBlockCheat(foundation, new Vector3(0f, 25f, 0f), Vector3.zero, DPS.Default, 0);
				Check(ref ok, made != null, BuildCost.Describe(p) + ": a foundation placed");
				if (made == null) continue;
				yield return null;

				// Repair, the way Hammer.HandleRepairingOfBlock does it: enough materials, take the repair cost, Block.Repair
				CostMultiple[] rc = repair.settings_recipe.NewCost;
				foreach (CostMultiple c in rc) player.Inventory.AddItem(c.items[0].UniqueName, c.amount * 3);
				made.Damage(Mathf.Max(1, made.MaxHealth / 2));
				bool can = made.CanBeRepaired() && player.BlockCreator.HasEnoughResourcesToBuild(rc);
				Dictionary<string, int> r0 = Counts(player, rc);
				player.Inventory.RemoveCostMultiple(rc, false);
				made.Repair(repairAmount);
				Dictionary<string, int> r1 = Counts(player, rc);
				List<string> took = rc.Where(c => r0[c.items[0].UniqueName] - r1[c.items[0].UniqueName] != BuildCost.Cost(BuildCost.OriginalOf(c), p))
					.Select(c => c.items[0].UniqueName + " took " + (r0[c.items[0].UniqueName] - r1[c.items[0].UniqueName]) + " (want " + BuildCost.Cost(BuildCost.OriginalOf(c), p) + ")").ToList();
				Check(ref ok, can && took.Count == 0, BuildCost.Describe(p) + ": repairing a damaged foundation (" + made.Health + "/" + made.MaxHealth + " after) took " + CostText(rc) +
					(can ? "" : " - COULD NOT REPAIR") + (took.Count > 0 ? " - WRONG " + string.Join(", ", took.ToArray()) : ""));

				// Reinforce through Raft's own Hammer.ReinforceBlock (it takes the cost from the hammer's player)
				CostMultiple[] fc = reinforce.settings_recipe.NewCost;
				foreach (CostMultiple c in fc) player.Inventory.AddItem(c.items[0].UniqueName, c.amount * 3);
				bool canR = made.CanBeReinforced();
				Dictionary<string, int> f0 = Counts(player, fc);
				bool done = false;
				// (the hammer as Raft's player holds it: its player and host)
				var th = HarmonyLib.Traverse.Create(hammer);
				if (th.Field("playerNetwork").GetValue() == null) th.Field("playerNetwork").SetValue(player);
				if (th.Field("hostNetwork").GetValue() == null) th.Field("hostNetwork").SetValue(ComponentManager<Network_Host>.Value);
				try { done = (bool)HarmonyLib.AccessTools.Method(typeof(Hammer), "ReinforceBlock").Invoke(hammer, new object[] { made }); }
				catch (System.Exception e) { Debug.Log("[CITEST] ReinforceBlock threw " + (e.InnerException ?? e)); }
				Dictionary<string, int> f1 = Counts(player, fc);
				List<string> tookR = fc.Where(c => f0[c.items[0].UniqueName] - f1[c.items[0].UniqueName] != BuildCost.Cost(BuildCost.OriginalOf(c), p))
					.Select(c => c.items[0].UniqueName + " took " + (f0[c.items[0].UniqueName] - f1[c.items[0].UniqueName]) + " (want " + BuildCost.Cost(BuildCost.OriginalOf(c), p) + ")").ToList();
				Check(ref ok, canR && done && made.Reinforced && tookR.Count == 0, BuildCost.Describe(p) + ": reinforcing it (Raft's Hammer.ReinforceBlock) took " + CostText(fc) +
					(canR && done && made.Reinforced ? "" : " - NOT REINFORCED (can " + canR + ", done " + done + ")") + (tookR.Count > 0 ? " - WRONG " + string.Join(", ", tookR.ToArray()) : ""));
				BlockCreator.RemoveBlockNetwork(made, null, true);
				yield return new WaitForSeconds(0.5f);
			}
			BuildCost.Set(before);
			if (ok) Log("PASS: build cost repair"); else Fail("build cost repair");
		}

		[ConsoleCommand(name: "CIBuildCostMenu", docs: "Dev, in game (host): Raft's build menu shows the build cost - the menu opened with a foundation chosen, its cost panel reads Raft's amounts at 0 % and Raft's + 50 %, rounded, at 50 % (Raft fills it from the same list); picture build_cost_menu")]
		public static void BuildCostMenu()
		{
			StartTest(BuildCostMenuRoutine());
		}

		static IEnumerator BuildCostMenuRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			Network_Player player = RAPI.GetLocalPlayer();
			BuildMenu menu = Resources.FindObjectsOfTypeAll<BuildMenu>().FirstOrDefault(m => m.gameObject.scene.IsValid());
			CanvasHelper canvasHelper = ComponentManager<CanvasHelper>.Value ?? UnityEngine.Object.FindObjectOfType<CanvasHelper>();
			Item_Base foundation = ItemManager.GetItemByName("Block_Foundation");
			if (player == null || menu == null || canvasHelper == null || foundation == null) { Fail("no player / build menu / canvas / foundation"); yield break; }
			bool ok = true;
			int before = BuildCost.Current;
			var menuT = HarmonyLib.Traverse.Create(menu);
			// (Raft's BuildMenu.Update fills the cost panel only while the hammer is in hand: its BlockCreator active)
			Item_Base hammerItem = ItemManager.GetItemByName("Hammer");
			Hotbar hotbar = HarmonyLib.Traverse.Create(player.Inventory).Field("hotbar").GetValue<Hotbar>();
			if (hammerItem != null && player.Inventory.GetItemCount(hammerItem) == 0) player.Inventory.AddItem(hammerItem.UniqueName, 1);
			if (hammerItem != null && hotbar != null) hotbar.SelectItem(hammerItem);
			for (int i = 0; i < 10; i++) yield return null;
			foreach (int p in new[] { 0, 50 })
			{
				BuildCost.Set(p);
				canvasHelper.OpenMenuCloseOther(MenuType.BuildMenu, true);
				yield return null;
				try { HarmonyLib.AccessTools.Method(typeof(BuildMenu), "SelectBlock").Invoke(menu, new object[] { foundation }); }
				catch (System.Exception e) { Debug.Log("[CITEST] SelectBlock threw " + (e.InnerException ?? e).Message); }
				yield return null;
				// (the cost panel is filled by DisplayBlockInfo - what Raft runs when the pointer is on a block's button)
				try { HarmonyLib.AccessTools.Method(typeof(BuildMenu), "DisplayBlockInfo").Invoke(menu, new object[] { foundation }); }
				catch (System.Exception e) { Debug.Log("[CITEST] DisplayBlockInfo threw " + (e.InnerException ?? e).Message); }
				for (int i = 0; i < 10; i++) yield return null;
				CostCollection panel = menuT.Field("costColletionPanel").GetValue<CostCollection>();
				List<BuildingUI_CostBox> boxes = panel != null ? (HarmonyLib.Traverse.Create(panel).Field("costBoxes").GetValue<List<BuildingUI_CostBox>>() ?? new List<BuildingUI_CostBox>()) : new List<BuildingUI_CostBox>();
				var shown = new Dictionary<string, int>();
				foreach (BuildingUI_CostBox b in boxes.Where(b => b != null && b.gameObject.activeInHierarchy))
				{
					var bt = HarmonyLib.Traverse.Create(b);
					List<Item_Base> its = bt.Field("items").GetValue<List<Item_Base>>();
					if (its != null && its.Count > 0 && its[0] != null) shown[its[0].UniqueName] = bt.Field("requiredAmount").GetValue<int>();
				}
				string texts = string.Join(" | ", boxes.Where(b => b != null && b.gameObject.activeInHierarchy).SelectMany(b => b.GetComponentsInChildren<Text>()).Select(t => t.text).Where(t => !string.IsNullOrEmpty(t)).ToArray());
				List<string> wrong = foundation.settings_recipe.NewCost.Where(c => { int s; return !shown.TryGetValue(c.items[0].UniqueName, out s) || s != BuildCost.Cost(BuildCost.OriginalOf(c), p); })
					.Select(c => c.items[0].UniqueName + " shows " + (shown.ContainsKey(c.items[0].UniqueName) ? shown[c.items[0].UniqueName].ToString() : "nothing") + " (want " + BuildCost.Cost(BuildCost.OriginalOf(c), p) + ")").ToList();
				Check(ref ok, CanvasHelper.ActiveMenu == MenuType.BuildMenu && wrong.Count == 0, BuildCost.Describe(p) + ": the build menu (" + CanvasHelper.ActiveMenu + ") shows a foundation costing " +
					string.Join(" + ", shown.Select(kv => kv.Value + " " + kv.Key).ToArray()) + " (texts: " + texts + "; panel " + (panel == null ? "missing" : panel.gameObject.activeInHierarchy ? "shown" : "hidden") + ", " + boxes.Count + " boxes, " + boxes.Count(b => b != null && b.gameObject.activeSelf) + " on, chosen " + (menuT.Field("currentBuildable").GetValue() ?? "none") + ")" + (wrong.Count > 0 ? " - WRONG " + string.Join(", ", wrong.ToArray()) : ""));
				if (p == 50) { yield return new WaitForSeconds(0.3f); Screenshot(new[] { "build_cost_menu" }); yield return new WaitForSeconds(0.5f); }
				canvasHelper.CloseMenu(MenuType.BuildMenu);
				yield return null;
			}
			BuildCost.Set(before);
			if (ok) Log("PASS: build cost menu"); else Fail("build cost menu");
		}

		#endregion

		#region Two players

		/// <summary>
		/// The shared clock that movers (and everything timed) follow, with this PC's time: both players' lines taken a moment
		/// apart must differ by as much on the clock as on the PC's time (both Rafts run on one PC in the tests).
		/// </summary>
		[ConsoleCommand(name: "CIClockSig", docs: "Dev, two players: logs 'CLOCK <shared clock s> AT <PC time ms> SYNCED <bool>' to compare between the players (movers follow the shared clock)")]
		public static void ClockSig()
		{
			Log("CLOCK " + SharedClock.Now.ToString("F3", CultureInfo.InvariantCulture) + " AT " + (DateTime.UtcNow.Ticks / 10000L).ToString(CultureInfo.InvariantCulture) +
				" SYNCED " + SharedClock.Synced + " (" + (Raft_Network.IsHost ? "host" : "player 2") + ")");
		}

		static Network_Entity markEntity;
		static float markHealth, markTime;

		static Network_Entity MarkTarget(string what)
		{
			if (what == "me") { Network_Player p = RAPI.GetLocalPlayer(); return p != null ? p.Stats : null; }
			// (the same shark on both machines: the lowest network id)
			AI_NetworkBehaviour s = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a.behaviourType == AI_NetworkBehaviourType.Shark && a.networkEntity != null && !a.networkEntity.IsDead)
				.OrderBy(a => a.ObjectIndex).FirstOrDefault();
			return s != null ? s.networkEntity : null;
		}

		[ConsoleCommand(name: "CIMonsterMark", docs: "Dev, two players: remembers the health of Bruce (the shark with the lowest network id) or of this player: CIMonsterMark shark|me")]
		public static void MonsterMark(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "shark";
			markEntity = MarkTarget(what);
			if (markEntity == null) { Fail("no " + what + " to mark"); return; }
			var ps = markEntity as PlayerStats;
			if (ps != null) { KeepAlive(RAPI.GetLocalPlayer()); ps.stat_BonusHealth.statTarget.Value = 0f; }
			markHealth = markEntity.stat_health.Value;
			markTime = Time.realtimeSinceStartup;
			Log("Marked " + what + ": health " + markHealth.ToString("0.##") + " (" + (Raft_Network.IsHost ? "host" : "player 2") + ", monsters " + MonsterDifficulty.Name(MonsterDifficulty.Current) + ")");
		}

		[ConsoleCommand(name: "CIMonsterHit", docs: "Dev, two players (either): hits Bruce as a player's spear does, through Raft's DamageEntity (a client's hit goes to the host): CIMonsterHit <damage>")]
		public static void MonsterHit(string[] args)
		{
			float d = 12f;
			if (args != null && args.Length > 0) float.TryParse(args[args.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out d);
			Network_Entity e = MarkTarget("shark");
			Network_Host net = ComponentManager<Network_Host>.Value;
			if (e == null || net == null) { Fail("no shark / Network_Host"); return; }
			net.DamageEntity(e, e.transform, d, e.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("Hit Bruce for " + d + " (" + (Raft_Network.IsHost ? "host" : "player 2: sent to the host") + ", monsters " + MonsterDifficulty.Name(MonsterDifficulty.Current) + ")");
		}

		[ConsoleCommand(name: "CIMonsterHurt", docs: "Dev, two players (host): a monster's bite on every other player, through Raft's DamageEntity: CIMonsterHurt <damage>")]
		public static void MonsterHurt(string[] args)
		{
			float d = 10f;
			if (args != null && args.Length > 0) float.TryParse(args[args.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out d);
			Raft_Network network = ComponentManager<Raft_Network>.Value;
			Network_Host net = ComponentManager<Network_Host>.Value;
			if (!Raft_Network.IsHost || network == null || net == null) { Fail("run as the host of a two-player world"); return; }
			Network_Player me = RAPI.GetLocalPlayer();
			int n = 0;
			foreach (Network_Player p in network.remoteUsers.Values.Where(p => p != null && p != me && p.Stats != null))
			{
				net.DamageEntity(p.Stats, p.transform, d, p.transform.position + Vector3.up, Vector3.up, EntityType.Enemy, null);
				n++;
			}
			Log("Bit " + n + " other player(s) for " + d + " (monsters " + MonsterDifficulty.Name(MonsterDifficulty.Current) + ")");
		}

		[ConsoleCommand(name: "CIMonsterMarkCheck", docs: "Dev, two players: the marked health went down as a hit of <damage> should at this world's monster difficulty: CIMonsterMarkCheck shark|me <damage>")]
		public static void MonsterMarkCheck(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "shark";
			float d = 12f;
			if (args != null && args.Length > 1) float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out d);
			if (markEntity == null) { Fail("nothing marked (CIMonsterMark first)"); return; }
			bool ok = true;
			float drop = markHealth - markEntity.stat_health.Value;
			bool me = what == "me";
			float want = ExpectedDrop(markEntity, d, me ? EntityType.Enemy : EntityType.Player, !me);
			// (health comes back slowly: what it could have regained since the mark)
			float regen = markEntity.stat_health.regenPerSecond * (Time.realtimeSinceStartup - markTime);
			Check(ref ok, drop > 0f && drop <= want + 0.05f && drop >= want - 0.05f - Mathf.Max(0f, regen),
				(me ? "this player" : "Bruce") + " lost " + drop.ToString("0.##") + " from a hit of " + d + " at " + MonsterDifficulty.Name(MonsterDifficulty.Current) + " (want " + want.ToString("0.##") + (regen > 0f ? ", less up to " + regen.ToString("0.##") + " regained" : "") + ")");
			if (me) KeepAlive(RAPI.GetLocalPlayer());
			markEntity = null;
			if (ok) Log("PASS: monster difficulty mark check"); else Fail("monster difficulty mark check");
		}

		#endregion
	}
}

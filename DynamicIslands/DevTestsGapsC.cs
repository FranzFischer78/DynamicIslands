using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Two-player gaps, round 3 (mpgaps2.ps1; TEST_CATALOGUE IM7, IM21-IM25, IM31, IM34, IM36). Only what the older commands
	/// cannot do: player 2's radar (IM22), player 2's own pool list (IM21), an "older build" that ignores the newer message
	/// kinds, an error counter and junk messages (IM23), player 2's last New Game choices, generator settings and presets
	/// (IM24, IM31), what an island's file holds on each machine (IM24, IM31), the culture (IM25), a specific moved crate or
	/// hoard taken at a set moment by both players (IM36), and one island of each oddity kind (IM31).
	/// Reused unchanged: CIRandomizerSig, CIServerSig, CIMovedState, CICaveState, CIIslandHash, CIUniqueCopy, CISpawnPoolSet,
	/// CICulture, CIFakeVersion, CIVersionCheck, CIPickMoved, CIOpenChest, CIGrotto, CIRandomizerLarge, CISharkLook.
	/// </summary>
	public static partial class DevTests
	{
		static long GcNowMs() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

		/// <summary>The arguments without a "@&lt;epoch ms&gt;" one (0 when there is none): both players act at that moment.</summary>
		static string[] GcSplitAt(string[] args, out long at)
		{
			at = 0;
			var rest = new List<string>();
			foreach (string a in args ?? new string[0])
			{
				long v;
				if (a.StartsWith("@") && long.TryParse(a.Substring(1), NumberStyles.Integer, GInv, out v)) at = v; else rest.Add(a);
			}
			return rest.ToArray();
		}

		static int GcInvTotal(PlayerInventory inv)
		{
			int sum = 0;
			if (inv == null) return -1;
			foreach (Item_Base item in ItemManager.GetAllItems())
			{
				if (item == null || string.IsNullOrEmpty(item.UniqueName)) continue;
				try { sum += inv.GetItemCount(item.UniqueName); } catch { }
			}
			return sum;
		}

		/// <summary>A message to the other side: the host to every client, a client to the host (as IslandNetwork's own senders do).</summary>
		static bool GcSend(IslandNetMessage msg)
		{
			if (!IslandNetwork.InGame) return false;
			try
			{
				if (Raft_Network.IsHost) DynamicIslands.instance.SendNetworkMessage(msg, Target.Other, Steamworks.EP2PSend.k_EP2PSendReliable);
				else
				{
					Raft_Network net = ComponentManager<Raft_Network>.Value;
					if (net == null) return false;
					DynamicIslands.instance.SendNetworkMessageToPlayer(msg, net.HostID, Steamworks.EP2PSend.k_EP2PSendReliable);
				}
				return true;
			}
			catch (Exception e) { Log("send failed: " + e.Message); return false; }
		}

		#region IM7, IM34: how Raft's animals look, in a few short lines

		[ConsoleCommand(name: "CIGapsLooks", docs: "Dev, world (either player), IM7/IM34: every animal of Raft's by network index and the ones with a look from the randomizer (colour, alpha, Big Bruce), in lines short enough for ci.ps1: LOOKS <n> idx <i,i,..> and LOOK <index> <type> <variant> alive|dead")]
		public static void GapsLooksCommand()
		{
			List<AI_NetworkBehaviour> all = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null && !CreatureSpawner.IsOnCustomIsland(a)).OrderBy(a => a.ObjectIndex).ToList();
			Log("LOOKS " + all.Count + " idx " + string.Join(",", all.Select(a => a.ObjectIndex.ToString(GInv)).ToArray()) + " (" + GWho() + ")");
			foreach (AI_NetworkBehaviour a in all)
			{
				string v;
				if (WorldRandomizer.VariantOfIndex.TryGetValue(a.ObjectIndex, out v))
					Log("LOOK " + a.ObjectIndex + " " + a.behaviourType + " " + v + " " + (a.networkEntity != null && a.networkEntity.IsDead ? "dead" : "alive"));
			}
			Log("PASS: looks");
		}

		#endregion

		#region IM22: the Receiver's dots on any player's machine

		[ConsoleCommand(name: "CIGapsRadar", docs: "Dev, in game (either player; player 2), IM22: puts a Receiver next to this player with its radar on and counts its dots: they must be one per island the radar shows when the host's showOnReceiver is on (WorldRules.ShowOnReceiver) and none when it is off, each pointing the right way; logs RADAR receiver shown|hidden dots N islands M")]
		public static void GapsRadarCommand() { StartTest(GapsRadarRoutine()); }

		static IEnumerator GapsRadarRoutine()
		{
			Network_Player me = RAPI.GetLocalPlayer();
			if (!LoadSceneManager.IsGameSceneLoaded || me == null) { Fail("run in a world"); yield break; }
			Reciever prefab = null;
			foreach (Item_Base item in ItemManager.GetAllItems())
			{
				try
				{
					if (item == null || item.settings_buildable == null || !item.settings_buildable.Placeable) continue;
					Block[] blocks = item.settings_buildable.GetBlockPrefabs();
					prefab = blocks != null ? blocks.Where(b => b != null).Select(b => b.GetComponentInChildren<Reciever>(true)).FirstOrDefault(rc => rc != null) : null;
					if (prefab != null) break;
				}
				catch { }
			}
			if (prefab == null) { Fail("could not find Raft's receiver block"); yield break; }
			GameObject go = UnityEngine.Object.Instantiate(prefab.transform.root.gameObject, me.transform.position + Vector3.up * 3f, Quaternion.identity);
			Reciever r = go.GetComponentInChildren<Reciever>(true);
			yield return null;
			if (r.radarSection != null) r.radarSection.SetActive(true);
			IslandRadar.Draw(r);
			List<Reciever_Dot> dots = IslandRadar.DotsOf(r).Where(d => d != null && d.gameObject.activeSelf).ToList();
			List<IslandWorldState.Entry> shown = IslandRadar.Shown;
			bool wantShown = WorldRules.ShowOnReceiver;
			float worst = 0f;
			for (int i = 0; wantShown && i < dots.Count && i < shown.Count; i++)
			{
				Vector3 toIsland = shown[i].Position - r.transform.position;
				Vector3 seen = Quaternion.Euler(0f, -r.transform.eulerAngles.y, 0f) * new Vector3(toIsland.x, 0f, toIsland.z);
				Vector2 dotDir = ((RectTransform)dots[i].transform).anchoredPosition;
				if (dotDir.sqrMagnitude > 0.01f) worst = Mathf.Max(worst, Vector2.Angle(new Vector2(seen.x, seen.z), -dotDir));
			}
			bool ok = wantShown ? dots.Count == shown.Count && shown.Count > 0 && worst < 3f : dots.Count == 0;
			Log("RADAR receiver " + (wantShown ? "shown" : "hidden") + " dots " + dots.Count + " islands " + shown.Count + " in world " + IslandWorldState.Islands.Count + ", worst angle " + Gf(worst, "F1") +
				", this machine's own file says " + (CustomIslandSpawner.ShowOnReceiver ? "shown" : "hidden") + " (" + GWho() + ")");
			UnityEngine.Object.Destroy(go);
			if (ok) Log("PASS: radar"); else Fail("radar: " + dots.Count + " dot(s) for " + shown.Count + " island(s), receiver " + (wantShown ? "shown" : "hidden"));
		}

		#endregion

		#region IM21: player 2's own pool list

		[ConsoleCommand(name: "CIGapsPoolList", docs: "Dev (either player; player 2), IM21: gives this machine another island list in its spawn pool than its spawnpool.txt, in memory only: CIGapsPoolList <island> [weight] <island> [weight] ... | CIGapsPoolList reset (reads the file again). Run it after CISpawnPoolSet, which reads the file again")]
		public static void GapsPoolListCommand(string[] args)
		{
			var lines = Traverse.Create(typeof(CustomIslandSpawner)).Field("poolLines").GetValue() as List<KeyValuePair<string, float>>;
			if (lines == null) { Fail("poolLines not found in CustomIslandSpawner"); return; }
			if (args != null && args.Length == 1 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase)) CustomIslandSpawner.LoadPool(true);
			else
			{
				if (args == null || args.Length == 0) { Fail("usage: CIGapsPoolList <island> [weight] ... | reset"); return; }
				CustomIslandSpawner.LoadPool(true);
				lines.Clear();
				int i = 0;
				while (i < args.Length)
				{
					string island = args[i++];
					float w;
					if (i < args.Length && float.TryParse(args[i], NumberStyles.Float, GInv, out w)) i++; else w = 1f;
					lines.Add(new KeyValuePair<string, float>(island, w));
				}
			}
			Log("POOL here: " + (lines.Count == 0 ? "empty" : string.Join(", ", lines.Select(p => p.Key + " " + Gf(p.Value, "F1")).ToArray())) + "; chancePerKm " + Gf(CustomIslandSpawner.ChancePerKm) + " (" + GWho() + ")");
			Log("PASS: pool list");
		}

		#endregion

		#region IM23: an older build, junk and errors

		static int gcErrors;
		static readonly List<string> gcErrorText = new List<string>();
		static bool gcErrorsOn;

		static void GcLogHook(string text, string trace, LogType type)
		{
			if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
			text = text ?? "";
			if (text.StartsWith("[CITEST]")) return;
			if (!(text.Contains("CUSTOM ISLANDS") || text.Contains("DynamicIslands") || (trace ?? "").Contains("DynamicIslands"))) return;
			gcErrors++;
			if (gcErrorText.Count < 4) gcErrorText.Add(text.Length > 140 ? text.Substring(0, 140) : text);
		}

		[ConsoleCommand(name: "CIGapsErrors", docs: "Dev, anywhere, IM23: counts the errors and exceptions this mod logs: CIGapsErrors start (counts from now) | CIGapsErrors report (logs ERRORS n [first ones]; PASS when there are none) | CIGapsErrors stop")]
		public static void GapsErrorsCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "report";
			if (what == "start" || what == "stop") { if (gcErrorsOn) Application.logMessageReceived -= GcLogHook; gcErrorsOn = false; gcErrors = 0; gcErrorText.Clear(); }
			if (what == "start") { Application.logMessageReceived += GcLogHook; gcErrorsOn = true; Log("ERRORS counting from now (" + GWho() + ")"); Log("PASS: errors started"); return; }
			if (what == "stop") { Log("ERRORS stopped"); Log("PASS: errors stopped"); return; }
			Log("ERRORS " + gcErrors + (gcErrorsOn ? "" : " (not counting: CIGapsErrors start first)") + (gcErrorText.Count > 0 ? ": " + string.Join(" | ", gcErrorText.ToArray()) : "") + " (" + GWho() + ")");
			if (gcErrors == 0 && gcErrorsOn) Log("PASS: no errors"); else Fail("errors: " + gcErrors);
		}

		static int gcOldBuildMax = -1, gcIgnored;
		static readonly List<int> gcIgnoredKinds = new List<int>();
		static HarmonyLib.Harmony gcHarmony;

		/// <summary>Prefix of IslandNetwork.OnMessage: a message of a kind the "older build" does not know is dropped, as such a build's switch does (no case = nothing happens).</summary>
		static bool GcOldBuildPrefix(object message, ref bool __result)
		{
			IslandNetMessage msg = message as IslandNetMessage;
			if (msg == null || gcOldBuildMax < 0 || msg.Kind <= gcOldBuildMax) return true;
			gcIgnored++;
			if (!gcIgnoredKinds.Contains(msg.Kind)) gcIgnoredKinds.Add(msg.Kind);
			__result = true;
			return false;
		}

		[ConsoleCommand(name: "CIGapsOldBuild", docs: "Dev, anywhere (player 2), IM23: this machine acts as an older build that knows message kinds up to <max> only (default 12; kinds 13-15 were newer) and ignores the rest: CIGapsOldBuild [max] | CIGapsOldBuild off | CIGapsOldBuild status; logs OLDBUILD max M ignored N kinds [..]. Pair with CIFakeVersion for the version notice")]
		public static void GapsOldBuildCommand(string[] args)
		{
			string a = args != null && args.Length > 0 ? args[0] : "12";
			if (a != "status")
			{
				int max;
				if (a == "off") max = -1; else if (!int.TryParse(a, out max)) { Fail("CIGapsOldBuild [max]|off|status"); return; }
				if (max >= 0 && gcHarmony == null)
				{
					MethodInfo target = AccessTools.Method(typeof(IslandNetwork), "OnMessage");
					if (target == null) { Fail("IslandNetwork.OnMessage not found"); return; }
					gcHarmony = new HarmonyLib.Harmony("ci.gaps.oldbuild");
					gcHarmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "GcOldBuildPrefix")));
				}
				gcOldBuildMax = max;
				if (max >= 0) { gcIgnored = 0; gcIgnoredKinds.Clear(); }
			}
			Log("OLDBUILD max " + gcOldBuildMax + " ignored " + gcIgnored + " message(s), kinds [" + string.Join(" ", gcIgnoredKinds.OrderBy(k => k).Select(k => k.ToString()).ToArray()) + "] (" + GWho() + ")");
			Log("PASS: old build");
		}

		[ConsoleCommand(name: "CIGapsNetJunk", docs: "Dev, in game (player 2 to the host; the host to every player), IM23: sends one message of any kind: CIGapsNetJunk <kind> [name=..] [data=..] [index=n] [count=n] [ids=1,2] [full]; logs JUNK kind K sent")]
		public static void GapsNetJunkCommand(string[] args)
		{
			int kind;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out kind)) { Fail("usage: CIGapsNetJunk <kind> [name=..] [data=..] [index=n] [count=n] [ids=1,2] [full]"); return; }
			var msg = new IslandNetMessage { Kind = kind };
			foreach (string a in args.Skip(1))
			{
				int eq = a.IndexOf('=');
				string k = (eq > 0 ? a.Substring(0, eq) : a).ToLowerInvariant(), v = eq > 0 ? a.Substring(eq + 1) : "";
				int n;
				if (k == "name") msg.Name = v;
				else if (k == "data") msg.Data = v;
				else if (k == "index" && int.TryParse(v, out n)) msg.Index = n;
				else if (k == "count" && int.TryParse(v, out n)) msg.Count = n;
				else if (k == "ids") msg.Ids = v.Split(',').Select(x => { int i; return int.TryParse(x, out i) ? i : 0; }).ToArray();
				else if (k == "full") msg.FullList = true;
			}
			bool sent = GcSend(msg);
			Log("JUNK kind " + kind + (sent ? " sent" : " NOT sent") + " (" + GWho() + ")");
			if (sent) Log("PASS: net junk"); else Fail("net junk: not in a two-player game");
		}

		[ConsoleCommand(name: "CIGapsNetJunkAll", docs: "Dev, in game (player 2 to the host; the host to every player), IM23: a batch of junk messages: CIGapsNetJunkAll unknown (kinds nobody has: 99, 0, -5, 1000) | wrongway (the host-to-player kinds, sent the other way with other values than the world's: randomizer, world rules, options, story chain, levels, story, world copy/save) | malformed (known kinds with missing fields). Logs JUNK batch <what>: N sent")]
		public static void GapsNetJunkAllCommand(string[] args) { StartTest(GapsNetJunkAllRoutine(args != null && args.Length > 0 ? args[0] : "unknown")); }

		static IEnumerator GapsNetJunkAllRoutine(string what)
		{
			var list = new List<IslandNetMessage>();
			if (what == "unknown")
			{
				list.Add(new IslandNetMessage { Kind = 99, Name = "cigaps-junk", Data = "x", Index = 1, Count = 2 });
				list.Add(new IslandNetMessage { Kind = 0 });
				list.Add(new IslandNetMessage { Kind = -5, Ids = new[] { 1, 2 } });
				list.Add(new IslandNetMessage { Kind = 1000, Data = new string('x', 5000), FullList = true });
			}
			else if (what == "wrongway")
			{
				RandomizerSettings other = WorldRandomizer.Current.Copy();
				other.Level = other.Level == RandomizerSettings.LevelNames.Length - 1 ? 1 : RandomizerSettings.LevelNames.Length - 1;
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.Randomizer, Data = other.Encode() });
				IslandNetMessage rules = WorldRules.Message();
				rules.Index = (MonsterDifficulty.Current + 2) % MonsterDifficulty.Names.Length;
				rules.Count = BuildCost.Current == 77 ? 33 : 77;
				rules.Data = "receiver=" + (WorldRules.ShowOnReceiver ? 0 : 1) + ";unload=999;regrow=9;rdist=1";
				list.Add(rules);
				IslandNetMessage opt = WorldOptions.Message();
				if (opt != null) { opt.Data = WorldOptions.Encode(WorldOptions.Offered, 4242); list.Add(opt); }
				IslandNetMessage chain = StoryChain.Message();
				if (chain != null) { chain.Data = "off;garbage"; list.Add(chain); }
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "mine", Data = "garbage" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "state", Data = "garbage" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.Story, Name = "all", Data = "garbage\tgarbage" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.QuestCount, Data = "group\tname\t1" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.WorldCopy, Name = "cigaps-junk", Hash = "1", Index = 0, Count = 1, Data = "@auto=off" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.WorldSave, Name = "cigaps-junk", Hash = "cigaps", Index = 0, Count = 1, Data = "AAAA" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.Remove, Ids = new[] { 1, 2, 3, 4, 5, 6 } });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.CreatureSpots, Data = "1:1:1;2:2:2" });
			}
			else if (what == "malformed")
			{
				foreach (int kind in new[] { 1, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 22, 23, 24, 25, 26, 27, 29 })
					list.Add(new IslandNetMessage { Kind = kind });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.SyncRequest });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.FileChunk, Name = "../../cigaps-evil", Hash = "zz", Index = 0, Count = 1, Data = "AAAA" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.FileRequest, Name = "..\\cigaps-evil", Hash = "../x" });
				list.Add(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "mine" });
			}
			else { Fail("CIGapsNetJunkAll unknown|wrongway|malformed"); yield break; }
			int sent = 0;
			foreach (IslandNetMessage m in list)
			{
				if (GcSend(m)) sent++;
				yield return new WaitForSeconds(0.25f);
			}
			Log("JUNK batch " + what + ": " + sent + " of " + list.Count + " sent (" + GWho() + ")");
			if (sent == list.Count) Log("PASS: net junk batch"); else Fail("net junk batch: " + sent + " of " + list.Count + " sent");
		}

		#endregion

		#region IM24: this PC's own last choices, generator settings and presets

		static Dictionary<string, string> gcLastOrig;
		static string gcRandomizerFileOrig;
		static bool gcRandomizerFileHad;
		static readonly string[] GcLastKeys = { "monsters", "buildcost", "levels", "options", "headstart" };

		static string GcRandomizerFile { get { return System.IO.Path.Combine(DynamicIslands.assetpath, WorldRandomizer.DefaultsFileName); } }

		[ConsoleCommand(name: "CIGapsLastChoices", docs: "Dev, anywhere (player 2), IM24: this PC's last New Game choices (world_rules.txt: monsters Savage, build cost +100%, levels on, every option, head start 3; randomizer.txt: Wild) set to values that differ from a test world's: CIGapsLastChoices set | check | reset (puts the files back). Logs LAST monsters=.. buildcost=.. levels=.. options=N headstart=.. randomizer=..")]
		public static void GapsLastChoicesCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "check";
			if (what == "set")
			{
				if (gcLastOrig == null)
				{
					gcLastOrig = new Dictionary<string, string>();
					foreach (string k in GcLastKeys) gcLastOrig[k] = WorldRules.ReadDefault(k);
					gcRandomizerFileHad = System.IO.File.Exists(GcRandomizerFile);
					gcRandomizerFileOrig = gcRandomizerFileHad ? System.IO.File.ReadAllText(GcRandomizerFile) : null;
				}
				WorldRules.SaveDefault("monsters", "savage");
				WorldRules.SaveDefault("buildcost", "100");
				WorldRules.SaveDefault("levels", "on");
				WorldOptions.SaveDefaults(WorldOptions.Offered);
				WorldRules.SaveDefault("headstart", "3");
				var s = new RandomizerSettings();
				s.Level = RandomizerSettings.LevelNames.Length - 1;
				WorldRandomizer.SaveDefaults(s);
			}
			else if (what == "reset")
			{
				if (gcLastOrig != null)
				{
					foreach (string k in GcLastKeys) WorldRules.SaveDefault(k, gcLastOrig[k] ?? "");
					try { if (gcRandomizerFileHad) System.IO.File.WriteAllText(GcRandomizerFile, gcRandomizerFileOrig); else if (System.IO.File.Exists(GcRandomizerFile)) System.IO.File.Delete(GcRandomizerFile); } catch (Exception e) { Log("randomizer.txt: " + e.Message); }
					gcLastOrig = null;
				}
			}
			Log("LAST monsters=" + (WorldRules.ReadDefault("monsters") ?? "-") + " buildcost=" + (WorldRules.ReadDefault("buildcost") ?? "-") + " levels=" + (WorldRules.ReadDefault("levels") ?? "-") +
				" options=" + (WorldRules.ReadDefault("options") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length + " headstart=" + (WorldRules.ReadDefault("headstart") ?? "-") +
				" randomizer=" + WorldRandomizer.Defaults.Encode() + " (" + GWho() + ")");
			Log("PASS: last choices " + what);
		}

		static IslandGenSettings gcGenLast;
		static Dictionary<string, float> gcOverridesBefore;
		const string GcPresetName = "cigapspreset";

		[ConsoleCommand(name: "CIGapsP2Gen", docs: "Dev, anywhere (player 2), IM24/IM31: this PC's own generator inputs made unlike the host's: spawn pool numbers (generated 80, flying 1, gather 1, shallows 1), the generator window's last settings, and a preset file 'cigapspreset': CIGapsP2Gen on | off | check. Logs P2GEN ..")]
		public static void GapsP2GenCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "check";
			string preset = System.IO.Path.Combine(GeneratorWindow.PresetFolder, GcPresetName + ".txt");
			if (what == "on")
			{
				if (gcOverridesBefore == null) { gcOverridesBefore = new Dictionary<string, float>(CustomIslandSpawner.TestOverrides); gcGenLast = IslandGenerator.Last; }
				foreach (var kv in new Dictionary<string, float> { { "generated", 80f }, { "generatedflyingchance", 1f }, { "generatedgather", 1f }, { "generatedshallows", 1f } })
				{ CustomIslandSpawner.SetValue(kv.Key, kv.Value); CustomIslandSpawner.TestOverrides[kv.Key] = kv.Value; }
				var odd = new IslandGenSettings { Radius = 260f, Height = 140f, Roughness = 1f, Peaks = 6, ObjectDensity = 1f };
				IslandGenerator.Last = odd;
				System.IO.Directory.CreateDirectory(GeneratorWindow.PresetFolder);
				System.IO.File.WriteAllText(preset, odd.ToText());
				CustomIslandSpawner.LoadPool(true);
			}
			else if (what == "off")
			{
				if (gcOverridesBefore != null)
				{
					CustomIslandSpawner.TestOverrides.Clear();
					foreach (var kv in gcOverridesBefore) CustomIslandSpawner.TestOverrides[kv.Key] = kv.Value;
					IslandGenerator.Last = gcGenLast;
					gcOverridesBefore = null;
				}
				try { if (System.IO.File.Exists(preset)) System.IO.File.Delete(preset); } catch { }
				CustomIslandSpawner.LoadPool(true);
			}
			Log("P2GEN generated " + Gf(CustomIslandSpawner.GeneratedWeight, "F0") + " flying " + Gf(CustomIslandSpawner.GeneratedFlyingChance) + " gather " + Gf(CustomIslandSpawner.GeneratedGather) + " shallows " + Gf(CustomIslandSpawner.GeneratedShallows) +
				", generator window radius " + Gf(IslandGenerator.Last.Radius, "F0") + ", presets [" + string.Join(" ", GeneratorWindow.PresetNames().ToArray()) + "] (" + GWho() + ")");
			Log("PASS: p2 gen " + what);
		}

		[ConsoleCommand(name: "CIGapsIslandFile", docs: "Dev, in game (either player), IM24/IM31: the island file this machine uses for a world island (by the host's name) and what is in it: CIGapsIslandFile <island>; logs ISLFILE <name> uses <file> entry-hash H file-hash F own-same-name-hash O objects N creatures C beast B title 'T' loaded yes|no")]
		public static void GapsIslandFileCommand(string[] args)
		{
			string name = args != null ? string.Join(" ", args) : "";
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name || (string.IsNullOrEmpty(i.HostName) && i.Name == name));
			if (e == null) { Log("ISLFILE " + name + " not in this machine's list (" + GWho() + ")"); Fail("no island '" + name + "' in the list here"); return; }
			string used = e.Name;
			string path = IslandSpawner.PathFor(used);
			IslandFile f = null;
			try { if (System.IO.File.Exists(path)) f = IslandFile.Load(path); } catch { }
			string fileHash = f != null ? IslandNetwork.HashOf(used) : "-";
			string own = System.IO.File.Exists(IslandSpawner.PathFor(name)) ? IslandNetwork.HashOf(name) : "-";
			IslandObject beast = f == null ? null : f.Objects.Where(o => o.Name.StartsWith("Creature_")).OrderByDescending(o => ObjectProps.GetFloat(o.Props, ObjectProps.CreatureHealth, 1f)).FirstOrDefault();
			Log("ISLFILE " + name + " uses " + used + " entry-hash " + (string.IsNullOrEmpty(e.Hash) ? "-" : e.Hash) + " file-hash " + (string.IsNullOrEmpty(fileHash) ? "-" : fileHash) + " own-same-name-hash " + (string.IsNullOrEmpty(own) ? "-" : own) +
				" objects " + (f != null ? f.Objects.Count : -1) + " creatures " + (f != null ? f.Objects.Count(o => o.Name.StartsWith("Creature_")) : -1) +
				" beast " + (beast != null ? beast.Name + "x" + Gf(ObjectProps.GetFloat(beast.Props, ObjectProps.CreatureHealth, 1f), "F1") : "-") +
				" title '" + (f != null ? ObjectProps.Get(f.Props, IslandProps.Title) : "-") + "' loaded " + (e.Root != null ? "yes" : "no") + (e.WaitingForFile ? " (waiting for its file)" : "") + " (" + GWho() + ")");
			Log("PASS: island file");
		}

		[ConsoleCommand(name: "CIGapsCleanFiles", docs: "Dev, anywhere: deletes the test islands' files this suite made - cigaps*.island in this PC's island folder (its own copies and the <name>_<hash> downloads): CIGapsCleanFiles")]
		public static void GapsCleanFilesCommand()
		{
			int n = 0;
			try
			{
				foreach (string p in System.IO.Directory.GetFiles(DynamicIslands.assetpath, "cigaps*" + IslandFile.Extension)) { System.IO.File.Delete(p); n++; }
			}
			catch (Exception e) { Log("clean: " + e.Message); }
			Log("CLEAN " + n + " file(s) (" + GWho() + ")");
			Log("PASS: clean files");
		}

		#endregion

		#region IM25: the culture and the host's numbers as this machine reads them

		[ConsoleCommand(name: "CIGapsCulture", docs: "Dev, anywhere, IM25: the culture this machine runs under now and the host's shared numbers as it reads them (to compare between players): logs CULTURE <name> sample '<1.5 as written>' unload U rdist R regrow D receiver shown|hidden")]
		public static void GapsCultureCommand()
		{
			var c = System.Threading.Thread.CurrentThread.CurrentCulture;
			Log("CULTURE " + c.Name + " sample '" + 1.5f.ToString() + "' unload " + Gf(WorldRules.UnloadDistance) + " rdist " + Gf(WorldRules.ReceiverDistance) + " regrow " + WorldRules.RegrowDays + " receiver " + (WorldRules.ShowOnReceiver ? "shown" : "hidden") + " (" + GWho() + ")");
			Log("PASS: culture now");
		}

		#endregion

		#region IM31: one island of an oddity kind, the lair or a large island

		[ConsoleCommand(name: "CIGapsKind", docs: "Dev, in game (host), IM31: brings one island of a kind the randomizer brings - an oddity (van, caravan, planecrash, boatwreck, shack, statue, rocket, hut), lair or large - to the nearest free sea and waits for it to load; it stays: CIGapsKind <type>; logs KIND <type> <island> loaded yes|no")]
		public static void GapsKindCommand(string[] args)
		{
			if (!Raft_Network.IsHost || args == null || args.Length == 0) { Fail("CIGapsKind <type>, as the host"); return; }
			StartTest(GapsKindRoutine(args[0]));
		}

		static IEnumerator GapsKindRoutine(string type)
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || MapTypes.Get(type) == null) { Fail("kind " + type + ": no raft or no such map type"); yield break; }
			IslandWorldState.Entry e = SpawnTypeNear(type, raft.Value);
			if (e == null) { Fail("kind " + type + ": no free sea near the raft"); yield break; }
			for (float t = 0; e.Root == null && !e.Failed && t < 240f; t += 1f) yield return new WaitForSeconds(1f);
			Log("KIND " + type + " " + (string.IsNullOrEmpty(e.HostName) ? e.Name : e.HostName) + " loaded " + (e.Root != null ? "yes" : "no") + (e.Failed ? " (failed)" : ""));
			if (e.Root != null) Log("PASS: kind " + type); else Fail("kind " + type + " did not load");
		}

		#endregion

		#region IM36: a moved crate or a hoard taken by both players at the same moment

		[ConsoleCommand(name: "CIGapsMovedKeys", docs: "Dev, world (either player), IM36: the keys (island/item) of Raft's moved crates and clams still there on this machine, in key order: CIGapsMovedKeys [n]; logs MOVEDKEYS k1 k2 ..")]
		public static void GapsMovedKeysCommand(string[] args)
		{
			int n;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out n)) n = 8;
			var keys = MovedLoot().Where(x => x.Value.gameObject.activeInHierarchy && x.Value.GetComponentInChildren<PickupItem>() != null).Select(x => x.Key).OrderBy(k => k, StringComparer.Ordinal).Take(n).ToArray();
			Log("MOVEDKEYS " + string.Join(" ", keys) + " (" + GWho() + ")");
			Log("PASS: moved keys");
		}

		[ConsoleCommand(name: "CIGapsPickItem", docs: "Dev, world (either player), IM36: goes to one named moved crate or clam and picks it up as a player would at a set moment, then counts what came into the inventory: CIGapsPickItem <island/item> [@<epoch ms>]; logs PICKITEM <key> got N gone yes|no")]
		public static void GapsPickItemCommand(string[] args)
		{
			long at;
			string[] a = GcSplitAt(args, out at);
			if (a.Length == 0) { Fail("CIGapsPickItem <island/item> [@epochMs]"); return; }
			StartTest(GapsPickItemRoutine(a[0], at));
		}

		static IEnumerator GapsPickItemRoutine(string key, long at)
		{
			KeyValuePair<string, LandmarkItem> m = MovedLoot().FirstOrDefault(x => x.Key == key);
			Network_Player me = RAPI.GetLocalPlayer();
			PickupItem item = m.Value != null ? m.Value.GetComponentInChildren<PickupItem>() : null;
			if (m.Value == null || item == null || me == null) { Fail("no moved crate or clam " + key + " here (" + GWho() + ")"); yield break; }
			Pickup pickup = me.GetComponentInChildren<Pickup>(true);
			PutPlayerNear(item.transform);
			yield return new WaitForSeconds(1.5f);
			int before = GcInvTotal(me.Inventory);
			while (at > 0 && GcNowMs() < at) yield return null;
			long fired = GcNowMs();
			if (item != null) pickup.PickupItemByType(item, true);
			yield return new WaitForSeconds(6f);
			int after = GcInvTotal(me.Inventory);
			bool there = m.Value != null && m.Value.gameObject.activeInHierarchy && item != null && item.gameObject.activeInHierarchy;
			Log("PICKITEM " + key + " got " + (after - before) + " gone " + (there ? "no" : "yes") + " fired " + fired + " (" + GWho() + ")");
			Log("PASS: pick item " + key);
		}

		[ConsoleCommand(name: "CIGapsOpenAt", docs: "Dev, in game (either player), IM36: opens a chest of a custom island (by its title, as CIOpenChest does) at a set moment: CIGapsOpenAt <island> <chest title or number> [@<epoch ms>]; logs Opened chest: got ..")]
		public static void GapsOpenAtCommand(string[] args)
		{
			long at;
			string[] a = GcSplitAt(args, out at);
			IslandWorldState.Entry e = LoadedIsland(a);
			if (e == null) return;
			LootCrate[] chests = e.Root.GetComponentsInChildren<LootCrate>(false).OrderBy(c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null ? r.Index : 0; }).ToArray();
			string title = a.Length > 1 ? string.Join(" ", a.Skip(1).ToArray()) : "";
			int n;
			Func<LootCrate, string> titleOf = c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null ? ObjectProps.Get(r.Props, ObjectProps.NoteTitle) : ""; };
			LootCrate chest = title.Length == 0 ? chests.FirstOrDefault(c => !c.Looted)
				: int.TryParse(title, out n) ? chests.ElementAtOrDefault(n - 1)
				: chests.FirstOrDefault(c => titleOf(c).Equals(title, StringComparison.OrdinalIgnoreCase));
			if (chest == null) { Fail("no such chest on '" + e.HostName + "' (" + chests.Length + " chests)"); return; }
			StartTest(GapsOpenAtRoutine(e, chest, at));
		}

		static IEnumerator GapsOpenAtRoutine(IslandWorldState.Entry e, LootCrate chest, long at)
		{
			PutPlayerNear(chest.transform);
			yield return new WaitForSeconds(1.5f);
			while (at > 0 && GcNowMs() < at) yield return null;
			yield return OpenChestRoutine(e, chest);
		}

		#endregion
	}
}

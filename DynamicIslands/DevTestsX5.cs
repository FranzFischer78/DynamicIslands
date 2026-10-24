using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Tests for ROADMAP X5: the world file read and written as a whole (IslandWorldState) - a world saved by an older version
	/// (before the world rules, the level up system, the randomizer, the world's options and its islands list), the older
	/// forms of those lines, a file written by a newer version, and every one of them on at once in one world.
	/// Each test writes the world's file as it wants it, reads it as when the world loads (OnWorldLoaded) and saves it as
	/// Raft's save does, then puts the world's own file back.
	/// </summary>
	public static partial class DevTests
	{
		/// <summary>The world file's lines that belong to one of the world's settings (not the stamps a save adds).</summary>
		static readonly string[] X5SettingKeys = { "monsters", "buildcost", "builtat", "regrow", "levels", "level", "randomizer", "rndsailed", "rndseen", "options", "optionseed", "optionsused", "islandsoff", "raftgap", "raftgapcount", "playtime" };
		// (@libseen: when the world last looked for library updates of its islands - each load sets it)
		static readonly string[] X5StampKeys = { "savedat", "savecount", "between", "raftsave", "savedby", "modversion", "libseen" };

		/// <summary>Null when the test can run here: host, in a saved test world 'CI ...' (loaded, not created this session) without custom islands.</summary>
		static string X5Where(string what)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ") || IslandWorldState.Islands.Count > 0)
				return what + ": host, in a test world 'CI ...' without custom islands (here: " + (!LoadSceneManager.IsGameSceneLoaded ? "no world" : !Raft_Network.IsHost ? "not the host"
					: "world '" + SaveAndLoad.CurrentGameFileName + "', " + IslandWorldState.Islands.Count + " custom islands: " + string.Join(", ", IslandWorldState.Islands.Take(5).Select(e => e.Name).ToArray())) + ")";
			bool isNew = false;
			try { isNew = GameManager.IsInNewGame; } catch { }
			// (a world created this session reads its file as a new world's: the rules, options and islands chosen for it)
			if (isNew) return what + ": a saved test world loaded from Raft's Load Game box, not one created this session";
			return null;
		}

		/// <summary>The world's file as it is now (null: it has none).</summary>
		static string[] X5Backup() { string f = IslandWorldState.WorldFilePath; return File.Exists(f) ? File.ReadAllLines(f) : null; }

		/// <summary>
		/// These lines become the world's file and are read as when the world loads. The save counter is put above every
		/// copy's, so this file is the copy chosen (WorldCopy: the copy in Raft's world folder is newer otherwise).
		/// </summary>
		static void X5Load(IEnumerable<string> lines)
		{
			List<string> all = lines.Where(l => !l.StartsWith("@savecount=")).ToList();
			all.Add("@savecount=" + (WorldCopy.SaveCount + 1000).ToString(CultureInfo.InvariantCulture));
			string f = IslandWorldState.WorldFilePath;
			Directory.CreateDirectory(Path.GetDirectoryName(f));
			File.WriteAllLines(f, all.ToArray());
			IslandWorldState.OnWorldLoaded();
		}

		/// <summary>The world's own file back (or none, as it had), read and saved again so Raft's world folder's copy follows.</summary>
		static void X5Restore(string[] before)
		{
			try
			{
				X5Load(before ?? new[] { "@auto=on" });
				IslandWorldState.Save();
			}
			catch (Exception e) { Fail("putting the world's file back: " + e); }
		}

		static string[] X5Saved() { string f = IslandWorldState.WorldFilePath; return File.Exists(f) ? File.ReadAllLines(f) : new string[0]; }

		static string X5Key(string line) { int eq = line.IndexOf('='); return line.StartsWith("@") && eq > 1 ? line.Substring(1, eq - 1).ToLowerInvariant() : null; }

		static string[] X5Settings(string[] lines) { return lines.Where(l => X5SettingKeys.Contains(X5Key(l) ?? "")).ToArray(); }

		/// <summary>Every line of a save but its stamps and comments (what must come back the same after reading it again).</summary>
		static string[] X5Content(string[] lines) { return lines.Where(l => !l.StartsWith("#") && l.Trim().Length > 0 && !X5StampKeys.Contains(X5Key(l) ?? "")).ToArray(); }

		/// <summary>The same lines, in any order (players' records follow a dictionary's order).</summary>
		static bool X5Same(string[] a, string[] b) { return a.Length == b.Length && !a.Except(b).Any() && !b.Except(a).Any(); }

		/// <summary>What changed from the first save to the second (empty when nothing did).</summary>
		static string X5Diff(string[] first, string[] again)
		{
			string[] lost = first.Except(again).ToArray(), added = again.Except(first).ToArray();
			return lost.Length + added.Length == 0 ? "" : " - lost " + string.Join("  ", lost) + "; new " + string.Join("  ", added);
		}

		static IslandWorldState.Entry X5Island(string name) { return IslandWorldState.Islands.FirstOrDefault(e => e.HostName == name); }

		#region A world saved by an older version

		[ConsoleCommand(name: "CIOldWorldFile", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): a world file from before the world rules, levels, randomizer, options and the islands list (only islands - name|x|y|z, some with an empty state - and @auto) reads as Raft's own rules, everything off, every island taking part, long played; its islands are there without rule, label or hash; this version's save invents no setting, writes its version and the islands in today's form, and reads back the same; @auto=off is kept. The world's file is put back after (ROADMAP X5)")]
		public static void OldWorldFileCommand()
		{
			string where = X5Where("old world file");
			if (where != null) { Fail(where); return; }
			bool ok = true;
			string[] before = X5Backup();
			try
			{
				// (islands 50 km away: read, never loaded during the test)
				X5Load(new[]
				{
					"# Custom islands in world '" + SaveAndLoad.CurrentGameFileName + "': name|x|y|z",
					"@auto=on",
					"ciold_a|50000|0|50000",
					"ciold_b|-50000|2.5|50000|",
				});
				IslandWorldState.Entry a = X5Island("ciold_a"), b = X5Island("ciold_b");
				Check(ref ok, a != null && b != null, "both islands of the old file are in the world's list (" + IslandWorldState.Islands.Count + ")");
				if (a != null && b != null)
				{
					Check(ref ok, (a.Position - new Vector3(50000f, 0f, 50000f)).magnitude < 0.01f && (b.Position - new Vector3(-50000f, 2.5f, 50000f)).magnitude < 0.01f, "at their places (" + a.Position + ", " + b.Position + ")");
					Check(ref ok, a.Rule == "" && a.Label == "" && a.Hash == null && a.State.Count == 0 && !a.WaitingForFile && b.Rule == "" && b.Hash == null && b.State.Count == 0,
						"without rule, Receiver label, hash or used objects, not waiting for a file");
				}
				Check(ref ok, CustomIslandSpawner.Enabled, "automatic islands on (@auto=on)");
				Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Normal && BuildCost.Current == 0, "Raft's own rules: monsters " + MonsterDifficulty.Name(MonsterDifficulty.Current) + ", build cost " + BuildCost.Describe(BuildCost.Current));
				Check(ref ok, BuildCostRefund.Base == 0 && BuildCostRefund.Count == 0, "every block built so far gives back Raft's own (base " + BuildCostRefund.Base + "%, " + BuildCostRefund.Count + " noted)");
				Check(ref ok, WorldRules.RegrowDays == CustomIslandSpawner.RegrowDays, "the world takes this PC's regrow days (" + WorldRules.RegrowDays + ")");
				Check(ref ok, !PlayerLevels.On && !PlayerLevels.OffByHost, "the level up system off (not switched off by a host)");
				Check(ref ok, !WorldRandomizer.Current.On, "the randomizer off (" + WorldRandomizer.Current.Describe() + ")");
				Check(ref ok, WorldOptions.Current.Count == 0 && WorldOptions.Seed == 0, "no world options (" + WorldOptions.Describe() + ")");
				Check(ref ok, WorldIslands.Off.Count == 0 && WorldIslands.GapMin == WorldIslands.GapDefaultMin && WorldIslands.GapMax == WorldIslands.GapDefaultMax,
					"every island of the pool takes part, one after every " + WorldIslands.GapText(WorldIslands.GapMin, WorldIslands.GapMax) + " of Raft's");
				Check(ref ok, WorldIslands.PlaySeconds >= WorldIslands.LongPlayed, "the world counts as long played (no quiet first minutes)");

				// This version's save
				IslandWorldState.Save();
				string[] saved = X5Saved();
				// (but the regrow days: a world without its own takes this PC's and keeps them from then on - WorldRules.OnWorldRead)
				string[] invented = X5Settings(saved).Where(l => l != "@regrow=" + CustomIslandSpawner.RegrowDays.ToString(CultureInfo.InvariantCulture)).ToArray();
				Check(ref ok, invented.Length == 0, "the save invents no setting" + (invented.Length > 0 ? ": " + string.Join("  ", invented) : ""));
				Check(ref ok, saved.Contains("@modversion=" + LibraryPack.ModVersion) && saved.Contains("@auto=on"), "it writes @auto=on and this version (" + LibraryPack.ModVersion + ")");
				string la = saved.FirstOrDefault(l => l.StartsWith("ciold_a|")), lb = saved.FirstOrDefault(l => l.StartsWith("ciold_b|"));
				Check(ref ok, la != null && lb != null && la.Split('|').Length == 8 && lb.Split('|').Length == 8, "both islands written in today's form: " + la + "  " + lb);

				// Read again: the same
				string[] content = X5Content(saved);
				IslandWorldState.OnWorldLoaded();
				IslandWorldState.Entry a2 = X5Island("ciold_a"), b2 = X5Island("ciold_b");
				Check(ref ok, a2 != null && b2 != null && a2.Rule == "" && a2.Label == "" && b2.State.Count == 0 && !PlayerLevels.On && !WorldRandomizer.Current.On && WorldOptions.Current.Count == 0 &&
					MonsterDifficulty.Current == MonsterDifficulty.Normal && BuildCost.Current == 0, "read back: the same islands, everything still off");
				IslandWorldState.Save();
				string[] again = X5Content(X5Saved());
				Check(ref ok, X5Same(again, content), "saved again: the same lines (" + content.Length + ")" + X5Diff(content, again));

				// @auto=off of an old file
				X5Load(new[] { "@auto=off", "ciold_a|50000|0|50000" });
				Check(ref ok, !CustomIslandSpawner.Enabled && X5Island("ciold_a") != null, "@auto=off: automatic islands stay off");
				IslandWorldState.Save();
				Check(ref ok, X5Saved().Contains("@auto=off"), "and are saved off");
			}
			catch (Exception e) { Fail("old world file: " + e); ok = false; }
			finally { X5Restore(before); }
			if (ok) Log("PASS: old world file"); else Fail("old world file");
		}

		[ConsoleCommand(name: "CIOldWorldLines", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): the older and hand-written forms of each setting's line read as meant - @monsters=Savage, @buildcost=40% without @builtat (blocks built before were built at 40%), a level record of eight stats without kills, a randomizer level by number, @rndsailed without the large islands' distance, options with spaces, islands left out; this version's save writes them in today's form, and they read back the same. The randomizer is on with every part off (nothing changes on Raft's islands); the world's file is put back after (ROADMAP X5)")]
		public static void OldWorldLinesCommand()
		{
			string where = X5Where("old world lines");
			if (where != null) { Fail(where); return; }
			bool ok = true;
			string[] before = X5Backup();
			const string oldRecord = "300|1,0,0,0,0,0,0,0";
			const string allOff = "colours,animals,alphas,loot,finds,oddities,bosses,large";
			try
			{
				X5Load(new[]
				{
					"@auto=on",
					"@monsters=Savage",
					"@buildcost=40%",
					"@levels=on",
					"@level=4242|" + oldRecord,
					"@randomizer=level=2;seed=77;off=" + allOff,
					"@rndsailed=1500,4000",
					"@options=blueprints, ghostrafts",
					"@optionseed=31337",
					"@islandsoff=ciold_x|type:wreck",
				});
				Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Savage, "@monsters=Savage: " + MonsterDifficulty.Name(MonsterDifficulty.Current));
				Check(ref ok, BuildCost.Current == 40, "@buildcost=40%: " + BuildCost.Describe(BuildCost.Current));
				Check(ref ok, BuildCostRefund.Base == 40 && BuildCostRefund.PercentOf(123456u) == 40, "no @builtat: the blocks built before were built at 40% (base " + BuildCostRefund.Base + "%)");
				LevelRecord r = PlayerLevels.RecordOf(4242UL);
				Check(ref ok, PlayerLevels.On && r != null && r.Xp == 300 && r.Points.Length == LevelRules.StatCount && r.Points[0] == 1 && r.Kills == 0 && r.Level == 3,
					"a level record of eight stats, no kills: on, " + (r != null ? r.Xp + " EXP, level " + r.Level + ", " + r.Points.Length + " stats" : "no record"));
				RandomizerSettings rs = WorldRandomizer.Current;
				Check(ref ok, rs.Level == RandomizerSettings.Normal && rs.Seed == 77 && rs.Disabled.Count == RandomizerSettings.Features.Length && RandomizerSettings.Features.All(f => !rs.Has(f)),
					"a randomizer level by number: " + rs.Describe() + " (seed " + rs.Seed + ")");
				Check(ref ok, WorldOptions.On(WorldOptions.Blueprints) && WorldOptions.On(WorldOptions.GhostRafts) && WorldOptions.Current.Count == 2 && WorldOptions.Seed == 31337,
					"options with spaces: " + WorldOptions.Describe());
				Check(ref ok, WorldIslands.Off.Count == 2 && !WorldIslands.TakesPart("CIOLD_X") && !WorldIslands.TakesPart("type:wreck"), "islands left out: " + WorldIslands.Describe());

				IslandWorldState.Save();
				string[] saved = X5Saved();
				string record = "@level=4242|" + LevelRecord.Decode(oldRecord).Encode();
				string[] want =
				{
					"@monsters=savage", "@buildcost=40", "@builtat=40|", "@levels=on", record,
					"@randomizer=level=normal;seed=77;off=" + allOff, "@rndsailed=1500,4000,0",
					"@options=blueprints,ghostrafts", "@optionseed=31337", "@optionsused=1", "@islandsoff=ciold_x|type:wreck",
				};
				string[] missing = want.Where(w => !saved.Contains(w)).ToArray();
				Check(ref ok, missing.Length == 0, "saved in today's form" + (missing.Length > 0 ? " - missing " + string.Join("  ", missing) + " (saved: " + string.Join("  ", X5Settings(saved)) + ")" : ""));

				string[] content = X5Content(saved);
				IslandWorldState.OnWorldLoaded();
				r = PlayerLevels.RecordOf(4242UL);
				Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Savage && BuildCost.Current == 40 && BuildCostRefund.Base == 40 && PlayerLevels.On && r != null && r.Xp == 300 &&
					WorldRandomizer.Current.Level == RandomizerSettings.Normal && WorldRandomizer.Current.Seed == 77 && WorldOptions.Current.Count == 2 && WorldOptions.Seed == 31337 && WorldIslands.Off.Count == 2,
					"read back: the same settings");
				IslandWorldState.Save();
				string[] again = X5Content(X5Saved());
				Check(ref ok, X5Same(again, content), "saved again: the same lines" + X5Diff(content, again));
			}
			catch (Exception e) { Fail("old world lines: " + e); ok = false; }
			finally { X5Restore(before); }
			if (ok) Log("PASS: old world lines"); else Fail("old world lines");
		}

		[ConsoleCommand(name: "CILevelBadLines", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): a hand-edited world file's bad @level lines read without errors and clamped - no id, a bad or too long id, words for numbers, more points than the level gives, points over the most a stat takes and below 0, no kills field, negative EXP and kills; the save writes only good records. The world's file is put back after (ROADMAP X5, IL23)")]
		public static void LevelBadLinesCommand()
		{
			string where = X5Where("level bad lines");
			if (where != null) { Fail(where); return; }
			bool ok = true;
			string[] before = X5Backup();
			int rich = LevelRules.TotalFor(40); // (enough levels for every point a stat takes)
			try
			{
				X5Load(new[]
				{
					"@auto=on",
					"@levels=on",
					"@level=",
					"@level=4240",
					"@level=abc|100|1|1",
					"@level=99999999999999999999999|100|1|1",
					"@level=4241|xyz|q,w|e",
					"@level=4242|300|9,9,9,9,9,9,9,9,9|5",
					"@level=" + "4243|" + rich + "|99,-5,3|7",
					"@level=4244|300|1",
					"@level=4245|-50|0|-3",
					"@level=4246|" + rich + "|1,1,1,1,1,1,1,1,1,1,1,1|2",
				});
				Check(ref ok, PlayerLevels.On, "the system is on");
				Check(ref ok, PlayerLevels.RecordOf(4240UL) == null, "a line without a record is left out");
				LevelRecord a = PlayerLevels.RecordOf(4241UL), b = PlayerLevels.RecordOf(4242UL), c = PlayerLevels.RecordOf(4243UL), d = PlayerLevels.RecordOf(4244UL), e = PlayerLevels.RecordOf(4245UL), g = PlayerLevels.RecordOf(4246UL);
				Check(ref ok, a != null && a.Xp == 0 && a.Spent == 0 && a.Kills == 0, "words for numbers: a fresh record" + (a != null ? " (" + a.Encode() + ")" : ""));
				Check(ref ok, b != null && b.Spent == LevelRules.PointsAt(b.Level) && b.Points[0] == LevelRules.PointsAt(b.Level) && b.Kills == 5,
					"more points than level " + (b != null ? b.Level : 0) + " gives: cut to " + (b != null ? b.Encode() : "no record"));
				Check(ref ok, c != null && c.Points[0] == LevelRules.MaxPoints && c.Points[1] == 0 && c.Points[2] == 3 && c.Kills == 7,
					"points over " + LevelRules.MaxPoints + " and below 0 clamped: " + (c != null ? c.Encode() : "no record"));
				Check(ref ok, d != null && d.Xp == 300 && d.Points[0] == 1 && d.Kills == 0, "no kills field: 0 kills (" + (d != null ? d.Encode() : "no record") + ")");
				Check(ref ok, e != null && e.Xp == 0 && e.Kills == 0 && e.Level == 1, "negative EXP and kills: 0 (" + (e != null ? e.Encode() : "no record") + ")");
				Check(ref ok, g != null && g.Points.Length == LevelRules.StatCount && g.Spent == LevelRules.StatCount, "more stats than this version has: the extra left out (" + (g != null ? g.Encode() : "no record") + ")");

				IslandWorldState.Save();
				string[] levels = X5Saved().Where(l => l.StartsWith("@level=") && !l.StartsWith("@level=" + RAPI.GetLocalPlayer().steamID.Id.ToString(CultureInfo.InvariantCulture) + "|")).ToArray(); // (the host's own record may come too)
				string[] want = new[] { a, b, c, d, e, g }.Zip(new[] { 4241, 4242, 4243, 4244, 4245, 4246 }, (r, id) => r == null ? "" : "@level=" + id + "|" + r.Encode()).ToArray();
				Check(ref ok, levels.Length == want.Length && want.All(w => levels.Contains(w)), "saved: only the good records, in today's form (" + string.Join("  ", levels) + ")");
				IslandWorldState.OnWorldLoaded();
				Check(ref ok, new[] { 4241UL, 4242UL, 4243UL, 4244UL, 4245UL, 4246UL }.All(id => PlayerLevels.RecordOf(id) != null) && PlayerLevels.RecordOf(4243UL).Encode() == c.Encode(), "read back the same");
			}
			catch (Exception ex) { Fail("level bad lines: " + ex); ok = false; }
			finally { X5Restore(before); }
			if (ok) Log("PASS: level bad lines"); else Fail("level bad lines");
		}

		#endregion

		#region A world saved by a newer version

		[ConsoleCommand(name: "CINewerWorldFile", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): a world file written by a newer version (@modversion=99.0) reads: what this version knows is read (@monsters, an island line), a setting it doesn't know is kept and written back as it was, an island line with a field more than this version writes isn't lost at the next save, a broken line doesn't stop the rest; the save names this version. The world's file is put back after (ROADMAP X5, AU5)")]
		public static void NewerWorldFileCommand()
		{
			string where = X5Where("newer world file");
			if (where != null) { Fail(where); return; }
			bool ok = true;
			string[] before = X5Backup();
			const string future = "@cifuture=kept|as it is;a=1";
			try
			{
				X5Load(new[]
				{
					"@auto=on",
					"@modversion=99.0",
					future,
					"this line is broken",
					"@monsters=fierce",
					"cinew_a|50000|0|50000||rule1|Label A|",
					"cinew_b|-50000|0|50000||||abc|a field from a newer version",
				});
				IslandWorldState.Entry a = X5Island("cinew_a");
				Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Fierce, "a setting this version knows is read (" + MonsterDifficulty.Name(MonsterDifficulty.Current) + ")");
				Check(ref ok, a != null && a.Rule == "rule1" && a.Label == "Label A", "an island line is read past a broken line (" + (a != null ? a.Rule + ", " + a.Label : "missing") + ")");

				IslandWorldState.Save();
				string[] saved = X5Saved();
				Check(ref ok, saved.Contains(future), "the setting this version doesn't know is written back as it was");
				Check(ref ok, saved.Contains("@modversion=" + LibraryPack.ModVersion) && !saved.Contains("@modversion=99.0"), "the save names this version (" + LibraryPack.ModVersion + ")");
				Check(ref ok, saved.Contains("@monsters=fierce") && saved.Any(l => l.StartsWith("cinew_a|")), "what it knows is saved");
				Check(ref ok, X5Island("cinew_b") != null || saved.Any(l => l.StartsWith("cinew_b|")),
					"an island line with a field this version doesn't write (a newer version's) isn't lost at the save - " + (saved.Any(l => l.StartsWith("cinew_b|")) ? "kept" : "dropped as a bad line, and the save erased it"));
			}
			catch (Exception e) { Fail("newer world file: " + e); ok = false; }
			finally { X5Restore(before); }
			if (ok) Log("PASS: newer world file"); else Fail("newer world file");
		}

		#endregion

		#region Everything on at once

		[ConsoleCommand(name: "CIEverythingOn", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): every world setting on in one world - monsters Nightmare, build cost +35%, regrow 7 days, the level up system with two players' records, the randomizer Wild with every part, all four world options, an island left out and one every 5-12 of Raft's - saved in one file with each line, read back all together as when the world loads, sent whole to a joining player (rules, options, levels), and saved again unchanged. The randomizer's extras are removed and the world's file put back after (ROADMAP X5)")]
		public static void EverythingOnCommand()
		{
			string where = X5Where("everything on");
			if (where != null) { Fail(where); return; }
			bool ok = true;
			string[] before = X5Backup();
			const string guest = "1234|0,0,1,1,1,0,0,0,0|3";
			try
			{
				// Everything on, as the host's commands do (each saves the world)
				MonsterDifficulty.Set(MonsterDifficulty.Nightmare);
				BuildCost.Set(35);
				WorldRules.SetRegrow(7);
				PlayerLevels.SetEnabled(true);
				PlayerLevels.ReadLine("level", "4242|" + guest);
				PlayerLevels.SetMine(new LevelRecord { Xp = 345, Points = new[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, Kills = 12 });
				WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 4711 });
				WorldOptions.Set(WorldOptions.All);
				int optionSeed = WorldOptions.Seed;
				WorldIslands.GapMin = 5; WorldIslands.GapMax = 12;
				WorldIslands.Set("cix5_none", false);
				LevelRecord mine = PlayerLevels.Mine;
				Check(ref ok, PlayerLevels.On && mine != null && mine.Xp == 345, "everything switched on (" + WorldRules.Describe() + "; " + WorldRandomizer.Current.Describe() + "; " + WorldOptions.Describe() + ")");

				string[] first = X5Saved();
				string[] want =
				{
					"@monsters=nightmare", "@buildcost=35", "@regrow=7", "@levels=on", "@level=4242|" + guest,
					"@randomizer=level=wild;seed=4711", "@options=" + string.Join(",", WorldOptions.All), "@optionseed=" + optionSeed.ToString(CultureInfo.InvariantCulture),
					"@raftgap=5-12", "@auto=" + (CustomIslandSpawner.Enabled ? "on" : "off"), "@modversion=" + LibraryPack.ModVersion,
				};
				// (the world may leave out more: a new world takes the New Game box's last choice)
				string[] missing = want.Where(w => !first.Contains(w)).Concat(first.Any(l => l.StartsWith("@islandsoff=") && WorldIslands.Parse(l.Substring(12)).Contains("cix5_none")) ? new string[0] : new[] { "@islandsoff=...cix5_none" }).ToArray();
				Check(ref ok, missing.Length == 0, "one file with every setting's line" + (missing.Length > 0 ? " - missing " + string.Join("  ", missing) : ""));
				Check(ref ok, first.Any(l => l.StartsWith("@rndsailed=")) && mine != null && first.Contains("@level=" + RAPI.GetLocalPlayer().steamID.Id.ToString(CultureInfo.InvariantCulture) + "|" + mine.Encode()),
					"with the randomizer's distances and the host's own level record");
				string[] doubled = X5Settings(first).Where(l => X5Key(l) != "level").GroupBy(X5Key).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
				Check(ref ok, doubled.Length == 0, "no setting written twice" + (doubled.Length > 0 ? ": " + string.Join(", ", doubled) : ""));

				// Read back all together, as when the world loads
				string[] content = X5Content(first);
				IslandWorldState.OnWorldLoaded();
				LevelRecord g2 = PlayerLevels.RecordOf(4242UL), m2 = PlayerLevels.Mine;
				Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Nightmare && BuildCost.Current == 35 && WorldRules.RegrowDays == 7, "the rules back: " + WorldRules.Describe() + ", regrow " + WorldRules.RegrowDays + " days");
				Check(ref ok, PlayerLevels.On && g2 != null && g2.Encode() == guest && m2 != null && m2.Xp == 345 && m2.Kills == 12, "the level up system back with both records (" + (m2 != null ? m2.Encode() : "no own record") + ")");
				RandomizerSettings rs = WorldRandomizer.Current;
				Check(ref ok, rs.Level == RandomizerSettings.Wild && rs.Seed == 4711 && rs.Disabled.Count == 0, "the randomizer back: " + rs.Describe() + " (seed " + rs.Seed + ")");
				Check(ref ok, WorldOptions.All.All(WorldOptions.On) && WorldOptions.Seed == optionSeed, "every option back: " + WorldOptions.Describe());
				Check(ref ok, !WorldIslands.TakesPart("cix5_none") && WorldIslands.GapMin == 5 && WorldIslands.GapMax == 12, "the islands list back: " + WorldIslands.Describe() + ", " + WorldIslands.DescribeGap());

				// What a joining player is sent
				IslandNetMessage rules = WorldRules.Message(), options = WorldOptions.Message(), levels = PlayerLevels.StateFor(4242UL);
				Check(ref ok, rules.Index == MonsterDifficulty.Nightmare && rules.Count == 35 && rules.Name == BuildCostRefund.Encode(), "the rules message: monsters " + rules.Index + ", build cost " + rules.Count + ", built at " + rules.Name);
				Check(ref ok, options.Data == WorldOptions.Encode(WorldOptions.All, optionSeed), "the options message: " + options.Data);
				Check(ref ok, levels != null && levels.Name == "state" && levels.Data == guest, "the joining player's level record: " + (levels != null ? levels.Data : "none"));

				// Saved again: nothing lost, doubled or changed
				IslandWorldState.Save();
				string[] again = X5Content(X5Saved());
				string[] lost = content.Except(again).ToArray(), added = again.Except(content).ToArray();
				Check(ref ok, X5Same(again, content),
					"saved again: the same " + content.Length + " lines" + (lost.Length + added.Length > 0 ? " - lost " + string.Join("  ", lost) + "; new " + string.Join("  ", added) : ""));
			}
			catch (Exception e) { Fail("everything on: " + e); ok = false; }
			finally
			{
				// (as CIRandomizerClean: the extras out of the world, Raft's crates back where Raft put them)
				try
				{
					IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(WorldRandomizer.IsExtras).Select(e => e.Id).ToList(), true);
					WorldRandomizer.Set(new RandomizerSettings());
					WorldRandomizer.ForgetSeen();
					PlayerLevels.TurnOff();
				}
				catch (Exception e) { Fail("everything on, randomizer off: " + e); }
				X5Restore(before);
			}
			if (ok) Log("PASS: everything on"); else Fail("everything on");
		}

		#endregion
	}
}

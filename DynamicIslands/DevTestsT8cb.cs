using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// T8 (story items taken out as Raft items), CB1 (a recipe's decorative copies stay decoration) and CB2 (what the host
	/// takes from another player: reports only from near the island, EXP only as credited).
	/// </summary>
	public static partial class DevTests
	{
		const string T8Id = "ci-t8-plank", T8Plain = "ci-t8-plain", T8Raft = "Plank";

		[ConsoleCommand(name: "CIT8Items", docs: "Dev, in game (host): T8 - a story item that names a Raft item: the definition line and the world file keep it (old lines read as before), the journal's Take out button, taking out (the Raft item in the inventory, one less in the journal), a player's take-out asked of the host, and none left")]
		public static void T8ItemsCommand(string[] args) { StartTest(T8ItemsRoutine()); }

		static IEnumerator T8ItemsRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			yield return EnsureAlive();
			// (a new world can be up a little before Raft counts the game scene as loaded - the journal closes itself until then - and
			// before the mod notices the new world, which starts the story book afresh: wait for both first)
			for (float t = 0f; t < 60f && !(LoadSceneManager.IsGameSceneLoaded && WorldDirector.WorldHandled); t += Time.unscaledDeltaTime) yield return null;
			player = RAPI.GetLocalPlayer();
			if (player == null || !Raft_Network.IsHost) { Fail("run in a world, as the host (player " + (player != null) + ", host " + Raft_Network.IsHost + ")"); yield break; }
			bool ok = true;

			// The definition line: the Raft item at the end; a line from before (a '|' in its description) reads as before
			StoryItemDef d = StoryItemDef.Parse(T8Id + "|Old plank|" + T8Raft + "|A plank from the wreck|raft=" + T8Raft);
			Check(ref ok, d != null && d.RaftItem == T8Raft && d.Description == "A plank from the wreck", "the line's raft= field is the Raft item (" + (d != null ? d.RaftItem + " / " + d.Description : "null") + ")");
			StoryItemDef back = StoryItemDef.Parse(d.ToLine());
			Check(ref ok, back.RaftItem == T8Raft && back.Description == d.Description && back.Copy().RaftItem == T8Raft, "ToLine and Copy keep it (" + d.ToLine() + ")");
			StoryItemDef old = StoryItemDef.Parse("old-one|Old one|quest:x|Says a|b|c");
			Check(ref ok, old.RaftItem == "" && old.Description == "Says a|b|c" && !old.ToLine().Contains("raft="), "a line from before reads as before (" + old.Description + ")");

			// Given (as a chest gives it): held with its Raft item; a plain one has no Take out
			StoryBook.Take(T8Id, 99);
			StoryBook.Take(T8Plain, 99);
			GiveDef(d, 2);
			GiveDef(new StoryItemDef { Id = T8Plain, Name = "Plain thing" }, 1);
			Check(ref ok, StoryBook.Count(T8Id) == 2 && StoryBook.DefOf(T8Id).RaftItem == T8Raft, "the crew holds 2, the Raft item known (" + StoryBook.Count(T8Id) + ")");
			Check(ref ok, StoryBook.CanTakeOut(T8Id) && !StoryBook.CanTakeOut(T8Plain) && !StoryBook.CanTakeOut("ci-none"), "only the one with a Raft item can be taken out");

			// The world file keeps the Raft item (and a line from before has none)
			string line = StoryBook.WriteLines().FirstOrDefault(l => l.StartsWith("@story.item=" + T8Id));
			Check(ref ok, line != null && line.EndsWith("|" + T8Raft), "the world's line ends with the Raft item (" + line + ")");

			// The journal: Take out on that item, not on the plain one
			JournalWindow.Open();
			yield return null; yield return null;
			Check(ref ok, JournalWindow.ShowItemOf(T8Id), "the journal shows the item (open " + JournalWindow.IsOpen + ", held " + StoryBook.Items.Count(x => x.Def != null && x.Def.Id == T8Id) + " of " + StoryBook.Items.Count() + ")");
			string label = JournalWindow.TakeOutLabel;
			Check(ref ok, label != null && label.StartsWith("Take out"), "its Take out button shows (" + label + ")");
			JournalWindow.ShowItemOf(T8Plain);
			Check(ref ok, JournalWindow.TakeOutLabel == null, "none on a plain story item");

			// Taken out from the journal: a plank in the inventory, one less in the journal
			int planks = player.Inventory.GetItemCount(T8Raft);
			JournalWindow.ShowItemOf(T8Id);
			JournalWindow.ClickTakeOut();
			yield return null; yield return null; yield return null;
			Check(ref ok, StoryBook.Count(T8Id) == 1, "one left in the journal (" + StoryBook.Count(T8Id) + ")");
			Check(ref ok, player.Inventory.GetItemCount(T8Raft) == planks + 1, "a plank in the inventory (" + planks + " -> " + player.Inventory.GetItemCount(T8Raft) + ")");

			// A player's take-out asked of the host (as their message comes): one taken here; then none left: refused
			StoryBook.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Story, Name = "out", Data = Uri.EscapeDataString(T8Id) }, 4242UL, null);
			Check(ref ok, StoryBook.Count(T8Id) == 0, "a player's take-out takes the last one (" + StoryBook.Count(T8Id) + ")");
			StoryBook.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Story, Name = "out", Data = Uri.EscapeDataString(T8Id) }, 4242UL, null);
			Check(ref ok, StoryBook.Count(T8Id) == 0, "with none left, it is refused (" + StoryBook.Count(T8Id) + ")");
			Check(ref ok, !StoryBook.TakeOut(T8Id) && player.Inventory.GetItemCount(T8Raft) == planks + 1, "nothing to take out: no plank");
			yield return null; yield return null;
			Check(ref ok, JournalWindow.TakeOutLabel == null, "no Take out once none is held");
			JournalWindow.Close();
			StoryBook.Take(T8Plain, 99);
			if (ok) Log("PASS: t8 items"); else Fail("t8 items");
		}

		/// <summary>Gives a story item with its definition, as a chest on its island does.</summary>
		static void GiveDef(StoryItemDef d, int count)
		{
			// (StoryBook.Give looks the definition up on the islands; a test item isn't on one: the give carries it as the network does)
			StoryBook.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Story, Name = "give", Data = string.Join("|", new[] { d.Id, count.ToString(), d.Name, d.Icon, d.Description, d.RaftItem }.Select(x => Uri.EscapeDataString(x ?? "")).ToArray()) }, 4242UL, null);
		}

		[ConsoleCommand(name: "CICB1Recipe", docs: "Dev, the editor (opened when needed): CB1 - a recipe's ready piece placed without an alias is decoration (none of the Ready pieces' settings), one given an alias works as a ready piece; ObjectProps.Defaults(name, false) for every ready piece")]
		public static void CB1RecipeCommand(string[] args) { StartTest(CB1RecipeRoutine()); }

		static IEnumerator CB1RecipeRoutine()
		{
			bool ok = true;
			// Every ready piece: the list's defaults have its settings, the decorative ones none of them
			int withReady = 0;
			foreach (ReadyPieces.Piece rp in ReadyPieces.All)
			{
				var ready = new Dictionary<string, string>();
				ReadyPieces.Defaults(rp.Name, ready);
				if (ready.Count == 0) continue;
				withReady++;
				Dictionary<string, string> list = ObjectProps.Defaults(rp.Name), plain = ObjectProps.Defaults(rp.Name, false);
				bool listHas = ready.Keys.All(k => list.ContainsKey(k)), decoNone = ready.Keys.All(k => !plain.ContainsKey(k));
				if (!listHas || !decoNone) { ok = false; Log("  " + rp.Name + ": list " + listHas + ", decoration without " + decoNone); }
			}
			Check(ref ok, withReady > 10, withReady + " ready pieces have settings: on from the list, off as decoration");

			// A recipe: the same piece twice, once without an alias and once with one
			ReadyPieces.Piece piece = ReadyPieces.All.FirstOrDefault(rp => { var r = new Dictionary<string, string>(); ReadyPieces.Defaults(rp.Name, r); return r.Count > 0 && rp.Name.IndexOf("Generator", StringComparison.OrdinalIgnoreCase) >= 0; })
				?? ReadyPieces.All.First(rp => { var r = new Dictionary<string, string>(); ReadyPieces.Defaults(rp.Name, r); return r.Count > 0; });
			var keys = new Dictionary<string, string>();
			ReadyPieces.Defaults(piece.Name, keys);
			string path = Path.Combine(RecipeFolder, "ci_cb1.recipe");
			try
			{
				Directory.CreateDirectory(RecipeFolder);
				File.WriteAllLines(path, new[]
				{
					"# CB1 check (CICB1Recipe): a ready piece as decoration and as a working piece",
					"new",
					"gen Seed=1 Radius=40 Height=4 Shape=4 Roughness=0.1 ObjectDensity=0 Trees=0 Bushes=0 Rocks=0 BeachThings=0 Harvest=0",
					"gen Hostiles=0 Friendly=0 SeaLife=0 Water=0 SeaRocks=0 SeaFinds=0 Sunken=0",
					"generate",
					"brush flatten at 0 0 r=35 s=20 frames=30",
					"place " + piece.Name + " - at -6 0 ground",
					"place " + piece.Name + " cbworking at 6 0 ground",
				});
			}
			catch (Exception e) { Fail("cb1 recipe: writing " + path + ": " + e.Message); yield break; }
			yield return RecipeRoutine("ci_cb1");
			try { File.Delete(path); } catch { }
			EditorGameObject working;
			RecipeObjects.TryGetValue("cbworking", out working);
			EditorGameObject deco = UnityEngine.Object.FindObjectsOfType<EditorGameObject>().FirstOrDefault(x => x != working && x.GameObjectName == piece.Name);
			Check(ref ok, working != null && deco != null, "both copies of " + piece.Name + " are on the island");
			if (working != null && deco != null)
			{
				Check(ref ok, keys.Keys.All(k => working.Props.ContainsKey(k)), "the one with an alias has the ready piece's settings (" + string.Join(", ", keys.Keys.ToArray()) + ")");
				Check(ref ok, keys.Keys.All(k => !deco.Props.ContainsKey(k)), "the one without stays decoration (" + string.Join(", ", deco.Props.Keys.ToArray()) + ")");
			}
			if (ok) Log("PASS: cb1 recipe"); else Fail("cb1 recipe");
		}

		const string TrustNear = "citrustnear", TrustFar = "citrustfar";
		const int TrustUsedFar = 0x7701, TrustUsedNear = 0x7702;

		[ConsoleCommand(name: "CITrust", docs: "Dev, two players (CB2, T8): CITrust setup (host: the host's checks on, levels on, an island near the raft and one 5 km off, 2 story planks) | send <near> <far> (player 2: forged reports - used, harvest, quest step, fired rule - on the far island, a used on the near one) | check <near> <far> (host: the far ones refused, the near one taken, player 2's EXP not more than credited) | level (player 2: put right to the host's EXP) | out (player 2: takes a story plank out) | outcheck (host) | on (host: back to the CI default)")]
		public static void TrustCommand(string[] args) { StartTest(TrustRoutine(args ?? new string[0])); }

		static IEnumerator TrustRoutine(string[] args)
		{
			string what = args.Length > 0 ? args[0] : "";
			int near = 0, far = 0;
			if (args.Length > 2) { int.TryParse(args[1], out near); int.TryParse(args[2], out far); }
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("run in a world"); yield break; }
			bool ok = true;
			switch (what)
			{
				case "setup":
				{
					if (!Raft_Network.IsHost) { Fail("setup: as the host"); yield break; }
					// (a new world: wait until the mod has set it up - that start resets the islands, levels and story book)
					for (float t = 0f; t < 60f && !(LoadSceneManager.IsGameSceneLoaded && WorldDirector.WorldHandled); t += Time.unscaledDeltaTime) yield return null;
					PlayerTrust.ForTests = false;
					PlayerTrust.ResetCount();
					if (!PlayerLevels.On) PlayerLevels.TurnOn(false);
					Vector3? raft = CustomIslandSpawner.RaftPosition;
					if (!raft.HasValue) { Fail("no raft"); yield break; }
					var s = new IslandGenSettings { Seed = 7171, Radius = 30f, Height = 8f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
					foreach (string n in new[] { TrustNear, TrustFar })
					{
						IslandWorldState.Remove(n);
						IslandGenerator.CreateFile(s, n).Save(IslandSpawner.PathFor(n));
					}
					Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(TrustNear), 300f);
					if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
					yield return DynamicIslands.instance.SpawnIslandFile(TrustNear, spot.Value, true);
					yield return DynamicIslands.instance.SpawnIslandFile(TrustFar, raft.Value + new Vector3(5000f, 0f, 0f), true);
					IslandWorldState.Entry en = IslandWorldState.Islands.LastOrDefault(i => i.HostName == TrustNear), ef = IslandWorldState.Islands.LastOrDefault(i => i.HostName == TrustFar);
					if (en == null || ef == null) { Fail("the islands did not come"); yield break; }
					StoryBook.Take(T8Id, 99);
					GiveDef(new StoryItemDef { Id = T8Id, Name = "Old plank", Icon = T8Raft, Description = "A plank from the wreck", RaftItem = T8Raft }, 2);
					Log("TRUST ids " + en.Id + " " + ef.Id);
					Log("PASS: trust setup (near " + en.Id + ", far " + ef.Id + ", " + PlayerTrust.Allowed(ef).ToString("F0") + " m allowed)");
					yield break;
				}
				case "send":
					if (Raft_Network.IsHost) { Fail("send: as player 2"); yield break; }
					IslandNetwork.SendUsed(far, TrustUsedFar, 5);
					IslandNetwork.SendHarvest(far, 3, false, 0, 5);
					IslandNetwork.SendQuestAdd(far, 0, 1);
					IslandNetwork.SendEvent(far, 0, "use", false);
					IslandNetwork.SendUsed(near, TrustUsedNear, 5);
					Log("PASS: trust sent (far " + far + ", near " + near + ")");
					yield break;
				case "check":
				{
					if (!Raft_Network.IsHost) { Fail("check: as the host"); yield break; }
					yield return new WaitForSeconds(3f);
					IslandWorldState.Entry en = IslandWorldState.Islands.FirstOrDefault(i => i.Id == near), ef = IslandWorldState.Islands.FirstOrDefault(i => i.Id == far);
					Check(ref ok, PlayerTrust.Refused == 4, "the far island's 4 reports are refused (" + PlayerTrust.Refused + ": " + PlayerTrust.LastRefused + ")");
					Check(ref ok, ef != null && !ContentState.IsUsed(ef, TrustUsedFar), "the far island's use is not marked");
					Check(ref ok, en != null && ContentState.IsUsed(en, TrustUsedNear), "the near island's use is marked");
					Network_Player other = Players.All.FirstOrDefault(p => p != null && p.steamID.Id != player.steamID.Id);
					LevelRecord rec = other != null ? PlayerLevels.RecordOf(other.steamID.Id) : null;
					Check(ref ok, PlayerLevels.Corrected >= 1 && rec != null && rec.Xp == 0, "player 2's EXP given on their side is put right (" + (rec != null ? rec.Xp + " EXP" : "no record") + ", " + PlayerLevels.Corrected + " corrected)");
					if (ok) Log("PASS: trust check"); else Fail("trust check");
					yield break;
				}
				case "level":
					yield return new WaitForSeconds(2f);
					Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == 0, "player 2's record is the host's again (" + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp + " EXP" : "none") + ")");
					if (ok) Log("PASS: trust level"); else Fail("trust level");
					yield break;
				case "out":
				{
					if (Raft_Network.IsHost) { Fail("out: as player 2"); yield break; }
					int planks = player.Inventory.GetItemCount(T8Raft);
					Check(ref ok, StoryBook.CanTakeOut(T8Id), "player 2 can take the story plank out (" + StoryBook.Count(T8Id) + " held)");
					StoryBook.TakeOut(T8Id);
					yield return new WaitForSeconds(3f);
					Check(ref ok, player.Inventory.GetItemCount(T8Raft) == planks + 1, "the host's yes gives player 2 a plank (" + planks + " -> " + player.Inventory.GetItemCount(T8Raft) + ")");
					Check(ref ok, StoryBook.Count(T8Id) == 1, "one left for the crew on player 2's side (" + StoryBook.Count(T8Id) + ")");
					if (ok) Log("PASS: trust out"); else Fail("trust out");
					yield break;
				}
				case "outcheck":
					Check(ref ok, StoryBook.Count(T8Id) == 1, "one left for the crew on the host (" + StoryBook.Count(T8Id) + ")");
					if (ok) Log("PASS: trust outcheck"); else Fail("trust outcheck");
					yield break;
				case "on":
					PlayerTrust.ForTests = true;
					Log("PASS: trust on (CI default)");
					yield break;
			}
			Fail("CITrust setup | send <near> <far> | check <near> <far> | level | out | outcheck | on");
		}
	}
}

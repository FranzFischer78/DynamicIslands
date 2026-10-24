using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Test gaps 2026-10-13 (TEST_GAPS_2026-10-13.md), story side: a full story set played in a world with a world save and
	/// read in the middle (CIStorySetsPlay), the behaviour verbs wait / take (CIBehaviourWait), the character verb across
	/// a reload (CICharacterReload), extra upgrades crafted through Raft's craft menu (CIUpgradesCraft), the player-facing
	/// console commands with bad arguments (CIConsoleArgs). The two-player helpers CIGs* are driven by
	/// _ProjectDocs/tools/mpstoryplay.ps1.
	/// </summary>
	public static partial class DevTests
	{
		#region Shared helpers (Gst)

		const float GstWait1 = 6f, GstWait2 = 5f;

		static StoryItemDef GstDef(string id, string name, string icon, string text)
		{
			return new StoryItemDef { Id = id, Name = name, Icon = StoryItems.QuestIcon + icon, Description = text };
		}

		/// <summary>
		/// An island file for these tests (the sample island, 25 m round its middle cleared), objects by their names:
		/// sets = the locked door and its key, the note trail, the treasure map (StorySets' props, closer together);
		/// wait = a lever whose chain waits twice, a box that needs a small key (take), a lever that takes 3 planks;
		/// charIndex >= 0 = a lever that unlocks that character.
		/// </summary>
		static IslandFile GstBuild(string name, bool sets, bool wait, int charIndex)
		{
			IslandFile f = SampleIsland();
			f.Name = name;
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			float step = f.TerrainSize.x / (f.HeightmapResolution - 1);
			Func<float, float, Vector3> g = (dx, dz) =>
			{
				float x = c.x + dx, z = c.y + dz;
				return new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			};
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 25f);
			var defs = new List<StoryItemDef>();
			if (sets)
			{
				defs.Add(GstDef("rusty-key", "Rusty key", "Caravan_KeyInfirmary", "An old key. It must open a door somewhere on this island."));
				defs.Add(GstDef("treasure-map", "Treasure map", "Vasagatan_FourDigitCode", "A torn map with an X on it. The X is on this island."));
				defs.Add(GstDef("captains-log", "Captain's log", "Vasagatan_Recorder", "The last pages of the captain's log."));
				// The locked door and its key (StorySets.LockedDoor)
				f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = g(-9f, -4f), Props = P(BehaviourProps.Name, "locked-door", BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "0.5", BehaviourProps.Use, "Open the door",
					BehaviourProps.CheckKey("use"), "has|story:rusty-key|1",
					BehaviourProps.ElseKey("use"), "message||It's locked. Maybe there's a key somewhere...",
					BehaviourProps.EventKey("use"), "switch||\nmessage||The rusty key turns in the lock.") });
				f.Objects.Add(new IslandObject { Name = "Loot_ChestSmall", Position = g(-5f, -2f), Props = P(BehaviourProps.Name, "key-chest", ObjectProps.LootItems, "story:rusty-key*1", ObjectProps.NoteTitle, "Key chest") });
				f.Objects.Add(new IslandObject { Name = "Note_Paper", Position = g(-6f, 0f), Props = P(ObjectProps.NoteTitle, "Door note", ObjectProps.NoteText, "I locked the door and hid the key in the chest. Nobody gets in without it.") });
				// The note trail (StorySets.NoteTrail)
				f.Objects.Add(new IslandObject { Name = "Note_Bottle", Position = g(-3f, -9f), Props = P(ObjectProps.NoteTitle, "Trail bottle", ObjectProps.NoteText, "If you find this, walk towards the rising sun. I left more for you there.") });
				f.Objects.Add(new IslandObject { Name = "Note_Paper", Position = g(2f, -9f), Props = P(ObjectProps.NoteTitle, "Trail page", ObjectProps.NoteText, "You're getting closer. Look to the north: an open book waits by the rocks.") });
				f.Objects.Add(new IslandObject { Name = "Note_Book", Position = g(2f, -5f), Props = P(ObjectProps.NoteTitle, "Trail diary", ObjectProps.NoteText, "Here it is. The chest behind me holds what is left of my log.",
					BehaviourProps.EventKey("read"), "show|hidden-chest|\njournal|The captain's trail|Three notes led to a hidden chest behind the open book.") });
				f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = g(4f, -3f), Props = P(BehaviourProps.Name, "hidden-chest", BehaviourProps.Hidden, "1", ObjectProps.LootItems, "story:captains-log*1;Plank*6", ObjectProps.NoteTitle, "Hidden chest") });
				// The treasure map (StorySets.TreasureMap)
				f.Objects.Add(new IslandObject { Name = "Note_Bottle", Position = g(-1f, 9f), Props = P(ObjectProps.NoteTitle, "Map bottle", ObjectProps.NoteText, "Rolled up inside the bottle is a map. Someone marked a spot on this island with an X.",
					BehaviourProps.EventKey("read"), "give||story:treasure-map*1") });
				f.Objects.Add(new IslandObject { Name = "Loot_ChestSmall", Position = g(9f, 4f), Props = P(BehaviourProps.Name, "buried-treasure", BehaviourProps.Hidden, "1", ObjectProps.LootItems, "Scrap*6;Nail*12;Plank*8", ObjectProps.NoteTitle, "Buried chest") });
				f.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = g(9f, 4f), Props = P(ObjectProps.ZoneId, "x-marks-the-spot", ObjectProps.ZoneRadius, "5", ObjectProps.ZoneRepeat, "1",
					BehaviourProps.CheckKey("enter"), "has|story:treasure-map|1\n!state|buried-treasure|shown",
					BehaviourProps.EventKey("enter"), "show|buried-treasure|\nmessage||The map leads here: something is buried under the sand!") });
			}
			if (wait)
			{
				defs.Add(GstDef("small-key", "Small key", "Vasagatan_BlueKey", "A small key, the kind that opens a box."));
				f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = g(-9f, 6f), Props = P(BehaviourProps.Name, "chain-door", BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "0.5") });
				f.Objects.Add(new IslandObject { Name = "Log", Position = g(-5f, 7f), Props = P(BehaviourProps.Name, "late-a", BehaviourProps.Hidden, "1") });
				f.Objects.Add(new IslandObject { Name = "Log", Position = g(-3f, 7f), Props = P(BehaviourProps.Name, "late-b", BehaviourProps.Hidden, "1") });
				f.Objects.Add(new IslandObject { Name = "Log", Position = g(-7f, 8f), Props = P(BehaviourProps.Name, "chain", BehaviourProps.Use, "Start the chain",
					BehaviourProps.EventKey("use"), "switch|chain-door|\nmessage||The chain starts\nwait||" + GstWait1.ToString("0") + "\nshow|late-a|\nwait||" + GstWait2.ToString("0") + "\nshow|late-b|\nsignal||chain-done") });
				f.Objects.Add(new IslandObject { Name = "Loot_ChestSmall", Position = g(0f, 1f), Props = P(BehaviourProps.Name, "locked-box", ObjectProps.LootItems, "Scrap*2", ObjectProps.NoteTitle, "Locked box",
					BehaviourProps.CheckKey("open"), "take|story:small-key|1", BehaviourProps.ElseKey("open"), "message||The box is locked.") });
				f.Objects.Add(new IslandObject { Name = "Log", Position = g(-2f, 4f), Props = P(BehaviourProps.Name, "toll", BehaviourProps.Use, "Pay the toll",
					BehaviourProps.CheckKey("use"), "take|Plank|3", BehaviourProps.ElseKey("use"), "message||Not enough planks.",
					BehaviourProps.EventKey("use"), "message||Paid.\nsignal||toll-paid") });
			}
			if (charIndex >= 0)
				f.Objects.Add(new IslandObject { Name = "Log", Position = g(4f, 8f), Props = P(BehaviourProps.Name, "unlock", BehaviourProps.Use, "Unlock", BehaviourProps.EventKey("use"), "character||" + charIndex) });
			if (defs.Count > 0) f.Props[StoryItems.Key] = StoryItems.Text(defs);
			f.Save(IslandSpawner.PathFor(name));
			return f;
		}

		static bool GstShown(IslandWorldState.Entry e, string name) { IslandObjectRef r = ScObjOf(e, name); return r != null && r.gameObject.activeSelf; }

		static bool GstOpen(IslandWorldState.Entry e, string name)
		{
			IslandObjectRef r = ScObjOf(e, name);
			IslandBehaviour b = r != null ? r.GetComponent<IslandBehaviour>() : null;
			return b != null && b.Target >= 0.5f;
		}

		static LootCrate GstChestOf(IslandWorldState.Entry e, string title)
		{
			return e != null && e.Root != null ? e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c =>
			{
				IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>();
				return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle).Equals(title, StringComparison.OrdinalIgnoreCase);
			}) : null;
		}

		static bool GstLooted(IslandWorldState.Entry e, string title) { LootCrate c = GstChestOf(e, title); return c != null && c.Looted; }

		/// <summary>Pending shared actions of a wait chain kept in the island's state.</summary>
		static int GstPending(IslandWorldState.Entry e)
		{
			return e.State.Keys.Count(k => k >= Behaviours.PendingBase && k < Behaviours.PendingBase + 0x100000);
		}

		static void GstSetItem(Network_Player p, string item, int count)
		{
			int have = p.Inventory.GetItemCount(item);
			if (have > count) p.Inventory.RemoveItem(item, have - count);
			else if (have < count) p.Inventory.AddItem(item, count - have);
		}

		/// <summary>
		/// A world save and a world read, as leaving a world and loading it again does for the mod: every island unloaded
		/// (its state captured), the world file written (IslandWorldState.Save), read back (OnWorldLoaded: story book,
		/// plan, island list and each island's state from the file) and the islands loaded again. 'made' is refilled with
		/// the new entries (same host names, new ids).
		/// </summary>
		static IEnumerator GstReload(List<IslandWorldState.Entry> made)
		{
			List<string> names = made.Where(m => m != null).Select(m => m.HostName).ToList();
			foreach (IslandWorldState.Entry x in IslandWorldState.Islands.ToList())
				if (x.Root != null) { IslandObjectState.Capture(x); IslandSpawner.Despawn(x.Root); x.Root = null; }
			yield return null;
			IslandWorldState.Save();
			yield return null;
			made.Clear();
			IslandWorldState.OnWorldLoaded();
			yield return null;
			foreach (string n in names)
			{
				IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.HostName == n);
				if (e == null) { Fail("world read: island '" + n + "' is not in the world file"); continue; }
				made.Add(e);
				float t0 = Time.realtimeSinceStartup;
				while (e.Root == null && !e.Failed && Time.realtimeSinceStartup - t0 < 30f)
				{
					// (the spawner streams islands near the raft in by itself; one it left alone for 3 s is loaded here)
					if (!e.Loading && Time.realtimeSinceStartup - t0 > 3f) { e.Loading = true; yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e); }
					else yield return new WaitForSeconds(0.25f);
				}
			}
			yield return new WaitForSeconds(0.5f);
			Log("World saved and read back: " + string.Join(", ", made.Select(m => m.HostName + (m.Root != null ? " (loaded)" : " (NOT loaded)")).ToArray()));
		}

		#endregion

		#region CIStorySetsPlay

		[ConsoleCommand(name: "CIStorySetsPlay", docs: "Dev, world (host, a test world 'CI ...'): the three story sets played end to end on one island (the sample island with StorySets' locked door + key chest + note, the note trail with its hidden chest and journal page, the treasure map with its zone and buried chest): the door stays locked without the key, opens with it; the trail's diary shows the hidden chest only after the three notes are read; the map's zone does nothing until the bottle gave the map. In the middle of the note trail the world is saved and read back (every island unloaded, IslandWorldState.Save, OnWorldLoaded, islands loaded again): the key, the open door, the hidden chest still hidden, the crew's story items kept; the trail then finishes, a second save and read at the end: the open door, the log, the map, the shown and opened chests, the journal page")]
		public static void StorySetsPlayCommand() { StartTest(StorySetsPlayRoutine()); }

		static IEnumerator StorySetsPlayRoutine()
		{
			if (!CvInTestWorld("story sets play")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("story sets play: run as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			const string isl = "cigsets";
			var made = new List<IslandWorldState.Entry>();
			int scrap0 = player.Inventory.GetItemCount("Scrap"), nail0 = player.Inventory.GetItemCount("Nail"), plank0 = player.Inventory.GetItemCount("Plank");
			ResetStoryEverywhere();
			try
			{
				GstBuild(isl, true, false, -1);
				Vector3? spot = ScSpot(isl, 320f);
				if (!spot.HasValue) { Fail("story sets play: no open sea near the raft"); yield break; }
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("story sets play: the island didn't come"); yield break; }
				yield return new WaitForSeconds(1f);

				// 1. The locked door: shut without the key
				Check(ref ok, ScObjOf(e, "locked-door") != null && ScObjOf(e, "hidden-chest") != null && ScObjOf(e, "buried-treasure") != null, "the three sets are on the island");
				Check(ref ok, !GstShown(e, "hidden-chest") && !GstShown(e, "buried-treasure"), "the hidden chest and the buried chest start hidden");
				ScUse(e, "locked-door");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, !GstOpen(e, "locked-door") && (Behaviours.LastMessage ?? "").Contains("locked"), "without the key the door stays shut and says so (" + Behaviours.LastMessage + ")");
				// 2. The key chest and the note beside it
				ScReadNote(e, "Door note");
				List<string> got = ScOpenChest(e, "Key chest");
				yield return new WaitForSeconds(0.5f);
				NoteReader.Close();
				Check(ref ok, GstLooted(e, "Key chest") && StoryBook.Count("rusty-key") == 1, "the key chest gave the crew the rusty key (" + StoryBook.Count("rusty-key") + "; got " + (got == null ? "nothing" : string.Join(", ", got.ToArray())) + ")");
				ScUse(e, "locked-door");
				yield return new WaitForSeconds(1.2f);
				Check(ref ok, GstOpen(e, "locked-door") && StoryBook.Count("rusty-key") == 1, "with the key the door opens and the key is not used up");

				// 3. The note trail, two of three notes read
				ScReadNote(e, "Trail bottle");
				ScReadNote(e, "Trail page");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, !GstShown(e, "hidden-chest") && !StoryBook.Pages.Any(p => p.Title == "The captain's trail"), "two notes of the trail read: the hidden chest is still hidden, no journal page yet");

				// 4. The world is saved and read back in the middle of the trail
				int pages0 = StoryBook.Pages.Count;
				yield return GstReload(made);
				e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("story sets play: the island didn't come back after the world was read"); yield break; }
				yield return new WaitForSeconds(1f);
				Check(ref ok, StoryBook.Count("rusty-key") == 1 && StoryBook.Pages.Count == pages0, "after the save and read: the crew still has the key (" + StoryBook.Count("rusty-key") + "), the journal as it was (" + StoryBook.Pages.Count + " pages)");
				Check(ref ok, GstOpen(e, "locked-door") && GstLooted(e, "Key chest"), "... the door is still open and the key chest opened");
				Check(ref ok, !GstShown(e, "hidden-chest"), "... the hidden chest is still hidden");

				// 5. The trail finishes after the reload
				ScReadNote(e, "Trail diary");
				yield return new WaitForSeconds(0.8f);
				Check(ref ok, GstShown(e, "hidden-chest"), "the diary, read after the reload, shows the hidden chest");
				Check(ref ok, StoryBook.Pages.Any(p => p.Title == "The captain's trail"), "... and the journal page 'The captain's trail' is there");
				got = ScOpenChest(e, "Hidden chest");
				yield return new WaitForSeconds(0.5f);
				NoteReader.Close();
				Check(ref ok, GstLooted(e, "Hidden chest") && StoryBook.Count("captains-log") == 1, "the hidden chest gave the captain's log (" + StoryBook.Count("captains-log") + ")");

				// 6. The treasure map: the zone does nothing before the map
				ScEnterZone(e, "x-marks-the-spot");
				yield return new WaitForSeconds(0.8f);
				Check(ref ok, !GstShown(e, "buried-treasure"), "the map's zone entered without the map: the chest stays buried");
				ScReadNote(e, "Map bottle");
				yield return new WaitForSeconds(0.8f);
				Check(ref ok, StoryBook.Count("treasure-map") == 1, "the bottle gave the crew the treasure map (" + StoryBook.Count("treasure-map") + ")");
				ScEnterZone(e, "x-marks-the-spot");
				yield return new WaitForSeconds(0.8f);
				Check(ref ok, GstShown(e, "buried-treasure"), "with the map the zone shows the buried chest");
				got = ScOpenChest(e, "Buried chest");
				yield return new WaitForSeconds(0.5f);
				NoteReader.Close();
				Check(ref ok, GstLooted(e, "Buried chest") && player.Inventory.GetItemCount("Scrap") - scrap0 >= 6, "the buried chest gave its scrap, nails and planks (scrap +" + (player.Inventory.GetItemCount("Scrap") - scrap0) + ")");

				// 7. A second save and read at the end: all of it kept
				yield return GstReload(made);
				e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("story sets play: the island didn't come back after the second read"); yield break; }
				yield return new WaitForSeconds(1f);
				Check(ref ok, StoryBook.Count("rusty-key") == 1 && StoryBook.Count("captains-log") == 1 && StoryBook.Count("treasure-map") == 1, "after the second read: key, log and map are all still the crew's");
				Check(ref ok, GstOpen(e, "locked-door") && GstShown(e, "hidden-chest") && GstShown(e, "buried-treasure"), "... the door open, both chests shown");
				Check(ref ok, GstLooted(e, "Hidden chest") && GstLooted(e, "Buried chest") && GstLooted(e, "Key chest"), "... all three chests still opened");
				Check(ref ok, StoryBook.Pages.Any(p => p.Title == "The captain's trail"), "... the journal page is still there");
			}
			finally
			{
				ScRemove(made, isl);
				ResetStoryEverywhere();
				foreach (string[] it in new[] { new[] { "Scrap" }, new[] { "Nail" }, new[] { "Plank" } })
				{
					int before = it[0] == "Scrap" ? scrap0 : it[0] == "Nail" ? nail0 : plank0;
					GstSetItem(player, it[0], before);
				}
				try { OnRaftCommand(); } catch { }
			}
			if (ok) Log("PASS: story sets play"); else Fail("story sets play");
		}

		#endregion

		#region CIBehaviourWait

		[ConsoleCommand(name: "CIBehaviourWait", docs: "Dev, world (host, a test world 'CI ...'): the 'wait' and 'take' verbs. A lever with switch, wait 6, show, wait 5, show + signal: the first part runs at once, the later parts are kept in the island's state (pending); the world is saved and read back during the first wait (islands unloaded, IslandWorldState.Save, OnWorldLoaded): the chain goes on after the island loads (the part after the first wait now, the last after its wait), the door is not switched twice, the pending state is cleared at the end. 'take': a box that needs a small story key - shut and the key kept without it, the key used up by opening it; a lever that takes 3 planks from the player - refused with 2 (planks kept), 3 taken from 5. The player's planks and the crew's items are put back")]
		public static void BehaviourWaitCommand() { StartTest(BehaviourWaitRoutine()); }

		static IEnumerator BehaviourWaitRoutine()
		{
			if (!CvInTestWorld("behaviour wait")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("behaviour wait: run as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			const string isl = "cigswait";
			var made = new List<IslandWorldState.Entry>();
			int plank0 = player.Inventory.GetItemCount("Plank"), scrap0 = player.Inventory.GetItemCount("Scrap");
			ResetStoryEverywhere();
			try
			{
				GstBuild(isl, false, true, -1);
				Vector3? spot = ScSpot(isl, 320f);
				if (!spot.HasValue) { Fail("behaviour wait: no open sea near the raft"); yield break; }
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("behaviour wait: the island didn't come"); yield break; }
				yield return new WaitForSeconds(1f);

				// 1. The chain: the first part at once, the rest kept as pending
				Check(ref ok, !GstOpen(e, "chain-door") && !GstShown(e, "late-a") && !GstShown(e, "late-b") && !Behaviours.HasSignal(e, "chain-done") && GstPending(e) == 0, "before: door shut, nothing shown, no signal, nothing pending");
				ScUse(e, "chain");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, GstOpen(e, "chain-door"), "the part before the first wait ran at once: the door opened");
				Check(ref ok, !GstShown(e, "late-a") && !GstShown(e, "late-b") && !Behaviours.HasSignal(e, "chain-done"), "the parts after the waits have not run yet");
				Check(ref ok, GstPending(e) >= 1, "the rest of the chain is kept in the island's state (" + GstPending(e) + " pending)");

				// 2. A world save and read during the first wait
				yield return new WaitForSeconds(1f);
				yield return GstReload(made);
				e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("behaviour wait: the island didn't come back"); yield break; }
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, GstShown(e, "late-a"), "after the save and read the chain went on: the part after the first wait ran (late-a shown)");
				Check(ref ok, !GstShown(e, "late-b") && !Behaviours.HasSignal(e, "chain-done"), "... and the last part still waits (late-b hidden, no signal)");
				Check(ref ok, GstOpen(e, "chain-door"), "... the door is still open (the first part did not run a second time)");
				yield return new WaitForSeconds(GstWait2 + 3f);
				Check(ref ok, GstShown(e, "late-b") && Behaviours.HasSignal(e, "chain-done"), "the chain finished: late-b shown and the signal 'chain-done' sent");
				Check(ref ok, GstOpen(e, "chain-door") && GstPending(e) == 0, "... the door still open (switched once), nothing pending any more (" + GstPending(e) + ")");

				// 3. take, a story item: the crew's key used up
				ScOpenChest(e, "Locked box");
				yield return new WaitForSeconds(0.5f);
				NoteReader.Close();
				Check(ref ok, !GstLooted(e, "Locked box") && (Behaviours.LastMessage ?? "").Contains("locked"), "the box without the small key stays shut and says so (" + Behaviours.LastMessage + ")");
				StoryBook.Give("small-key", 1);
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, StoryBook.Count("small-key") == 1, "the crew finds a small key");
				ScOpenChest(e, "Locked box");
				yield return new WaitForSeconds(0.5f);
				NoteReader.Close();
				Check(ref ok, GstLooted(e, "Locked box") && StoryBook.Count("small-key") == 0, "opening the box used the key up (keys left " + StoryBook.Count("small-key") + ") and the box is open");
				// (told only: what an opened box does with another key)
				StoryBook.Give("small-key", 1);
				yield return new WaitForSeconds(0.3f);
				ScOpenChest(e, "Locked box");
				yield return new WaitForSeconds(0.5f);
				NoteReader.Close();
				Log("  an opened box opened again with a key in hand: keys left " + StoryBook.Count("small-key") + " (1 = the key was not wasted)");
				if (StoryBook.Count("small-key") > 0) StoryBook.Take("small-key", StoryBook.Count("small-key"));

				// 4. take, Raft's planks: from the player's inventory
				GstSetItem(player, "Plank", 2);
				ScUse(e, "toll");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, player.Inventory.GetItemCount("Plank") == 2 && !Behaviours.HasSignal(e, "toll-paid") && (Behaviours.LastMessage ?? "").Contains("Not enough"), "the toll with 2 planks: refused, the planks kept (" + player.Inventory.GetItemCount("Plank") + "), no signal");
				GstSetItem(player, "Plank", 5);
				yield return new WaitForSeconds(1.2f);
				ScUse(e, "toll");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, player.Inventory.GetItemCount("Plank") == 2 && Behaviours.HasSignal(e, "toll-paid"), "the toll with 5 planks: 3 taken (" + player.Inventory.GetItemCount("Plank") + " left) and the signal sent");
			}
			finally
			{
				ScRemove(made, isl);
				ResetStoryEverywhere();
				GstSetItem(player, "Plank", plank0);
				GstSetItem(player, "Scrap", scrap0);
				try { OnRaftCommand(); } catch { }
			}
			if (ok) Log("PASS: behaviour wait"); else Fail("behaviour wait");
		}

		#endregion

		#region CICharacterReload

		/// <summary>One of Raft's characters that can be locked again (from the last), locked now; null when none.</summary>
		static SO_Character GstLockableCharacter(out bool wasUnlocked)
		{
			wasUnlocked = false;
			foreach (SO_Character candidate in Enumerable.Reverse(Behaviours.Characters()))
			{
				bool was = CvUnlocked().Contains(candidate.index);
				if (was) CharacterManager.ResetCharacterUnlock(candidate.index);
				if (!CvUnlocked().Contains(candidate.index)) { wasUnlocked = was; return candidate; }
			}
			return null;
		}

		[ConsoleCommand(name: "CICharacterReload", docs: "Dev, world (host, a test world 'CI ...'): the 'character' verb across a reload. A lever with character|<number> unlocks a locked character in the profile; the island is unloaded and loaded again, Raft's own save is made (SaveGame), the world is saved and read back (IslandWorldState.Save, OnWorldLoaded) and the lever pulled once more: the character stays unlocked each time, nothing else is unlocked. Left out: Raft's profile file itself is not read from disk (it is Raft's); the profile's unlock is put back to how it was")]
		public static void CharacterReloadCommand() { StartTest(CharacterReloadRoutine()); }

		static IEnumerator CharacterReloadRoutine()
		{
			if (!CvInTestWorld("character reload")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("character reload: run as the host"); yield break; }
			bool ok = true;
			if (Behaviours.Characters().Count == 0) { Fail("character reload: Raft lists no characters"); yield break; }
			bool wasUnlocked;
			SO_Character c = GstLockableCharacter(out wasUnlocked);
			if (c == null) { Fail("character reload: no character of Raft's can be locked"); yield break; }
			const string isl = "cigschar";
			var made = new List<IslandWorldState.Entry>();
			try
			{
				GstBuild(isl, false, false, c.index);
				Vector3? spot = ScSpot(isl, 320f);
				if (!spot.HasValue) { Fail("character reload: no open sea near the raft"); yield break; }
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("character reload: the island didn't come"); yield break; }
				yield return new WaitForSeconds(1f);
				Func<bool> unlocked = () => CvUnlocked().Contains(c.index);
				List<int> others = CvUnlocked().Where(i => i != c.index).OrderBy(i => i).ToList();
				Log("  character " + c.index + " " + c.displayName + ", " + (wasUnlocked ? "unlocked" : "locked") + " before; locked for the test");
				Check(ref ok, !unlocked(), "'" + c.displayName + "' is locked before");
				ScUse(e, "unlock");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, unlocked() && CvUnlocked().Where(i => i != c.index).OrderBy(i => i).SequenceEqual(others), "the lever unlocks '" + c.displayName + "' and nothing else");
				// the island unloaded and loaded again
				yield return ReloadIslandRoutine(e);
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, unlocked(), "after the island was unloaded and loaded again: still unlocked");
				// Raft's own save
				try { UnityEngine.Object.FindObjectOfType<SaveAndLoad>().SaveGame(false); } catch (Exception ex) { Log("  (Raft's save: " + ex.Message + ")"); }
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, unlocked(), "after Raft saved the game: still unlocked");
				// the world saved and read back
				yield return GstReload(made);
				e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("character reload: the island didn't come back"); yield break; }
				yield return new WaitForSeconds(1f);
				Check(ref ok, unlocked() && CvUnlocked().Where(i => i != c.index).OrderBy(i => i).SequenceEqual(others), "after the world was saved and read back: still unlocked, nothing else changed");
				ScUse(e, "unlock");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, unlocked() && CvUnlocked().Count(i => i == c.index) == 1, "the lever pulled again after the read: unlocked once, not twice");
			}
			finally
			{
				if (wasUnlocked) CharacterManager.UnlockCharacterByIndex(c.index); else CharacterManager.ResetCharacterUnlock(c.index);
				ScRemove(made, isl);
				try { OnRaftCommand(); } catch { }
			}
			Check(ref ok, CvUnlocked().Contains(c.index) == wasUnlocked, "the profile is as it was ('" + c.displayName + "' " + (wasUnlocked ? "unlocked" : "locked") + ")");
			if (ok) Log("PASS: character reload"); else Fail("character reload");
		}

		#endregion

		#region CIUpgradesCraft

		static HashSet<string> gupOptions;

		/// <summary>The upgrade the craft test builds: a machine with a battery slot (the fast recycler).</summary>
		static ExtraUpgrades.Upgrade GupMachine() { return ExtraUpgrades.Find("DI_FastRecycler"); }

		static string GupRole { get { return Raft_Network.IsHost ? "host" : "client"; } }

		static void GupBroadcast() { if (Raft_Network.IsHost) IslandNetwork.SendUpgradeTable(UpgradeTable.Message(), null); }

		static List<Block> GupBlocks(ExtraUpgrades.Upgrade u)
		{
			return BlockCreator.GetPlacedBlocks().Where(b => b != null && b.buildableItem != null && b.buildableItem.UniqueIndex == u.Index).ToList();
		}

		static void GupCloseMenu()
		{
			try { CanvasHelper ch = ComponentManager<CanvasHelper>.Value ?? UnityEngine.Object.FindObjectOfType<CanvasHelper>(); if (ch != null) ch.CloseMenu(MenuType.Inventory); } catch { }
		}

		/// <summary>Selects a recipe in Raft's crafting menu and presses its Craft button (CraftingMenu.SelectRecipe, CraftItem).</summary>
		static bool GupCraftPress(Item_Base item, out string problem)
		{
			problem = null;
			CraftingMenu cm = Resources.FindObjectsOfTypeAll<CraftingMenu>().FirstOrDefault(m => m != null && m.gameObject.scene.IsValid());
			if (cm == null) { problem = "no crafting menu in the scene"; return false; }
			try
			{
				try { CanvasHelper ch = ComponentManager<CanvasHelper>.Value ?? UnityEngine.Object.FindObjectOfType<CanvasHelper>(); if (ch != null) ch.OpenMenuCloseOther(MenuType.Inventory, true); } catch { }
				HarmonyLib.AccessTools.Method(typeof(CraftingMenu), "SelectRecipe").Invoke(cm, new object[] { item, false });
				object box = HarmonyLib.Traverse.Create(cm).Field("selectedRecipeBox").GetValue();
				if (box != null)
				{
					var t = HarmonyLib.Traverse.Create(box);
					if (t.Field("selectedRecipeItem").GetValue<Item_Base>() != item) t.Field("selectedRecipeItem").SetValue(item);
				}
				// (CraftItem itself takes what it can and makes the item: Raft's only guard is the button, greyed out by
				// SelectedRecipeBox.Update when the inventory doesn't hold the cost - so a press of a greyed button does nothing)
				if (!item.settings_recipe.NewCost.All(c => c.HasEnoughInInventory(RAPI.GetLocalPlayer().Inventory)))
				{ Log("  (the Craft button is greyed out: short of the cost)"); return true; }
				HarmonyLib.AccessTools.Method(typeof(CraftingMenu), "CraftItem").Invoke(cm, null);
				return true;
			}
			catch (Exception ex) { problem = (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message; return false; }
			finally { GupCloseMenu(); }
		}

		class GupResult { public bool Ok = true; }

		/// <summary>
		/// Crafts an upgrade through Raft's crafting menu: short of the cost nothing is made; with exactly the cost in the
		/// inventory one is made and the cost taken.
		/// </summary>
		static IEnumerator GupCraft(Network_Player player, Item_Base item, GupResult r)
		{
			PlayerInventory inv = player.Inventory;
			CostMultiple[] cost = item.settings_recipe.NewCost;
			Check(ref r.Ok, item.settings_recipe.Learned, GupRole + ": " + item.settings_Inventory.DisplayName + " is craftable (its recipe learned)");
			// short of the cost: the Craft button does nothing
			bool has = cost.All(c => c.HasEnoughInInventory(inv));
			string problem;
			if (!has)
			{
				int before = inv.GetItemCount(item.UniqueName);
				bool pressed = GupCraftPress(item, out problem);
				yield return new WaitForSeconds(0.3f);
				Check(ref r.Ok, pressed && inv.GetItemCount(item.UniqueName) == before, GupRole + ": short of the cost, the Craft button makes nothing" + (pressed ? "" : " (" + problem + ")"));
			}
			else Log("  (" + GupRole + ": the player already holds the whole cost of " + item.UniqueName + ": the short-of-cost press is left out)");
			// exactly the cost: one is made, the cost taken
			var gave = new List<KeyValuePair<Item_Base, int>>();
			foreach (CostMultiple c in cost)
			{
				Item_Base pay = c.items.First(i => i != null);
				int need = c.amount - (c.items.Where(i => i != null).Sum(i => inv.GetItemCount(i.UniqueName)));
				if (need > 0) { inv.AddItem(pay.UniqueName, need); gave.Add(new KeyValuePair<Item_Base, int>(pay, need)); }
			}
			yield return null;
			Check(ref r.Ok, cost.All(c => c.HasEnoughInInventory(inv)), GupRole + ": the player holds the cost (" + ExtraUpgrades.CostText(cost) + ")");
			var heldBefore = cost.Select(c => c.items.Where(i => i != null).Sum(i => inv.GetItemCount(i.UniqueName))).ToList();
			int itemBefore = inv.GetItemCount(item.UniqueName);
			bool ok2 = GupCraftPress(item, out problem);
			yield return new WaitForSeconds(0.5f);
			Check(ref r.Ok, ok2, GupRole + ": the Craft button pressed" + (ok2 ? "" : " (" + problem + ")"));
			Check(ref r.Ok, inv.GetItemCount(item.UniqueName) == itemBefore + 1, GupRole + ": crafting made one " + item.settings_Inventory.DisplayName + " (" + (inv.GetItemCount(item.UniqueName) - itemBefore) + " more)");
			var heldAfter = cost.Select(c => c.items.Where(i => i != null).Sum(i => inv.GetItemCount(i.UniqueName))).ToList();
			bool paid = true;
			for (int i = 0; i < cost.Length; i++) if (heldBefore[i] - heldAfter[i] != cost[i].amount) paid = false;
			Check(ref r.Ok, paid, GupRole + ": the cost was taken (" + string.Join(", ", cost.Select((c, i) => (heldBefore[i] - heldAfter[i]) + "/" + c.amount).ToArray()) + ")");
		}

		[ConsoleCommand(name: "CIUpgradesCraft", docs: "Dev, world (either player): extra upgrades built for real. CIUpgradesCraft setup (host: option on, the fast recycler and the large battery learned, told to the players; run before the first build) / CIUpgradesCraft [build] (this player: crafts the large battery and the fast recycler through Raft's crafting menu - CraftingMenu.SelectRecipe + CraftItem: short of the cost nothing is made, with the cost one is made and the cost taken -; places the recycler on the first foundation of the raft with the build path (BlockCreator.CreateBlockCheat on the raft, then takes the crafted item out as placing does), puts the large battery in its slot, and says what the block is) / CIUpgradesCraft place (host: places a fast recycler with a large battery for player 2, whose build only crafts and hands both over - Raft's cheat placement is host-only) / CIUpgradesCraft check (the recycler is on the raft once, an upgrade block, its slot holds the large battery with its charge: run after CISave + a reload, and on the other player) / CIUpgradesCraft end (host: the blocks taken down, the learned upgrades and the option as they were). Built with exactly the cost in hand; Raft's cheat placement does not need the hotbar item, so the test takes the crafted item out itself")]
		public static void UpgradesCraftCommand(string[] args) { StartTest(UpgradesCraftRoutine(args != null && args.Length > 0 ? args[0] : "build")); }

		const int GupBatteryLeft = 5;

		static IEnumerator UpgradesCraftRoutine(string part)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			ExtraUpgrades.Upgrade machine = GupMachine();
			Item_Base large = ExtraUpgrades.LargeBattery;
			if (player == null || !CustomIslandSpawner.RaftPosition.HasValue) { Fail("upgrades craft: in a world"); yield break; }
			if (machine == null || machine.Item == null || large == null) { Fail("upgrades craft: not registered (" + (machine == null || machine.Item == null ? "no fast recycler" : "no large battery") + ")"); yield break; }
			ExtraUpgrades.Upgrade largeUp = ExtraUpgrades.Find(ExtraUpgrades.LargeBatteryIndex);
			var r = new GupResult();
			if (part == "setup")
			{
				if (!Raft_Network.IsHost) { Fail("upgrades craft setup: run as the host"); yield break; }
				if (gupOptions == null) gupOptions = new HashSet<string>(WorldOptions.Current);
				var set = new HashSet<string>(WorldOptions.Current) { WorldOptions.Upgrades };
				WorldOptions.Set(set);
				yield return new WaitForSeconds(0.5f);
				UpgradeTable.SetLearned(machine, true);
				UpgradeTable.SetLearned(largeUp, true);
				GupBroadcast();
				yield return new WaitForSeconds(1.5f);
				Check(ref r.Ok, WorldOptions.On(WorldOptions.Upgrades) && machine.Item.settings_recipe.Learned && large.settings_recipe.Learned, "host: the option is on and both recipes are learned");
				if (r.Ok) Log("PASS: upgrades craft setup"); else Fail("upgrades craft setup");
				yield break;
			}
			if (part == "end")
			{
				if (!Raft_Network.IsHost) { Fail("upgrades craft end: run as the host"); yield break; }
				foreach (Block b in GupBlocks(machine)) BlockCreator.RemoveBlockNetwork(b, null, true);
				yield return new WaitForSeconds(0.5f);
				UpgradeTable.SetLearned(machine, false);
				UpgradeTable.SetLearned(largeUp, false);
				GupBroadcast();
				if (gupOptions != null) { WorldOptions.Set(gupOptions); gupOptions = null; }
				yield return new WaitForSeconds(0.5f);
				Check(ref r.Ok, GupBlocks(machine).Count == 0, "host: no fast recycler left on the raft");
				if (r.Ok) Log("PASS: upgrades craft end"); else Fail("upgrades craft end");
				yield break;
			}
			if (part == "check")
			{
				yield return new WaitForSeconds(1f);
				List<Block> blocks = GupBlocks(machine);
				Check(ref r.Ok, blocks.Count == 1, GupRole + ": one fast recycler on the raft (" + blocks.Count + ")");
				Block blk = blocks.FirstOrDefault();
				UpgradeBlock ub = blk != null ? blk.GetComponent<UpgradeBlock>() : null;
				Check(ref r.Ok, ub != null && ub.Upgrade == machine, GupRole + ": it is the upgrade's block (UpgradeBlock " + (ub != null && ub.Upgrade != null ? ub.Upgrade.Display : "none") + ")");
				Battery slot = blk != null ? blk.GetComponentInChildren<Battery>(true) : null;
				ItemInstance inside = slot != null ? slot.GetBatteryInstance() : null;
				Check(ref r.Ok, slot != null && inside != null && inside.UniqueIndex == large.UniqueIndex, GupRole + ": the battery is still in its machine (" + (inside != null ? inside.UniqueName : slot == null ? "no battery slot" : "empty") + ")");
				Check(ref r.Ok, slot != null && Math.Abs(slot.BatteryUses - GupBatteryLeft) <= 2, GupRole + ": with its charge (" + (slot != null ? slot.BatteryUses : -1) + ", it was put in with " + GupBatteryLeft + ")");
				Check(ref r.Ok, player.Inventory.GetItemCount(large.UniqueName) == 0 && player.Inventory.GetItemCount(machine.Item.UniqueName) == 0, GupRole + ": nothing of it left in the inventory");
				if (r.Ok) Log("PASS: upgrades craft check " + GupRole); else Fail("upgrades craft check " + GupRole);
				yield break;
			}

			// build
			if (Raft_Network.IsHost && !WorldOptions.On(WorldOptions.Upgrades)) { Fail("upgrades craft: run CIUpgradesCraft setup first"); yield break; }
			float t0 = Time.realtimeSinceStartup;
			while ((!machine.Item.settings_recipe.Learned || !large.settings_recipe.Learned) && Time.realtimeSinceStartup - t0 < 12f) yield return new WaitForSeconds(0.5f);
			Block floor = BlockCreator.GetPlacedBlocks().FirstOrDefault(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName == "Block_Foundation");
			if (floor == null) { Fail("upgrades craft: no foundation on the raft"); yield break; }
			int recyclers0 = GupBlocks(machine).Count;
			// (a reused world: player 2 may still hold some from an earlier run - counted from here)
			int heldM0 = player.Inventory.GetItemCount(machine.Item.UniqueName), heldL0 = player.Inventory.GetItemCount(large.UniqueName);
			if (part != "place")
			{
				yield return GupCraft(player, large, r);
				yield return GupCraft(player, machine.Item, r);
			}
			else if (!Raft_Network.IsHost) { Fail("upgrades craft place: run as the host"); yield break; }
			if (part == "build" && !Raft_Network.IsHost)
			{
				// (Raft's cheat placement works on the host only: the client hands both over as placing does, and the
				// host places them for it with CIUpgradesCraft place)
				int heldM = player.Inventory.GetItemCount(machine.Item.UniqueName), heldL = player.Inventory.GetItemCount(large.UniqueName);
				Check(ref r.Ok, heldM == heldM0 + 1 && heldL == heldL0 + 1, GupRole + ": holds one more recycler and large battery (" + heldM0 + " -> " + heldM + ", " + heldL0 + " -> " + heldL + ")");
				if (heldM > 0) player.Inventory.RemoveItem(machine.Item.UniqueName, heldM);
				if (heldL > 0) player.Inventory.RemoveItem(large.UniqueName, heldL);
				if (r.Ok) Log("PASS: upgrades craft " + GupRole + " (the host places them for this player: CIUpgradesCraft place)"); else Fail("upgrades craft " + GupRole);
				yield break;
			}
			// place it on the raft
			Collider[] cs = floor.GetComponentsInChildren<Collider>();
			float top = cs.Length > 0 ? cs.Max(c => c.bounds.max.y) - floor.transform.position.y : 0.5f;
			float side = part == "place" ? -0.7f : 0.7f;
			Vector3 local = floor.transform.localPosition + new Vector3(side, top, -side);
			Block placed = null;
			try { placed = player.BlockCreator.CreateBlockCheat(machine.Item, local, Vector3.zero, DPS.Default, 0); }
			catch (Exception ex) { Fail("upgrades craft: placing: " + ex.Message); }
			yield return new WaitForSeconds(0.8f);
			Check(ref r.Ok, placed != null, GupRole + ": the recycler is placed on the raft");
			if (placed == null) { Fail("upgrades craft"); yield break; }
			// (Raft's cheat placement takes nothing from the hotbar: the crafted item is taken out as placing does)
			int held = player.Inventory.GetItemCount(machine.Item.UniqueName);
			if (held > 0) player.Inventory.RemoveItem(machine.Item.UniqueName, held);
			Check(ref r.Ok, GupBlocks(machine).Count == recyclers0 + 1, GupRole + ": one more fast recycler on the raft (" + GupBlocks(machine).Count + ")");
			UpgradeBlock ub2 = placed.GetComponent<UpgradeBlock>();
			Check(ref r.Ok, ub2 != null && ub2.Upgrade == machine, GupRole + ": the placed block is the upgrade's (UpgradeBlock " + (ub2 != null && ub2.Upgrade != null ? ub2.Upgrade.Display : "none") + ")");
			// the battery
			Battery slot2 = placed.GetComponentInChildren<Battery>(true);
			Check(ref r.Ok, slot2 != null && slot2.itemEnabler != null && slot2.itemEnabler.DoesAcceptItem(large), GupRole + ": its battery slot takes the large battery");
			if (slot2 != null)
			{
				bool put = slot2.Insert(null, GupBatteryLeft, large.UniqueIndex);
				// (as Raft's own insert by hand: the others get Battery_Insert, else player 2's machine stays empty)
				MonoBehaviour_ID_Network net = HarmonyLib.Traverse.Create(slot2).Field("networkBehaviourID").GetValue<MonoBehaviour_ID_Network>();
				if (put && net != null)
					player.Network.RPC(new Message_Battery(Messages.Battery_Insert, player.Network.NetworkIDManager, player.steamID, net.ObjectIndex, GupBatteryLeft, slot2.BatteryIndex, large.UniqueIndex),
						Target.Other, Steamworks.EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
				int heldBattery = player.Inventory.GetItemCount(large.UniqueName);
				if (heldBattery > 0) player.Inventory.RemoveItem(large.UniqueName, heldBattery);
				yield return new WaitForSeconds(0.8f);
				ItemInstance inside = slot2.GetBatteryInstance();
				Check(ref r.Ok, put && inside != null && inside.UniqueIndex == large.UniqueIndex, GupRole + ": the large battery is in the machine (" + (inside != null ? inside.UniqueName : "empty") + ", charge " + slot2.BatteryUses + ")");
			}
			string what = part == "place" ? "place (for player 2)" : GupRole;
			if (r.Ok) Log("PASS: upgrades craft " + what + " (now CISave and reload, then CIUpgradesCraft check; CIUpgradesCraft end cleans up)"); else Fail("upgrades craft " + what);
		}

		#endregion

		#region CIConsoleArgs

		static readonly List<string> gcaLines = new List<string>();
		static void GcaHandler(string msg, string stack, LogType type) { gcaLines.Add((type == LogType.Exception ? "EXC: " : "") + msg); }
		static void GcaStart() { gcaLines.Clear(); Application.logMessageReceived += GcaHandler; }
		static List<string> GcaStop() { Application.logMessageReceived -= GcaHandler; return gcaLines.Where(l => !l.StartsWith("[CITEST]")).ToList(); }

		/// <summary>Runs a console command's method with these words; the lines it logged, and the exception it threw.</summary>
		static List<string> GcaRun(Action call, out Exception thrown)
		{
			thrown = null;
			GcaStart();
			try { call(); } catch (Exception ex) { thrown = ex; }
			return GcaStop();
		}

		static string GcaText(List<string> lines) { return lines.Count == 0 ? "(no message)" : string.Join(" / ", lines.Select(l => l.Length > 110 ? l.Substring(0, 110) : l).ToArray()); }

		[ConsoleCommand(name: "CIConsoleArgs", docs: "Dev, world (host, a test world 'CI ...'): the player-facing console commands with bad arguments - SpawnIsland (no words, a name no island has with text / negative / huge distance and height: a message, no exception, no island added), WorldIslandsGap (empty, text, one number, '5-', '-3-9', '5-12-20', a number beyond int: a message, the gap unchanged; '1-1000' is clamped to the allowed span; put back), HeadStart (text, negative, huge, in a world: the level and the next world's choice unchanged), GenerateIsland (refused outside the editor, nothing generated), SetElevation and ChangeWidth with no words or text (a usage message, the value unchanged), plus ListSpawned (one line per island of the world, its state suffix right, a far-away island 'unloaded') and StoryChain (the lines match StoryChain.Describe, one per rule, 'in the world' exactly for the rules brought). CIConsoleArgs editor = in the editor: SetElevation and ChangeWidth with negative / huge / NaN / Infinity values stay in their range (NaN must change nothing), GenerateIsland with text / negative / huge arguments makes a clamped island (CIConsoleArgs nan adds the NaN case)")]
		public static void ConsoleArgsCommand(string[] args)
		{
			bool editor = args != null && args.Contains("editor");
			StartTest(editor ? ConsoleArgsEditorRoutine(args.Contains("nan")) : ConsoleArgsRoutine());
		}

		static IEnumerator ConsoleArgsRoutine()
		{
			if (!CvInTestWorld("console args")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("console args: run as the host"); yield break; }
			bool ok = true;
			Exception ex;
			List<string> msgs;
			const string isl = "cigsargs";
			var made = new List<IslandWorldState.Entry>();
			int gapMin0 = WorldIslands.GapMin, gapMax0 = WorldIslands.GapMax;
			try
			{
				// SpawnIsland: missing words, then a name no island has with bad numbers
				int count0 = IslandWorldState.Islands.Count;
				msgs = GcaRun(() => DynamicIslands.SpawnIslandCommand(new string[0]), out ex);
				Check(ref ok, ex == null && msgs.Any(m => m.Contains("Usage")) && IslandWorldState.Islands.Count == count0, "SpawnIsland with no words: a usage message, no exception, no island (" + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				msgs = GcaRun(() => DynamicIslands.SpawnIslandCommand(null), out ex);
				Check(ref ok, ex == null && IslandWorldState.Islands.Count == count0, "SpawnIsland with null arguments: no exception, no island" + (ex != null ? " (EXCEPTION " + ex.Message + ")" : ""));
				var cases = new List<string[]>
				{
					new[] { "cigsnosuch", "abc" }, new[] { "cigsnosuch", "-50" }, new[] { "cigsnosuch", "99999999" }, new[] { "cigsnosuch", "100", "99999" },
					new[] { "cigsnosuch", "100", "-99999" }, new[] { "cigsnosuch", "NaN" }, new[] { "cigsnosuch", "Infinity", "-Infinity" }
				};
				foreach (string[] a in cases)
				{
					GcaStart();
					Exception thrown = null;
					try { DynamicIslands.SpawnIslandCommand(a); } catch (Exception e2) { thrown = e2; }
					yield return new WaitForSeconds(1.2f);
					msgs = GcaStop();
					Check(ref ok, thrown == null && !msgs.Any(m => m.StartsWith("EXC:")) && msgs.Any(m => m.Contains("No saved island named")) && IslandWorldState.Islands.Count == count0,
						"SpawnIsland " + string.Join(" ", a) + ": 'No saved island named', no exception, no island added (" + GcaText(msgs) + (thrown != null ? "; EXCEPTION " + thrown.Message : "") + ")");
				}

				// WorldIslandsGap: unreadable words change nothing
				var bad = new[] { new string[0], new[] { "abc" }, new[] { "5" }, new[] { "5-" }, new[] { "-3-9" }, new[] { "5-12-20" }, new[] { "5-99999999999999" }, new[] { "a-b" }, new[] { "NaN-NaN" } };
				foreach (string[] a in bad)
				{
					msgs = GcaRun(() => WorldIslands.GapCommand(a), out ex);
					bool readOnly = a.Length == 0;
					Check(ref ok, ex == null && WorldIslands.GapMin == gapMin0 && WorldIslands.GapMax == gapMax0 && msgs.Count > 0 && (readOnly || msgs.Any(m => m.Contains("nothing changed"))),
						"WorldIslandsGap '" + string.Join(" ", a) + "': a message" + (readOnly ? "" : " (nothing changed)") + ", no exception, the gap stays " + gapMin0 + "-" + gapMax0 + " (now " + WorldIslands.GapMin + "-" + WorldIslands.GapMax + "; " + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				}
				msgs = GcaRun(() => WorldIslands.GapCommand(new[] { "1-1000" }), out ex);
				Check(ref ok, ex == null && WorldIslands.GapMin >= WorldIslands.GapLowest && WorldIslands.GapMin <= WorldIslands.GapHighest && WorldIslands.GapMax >= WorldIslands.GapLowestTop && WorldIslands.GapMax <= WorldIslands.GapHighestTop && WorldIslands.GapMin <= WorldIslands.GapMax,
					"WorldIslandsGap 1-1000: clamped to the allowed span (" + WorldIslands.GapMin + "-" + WorldIslands.GapMax + ")");
				WorldIslands.GapMin = gapMin0; WorldIslands.GapMax = gapMax0;

				// HeadStart in a world: the choice is for a new world, in the main menu
				int level0 = HeadStart.Level;
				// (Chosen, not Pending: Pending is filled from the saved default the first time Chosen is read - the command's
				// own message reads it - so it went from null to the default with nothing chosen)
				int chosen0 = HeadStart.Chosen;
				foreach (string[] a in new[] { new string[0], new[] { "abc" }, new[] { "-1" }, new[] { "99" }, new[] { "2147483647" }, new[] { "99999999999" }, new[] { "1.5" } })
				{
					msgs = GcaRun(() => HeadStart.HeadStartCommand(a), out ex);
					Check(ref ok, ex == null && HeadStart.Level == level0 && HeadStart.Chosen == chosen0 && msgs.Count > 0,
						"HeadStart '" + string.Join(" ", a) + "' in a world: a message, no exception, the level (" + HeadStart.Level + ") and the next world's choice unchanged (" + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				}

				// The editor's commands outside the editor
				object lastGen = IslandGenerator.Last;
				int lastSeed = IslandGenerator.Last.Seed;
				msgs = GcaRun(() => DynamicIslands.GenerateIslandCommand(new[] { "abc", "-5", "99999" }), out ex);
				Check(ref ok, ex == null && msgs.Any(m => m.Contains("only works inside the editor")) && ReferenceEquals(lastGen, IslandGenerator.Last) && IslandGenerator.Last.Seed == lastSeed,
					"GenerateIsland outside the editor: refused with a message, nothing generated (" + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				float elev0 = DynamicIslands.currentElevation, brush0 = terraineditor.brushRadius;
				foreach (string[] a in new[] { new string[0], new[] { "abc" }, new[] { "" } })
				{
					msgs = GcaRun(() => DynamicIslands.SetElevationCommand(a), out ex);
					Check(ref ok, ex == null && DynamicIslands.currentElevation == elev0 && msgs.Count > 0, "SetElevation '" + string.Join(" ", a) + "': a usage message, no exception, the elevation unchanged (" + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
					msgs = GcaRun(() => DynamicIslands.TerrainWidth(a), out ex);
					Check(ref ok, ex == null && terraineditor.brushRadius == brush0 && msgs.Any(m => m.Contains("Usage")), "ChangeWidth '" + string.Join(" ", a) + "': a usage message, no exception, the brush unchanged (" + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				}

				// ListSpawned: one line per island, the state suffix right
				GstBuild(isl, false, false, -1);
				Vector3? spot = ScSpot(isl, 320f);
				if (!spot.HasValue) { Check(ref ok, false, "ListSpawned: no open sea near the raft for a test island"); }
				else
				{
					yield return ScBring(isl, spot.Value, made);
					IslandWorldState.Entry e = made.FirstOrDefault();
					if (e == null || e.Root == null) Check(ref ok, false, "ListSpawned: the test island didn't come");
					else
					{
						yield return new WaitForSeconds(0.5f);
						msgs = GcaRun(() => DynamicIslands.ListSpawnedCommand(), out ex);
						var lines = msgs.Where(m => m.StartsWith("[CUSTOM ISLANDS]")).ToList();
						Check(ref ok, ex == null && lines.Count == IslandWorldState.Islands.Count, "ListSpawned: " + lines.Count + " lines for the world's " + IslandWorldState.Islands.Count + " islands");
						bool each = IslandWorldState.Islands.All(i => lines.Count(l => l.Contains("] " + i.HostName + " at ")) == 1);
						Check(ref ok, each, "ListSpawned: every island named on exactly one line");
						string mine = lines.FirstOrDefault(l => l.Contains("] " + e.HostName + " at "));
						Check(ref ok, mine != null && mine.Contains(" m from the raft") && !mine.Contains("(unloaded") && !mine.Contains("(loading") && !mine.Contains("missing"), "ListSpawned: the loaded island has its distance and no state suffix (" + (mine ?? "no line") + ")");
						// the island far away (unloaded)
						IslandObjectState.Capture(e);
						IslandSpawner.Despawn(e.Root);
						e.Root = null;
						yield return null;
						msgs = GcaRun(() => DynamicIslands.ListSpawnedCommand(), out ex);
						mine = msgs.FirstOrDefault(l => l.Contains("] " + e.HostName + " at "));
						Check(ref ok, mine != null && mine.Contains("(unloaded: far away)"), "ListSpawned: an unloaded island says so (" + (mine ?? "no line") + ")");
						e.Loading = true;
						yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
						msgs = GcaRun(() => DynamicIslands.ListSpawnedCommand(), out ex);
						mine = msgs.FirstOrDefault(l => l.Contains("] " + e.HostName + " at "));
						Check(ref ok, e.Root != null && mine != null && !mine.Contains("(unloaded"), "ListSpawned: loaded again, the suffix is gone (" + (mine ?? "no line") + ")");
					}
				}

				// StoryChain: the lines match the chain
				msgs = GcaRun(() => StoryChain.StoryChainCommand(), out ex);
				var chainLines = msgs.Where(m => m.StartsWith("[CUSTOM ISLANDS]")).Select(m => m.Substring("[CUSTOM ISLANDS] ".Length)).ToList();
				string[] want = StoryChain.Describe().Split('\n');
				Check(ref ok, ex == null && chainLines.SequenceEqual(want), "StoryChain prints StoryChain.Describe line for line (" + chainLines.Count + " lines, " + want.Length + " expected)");
				Check(ref ok, chainLines.Count == 1 + StoryChain.Rules.Count, "StoryChain: one heading and one line per rule (" + StoryChain.Rules.Count + " rules, " + chainLines.Count + " lines)");
				Check(ref ok, chainLines.Count > 0 && chainLines[0].StartsWith("Story chain: ") && (StoryChain.Active ? !chainLines[0].Contains("Raft's own") : chainLines[0].Contains("Raft's own")), "StoryChain: the heading says " + (StoryChain.Active ? "the plan's chain" : "Raft's own") + " (" + (chainLines.Count > 0 ? chainLines[0] : "none") + ")");
				bool broughtRight = StoryChain.Rules.All(r =>
				{
					string line = chainLines.FirstOrDefault(l => l.TrimStart().StartsWith(r.Id + ":"));
					return line != null && line.Contains(" - in the world") == StoryChain.Brought.Contains(r.Id);
				});
				Check(ref ok, broughtRight, "StoryChain: each rule's line says 'in the world' exactly when the world brought it");
			}
			finally
			{
				WorldIslands.GapMin = gapMin0; WorldIslands.GapMax = gapMax0;
				ScRemove(made, isl);
				try { OnRaftCommand(); } catch { }
			}
			if (ok) Log("PASS: console args"); else Fail("console args");
		}

		static IEnumerator ConsoleArgsEditorRoutine(bool nan)
		{
			yield return WaitForEditor(false);
			bool ok = true;
			Exception ex;
			List<string> msgs;
			float elev0 = DynamicIslands.currentElevation, brush0 = terraineditor.brushRadius;
			try
			{
				// SetElevation: stays in its range; NaN changes nothing
				foreach (string v in new[] { "-5", "-99999", "99999", "Infinity", "-Infinity", "NaN", "1e38", "abc", "" })
				{
					DynamicIslands.currentElevation = 7f;
					msgs = GcaRun(() => DynamicIslands.SetElevationCommand(new[] { v }), out ex);
					float now = DynamicIslands.currentElevation;
					Check(ref ok, ex == null && !float.IsNaN(now) && now >= IslandSpawner.MinElevation && now <= IslandSpawner.MaxElevation,
						"SetElevation '" + v + "': no exception, the elevation stays a number in " + IslandSpawner.MinElevation + ".." + IslandSpawner.MaxElevation + " (now " + now + "; " + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				}
				foreach (string v in new[] { "-5", "-99999", "99999", "Infinity", "-Infinity", "NaN", "1e38", "abc", "0" })
				{
					terraineditor.brushRadius = 15f;
					msgs = GcaRun(() => DynamicIslands.TerrainWidth(new[] { v }), out ex);
					float now = terraineditor.brushRadius;
					Check(ref ok, ex == null && !float.IsNaN(now) && now >= terraineditor.MinRadius && now <= terraineditor.MaxRadius,
						"ChangeWidth '" + v + "': no exception, the brush stays in " + terraineditor.MinRadius * 2f + ".." + terraineditor.MaxRadius * 2f + " m (radius now " + now + "; " + GcaText(msgs) + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				}
				// GenerateIsland: text, negative and huge arguments make a clamped island
				var gen = new List<string[]>
				{
					new[] { "abc" },
					new[] { "-1", "-50", "-50", "-3", "-4", "-1", "nosuchstyle" },
					new[] { "1", "99999999", "99999999", "99", "999", "99", "Tropical" }
				};
				if (nan) gen.Add(new[] { "1", "NaN", "NaN", "NaN", "3", "NaN" });
				foreach (string[] a in gen)
				{
					msgs = GcaRun(() => DynamicIslands.GenerateIslandCommand(a), out ex);
					yield return new WaitForSeconds(1f);
					IslandGenSettings s = IslandGenerator.Last;
					Check(ref ok, ex == null && s.Radius >= IslandGenSettings.MinRadius && s.Radius <= IslandGenSettings.MaxRadius && s.Height >= 2f && s.Height <= IslandGenSettings.MaxHeight && s.Roughness >= 0f && s.Roughness <= 1f && s.Peaks >= 1 && s.ObjectDensity >= 0f && s.ObjectDensity <= 1f,
						"GenerateIsland '" + string.Join(" ", a) + "': no exception, an island with settings in range (radius " + s.Radius + ", height " + s.Height + ", roughness " + s.Roughness + ", peaks " + s.Peaks + ", objects " + s.ObjectDensity + (ex != null ? "; EXCEPTION " + ex.Message : "") + ")");
				}
			}
			finally
			{
				DynamicIslands.currentElevation = elev0;
				terraineditor.brushRadius = brush0;
				try { DynamicIslands.NewIsland(); } catch { }
			}
			if (ok) Log("PASS: console args editor"); else Fail("console args editor");
		}

		#endregion

		#region Two-player helpers (CIGs*, driven by mpstoryplay.ps1)

		static int gsMpChar = -1;
		static bool gsMpCharWas;

		static string GsYes(bool b) { return b ? "yes" : "no"; }

		[ConsoleCommand(name: "CIGsMp", docs: "Dev, world (host, two-player test): CIGsMp setup = the story sets, the wait chain, the take box and toll lever and the character lever on one island 'cigsmp' near the raft, kept for player 2 (the crew's story book emptied first); CIGsMp end = the island removed, the story book emptied. See mpstoryplay.ps1")]
		public static void GsMpCommand(string[] args) { StartTest(GsMpRoutine(args != null && args.Length > 0 ? args[0] : "setup")); }

		static IEnumerator GsMpRoutine(string part)
		{
			if (!Raft_Network.IsHost || !CustomIslandSpawner.RaftPosition.HasValue || RAPI.GetLocalPlayer() == null) { Fail("gs mp: run as the host, in a world"); yield break; }
			if (part == "end")
			{
				IslandWorldState.Remove("cigsmp");
				try { if (System.IO.File.Exists(IslandSpawner.PathFor("cigsmp"))) System.IO.File.Delete(IslandSpawner.PathFor("cigsmp")); } catch { }
				IslandCache.Forget();
				ResetStoryEverywhere();
				Log("PASS: gs mp end");
				yield break;
			}
			List<SO_Character> chars = Behaviours.Characters();
			if (chars.Count == 0) { Fail("gs mp: Raft lists no characters"); yield break; }
			gsMpChar = chars[chars.Count - 1].index;
			IslandWorldState.Remove("cigsmp");
			GstBuild("cigsmp", true, true, gsMpChar);
			Vector3? spot = ScSpot("cigsmp", 390f);
			if (!spot.HasValue) { Fail("gs mp: no open sea near the raft"); yield break; }
			ResetStoryEverywhere();
			yield return DynamicIslands.instance.SpawnIslandFile("cigsmp", spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == "cigsmp");
			if (e == null || e.Root == null) { Fail("gs mp: cigsmp did not spawn"); yield break; }
			Log("PASS: gs mp setup (island " + e.Id + ", the character lever unlocks " + gsMpChar + ")");
		}

		[ConsoleCommand(name: "CIGsStatus", docs: "Dev, two-player test (either player): one line of what this machine sees of 'cigsmp' - the sets, the chain, the box, the toll, the character: GS status <role>: door=.. key=.. (see mpstoryplay.ps1)")]
		public static void GsStatusCommand() { StartTest(GsStatusRoutine()); }

		static IEnumerator GsStatusRoutine()
		{
			yield return null;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName == "cigsmp");
			if (e == null) { Fail("gs status: 'cigsmp' is not loaded here"); yield break; }
			Network_Player p = RAPI.GetLocalPlayer();
			int chr = Behaviours.Characters().Count > 0 ? Behaviours.Characters()[Behaviours.Characters().Count - 1].index : -1;
			Log("GS status " + GupRole + ": door=" + (GstOpen(e, "locked-door") ? "open" : "closed") + " key=" + StoryBook.Count("rusty-key") + " map=" + StoryBook.Count("treasure-map") + " log=" + StoryBook.Count("captains-log") +
				" smallkey=" + StoryBook.Count("small-key") + " keychest=" + (GstLooted(e, "Key chest") ? "opened" : "closed") + " box=" + (GstLooted(e, "Locked box") ? "opened" : "closed") +
				" hidden=" + (GstShown(e, "hidden-chest") ? "shown" : "hidden") + " buried=" + (GstShown(e, "buried-treasure") ? "shown" : "hidden") +
				" lateA=" + (GstShown(e, "late-a") ? "shown" : "hidden") + " lateB=" + (GstShown(e, "late-b") ? "shown" : "hidden") + " chainDoor=" + (GstOpen(e, "chain-door") ? "open" : "closed") +
				" signalChain=" + GsYes(Behaviours.HasSignal(e, "chain-done")) + " signalToll=" + GsYes(Behaviours.HasSignal(e, "toll-paid")) +
				" planks=" + (p != null ? p.Inventory.GetItemCount("Plank") : -1) + " char=" + (chr >= 0 && CvUnlocked().Contains(chr) ? "unlocked" : "locked") + " pending=" + GstPending(e) + " pages=" + StoryBook.Pages.Count +
				" message=" + (Behaviours.LastMessage ?? "").Replace(' ', '_'));
			Log("PASS: gs status " + GupRole);
		}

		[ConsoleCommand(name: "CIGsSet", docs: "Dev, two-player test (either player): this player's Raft item set to exactly a number: CIGsSet Plank 5; CIGsSet story:small-key 2 = the crew's story item (host)")]
		public static void GsSetCommand(string[] args)
		{
			int n;
			if (args == null || args.Length < 2 || !int.TryParse(args[1], out n) || n < 0) { Fail("usage: CIGsSet <item or story:id> <number>"); return; }
			if (args[0].StartsWith("story:", StringComparison.OrdinalIgnoreCase))
			{
				if (!Raft_Network.IsHost) { Fail("story items are set on the host"); return; }
				string id = StoryItems.IdOf(args[0]);
				int have = StoryBook.Count(id);
				if (have > n) StoryBook.Take(id, have - n); else if (have < n) StoryBook.Give(id, n - have);
				StartTest(AfterAction("Crew's " + id + " set to " + n));
				return;
			}
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null) { Fail("in a world"); return; }
			GstSetItem(p, args[0], n);
			Log("PASS: gs set " + args[0] + " " + n + " (" + GupRole + ")");
		}

		[ConsoleCommand(name: "CIGsChar", docs: "Dev, two-player test (either player): CIGsChar lock = the character the 'cigsmp' lever unlocks is locked in this profile (remembered); CIGsChar restore = the profile as it was")]
		public static void GsCharCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "lock";
			List<SO_Character> chars = Behaviours.Characters();
			if (chars.Count == 0) { Fail("Raft lists no characters"); return; }
			int idx = chars[chars.Count - 1].index;
			if (what == "restore")
			{
				if (gsMpChar >= 0) { if (gsMpCharWas) CharacterManager.UnlockCharacterByIndex(gsMpChar); else CharacterManager.ResetCharacterUnlock(gsMpChar); gsMpChar = -1; }
				Log("PASS: gs char restored (" + GupRole + ")");
				return;
			}
			if (gsMpChar != idx) { gsMpCharWas = CvUnlocked().Contains(idx); gsMpChar = idx; }
			CharacterManager.ResetCharacterUnlock(idx);
			bool locked = !CvUnlocked().Contains(idx);
			if (locked) Log("PASS: gs char " + idx + " locked (" + GupRole + ", was " + (gsMpCharWas ? "unlocked" : "locked") + ")"); else Fail("gs char " + idx + " cannot be locked (" + GupRole + ")");
		}

		#endregion
	}
}

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
	/// <summary>Tests for delays and checks on events, story items and the journal, and movers in step (StoryItems.cs, Behaviours.cs).</summary>
	public static partial class DevTests
	{
		#region Editor

		[ConsoleCommand(name: "CIStoryTest", docs: "Dev, editor: checks and waits read and written, the story items window, story sets (locked door, trail of notes) and their undo, the item picker's story items, the checks in the Behaviour window, save and load; SL5: a story item deleted while a collect step, a has check and a locked chest use it (Check warns), Ctrl+Z brings it back")]
		public static void StoryTest()
		{
			StartTest(StoryTestRoutine());
		}

		static IEnumerator StoryTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;

			// Checks and waits as text
			List<ObjCheck> checks = ObjCheck.ParseLines("take|story:old-key|1\nhas|Plank|3\nstate|gate|closed\nsignal|horn\nquest|2\nbogus|x");
			Check(ref ok, checks.Count == 5 && checks[0].IsItem && checks[1].Count == 3 && checks[2].Arg == "closed" && checks[4].Target == "2", "checks are read (an unknown kind is left out)");
			Check(ref ok, ObjCheck.ParseLines(ObjCheck.ToLines(checks)).Select(c => c.Describe()).SequenceEqual(checks.Select(c => c.Describe())), "checks written and read back are the same");
			List<ObjAction> acts = ObjAction.ParseLines("open|gate|\nwait||2.5\nclose|gate|\nwait|4\njournal|The gate|It opened");
			Check(ref ok, acts.Count == 5 && Mathf.Approximately(acts[1].Seconds, 2.5f) && Mathf.Approximately(acts[3].Seconds, 4f) && acts[4].Shared && !acts[1].Shared, "waits (\"wait||2.5\" and \"wait|4\") and journal pages are read");
			Check(ref ok, BehaviourProps.EventKey("use!") == "else.use" && BehaviourProps.CheckKey("use") == "if.use" && BehaviourProps.Any(P("if.use", "has|x|1")), "the keys of checks and of what happens otherwise");
			Check(ref ok, StoryItems.IsStory("story:key") && StoryItems.IdOf("story: key ") == "key" && !StoryItems.IsStory("Plank"), "story item names");
			string notAny = "any\n!has|story:key|1\nsignal|horn|";
			List<ObjCheck> na = ObjCheck.ParseLines(notAny);
			Check(ref ok, na.Count == 2 && na[0].Not && !na[1].Not && ObjCheck.IsAny(notAny) && !ObjCheck.IsAny("has|x|1") && ObjCheck.ToLines(na, true) == notAny && na[0].Describe().StartsWith("NOT"),
				"\"not\" checks (!has) and \"any of\" (a line any) are read and written back");
			var steps = IslandQuest.From(P(IslandQuest.KeySteps, "collect|story:map-piece|3|\npages||2|\npages|all|1|"));
			Check(ref ok, steps.Steps.Count == 3 && steps.Steps[0].Count == 3 && IslandQuest.Counted("pages") && steps.Steps[2].Describe().Contains("any island"), "quest steps collect and pages are read (" + string.Join(" / ", steps.Steps.Select(s => s.Describe()).ToArray()) + ")");

			// The story items window
			EditorUI.SetTab(TAB.Island);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			var saved = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
			DynamicIslands.currentIslandProps.Remove(StoryItems.Key);
			StoryItemsWindow.Open();
			yield return new WaitForSecondsRealtime(0.3f);
			Check(ref ok, StoryItemsWindow.IsOpen && StoryItemsWindow.Editing.Count == 0, "the Story items window opens (none yet)");
			Check(ref ok, StoryItems.QuestItems.Length > 20 && StoryItems.IconSprite(StoryItems.QuestIcon + "Vasagatan_GreenKey") != null, "Raft's quest item pictures are there (" + StoryItems.QuestItems.Length + ")");
			Click(new[] { "+ Add a story item" });
			yield return new WaitForSecondsRealtime(0.2f);
			Check(ref ok, StoryItemsWindow.Editing.Count == 1 && StoryItemsWindow.Editing[0].Id == "old-key", "adding a story item (id old-key)");
			yield return null;
			Screenshot(new[] { "story_window" });
			yield return new WaitForSecondsRealtime(0.6f); // (the picture is taken at the end of a later frame)

			// A story set: the locked door and its key (placed around the view, one undo step)
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, 0f, 500f);
			Camera.main.transform.position = c0 + new Vector3(0f, 60f, -30f);
			Camera.main.transform.LookAt(c0);
			List<GameObject> set = StorySets.LockedDoor(StoryItemsWindow.Editing, StorySets.ViewCentre());
			Check(ref ok, set.Count == 3 && StoryItemsWindow.Editing.Count == 2, "the locked door set: a door, a chest and a note, and a new story item");
			EditorGameObject door = set.Count > 0 ? set[0].GetComponent<EditorGameObject>() : null;
			string keyId = StoryItemsWindow.Editing.Count > 1 ? StoryItemsWindow.Editing[1].Id : "";
			Check(ref ok, door != null && ObjectProps.Get(door.Props, BehaviourProps.CheckKey("use")) == "has|story:" + keyId + "|1" && ObjectProps.Get(door.Props, BehaviourProps.ElseKey("use")).StartsWith("message||It's locked"),
				"the door asks for the key and says it's locked otherwise");
			EditorGameObject chest = set.Count > 1 ? set[1].GetComponent<EditorGameObject>() : null;
			Check(ref ok, chest != null && ObjectProps.Get(chest.Props, ObjectProps.LootItems) == "story:" + keyId + "*1", "the chest holds the key");
			List<GameObject> trail = StorySets.NoteTrail(StoryItemsWindow.Editing, StorySets.ViewCentre() + new Vector3(-40f, 0f, 0f));
			Check(ref ok, trail.Count == 4 && trail.Count(go => go.GetComponent<EditorGameObject>().GameObjectName.StartsWith("Note_")) == 3, "the trail of notes: three notes and a hidden chest");
			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, trail.All(go => !go.activeSelf) && set.All(go => go.activeSelf), "undo takes the trail away (the door set stays)");
			CommandUndoRedo.UndoRedoManager.Redo();
			Check(ref ok, trail.All(go => go.activeSelf), "redo brings it back");
			List<GameObject> map = StorySets.TreasureMap(StoryItemsWindow.Editing, StorySets.ViewCentre() + new Vector3(0f, 0f, 40f));
			EditorGameObject zone = map.Select(go => go.GetComponent<EditorGameObject>()).FirstOrDefault(x => x.GameObjectName == ContentCatalog.TriggerZone);
			Check(ref ok, map.Count == 3 && zone != null && ObjectProps.Get(zone.Props, BehaviourProps.CheckKey("enter")).Contains("!state|"), "the treasure map set: a bottle, a buried chest and a zone that needs the map (and a not-yet-dug chest)");
			List<GameObject> locked = StorySets.LockedChest(StoryItemsWindow.Editing, StorySets.ViewCentre() + new Vector3(40f, 0f, 40f));
			EditorGameObject lockedChest = locked.Count > 0 ? locked[0].GetComponent<EditorGameObject>() : null;
			Check(ref ok, locked.Count == 2 && lockedChest != null && ObjectProps.Get(lockedChest.Props, BehaviourProps.CheckKey("open")).StartsWith("take|story:small-key") &&
				ObjectProps.Get(locked[1].GetComponent<EditorGameObject>().Props, BehaviourProps.CheckKey("use")).StartsWith("!has|"), "the locked chest set: the chest needs the key, the driftwood gives it while the crew hasn't got it");
			StoryItemsWindow.SaveNow();
			yield return null;
			List<StoryItemDef> defs = StoryItems.Of(DynamicIslands.currentIslandProps);
			Check(ref ok, !StoryItemsWindow.IsOpen && defs.Count == 5 && defs.Any(d => d.Id == keyId && d.Icon.StartsWith(StoryItems.QuestIcon)), "saved: 5 story items on the island (" + string.Join(", ", defs.Select(d => d.Id).ToArray()) + ")");
			Check(ref ok, ContentCatalog.ItemLabel("story:" + keyId) == "Rusty key" && ContentCatalog.ItemSprite("story:" + keyId) != null, "story items have their name and picture wherever items show");

			// The item picker lists them first
			ItemPickerWindow.OpenFor(() => "", v => { });
			yield return new WaitForSecondsRealtime(0.4f);
			Transform storyGrid = EditorUI.Canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "StoryGrid");
			Check(ref ok, storyGrid != null && storyGrid.childCount == 5, "the item picker shows the island's 5 story items first (" + (storyGrid != null ? storyGrid.childCount : -1) + ")");
			Screenshot(new[] { "story_picker" });
			yield return new WaitForSecondsRealtime(0.6f);
			ItemPickerWindow.Close();

			// The door's checks in the Behaviour window
			if (door != null)
			{
				BehaviourWindow.Open(door);
				yield return new WaitForSecondsRealtime(0.5f);
				Transform part = EditorUI.Canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Checks" && t.gameObject.activeInHierarchy);
				Check(ref ok, part != null && part.GetComponentsInChildren<Transform>().Count(t => t.name == "Check") == 1, "the Behaviour window shows the door's check and what it says otherwise");
				Screenshot(new[] { "behaviour_checks" });
				yield return new WaitForSecondsRealtime(0.3f);
				BehaviourWindow.Close();
			}

			// Save and load keep the checks, waits and story items
			if (door != null) PropsCommand.Change(door, ObjectProps.With(door.Props, BehaviourProps.EventKey("use"), "switch||\nwait||3\nswitch||"));
			DynamicIslands.SaveIsland("cistory");
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor("cistory"));
			IslandObject fd = f.Objects.FirstOrDefault(o => ObjectProps.Get(o.Props, BehaviourProps.CheckKey("use")).Length > 0);
			Check(ref ok, fd != null && ObjectProps.Get(fd.Props, BehaviourProps.EventKey("use")) == "switch||\nwait||3\nswitch||" && ObjectProps.Get(fd.Props, BehaviourProps.ElseKey("use")).Length > 0 &&
				StoryItems.Of(f.Props).Count == 5, "saved and read back: checks, otherwise, waits and story items");
			File.Delete(IslandSpawner.PathFor("cistory"));

			// SL5: a story item deleted while a collect step, a "has" check and a locked chest use it
			{
				const string slId = "cikey";
				StoryItemsWindow.Open();
				yield return new WaitForSecondsRealtime(0.2f);
				StoryItemsWindow.Editing.Add(new StoryItemDef { Id = slId, Name = "CI key" });
				StoryItemsWindow.SaveNow();
				yield return null;
				Check(ref ok, StoryItems.Of(DynamicIslands.currentIslandProps).Any(d => d.Id == slId), "SL5: the story item 'cikey' is on the island");
				Func<IslandFile> slIsland = () =>
				{
					int res = 17;
					var hh = new float[res, res]; for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) hh[y, x] = 0.4f;
					var sl = new IslandFile { TerrainSize = new Vector3(64f, 60f, 64f), HeightmapResolution = res, Heights = hh, Name = "cistory-sl5" };
					foreach (var kv in DynamicIslands.currentIslandProps) sl.Props[kv.Key] = kv.Value;
					sl.Props[IslandQuest.KeySteps] = "collect|" + StoryItems.Ref(slId) + "|1|"; // (the collect step)
					// (a chest that gives it, a door whose "has" check wants it, a locked chest that takes it)
					sl.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = new Vector3(20, 25, 20), Props = new Dictionary<string, string> { { ObjectProps.LootItems, StoryItems.Ref(slId) + "*1" } } });
					sl.Objects.Add(new IslandObject { Name = "Log", Position = new Vector3(24, 25, 20), Props = new Dictionary<string, string> { { BehaviourProps.Use, "Look" }, { BehaviourProps.CheckKey("use"), "has|" + StoryItems.Ref(slId) + "|1" }, { BehaviourProps.EventKey("use"), "message||Yes" } } });
					sl.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = new Vector3(28, 25, 20), Props = new Dictionary<string, string> { { ObjectProps.LootItems, "Scrap*5" }, { BehaviourProps.CheckKey("open"), "take|" + StoryItems.Ref(slId) + "|1" }, { BehaviourProps.ElseKey("open"), "message||Locked." } } });
					return sl;
				};
				// (what the editor's Check says about the island: its quest, and its traps - PlanChecker.Check with the island's facts)
				Func<IslandFile, List<string>> slAbout = f2 => PlanChecker.QuestFindings(f2).Concat(PlanChecker.Check(new WorldPlan(), true, false, PlanChecker.FromFile(f2, f2.Name, false))).Select(x => x.Text).Where(t => t.IndexOf(slId, StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("CI key", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
				List<string> slBefore = slAbout(slIsland());
				// (the Story items window's ×: the item leaves the list, Save keeps that)
				StoryItemsWindow.Open();
				yield return new WaitForSecondsRealtime(0.2f);
				StoryItemsWindow.Editing.RemoveAll(d => d.Id == slId);
				StoryItemsWindow.SaveNow();
				yield return null;
				Check(ref ok, !StoryItems.Of(DynamicIslands.currentIslandProps).Any(d => d.Id == slId), "SL5: 'cikey' is deleted in the Story items window");
				List<string> slAfter = slAbout(slIsland()).Where(t => !slBefore.Contains(t)).ToList();
				foreach (string t in slAfter) Log("  SL5 Check says: " + t);
				Check(ref ok, slAfter.Any(t => t.IndexOf("Story items list", StringComparison.OrdinalIgnoreCase) >= 0), "SL5: Check warns that 'cikey' is used but no longer in the Story items list");
				Check(ref ok, !slBefore.Any(t => t.IndexOf("Story items list", StringComparison.OrdinalIgnoreCase) >= 0), "SL5: no such warning while 'cikey' was in the list");
				CommandUndoRedo.UndoRedoManager.Undo(); // (Ctrl+Z)
				yield return null;
				Check(ref ok, StoryItems.Of(DynamicIslands.currentIslandProps).Any(d => d.Id == slId), "SL5: Ctrl+Z brings 'cikey' back");
				CommandUndoRedo.UndoRedoManager.Undo(); // (and once more: the item added above goes, as it was)
				yield return null;
				Check(ref ok, !StoryItems.Of(DynamicIslands.currentIslandProps).Any(d => d.Id == slId), "SL5: put back - 'cikey' is gone again");
			}
			DynamicIslands.currentIslandProps.Clear();
			foreach (var kv in saved) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
			DynamicIslands.NewIsland();
			if (ok) Log("PASS: story items and checks in the editor"); else Fail("story items and checks in the editor");
		}

		#endregion

		#region World

		[ConsoleCommand(name: "CIQuestMilestones", docs: "Dev, in game (host): the quests' milestones - at 90 % done a banner for every player \"Nearing the end\", at 100 % \"The whole quest line is done\", each once per world (saved with it), straight to 100 % only that one, none with fewer than 5 quests; the journal says ALL DONE")]
		public static void QuestMilestonesCommand()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("CIQuestMilestones (in a world, host)"); return; }
			bool ok = true;
			bool hadNear = WorldDirector.Done.Contains(QuestMilestones.NearMark), hadAll = WorldDirector.Done.Contains(QuestMilestones.AllMark);
			Func<int, int, List<QuestCount.Quest>> count = (done, total) => Enumerable.Range(0, total).Select(i => new QuestCount.Quest { Name = "q" + i, Group = QuestCount.Met, Done = i < done }).ToList();
			Action clear = () => { WorldDirector.Done.Remove(QuestMilestones.NearMark); WorldDirector.Done.Remove(QuestMilestones.AllMark); };
			try
			{
				clear();
				Check(ref ok, QuestMilestones.Check(count(5, 10)) == null, "5 of 10 done: no banner");
				Check(ref ok, QuestMilestones.Check(count(8, 10)) == null, "8 of 10 (80 %): no banner");
				string near = QuestMilestones.Check(count(9, 10));
				Check(ref ok, near == QuestMilestones.NearTitle && StoryChain.LastBanner.Contains("You are nearing the end: 9 of 10 quests done (90%)"), "9 of 10 (90 %): \"" + StoryChain.LastBanner + "\"");
				Check(ref ok, QuestMilestones.Check(count(9, 10)) == null && QuestMilestones.Check(count(19, 20)) == null, "only once: not again at 9 of 10 or 19 of 20");
				string all = QuestMilestones.Check(count(10, 10));
				Check(ref ok, all == QuestMilestones.AllTitle && StoryChain.LastBanner.Contains("You have completed the whole quest line: all 10 quests"), "10 of 10: \"" + StoryChain.LastBanner + "\"");
				Check(ref ok, QuestMilestones.Check(count(12, 12)) == null, "only once: not again when more quests came and are done (12 of 12)");
				Check(ref ok, WorldDirector.WriteLines().Any(l => l.StartsWith("@done=") && l.Contains(QuestMilestones.NearMark) && l.Contains(QuestMilestones.AllMark)), "both marks are saved with the world (@done=)");
				clear();
				string straight = QuestMilestones.Check(count(10, 10));
				Check(ref ok, straight == QuestMilestones.AllTitle && QuestMilestones.Check(count(10, 10)) == null && WorldDirector.Done.Contains(QuestMilestones.NearMark), "straight to 100 %: only the whole quest line's banner (the 90 % one never comes after it)");
				clear();
				Check(ref ok, QuestMilestones.Check(count(4, 4)) == null && QuestMilestones.Check(count(3, 3)) == null, "a world with fewer than " + QuestMilestones.MinQuests + " quests: no banner (one island's quest isn't the whole quest line)");
			}
			finally
			{
				clear();
				if (hadNear) WorldDirector.Done.Add(QuestMilestones.NearMark);
				if (hadAll) WorldDirector.Done.Add(QuestMilestones.AllMark);
			}
			if (ok) Log("PASS: quest milestones"); else Fail("quest milestones");
		}

		[ConsoleCommand(name: "CIStoryWorld", docs: "Dev, in game (host): a locked door opens only with the story item from a chest (and uses it up), a lever that needs planks, a gate that closes again after a wait, the journal (notes, pages, items), the client's path, saving the story, movers in step, a locked chest, not and any-of checks, collect and pages quest steps; SL6: an item and a page with |, %, =, a line break, a-ring and o-diaeresis and an emoji are the same after the world file write/read and after the Story message round trip (caught locally)")]
		public static void StoryWorld()
		{
			StartTest(StoryWorldRoutine());
		}

		static IEnumerator StoryWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			ResetStoryEverywhere();

			IslandFile f = SampleIsland();
			f.Name = "cistoryworld";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 25f);
			f.Props[StoryItems.Key] = "brass-key|Brass key|" + StoryItems.QuestIcon + "Vasagatan_GreenKey|A small brass key.";
			int doorIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = ground(c.x + 6f, c.y), Props = P(BehaviourProps.Name, "vault", BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "0.5",
				BehaviourProps.Use, "Open the vault", BehaviourProps.CheckKey("use"), "take|story:brass-key|1", BehaviourProps.ElseKey("use"), "message||It's locked.",
				BehaviourProps.EventKey("use"), "open||\nmessage||The key turns.\njournal|The vault|We opened the vault with the brass key.") });
			int chestIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Loot_ChestSmall", Position = ground(c.x - 4f, c.y - 3f), Props = P(ObjectProps.LootItems, "story:brass-key*1", ObjectProps.NoteTitle, "Chest") });
			int gateIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = ground(c.x + 6f, c.y + 6f), Props = P(BehaviourProps.Name, "gate", BehaviourProps.Move, "0,3,0", BehaviourProps.MoveTime, "0.3") });
			int leverIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x + 3f, c.y + 3f), Props = P(BehaviourProps.Name, "lever", BehaviourProps.Use, "Pull the lever",
				BehaviourProps.CheckKey("use"), "take|Plank|1\nstate|gate|closed", BehaviourProps.ElseKey("use"), "message||It needs a plank, and the gate must be closed.",
				BehaviourProps.EventKey("use"), "open|gate|\nwait||2\nclose|gate|\nmessage||The gate falls shut.") });
			int noteIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Note_Paper", Position = ground(c.x - 2f, c.y + 4f), Props = P(ObjectProps.NoteTitle, "The keeper's note", ObjectProps.NoteText, "The key is in the small chest.") });
			int loopIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x - 6f, c.y) + Vector3.up, Props = P(BehaviourProps.Move, "0,2,0", BehaviourProps.MoveTime, "3", BehaviourProps.MoveMode, "loop",
				BehaviourProps.Spin, "40", BehaviourProps.Bob, "0.3", BehaviourProps.Collision, "none") });
			int lockedIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = ground(c.x - 8f, c.y - 6f), Props = P(ObjectProps.LootItems, "Rope*2",
				BehaviourProps.CheckKey("open"), "take|story:small-key|1", BehaviourProps.ElseKey("open"), "message||The chest is locked.") });
			int driftIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x - 8f, c.y + 6f), Props = P(BehaviourProps.Use, "Search the driftwood",
				BehaviourProps.CheckKey("use"), "!has|story:small-key|1", BehaviourProps.EventKey("use"), "give||story:small-key*1\nmessage||A small key!", BehaviourProps.ElseKey("use"), "message||Nothing else here.") });
			int bellIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x + 8f, c.y - 6f), Props = P(BehaviourProps.Use, "Ring the bell",
				BehaviourProps.CheckKey("use"), "any\nhas|story:gem|5\nsignal|bell-ok|", BehaviourProps.EventKey("use"), "message||The bell rings.", BehaviourProps.ElseKey("use"), "message||The bell is stuck.") });
			f.Props[StoryItems.Key] += "\nsmall-key|Small key||A small key.\ngem|Gem||A green gem.";
			f.Props[IslandQuest.KeyTitle] = "Gems and pages";
			f.Props[IslandQuest.KeySteps] = "collect|story:gem|2|\npages||1|";
			f.Save(IslandSpawner.PathFor("cistoryworld"));
			var created = new List<string> { "cistoryworld" };

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("cistoryworld"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("cistoryworld", spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (e == null || e.Root == null) { Fail("the island did not spawn"); yield break; }
			Func<int, IslandObjectRef> obj = i => e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(r => r.Index == i);
			Func<int, bool> isOpen = i => { ObjectState s; return e.State.TryGetValue(Behaviours.StateBase + i, out s) && s.Yield == 1; };
			yield return EnsureAlive();
			yield return StandRoutine(e.Root);
			var sent = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => sent.Add(m);
			try
			{
				// The locked door without the key
				Behaviours.Fire(e, doorIdx, "use", true);
				Check(ref ok, !isOpen(doorIdx) && Behaviours.LastMessage == "It's locked." && (Behaviours.LastFailedCheck ?? "").Contains("Brass key"),
					"without the key the vault stays shut and says so (" + Behaviours.LastFailedCheck + ")");

				// The chest gives the story item to the crew
				LootCrate chest = obj(chestIdx) != null ? obj(chestIdx).GetComponentInChildren<LootCrate>(true) : null;
				if (chest != null) { chest.Open(); NoteReader.Close(); }
				Check(ref ok, StoryBook.Count("brass-key") == 1 && StoryBook.DefOf("brass-key") != null && StoryBook.DefOf("brass-key").Name == "Brass key", "the chest gives the crew the brass key (a story item)");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Story && m.Name == "all" && (m.Data ?? "").Contains("brass-key")), "the other players are sent the story state");

				// With the key: it opens, the key is used up, the journal gets a page
				Behaviours.Fire(e, doorIdx, "use", true);
				Check(ref ok, isOpen(doorIdx) && StoryBook.Count("brass-key") == 0 && Behaviours.LastMessage == "The key turns.", "with the key the vault opens and the key is used up");
				Check(ref ok, StoryBook.Pages.Any(p => p.Title == "The vault" && p.Text.Contains("brass key")), "the journal page from the action");
				Behaviours.Fire(e, doorIdx, "use", true);
				Check(ref ok, isOpen(doorIdx) && Behaviours.LastMessage == "It's locked.", "without a key left it doesn't work again");

				// A Raft item check, and a wait: the gate opens and falls shut 2 s later
				PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
				int have = inv.GetItemCount("Plank");
				if (have > 0) inv.RemoveItem("Plank", have);
				Behaviours.Fire(e, leverIdx, "use", true);
				Check(ref ok, !isOpen(gateIdx) && Behaviours.LastMessage.StartsWith("It needs a plank"), "the lever needs a plank");
				inv.AddItem("Plank", 3);
				Behaviours.Fire(e, leverIdx, "use", true);
				Check(ref ok, isOpen(gateIdx) && inv.GetItemCount("Plank") == 2, "with a plank: the gate opens at once and the plank is used up");
				Behaviours.Fire(e, leverIdx, "use", true);
				Check(ref ok, inv.GetItemCount("Plank") == 2 && Behaviours.LastMessage.StartsWith("It needs a plank"), "while the gate is open the lever does nothing (a state check)");
			}
			finally { IslandNetwork.Loopback = null; }
			yield return new WaitForSeconds(1f);
			Check(ref ok, isOpen(gateIdx), "after 1 s the gate is still open");
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, !isOpen(gateIdx) && Behaviours.LastMessage == "The gate falls shut.", "after the 2 s wait the gate falls shut (and the message after the wait)");

			// A client's lever: the host does the shared part, with the wait
			Behaviours.OnEventMessage(e.Id, leverIdx, "use", false);
			Check(ref ok, isOpen(gateIdx), "a client's lever reaches the host: the gate opens");
			yield return new WaitForSeconds(2.5f);
			Check(ref ok, !isOpen(gateIdx), "... and falls shut after the wait on the host");
			// A client's story change reaches the host
			StoryBook.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Story, Name = "give", Data = "brass-key|2|Brass%20key||" });
			Check(ref ok, StoryBook.Count("brass-key") == 2, "a client's story change (2 keys) is applied by the host");

			// A locked chest, and a "not" check on the driftwood that hides its key
			LootCrate locked = obj(lockedIdx) != null ? obj(lockedIdx).GetComponentInChildren<LootCrate>(true) : null;
			List<string> got = locked != null ? locked.Open() : new List<string>();
			NoteReader.Close();
			Check(ref ok, locked != null && got.Count == 0 && !locked.Looted && Behaviours.LastMessage == "The chest is locked.", "the locked chest keeps its loot without the key and says so");
			Behaviours.Fire(e, driftIdx, "use", true);
			Check(ref ok, StoryBook.Count("small-key") == 1 && Behaviours.LastMessage == "A small key!", "the driftwood gives the key (the crew hasn't got one)");
			Behaviours.Fire(e, driftIdx, "use", true);
			Check(ref ok, StoryBook.Count("small-key") == 1 && Behaviours.LastMessage == "Nothing else here.", "a \"not\" check: while the crew holds the key the driftwood gives nothing");
			got = locked != null ? locked.Open() : new List<string>();
			NoteReader.Close();
			Check(ref ok, got.Count > 0 && locked.Looted && StoryBook.Count("small-key") == 0, "with the key the chest opens, gives its loot and uses the key up (" + string.Join(", ", got.ToArray()) + ")");

			// "Any of": one passing check is enough
			Behaviours.Fire(e, bellIdx, "use", true);
			Check(ref ok, Behaviours.LastMessage == "The bell is stuck.", "any of: when none passes, the otherwise message (" + Behaviours.LastFailedCheck + ")");
			e.State[Behaviours.SignalKey("bell-ok")] = new ObjectState { Active = true };
			Behaviours.Fire(e, bellIdx, "use", true);
			Check(ref ok, Behaviours.LastMessage == "The bell rings.", "any of: one passing check (the signal) is enough");

			// Counted quest steps: collect story items, find pages
			Check(ref ok, QuestTracker.StepOf(e) == 0, "the quest waits for 2 gems");
			StoryBook.Give("gem", 2);
			yield return new WaitForSecondsRealtime(1.2f);
			Check(ref ok, QuestTracker.StepOf(e) >= 1, "collecting 2 gems (story items) does the collect step");
			yield return new WaitForSecondsRealtime(1.2f);
			Check(ref ok, QuestTracker.StepOf(e) >= 2, "a journal page from this island (the vault) does the pages step: the quest is done");

			// Reading a note puts it in the journal; the journal window
			int notesFound0, notesTotal0, notesFound1, notesTotal1;
			NoteCount.Count(out notesFound0, out notesTotal0);
			CustomNote note = obj(noteIdx) != null ? obj(noteIdx).GetComponentInChildren<CustomNote>(true) : null;
			NoteReader.Open(note);
			yield return null;
			NoteReader.Close();
			Check(ref ok, StoryBook.Pages.Any(p => p.Title == "The keeper's note" && p.Text.Contains("small chest")), "a note read goes into the journal");
			// (notes found of the world's notes - the user, 2026-10-03: "5/38 notes found")
			NoteCount.Count(out notesFound1, out notesTotal1);
			Check(ref ok, notesTotal1 == notesTotal0 && notesTotal1 >= IslandCache.NotesOf(e.Name).Count && notesTotal1 > 0 && notesFound1 == notesFound0 + 1,
				"the note counts as found: " + notesFound0 + " / " + notesTotal0 + " -> " + notesFound1 + " / " + notesTotal1 + " notes found (this island has " + IslandCache.NotesOf(e.Name).Count + ")"); 
			JournalWindow.Open();
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, JournalWindow.IsOpen && JournalWindow.ShownTitle != null, "the journal opens (" + JournalWindow.ShownTitle + ")");
			// (the Progress panel: every category as found / total - the user, 2026-10-03)
			List<string> progress = JournalWindow.ProgressShown;
			List<WorldProgress.Row> rowsNow = WorldProgress.Rows();
			// (CA31: the saved islands ticked for the world's sailing count before they come - a world with ~50 ticked showed 3/19)
			string ticked = QuestCount.TickedToCome().FirstOrDefault();
			int islandsTotal = rowsNow.First(r => r.Name == "Islands reached").Total;
			if (ticked != null)
			{
				WorldIslands.Off.Add(ticked);
				QuestCount.Recount();
				int without = WorldProgress.Rows().First(r => r.Name == "Islands reached").Total;
				WorldIslands.Off.Remove(ticked);
				QuestCount.Recount();
				Check(ref ok, without == islandsTotal - 1, "Islands reached counts the ticked saved islands still to come: " + islandsTotal + ", " + without + " with '" + ticked + "' left out");
			}
			else Log("(no saved island ticked for sailing here: the count of ticked islands isn't checked)");
			Check(ref ok, progress.Count == 6 && progress[0].StartsWith("Quests  " + rowsNow[0].Done + "/" + rowsNow[0].Total) && progress.Any(l => l == "Notes found  " + notesFound1 + "/" + notesTotal1)
				&& progress.Any(l => l.StartsWith("Story items found  ")) && progress.Any(l => l.StartsWith("Islands reached  ")) && progress.Any(l => l.StartsWith("Journal pages  ")) && progress.Last().StartsWith("Overall  ") && progress.Last().EndsWith("%"),
				"the journal's Progress panel: " + string.Join(" | ", progress.ToArray()));
			List<string> helps = JournalWindow.ProgressHelp;
			Check(ref ok, helps.Count == 6 && helps.All(h => !string.IsNullOrEmpty(h)) && JournalWindow.ProgressMarks == 0, "each Progress line shows its help on hover, no ? marks (" + JournalWindow.ProgressMarks + " buttons in the panel)");
			WorldProgress.Row itemsRow = rowsNow.First(r => r.Name == "Story items found"), pagesRow = rowsNow.First(r => r.Name == "Journal pages");
			Check(ref ok, itemsRow.Total >= 1 && itemsRow.Done >= 1 && pagesRow.Done >= 2 && pagesRow.Total >= pagesRow.Done, "story items found " + itemsRow.Done + "/" + itemsRow.Total + " (the brass key, used up, still counts), journal pages " + pagesRow.Done + "/" + pagesRow.Total + " (the note and the vault's page)");
			// (and each island's line its own: "(1/1 notes)" - the user, 2026-10-03: so you know if some are left on it)
			int isleFound, isleTotal;
			bool isle = NoteCount.OfIsland(e.HostName, out isleFound, out isleTotal);
			string isleLine = JournalWindow.ListLines().FirstOrDefault(l => l.StartsWith("# " + Behaviours.IslandTitle(e))) ?? "";
			Check(ref ok, isle && isleFound >= 1 && isleLine.EndsWith("(" + isleFound + "/" + isleTotal + " notes)"), "the island's line shows its notes found: \"" + isleLine + "\"");
			// (the pages by island: under the island's name and its quest, a tick once done - the user, 2026-10-02: notes of
			// several islands in one list got mixed up)
			string islandTitle = Behaviours.IslandTitle(e), questTitle = IslandQuest.From(IslandCache.PropsOf(e)).Title;
			List<string> listed = JournalWindow.ListLines();
			int head = listed.FindIndex(l => l.StartsWith("# " + islandTitle));
			Check(ref ok, head >= 0 && listed[head].Contains(questTitle) && listed[head].Contains("done"), "the journal lists the island's pages under its name and its quest, done: \"" + (head >= 0 ? listed[head] : "(no line for " + islandTitle + ")") + "\"");
			int vault = listed.IndexOf("The vault"), keeper = listed.IndexOf("The keeper's note");
			Check(ref ok, head >= 0 && vault > head && keeper > head && !listed.Skip(head + 1).Take(Math.Max(vault, keeper) - head - 1).Any(l => l.StartsWith("# ")),
				"its pages (the vault, the keeper's note) are under it: " + string.Join(" | ", listed.ToArray()));
			StoryBook.Page vaultPage = StoryBook.Pages.FirstOrDefault(p => p.Title == "The vault");
			Check(ref ok, vaultPage != null && JournalWindow.ShowKey(vaultPage.Key) && JournalWindow.ShownText.Contains("\u2014 " + islandTitle + " (" + questTitle + "), day "),
				"a page is signed with its island and quest: \"" + (JournalWindow.ShownText ?? "").Split('\n').Last() + "\"");
			// (the world's quests done, in the head - the user, 2026-10-02: "35/100% completed quests": Raft's story islands,
			// the plan's, and every island with a quest that came; this island's quest is done)
			List<QuestCount.Quest> quests = QuestCount.All();
			QuestCount.Quest mine = quests.FirstOrDefault(q => q.Name.StartsWith(islandTitle));
			Check(ref ok, mine != null && mine.Done, "the quest count has this island's quest, done: " + (mine != null ? mine.Name + " (" + mine.Group + ")" : "missing - " + string.Join(", ", quests.Select(q => q.Name).ToArray())));
			int qDone, qTotal;
			QuestCount.Count(quests, out qDone, out qTotal);
			yield return new WaitForSeconds(0.2f);
			Check(ref ok, (JournalWindow.QuestsShown ?? "").StartsWith("QUESTS  " + qDone + " / " + qTotal + "  \u00B7  " + QuestCount.Percent(qDone, qTotal) + "%") && (qDone < qTotal || JournalWindow.QuestsShown.EndsWith("ALL DONE")), "the journal's head shows it: \"" + JournalWindow.QuestsShown + "\" (" + QuestCount.Summary(quests) + ")");
			// (only the custom islands' quests: Raft's own story isn't counted - the user, 2026-10-03)
			string[] raftNames = StoryOrder.Chain.Select(StoryOrder.Name).ToArray();
			Check(ref ok, !quests.Any(q => raftNames.Contains(q.Name)), "the count leaves Raft's story out: " + string.Join(", ", quests.Select(q => q.Name).ToArray()));
			// (the story items' list scrolls when it holds more than it shows - the user, 2026-10-03: twelve more items)
			for (int i = 1; i <= 12; i++) StoryBook.Give("ciscroll" + i, 1);
			yield return new WaitForSeconds(0.6f);
			string[] scroll = (JournalWindow.ItemsScrollCheck() ?? "0|0|False").Split('|');
			float contentH = float.Parse(scroll[0], System.Globalization.CultureInfo.InvariantCulture), viewH = scroll.Length > 1 ? float.Parse(scroll[1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
			Check(ref ok, scroll.Length == 3 && contentH > viewH + 20f && scroll[2] == "True", "the story items' list scrolls: " + (StoryBook.Items.Count()) + " items, the list " + scroll[0] + " px in a view of " + (scroll.Length > 1 ? scroll[1] : "?") + " px, the last one in view at the bottom: " + (scroll.Length > 2 ? scroll[2] : "?"));
			Screenshot(new[] { "journal_items_scroll" });
			yield return new WaitForSeconds(0.5f);
			for (int i = 1; i <= 12; i++) StoryBook.Take("ciscroll" + i, 1);
			yield return new WaitForSeconds(0.4f);
			JournalWindow.ShowQuestList();
			yield return new WaitForSeconds(0.3f);
			Check(ref ok, JournalWindow.ShownTitle.StartsWith("Quests: ") && JournalWindow.ShownText.Contains("\u221a  " + mine.Name), "a click on the count lists the quests on the paper: \"" + JournalWindow.ShownTitle + "\"");
			Screenshot(new[] { "journal_quests" });
			yield return new WaitForSeconds(0.5f); // (the picture is taken at the end of the frame)
			JournalWindow.ShowKey(vaultPage.Key);
			yield return new WaitForSeconds(0.3f);
			Screenshot(new[] { "journal" });
			yield return new WaitForSeconds(0.5f);
			JournalWindow.Close();

			// The story is saved with the world
			List<string> lines = StoryBook.WriteLines().ToList();
			int pages = StoryBook.Pages.Count;
			StoryBook.Reset();
			foreach (string l in lines) { int eq = l.IndexOf('='); StoryBook.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
			Check(ref ok, StoryBook.Count("brass-key") == 2 && StoryBook.Pages.Count == pages && StoryBook.Pages.Any(p => p.Text.Contains("\n") || p.Title == "The vault"), "the story state is written and read back (" + lines.Count + " lines)");

			// SL6 (host part): an item and a page whose texts hold | % = a line break, a/o with rings and an emoji are the same after the world file and after the Story message
			{
				const string slOdd = "Pipe | percent % equals = line\nbreak åö 😀";
				Func<string, string> slEsc = x => Uri.EscapeDataString(x);
				Func<bool> slSame = () =>
				{
					StoryBook.Held it = StoryBook.Items.FirstOrDefault(h => h.Def.Id == "sl6-item");
					StoryBook.Page pg = StoryBook.Pages.FirstOrDefault(p => p.Key == "sl6:page");
					return it != null && it.Count == 3 && it.Def.Name == slOdd && it.Def.Description == slOdd && pg != null && pg.Title == slOdd && pg.Text == slOdd && pg.Island == "Isle | one" && pg.Day == 4;
				};
				Action<IEnumerable<string>> slRead = ls => { foreach (string l2 in ls) { string t = l2.StartsWith("@") ? l2.Substring(1) : l2; int q = t.IndexOf('='); if (q > 0) StoryBook.ReadLine(t.Substring(0, q), t.Substring(q + 1)); } };
				string slPath = Path.Combine(Path.GetTempPath(), "cistory-sl6.txt");
				Action<IslandNetMessage> slLoopBefore = IslandNetwork.Loopback;
				try
				{
					StoryBook.Reset();
					StoryBook.ReadLine("story.item", string.Join("|", new[] { "sl6-item", "3", slOdd, "", slOdd, "3" }.Select(slEsc).ToArray()));
					StoryBook.ReadLine("story.page", string.Join("|", new[] { "sl6:page", "4", slOdd, "Isle | one", slOdd }.Select(slEsc).ToArray()));
					Check(ref ok, slSame(), "SL6: an item and a page with | % = a line break, åö and an emoji are kept as given");
					// the world file: written to a file, read back
					List<string> slWritten = StoryBook.WriteLines().ToList();
					File.WriteAllLines(slPath, slWritten.ToArray());
					List<string> slFromFile = File.ReadAllLines(slPath).ToList();
					StoryBook.Reset();
					slRead(slFromFile);
					Check(ref ok, slFromFile.Count == slWritten.Count && slSame(), "SL6: the same after the world file's write and read (" + slWritten.Count + " lines, one line each)");
					// the Story message: the host's state, caught locally, decoded as a client does (OnMessage \"all\")
					IslandNetMessage slMsg = null;
					IslandNetwork.Loopback = m => slMsg = m;
					IslandNetwork.SendStory(StoryBook.StateMessage());
					IslandNetwork.Loopback = slLoopBefore;
					Check(ref ok, slMsg != null && slMsg.Kind == IslandNetMessage.Story && slMsg.Name == "all" && !string.IsNullOrEmpty(slMsg.Data), "SL6: the Story message is made (" + (slMsg != null && slMsg.Data != null ? slMsg.Data.Split('\n').Length + " lines" : "none") + ")");
					if (slMsg != null)
					{
						StoryBook.Reset();
						slRead((slMsg.Data ?? "").Split('\n'));
						Check(ref ok, slSame(), "SL6: the same after the Story message's encode and decode (a line break inside a text doesn't split a line)");
					}
				}
				finally
				{
					IslandNetwork.Loopback = slLoopBefore;
					try { if (File.Exists(slPath)) File.Delete(slPath); } catch { }
					StoryBook.Reset(); // (the world's own story put back, as it was)
					slRead(lines);
				}
			}

			// Movers in step: a second copy of the island moves exactly like the first
			Check(ref ok, SharedClock.Synced, "the shared clock follows the host's water time (" + SharedClock.Now.ToString("F1") + ")");
			int count = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("cistoryworld", spot.Value + new Vector3(0f, 0f, 700f), true);
			IslandWorldState.Entry e2 = IslandWorldState.Islands.Skip(count).FirstOrDefault();
			yield return new WaitForSeconds(0.7f);
			IslandObjectRef a1 = obj(loopIdx), a2 = e2 != null && e2.Root != null ? e2.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(r => r.Index == loopIdx) : null;
			bool same = a1 != null && a2 != null && (a1.transform.localPosition - a2.transform.localPosition).magnitude < 0.01f && Quaternion.Angle(a1.transform.localRotation, a2.transform.localRotation) < 0.5f;
			Check(ref ok, same, "two copies of the island show the looping, spinning, bobbing log in the same pose" + (a1 != null && a2 != null ? " (" + a1.transform.localPosition.y.ToString("F2") + " / " + a2.transform.localPosition.y.ToString("F2") + ")" : ""));

			yield return new WaitForSeconds(0.5f);
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(before).Select(x => x.Id).ToList(), true);
			foreach (string n in created) if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n));
			ResetStoryEverywhere();
			if (ok) Log("PASS: story items, checks and waits in a world"); else Fail("story items, checks and waits in a world");
		}

		#endregion
	}
}

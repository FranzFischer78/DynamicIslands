using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Tests of the world setting Head start raft (HeadStart): the World settings button, and the raft a new world starts on.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIHeadStartToggle", docs: "Dev, main menu: the World settings window's Head start raft button goes off -> level 1 -> 2 -> 3 -> off, its label and the summary say so; the choice is put back after")]
		public static void HeadStartToggleCommand() { StartTest(HeadStartToggleRoutine()); }

		static IEnumerator HeadStartToggleRoutine()
		{
			if (LoadSceneManager.IsGameSceneLoaded) { Fail("head start button: main menu only"); yield break; }
			Button b = WorldSettingsWindow.HeadStartToggle;
			if (b == null) { Fail("head start button: no World settings window (open the New Game box once)"); yield break; }
			int before = HeadStart.Chosen;
			bool ok = true;
			try
			{
				HeadStart.Chosen = 0;
				WorldSettingsWindow.Show();
				var seen = new List<string> { UIKit.LabelOf(b).text };
				for (int i = 0; i < 4; i++) { b.onClick.Invoke(); seen.Add(UIKit.LabelOf(b).text + " = " + HeadStart.Chosen); }
				Check(ref ok, seen[0].EndsWith("off") && seen[1].Contains("LEVEL 1") && seen[2].Contains("LEVEL 2") && seen[3].Contains("LEVEL 3") && seen[4].Contains("off") && HeadStart.Chosen == 0, "the button goes round: " + string.Join(" / ", seen.ToArray()));
				b.onClick.Invoke();
				Check(ref ok, HeadStart.Describe(HeadStart.Chosen) == "level 1 (6 x 12)" && HeadStart.Width(3) == 10 && HeadStart.Length(3) == 20 && HeadStart.Width(2) == 8 && HeadStart.Length(2) == 14, "the levels' sizes: " + HeadStart.Describe(1) + ", " + HeadStart.Describe(2) + ", " + HeadStart.Describe(3));
			}
			finally { HeadStart.Chosen = before; WorldSettingsWindow.Show(); }
			yield return null;
			if (ok) Log("PASS: head start button"); else Fail("head start button");
		}

		[ConsoleCommand(name: "CIHeadStartCheck", docs: "Dev, world (host or joiner): the head start raft of a world made with Head start raft level N is there once: CIHeadStartCheck <level> [client] - at least W x L foundations, each placeable of the level exactly once (the nets 10, the storages 1/3/5), the storages hold the supplies, the placeables stand on the floor, level 3's receiver has its 3 antennas and a battery; the host's world file says built")]
		public static void HeadStartCheckCommand(string[] args) { StartTest(HeadStartCheckRoutine(args)); }

		static IEnumerator HeadStartCheckRoutine(string[] args)
		{
			int level = 0;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out level) || level < 1 || level > 3) { Fail("head start check: CIHeadStartCheck <1-3> [client]"); yield break; }
			bool client = args.Length > 1 && args[1] == "client";
			// (the host builds a moment after the world is there: up to 30 s for it)
			float until = Time.realtimeSinceStartup + 30f;
			while (!client && !HeadStart.Done && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.5f);
			yield return new WaitForSecondsRealtime(1f);
			bool ok = true;
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null) { Fail("head start check: no raft"); yield break; }
			if (!client)
			{
				Check(ref ok, HeadStart.Level == level && HeadStart.Done && !HeadStart.LastBuild.StartsWith("skipped"), "the world's head start: " + HeadStart.Describe(HeadStart.Level) + (HeadStart.Done ? ", built" : ", not built") + " - " + HeadStart.LastBuild);
				Check(ref ok, HeadStart.WriteLines().SequenceEqual(new[] { "@headstart=" + level, "@headstartdone=1" }), "the world file's lines: " + string.Join(" ", HeadStart.WriteLines().ToArray()));
			}
			List<Block> blocks = raft.GetComponentsInChildren<Block>().Where(b => b != null && b.buildableItem != null).ToList();
			Func<string, int> count = n => blocks.Count(b => b.buildableItem.UniqueName == n);
			int w = HeadStart.Width(level), l = HeadStart.Length(level);
			int floors = HeadStart.Foundations(raft).Count;
			Check(ref ok, floors >= w * l && floors <= w * l + 12, floors + " foundations for " + w + " x " + l);
			var want = new Dictionary<string, int>
			{
				{ "Placeable_CookingStand_Food_One", 1 }, { "Placeable_CookingStand_Purifier_One", 1 }, { "Placeable_ResearchTable", 1 }, { "Placeable_Bed_Basic", 1 }, { "Placeable_Storage_Small", 1 },
				{ "Placeable_CookingStand_Smelter", level >= 2 ? 2 : 0 }, { "Placeable_Storage_Medium", level >= 3 ? 4 : level == 2 ? 2 : 0 }, { "Placeable_Sail", level >= 2 ? 1 : 0 },
				{ "Placeable_CollectionNet_Basic", level >= 2 ? 10 : 0 }, { "Placeable_Reciever", level >= 3 ? 1 : 0 }, { "Placeable_Reciever_Antenna", level >= 3 ? 3 : 0 },
			};
			Check(ref ok, want.All(kv => count(kv.Key) == kv.Value), "each placeable once (no second copy): " + string.Join(", ", want.Select(kv => kv.Key.Replace("Placeable_", "") + " " + count(kv.Key) + "/" + kv.Value).ToArray()));
			// (the storages' supplies: the first holds the planks)
			List<Storage_Small> storages = blocks.Where(b => b.buildableItem.UniqueName.StartsWith("Placeable_Storage")).Select(b => b.GetComponentInChildren<Storage_Small>(true)).Where(s => s != null).ToList();
			int planks = 0, total = 0, batteries = 0, flippers = 0;
			foreach (Storage_Small s in storages)
			{
				Inventory inv = Traverse.Create(s).Field("inventoryReference").GetValue<Inventory>();
				if (inv == null) continue;
				planks += inv.GetItemCount("Plank"); batteries += inv.GetItemCount("Battery"); flippers += inv.GetItemCount("Flipper");
				total += inv.allSlots.Where(x => x != null && !x.IsEmpty).Sum(x => x.itemInstance.Amount);
			}
			int wantTotal = level >= 3 ? 50 + 52 + 52 + 150 + 110 : level == 2 ? 50 + 52 + 52 : 50;
			Check(ref ok, planks == (level >= 3 ? 50 : 10) && total == wantTotal && batteries == (level >= 2 ? 2 : 0) && flippers == (level >= 2 ? 2 : 0), "the storages hold " + total + " items (want " + wantTotal + "): " + planks + " planks, " + batteries + " batteries, " + flippers + " flippers");
			// (on the floor: each placeable's solid bottom at the foundations' top, the nets in the water)
			Physics.SyncTransforms();
			Block floor = HeadStart.Foundations(raft).FirstOrDefault();
			// (a joiner's foundation blocks have no solid collider of their own - Raft's FoundationCollider stands in - so
			// their top fell back to 0 and every placeable read 0.5-0.8 m "above the floor": the block's own y, the top on the host)
			float top = floor != null ? floor.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.enabled).Select(c => c.bounds.max.y).DefaultIfEmpty(floor.transform.position.y).Max() : 0f;
			var gaps = new List<string>();
			float worst = 0f;
			foreach (Block b in blocks.Where(x => x.buildableItem.UniqueName.StartsWith("Placeable_") && !x.buildableItem.UniqueName.Contains("CollectionNet")))
			{
				float bottom = b.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.enabled).Select(c => c.bounds.min.y).DefaultIfEmpty(float.NaN).Min();
				if (float.IsNaN(bottom)) continue;
				float gap = bottom - top;
				gaps.Add(b.buildableItem.UniqueName.Replace("Placeable_", "") + " " + gap.ToString("F2"));
				if (Mathf.Abs(gap) > Mathf.Abs(worst)) worst = gap;
			}
			Log("bottoms above the floor: " + string.Join(", ", gaps.ToArray()));
			// (too far: in the raft's own space, each placeable's height over the floor block - the same on host and joiner if
			// they are placed alike, so a wrong floor reading shows here)
			if (floor != null && Mathf.Abs(worst) >= 0.35f)
			{
				Func<Transform, float> localY = t => raft.transform.InverseTransformPoint(t.position).y;
				Vector3 tilt = raft.transform.eulerAngles;
				Log("raft tilt x " + tilt.x.ToString("F1") + " z " + tilt.z.ToString("F1") + ", floor block " + floor.name + " top " + top.ToString("F2") + " (its own y " + floor.transform.position.y.ToString("F2") + "); heights in the raft's space: "
					+ string.Join(", ", blocks.Where(x => x.buildableItem.UniqueName.StartsWith("Placeable_") && !x.buildableItem.UniqueName.Contains("CollectionNet"))
						.Select(x => x.buildableItem.UniqueName.Replace("Placeable_", "") + " " + (localY(x.transform) - localY(floor.transform)).ToString("F2")).ToArray()));
			}
			Check(ref ok, Mathf.Abs(worst) < 0.35f, "the placeables stand on the floor (the farthest " + worst.ToString("F2") + " m)");
			if (level >= 3)
			{
				Reciever rec = blocks.Where(b => b.buildableItem.UniqueName == "Placeable_Reciever").Select(b => b.GetComponentInChildren<Reciever>(true)).FirstOrDefault();
				Battery slot = rec != null ? rec.GetComponentInChildren<Battery>(true) : null;
				int connected = 0;
				try { var list = Traverse.Create(rec).Field("antennas").GetValue() as System.Collections.ICollection; connected = list != null ? list.Count : 0; } catch { }
				Check(ref ok, rec != null && slot != null && !slot.BatterySlotIsEmpty && (client || connected == 3), "the receiver: " + connected + " antennas connected, battery " + (slot != null && !slot.BatterySlotIsEmpty ? "in (" + slot.BatteryUses + ")" : "missing"));
			}
			if (ok) Log("PASS: head start raft level " + level + (client ? " (joiner)" : "")); else Fail("head start raft level " + level);
		}
	}
}

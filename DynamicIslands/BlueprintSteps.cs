using System;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// The "blueprint" quest step (SL8): the player must find one of Raft's blueprints (Blueprint_X) - picked up and carried,
	/// or already learned at the research table. Empty target: any of the blueprints on Raft's story islands
	/// (raft_blueprints.txt). Every machine checks its own player (QuestTracker.CheckBlueprints); a blueprint missed on its
	/// island can be bought at a Raft store on the plan's later islands (RaftStore sells the story blueprints the crew lacks).
	/// </summary>
	public static class BlueprintSteps
	{
		public const string Type = "blueprint";

		/// <summary>Blueprint -> the items whose recipe it teaches (learned = any of them learned), built once.</summary>
		static readonly Dictionary<string, List<Item_Base>> teaches = new Dictionary<string, List<Item_Base>>(StringComparer.OrdinalIgnoreCase);

		/// <summary>The items a blueprint teaches. Raft links them either way round: the item's recipe names its blueprint, or
		/// the blueprint's own recipe names its item.</summary>
		public static List<Item_Base> Teaches(string bp)
		{
			List<Item_Base> list;
			if (teaches.TryGetValue(bp, out list)) return list;
			List<Item_Base> items = ItemManager.GetAllItems() ?? new List<Item_Base>();
			list = items.Where(i => i != null && i.settings_recipe != null &&
				((i.settings_recipe.BlueprintItem != null && i.settings_recipe.BlueprintItem.UniqueName == bp) ||
				 (i.settings_recipe.ExtraBlueprintItems != null && i.settings_recipe.ExtraBlueprintItems.Any(x => x != null && x.UniqueName == bp)))).ToList();
			Item_Base bpItem = items.FirstOrDefault(i => i != null && i.UniqueName == bp);
			if (bpItem != null && bpItem.settings_recipe != null && bpItem.settings_recipe.BlueprintItem != null && bpItem.settings_recipe.BlueprintItem != bpItem) list.Add(bpItem.settings_recipe.BlueprintItem);
			if (items.Count > 0) teaches[bp] = list;
			return list;
		}

		/// <summary>Whether Raft has an item of that name (false before Raft's items are loaded too).</summary>
		public static bool Exists(string bp) { return ItemManager.GetItemByName(bp) != null; }

		/// <summary>Whether the local player has the blueprint: carries it, or learned what it teaches.</summary>
		public static bool Has(string bp)
		{
			Network_Player me = RAPI.GetLocalPlayer();
			if (me != null && me.Inventory != null && me.Inventory.GetItemCount(bp) > 0) return true;
			return Teaches(bp).Any(i => i.settings_recipe != null && i.settings_recipe.Learned);
		}

		/// <summary>Raft's story-island blueprints (raft_blueprints.txt), sorted.</summary>
		public static List<string> StoryBlueprints()
		{
			ScrambledBlueprints.Read();
			return ScrambledBlueprints.OnIslands.Keys.OrderBy(b => b, StringComparer.OrdinalIgnoreCase).ToList();
		}

		/// <summary>The blueprint a step's target asks for that the local player has (the target, or for an empty target the
		/// first story blueprint found), or null.</summary>
		public static string Found(string target)
		{
			string t = (target ?? "").Trim();
			if (t.Length > 0) return Has(t) ? t : null;
			return StoryBlueprints().FirstOrDefault(Has);
		}

		/// <summary>The blueprints a quest editor's list offers: every Raft item named Blueprint_*, story ones first.</summary>
		public static List<string> All()
		{
			List<string> story = StoryBlueprints();
			List<Item_Base> items = ItemManager.GetAllItems() ?? new List<Item_Base>();
			return story.Concat(items.Where(i => i != null && i.UniqueName != null && i.UniqueName.StartsWith("Blueprint_")).Select(i => i.UniqueName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}
	}
}

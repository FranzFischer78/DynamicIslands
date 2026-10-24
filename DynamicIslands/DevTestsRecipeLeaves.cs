using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RaftModLoader;
using HMLLibrary;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Dev dump: every item's craft cost and cooking/smelting result, for the raw-resource audit.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIRecipeLeaves", docs: "Dev: writes every item's craft cost (CRAFT), cooking/smelting result (COOK) and simple recipe settings to D:\\Temp\\scratch_8e762ddb\\raft_recipes.txt")]
		public static void CIRecipeLeaves()
		{
			try
			{
				string dir = @"D:\Temp\scratch_8e762ddb";
				Directory.CreateDirectory(dir);
				var lines = new List<string>();
				foreach (Item_Base it in ItemManager.GetAllItems())
				{
					if (it == null) continue;
					string id = it.UniqueName + "#" + it.UniqueIndex;
					var r = it.settings_recipe;
					if (r != null && r.NewCost != null && r.NewCost.Length > 0)
					{
						string info = RlFields(r);
						string cost = string.Join("; ", r.NewCost.Where(c => c != null).Select(c => string.Join("|", (c.items ?? new Item_Base[0]).Where(i => i != null).Select(i => i.UniqueName).ToArray()) + " x" + c.amount).ToArray());
						lines.Add("CRAFT\t" + id + "\t" + cost + "\t" + info);
					}
					object ck = it.settings_cookable;
					if (ck != null)
					{
						string s = RlFields(ck);
						if (s.Contains("Result=") && !s.Contains("Result=null")) lines.Add("COOK\t" + id + "\t" + s);
					}
					lines.Add("ITEM\t" + id + "\t" + it.settings_Inventory.DisplayName);
				}
				File.WriteAllLines(Path.Combine(dir, "raft_recipes.txt"), lines.ToArray());
				Log("PASS CIRecipeLeaves: " + lines.Count + " lines");
			}
			catch (Exception e) { Fail("CIRecipeLeaves: " + e); }
			Log("IDLE");
		}

		static string RlFields(object o)
		{
			var parts = new List<string>();
			foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				object v; try { v = f.GetValue(o); } catch { continue; }
				if (f.Name == "NewCost") continue;
				if (v == null) { parts.Add(f.Name + "=null"); continue; }
				Type t = v.GetType();
				if (t.IsPrimitive || t.IsEnum || v is string) parts.Add(f.Name + "=" + v);
				else if (v is Item_Base) parts.Add(f.Name + "=" + ((Item_Base)v).UniqueName);
				else if (v is Cost) { var c = (Cost)v; parts.Add(f.Name + "=" + (c.item != null ? c.item.UniqueName : "null") + " x" + c.amount); }
			}
			return string.Join(" ", parts.ToArray());
		}
	}
}

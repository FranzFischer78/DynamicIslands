using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's quest items on custom islands (ROADMAP LM12, the user 2026-10-05: every object and quest item of Raft's story
	/// islands available to island makers). Raft's quest item pickups (Vasagatan's keys and keycard, Tangaroa's tokens and
	/// tapes, Utopia's cable, Varuna Point's crane key...) are objects in the editor - their models; placed, a player picks
	/// one up with the interact key and the crew gets it as a story item: "raft-&lt;its quest item type&gt;", with Raft's own
	/// name and picture, needing no definition on the island (StoryItems.Find knows them). Checks and chests use it as any
	/// story item (story:raft-Vasagatan_KeyCardOffice), and it stays with the crew for later islands until a check uses it up.
	/// </summary>
	public static class QuestItemPickups
	{
		public const string IdPrefix = "raft-";
		/// <summary>A pickup picked up (one player's - Claims): KeyBase + object, after CodeLock.LockKeyBase 0xA0000 + 0xFFFF.</summary>
		public const int KeyBase = 0xB0000;
		static readonly Regex Model = new Regex(@"^QuestItemPickup_", RegexOptions.IgnoreCase);
		/// <summary>Model name -> Raft's quest item type (read from the scene's pickup before its scripts were taken off).</summary>
		static readonly Dictionary<string, string> types = new Dictionary<string, string>();

		public static bool IsModel(string name) { return name != null && Model.IsMatch(name); }

		/// <summary>The story item a model gives ("raft-Vasagatan_Key_Red"), or null when Raft's quest item isn't known.</summary>
		public static string StoryId(string model)
		{
			string type;
			if (model == null || !types.TryGetValue(model, out type)) type = Guess(model);
			return type != null ? IdPrefix + type : null;
		}

		/// <summary>Reads which quest item a scene's pickup gives: a field of Raft's quest item type on its scripts, or in its yield.</summary>
		internal static void Remember(string model, GameObject pickup)
		{
			if (types.ContainsKey(model)) return;
			const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (MonoBehaviour m in pickup.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (m == null) continue;
				foreach (FieldInfo f in m.GetType().GetFields(all))
				{
					object v;
					try { v = f.GetValue(m); } catch { continue; }
					string t = TypeOf(v);
					if (t == null && v is System.Collections.IEnumerable && !(v is string))
						foreach (object e in (System.Collections.IEnumerable)v) { t = TypeOf(e) ?? TypeOfFields(e); if (t != null) break; }
					if (t != null) { types[model] = t; return; }
				}
			}
		}

		static string TypeOf(object v)
		{
			if (v is SO_QuestItem) return ((SO_QuestItem)v).questItemType.ToString();
			if (v != null && v.GetType().Name == "QuestItemType") return v.ToString();
			return null;
		}

		static string TypeOfFields(object o)
		{
			if (o == null || o is UnityEngine.Object) return null;
			foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				string t;
				try { t = TypeOf(f.GetValue(o)); } catch { continue; }
				if (t != null) return t;
			}
			return null;
		}

		/// <summary>A quest item type from the model's name, when the scene wasn't read ("QuestItemPickup_Vasagatan_Key_Red").</summary>
		static string Guess(string model)
		{
			if (model == null) return null;
			string n = Model.Replace(model, "").Replace(" ", "");
			SO_QuestItem q = StoryItems.QuestItems.FirstOrDefault(x => x.questItemType.ToString().Equals(n, StringComparison.OrdinalIgnoreCase))
				?? StoryItems.QuestItems.FirstOrDefault(x => n.IndexOf(x.questItemType.ToString(), StringComparison.OrdinalIgnoreCase) >= 0 || x.questItemType.ToString().IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
			return q != null ? q.questItemType.ToString() : null;
		}

		/// <summary>The story item definition of one of Raft's quest items ("raft-Tangaroa_KeyCard"), or null.</summary>
		public static StoryItemDef Def(string id)
		{
			if (id == null || !id.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase)) return null;
			string type = id.Substring(IdPrefix.Length);
			SO_QuestItem q = StoryItems.QuestItems.FirstOrDefault(x => x.questItemType.ToString().Equals(type, StringComparison.OrdinalIgnoreCase));
			if (q == null) return null;
			string name = Regex.Replace(type.Replace('_', ' '), "([a-z])([A-Z])", "$1 $2");
			return new StoryItemDef { Id = id, Name = name, Icon = StoryItems.QuestIcon + q.questItemType, Description = "One of Raft's quest items." };
		}

		/// <summary>What a placed model starts with: picked up with the interact key, it gives its quest item and goes.</summary>
		public static void Defaults(string model, Dictionary<string, string> props)
		{
			string id = StoryId(model);
			if (id == null) return;
			StoryItemDef d = Def(id);
			props[BehaviourProps.Use] = "Pick up the " + (d != null ? d.Name.ToLowerInvariant() : "item");
			props[BehaviourProps.EventKey("use")] = "give||" + StoryItems.Ref(id) + "*1\nhide|";
		}
	}
}

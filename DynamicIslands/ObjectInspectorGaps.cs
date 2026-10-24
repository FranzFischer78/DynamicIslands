using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>The inspector's rows for the gap features (2026-10-24): a zone's hazard, a generator's parts, a creature's zap,
	/// a boss's shield, a light that keeps creatures back.</summary>
	public static partial class ObjectInspector
	{
		/// <summary>A trigger zone's hazard (Hazards): kind, damage, what protects.</summary>
		static void HazardGroup(EditorGameObject target)
		{
			Dictionary<string, string> p = target.Props;
			string kind = Hazards.KindOf(p);
			RectTransform g = UIKit.Group(root, "Hazard");
			RectTransform row = UIKit.Row(g, 26f, 3f, "HazardKinds");
			foreach (string k in Hazards.Kinds)
			{
				string kk = k;
				Button b = UIKit.Button(row, Hazards.Label(k), () =>
				{
					var np = new Dictionary<string, string>(target.Props);
					if (kk.Length == 0) { np.Remove(Hazards.Kind); np.Remove(Hazards.Damage); np.Remove(Hazards.Safe); np.Remove(Hazards.Message); }
					else { np[Hazards.Kind] = kk; np.Remove(Hazards.Damage); }
					PropsCommand.Change(target, np);
					Refresh();
				}, Hazards.Hint(k), -1, 26f, 11);
				UIKit.SetActive(b, k == kind);
			}
			if (kind.Length == 0)
			{
				UIKit.Label(g, "<i>A hazard hurts players standing in the zone (acid, live water, fumes, spikes, cold), darkens the screen, or takes their breath - unless something protects them. Hide the zone (a lever, a generator) to switch it off.</i>", 11, UIKit.TextMuted);
				return;
			}
			UIKit.Label(g, Hazards.Hint(kind), 11, UIKit.TextMuted);
			if (kind != Hazards.Dark)
				UIKit.Slider(g, kind == Hazards.Deep ? "Breath lost / s" : "Damage / s", 0f, Hazards.MaxDamage, Hazards.DamageOf(p), v => v.ToString("0", CultureInfo.InvariantCulture),
					v => Set(target, Hazards.Damage, ObjectProps.Format(Mathf.Round(v)), Hazards.DefaultDamage(kind).ToString("0", CultureInfo.InvariantCulture), false), "How hard it hurts (each second)", true);
			UIKit.Size(UIKit.Label(g, "Protected when (one line each; empty = " + Hazards.DescribeSafe(Hazards.DefaultSafe(kind)) + ")", 12, UIKit.TextMuted).gameObject, -1, 16);
			InputField safe = UIKit.TextArea(g, "suit / wears|HeadLight* / holds|Torch / has|story:charm|1 / state|Valve|closed / none", 46f,
				"Lines like the behaviour window's checks: suit (a protective suit is on), wears|item, holds|item, has|item|n, state|name|open, signal|name, quest|steps. One holding is enough. none = nothing protects");
			safe.text = ObjectProps.Get(p, Hazards.Safe);
			safe.onEndEdit.AddListener(v => Set(target, Hazards.Safe, v.Trim().Length > 0 ? v.Trim() : null));
			amountFields.Add(safe);
			UIKit.Label(g, "Now: protected when " + Hazards.DescribeSafe(Hazards.SafeOf(p)) + ".", 11, UIKit.TextMuted);
			InputField msg = UIKit.TextArea(g, Hazards.DefaultMessage(kind), 36f, "What the player reads while it hurts (at most every 6 s; empty = the hazard's own line)");
			msg.text = ObjectProps.Get(p, Hazards.Message);
			msg.characterLimit = 200;
			msg.onEndEdit.AddListener(v => Set(target, Hazards.Message, v.Trim().Length > 0 ? v.Trim() : null));
			amountFields.Add(msg);
		}

		/// <summary>A generator's parts, a creature's zap and a boss's shield, a light that keeps creatures back.</summary>
		static void GapGroup(EditorGameObject target, ContentCatalog.CreatureKind kind)
		{
			Dictionary<string, string> p = target.Props;
			bool gen = ReadyPieces.IsGenerator(target.GameObjectName) && ReadyPieces.PartsOf(p) > 0;
			if (gen)
			{
				RectTransform g = UIKit.Group(root, "Generator");
				UIKit.Slider(g, "Parts needed", 1f, 6f, ReadyPieces.PartsOf(p), v => v.ToString("0", CultureInfo.InvariantCulture), v =>
				{
					var np = new Dictionary<string, string>(target.Props);
					ReadyPieces.SetParts(target.GameObjectName, np, Mathf.RoundToInt(v));
					PropsCommand.Change(target, np);
				}, "How many generator parts (Raft's quest item) it wants: place that many generator part pickups on the island", true);
			}
			if (kind != null)
			{
				RectTransform g = UIKit.Group(root, "Danger");
				UIKit.Slider(g, "Zap", 0f, GapFeatures.MaxZap, GapFeatures.ZapOf(p), v => v <= 0f ? "off" : v.ToString("0", CultureInfo.InvariantCulture),
					v => Set(target, GapFeatures.Zap, v <= 0f ? null : ObjectProps.Format(Mathf.Round(v)), null, false), "The animals shock a player who comes within 2 m (Tangaroa's butler bots): this much damage, at most every 2 s", true);
				bool shield = ObjectProps.GetBool(p, GapFeatures.Shield, false);
				RectTransform row = UIKit.Row(g, 26f, 4f, "Shield");
				UIKit.Size(UIKit.Label(row, "In its phase", 12, UIKit.TextMuted).gameObject, 80);
				Button off = UIKit.Button(row, "Weapons hurt it", () => { Set(target, GapFeatures.Shield, null); Refresh(); }, "Players' weapons hurt the boss all through its fight", -1, 26f, 12);
				Button on = UIKit.Button(row, "Only blasts hurt it", () => { Set(target, GapFeatures.Shield, "1"); Refresh(); },
					"Once its phase starts (boss phases), weapons glance off: only a \"hurt creatures\" action does harm - an explosive barrel, a falling pillar. Name the spot (\"boss\") so they find it", -1, 26f, 12);
				UIKit.SetActive(off, !shield);
				UIKit.SetActive(on, shield);
				return;
			}
			if (ContentCatalog.IsZone(target.GameObjectName)) return;
			// (lights only - a spotlight, a lamp, a torch - or one set so already)
			string n = target.GameObjectName.ToLowerInvariant();
			if (GapFeatures.RepelOf(p) <= 0f && !n.Contains("light") && !n.Contains("lamp") && !n.Contains("torch") && !n.Contains("lantern")) return;
			RectTransform l = UIKit.Group(root, "Keeps creatures back");
			UIKit.Slider(l, "Radius", 0f, GapFeatures.MaxRepel, GapFeatures.RepelOf(p), v => v <= 0f ? "off" : v.ToString("0", CultureInfo.InvariantCulture) + " m",
				v => Set(target, GapFeatures.Repel, v <= 0f ? null : ObjectProps.Format(Mathf.Round(v)), null, false),
				"While it is shown, the island's animals (and Raft's jellyfish) are kept out of this radius - a light against the dark's creatures. Hide it (a lever) to switch it off", true);
		}
	}
}

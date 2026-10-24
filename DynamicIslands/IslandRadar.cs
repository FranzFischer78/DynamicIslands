using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Custom islands show up on Raft's Receiver (roadmap 1.4 "how to find islands"; Discord ideas: coordinates /
	/// radio codes for custom islands). Raft's receiver draws a dot per signal (ChunkManager points) in HandleUI
	/// every frame; after it, we draw our own dots, one per island in the world's island list, the same way:
	/// direction relative to the receiver, distance clamped to the radar edge, "123m" label. They are tinted green
	/// so they can be told apart from story signals. Purely local UI: every player's receiver shows the islands that
	/// player's game knows about (clients get the list from the host). Off with showOnReceiver = 0 in spawnpool.txt.
	/// </summary>
	[HarmonyPatch(typeof(Reciever), "HandleUI")]
	static class IslandRadar
	{
		static readonly Color DotColor = new Color(0.35f, 1f, 0.45f);
		static readonly Dictionary<Reciever, List<Reciever_Dot>> dots = new Dictionary<Reciever, List<Reciever_Dot>>();

		static void Postfix(Reciever __instance)
		{
			try { Draw(__instance); }
			catch (System.Exception e)
			{
				// Never break Raft's own receiver; report once per receiver
				if (!failed.Contains(__instance)) { failed.Add(__instance); Debug.LogWarning("[CUSTOM ISLANDS] Receiver dots: " + e); }
			}
		}

		static readonly HashSet<Reciever> failed = new HashSet<Reciever>();

		/// <summary>Dots currently drawn on a receiver (dev tests).</summary>
		internal static List<Reciever_Dot> DotsOf(Reciever r)
		{
			List<Reciever_Dot> list;
			return dots.TryGetValue(r, out list) ? list : new List<Reciever_Dot>();
		}

		/// <summary>An island the players still need (its quest begun, one the plan waits for: ReturningIslands), cached a second.</summary>
		static bool Needed(IslandWorldState.Entry e)
		{
			float t;
			bool v;
			if (neededAt.TryGetValue(e.Id, out t) && Time.time - t < 1f && needed.TryGetValue(e.Id, out v)) return v;
			try { v = ReturningIslands.Why(e) != null; } catch { v = false; }
			needed[e.Id] = v; neededAt[e.Id] = Time.time;
			return v;
		}

		/// <summary>SL9: an island whose rule needs a blueprint (needs=engine...) shows once this player has it (BlueprintSteps.Has).</summary>
		internal static bool Unlocked(IslandWorldState.Entry e)
		{
			string bp = ReturningIslands.NeedsOf(e);
			if (bp.Length == 0) return true;
			float t;
			bool v;
			if (hasAt.TryGetValue(bp, out t) && Time.time - t < 1f && has.TryGetValue(bp, out v)) return v;
			v = BlueprintSteps.Has(bp);
			has[bp] = v; hasAt[bp] = Time.time;
			return v;
		}

		static readonly Dictionary<string, float> hasAt = new Dictionary<string, float>();
		static readonly Dictionary<string, bool> has = new Dictionary<string, bool>();

		static readonly Dictionary<int, float> neededAt = new Dictionary<int, float>();
		static readonly Dictionary<int, bool> needed = new Dictionary<int, bool>();
		/// <summary>The islands shown this frame (one list kept: HandleUI runs every frame).</summary>
		static readonly List<IslandWorldState.Entry> islands = new List<IslandWorldState.Entry>();
		/// <summary>The islands the last Draw showed, dot i for island i (tests).</summary>
		internal static List<IslandWorldState.Entry> Shown { get { return islands.ToList(); } }
		/// <summary>Each dot's text and what it was made from (label, metres): made again only when they change.</summary>
		sealed class DotText { public string Label, Text; public int Metres; }
		static readonly Dictionary<Reciever_Dot, DotText> dotText = new Dictionary<Reciever_Dot, DotText>();

		internal static void Draw(Reciever r)
		{
			if (r == null || r.dotPrefab == null || r.dotParent == null) return;
			List<Reciever_Dot> list;
			if (!dots.TryGetValue(r, out list))
			{
				foreach (Reciever dead in dots.Keys.Where(k => k == null).ToList()) dots.Remove(dead);
				dots[r] = list = new List<Reciever_Dot>();
			}

			bool show = WorldRules.ShowOnReceiver && r.radarSection != null && r.radarSection.activeSelf; // (the host's setting)
			// (not the randomizer's extras on Raft's own islands: Raft shows those islands itself)
			// (ROADMAP LM2: only what helps - the islands within receiverDistance, and those the players still need however far)
			float range = WorldRules.ReceiverDistance;
			Vector3 rp0 = r.transform.position;
			islands.Clear();
			IList<IslandWorldState.Entry> all = IslandWorldState.Islands;
			for (int k = 0; k < all.Count; k++)
			{
				IslandWorldState.Entry e = all[k];
				if (!WorldRandomizer.IsExtras(e) && !e.Excluded && (range <= 0f || new Vector2(e.Position.x - rp0.x, e.Position.z - rp0.z).magnitude <= range || Needed(e)) && Unlocked(e)) islands.Add(e);
			}
			int wanted = show ? islands.Count : 0;
			while (list.Count < wanted)
			{
				Reciever_Dot d = Object.Instantiate(r.dotPrefab, r.dotParent);
				d.transform.localScale = r.dotPrefab.transform.localScale;
				// The dot looks these up in its own Awake, which hasn't run if the receiver's UI is inactive
				if (d.rect == null) d.rect = (RectTransform)d.transform;
				if (d.dotNumberText == null) d.dotNumberText = d.GetComponentInChildren<Text>(true);
				if (d.dotImage == null) d.dotImage = d.GetComponentInChildren<Image>(true);
				foreach (Graphic g in d.GetComponentsInChildren<Graphic>(true)) if (!(g is Text)) g.color = DotColor;
				list.Add(d);
			}

			Vector3 rp = r.transform.position;
			for (int i = 0; i < list.Count; i++)
			{
				Reciever_Dot d = list[i];
				if (d == null) continue;
				bool on = i < wanted;
				if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
				if (!on) continue;

				// Same maths as Reciever.HandleUI: angle from the receiver's forward, distance clamped to the radar edge
				Vector3 p = islands[i].Position; // follows world shifts, also while the island is unloaded
				Vector2 delta = new Vector2(p.x - rp.x, p.z - rp.z);
				float a = Vector2.SignedAngle(Vector2.up, delta);
				if (a < 0f) a += 360f;
				a = 360f - a;
				float rad = (360f - r.transform.eulerAngles.y + a) * Mathf.Deg2Rad;
				Vector3 dir = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f) * -1f;
				float dist = delta.magnitude;
				d.SetTargetedByReciever(true);
				d.SetLocalPosition(dir * (r.radarUIWidth * 0.5f * Mathf.Clamp01(dist / r.radarLength)));
				d.SetLengthToPoint(dist);
				// Islands brought by a rule (WorldDirector) carry a name, e.g. "Old camp 820m"
				string label = islands[i].Label ?? "";
				int metres = Mathf.RoundToInt(dist);
				DotText t;
				if (!dotText.TryGetValue(d, out t)) dotText[d] = t = new DotText();
				if (t.Text == null || t.Label != label || t.Metres != metres)
				{
					t.Label = label;
					t.Metres = metres;
					if (label.Length > 18) label = label.Substring(0, 17) + "\u2026";
					t.Text = (label.Length > 0 ? label + " " : "") + metres + "m";
				}
				// (set again only if something else wrote over it)
				if (d.dotNumberText == null || d.dotNumberText.text != t.Text) d.SetText(t.Text);
				r.isCurrentlyShowingRadarDot = true;
			}
		}
	}
}

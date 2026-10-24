using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Hazard zones (G1/G13, 2026-10-24): a trigger zone with zone.hazard hurts the local player while they stand in it
	/// (each machine its own player, every second), unless one of its protection lines holds (zone.safe; empty = the
	/// hazard's own default, "none" = nothing protects). Lines are checks like a behaviour's: has / wears / holds an
	/// item, an object's state, a signal, the quest - or "suit": a timed protective suit is on (the "suit" action, or
	/// Raft's own hazmat suit). A hidden zone does nothing: a generator or a lever that hides the zone switches it off.
	///   acid, miasma (protected by a suit), shock, spikes, cold: damage per second (zone.damage) and a message.
	///   dark: no damage - the screen goes black unless the player wears a headlight (then a headlight's cone).
	///   deep: the player's breath drains (zone.damage per second) unless they wear an oxygen tank.
	/// </summary>
	public static class Hazards
	{
		public const string Kind = "zone.hazard", Damage = "zone.damage", Safe = "zone.safe", Message = "zone.hazardMsg";
		public const string Acid = "acid", Shock = "shock", Miasma = "miasma", Spikes = "spikes", Cold = "cold", Dark = "dark", Deep = "deep";
		public static readonly string[] Kinds = { "", Acid, Shock, Miasma, Spikes, Cold, Dark, Deep };
		public const string SuitLine = "suit", NoneLine = "none";
		public const float MaxDamage = 50f;

		public static string Label(string k)
		{
			switch (k)
			{
				case Acid: return "Acid";
				case Shock: return "Live water";
				case Miasma: return "Fumes";
				case Spikes: return "Spikes";
				case Cold: return "Cold";
				case Dark: return "Darkness";
				case Deep: return "Deep water";
			}
			return "None";
		}

		public static string Hint(string k)
		{
			switch (k)
			{
				case Acid: return "Acid burns players standing in it (a protective suit keeps it off)";
				case Shock: return "Electrified water shocks players in it (switch the power off: hide the zone with a lever)";
				case Miasma: return "Poison fumes choke players in it (a protective suit keeps it off)";
				case Spikes: return "Spikes hurt players walking over them";
				case Cold: return "Bitter cold hurts players standing in it";
				case Dark: return "Pitch dark: the screen goes black unless the player wears a headlight";
				case Deep: return "A deep, strong current: breath runs out fast unless the player wears an oxygen tank";
			}
			return "No hazard";
		}

		public static float DefaultDamage(string k)
		{
			switch (k)
			{
				case Acid: return 4f;
				case Shock: return 6f;
				case Miasma: return 3f;
				case Spikes: return 8f;
				case Cold: return 2f;
				case Deep: return 15f;
			}
			return 0f;
		}

		public static string DefaultMessage(string k)
		{
			switch (k)
			{
				case Acid: return "The acid burns! Something would keep it off.";
				case Shock: return "Zzzt! The water is live. The power must be switched off.";
				case Miasma: return "The fumes choke you. You need protection to go on.";
				case Spikes: return "Spikes! Watch your step.";
				case Cold: return "The cold bites through your clothes.";
				case Dark: return "It is pitch dark. You need a light to go on.";
				case Deep: return "The current pulls the air from your lungs. An oxygen tank would help.";
			}
			return "";
		}

		public static string DefaultSafe(string k)
		{
			switch (k)
			{
				case Acid:
				case Miasma: return SuitLine;
				case Dark: return "wears|HeadLight*";
				case Deep: return "wears|OxygenBottle*";
			}
			return "";
		}

		public static string KindOf(IDictionary<string, string> p)
		{
			string k = ObjectProps.Get(p, Kind).Trim().ToLowerInvariant();
			return Kinds.Contains(k) ? k : "";
		}

		public static float DamageOf(IDictionary<string, string> p)
		{
			string k = KindOf(p);
			return Mathf.Clamp(ObjectProps.GetFloat(p, Damage, DefaultDamage(k)), 0f, MaxDamage);
		}

		/// <summary>The protection lines in force (the hazard's default when none are set; "none" = nothing protects).</summary>
		public static string SafeOf(IDictionary<string, string> p)
		{
			string s = ObjectProps.Get(p, Safe).Trim();
			if (s.Equals(NoneLine, StringComparison.OrdinalIgnoreCase)) return "";
			return s.Length > 0 ? s : DefaultSafe(KindOf(p));
		}

		public static string MessageOf(IDictionary<string, string> p)
		{
			string m = ObjectProps.Get(p, Message).Trim();
			return m.Length > 0 ? m : DefaultMessage(KindOf(p));
		}

		public static string DescribeSafe(string lines)
		{
			var parts = new List<string>();
			foreach (string raw in (lines ?? "").Split('\n'))
			{
				string l = raw.Trim();
				if (l.Length == 0 || l.Equals(ObjCheck.AnyLine, StringComparison.OrdinalIgnoreCase)) continue;
				if (l.Equals(SuitLine, StringComparison.OrdinalIgnoreCase)) { parts.Add("a protective suit is on"); continue; }
				ObjCheck c = ObjCheck.Parse(l);
				parts.Add(c != null ? c.Describe() : "'" + l + "' (not a check)");
			}
			return parts.Count == 0 ? "nothing protects" : string.Join(" or ", parts.ToArray());
		}

		public static string Describe(IDictionary<string, string> p)
		{
			string k = KindOf(p);
			if (k.Length == 0) return "";
			string what = k == Dark ? "dark" : k == Deep ? Label(k).ToLowerInvariant() + " (-" + DamageOf(p).ToString("0", CultureInfo.InvariantCulture) + " breath/s)" :
				Label(k).ToLowerInvariant() + " (" + DamageOf(p).ToString("0", CultureInfo.InvariantCulture) + "/s)";
			return what;
		}

		/// <summary>The timed suit of the "suit" action (this machine's player): protects until then.</summary>
		public static float SuitUntil;

		public static bool SuitOn(Network_Player p)
		{
			if (Time.time < SuitUntil) return true;
			try { return p != null && p.HazmatSuit != null && p.HazmatSuit.Equipped; } catch { return false; }
		}

		/// <summary>The "suit" action: this machine's player wears a protective suit for some seconds (Raft's hazmat suit when
		/// it can be put on, and the mod's own timer either way).</summary>
		public static void PutOnSuit(string arg)
		{
			float s;
			if (!float.TryParse((arg ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out s) || s <= 0f) s = 120f;
			s = Mathf.Clamp(s, 5f, 3600f);
			SuitUntil = Time.time + s;
			Network_Player p = RAPI.GetLocalPlayer();
			try { if (p != null && p.HazmatSuit != null) p.HazmatSuit.Equip(s); }
			catch (Exception e) { Debug.Log("[CUSTOM ISLANDS] Raft's hazmat suit could not be put on (the mod's own suit is): " + e.Message); }
			IslandInfo.ShowMessage("You put on a protective suit (" + s.ToString("0", CultureInfo.InvariantCulture) + " s)");
			Debug.Log("[CUSTOM ISLANDS] Protective suit on for " + s.ToString("0", CultureInfo.InvariantCulture) + " s");
		}

		/// <summary>Whether one of the zone's protection lines holds for this machine's player.</summary>
		public static bool Protected(TriggerZone z, Network_Player player)
		{
			string lines = SafeOf(z.Props);
			if (lines.Length == 0) return false;
			IslandWorldState.Entry e = ContentState.EntryOf(z.transform);
			IslandObjectRef r = z.GetComponent<IslandObjectRef>();
			int index = r != null ? r.Index : -1;
			foreach (string raw in lines.Split('\n'))
			{
				string l = raw.Trim();
				if (l.Length == 0 || l.Equals(ObjCheck.AnyLine, StringComparison.OrdinalIgnoreCase)) continue;
				if (l.Equals(SuitLine, StringComparison.OrdinalIgnoreCase)) { if (SuitOn(player)) return true; continue; }
				ObjCheck c = ObjCheck.Parse(l);
				if (c != null && e != null && Behaviours.CheckNow(e, index, c)) return true;
			}
			return false;
		}
	}

	/// <summary>The checks "wears" (a worn item: headlight, flippers, oxygen tank...) and "holds" (the item in the player's
	/// hand: binoculars, a hook, an axe). A name ending in * matches every item starting so (Axe* = any axe), and the mod's
	/// upgrades count as the item they improve (the bright headlight is a headlight).</summary>
	public static class GapChecks
	{
		public static bool NameMatches(string item, string want)
		{
			if (string.IsNullOrEmpty(item) || string.IsNullOrEmpty(want)) return false;
			want = want.Trim();
			bool prefix = want.EndsWith("*");
			string w = want.TrimEnd('*');
			Func<string, bool> m = n => !string.IsNullOrEmpty(n) && (prefix ? n.StartsWith(w, StringComparison.OrdinalIgnoreCase) : n.Equals(w, StringComparison.OrdinalIgnoreCase));
			if (m(item)) return true;
			ExtraUpgrades.Upgrade u = ExtraUpgrades.Find(item);
			return u != null && m(u.BaseName);
		}

		public static bool Wears(string want)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null || p.PlayerEquipment == null) return false;
			foreach (int i in p.PlayerEquipment.GetEquipedIndexes())
			{
				Item_Base it = ItemManager.GetItemByIndex(i);
				if (it != null && NameMatches(it.UniqueName, want)) return true;
			}
			return false;
		}

		public static bool Holds(string want)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null || p.Inventory == null) return false;
			ItemInstance s = p.Inventory.GetSelectedHotbarItem();
			return s != null && s.baseItem != null && NameMatches(s.baseItem.UniqueName, want);
		}
	}

	/// <summary>A dark zone's screen (this machine): black without a light, a headlight's cone with one. Drawn while a dark
	/// zone said so in the last half second, so walking out (or the zone hidden) clears it.</summary>
	public class DarknessOverlay : MonoBehaviour
	{
		static DarknessOverlay instance;
		static float darkAt = -10f, litAt = -10f;
		static Texture2D cone;
		float alpha;

		public static bool Dark { get { return Time.time - darkAt < 0.6f; } }
		public static bool Lit { get { return !Dark && Time.time - litAt < 0.6f; } }

		public static void Mark(bool lit)
		{
			if (instance == null && global::DynamicIslands.DynamicIslands.instance != null) instance = global::DynamicIslands.DynamicIslands.instance.gameObject.AddComponent<DarknessOverlay>();
			if (lit) litAt = Time.time; else darkAt = Time.time;
		}

		static Texture2D Cone()
		{
			if (cone != null) return cone;
			const int n = 128;
			cone = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
			var px = new Color[n * n];
			for (int y = 0; y < n; y++)
				for (int x = 0; x < n; x++)
				{
					float dx = (x - n * 0.5f) / (n * 0.5f), dy = (y - n * 0.45f) / (n * 0.5f);
					float d = Mathf.Sqrt(dx * dx + dy * dy);
					px[y * n + x] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.96f, Mathf.InverseLerp(0.2f, 0.85f, d)));
				}
			cone.SetPixels(px);
			cone.Apply();
			return cone;
		}

		void OnGUI()
		{
			if (Event.current.type != EventType.Repaint) return;
			bool dark = Dark, lit = Lit;
			alpha = Mathf.MoveTowards(alpha, dark || lit ? 1f : 0f, Time.unscaledDeltaTime * 2f);
			// (not over Raft's menus: they show the cursor)
			if (alpha <= 0.01f || Cursor.visible) return;
			Color old = GUI.color;
			var all = new Rect(0f, 0f, Screen.width, Screen.height);
			if (dark) { GUI.color = new Color(0f, 0f, 0f, 0.97f * alpha); GUI.DrawTexture(all, Texture2D.whiteTexture); }
			else { GUI.color = new Color(1f, 1f, 1f, alpha); GUI.DrawTexture(all, Cone()); }
			GUI.color = old;
		}
	}

	/// <summary>
	/// The rest of the gap features (2026-10-24):
	///   creature.zap (G11)   a creature spot's animals zap a player who comes within 2 m (Tangaroa's butler bots), that
	///                        much damage at most every 2 seconds - each machine its own player.
	///   beh.repel (G12)      a light that keeps creatures back: while the object is shown, the host pushes the island's
	///                        animals (and Raft's jellyfish) out of that radius. A lever hiding the light switches it off.
	///   hit (G4)             a shared action: the named creature spots' animals lose that share of their health (an
	///                        explosive barrel, a falling pillar); boss.shield = only such hits hurt the boss in its phase.
	/// </summary>
	public static class GapFeatures
	{
		public const string Zap = "creature.zap", Repel = "beh.repel", Shield = "boss.shield";
		public const float ZapRange = 2.2f, ZapEvery = 2f, MaxZap = 40f, MaxRepel = 30f;

		static float nextLook, zapReadyAt, nextRepel, cacheAt = -10f;
		static readonly List<KeyValuePair<IslandWorldState.Entry, CreatureSpawnPoint>> spots = new List<KeyValuePair<IslandWorldState.Entry, CreatureSpawnPoint>>();
		static readonly List<IslandObjectRef> lights = new List<IslandObjectRef>();
		/// <summary>An environment hit is being dealt (the boss shield lets it through).</summary>
		internal static bool EnvHit;
		/// <summary>Zaps and pushes on this machine since start-up (the tests count them).</summary>
		public static int Zaps, Pushes, Hits;

		public static float ZapOf(IDictionary<string, string> p) { return Mathf.Clamp(ObjectProps.GetFloat(p, Zap, 0f), 0f, MaxZap); }
		public static float RepelOf(IDictionary<string, string> p) { return Mathf.Clamp(ObjectProps.GetFloat(p, Repel, 0f), 0f, MaxRepel); }

		/// <summary>Settings an object placed from the editor's list starts with (old islands and recipes keep theirs): a
		/// generator wants 3 parts (G7), a butler bot zaps.</summary>
		public static void EditorDefaults(string name, Dictionary<string, string> props)
		{
			if (props == null) return;
			ReadyPieces.Piece rp = ReadyPieces.Of(name);
			if (rp != null && rp.Kind == ReadyPieces.Generator) ReadyPieces.SetParts(name, props, ReadyPieces.DefaultGeneratorParts);
			ContentCatalog.CreatureKind k = ContentCatalog.CreatureOf(name);
			if (k != null && k.Type == AI_NetworkBehaviourType.ButlerBot) props[Zap] = "8";
		}

		static void Cache()
		{
			if (Time.time < cacheAt) return;
			cacheAt = Time.time + 3f;
			spots.Clear();
			lights.Clear();
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e == null || e.Root == null) continue;
				foreach (CreatureSpawnPoint p in e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true)) spots.Add(new KeyValuePair<IslandWorldState.Entry, CreatureSpawnPoint>(e, p));
				foreach (IslandObjectRef r in e.Root.GetComponentsInChildren<IslandObjectRef>(true)) if (RepelOf(r.Props) > 0f) lights.Add(r);
			}
		}

		public static void Tick()
		{
			if (Time.time < nextLook) return;
			nextLook = Time.time + 0.5f;
			Cache();
			Network_Player pl = RAPI.GetLocalPlayer();
			if (pl != null && Time.time >= zapReadyAt && spots.Any(s => s.Value != null && ZapOf(s.Value.Props) > 0f)) ZapTick(pl);
			if (Raft_Network.IsHost && lights.Count > 0 && Time.time >= nextRepel) { nextRepel = Time.time + 0.5f; RepelTick(); }
		}

		static void ZapTick(Network_Player pl)
		{
			Vector3 at = pl.transform.position;
			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>())
			{
				if (ai == null || ai.networkEntity == null || ai.networkEntity.IsDead) continue;
				if ((ai.transform.position - at).sqrMagnitude > ZapRange * ZapRange) continue;
				CreatureSpawnPoint p = CreatureSpawner.SpotOf(ai);
				float zap = p != null ? ZapOf(p.Props) : 0f;
				if (zap <= 0f) continue;
				zapReadyAt = Time.time + ZapEvery;
				Zaps++;
				try { pl.Stats.Damage(zap, at + Vector3.up, (at - ai.transform.position).normalized, EntityType.Enemy, true, null); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] A zap: " + e.Message); }
				IslandInfo.ShowMessage("Zap! The " + (p.Kind != null ? p.Kind.Label.ToLowerInvariant() : "machine") + " shocks you.");
				Debug.Log("[CUSTOM ISLANDS] Zapped by a " + (p.Kind != null ? p.Kind.Label : "creature") + " for " + zap.ToString("0.#", CultureInfo.InvariantCulture));
				return;
			}
		}

		static FieldInfo jellyScared;

		static void RepelTick()
		{
			List<JellyFish> jellies = null;
			foreach (IslandObjectRef light in lights)
			{
				if (light == null || !light.gameObject.activeInHierarchy) continue;
				float r = RepelOf(light.Props);
				Vector3 c = light.transform.position;
				foreach (KeyValuePair<IslandWorldState.Entry, CreatureSpawnPoint> s in spots)
				{
					if (s.Value == null) continue;
					foreach (AI_NetworkBehaviour ai in s.Value.Spawned.Concat(s.Value.Summoned).ToList())
					{
						if (ai == null || ai.networkEntity == null || ai.networkEntity.IsDead) continue;
						Vector3 d = ai.transform.position - c;
						d.y = 0f;
						if (d.sqrMagnitude >= r * r) continue;
						if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
						Vector3 to = c + d.normalized * (r + 0.5f);
						to.y = ai.transform.position.y;
						ai.transform.position = to;
						Pushes++;
					}
				}
				// (Raft's jellyfish swim off when scared, as from Raft's own lights)
				if (jellies == null) jellies = UnityEngine.Object.FindObjectsOfType<JellyFish>().ToList();
				foreach (JellyFish j in jellies)
				{
					if (j == null || (j.transform.position - c).sqrMagnitude > r * r) continue;
					try
					{
						if (jellyScared == null) jellyScared = typeof(JellyFish).GetField("scared", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (jellyScared != null) jellyScared.SetValue(j, true);
						Pushes++;
					}
					catch { }
				}
			}
		}

		static Network_Host host;

		/// <summary>Host, the "hit" action: the named creature spots' animals lose a share of their health (arg: per cent,
		/// default 25) - Raft's own damage, so a kill counts and a boss phase starts as from a weapon.</summary>
		internal static int HitCreatures(IslandWorldState.Entry e, IEnumerable<IslandObjectRef> targets, string arg)
		{
			float pct;
			if (!float.TryParse((arg ?? "").Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out pct) || pct <= 0f) pct = 25f;
			pct = Mathf.Clamp(pct, 1f, 100f);
			int hit = 0;
			foreach (IslandObjectRef r in targets)
			{
				CreatureSpawnPoint p = r != null ? r.GetComponent<CreatureSpawnPoint>() : null;
				if (p == null) continue;
				foreach (AI_NetworkBehaviour ai in p.Spawned.Concat(p.Summoned).ToList())
				{
					if (ai == null || ai.networkEntity == null || ai.networkEntity.IsDead || ai.networkEntity.stat_health == null) continue;
					float dmg = ai.networkEntity.stat_health.Max * pct / 100f;
					EnvHit = true;
					try
					{
						if (host == null) host = UnityEngine.Object.FindObjectOfType<Network_Host>();
						if (host != null) host.DamageEntity(ai.networkEntity, ai.transform, dmg, ai.transform.position + Vector3.up, Vector3.up, EntityType.Environment, null);
						else ai.networkEntity.Damage(dmg, ai.transform.position + Vector3.up, Vector3.up, EntityType.Environment, true, null);
						hit++;
						Hits++;
					}
					catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] An environment hit: " + ex.Message); }
					finally { EnvHit = false; }
				}
				BossPhases.Check(e, p);
			}
			Debug.Log("[CUSTOM ISLANDS] Environment hit on '" + (e != null ? e.HostName : "?") + "': " + hit + " animal(s) lost " + pct.ToString("0", CultureInfo.InvariantCulture) + "% of their health");
			return hit;
		}

		/// <summary>Host: a player's hit on a boss whose spot has boss.shield, in its phase: it glances off.</summary>
		internal static bool Shielded(Network_Entity entity)
		{
			AI_NetworkBehaviour ai = entity.GetComponentInParent<AI_NetworkBehaviour>();
			if (ai == null) ai = entity.GetComponentInChildren<AI_NetworkBehaviour>();
			if (ai == null || !CreatureSpawner.IsOurs(ai)) return false;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) continue;
				foreach (CreatureSpawnPoint p in e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true))
					if (p != null && p.Spawned.Contains(ai)) return ObjectProps.GetBool(p.Props, Shield, false) && BossPhases.InPhase(e, p);
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(Network_Host), "DamageEntity")]
	static class BossShieldPatch
	{
		static float toldAt = -10f;

		static bool Prefix(Network_Entity entity, EntityType damageInflictorEntityType)
		{
			try
			{
				if (GapFeatures.EnvHit || entity == null || damageInflictorEntityType != EntityType.Player || !Raft_Network.IsHost) return true;
				if (!GapFeatures.Shielded(entity)) return true;
				if (Time.time - toldAt > 8f) { toldAt = Time.time; IslandInfo.ShowMessage("The blow glances off. Something bigger is needed!"); }
				return false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Boss shield: " + e.Message); return true; }
		}
	}
}

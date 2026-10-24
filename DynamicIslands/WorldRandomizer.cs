using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>What the world randomizer does in a world: how much (off, light, normal, wild), which parts, and the world's seed.</summary>
	public class RandomizerSettings
	{
		public const int Off = 0, Light = 1, Normal = 2, Wild = 3;
		public static readonly string[] LevelNames = { "Off", "Light", "Normal", "Wild" };

		public const string Colours = "colours", Animals = "animals", Alphas = "alphas", Loot = "loot", Finds = "finds", Oddities = "oddities", Bosses = "bosses", Large = "large";
		public static readonly string[] Features = { Colours, Animals, Alphas, Loot, Finds, Oddities, Bosses, Large };
		public static readonly string[] FeatureLabels = { "Colours", "Animals", "Alphas", "Loot", "Finds", "Oddities", "Bosses", "Large" };
		/// <summary>
		/// What each part does exactly - what changes, how often (Light / Normal / Wild), where, and what stays as Raft has it.
		/// Shown when the part's button is hovered (New Game box, Esc > Custom Islands; the user, 2026-10-04). The numbers are
		/// the code's own (Pick(light, normal, wild) below and in RandomizerContent).
		/// </summary>
		public static readonly string[] FeatureHints =
		{
			"COLOURS: animals and sharks (Bruce too) get another colour - charcoal, ash, rust, moss, frost, night, snow, cream, rarely a gold shark. " +
				"How often: about 15% / 30% / 55% of them (Light / Normal / Wild). Where: everywhere - Raft's islands, the sea, custom islands. " +
				"Stays as in Raft: how they behave, their health and damage.",
			"ANIMALS: more animals on Raft's islands. An island that has animals gets more of its own kinds near where Raft puts them " +
				"(50% / 80% / every island: 1 / 2 / 3 spots); an island without any gets warthogs, chickens, goats or llamas (15% / 30% / 50%; bears on Wild); " +
				"a puffer fish guarding the reef (10% / 25% / 40%). Where: Raft's islands as you meet them. Stays: Raft's own animals and spawners.",
			"ALPHAS: rare bigger, darker alpha warthogs, bears, hyenas and screechers (3x health, harder hits) and a huge Big Bruce - " +
				"about 3% / 6% / 12% of fighters, 6% / 12% / 22% of sharks. A banner warns you; killed, they drop a trophy head, meat and leather. " +
				"Stays: every other animal; the raft takes Bruce's bites as in Raft.",
			"LOOT: some of the crates and giant clams on Raft's islands lie in other places (on 30% / 50% / 80% of islands), and now and then " +
				"extra crates on land and barrels under water (35% / 60% / 85% of islands: up to 1 / 2 / 3 on land, 1 / 2 / 3 under water). " +
				"Stays: what is in Raft's crates; its story items and blueprints are never moved.",
			"FINDS: a treasure hunt (a map in a bottle leads to a buried chest), an abandoned camp or a castaway's stash on 10% / 20% / 30% of Raft's islands " +
				"(more on big ones); on Raft's big islands a den under rocks with a guard and a hoard (25% / 45% / 70%) and an outpost with props of the quest islands " +
				"(20% / 35% / 50%). Stays: everything Raft put on the island keeps its place.",
			"ODDITIES: small odd islands while you sail - a van, a caravan, a crashed plane, a stranded boat, a hermit's shack, a statue, rocket debris, a hut of raft blocks, " +
				"each with loot, a note and a name banner. How often: after the first 1.5 km, about every 12 / 7 / 4 km. Stays: Raft's own islands and story.",
			"BOSSES: a boss lair - a plateau where a named beast (Old Ironhide, Frostfang, Ashmaw, the Tusk King, the Laughing One) wakes with two guards " +
				"and guards a big hoard. How often: after the first 4 km, about every 40 / 20 / 12 km. Stays: Raft's story bosses.",
			"LARGE: a large island as big as Raft's big ones, with a made-up name, warthogs and animals to catch, things to gather, hidden loot, scenes of the " +
				"quest islands, a den and up to two landmarks (a wreck, a lighthouse, a jetty...). How often: after the first 3 km, about every 20 / 10 / 7 km. " +
				"Stays: Raft's own big islands.",
		};

		/// <summary>The levels' hover text (New Game box and Esc > Custom Islands): how much each changes, in numbers.</summary>
		public static string LevelHint(int level)
		{
			return level <= Off ? "OFF: a normal Raft world - nothing is changed." :
				level == Light ? "LIGHT: now and then something is different - about 15% of animals in another colour, 3% alphas (6% of sharks), an oddity island about every 12 km, a large island every 20 km, a boss lair every 40 km." :
				level == Normal ? "NORMAL: a good share of the world is different - about 30% of animals in another colour, 6% alphas (12% of sharks), an oddity island about every 7 km, a large island every 10 km, a boss lair every 20 km; more loot and finds." :
				"WILD: lots of surprises - about 55% of animals in another colour, 12% alphas (22% of sharks), an oddity island about every 4 km, a large island every 7 km, a boss lair every 12 km; most islands get extra animals, loot and finds.";
		}

		public int Level;
		public int Seed;
		/// <summary>Parts switched off (all are on by default).</summary>
		public readonly HashSet<string> Disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public bool On { get { return Level > Off; } }
		public bool Has(string feature) { return Level > Off && !Disabled.Contains(feature); }

		/// <summary>The value for this level (light, normal, wild); 0 when off.</summary>
		public float Pick(float light, float normal, float wild) { return Level <= Off ? 0f : Level == Light ? light : Level == Normal ? normal : wild; }
		public int Pick(int light, int normal, int wild) { return Level <= Off ? 0 : Level == Light ? light : Level == Normal ? normal : wild; }

		public string LevelName { get { return LevelNames[Mathf.Clamp(Level, 0, LevelNames.Length - 1)]; } }

		public string Encode()
		{
			return "level=" + LevelName.ToLowerInvariant() + ";seed=" + Seed.ToString(CultureInfo.InvariantCulture) +
				(Disabled.Count > 0 ? ";off=" + string.Join(",", Features.Where(Disabled.Contains).ToArray()) : "");
		}

		public static RandomizerSettings Decode(string text)
		{
			var s = new RandomizerSettings();
			foreach (string part in (text ?? "").Split(';'))
			{
				int eq = part.IndexOf('=');
				if (eq <= 0) continue;
				string key = part.Substring(0, eq).Trim().ToLowerInvariant(), value = part.Substring(eq + 1).Trim();
				switch (key)
				{
					case "level":
						int l = Array.FindIndex(LevelNames, n => n.Equals(value, StringComparison.OrdinalIgnoreCase));
						if (l < 0 && !int.TryParse(value, out l)) l = Off;
						s.Level = Mathf.Clamp(l, Off, Wild);
						break;
					case "seed": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Seed); break;
					case "off":
						foreach (string f in value.Split(',')) if (Features.Contains(f.Trim(), StringComparer.OrdinalIgnoreCase)) s.Disabled.Add(f.Trim().ToLowerInvariant());
						break;
				}
			}
			return s;
		}

		public RandomizerSettings Copy() { return Decode(Encode()); }

		/// <summary>One line for players: "Normal: colours, animals, ..." or "Off".</summary>
		public string Describe()
		{
			if (!On) return "Off";
			string[] on = Features.Where(f => !Disabled.Contains(f)).Select(f => FeatureLabels[Array.IndexOf(Features, f)].ToLowerInvariant()).ToArray();
			return LevelName + ": " + (on.Length == 0 ? "nothing" : on.Length == Features.Length ? "everything" : string.Join(", ", on));
		}
	}

	/// <summary>
	/// The world randomizer: makes a normal Raft world (with or without custom islands) play out differently every time,
	/// without touching Raft's story. Chosen in the New Game box; everything follows from the world's seed.
	///   - Colours: animals and sharks sometimes get another colour. Worked out on every machine from the world's seed
	///     and the animal's network id, so every player sees the same colours without sending anything.
	///   - Alphas: rare bigger, darker, much tougher animals and a huge Bruce (the host sets their health and damage);
	///     killed, they drop a trophy head and more for the players.
	///   - Loot: some of Raft's crates and giant clams on its natural islands lie somewhere else (worked out on every
	///     machine from the seed and the island's network id; Raft still knows them by their old place, so picking
	///     them up works as before).
	///   - Animals, loot, finds on Raft's islands: the host puts them on a land-less custom island laid over Raft's island
	///     (its "extras", file rnd-&lt;seed&gt;-&lt;island id&gt;), so they are saved, sent to other players, respawn and
	///     regrow like any custom island's content. It is there only while Raft's island is.
	///   - Oddities and bosses: small islands with a set piece and boss lairs appear while sailing (map types).
	/// Raft's story islands (and the stranded boat, the pilot's island, floating rafts) are never changed.
	/// </summary>
	public static class WorldRandomizer
	{
		/// <summary>Files of the extras laid over Raft's islands: rnd-&lt;seed&gt;-&lt;island's network id&gt;.</summary>
		public const string ExtrasPrefix = "rnd-";
		public const string DefaultsFileName = "randomizer.txt";

		/// <summary>The name players read for an extras file: its find ("A castaway's stash"), else "Randomizer finds".</summary>
		public static string TitleFor(IEnumerable<string> parts)
		{
			string find = parts.FirstOrDefault(p => p == "a treasure hunt" || p == "an abandoned camp" || p == "a castaway's stash");
			return find != null ? char.ToUpperInvariant(find[0]) + find.Substring(1) : "Randomizer finds";
		}

		/// <summary>
		/// A readable name for an island name that may be an extras file name (rnd-...): its title, else "Randomizer finds".
		/// Pages and lists saved before extras had titles still hold the file name.
		/// </summary>
		public static string Readable(string name)
		{
			if (string.IsNullOrEmpty(name) || !name.StartsWith(ExtrasPrefix, StringComparison.OrdinalIgnoreCase)) return name ?? "";
			string t = "";
			try { t = ObjectProps.Get(IslandCache.Props(name), IslandProps.Title); } catch { }
			return t.Length > 0 && !t.StartsWith(ExtrasPrefix, StringComparison.OrdinalIgnoreCase) ? t : "Randomizer finds";
		}

		/// <summary>The current world's settings (clients get the host's).</summary>
		public static RandomizerSettings Current = new RandomizerSettings();
		/// <summary>Chosen in the New Game box for the world being created (null = the last choice, Defaults).</summary>
		public static RandomizerSettings Pending;

		/// <summary>Host: where Raft's islands were already looked at for extras (they get them once), in Raft's coordinates.</summary>
		static readonly List<Vector3> seen = new List<Vector3>();
		/// <summary>Every machine: the spawn (network id) each of Raft's islands was last handled for.</summary>
		static readonly Dictionary<Landmark, uint> handled = new Dictionary<Landmark, uint>();
		/// <summary>Raft's islands waiting a moment after they appeared (their colliders settle), with when they were first seen.</summary>
		static readonly Dictionary<Landmark, float> waiting = new Dictionary<Landmark, float>();
		static readonly Dictionary<Landmark, bool> natural = new Dictionary<Landmark, bool>();
		/// <summary>Where Raft put the crates and clams, relative to their island, before any were moved.</summary>
		static readonly Dictionary<LandmarkItem, KeyValuePair<Vector3, Quaternion>> original = new Dictionary<LandmarkItem, KeyValuePair<Vector3, Quaternion>>();
		/// <summary>Animals already looked at (instance id -> network id: Raft may reuse an object).</summary>
		static readonly Dictionary<int, uint> animalsSeen = new Dictionary<int, uint>();
		/// <summary>A player: when each animal not yet known to be an island's was first seen (HandleAnimals waits for its spot).</summary>
		static readonly Dictionary<int, float> animalFirstSeen = new Dictionary<int, float>();
		const float SpotsGraceSeconds = 3f;
		/// <summary>Host: alpha animals still alive, and what they are.</summary>
		static readonly Dictionary<AI_NetworkBehaviour, string> alphas = new Dictionary<AI_NetworkBehaviour, string>();
		static readonly HashSet<string> typesLogged = new HashSet<string>();
		static float nextTick, sailedOddity, sailedBoss;

		/// <summary>What happened last (tests read it).</summary>
		public static int ColouredCount, AlphaCount, MovedCount, ExtrasCount;
		public static readonly Dictionary<uint, string> VariantOfIndex = new Dictionary<uint, string>();

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [randomizer] " + msg); }

		#region Settings and the world file

		static string DefaultsPath { get { return Path.Combine(DynamicIslands.assetpath, DefaultsFileName); } }

		/// <summary>The last settings chosen in the New Game box (off until one was).</summary>
		public static RandomizerSettings Defaults
		{
			get
			{
				try { if (File.Exists(DefaultsPath)) return RandomizerSettings.Decode(File.ReadAllLines(DefaultsPath).FirstOrDefault(l => !l.StartsWith("#")) ?? ""); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + DefaultsPath + ": " + e.Message); }
				return new RandomizerSettings();
			}
		}

		public static void SaveDefaults(RandomizerSettings s)
		{
			try
			{
				RandomizerSettings d = s.Copy();
				d.Seed = 0;
				SafeFile.WriteAllText(DefaultsPath, "# World randomizer: the last choice in the New Game box (level=off|light|normal|wild; off=parts switched off)\n" + d.Encode() + "\n");
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not write " + DefaultsPath + ": " + e.Message); }
		}

		/// <summary>Before a world's island list is read.</summary>
		internal static void Reset()
		{
			Current = new RandomizerSettings();
			seen.Clear();
			handled.Clear();
			waiting.Clear();
			natural.Clear();
			original.Clear();
			animalsSeen.Clear();
			alphas.Clear();
			VariantOfIndex.Clear();
			sailedOddity = sailedBoss = 0f;
			oddityDue = bossDue = largeDue = false;
			sailedLarge = 0f;
			broughtOddity = broughtBoss = broughtLarge = 0;
			triesOddity = triesBoss = triesLarge = 0;
			groundOf.Clear();
			positionKeys.Clear();
			ColouredCount = AlphaCount = MovedCount = ExtrasCount = 0;
		}

		/// <summary>A "@key=value" line of the world file; false if it isn't ours.</summary>
		internal static bool ReadLine(string key, string value)
		{
			switch (key)
			{
				case "randomizer": Current = RandomizerSettings.Decode(value); return true;
				case "rndseen":
					foreach (string p in value.Split(';'))
					{
						string[] xz = p.Split(',');
						float x, z;
						if (xz.Length == 2 && float.TryParse(xz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(xz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
							seen.Add(new Vector3(x, 0f, z));
					}
					return true;
				case "rndsailed":
					string[] v = value.Split(',');
					if (v.Length >= 2) { sailedOddity = Metres(v[0]); sailedBoss = Metres(v[1]); }
					if (v.Length >= 3) sailedLarge = Metres(v[2]);
					return true;
				case "rndcount":
					string[] c = value.Split(',');
					if (c.Length >= 3) { int.TryParse(c[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out broughtOddity); int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out broughtBoss); int.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out broughtLarge); }
					return true;
			}
			return false;
		}

		/// <summary>A distance sailed of the world file: NaN, infinite or below 0 (an edited or broken file) is 0 - NaN never
		/// grew past the threshold, and that kind of island never came again.</summary>
		static float Metres(string text)
		{
			float m;
			return CustomIslandSpawner.ParseNumber(text, out m) && !float.IsNaN(m) && !float.IsInfinity(m) && m > 0f ? m : 0f;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (!Current.On && seen.Count == 0) yield break;
			yield return "@randomizer=" + Current.Encode();
			yield return "@rndsailed=" + sailedOddity.ToString("F0", CultureInfo.InvariantCulture) + "," + sailedBoss.ToString("F0", CultureInfo.InvariantCulture) + "," + sailedLarge.ToString("F0", CultureInfo.InvariantCulture);
			if (broughtOddity + broughtBoss + broughtLarge > 0)
				yield return "@rndcount=" + broughtOddity.ToString(CultureInfo.InvariantCulture) + "," + broughtBoss.ToString(CultureInfo.InvariantCulture) + "," + broughtLarge.ToString(CultureInfo.InvariantCulture);
			if (seen.Count > 0)
				yield return "@rndseen=" + string.Join(";", seen.Select(p => p.x.ToString("F1", CultureInfo.InvariantCulture) + "," + p.z.ToString("F1", CultureInfo.InvariantCulture)).ToArray());
		}

		/// <summary>True when the world file is needed for the randomizer alone.</summary>
		internal static bool HasState { get { return Current.On || seen.Count > 0; } }

		/// <summary>Host: a brand-new world gets the settings chosen in the New Game box, and its own seed.</summary>
		internal static void OnNewWorld()
		{
			Current = (Pending ?? Defaults).Copy();
			Pending = null;
			Current.Seed = new System.Random().Next(1, int.MaxValue);
			Log("New world: " + Current.Describe() + " (seed " + Current.Seed + ")");
			Broadcast();
		}

		/// <summary>Host: changes the current world's settings (the Randomizer command, tests). Keeps the seed.</summary>
		public static void Set(RandomizerSettings s)
		{
			int seed = Current.Seed != 0 ? Current.Seed : new System.Random().Next(1, int.MaxValue);
			Current = s.Copy();
			if (Current.Seed == 0) Current.Seed = seed;
			if (!Current.Has(RandomizerSettings.Loot)) RestoreLoot();
			// (an island that was due waits no more when its part is switched off: it would come the moment it's on again)
			if (!Current.Has(RandomizerSettings.Oddities)) oddityDue = false;
			if (!Current.Has(RandomizerSettings.Bosses)) bossDue = false;
			if (!Current.Has(RandomizerSettings.Large)) largeDue = false;
			handled.Clear();
			waiting.Clear();
			animalsSeen.Clear();
			Broadcast();
			// (saved with the world at once, like the other settings: the copy players keep for host swap follows)
			IslandWorldState.Save();
		}

		/// <summary>Tests: every island of Raft's is handled again (loot moved again from where Raft put it).</summary>
		internal static void Rehandle() { handled.Clear(); waiting.Clear(); }

		internal static int SeenCount { get { return seen.Count; } }

		/// <summary>Tests: this island of Raft's gets its extras again, now (another roll: salt).</summary>
		internal static IEnumerator ForceExtras(Landmark l, int salt)
		{
			Vector3 at = new Vector3(l.transform.position.x, 0f, l.transform.position.z);
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(e => IsExtras(e) && Flat(e.Position - at).sqrMagnitude < 100f).Select(e => e.Id).ToList(), true);
			seen.RemoveAll(p => (p - at).sqrMagnitude < 100f);
			seen.Add(at);
			yield return MakeExtras(l, SpawnKey(l) ^ (uint)salt, at);
		}

		/// <summary>Whether any of this island's crates or clams were looked at by the randomizer (and may have been moved).</summary>
		internal static bool MovedAny(Landmark l)
		{
			if (original.Count == 0 || l.landmarkItems == null) return false;
			foreach (LandmarkItem i in l.landmarkItems) if (i != null && original.ContainsKey(i)) return true;
			return false;
		}

		/// <summary>Tests: forget which of Raft's islands were looked at (their extras come again).</summary>
		internal static void ForgetSeen() { seen.Clear(); }

		/// <summary>Where Raft put a crate or clam that was moved (world position now), or null if it wasn't looked at.</summary>
		internal static Vector3? OriginalOf(Landmark l, LandmarkItem i)
		{
			KeyValuePair<Vector3, Quaternion> o;
			return original.TryGetValue(i, out o) ? l.transform.TransformPoint(o.Key) : (Vector3?)null;
		}

		/// <summary>Puts the crates and clams back where Raft put them (loot switched off).</summary>
		static void RestoreLoot()
		{
			foreach (Landmark l in WorldManager.AllLandmarks)
			{
				if (l == null || l.landmarkItems == null) continue;
				foreach (LandmarkItem i in l.landmarkItems)
				{
					KeyValuePair<Vector3, Quaternion> o;
					if (i == null || !original.TryGetValue(i, out o)) continue;
					i.transform.position = l.transform.TransformPoint(o.Key);
					i.transform.rotation = l.transform.rotation * o.Value;
				}
			}
		}

		public static void OnWorldShift(Vector3 shift)
		{
			for (int i = 0; i < seen.Count; i++) seen[i] -= new Vector3(shift.x, 0f, shift.z);
			foreach (Landmark l in positionKeys.Keys.ToList())
			{
				if (l == null) { positionKeys.Remove(l); continue; }
				positionKeys[l] = new KeyValuePair<uint, Vector3>(positionKeys[l].Key, positionKeys[l].Value - shift);
			}
		}

		#endregion

		#region Network

		/// <summary>Host -> clients: the world's randomizer settings (they work out colours and moved crates themselves).</summary>
		internal static IslandNetMessage Message() { return new IslandNetMessage { Kind = IslandNetMessage.Randomizer, Data = Current.Encode() }; }

		static void Broadcast()
		{
			if (Raft_Network.IsHost) IslandNetwork.SendToEveryone(Message());
		}

		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			Current = RandomizerSettings.Decode(msg.Data);
			if (!Current.Has(RandomizerSettings.Loot)) RestoreLoot();
			// (an island that was due waits no more when its part is switched off: it would come the moment it's on again)
			if (!Current.Has(RandomizerSettings.Oddities)) oddityDue = false;
			if (!Current.Has(RandomizerSettings.Bosses)) bossDue = false;
			if (!Current.Has(RandomizerSettings.Large)) largeDue = false;
			handled.Clear();
			waiting.Clear();
			animalsSeen.Clear();
			Log("The host's world randomizer: " + Current.Describe());
		}

		#endregion

		#region Every frame

		public static void Tick()
		{
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + 0.5f;
			if (!LoadSceneManager.IsGameSceneLoaded) return;
			// (alphas it made keep their spoils when the randomizer or its alphas are switched off: they stay as they are - big,
			// dark and tough, like everything it already changed ("islands already looked at keep what they got") - so they
			// are worth the fight to the end. They were watched only while it was on: a tough alpha died for nothing - AU41.
			// From the next load nothing rolls new ones.)
			if (Raft_Network.IsHost && alphas.Count > 0) try { WatchAlphas(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Alphas: " + e); }
			if (!Current.On) return;
			try { HandleIslands(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Raft's islands: " + e); }
			try { HandleAnimals(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Animals: " + e); }
		}

		/// <summary>Host: the raft sailed this far; now and then an oddity island or a boss lair appears ahead.</summary>
		internal static void OnSailed(float metres, Vector3 raftPos)
		{
			if (!Raft_Network.IsHost || !Current.On) return;
			if (Current.Has(RandomizerSettings.Oddities))
			{
				sailedOddity += metres;
				// Not within the first stretch of a world (the start is Raft's own), then a chance per km
				if (!oddityDue && sailedOddity > 1500f + DueAfter(OdditySalt, broughtOddity, Current.Pick(0.08f, 0.15f, 0.25f))) { oddityDue = true; sinceOddityTry = RetryMetres; triesOddity = 0; }
				if (oddityDue && Bring("oddity", OdditySalt, broughtOddity, ref triesOddity, ref sinceOddityTry, metres, raftPos)) { oddityDue = false; sailedOddity = 1500f - 400f; broughtOddity++; } // (not two right after each other)
			}
			if (Current.Has(RandomizerSettings.Bosses))
			{
				sailedBoss += metres;
				if (!bossDue && sailedBoss > 4000f + DueAfter(BossSalt, broughtBoss, Current.Pick(0.025f, 0.05f, 0.08f))) { bossDue = true; sinceBossTry = RetryMetres; triesBoss = 0; }
				if (bossDue && Bring("lair", BossSalt, broughtBoss, ref triesBoss, ref sinceBossTry, metres, raftPos)) { bossDue = false; sailedBoss = 4000f - 2500f; broughtBoss++; }
			}
			if (Current.Has(RandomizerSettings.Large))
			{
				sailedLarge += metres;
				if (!largeDue && sailedLarge > 3000f + DueAfter(LargeSalt, broughtLarge, Current.Pick(0.05f, 0.1f, 0.15f))) { largeDue = true; sinceLargeTry = RetryMetres; triesLarge = 0; }
				if (largeDue && Bring("large", LargeSalt, broughtLarge, ref triesLarge, ref sinceLargeTry, metres, raftPos)) { largeDue = false; sailedLarge = 3000f - 2000f; broughtLarge++; }
			}
		}

		static float sailedLarge, sinceLargeTry;
		static bool largeDue;

		/// <summary>Once one is due, tried every so many metres sailed until there is room ahead (Raft's sea is crowded).</summary>
		const float RetryMetres = 200f;
		static bool oddityDue, bossDue;
		internal static bool OddityDue { get { return oddityDue; } }
		static float sinceOddityTry, sinceBossTry;

		// How many of each of its own islands this world has had (saved with the world): with the world seed they decide
		// when the next one is due, what it is and where it goes - CA18. Tries: spots tried for the one due now.
		static int broughtOddity, broughtBoss, broughtLarge, triesOddity, triesBoss, triesLarge;
		const int OdditySalt = 41, BossSalt = 43, LargeSalt = 47;

		/// <summary>The randomizer's own islands (map types it brings while sailing).</summary>
		public static readonly string[] OwnTypes = { "oddity", "lair", "large" };

		/// <summary>
		/// Things to gather on the randomizer's own islands (ROADMAP LM9): the generator's Things to gather and Finds in the
		/// shallows set by its level - light a little, normal some, wild much - where the type's settings have none. Other
		/// map types, and worlds without the randomizer, are left as they are.
		/// </summary>
		public static void GatherFor(IslandGenSettings s, string type, RandomizerSettings r)
		{
			if (s == null || r == null || !r.On || !OwnTypes.Contains(type ?? "", StringComparer.OrdinalIgnoreCase)) return;
			if (s.Gather <= 0f) s.Gather = r.Pick(0.3f, 0.55f, 0.85f);
			if (s.Shallows <= 0f) s.Shallows = r.Pick(0.3f, 0.5f, 0.8f);
		}

		static bool Bring(string type, int salt, int count, ref int tries, ref float sinceTry, float metres, Vector3 raftPos)
		{
			sinceTry += metres;
			if (sinceTry < RetryMetres) return false;
			sinceTry = 0f;
			string r = CustomIslandSpawner.TrySpawn(raftPos, false, CustomIslandSpawner.TypePrefix + type, Hash(Current.Seed, salt, count), tries++);
			Log((type == "lair" ? "Boss lair: " : type == "large" ? "Large island: " : "Oddity island: ") + r);
			return r.StartsWith("Spawning");
		}

		/// <summary>
		/// Metres sailed past the first stretch before the count-th island of this kind is due, from the world seed: the same
		/// chance per km as rolling it every stretch sailed ((1 - chance)^km of none yet), but the same for every sail of
		/// this world. Infinite when the chance is 0.
		/// </summary>
		static float DueAfter(int salt, int count, float chancePerKm)
		{
			if (chancePerKm <= 0f) return float.PositiveInfinity;
			float u = Unit(Hash(Current.Seed, salt, count, 1));
			return 1000f * Mathf.Log(1f - u) / Mathf.Log(1f - Mathf.Min(chancePerKm, 0.99f));
		}

		#endregion

		#region Raft's islands

		/// <summary>Whether this island of the world's list is the randomizer's extras on one of Raft's islands.</summary>
		public static bool IsExtras(IslandWorldState.Entry e)
		{
			return e != null && (e.HostName ?? e.Name ?? "").StartsWith(ExtrasPrefix, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Whether Raft's island the extras belong to is there now (its extras are laid over it only then).</summary>
		public static bool HasIslandUnder(IslandWorldState.Entry e)
		{
			Landmark l = IslandAt(e.Position);
			if (l == null || !GroundOn(l)) return false;
			// (laid exactly over Raft's island: they were made around its middle. A player who joined places the host's
			// islands beside its own raft, which lags the host's while it moves - after a fast sail the extras came several
			// metres off and never found their island there)
			if (e.Root == null) e.Position = new Vector3(l.transform.position.x, e.Position.y, l.transform.position.z);
			return true;
		}

		/// <summary>The spawned natural island of Raft's whose middle is here, or null.</summary>
		public static Landmark IslandAt(Vector3 position)
		{
			foreach (Landmark l in WorldManager.AllLandmarks)
			{
				if (l == null || !l.isSpawned) continue;
				Vector3 d = l.transform.position - position;
				if (d.x * d.x + d.z * d.z < 15f * 15f) return l; // (Raft's islands are hundreds of metres apart)
			}
			return null;
		}

		/// <summary>"Landmark_Big" from "32#Landmark_Big#": the kind of one of Raft's islands.</summary>
		public static string KindOf(Landmark l)
		{
			string[] p = (l != null ? l.name : "").Split('#');
			return p.Length > 1 ? p[1] : l != null ? l.name : "";
		}

		/// <summary>This spawn of one of Raft's islands: its network id, the same on every machine and kept in saves.</summary>
		public static uint SpawnKey(Landmark l)
		{
			PickupItem_Networked net = l.GetComponent<PickupItem_Networked>();
			if (net != null && net.ObjectIndex != 0) return net.ObjectIndex;
			// (none: by where it is - kept through world shifts, which move it but don't make it another spawn; a pooled
			// island moved somewhere else is one)
			Vector3 p = l.transform.position;
			KeyValuePair<uint, Vector3> known;
			if (positionKeys.TryGetValue(l, out known) && Flat(known.Value - p).sqrMagnitude < 1f) return known.Key;
			uint key = (uint)Hash((int)l.uniqueLandmarkIndex, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
			positionKeys[l] = new KeyValuePair<uint, Vector3>(key, p);
			return key;
		}

		/// <summary>Raft's islands without a network id: the key each got from where it was, and where it is now.</summary>
		static readonly Dictionary<Landmark, KeyValuePair<uint, Vector3>> positionKeys = new Dictionary<Landmark, KeyValuePair<uint, Vector3>>();

		static readonly Type[] StoryParts = { typeof(QuestItemPickup), typeof(NoteBookNotePickup), typeof(LandmarkItem_Quest), typeof(LandmarkItem_Quest_OneWay),
			typeof(LandmarkItem_CharacterUnlock), typeof(LandmarkItem_Keypad), typeof(LandmarkEntitySpawner_UniqueQuest), typeof(LandmarkEntitySpawner_Repeating_QuestRequirement) };

		/// <summary>
		/// One of Raft's plain islands (the big and small tropical ones and the small islands around the story places):
		/// nothing of a quest on it. Story islands, the stranded boat, the pilot's island and floating rafts are left alone.
		/// </summary>
		public static bool IsNatural(Landmark l)
		{
			bool ok;
			if (natural.TryGetValue(l, out ok)) return ok;
			string kind = KindOf(l);
			ok = kind.Equals("Landmark_Big", StringComparison.OrdinalIgnoreCase) || kind.IndexOf("Small", StringComparison.OrdinalIgnoreCase) >= 0;
			string story = null;
			if (ok)
				foreach (Type t in StoryParts)
					if (l.GetComponentInChildren(t, true) != null) { ok = false; story = t.Name; break; }
			natural[l] = ok;
			if (typesLogged.Add(kind + ok)) Log("Raft's island kind '" + kind + "' (" + l.name + "): " + (ok ? "randomized" : "left alone" + (story != null ? " (" + story + ")" : "")));
			return ok;
		}

		static void HandleIslands()
		{
			float now = Time.unscaledTime;
			foreach (Landmark l in WorldManager.AllLandmarks)
			{
				if (l == null || !l.isSpawned) continue;
				uint key = SpawnKey(l);
				uint was;
				if (handled.TryGetValue(l, out was) && was == key) continue;
				// Raft switches the ground of its far islands off (about a kilometre away): wait until it is there
				if (!GroundOn(l)) { waiting.Remove(l); continue; }
				// (a pooled island was just moved here: give physics a moment to follow)
				float since;
				if (!waiting.TryGetValue(l, out since)) { waiting[l] = now; continue; }
				if (now - since < 1f) continue;
				// (a player whose copy of the island's extras is still on its way waits for it before moving crates - they keep
				// out of its dens and outposts, as on the host - for at most half a minute)
				if (Current.Has(RandomizerSettings.Loot) && now - since < 30f && IsNatural(l) && ExtrasOf(l) != null && ExtrasOf(l).WaitingForFile) continue;
				waiting.Remove(l);
				handled[l] = key;
				if (!IsNatural(l)) continue;
				Physics.SyncTransforms();
				if (Current.Has(RandomizerSettings.Loot)) ShuffleLoot(l, key);
				if (Raft_Network.IsHost && IslandNetwork.HasList) GiveExtras(l, key);
			}
		}

		static readonly Dictionary<Landmark, Collider> groundOf = new Dictionary<Landmark, Collider>();

		/// <summary>Whether the island's ground is switched on (Raft turns it off far from the raft; rays find nothing then).</summary>
		public static bool GroundOn(Landmark l)
		{
			Collider c;
			if (!groundOf.TryGetValue(l, out c) || c == null)
			{
				c = (Collider)l.GetComponentInChildren<TerrainCollider>(true) ?? l.GetComponentsInChildren<Collider>(true).FirstOrDefault(x => !x.isTrigger && x.GetComponentInParent<LandmarkItem>() == null);
				groundOf[l] = c;
			}
			return c != null && c.enabled && c.gameObject.activeInHierarchy;
		}

		static readonly System.Text.RegularExpressions.Regex RaftLoot = new System.Text.RegularExpressions.Regex(@"^Pickup_Landmark_(LandmarkCrate\w*|GiantClam)$");

		public static bool IsRaftLoot(LandmarkItem i) { return i != null && RaftLoot.IsMatch(PlaceableCatalog.CleanName(i.name)); }

		/// <summary>
		/// Every machine: some of the island's crates and giant clams go to another spot of the same kind (on land, or under
		/// water at a similar depth). Always the same spots for this island's spawn: the seed and its network id decide.
		/// </summary>
		static void ShuffleLoot(Landmark l, uint key)
		{
			if (l.landmarkItems == null) return;
			List<LandmarkItem> loot = l.landmarkItems.Where(IsRaftLoot).ToList();
			if (loot.Count == 0) return;
			// Back where Raft put them first (this island may have been moved here from somewhere else)
			foreach (LandmarkItem i in loot)
			{
				KeyValuePair<Vector3, Quaternion> o;
				if (!original.TryGetValue(i, out o)) original[i] = o = new KeyValuePair<Vector3, Quaternion>(l.transform.InverseTransformPoint(i.transform.position), Quaternion.Inverse(l.transform.rotation) * i.transform.rotation);
				i.transform.position = l.transform.TransformPoint(o.Key);
				i.transform.rotation = l.transform.rotation * o.Value;
			}
			Physics.SyncTransforms();
			var ground = new LandGround(l);
			var rnd = new System.Random(Hash(Current.Seed, (int)key, 7));
			float chance = Current.Pick(0.3f, 0.5f, 0.8f);
			// (everything of Raft's on the island keeps its room, where Raft put it)
			List<Vector3> taken = l.landmarkItems.Where(i => i != null && !loot.Contains(i)).Select(i => i.transform.position).ToList();
			taken.AddRange(loot.Select(i => i.transform.position));
			// (not into the randomizer's dens and outposts on the island: made while Loot was off, they kept clear of where Raft
			// put the crates, not of where they go - CA23)
			Func<Vector3, bool> inExtras = ExtrasPieces(l);
			int moved = 0;
			foreach (LandmarkItem i in loot)
			{
				bool move = rnd.NextDouble() < chance;
				int seed = rnd.Next();
				if (!move) continue;
				Vector3 from = i.transform.position;
				bool under = from.y < -0.5f;
				Vector3 hit, normal;
				float lift = ground.Hit(from.x, from.z, out hit, out normal) ? Mathf.Clamp(from.y - hit.y, -0.6f, 0.6f) : 0f;
				var r = new System.Random(seed);
				Vector3? to = ground.Find(r, (h, slope) => (under ? h > -16f && h < -2.5f : h > 1.2f && h < 45f) && slope < 32f, taken, 3f, 80);
				if (!to.HasValue || (inExtras != null && inExtras(to.Value))) continue;
				ground.Hit(to.Value.x, to.Value.z, out hit, out normal);
				i.transform.position = to.Value + Vector3.up * lift;
				i.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.5f)) * Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f);
				taken.Add(to.Value);
				moved++;
			}
			MovedCount += moved;
			if (moved > 0) Log("'" + l.name + "': " + moved + " of " + loot.Count + " crates and clams lie somewhere else");
		}

		/// <summary>The randomizer's extras on this island of Raft's in the world's list, or null.</summary>
		static IslandWorldState.Entry ExtrasOf(Landmark l)
		{
			Vector3 at = new Vector3(l.transform.position.x, 0f, l.transform.position.z);
			return IslandWorldState.Islands.FirstOrDefault(e => IsExtras(e) && Flat(e.Position - at).sqrMagnitude < 100f);
		}

		/// <summary>
		/// Whether a world spot is under one of the dens or outposts of this island's extras (a den by the ellipse its outcrop's
		/// footprint holds and a metre around - Grotto; an outpost's props by the room DressWorld keeps around them), from the
		/// extras' file; null when there are none, or no file here yet.
		/// </summary>
		static Func<Vector3, bool> ExtrasPieces(Landmark l)
		{
			IslandWorldState.Entry e = ExtrasOf(l);
			if (e == null || e.WaitingForFile) return null;
			IslandFile file;
			try { string path = IslandSpawner.PathFor(e.Name); if (!File.Exists(path)) return null; file = IslandFile.Load(path); }
			catch (Exception x) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Reading the extras of '" + l.name + "': " + x.Message); return null; }
			Vector2 mid = new Vector2(file.TerrainSize.x / 2f, file.TerrainSize.z / 2f);
			string centre;
			if (file.Props != null && file.Props.TryGetValue(IslandProps.Centre, out centre))
			{
				string[] xz = centre.Split(',');
				float cx, cz;
				if (xz.Length == 2 && float.TryParse(xz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out cx) && float.TryParse(xz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out cz)) mid = new Vector2(cx, cz);
			}
			Vector3 island = l.transform.position;
			var tests = new List<Func<Vector3, bool>>();
			foreach (IslandObject o in file.Objects)
			{
				if (o == null || o.Props == null || !o.Props.ContainsKey("set.piece")) continue;
				float wx = o.Position.x - mid.x + island.x, wz = o.Position.z - mid.y + island.z;
				PropInfo pi = RaftProps.Get(o.Name);
				if (o.Props.ContainsKey("cave") && pi != null)
				{
					Quaternion toDen = Quaternion.Inverse(Quaternion.Euler(0f, o.EulerRotation.y, 0f));
					float ax = pi.Size.x * 0.5f + 1f, az = pi.Size.z * 0.5f + 1f;
					Vector3 centreOf = new Vector3(pi.Centre.x, 0f, pi.Centre.z);
					tests.Add(p => { Vector3 d = toDen * new Vector3(p.x - wx, 0f, p.z - wz) - centreOf; return d.x * d.x / (ax * ax) + d.z * d.z / (az * az) < 1f; });
				}
				else
				{
					float clear = pi != null ? Mathf.Max(1.5f, Mathf.Max(pi.Size.x, pi.Size.z) * 0.4f + 0.5f) : 1.5f;
					tests.Add(p => (p.x - wx) * (p.x - wx) + (p.z - wz) * (p.z - wz) < clear * clear);
				}
			}
			if (tests.Count == 0) return null;
			return p => tests.Any(t => t(p));
		}

		/// <summary>Host: an island of Raft's appears for the first time in this world: maybe it gets extras (once).</summary>
		static void GiveExtras(Landmark l, uint key)
		{
			Vector3 at = new Vector3(l.transform.position.x, 0f, l.transform.position.z);
			if (seen.Any(p => (p - at).sqrMagnitude < 100f)) return;
			if (IslandWorldState.Islands.Any(e => IsExtras(e) && (Flat(e.Position - at)).sqrMagnitude < 100f)) return;
			seen.Add(at);
			if (!Current.Has(RandomizerSettings.Animals) && !Current.Has(RandomizerSettings.Loot) && !Current.Has(RandomizerSettings.Finds)) return;
			DynamicIslands.instance.StartCoroutine(MakeExtras(l, key, at));
		}

		static IEnumerator MakeExtras(Landmark l, uint key, Vector3 at)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			if (l == null || !l.isSpawned || !GroundOn(l) || !Raft_Network.IsHost) yield break;
			// (where the island is now: a world shift while the catalog was made moved it, and extras put at the old spot never
			// loaded - they wait for Raft's island under them - AU58)
			at = new Vector3(l.transform.position.x, 0f, l.transform.position.z);
			string name = ExtrasPrefix + Current.Seed + "-" + key;
			IslandFile file = null;
			string what = "";
			try { file = RandomizerContent.Extras(l, name, Current, Hash(Current.Seed, (int)key, 11), out what); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Extras for '" + l.name + "': " + e); }
			if (file == null) { Log("'" + l.name + "': nothing extra"); yield break; }
			Log("'" + l.name + "' gets extras: " + what);
			ExtrasCount++;
			// (its size known before it is in the world: its file is written a moment later, and a look at it meanwhile
			// logged the file missing and kept -1, which then read as grown and moved it off Raft's island - UW3)
			CustomIslandSpawner.CacheSize(name, IslandSpawner.LandRadius(file), file.Elevation);
			IslandWorldState.Entry entry = IslandWorldState.Add(name, at, null, false);
			entry.Loading = true;
			IslandFile f = file;
			yield return CustomIslandSpawner.GenerateAndSpawn(() => f, name, entry);
		}

		#endregion

		#region Animals

		public class Variant
		{
			public string Name;
			public Color Tint;
			public bool Alpha;
			/// <summary>Lighter than the animal: its textures brightened (TintTextures), not its colour multiplied.</summary>
			public bool Light;
		}

		// Colours multiply the animal's own texture: darker or tinted; the light ones (a channel over 1: snow, cream, pale -
		// ROADMAP LM5) brighten copies of its textures instead, as a colour property can't lighten
		static readonly Variant[] Fur =
		{
			V("charcoal", 0.36f, 0.36f, 0.38f), V("ash", 0.72f, 0.72f, 0.76f), V("umber", 0.72f, 0.52f, 0.38f), V("rust", 0.95f, 0.58f, 0.40f),
			V("sand", 1.00f, 0.88f, 0.66f), V("moss", 0.68f, 0.80f, 0.56f), V("frost", 0.78f, 0.88f, 1.00f), V("night", 0.45f, 0.48f, 0.66f),
			V("snow", 1.75f, 1.75f, 1.8f), V("cream", 1.6f, 1.48f, 1.2f),
		};
		static readonly Variant[] Feathers = { V("crimson", 1f, 0.52f, 0.48f), V("slate", 0.55f, 0.6f, 0.72f), V("gold", 1f, 0.84f, 0.45f), V("charcoal", 0.38f, 0.38f, 0.4f), V("moss", 0.7f, 0.82f, 0.58f) };
		static readonly Variant[] Scales = { V("midnight", 0.42f, 0.46f, 0.6f), V("tiger", 0.88f, 0.74f, 0.5f), V("rust", 0.9f, 0.56f, 0.44f), V("reef", 0.55f, 0.82f, 0.86f), V("olive", 0.7f, 0.76f, 0.5f), V("gold", 1f, 0.8f, 0.36f), V("pale", 1.7f, 1.75f, 1.8f) };
		static readonly Variant AlphaFur = new Variant { Name = "alpha", Tint = new Color(0.46f, 0.3f, 0.28f), Alpha = true };
		static readonly Variant BigBruce = new Variant { Name = "big bruce", Tint = new Color(0.34f, 0.36f, 0.44f), Alpha = true };

		static Variant V(string name, float r, float g, float b) { return new Variant { Name = name, Tint = new Color(r, g, b), Light = r > 1f || g > 1f || b > 1f }; }

		static readonly HashSet<AI_NetworkBehaviourType> LandAnimals = new HashSet<AI_NetworkBehaviourType>
		{
			AI_NetworkBehaviourType.Boar, AI_NetworkBehaviourType.Pig, AI_NetworkBehaviourType.Bear, AI_NetworkBehaviourType.PolarBear, AI_NetworkBehaviourType.Hyena,
			AI_NetworkBehaviourType.Rat, AI_NetworkBehaviourType.Rat_Tangaroa, AI_NetworkBehaviourType.Roach, AI_NetworkBehaviourType.Chicken, AI_NetworkBehaviourType.Goat, AI_NetworkBehaviourType.Llama,
		};
		static readonly HashSet<AI_NetworkBehaviourType> Birds = new HashSet<AI_NetworkBehaviourType> { AI_NetworkBehaviourType.StoneBird, AI_NetworkBehaviourType.StoneBird_Caravan, AI_NetworkBehaviourType.Puffin };
		static readonly HashSet<AI_NetworkBehaviourType> SeaAnimals = new HashSet<AI_NetworkBehaviourType>
		{
			AI_NetworkBehaviourType.Shark, AI_NetworkBehaviourType.PufferFish, AI_NetworkBehaviourType.AnglerFish, AI_NetworkBehaviourType.Turtle, AI_NetworkBehaviourType.Stingray, AI_NetworkBehaviourType.Dolphin,
		};
		/// <summary>Animals that can be alphas (the fighters).</summary>
		static readonly HashSet<AI_NetworkBehaviourType> Fighters = new HashSet<AI_NetworkBehaviourType>
		{
			AI_NetworkBehaviourType.Boar, AI_NetworkBehaviourType.Pig, AI_NetworkBehaviourType.Bear, AI_NetworkBehaviourType.PolarBear, AI_NetworkBehaviourType.Hyena, AI_NetworkBehaviourType.StoneBird,
		};

		/// <summary>The palette for an animal, or null if it keeps its looks (bosses, people, bugs, the whale...).</summary>
		public static Variant[] PaletteOf(AI_NetworkBehaviourType t)
		{
			if (LandAnimals.Contains(t)) return Fur;
			if (Birds.Contains(t)) return Feathers;
			if (SeaAnimals.Contains(t)) return Scales;
			return null;
		}

		/// <summary>What this animal looks like in this world, or null for Raft's own look. The same on every machine.</summary>
		public static Variant VariantOf(AI_NetworkBehaviourType type, uint objectIndex)
		{
			Variant[] palette = PaletteOf(type);
			if (palette == null || !Current.On) return null;
			// (the Rogue shark option's has its own colour)
			if (type == AI_NetworkBehaviourType.Shark && RogueShark.IsRogueIndex(objectIndex)) return null;
			if (Current.Has(RandomizerSettings.Alphas))
			{
				float alpha = type == AI_NetworkBehaviourType.Shark ? Current.Pick(0.06f, 0.12f, 0.22f) : Fighters.Contains(type) ? Current.Pick(0.03f, 0.06f, 0.12f) : 0f;
				if (Unit(Hash(Current.Seed, (int)objectIndex, 3)) < alpha) return type == AI_NetworkBehaviourType.Shark ? BigBruce : AlphaFur;
			}
			if (!Current.Has(RandomizerSettings.Colours)) return null;
			int h = Hash(Current.Seed, (int)objectIndex, 5);
			if (Unit(h) >= Current.Pick(0.15f, 0.3f, 0.55f)) return null;
			Variant v = palette[(int)((uint)Hash(h, 9) % (uint)palette.Length)];
			// Gold is rare
			if (v.Name == "gold" && Unit(Hash(h, 13)) > 0.35f) v = palette[0];
			return v;
		}

		static void HandleAnimals()
		{
			AI_NetworkBehaviour[] all = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>();
			foreach (AI_NetworkBehaviour ai in all)
			{
				if (ai == null) continue;
				int id = ai.GetInstanceID();
				uint was;
				if (animalsSeen.TryGetValue(id, out was) && was == ai.ObjectIndex) continue;
				if (ai.ObjectIndex == 0) continue; // (not set up yet)
				// Animals of custom islands have the builder's looks (and the extras' animals their own colours)
				bool custom = CreatureSpawner.IsOnCustomIsland(ai);
				// (a player: the host's word on an island's animal goes on another channel than the animal itself and may come
				// a moment later - an animal without a spawner is looked at once it has been here a few seconds)
				if (!custom && !Raft_Network.IsHost && CreatureSpawner.HostSendsSpots && ai.connectedSpawner == null)
				{
					float first;
					if (!animalFirstSeen.TryGetValue(id, out first)) animalFirstSeen[id] = first = Time.unscaledTime;
					if (Time.unscaledTime - first < SpotsGraceSeconds) continue;
				}
				animalFirstSeen.Remove(id);
				animalsSeen[id] = ai.ObjectIndex;
				if (custom) continue;
				Variant v = VariantOf(ai.behaviourType, ai.ObjectIndex);
				if (v == null) continue;
				Apply(ai, v);
			}
			if (animalsSeen.Count > all.Length * 2 + 64)
			{
				var alive = new HashSet<int>(all.Where(a => a != null).Select(a => a.GetInstanceID()));
				foreach (int dead in animalsSeen.Keys.Where(k => !alive.Contains(k)).ToList()) animalsSeen.Remove(dead);
			}
			if (animalFirstSeen.Count > all.Length + 64) animalFirstSeen.Clear();
		}

		static readonly FieldInfo SharkRendererField = typeof(AI_StateMachine_Shark).GetField("sharkRenderer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		/// <summary>The renderers of the animal's body: its skinned meshes (not the net or name tag it gets when caught); the shark's lives apart from it.</summary>
		public static List<Renderer> BodyOf(AI_NetworkBehaviour ai)
		{
			var list = new List<Renderer>();
			var shark = ai as AI_NetworkBehavior_Shark;
			if (shark != null && shark.stateMachineShark != null && SharkRendererField != null)
			{
				Renderer r = SharkRendererField.GetValue(shark.stateMachineShark) as Renderer;
				if (r != null) list.Add(r);
			}
			foreach (SkinnedMeshRenderer r in ai.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				Material m = r.sharedMaterial;
				string n = (r.name + " " + (m != null ? m.name : "")).ToLowerInvariant();
				if (n.Contains("net") || n.Contains("nametag")) continue;
				if (!list.Contains(r)) list.Add(r);
			}
			return list;
		}

		/// <summary>Tests (AT36): the animal gets the alpha look and stats as when the world's roll makes it one - again each
		/// time, as every change of the randomizer looks at the animals again.</summary>
		internal static void ApplyAlphaForTest(AI_NetworkBehaviour ai) { if (ai != null) Apply(ai, AlphaFur); }

		/// <summary>Tests (LM5): the animal gets one of the colours by name ("snow", "charcoal"...).</summary>
		internal static bool ApplyColourForTest(AI_NetworkBehaviour ai, string name)
		{
			Variant v = Fur.Concat(Feathers).Concat(Scales).FirstOrDefault(x => x.Name == name);
			if (ai == null || v == null) return false;
			Apply(ai, v);
			return true;
		}

		static void Apply(AI_NetworkBehaviour ai, Variant v)
		{
			int tinted = 0;
			foreach (Renderer r in BodyOf(ai))
			{
				if (v.Light) { tinted += TintTextures(r, v.Tint); continue; }
				int n = ObjectProps.TintRenderer(r, v.Tint);
				// (the shark's shader has no colour, only textures)
				tinted += n > 0 ? n : TintTextures(r, v.Tint);
			}
			VariantOfIndex[ai.ObjectIndex] = v.Name;
			if (!v.Alpha) { ColouredCount++; if (ColouredCount <= 30) Log(ai.behaviourType + " #" + ai.ObjectIndex + " is " + v.Name + " (" + tinted + " material(s))"); return; }

			AlphaCount++;
			// Bigger: the same size on every machine however Raft rolled it (land animals only: the shark's body lives apart)
			if (ai.behaviourType != AI_NetworkBehaviourType.Shark)
			{
				float baseScale = 1f;
				try { baseScale = Mathf.Max(ai.localScaleInterval.minValue, ai.localScaleInterval.maxValue); } catch { }
				ai.transform.localScale = Vector3.one * (baseScale > 0f ? baseScale : 1f) * 1.4f;
			}
			string label = LabelOf(ai.behaviourType);
			// (its stats once: every change of the randomizer - even its level clicked again - looks at the animals again,
			// and an alpha got x3 health on top of x3, healed: Big Bruce killed in one bite after two clicks)
			if (Raft_Network.IsHost && !alphas.ContainsKey(ai))
			{
				var props = new Dictionary<string, string>
				{
					{ ObjectProps.CreatureHealth, ai.behaviourType == AI_NetworkBehaviourType.Shark ? "2.5" : "3" },
					{ ObjectProps.CreatureDamage, "1.6" },
				};
				// (not on the raft: Big Bruce bites players harder, his bites on raft blocks stay Raft's - AU69)
				CreatureSpawner.ApplyStats(ai, props, false);
				// (and once to the other players: the health it has now, exactly - their guess from Raft's numbers went wrong now and then)
				CreatureSpawner.SendHostHealth(ai);
				alphas[ai] = label;
			}
			// (another player's copy: the host's health, or the alpha would die here long before it does on the host)
			else CreatureSpawner.MatchHealth(ai, ai.behaviourType == AI_NetworkBehaviourType.Shark ? 2.5f : 3f);
			Log((ai.behaviourType == AI_NetworkBehaviourType.Shark ? "Big Bruce" : "An alpha " + label.ToLowerInvariant()) + " #" + ai.ObjectIndex + " (" + tinted + " material(s))");
			// Tell the player when it is close
			Network_Player p = RAPI.GetLocalPlayer();
			if (p != null && (p.transform.position - ai.transform.position).magnitude < 150f)
			{
				if (ai.behaviourType == AI_NetworkBehaviourType.Shark) IslandInfo.Show("Big Bruce", "", "A huge, dark shark is circling. It bites harder - and it's worth a trophy.");
				else IslandInfo.Show("Alpha " + label.ToLowerInvariant(), "", "A huge, dark " + label.ToLowerInvariant() + " roams nearby. It hits hard and takes a beating - and it's worth it.");
			}
		}

		static readonly Dictionary<string, RenderTexture> tintedTextures = new Dictionary<string, RenderTexture>();
		static Material blitMaterial;

		/// <summary>
		/// Shaders without a colour to tint (the shark's): its textures are swapped for copies multiplied by the colour,
		/// made on the graphics card (Raft's textures can't be read). One copy per texture and colour, shared.
		/// </summary>
		public static int TintTextures(Renderer r, Color factor)
		{
			Material m = r.sharedMaterial;
			if (m == null) return 0;
			int n = 0;
			var block = new MaterialPropertyBlock();
			r.GetPropertyBlock(block);
			foreach (string prop in new[] { "_Diffuse", "_DiffuseDamaged", "_MainTex" })
			{
				if (!m.HasProperty(prop)) continue;
				Texture src = m.GetTexture(prop);
				RenderTexture rt = src != null ? Tinted(src, factor) : null;
				if (rt == null) continue;
				block.SetTexture(prop, rt);
				n++;
			}
			if (n > 0) r.SetPropertyBlock(block);
			return n;
		}

		static RenderTexture Tinted(Texture src, Color c)
		{
			// Not ToHtmlStringRGB: it clamps, so a light colour (over 1) would share plain white's copy
			string key = src.GetInstanceID() + "/" + c.r.ToString("F3") + "/" + c.g.ToString("F3") + "/" + c.b.ToString("F3");
			RenderTexture rt;
			if (tintedTextures.TryGetValue(key, out rt) && rt != null) return rt;
			if (blitMaterial == null)
			{
				Shader s = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
				if (s == null) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] No shader to colour textures with"); return null; }
				blitMaterial = new Material(s);
			}
			rt = new RenderTexture(Mathf.Min(src.width, 1024), Mathf.Min(src.height, 1024), 0, RenderTextureFormat.ARGB32)
			{
				useMipMap = true, autoGenerateMips = true, wrapMode = src.wrapMode, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "CI_Tinted_" + src.name,
			};
			// A light colour (a channel over 1) lifts the dark parts first, then tints: multiplying alone left a dark animal (the
			// warthog) as dark as it was, snow and cream looking like Raft's own (ROADMAP D6 picture)
			float top = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
			float lift = top > 1f ? (top - 1f) * 0.6f : 0f;
			if (top > 1f) c = new Color(c.r / top, c.g / top, c.b / top);
			blitMaterial.SetVector("_TextureSampleAdd", new Vector4(lift, lift, lift, 0f));
			blitMaterial.color = new Color(c.r, c.g, c.b, 1f);
			Graphics.Blit(src, rt, blitMaterial);
			tintedTextures[key] = rt;
			return rt;
		}

		static string LabelOf(AI_NetworkBehaviourType t)
		{
			if (t == AI_NetworkBehaviourType.Shark) return "Shark";
			ContentCatalog.CreatureKind k = ContentCatalog.Creatures.FirstOrDefault(c => c.Type == t);
			return k != null ? k.Label : t.ToString();
		}

		/// <summary>What an alpha drops when it dies: a trophy head of its kind if Raft has one, and more.</summary>
		static readonly Dictionary<AI_NetworkBehaviourType, string[]> Spoils = new Dictionary<AI_NetworkBehaviourType, string[]>
		{
			{ AI_NetworkBehaviourType.Boar, new[] { "Head_Boar*1", "Raw_GenericMeat*3", "Leather*3" } },
			{ AI_NetworkBehaviourType.Pig, new[] { "Head_Boar*1", "Raw_GenericMeat*3", "Leather*2" } },
			{ AI_NetworkBehaviourType.Bear, new[] { "Head_Bear*1", "Raw_GenericMeat*4", "Leather*4" } },
			{ AI_NetworkBehaviourType.PolarBear, new[] { "Head_PolarBear*1", "Raw_GenericMeat*4", "Leather*4", "Wool*3" } },
			{ AI_NetworkBehaviourType.Hyena, new[] { "Head_Hyena*1", "Raw_GenericMeat*2", "Leather*2" } },
			{ AI_NetworkBehaviourType.StoneBird, new[] { "Head_Screecher*1", "Feather*8" } },
			{ AI_NetworkBehaviourType.Shark, new[] { "Head_Shark*1", "Raw_Shark*4" } },
		};

		/// <summary>Host: an alpha that died drops its spoils for everyone (Raft's own dropped items, which it sends to the other players).</summary>
		static void WatchAlphas()
		{
			foreach (AI_NetworkBehaviour ai in alphas.Keys.ToList())
			{
				if (ai == null) { alphas.Remove(ai); continue; }
				if (ai.networkEntity == null || !ai.networkEntity.IsDead) continue;
				alphas.Remove(ai);
				DropSpoils(ai.behaviourType, ai.transform.position, Hash(Current.Seed, (int)ai.ObjectIndex, 21));
			}
		}

		public static List<string> DropSpoils(AI_NetworkBehaviourType type, Vector3 at, int seed)
		{
			var rnd = new System.Random(seed);
			var items = new List<string>();
			string[] list;
			if (Spoils.TryGetValue(type, out list)) items.AddRange(list);
			else items.Add("Leather*2");
			if (rnd.NextDouble() < 0.4) items.Add("TitaniumIngot*1");
			var dropped = new List<string>();
			for (int i = 0; i < items.Count; i++)
			{
				string[] p = items[i].Split('*');
				int n = p.Length > 1 ? int.Parse(p[1], CultureInfo.InvariantCulture) : 1;
				Item_Base item = null;
				try { item = ItemManager.GetItemByName(p[0]); } catch { }
				if (item == null) continue;
				float a = i * 2.1f;
				Vector3 pos = at + new Vector3(Mathf.Cos(a), 1.2f, Mathf.Sin(a)) * 0.8f;
				try { Helper.DropItem(new ItemInstance(item, n, item.MaxUses), pos, Vector3.up, false); dropped.Add(p[0] + "*" + n); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Dropping " + p[0] + ": " + e.Message); }
			}
			Log("An alpha " + type + " was defeated: dropped " + string.Join(", ", dropped.ToArray()));
			return dropped;
		}

		#endregion

		#region Helpers

		/// <summary>A well mixed hash of a few numbers (the same everywhere: no string or object hashes).</summary>
		public static int Hash(params int[] values)
		{
			unchecked
			{
				uint h = 2166136261u;
				foreach (int v in values)
				{
					uint k = (uint)v;
					k *= 0xcc9e2d51u; k = (k << 15) | (k >> 17); k *= 0x1b873593u;
					h ^= k; h = (h << 13) | (h >> 19); h = h * 5 + 0xe6546b64u;
				}
				h ^= h >> 16; h *= 0x85ebca6bu; h ^= h >> 13; h *= 0xc2b2ae35u; h ^= h >> 16;
				return (int)h;
			}
		}

		/// <summary>0..1 from a hash.</summary>
		public static float Unit(int hash) { return ((uint)hash & 0xFFFFFF) / 16777216f; }

		static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

		/// <summary>What the randomizer does in this world, for the Randomizer command.</summary>
		public static string Describe()
		{
			return "World randomizer: " + Current.Describe() + (Current.On ? " (seed " + Current.Seed + ")" : "") +
				"\nThis session: " + ColouredCount + " coloured animal(s), " + AlphaCount + " alpha(s), " + MovedCount + " crate(s)/clam(s) moved, " + ExtrasCount + " island(s) given extras; " +
				seen.Count + " of Raft's islands looked at, " + IslandWorldState.Islands.Count(IsExtras) + " with extras" +
				(Current.Has(RandomizerSettings.Oddities) || Current.Has(RandomizerSettings.Bosses) ? "\nSailed towards the next oddity " + (sailedOddity / 1000f).ToString("F1", CultureInfo.InvariantCulture) + " km, boss lair " + (sailedBoss / 1000f).ToString("F1", CultureInfo.InvariantCulture) + " km" : "") +
				"\nParts: " + string.Join(", ", RandomizerSettings.Features.Select((f, i) => RandomizerSettings.FeatureLabels[i] + (Current.Disabled.Contains(f) ? " off" : " on")).ToArray()) +
				"\nRandomizer off|light|normal|wild, Randomizer +part / -part (e.g. -alphas)";
		}

		#endregion
	}

	/// <summary>
	/// Raft saves which of an island's crates and clams were used by the place Raft put them (LandmarkItem.localLandmarkPosition),
	/// but finds them again by where they are now. For crates the randomizer moved, it finds them by that saved place again,
	/// so a used crate stays used after loading and for players who join.
	/// </summary>
	[HarmonyLib.HarmonyPatch(typeof(Landmark), "GetLandmarkItemByLocalPosition")]
	static class FindMovedLandmarkItems
	{
		static bool Prefix(Landmark __instance, Vector3 localPosition, ref LandmarkItem __result)
		{
			try
			{
				if (__instance.landmarkItems == null || !WorldRandomizer.MovedAny(__instance)) return true;
				foreach (LandmarkItem i in __instance.landmarkItems)
					if (i != null && Vector3.Distance(i.localLandmarkPosition, localPosition) <= 0.01f) { __result = i; return false; }
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [randomizer] Finding a moved crate: " + e.Message); }
			return true;
		}
	}

	/// <summary>
	/// The ground of one of Raft's islands, found by casting rays at its colliders (not at its pickups, trees and crates,
	/// which Raft may move or hide): the same answers on every machine.
	/// </summary>
	public class LandGround
	{
		readonly Transform root;
		public readonly Vector3 Centre;
		public readonly float Radius;

		public LandGround(Landmark l)
		{
			root = l.transform;
			// (Raft moves its pooled islands around: the colliders must be where the island is now)
			Physics.SyncTransforms();
			Bounds b = new Bounds(root.position, Vector3.zero);
			bool any = false;
			foreach (Collider c in l.GetComponentsInChildren<Collider>())
			{
				if (c.isTrigger || !c.enabled || c.GetComponentInParent<LandmarkItem>() != null) continue;
				if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
			}
			Centre = new Vector3(b.center.x, 0f, b.center.z);
			Radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 25f, 260f);
		}

		/// <summary>The island's ground straight below (x, z), if there is any.</summary>
		public bool Hit(float x, float z, out Vector3 point, out Vector3 normal)
		{
			point = Vector3.zero; normal = Vector3.up;
			RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 250f, z), Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
			float best = float.MaxValue;
			bool found = false;
			foreach (RaycastHit h in hits)
			{
				if (h.distance >= best || h.collider == null || !h.collider.transform.IsChildOf(root)) continue;
				if (h.collider.GetComponentInParent<LandmarkItem>() != null) continue;
				best = h.distance; point = h.point; normal = h.normal; found = true;
			}
			return found;
		}

		/// <summary>A random spot on the island where ok(height above the sea, slope) holds, at least apart metres from everything in taken.</summary>
		public Vector3? Find(System.Random rnd, Func<float, float, bool> ok, List<Vector3> taken, float apart, int tries = 200, Vector3? around = null, float radius = -1f)
		{
			Vector3 c = around ?? Centre;
			float r = radius > 0f ? radius : Radius;
			for (int i = 0; i < tries; i++)
			{
				double a = rnd.NextDouble() * Math.PI * 2, d = Math.Sqrt(rnd.NextDouble()) * r;
				float x = c.x + (float)(Math.Cos(a) * d), z = c.z + (float)(Math.Sin(a) * d);
				Vector3 p, n;
				if (!Hit(x, z, out p, out n)) continue;
				if (!ok(p.y, Vector3.Angle(n, Vector3.up))) continue;
				if (taken != null && taken.Any(t => (t - p).sqrMagnitude < apart * apart)) continue;
				return p;
			}
			return null;
		}
	}
}

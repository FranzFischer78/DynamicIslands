using System;
using System.Collections.Generic;
using System.Globalization;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Difficulty: Hard" on an island's row in the island picker and the island library (TODO 4d, the user 2026-10-19).
	/// Only a label: it changes nothing in the game. It comes from the island's monsters:
	///   every creature spawn point of a monster (not the animals to catch, the harmless ones, the harmless sea life) counts
	///   with its creature.count (a range "2..3" counts its highest number), plus the animals a boss calls (boss.summon).
	///   A monster made harmless in the editor (damage 0) doesn't count. The generator's monsters are spawn points in the
	///   file like any other, so they count; map types and random new islands are made new each time and get no label.
	/// The library's build-index.ps1 counts the same way (its "monsters" field in index.json): keep the two alike.
	/// </summary>
	public static class IslandDifficulty
	{
		/// <summary>The bands: up to this many monsters (inclusive) the name next to it; more than the last = Impossible.</summary>
		static readonly int[] Upto = { 0, 5, 20, 49 };
		static readonly string[] Names = { "Easy", "Medium", "Hard", "Extreme", "Impossible" };

		/// <summary>Creatures that aren't monsters (as LevelSystem.Harmless; no Unity here, the summaries read off the main thread).</summary>
		static readonly HashSet<string> Harmless = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{ "Chicken", "Goat", "Llama", "Puffin", "ButlerBot", "Turtle", "Dolphin", "Stingray", "Whale", "BirdPack" };
		const string Prefix = "Creature_";

		/// <summary>The name of a monster count: Easy (0), Medium (1-5), Hard (6-20), Extreme (21-49), Impossible (50+).</summary>
		public static string NameOf(int monsters)
		{
			for (int i = 0; i < Upto.Length; i++) if (monsters <= Upto[i]) return Names[i];
			return Names[Names.Length - 1];
		}

		/// <summary>"Difficulty: Hard", or "" when the count isn't known (below 0).</summary>
		public static string Label(int monsters) { return monsters < 0 ? "" : "Difficulty: " + NameOf(monsters); }

		/// <summary>Whether an object name is a monster's spawn point (by name only: ContentCatalog isn't touched off the main thread).</summary>
		public static bool IsMonster(string objectName)
		{
			if (objectName == null || !objectName.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			string kind = objectName.Substring(Prefix.Length);
			return kind.Length > 0 && !Harmless.Contains(kind) && !kind.StartsWith("NPC_", StringComparison.Ordinal);
		}

		/// <summary>The monsters of one placed object (0 for anything else).</summary>
		public static int MonstersOf(IslandObject o)
		{
			if (o == null || !IsMonster(o.Name)) return 0;
			IDictionary<string, string> p = o.Props;
			if (p != null && Number(p, ObjectProps.CreatureDamage, 1) <= 0) return 0;
			int count = Math.Max(1, Math.Min(ObjectProps.MaxCount, Number(p, ObjectProps.CreatureCount, 1)));
			return count + Math.Max(0, Math.Min(BossPhases.MaxSummon, Number(p, BossPhases.Summon, 0)));
		}

		/// <summary>All the island's monsters.</summary>
		public static int MonstersOf(IslandFile f)
		{
			int n = 0;
			if (f != null) foreach (IslandObject o in f.Objects) n += MonstersOf(o);
			return n;
		}

		/// <summary>A number property; a range "2..3" gives its highest number.</summary>
		static int Number(IDictionary<string, string> p, string key, int fallback)
		{
			string v;
			if (p == null || !p.TryGetValue(key, out v) || string.IsNullOrEmpty(v)) return fallback;
			int dots = v.LastIndexOf("..", StringComparison.Ordinal);
			if (dots >= 0) v = v.Substring(dots + 2);
			float f;
			return float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? (int)Math.Ceiling(f) : fallback;
		}
	}
}

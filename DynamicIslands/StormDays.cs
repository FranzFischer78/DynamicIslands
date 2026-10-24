using System;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Storm days" (WorldOptions.StormDays): on about one day in ten (never before day 3) the sea is rough
	/// and it rains all day: Raft's own rain weather is held from morning to the next morning (in the snow region its rough
	/// snow), the shark comes for the raft more often (x0.6 between looks, on top of Night is dangerous), and the rain waters
	/// the crops as Raft's rain always does. At nightfall before a storm day every player is warned.
	///   - Which days are stormy comes from the world's seed and the day: every machine knows it with no message. The weather
	///     itself is the host's: Raft sends it to every player (Message_WeatherManager_SetWeather).
	///   - Nothing is saved: a loaded world rolls the same days.
	/// </summary>
	public static class StormDays
	{
		public const float Chance = 0.1f, SharkStorm = 0.6f;
		public const int FirstDay = 3;

		/// <summary>Tests: the day whatever the world's counter says (null = the counter).</summary>
		internal static int? TestDay;
		/// <summary>Tests: night (true) or day (false) for the warning, whatever the sky says (null = the sky).</summary>
		internal static bool? TestNight;

		static int warnedDay = -1, bannerDay = -1;
		static bool holding;
		static float nextCheck;

		public static bool On { get { return WorldOptions.On(WorldOptions.StormDays); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [storm] " + msg); }

		public static int Today
		{
			get
			{
				if (TestDay.HasValue) return TestDay.Value;
				try { return WorldManager.DayCounter; } catch { return 0; }
			}
		}

		internal static void Reset() { TestDay = null; TestNight = null; warnedDay = -1; bannerDay = -1; holding = false; nextCheck = 0f; }

		/// <summary>Whether this day is stormy in this world (the same on every machine).</summary>
		public static bool Rolls(int seed, int day)
		{
			if (day < FirstDay) return false;
			return WorldOptions.Unit(seed, day, 77) < Chance; // (WorldOptions.Mix: storm days came in runs with System.Random)
		}

		/// <summary>Today is a storm day (with the option on).</summary>
		public static bool IsStorm { get { return On && Rolls(WorldOptions.Seed, Today); } }

		/// <summary>The shark's time between looks for the raft (1 unless a storm day).</summary>
		public static float SharkFactor { get { return IsStorm ? SharkStorm : 1f; } }

		static bool Night
		{
			get
			{
				if (TestNight.HasValue) return TestNight.Value;
				return NightDanger.IsNight;
			}
		}

		/// <summary>The storm's weather here: Raft's rough snow in the snow region, its rain elsewhere.</summary>
		public static UniqueWeatherType StormWeather(WeatherManager wm)
		{
			WeatherPoolType pool = Traverse.Create(wm).Field("currentPoolType").GetValue<WeatherPoolType>();
			return pool == WeatherPoolType.Snow ? UniqueWeatherType.SnowRough : UniqueWeatherType.Rain;
		}

		static WeatherManager manager;
		static WeatherManager Manager { get { if (manager == null) manager = UnityEngine.Object.FindObjectOfType<WeatherManager>(); return manager; } }

		/// <summary>Every second or two, in a world: the warning, the banner, and (host) the weather held.</summary>
		internal static void Tick()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Time.time < nextCheck) return;
			nextCheck = Time.time + 2f;
			int day = Today, seed = WorldOptions.Seed;
			if (!On)
			{
				if (holding) Release();
				return;
			}
			// The warning: at nightfall before a storm day
			if (Night && warnedDay != day && Rolls(seed, day + 1))
			{
				warnedDay = day;
				Log("A storm tomorrow (day " + (day + 1) + ")");
				IslandInfo.Show("A storm tomorrow", "", "The sky darkens: tomorrow the sea will be rough and the rain won't stop. Mind the raft - the shark likes a storm.");
			}
			bool storm = Rolls(seed, day);
			if (storm && bannerDay != day && !Night)
			{
				bannerDay = day;
				Log("A storm day (day " + day + ")");
				IslandInfo.Show("A storm day", "", "Rough sea and rain all day: the shark comes for the raft more often. The rain waters the crops.");
			}
			if (!Raft_Network.IsHost) return;
			WeatherManager wm = Manager;
			if (wm == null) return;
			if (storm)
			{
				UniqueWeatherType want = StormWeather(wm);
				// (Raft's own timer kept from choosing another weather while the storm lasts)
				if (wm.WeatherTimer < 120f) wm.WeatherTimer = 600f;
				if (wm.GetCurrentWeatherType() != want)
				{
					wm.SetWeather(want, false);
					Log("The storm's weather: " + want);
				}
				holding = true;
			}
			else if (holding) Release();
		}

		/// <summary>The storm over: Raft chooses its next weather soon.</summary>
		static void Release()
		{
			holding = false;
			WeatherManager wm = Manager;
			if (wm != null && Raft_Network.IsHost) wm.WeatherTimer = 5f;
			Log("The storm is over");
		}

		[ConsoleCommand(name: "StormDays", docs: "The world option Storm days: StormDays = today and the next storm day; StormDays roll <from> <to> = the storm days in a range; StormDays test <day>|off = this machine takes that day as today (tests)")]
		public static void StormDaysCommand(string[] args)
		{
			int seed = WorldOptions.Seed, from, to;
			if (args != null && args.Length > 2 && args[0] == "roll" && int.TryParse(args[1], out from) && int.TryParse(args[2], out to))
			{
				var days = Enumerable.Range(from, Math.Max(0, Math.Min(to - from + 1, 10000))).Where(d => Rolls(seed, d)).ToArray();
				Debug.Log("[CUSTOM ISLANDS] Storm days: " + from + "-" + to + " (seed " + seed + "): " + days.Length + ": " + string.Join(" ", days.Select(d => d.ToString()).ToArray()));
				return;
			}
			int forced;
			if (args != null && args.Length > 1 && args[0] == "test") TestDay = int.TryParse(args[1], out forced) ? forced : (int?)null;
			int day = Today, next = Enumerable.Range(day + 1, 1000).FirstOrDefault(d => Rolls(seed, d));
			WeatherManager wm = Manager;
			Debug.Log("[CUSTOM ISLANDS] Storm days: " + (On ? "on" : "off") + ", day " + day + (Rolls(seed, day) ? " is a storm day" : " is calm") + ", next storm day " + next +
				", weather " + (wm != null ? wm.GetCurrentWeatherType() + " (" + (wm.GetCurrentWeather() != null ? wm.GetCurrentWeather().name : "none") + ", next change in " + Mathf.RoundToInt(wm.WeatherTimer) + " s)" : "none") +
				", shark x" + SharkFactor.ToString("0.##"));
		}
	}
}

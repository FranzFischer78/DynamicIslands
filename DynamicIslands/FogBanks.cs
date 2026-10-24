using System;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AzureSky;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Fog banks" (WorldOptions.Fog, TODO 4i): now and then thick fog rolls in over the sea for a few hours
	/// and you see only 45-75 m, so islands show up late and the radar matters.
	///   - When: on about three days in five (from day FirstDay on) a bank starts at an hour of that day (any hour, so also at
	///     night) and lasts MinHours-MaxHours, with its own thickness. All of it comes from the world's seed and the day
	///     (WorldOptions.Unit): every machine knows it with no message of its own (the seed is the host's, sent with kind 17),
	///     a player who joins mid-fog is in the same fog, and a loaded world has the same banks. Nothing is saved.
	///   - The look is Raft's own sky fog (AzureSky's fog scattering, the same fog Raft's weathers use): FogBanksSkyPatch
	///     lends AzureSkyController a shorter fog distance and a full fog density just while it hands its values to the
	///     shaders (UpdateShaderUniforms) and puts Raft's back right after, so Raft's weather blending never sees ours. The
	///     fog takes the sky's colour: pale by day, dark blue at night. That fog covers the solid world only, so Unity's own
	///     fog (ApplyNearFog) covers the sea and the rest in the horizon's colour.
	///   - Smooth: a bank thickens over its first in-game hour and thins over its last, and what is shown follows that at
	///     most 1/FadeSeconds per second (a jump in time - sleeping, a loaded world - fades too). The distance is blended
	///     on a log scale, so the fog closes in evenly instead of all at the end.
	///   - Storms: while Raft's rough weather blows (rain, rough snow, big waves - Storm days and any storm option use
	///     those) the wind keeps the fog off; it comes back if the bank still lasts when the storm ends. Raft sends its
	///     weather to every player, so this too is the same everywhere. Under water the fog is off (Raft's own water fog).
	/// </summary>
	public static class FogBanks
	{
		/// <summary>The share of days with a fog bank (from day FirstDay on).</summary>
		public const float Chance = 0.6f;
		public const int FirstDay = 2;
		public const float MinHours = 3f, MaxHours = 6f, FadeHours = 1f;
		/// <summary>How far you see in a bank (each bank rolls between these).</summary>
		public const float NearVisibility = 45f, FarVisibility = 75f;
		/// <summary>The fastest the shown fog changes, 0 to full (real seconds).</summary>
		public const float FadeSeconds = 25f;
		/// <summary>AzureSky's fog distance for a visibility (its fog is full at about 1.25 x the distance).</summary>
		internal static float DistanceFactor = 0.8f;

		/// <summary>Tests: the target 0..1 whatever the schedule says (null = the schedule); the bank's visibility (m).</summary>
		internal static float? TestTarget, TestVisibility;
		/// <summary>Tests: no fading (the shown fog jumps to the target).</summary>
		internal static bool TestInstant;

		/// <summary>The fog shown now, 0..1.</summary>
		public static float Weight { get; private set; }
		/// <summary>How far you see in the bank shown now (m).</summary>
		public static float Visibility { get; private set; } = 60f;
		/// <summary>Raft's own fog distance and the one shown, at the last hand-over to the shaders (tests).</summary>
		public static float RaftDistance { get; private set; }
		public static float ShownDistance { get; private set; }
		public static float ShownDensity { get; private set; }

		static int noticedBank = int.MinValue;
		static float nextTarget, target;

		public static bool On { get { return WorldOptions.On(WorldOptions.Fog); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [fog] " + msg); }

		internal static void Reset() { TestTarget = null; TestVisibility = null; TestInstant = false; Weight = 0f; target = 0f; nextTarget = 0f; noticedBank = int.MinValue; ReleaseNearFog(); }

		/// <summary>One day's bank: Start in hours since day 0 (day x 24 + hour).</summary>
		public struct Bank
		{
			public int Day;
			public double Start;
			public float Hours, Visibility;
			public double End { get { return Start + Hours; } }
		}

		/// <summary>Whether this day has a fog bank in this world (the same on every machine).</summary>
		public static bool Rolls(int seed, int day)
		{
			if (day < FirstDay) return false;
			return WorldOptions.Unit(seed, day, 9101) < Chance;
		}

		public static Bank BankOf(int seed, int day)
		{
			return new Bank
			{
				Day = day,
				Start = day * 24.0 + WorldOptions.Unit(seed, day, 9102) * 24.0,
				Hours = MinHours + (float)WorldOptions.Unit(seed, day, 9103) * (MaxHours - MinHours),
				Visibility = NearVisibility + (float)WorldOptions.Unit(seed, day, 9104) * (FarVisibility - NearVisibility),
			};
		}

		/// <summary>The schedule's fog at a time (hours since day 0), 0..1, and the bank it belongs to (a bank of the day
		/// before can last past midnight).</summary>
		public static float At(int seed, double time, out Bank bank)
		{
			bank = default(Bank);
			float best = 0f;
			int day = (int)Math.Floor(time / 24.0);
			for (int d = day - 1; d <= day; d++)
			{
				if (!Rolls(seed, d)) continue;
				Bank b = BankOf(seed, d);
				double x = time - b.Start;
				if (x < 0 || x > b.Hours) continue;
				float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)Math.Min(x, b.Hours - x) / FadeHours));
				if (w > best) { best = w; bank = b; }
			}
			return best;
		}

		/// <summary>The next bank starting after a time.</summary>
		public static Bank Next(int seed, double time)
		{
			int day = Math.Max(FirstDay, (int)Math.Floor(time / 24.0));
			for (int d = day; d < day + 1000; d++)
			{
				if (!Rolls(seed, d)) continue;
				Bank b = BankOf(seed, d);
				if (b.Start > time) return b;
			}
			return default(Bank);
		}

		/// <summary>Raft's rough weathers: the wind keeps the fog off while they blow.</summary>
		public static bool Stormy(UniqueWeatherType w) { return w == UniqueWeatherType.Rain || w == UniqueWeatherType.SnowRough || w == UniqueWeatherType.BigWaves; }

		static WeatherManager weather;
		static AzureSkyController sky;

		/// <summary>Hours since day 0 by Raft's day counter and sky clock (-1 = no sky).</summary>
		public static double Now
		{
			get
			{
				try
				{
					if (sky == null) sky = ComponentManager<AzureSkyController>.Value ?? UnityEngine.Object.FindObjectOfType<AzureSkyController>();
					if (sky == null || sky.timeOfDay == null) return -1;
					return WorldManager.DayCounter * 24.0 + sky.timeOfDay.hour;
				}
				catch { return -1; }
			}
		}

		static UniqueWeatherType WeatherNow
		{
			get
			{
				try
				{
					if (weather == null) weather = UnityEngine.Object.FindObjectOfType<WeatherManager>();
					return weather != null ? weather.GetCurrentWeatherType() : UniqueWeatherType.Default;
				}
				catch { return UniqueWeatherType.Default; }
			}
		}

		/// <summary>Every frame: the schedule (once a second) and the shown fog following it.</summary>
		internal static void Tick()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Weight = 0f; target = 0f; ReleaseNearFog(); return; }
			if (Time.unscaledTime >= nextTarget)
			{
				nextTarget = Time.unscaledTime + 1f;
				target = 0f;
				if (TestTarget.HasValue)
				{
					target = Mathf.Clamp01(TestTarget.Value);
					Visibility = TestVisibility ?? Visibility;
				}
				else if (On)
				{
					double now = Now;
					Bank b = default(Bank);
					float w = now >= 0 ? At(WorldOptions.Seed, now, out b) : 0f;
					if (w > 0f)
					{
						if (Stormy(WeatherNow)) w = 0f;
						else Visibility = b.Visibility;
						if (w > 0f && noticedBank != b.Day)
						{
							noticedBank = b.Day;
							Log("A fog bank (day " + b.Day + ", " + Clock(b.Start) + " for " + b.Hours.ToString("0.0", CultureInfo.InvariantCulture) + " h, " + Mathf.RoundToInt(b.Visibility) + " m)");
							DynamicIslands.Notify("Fog rolls in: islands show up late - watch the radar.", false, 6);
						}
					}
					target = w;
				}
			}
			Weight = TestInstant ? target : Mathf.MoveTowards(Weight, target, Time.unscaledDeltaTime / FadeSeconds);
			if (Weight > 0.001f && !Underwater) ApplyNearFog(); else ReleaseNearFog();
		}

		/// <summary>
		/// The near fog: Unity's own fog (RenderSettings), which Raft's sea and everything drawn after the sky's fog take.
		/// AzureSky's fog is drawn over the solid world only (an opaque image effect), so the water and the raft's surroundings
		/// stayed clear to the horizon. Linear from NearStart x sight (clear around you) to NearEnd x sight (gone), in the
		/// colour of the sky's horizon ambient (pale by day, dark at night). Raft's values are kept and put back when the
		/// fog has lifted, under water (Raft's water fog) or out of the world.
		/// </summary>
		internal static float NearStart = 0.1f, NearEnd = 1.25f;
		const float FarEnd = 1500f;
		static bool nearTaken, nearFogWas;
		static FogMode nearModeWas;
		static float nearStartWas, nearEndWas;
		static Color nearColourWas;
		/// <summary>The near fog shown (tests): its end distance, 0 when not ours.</summary>
		public static float NearEndShown { get; private set; }

		static void ApplyNearFog()
		{
			if (!nearTaken)
			{
				nearTaken = true;
				nearFogWas = RenderSettings.fog; nearModeWas = RenderSettings.fogMode; nearStartWas = RenderSettings.fogStartDistance;
				nearEndWas = RenderSettings.fogEndDistance; nearColourWas = RenderSettings.fogColor;
			}
			float w = Weight;
			float end = Mathf.Exp(Mathf.Lerp(Mathf.Log(FarEnd), Mathf.Log(Mathf.Max(5f, Visibility * NearEnd)), w));
			RenderSettings.fog = true;
			RenderSettings.fogMode = FogMode.Linear;
			RenderSettings.fogStartDistance = Mathf.Lerp(FarEnd * 0.5f, Visibility * NearStart, w);
			RenderSettings.fogEndDistance = end;
			Color c = RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
				? Color.Lerp(RenderSettings.ambientEquatorColor, RenderSettings.ambientSkyColor, 0.25f) : RenderSettings.ambientLight;
			c.a = 1f;
			RenderSettings.fogColor = c;
			NearEndShown = end;
			SetTint(c, TintAlpha * w);
		}

		/// <summary>
		/// The fog's veil: a full-screen layer in the fog's colour, as strong as the fog (TintAlpha at its thickest). Raft's
		/// sea ignores both the sky's fog and Unity's, so without it the water 40-60 m out stayed crisp; with it the whole
		/// view greys over and the water fades into the fog. Off (disabled, alpha 0) whenever the fog is not ours.
		/// </summary>
		internal static float TintAlpha = 0.35f;
		static UnityEngine.UI.Image tint;
		/// <summary>The veil's strength now (tests): 0 when hidden.</summary>
		public static float TintShown { get { return tint != null && tint.enabled ? tint.color.a : 0f; } }

		static void SetTint(Color c, float alpha)
		{
			if (tint == null)
			{
				if (alpha <= 0.001f) return;
				Canvas canvas = UIKit.CreateCanvas("CustomIslands_FogTint", -101);
				UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
				RectTransform r = UIKit.Rect("Tint", canvas.transform);
				UIKit.Stretch(r);
				tint = r.gameObject.AddComponent<UnityEngine.UI.Image>();
				tint.raycastTarget = false;
			}
			tint.color = new Color(c.r, c.g, c.b, alpha);
			tint.enabled = alpha > 0.001f;
		}

		static void ReleaseNearFog()
		{
			NearEndShown = 0f;
			if (tint != null && tint.enabled) SetTint(Color.clear, 0f);
			if (!nearTaken) return;
			nearTaken = false;
			RenderSettings.fog = nearFogWas; RenderSettings.fogMode = nearModeWas; RenderSettings.fogStartDistance = nearStartWas;
			RenderSettings.fogEndDistance = nearEndWas; RenderSettings.fogColor = nearColourWas;
		}

		static string Clock(double time)
		{
			double h = ((time % 24.0) + 24.0) % 24.0;
			return ((int)h).ToString("00") + ":" + ((int)((h - (int)h) * 60)).ToString("00");
		}

		/// <summary>The values Raft had, put back after the hand-over.</summary>
		public struct Saved
		{
			public bool Set;
			public float Distance, Density, Blend;
		}

		static bool Underwater
		{
			get
			{
				Camera c = Camera.main;
				return c != null && c.transform.position.y < -0.6f;
			}
		}

		/// <summary>FogBanksSkyPatch, before Raft's sky hands its fog to the shaders: ours lent.</summary>
		internal static Saved Lend(AzureSkyController s)
		{
			RaftDistance = s.fogDistance;
			float w = Weight;
			if (w <= 0.001f || Underwater)
			{
				ShownDistance = s.fogDistance; ShownDensity = s.fogDensity;
				return default(Saved);
			}
			Saved saved = new Saved { Set = true, Distance = s.fogDistance, Density = s.fogDensity, Blend = s.fogBlend };
			float want = Mathf.Max(5f, Visibility * DistanceFactor);
			float from = Mathf.Max(want, s.fogDistance);
			// (never thinner than Raft's own: its foggy weather may already be closer)
			s.fogDistance = Mathf.Min(saved.Distance, Mathf.Exp(Mathf.Lerp(Mathf.Log(from), Mathf.Log(want), w)));
			s.fogDensity = Mathf.Lerp(s.fogDensity, 1f, w);
			ShownDistance = s.fogDistance; ShownDensity = s.fogDensity;
			return saved;
		}

		/// <summary>After the hand-over: Raft's own values back, so its weather blending goes on from them.</summary>
		internal static void Return(AzureSkyController s, Saved saved)
		{
			if (!saved.Set) return;
			s.fogDistance = saved.Distance;
			s.fogDensity = saved.Density;
			s.fogBlend = saved.Blend;
		}

		[ConsoleCommand(name: "FogBanks", docs: "The world option Fog banks: FogBanks = the fog now and the next bank; FogBanks roll <from> <to> = the fog banks of a range of days; FogBanks test on|off|auto [visibility] = this machine shows fog (on), none (off) or the schedule's (auto) - tests")]
		public static void FogBanksCommand(string[] args)
		{
			int seed = WorldOptions.Seed, from, to;
			if (args != null && args.Length > 2 && args[0] == "roll" && int.TryParse(args[1], out from) && int.TryParse(args[2], out to))
			{
				var banks = Enumerable.Range(from, Math.Max(0, Math.Min(to - from + 1, 1000))).Where(d => Rolls(seed, d)).Select(d => BankOf(seed, d)).ToArray();
				Debug.Log("[CUSTOM ISLANDS] Fog banks: days " + from + "-" + to + " (seed " + seed + "): " + banks.Length + ": " +
					string.Join(", ", banks.Select(b => "day " + b.Day + " " + Clock(b.Start) + " " + b.Hours.ToString("0.0", CultureInfo.InvariantCulture) + " h " + Mathf.RoundToInt(b.Visibility) + " m").ToArray()));
				return;
			}
			if (args != null && args.Length > 1 && args[0] == "test")
			{
				float v;
				if (args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) TestVisibility = v;
				TestTarget = args[1] == "on" ? 1f : args[1] == "off" ? 0f : (float?)null;
				nextTarget = 0f;
			}
			double now = Now;
			Bank cur = default(Bank);
			float w = now >= 0 ? At(seed, now, out cur) : 0f;
			Bank next = Next(seed, Math.Max(0, now));
			Debug.Log("[CUSTOM ISLANDS] Fog banks: " + (On ? "on" : "off") + (TestTarget.HasValue ? " (test " + TestTarget.Value + ")" : "") + ", day " + Math.Floor(now / 24) + " " + Clock(now) +
				(w > 0f ? ", in the bank of day " + cur.Day + " (" + Clock(cur.Start) + "-" + Clock(cur.End) + ", " + Mathf.RoundToInt(cur.Visibility) + " m)" : ", clear") +
				", next bank day " + next.Day + " " + Clock(next.Start) + " for " + next.Hours.ToString("0.0", CultureInfo.InvariantCulture) + " h" +
				", weather " + WeatherNow + (Stormy(WeatherNow) ? " (stormy: no fog)" : "") +
				", shown " + Weight.ToString("0.00", CultureInfo.InvariantCulture) + " of " + Mathf.RoundToInt(Visibility) + " m, sky fog distance " + RaftDistance.ToString("0", CultureInfo.InvariantCulture) + " -> " + ShownDistance.ToString("0", CultureInfo.InvariantCulture) +
				", density " + ShownDensity.ToString("0.00", CultureInfo.InvariantCulture) +
				(NearEndShown > 0f ? ", near fog to " + Mathf.RoundToInt(NearEndShown) + " m" : ""));
		}
	}

	/// <summary>Fog banks: our fog lent to Raft's sky just while it sets the shaders' fog (see FogBanks).</summary>
	[HarmonyPatch(typeof(AzureSkyController), "UpdateShaderUniforms")]
	static class FogBanksSkyPatch
	{
		static void Prefix(AzureSkyController __instance, out FogBanks.Saved __state) { __state = FogBanks.Lend(__instance); }
		static void Postfix(AzureSkyController __instance, FogBanks.Saved __state) { FogBanks.Return(__instance, __state); }
	}
}

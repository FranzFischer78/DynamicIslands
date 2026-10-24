using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Rare animal looks: cluckers, goats and llamas get six looks instead of Raft's three, and a world setting for how
	/// often a rare one comes (Normal = Raft's own 88 / 8 / 4 %, More, Lots ~ half rare). Plus a collection log in the journal.
	///
	/// How Raft does it: each domestic animal has a RareMaterial whose Randomizer lists its looks with weights (index 0 =
	/// the normal look, 1-2 = Raft's rare ones). The host rolls one when the animal first starts
	/// (AI_NetworkBehaviour_Domestic.Start -> AssignRandomRareMaterial) and sends the index to everyone (Message_RareMaterial);
	/// the save keeps it (RGD_Domestic.rareMaterialIndex), and loading puts it back (AssignSpecificMaterial(uint)).
	/// So the new looks are three more entries (indexes 3-5) on every machine's Randomizer, added before Raft uses it
	/// (AnimalLooksStart, AnimalLooksAssign): Raft's own message and save carry them. Without the mod, index 3-5 finds no
	/// entry and Raft keeps the normal look (AssignSpecificMaterial returns when the entry is missing).
	///
	/// The new looks are Raft's own textures recoloured on this machine (made the same on every machine, from Raft's normal
	/// and rare textures: no pictures shipped): patterns and colours painted over Raft's own shading, so they stay hand-painted.
	/// The prototype that shows them (looks_sheet.jpg) follows the same steps.
	/// </summary>
	public static class AnimalLooks
	{
		public const int Normal = 0, More = 1, Lots = 2;
		public static readonly string[] Names = { "Normal", "More", "Lots" };
		public const int Looks = 6;

		/// <summary>The weights of looks 0-5 per level. Normal leaves Raft's own and never rolls a new look.</summary>
		static readonly float[][] Weights =
		{
			null,
			new[] { 70f, 9f, 6f, 7f, 5f, 3f },
			new[] { 50f, 11f, 9f, 12f, 10f, 8f },
		};

		/// <summary>The three animals: index in the collection, Raft's type, the name shown, and the six looks' names.</summary>
		public static readonly AI_NetworkBehaviourType[] Animals = { AI_NetworkBehaviourType.Chicken, AI_NetworkBehaviourType.Goat, AI_NetworkBehaviourType.Llama };
		public static readonly string[] AnimalNames = { "Clucker", "Goat", "Llama" };
		public static readonly string[][] LookNames =
		{
			new[] { "blue", "brown", "red", "jade", "speckled", "golden" },
			new[] { "brown", "grey", "white", "toggenburg", "boer", "golden" },
			new[] { "brown", "patched", "striped", "piebald", "cream", "golden" },
		};

		/// <summary>The current world's level (clients get the host's).</summary>
		public static int Current;
		/// <summary>Chosen in the World settings window for the world being created (null = the last choice, Default).</summary>
		public static int? Pending;
		/// <summary>The looks tamed in this world (the host's): bit animal * 6 + look.</summary>
		public static int Caught { get; private set; }

		static bool clientKnown;

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [animal looks] " + msg); }

		public static int Clamp(int level) { return Mathf.Clamp(level, Normal, Lots); }

		public static string Describe(int level) { return level <= Normal ? "Raft's own" : Names[Clamp(level)]; }

		/// <summary>What a level does (the window's line).</summary>
		public static string DescriptionFor(int level)
		{
			switch (Clamp(level))
			{
				case More: return "About 1 in 3 cluckers, goats and llamas has a rare look, from six per animal: Raft's three and three new ones.";
				case Lots: return "About half of them have a rare look, from six per animal: Raft's three and three new ones (golden the rarest).";
				default: return "As in Raft: about 1 in 8 has one of Raft's two rare looks. New looks still show on animals that already have them.";
			}
		}

		public const string HelpText =
			"How often a clucker, goat or llama comes with a rare look in this world. Normal: Raft's own odds (88% the usual look, 8% and 4% Raft's two rare ones). " +
			"More: about one in three is rare. Lots: about half.\n" +
			"More and Lots also bring three new looks per animal, made from Raft's own textures: a jade, a speckled and a golden clucker; a toggenburg, a boer and a golden goat; " +
			"a piebald, a cream and a golden llama. Golden is the rarest.\n" +
			"An animal keeps its look when the world is saved and loaded, and every player sees the same one. " +
			"The journal's quest list counts the looks you have tamed (x/6 per animal). The host can change it later with the console command AnimalLooks (F10).";

		#region The last choice and the world file

		public static int Default
		{
			get { int l; return int.TryParse(WorldRules.ReadDefault("rarelooks") ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out l) ? Clamp(l) : Normal; }
		}

		public static void SaveDefault(int level) { WorldRules.SaveDefault("rarelooks", Clamp(level).ToString(CultureInfo.InvariantCulture)); }

		internal static void Reset(bool newWorld)
		{
			Caught = 0;
			if (newWorld)
			{
				Current = Clamp(Pending ?? Default);
				Pending = null;
				Log("New world: rare animal looks " + Describe(Current));
			}
			else Current = Normal;
		}

		internal static bool ReadLine(string key, string value)
		{
			int v;
			bool ok = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
			if (key.Equals("rarelooks", StringComparison.OrdinalIgnoreCase)) { Current = ok ? Clamp(v) : Normal; Log("This world's rare animal looks: " + Describe(Current)); return true; }
			if (key.Equals("lookscaught", StringComparison.OrdinalIgnoreCase)) { Caught = ok ? v & AllBits : 0; return true; }
			return false;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Current > Normal) yield return "@rarelooks=" + Current.ToString(CultureInfo.InvariantCulture);
			if (Caught != 0) yield return "@lookscaught=" + Caught.ToString(CultureInfo.InvariantCulture);
		}

		internal static bool HasState { get { return Current > Normal || Caught != 0; } }

		/// <summary>Host -> clients, in the world rules' Data.</summary>
		internal static string Data() { return ";rare=" + Current.ToString(CultureInfo.InvariantCulture) + ";caught=" + Caught.ToString(CultureInfo.InvariantCulture); }

		/// <summary>Client: a part of the host's world rules; true if it was ours.</summary>
		internal static bool FromHost(string key, float v)
		{
			if (key == "rare") { Current = Clamp(Mathf.RoundToInt(v)); return true; }
			if (key != "caught") return false;
			int before = Caught;
			Caught = Mathf.RoundToInt(v) & AllBits;
			if (clientKnown) Announce(before, Caught);
			clientKnown = true;
			return true;
		}

		internal static void OnWorldReceived() { Current = Normal; Caught = 0; clientKnown = false; }

		/// <summary>Host: changes the current world's level (the AnimalLooks command, the Esc window) and tells everyone.</summary>
		public static void Set(int level)
		{
			Current = Clamp(level);
			Log("Rare animal looks in this world: " + Describe(Current) + " (for animals that come from now on)");
			WorldRules.Broadcast();
			IslandWorldState.Save();
		}

		#endregion

		#region The collection

		const int AllBits = (1 << (Looks * 3)) - 1;

		public static int AnimalIndex(AI_NetworkBehaviourType t) { return Array.IndexOf(Animals, t); }

		public static bool Has(int animal, int look) { return (Caught & (1 << (animal * Looks + look))) != 0; }

		public static int CountOf(int animal) { int n = 0; for (int k = 0; k < Looks; k++) if (Has(animal, k)) n++; return n; }

		/// <summary>Host: an animal was tamed (Raft's Captured).</summary>
		internal static void OnCaptured(AI_NetworkBehaviour_Domestic a)
		{
			if (a == null || !Raft_Network.IsHost) return;
			int animal = AnimalIndex(a.behaviourType);
			if (animal < 0 || a.rareMaterialScript == null || a.rareMaterialScript.randomizer == null) return;
			int look = (int)a.rareMaterialScript.randomizer.lastRandomizedIndex;
			if (look < 0 || look >= Looks || Has(animal, look)) return;
			int before = Caught;
			Caught |= 1 << (animal * Looks + look);
			Log("Tamed a " + LookNames[animal][look] + " " + AnimalNames[animal].ToLowerInvariant() + " - " + CountOf(animal) + "/6");
			Announce(before, Caught);
			WorldRules.Broadcast();
		}

		/// <summary>New looks tamed (on every machine): a note, and a bigger one at six of six. Not for the animals a loaded world brings back.</summary>
		static void Announce(int before, int now)
		{
			if (Time.timeSinceLevelLoad < 15f) return;
			for (int animal = 0; animal < Animals.Length; animal++)
				for (int look = 1; look < Looks; look++)
				{
					int bit = 1 << (animal * Looks + look);
					if ((before & bit) != 0 || (now & bit) == 0) continue;
					int n = CountOf(animal);
					string name = AnimalNames[animal].ToLowerInvariant();
					if (n == Looks) DynamicIslands.Notify("Every " + name + " look tamed: all six! The journal's quest list keeps them.", false, 8);
					else DynamicIslands.Notify("A new look tamed: a " + LookNames[animal][look] + " " + name + " (" + n + "/6 " + name + " looks)", false, 5);
				}
		}

		/// <summary>The journal's lines (its quest list): the looks tamed per animal. Empty until one was.</summary>
		public static string JournalText()
		{
			if (Caught == 0) return "";
			var sb = new System.Text.StringBuilder("Animal looks tamed\n");
			for (int animal = 0; animal < Animals.Length; animal++)
			{
				int n = CountOf(animal);
				sb.Append("   ").Append(AnimalNames[animal]).Append(": ").Append(n).Append("/6");
				if (n == Looks) sb.Append("  - all six!");
				sb.Append("   ");
				for (int k = 0; k < Looks; k++) sb.Append(k > 0 ? ", " : "").Append(Has(animal, k) ? LookNames[animal][k] : "?");
				sb.Append('\n');
			}
			sb.Append(Current > Normal ? "" : "   (this world has Raft's own odds: the three new looks come with More or Lots)\n");
			return sb.Append('\n').ToString();
		}

		#endregion

		#region The looks on Raft's randomizer

		static readonly AccessTools.FieldRef<RareMaterial, RendererMaterialConnection[]> connectionsRef = AccessTools.FieldRefAccess<RareMaterial, RendererMaterialConnection[]>("materialConnections");
		static readonly Dictionary<int, Material[]> made = new Dictionary<int, Material[]>();
		static readonly HashSet<int> failed = new HashSet<int>();

		/// <summary>Every machine, before Raft reads the animal's looks: the three new ones added (once per animal), and the host's odds set.</summary>
		internal static void Ensure(RareMaterial rm, AI_NetworkBehaviourType type)
		{
			if (rm == null || rm.randomizer == null || rm.randomizer.items == null) return;
			Randomizer r = rm.randomizer;
			if (r.items.Length < 3) return;
			if (r.items.Length < Looks)
			{
				int animal = AnimalIndex(type);
				if (animal < 0) animal = AnimalOfMaterials(r);
				if (animal < 0) return;
				Material[] looks = LooksFor(animal, rm, r);
				if (looks == null) return;
				var items = r.items.ToList();
				for (int k = items.Count; k < Looks; k++) items.Add(new RandomItem { obj = looks[k - 3], weight = 0f, spawnChance = "" });
				r.items = items.ToArray();
			}
			float[] w = Weights[Clamp(Current)];
			if (w == null) { for (int k = 3; k < r.items.Length; k++) r.items[k].weight = 0f; return; }
			for (int k = 0; k < Looks && k < r.items.Length; k++) r.items[k].weight = w[k];
		}

		/// <summary>The animal by Raft's rare materials' names (ChickenMaterial_Rare1, GoatMaterial_..., LlamaMaterial_...).</summary>
		static int AnimalOfMaterials(Randomizer r)
		{
			string n = r.items[1] != null && r.items[1].obj != null ? r.items[1].obj.name.ToLowerInvariant() : "";
			if (n.Contains("chicken")) return 0;
			if (n.Contains("goat")) return 1;
			if (n.Contains("llama") || n.Contains("lama")) return 2;
			return -1;
		}

		static Material[] LooksFor(int animal, RareMaterial rm, Randomizer r)
		{
			Material[] looks;
			if (made.TryGetValue(animal, out looks) && looks != null && looks.All(m => m != null)) return looks;
			if (failed.Contains(animal)) return null;
			try
			{
				RendererMaterialConnection[] conns = connectionsRef(rm);
				RendererMaterialConnection c = conns != null ? conns.FirstOrDefault(x => x != null && x.renderer != null) : null;
				Material baseMat = c == null ? null : c.originalMaterial != null ? c.originalMaterial : c.renderer.sharedMaterial;
				Material r1 = r.items[1].obj as Material, r2 = r.items[2].obj as Material;
				if (baseMat == null || r1 == null || r2 == null) { failed.Add(animal); Log("No materials to make the " + AnimalNames[animal] + " looks from"); return null; }
				string prop = AlbedoProperty(baseMat, r1);
				Texture tb = baseMat.GetTexture(prop), t1 = r1.GetTexture(prop), t2 = r2.GetTexture(prop);
				if (tb == null || t1 == null || t2 == null) { failed.Add(animal); Log("No textures to make the " + AnimalNames[animal] + " looks from (" + prop + ")"); return null; }
				float started = Time.realtimeSinceStartup;
				int size = Mathf.Min(1024, tb.width);
				Color[] B = Read(tb, size), R1 = Read(t1, size), R2 = Read(t2, size);
				looks = new Material[3];
				for (int k = 0; k < 3; k++)
				{
					Color[] px = Recolour(animal, k + 3, B, R1, R2, size);
					var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "CI_" + AnimalNames[animal] + "_" + LookNames[animal][k + 3], wrapMode = tb.wrapMode, filterMode = tb.filterMode, anisoLevel = Mathf.Max(1, tb.anisoLevel) };
					tex.SetPixels(px);
					tex.Apply(true, false);
					tex.Compress(true);
					tex.Apply(false, true);
					var m = new Material(baseMat) { name = AnimalNames[animal] + "Material_CI_" + LookNames[animal][k + 3] };
					m.SetTexture(prop, tex);
					looks[k] = m;
				}
				made[animal] = looks;
				Log("Made the " + AnimalNames[animal] + "'s three new looks (" + prop + ", " + size + " px) in " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("F0", CultureInfo.InvariantCulture) + " ms");
				return looks;
			}
			catch (Exception e)
			{
				failed.Add(animal);
				Debug.LogWarning("[CUSTOM ISLANDS] [animal looks] Making the " + AnimalNames[animal] + " looks: " + e);
				return null;
			}
		}

		/// <summary>The texture Raft's rare material swaps (its albedo): the one that differs from the normal material's.</summary>
		static string AlbedoProperty(Material baseMat, Material rare)
		{
			foreach (string p in baseMat.GetTexturePropertyNames())
			{
				Texture a = baseMat.GetTexture(p);
				if (a != null && rare.HasProperty(p) && rare.GetTexture(p) != null && rare.GetTexture(p) != a) return p;
			}
			return "_MainTex";
		}

		/// <summary>A copy of a texture's colours (Raft's can't be read: drawn on the graphics card and read back), sRGB as painted.</summary>
		static Color[] Read(Texture src, int size)
		{
			RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture prev = RenderTexture.active;
			Texture2D t = null;
			try
			{
				Graphics.Blit(src, rt);
				RenderTexture.active = rt;
				t = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
				t.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
				t.Apply(false);
				return t.GetPixels();
			}
			finally
			{
				RenderTexture.active = prev;
				RenderTexture.ReleaseTemporary(rt);
				if (t != null) UnityEngine.Object.Destroy(t);
			}
		}

		#endregion

		#region The recolouring (as the prototype, looks.py)

		static float Lum(Color c) { return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f; }
		static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
		static float Diff(Color a, Color b) { return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b); }

		static float Hash01(long ix, long iy, long salt)
		{
			long h = (ix * 374761393L + iy * 668265263L + salt * 2246822519L) & 0xFFFFFFFFL;
			h = ((h ^ (h >> 13)) * 1274126177L) & 0xFFFFFFFFL;
			h ^= h >> 16;
			return (h & 0xFFFF) / 65535f;
		}

		/// <summary>Smooth value noise (hashed corners, smoothstep between): the speckles' clusters.</summary>
		static float VNoise(float x, float y, long salt)
		{
			long ix = (long)Math.Floor(x), iy = (long)Math.Floor(y);
			float fx = x - ix, fy = y - iy;
			fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
			float a = Hash01(ix, iy, salt), b = Hash01(ix + 1, iy, salt), c = Hash01(ix, iy + 1, salt), d = Hash01(ix + 1, iy + 1, salt);
			return (a * (1f - fx) + b * fx) * (1f - fy) + (c * (1f - fx) + d * fx) * fy;
		}

		/// <summary>Irregular soft speckles, in the picture's own rows (top first, as the prototype), only where mask:
		/// two sizes of slanted ovals, kept or dropped by a cluster field, each of its own strength.</summary>
		static float[] Speckles(int size, long salt, float[] mask)
		{
			var o = new float[size * size];
			float sc = size / 1024f;
			float[] cells = { 14f * sc, 28f * sc }, radii = { 2.4f * sc, 4.6f * sc };
			long[] salts = { 0, 10 };
			for (int i = 0; i < o.Length; i++)
			{
				if (mask[i] <= 0.001f) continue;
				int x = i % size, y = size - 1 - i / size;
				float best = 0f;
				for (int layer = 0; layer < 2; layer++)
				{
					float cell = cells[layer], radius = radii[layer];
					long s = salt + salts[layer];
					long gx = (long)Math.Floor(x / cell), gy = (long)Math.Floor(y / cell);
					for (long oy = -1; oy <= 1; oy++)
						for (long ox = -1; ox <= 1; ox++)
						{
							long cx = gx + ox, cy = gy + oy;
							float px = (cx + 0.1f + 0.8f * Hash01(cx, cy, s)) * cell, py = (cy + 0.1f + 0.8f * Hash01(cx, cy, s + 1)) * cell;
							float keepAt = 0.15f + 0.85f * Smooth(0.3f, 0.7f, VNoise(px / (150f * sc), py / (150f * sc), salt + 7));
							if (Hash01(cx, cy, s + 5) >= keepAt) continue;
							float r = radius * (0.55f + 0.9f * Hash01(cx, cy, s + 2));
							float ang = Hash01(cx, cy, s + 3) * Mathf.PI, asp = 1f + 0.9f * Hash01(cx, cy, s + 4);
							float strength = 0.55f + 0.45f * Hash01(cx, cy, s + 6);
							float dx = x - px, dy = y - py, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
							float u = (dx * cs + dy * sn) / r, v = (-dx * sn + dy * cs) * asp / r;
							float k = (1f - Smooth(0.4f, 1f, Mathf.Sqrt(u * u + v * v))) * strength;
							if (k > best) best = k;
						}
				}
				o[i] = best;
			}
			return o;
		}

		static float MeanLum(Color[] src, float[] mask)
		{
			double sum = 0; int n = 0;
			for (int i = 0; i < src.Length; i++) if (mask[i] > 0.5f) { sum += Lum(src[i]); n++; }
			return n > 0 ? (float)(sum / n) : 0.5f;
		}

		/// <summary>The colour shaded by the source's own light and dark where mask (its painted detail kept).</summary>
		static Color[] Paint(Color[] src, float[] mask, Color colour, float keep)
		{
			double sum = 0; int n = 0;
			for (int i = 0; i < src.Length; i++) if (mask[i] > 0.5f) { sum += Lum(src[i]); n++; }
			float mean = n > 0 ? Mathf.Max((float)(sum / n), 0.05f) : 0.5f;
			var o = new Color[src.Length];
			for (int i = 0; i < src.Length; i++)
			{
				Color s = src[i];
				float m = mask[i];
				if (m <= 0f) { o[i] = s; continue; }
				float ratio = Mathf.Clamp(1f + (Lum(s) / mean - 1f) * keep, 0.45f, 1.4f);
				o[i] = new Color(
					Mathf.Lerp(s.r, Mathf.Clamp01(colour.r * ratio), m),
					Mathf.Lerp(s.g, Mathf.Clamp01(colour.g * ratio), m),
					Mathf.Lerp(s.b, Mathf.Clamp01(colour.b * ratio), m), s.a);
			}
			return o;
		}

		/// <summary>Gold sheen: towards a warm light gold where the source itself is light (its own value), no dots.</summary>
		static Color[] Sheen(Color[] src, Color[] img, float[] mask)
		{
			float mean = MeanLum(src, mask);
			for (int i = 0; i < img.Length; i++)
			{
				float k = Smooth(mean - 0.02f, mean + 0.22f, Lum(src[i])) * mask[i] * 0.45f;
				if (k <= 0f) continue;
				Color c = img[i];
				img[i] = new Color(Mathf.Lerp(c.r, Mathf.Max(c.r, 0.88f), k), Mathf.Lerp(c.g, Mathf.Max(c.g, 0.73f), k), Mathf.Lerp(c.b, Mathf.Max(c.b, 0.42f), k), c.a);
			}
			return img;
		}

		static float[] Mask(int n, Func<int, float> f) { var m = new float[n]; for (int i = 0; i < n; i++) m[i] = f(i); return m; }

		/// <summary>Look 3, 4 or 5 of an animal from Raft's normal (B) and rare (R1, R2) textures.</summary>
		internal static Color[] Recolour(int animal, int look, Color[] B, Color[] R1, Color[] R2, int size)
		{
			int n = B.Length;
			if (animal == 0)
			{
				// (where Raft painted the red clucker: the feathers)
				float[] changed = Mask(n, i => Smooth(0.18f, 0.4f, Diff(R2[i], B[i])));
				if (look == 3) return Paint(B, changed, new Color(0.33f, 0.47f, 0.31f), 0.85f);
				if (look == 4)
				{
					// (slate feathers; speckles more on the light feather tips, few round the eye)
					Color[] o = Paint(B, changed, new Color(0.34f, 0.335f, 0.32f), 0.85f);
					float[] d = Speckles(size, 41, changed);
					float mean = MeanLum(B, changed);
					var dot = new Color(0.86f, 0.84f, 0.78f);
					for (int i = 0; i < n; i++)
					{
						if (d[i] <= 0f) continue;
						float fx = (i % size) / (float)size - 0.35f, fy = (size - 1 - i / size) / (float)size - 0.1f;
						float head = Smooth(0.1f, 0.24f, Mathf.Sqrt(fx * fx + fy * fy));
						float light = 0.55f + 0.45f * Smooth(mean - 0.06f, mean + 0.1f, Lum(B[i]));
						float k = d[i] * changed[i] * light * (0.25f + 0.75f * head);
						o[i] = new Color(Mathf.Lerp(o[i].r, dot.r, k), Mathf.Lerp(o[i].g, dot.g, k), Mathf.Lerp(o[i].b, dot.b, k), o[i].a);
					}
					return o;
				}
				return Sheen(B, Paint(B, changed, new Color(0.72f, 0.54f, 0.24f), 0.85f), changed);
			}
			if (animal == 1)
			{
				// (what Raft's grey goat recoloured, its white markings, and the dark patches of Raft's white goat)
				float[] coat = Mask(n, i => Smooth(0.08f, 0.16f, Lum(R1[i])) * Smooth(0.12f, 0.3f, Diff(R1[i], B[i])));
				if (look == 3)
				{
					float[] tan = Mask(n, i => coat[i] * (1f - Smooth(0.55f, 0.7f, Lum(R1[i])) * coat[i]));
					return Paint(R1, tan, new Color(0.52f, 0.38f, 0.25f), 0.85f);
				}
				if (look == 4)
				{
					float[] dark2 = Mask(n, i => (1f - Smooth(0.28f, 0.42f, Lum(R2[i]))) * Smooth(0.15f, 0.3f, Diff(R2[i], B[i])));
					return Paint(R2, dark2, new Color(0.48f, 0.25f, 0.14f), 0.8f);
				}
				return Sheen(R1, Paint(R1, coat, new Color(0.64f, 0.50f, 0.28f), 0.8f), coat);
			}
			// Llama: all the fur (not eyes, nose, hooves), and the rust patches of Raft's patched llama (its cream has r-g ~0.1)
			float[] fur = Mask(n, i => Smooth(0.08f, 0.16f, Lum(B[i])));
			if (look == 3)
			{
				float[] patch = Mask(n, i => Smooth(0.15f, 0.21f, R1[i].r - R1[i].g) * fur[i]);
				return Paint(R1, patch, new Color(0.22f, 0.20f, 0.19f), 0.8f);
			}
			if (look == 4) return Paint(B, fur, new Color(0.84f, 0.78f, 0.66f), 0.8f);
			return Sheen(B, Paint(B, fur, new Color(0.66f, 0.50f, 0.27f), 0.8f), fur);
		}

		#endregion

		[ConsoleCommand(name: "AnimalLooks", docs: "Rare animal looks: AnimalLooks = this world's and the looks tamed; AnimalLooks Normal|More|Lots = how often cluckers, goats and llamas have a rare look (host), or at the main menu for the next new world")]
		public static void AnimalLooksCommand(string[] args)
		{
			string arg = args != null ? string.Join(" ", args).Trim() : "";
			bool inGame = LoadSceneManager.IsGameSceneLoaded;
			if (arg.Length > 0)
			{
				int l = Array.FindIndex(Names, x => x.Equals(arg, StringComparison.OrdinalIgnoreCase));
				if (l < 0 && (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out l) || l < Normal || l > Lots)) { DynamicIslands.Notify("AnimalLooks: use Normal, More or Lots", true); return; }
				if (!inGame)
				{
					NewWorldRulesBox.LooksLevel = l;
					SaveDefault(l);
					DynamicIslands.Notify("Rare animal looks in the next new world: " + Names[l]);
					return;
				}
				if (!Raft_Network.IsHost) { DynamicIslands.Notify("Only the host can change the rare animal looks", true); return; }
				Set(l);
				DynamicIslands.Notify("Rare animal looks in this world: " + Names[Current] + " (for animals that come from now on)");
				return;
			}
			if (!inGame) { Log("Rare animal looks in the next new world: " + Names[NewWorldRulesBox.LooksLevel]); return; }
			Log("Rare animal looks in this world: " + Names[Current]);
			for (int a = 0; a < Animals.Length; a++)
				Log("  " + AnimalNames[a] + " " + CountOf(a) + "/6: " + string.Join(", ", Enumerable.Range(0, Looks).Select(k => (Has(a, k) ? "" : "(") + LookNames[a][k] + (Has(a, k) ? "" : ")")).ToArray()));
		}
	}

	/// <summary>An animal starts: its new looks on its randomizer before the host rolls one (Raft's Start).</summary>
	[HarmonyPatch(typeof(AI_NetworkBehaviour_Domestic), "Start")]
	static class AnimalLooksStart
	{
		static void Prefix(AI_NetworkBehaviour_Domestic __instance)
		{
			try { AnimalLooks.Ensure(__instance.rareMaterialScript, __instance.behaviourType); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [animal looks] " + e); }
		}
	}

	/// <summary>A look put on by its number (a loaded save, the host's message to a player): the new looks there first.</summary>
	[HarmonyPatch(typeof(RareMaterial), "AssignSpecificMaterial", new[] { typeof(uint) })]
	static class AnimalLooksAssign
	{
		static void Prefix(RareMaterial __instance, uint randomizerIndex)
		{
			if (randomizerIndex < 3) return;
			try
			{
				AI_NetworkBehaviour a = __instance.GetComponentInParent<AI_NetworkBehaviour>();
				AnimalLooks.Ensure(__instance, a != null ? a.behaviourType : (AI_NetworkBehaviourType)(-1));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [animal looks] " + e); }
		}
	}

	/// <summary>An animal tamed: its look counts for the collection (host).</summary>
	[HarmonyPatch(typeof(AI_NetworkBehaviour_Domestic), "Captured")]
	static class AnimalLooksCaptured
	{
		static void Postfix(AI_NetworkBehaviour_Domestic __instance)
		{
			try { AnimalLooks.OnCaptured(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [animal looks] " + e); }
		}
	}
}

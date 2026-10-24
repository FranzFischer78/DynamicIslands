using System;
using System.Collections;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>The terrain brushes of ROADMAP E6: brush falloff, the noise and erosion brushes, the sea floor switch and a mixed second style.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CITerrainBrushes", docs: "Dev, editor: the terrain brushes of E6 on a generated test island - the brush edge (smooth, linear, hard) shapes a Raise stroke, the noise brush adds bumps and Shift takes them away, the erosion brush wears a spike down without losing ground, the sea floor goes deep and shallow again keeping the land, a second style mixed in is painted and saved - and undo restores the heights every time")]
		public static void TerrainBrushesCommand()
		{
			StartTest(TerrainBrushesRoutine());
		}

		/// <summary>Largest difference (metres) between two heightmaps (normalised, terrain height sizeY), inside a block or everywhere.</summary>
		static float MaxHeightDiff(float[,] a, float[,] b, float sizeY, RectInt? area = null)
		{
			RectInt r = area ?? new RectInt(0, 0, a.GetLength(1), a.GetLength(0));
			float max = 0f;
			for (int z = Mathf.Max(0, r.y); z < Mathf.Min(a.GetLength(0), r.yMax); z++)
				for (int x = Mathf.Max(0, r.x); x < Mathf.Min(a.GetLength(1), r.xMax); x++)
					max = Mathf.Max(max, Mathf.Abs(a[z, x] - b[z, x]));
			return max * sizeY;
		}

		static IEnumerator TerrainBrushesRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			bool ok = true;
			EditorUI.SetTab(TAB.TerrainEdit);
			terraineditor editor = UnityEngine.Object.FindObjectOfType<terraineditor>();
			// A test island on a shallow sea floor, round and in the middle of the build area
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 4242, Radius = 70f, Height = 40f, SeaFloor = IslandGenSettings.SeaFloorShallow, Trees = 0.3f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f });
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Terrain terrain = terraineditor.terrain;
			TerrainData data = terrain.terrainData;
			int res = data.heightmapResolution;
			float spacing = data.size.x / (res - 1), sizeY = data.size.y;
			Vector3 o = terrain.transform.position;
			Func<float[,]> heights = () => data.GetHeights(0, 0, res, res);
			Func<Vector3, float> ground = p => terrain.SampleHeight(p);
			Func<Vector3, float, RectInt> around = (p, r) =>
			{
				int cx = Mathf.RoundToInt((p.x - o.x) / spacing), cz = Mathf.RoundToInt((p.z - o.z) / spacing), n = Mathf.CeilToInt(r / spacing);
				return new RectInt(cx - n, cz - n, n * 2 + 1, n * 2 + 1);
			};

			float radius = terraineditor.brushRadius, strength = terraineditor.strength, noiseScale = TerrainBrushes.NoiseScale, talus = TerrainBrushes.TalusAngle;
			TerrainBrushes.Falloff falloff = TerrainBrushes.BrushFalloff;
			terraineditor.TerrainModificationAction action = terraineditor.modificationAction;
			int paintLayer = terraineditor.paintLayer;

			// 1. The brush edge: weights in the middle, half way out and at the rim
			Func<float, TerrainBrushes.Falloff, float> wt = (d, f) => TerrainBrushes.Weight(d * d, f);
			Check(ref ok, Mathf.Abs(wt(0.5f, TerrainBrushes.Falloff.Smooth) - 0.5625f) < 0.001f && Mathf.Abs(wt(0.5f, TerrainBrushes.Falloff.Linear) - 0.5f) < 0.001f && wt(0.5f, TerrainBrushes.Falloff.Hard) > 0.999f
				&& new[] { TerrainBrushes.Falloff.Smooth, TerrainBrushes.Falloff.Linear, TerrainBrushes.Falloff.Hard }.All(f => wt(0f, f) > 0.999f && wt(1f, f) == 0f),
				"brush edge half way out: smooth " + wt(0.5f, TerrainBrushes.Falloff.Smooth).ToString("F3") + ", linear " + wt(0.5f, TerrainBrushes.Falloff.Linear).ToString("F3") + ", hard " + wt(0.5f, TerrainBrushes.Falloff.Hard).ToString("F3") + "; 1 in the middle, 0 at the rim");

			// 2. A Raise stroke on the flat sea floor with each edge: the ground 0.7 of the way out rises as much as the edge says
			terraineditor.brushRadius = 20f; terraineditor.strength = 10f;
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Raise;
			Vector3 flat = o + new Vector3(150f, 0f, 150f);
			float[] expected = { 0.2601f, 0.3f, 1f };
			foreach (TerrainBrushes.Falloff f in new[] { TerrainBrushes.Falloff.Smooth, TerrainBrushes.Falloff.Linear, TerrainBrushes.Falloff.Hard })
			{
				TerrainBrushes.BrushFalloff = f;
				float[,] before = heights();
				float g0 = ground(flat), g7 = ground(flat + new Vector3(14f, 0f, 0f)), gOut = ground(flat + new Vector3(22f, 0f, 0f));
				editor.SimulateStroke(flat, 10, 0.05f);
				float c = ground(flat) - g0, at7 = ground(flat + new Vector3(14f, 0f, 0f)) - g7, outside = ground(flat + new Vector3(22f, 0f, 0f)) - gOut;
				float ratio = c > 0.01f ? at7 / c : 0f;
				Check(ref ok, c > 4f && Mathf.Abs(ratio - expected[(int)f]) < 0.08f && Mathf.Abs(outside) < 0.01f,
					f + " edge: +" + c.ToString("F2") + " m in the middle, " + (ratio * 100f).ToString("F0") + "% of it 0.7 of the way out (expected " + (expected[(int)f] * 100f).ToString("F0") + "%), " + outside.ToString("F3") + " m past the rim");
				CommandUndoRedo.UndoRedoManager.Undo();
				float back = MaxHeightDiff(heights(), before, sizeY);
				Check(ref ok, back < 0.005f, f + " stroke undone: the heights as before (" + back.ToString("F4") + " m off at most)");
			}
			TerrainBrushes.BrushFalloff = TerrainBrushes.Falloff.Smooth;

			// 3. Noise on the island's land: bumps both ways under the brush, nothing past it; Shift takes them away; undo
			Vector3 land = o + new Vector3(500f, 0f, 500f);
			Check(ref ok, ground(land) + o.y > DynamicIslands.EditorWaterLevel + 5f, "the test island has land in the middle (" + (ground(land) + o.y - DynamicIslands.EditorWaterLevel).ToString("F1") + " m above the sea)");
			terraineditor.brushRadius = 25f; terraineditor.strength = 6f; TerrainBrushes.NoiseScale = 8f;
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Noise;
			{
				float[,] before = heights();
				RectInt inner = around(land, 20f), outer = around(land, 40f);
				editor.SimulateStroke(land, 20, 0.05f);
				float[,] noisy = heights();
				int up = 0, down = 0;
				for (int z = inner.y; z < inner.yMax; z++)
					for (int x = inner.x; x < inner.xMax; x++)
					{
						float d = (noisy[z, x] - before[z, x]) * sizeY;
						if (d > 0.05f) up++; else if (d < -0.05f) down++;
					}
				// (past the rim: the ring between 27 and 40 m out)
				float past = 0f;
				int cx = Mathf.RoundToInt((land.x - o.x) / spacing), cz = Mathf.RoundToInt((land.z - o.z) / spacing);
				for (int z = outer.y; z < outer.yMax; z++)
					for (int x = outer.x; x < outer.xMax; x++)
						if (new Vector2(x - cx, z - cz).magnitude * spacing > 27f) past = Mathf.Max(past, Mathf.Abs(noisy[z, x] - before[z, x]) * sizeY);
				Check(ref ok, up > 20 && down > 20 && past < 0.001f, "noise: " + up + " samples raised and " + down + " lowered by over 5 cm under the brush, " + past.ToString("F4") + " m change past its rim");
				terraineditor.SubtractNoise = true;
				editor.SimulateStroke(land, 20, 0.05f);
				terraineditor.SubtractNoise = false;
				float[,] smoothed = heights();
				float sum = 0f, max = 0f; int n = 0;
				for (int z = inner.y; z < inner.yMax; z++)
					for (int x = inner.x; x < inner.xMax; x++) { float d = Mathf.Abs(smoothed[z, x] - before[z, x]) * sizeY; sum += d; max = Mathf.Max(max, d); n++; }
				Check(ref ok, sum / n < 0.05f && max < 0.4f, "Shift takes the same noise away: " + (sum / n).ToString("F3") + " m from the old ground on average (" + max.ToString("F2") + " m at most)");
				CommandUndoRedo.UndoRedoManager.Undo();
				CommandUndoRedo.UndoRedoManager.Undo();
				float back = MaxHeightDiff(heights(), before, sizeY);
				Check(ref ok, back < 0.005f, "both noise strokes undone: the heights as before (" + back.ToString("F4") + " m off at most)");
			}

			// 4. Erosion: a 20 m spike (hard edge) worn down - the steepest slope falls, the ground is only moved, undo
			{
				float[,] before = heights();
				terraineditor.brushRadius = 6f; terraineditor.strength = 20f;
				TerrainBrushes.BrushFalloff = TerrainBrushes.Falloff.Hard;
				terraineditor.modificationAction = terraineditor.TerrainModificationAction.Raise;
				editor.SimulateStroke(land, 20, 0.05f);
				float[,] spiked = heights();
				RectInt area = around(land, 26f);
				Func<float[,], float> steepest = h =>
				{
					float m = 0f;
					for (int z = area.y; z < area.yMax - 1; z++)
						for (int x = area.x; x < area.xMax - 1; x++)
							m = Mathf.Max(m, Mathf.Max(Mathf.Abs(h[z, x + 1] - h[z, x]), Mathf.Abs(h[z + 1, x] - h[z, x])) * sizeY);
					return Mathf.Atan(m / spacing) * Mathf.Rad2Deg;
				};
				Func<float[,], double> volume = h =>
				{
					double v = 0;
					for (int z = area.y; z < area.yMax; z++) for (int x = area.x; x < area.xMax; x++) v += h[z, x] * sizeY;
					return v * spacing * spacing;
				};
				float slopeBefore = steepest(spiked), topBefore = ground(land);
				double volBefore = volume(spiked), added = volBefore - volume(before);
				terraineditor.brushRadius = 18f; TerrainBrushes.BrushFalloff = TerrainBrushes.Falloff.Smooth; TerrainBrushes.TalusAngle = 32f;
				terraineditor.modificationAction = terraineditor.TerrainModificationAction.Erode;
				editor.SimulateStroke(land, 60, 0.05f);
				float[,] eroded = heights();
				float slopeAfter = steepest(eroded), drop = topBefore - ground(land);
				double volAfter = volume(eroded), lost = Math.Abs(volAfter - volBefore);
				Check(ref ok, slopeAfter < slopeBefore - 10f && drop > 3f && lost < added * 0.03 + 50.0,
					"erosion: the steepest slope " + slopeBefore.ToString("F0") + "° -> " + slopeAfter.ToString("F0") + "°, the spike " + drop.ToString("F1") + " m lower, " + lost.ToString("F0") + " m³ of the " + added.ToString("F0") + " m³ spike gained or lost (moved, not removed)");
				CommandUndoRedo.UndoRedoManager.Undo();
				float backSpike = MaxHeightDiff(heights(), spiked, sizeY);
				CommandUndoRedo.UndoRedoManager.Undo();
				float back = MaxHeightDiff(heights(), before, sizeY);
				Check(ref ok, backSpike < 0.005f && back < 0.005f, "erosion undone (" + backSpike.ToString("F4") + " m off), then the spike (" + back.ToString("F4") + " m off)");
			}

			// 5. The sea floor deep and shallow again: the land keeps its height above the sea, the flat floor goes down; undo
			{
				float[,] before = heights();
				float sea = DynamicIslands.EditorWaterLevel;
				Check(ref ok, Mathf.Abs(sea - IslandFile.DefaultWaterLevel) < 0.01f && !TerrainBrushes.EditorDeep, "the test island is on a shallow sea floor (sea " + sea.ToString("F0") + " m above the terrain's base)");
				Vector3 corner = o + new Vector3(10f, 0f, 10f);
				// (the first flat floor east of the middle, and 60 m further out)
				int cz = Mathf.RoundToInt((land.z - o.z) / spacing), fx = Mathf.RoundToInt((land.x - o.x) / spacing);
				while (fx < res - 1 && before[cz, fx] * sizeY > TerrainBrushes.FloorEpsilon) fx++;
				Vector3 floorNear = o + new Vector3(fx * spacing, 0f, cz * spacing), floorFar = floorNear + new Vector3(60f, 0f, 0f);
				float landAbove = ground(land) - sea;
				Transform obj = GameObject.Find("PlacedObjects").GetComponentsInChildren<EditorGameObject>(false).Select(e => e.transform)
					.FirstOrDefault(t => Mathf.Abs(t.position.y - (ground(t.position) + o.y)) < 0.5f && ground(t.position) + o.y > sea + 1f && (t.parent == null || t.parent.GetComponentInParent<EditorGameObject>() == null));
				float objY = obj != null ? obj.position.y : 0f;

				string problem = TerrainBrushes.SwitchSeaFloor(true);
				float deep = DynamicIslands.EditorWaterLevel;
				float landAfter = ground(land) - deep, nearDepth = deep - ground(floorNear), farDepth = deep - ground(floorFar), cornerDepth = deep - ground(corner);
				Check(ref ok, problem == null && Mathf.Abs(deep - IslandFile.DeepWaterLevel) < 0.01f && TerrainBrushes.EditorDeep && Mathf.Abs(landAfter - landAbove) < 0.05f,
					"Deep: the sea " + deep.ToString("F0") + " m above the base (" + (problem ?? "done") + "), the middle of the island still " + landAfter.ToString("F2") + " m above it (was " + landAbove.ToString("F2") + ")");
				Check(ref ok, cornerDepth > IslandFile.DeepWaterLevel - 0.5f && nearDepth < 25f && farDepth > nearDepth + 5f && farDepth < IslandFile.DeepWaterLevel,
					"the floor: " + cornerDepth.ToString("F0") + " m deep far out, " + nearDepth.ToString("F1") + " m next to the island's slope, " + farDepth.ToString("F0") + " m 60 m further out (falling away, no wall)");
				if (obj != null) Check(ref ok, Mathf.Abs(obj.position.y - objY - (deep - sea)) < 0.05f, "an object on the land went up with it: " + (obj.position.y - objY).ToString("F2") + " m (" + obj.name + ")");
				else Log("(no object on the land to follow)");

				CommandUndoRedo.UndoRedoManager.Undo();
				float back = MaxHeightDiff(heights(), before, sizeY);
				Check(ref ok, Mathf.Abs(DynamicIslands.EditorWaterLevel - sea) < 0.01f && back < 0.005f && (obj == null || Mathf.Abs(obj.position.y - objY) < 0.01f),
					"undo: the sea back at " + DynamicIslands.EditorWaterLevel.ToString("F0") + " m, the heights as before (" + back.ToString("F4") + " m off), the object back");

				CommandUndoRedo.UndoRedoManager.Redo();
				problem = TerrainBrushes.SwitchSeaFloor(false);
				float[,] again = heights();
				float landOff = 0f;
				for (int z = 0; z < res; z++)
					for (int x = 0; x < res; x++)
						if (before[z, x] * sizeY > 0.5f) landOff = Mathf.Max(landOff, Mathf.Abs(again[z, x] - before[z, x]) * sizeY);
				Check(ref ok, problem == null && Mathf.Abs(DynamicIslands.EditorWaterLevel - sea) < 0.01f && landOff < 0.05f && Mathf.Abs(ground(corner)) < 0.05f,
					"Shallow again: sea " + DynamicIslands.EditorWaterLevel.ToString("F0") + " m, everything above the old floor where it was (" + landOff.ToString("F3") + " m off at most), the floor at the base");
				CommandUndoRedo.UndoRedoManager.Undo();
				CommandUndoRedo.UndoRedoManager.Undo();
				back = MaxHeightDiff(heights(), before, sizeY);
				Check(ref ok, Mathf.Abs(DynamicIslands.EditorWaterLevel - sea) < 0.01f && back < 0.005f, "both switches undone (" + back.ToString("F4") + " m off)");
			}

			// 6. A second style mixed in: eight layers, painted with, saved and opened again; an older version's view; undo
			{
				int snowy = TerrainPainter.StyleIndex("Snowy");
				TerrainBrushes.SetEditorMix(snowy);
				Check(ref ok, DynamicIslands.currentMixStyle == snowy && data.alphamapLayers == TerrainPainter.MixLayerCount && data.terrainLayers.Length == TerrainPainter.MixLayerCount,
					"Snowy mixed in: " + data.alphamapLayers + " texture layers");
				int ares = data.alphamapResolution;
				int px = Mathf.Clamp((int)((land.x - o.x) / data.size.x * ares), 0, ares - 1), pz = Mathf.Clamp((int)((land.z - o.z) / data.size.z * ares), 0, ares - 1);
				int layer = TerrainPainter.LayerCount + TerrainPainter.Grass;
				terraineditor.paintLayer = layer;
				terraineditor.modificationAction = terraineditor.TerrainModificationAction.PaintLayer;
				terraineditor.brushRadius = 10f; terraineditor.strength = 20f; TerrainBrushes.BrushFalloff = TerrainBrushes.Falloff.Hard;
				editor.SimulateStroke(land, 30, 0.05f);
				float[,,] px1 = data.GetAlphamaps(px, pz, 1, 1);
				Check(ref ok, px1[0, 0, layer] > 0.8f, "the mixed style's snow painted: layer " + (layer + 1) + " weighs " + px1[0, 0, layer].ToString("F2") + " in the middle");

				string path = Path.Combine(Path.GetTempPath(), "ci_terrainmix.island"), path4 = Path.Combine(Path.GetTempPath(), "ci_terrainmix4.island");
				IslandFile saved = DynamicIslands.CaptureIsland("ci_terrainmix");
				saved.Save(path);
				IslandFile loaded = IslandFile.Load(path);
				int n = loaded.AlphamapResolution * loaded.AlphamapResolution, p = pz * loaded.AlphamapResolution + px;
				float w6 = loaded.AlphamapLayers > layer ? loaded.Alphamaps[layer * n + p] / 255f : 0f;
				Check(ref ok, loaded.MixStyle == "Snowy" && loaded.HasMix && Mathf.Abs(w6 - px1[0, 0, layer]) < 0.02f && !loaded.Tail.ContainsKey(IslandFile.MixTag),
					"saved and opened: mixed style '" + loaded.MixStyle + "', " + loaded.AlphamapLayers + " layers, layer " + (layer + 1) + " " + w6.ToString("F2") + " (the tail's block read, not kept as unknown)");
				// (what a version without mixed styles sees: the four layers of the paint block, the snow on the first style's grass)
				loaded.MixStyle = "";
				loaded.Save(path4);
				IslandFile older = IslandFile.Load(path4);
				float grass = older.AlphamapLayers == TerrainPainter.LayerCount ? older.Alphamaps[TerrainPainter.Grass * n + p] / 255f : 0f;
				Check(ref ok, older.AlphamapLayers == TerrainPainter.LayerCount && grass > 0.8f, "the paint block alone (an older version): " + older.AlphamapLayers + " layers, the snow shown as the first style's grass (" + grass.ToString("F2") + ")");
				try { File.Delete(path); File.Delete(path4); } catch { }

				CommandUndoRedo.UndoRedoManager.Undo(); // (the paint)
				CommandUndoRedo.UndoRedoManager.Undo(); // (the mix)
				Check(ref ok, DynamicIslands.currentMixStyle == -1 && data.alphamapLayers == TerrainPainter.LayerCount, "undo: no second style, " + data.alphamapLayers + " layers again");
			}
			Screenshot(new[] { "terrain_brushes" });

			terraineditor.brushRadius = radius; terraineditor.strength = strength; TerrainBrushes.NoiseScale = noiseScale; TerrainBrushes.TalusAngle = talus;
			TerrainBrushes.BrushFalloff = falloff; terraineditor.modificationAction = action; terraineditor.paintLayer = paintLayer;
			EditorUI.RefreshSliders();
			if (ok) Log("PASS: terrain brushes"); else Fail("terrain brushes");
		}
	}
}

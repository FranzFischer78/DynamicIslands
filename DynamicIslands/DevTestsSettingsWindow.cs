using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>ROADMAP AU46: spawnpool.txt's settings set in a window (DefaultsWindow) instead of by hand.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CISettingsWindow", docs: "Dev, anywhere: ROADMAP AU46 - the Defaults window (spawnpool.txt's settings): opened, minSpacing and unloadDistance typed into its fields as a player does; spawnpool.txt has the two new values with every other line (notes, settings, island list) as it was, and the mod uses them; an unload distance below its range is kept within it (300); the New Game box's World settings has a Defaults... button; the file is put back as it was")]
		public static void SettingsWindowCommand() { StartTest(SettingsWindowRoutine()); }

		/// <summary>Whether a line of spawnpool.txt is this setting's ("key = value").</summary>
		static bool IsSettingLine(string line, string key)
		{
			string l = line.Trim();
			int eq = l.IndexOf('=');
			return !l.StartsWith("#") && eq > 0 && l.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
		}

		static IEnumerator SettingsWindowRoutine()
		{
			bool ok = true;
			var inv = CultureInfo.InvariantCulture;
			string pool = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
			string poolBefore = File.Exists(pool) ? File.ReadAllText(pool) : null;
			try
			{
				CustomIslandSpawner.LoadPool(true); // (makes the file if it isn't there)
				string[] before = File.ReadAllLines(pool);
				// Values unlike the ones there now
				float spacing = Mathf.Round(CustomIslandSpawner.MinSpacing) + 111f, unload = Mathf.Approximately(CustomIslandSpawner.UnloadDistance, 1234f) ? 1300f : 1234f;
				string spacingText = spacing.ToString("0", inv), unloadText = unload.ToString("0", inv);

				DefaultsWindow.Open();
				yield return null;
				InputField fs = DefaultsWindow.FieldFor("minSpacing"), fu = DefaultsWindow.FieldFor("unloadDistance");
				Check(ref ok, DefaultsWindow.IsOpen && fs != null && fu != null, "Defaults opens, with fields for minSpacing and unloadDistance");
				if (fs == null || fu == null) { Fail("settings window"); yield break; }
				Check(ref ok, CustomIslandSpawner.NumberKeys.All(k => k == "showOnReceiver" || DefaultsWindow.FieldFor(k) != null), "a field for every number setting of the file (" + CustomIslandSpawner.NumberKeys.Length + ", the Receiver's on/off a button)");
				Check(ref ok, fs.text == CustomIslandSpawner.FormatValue(CustomIslandSpawner.MinSpacing), "the field shows the file's value (" + fs.text + ")");
				yield return new WaitForSecondsRealtime(0.5f);
				Screenshot(new[] { "defaults_window" });
				yield return new WaitForSecondsRealtime(0.4f);

				// Two values typed, as a player does (each saved when its field is left)
				fs.text = spacingText; fs.onEndEdit.Invoke(spacingText);
				yield return null;
				fu.text = unloadText; fu.onEndEdit.Invoke(unloadText);
				yield return null;
				string[] after = File.ReadAllLines(pool);
				Check(ref ok, after.Count(l => IsSettingLine(l, "minSpacing")) >= 1 && after.Where(l => IsSettingLine(l, "minSpacing")).All(l => l.Trim() == "minSpacing = " + spacingText), "spawnpool.txt: minSpacing = " + spacingText);
				Check(ref ok, after.Count(l => IsSettingLine(l, "unloadDistance")) >= 1 && after.Where(l => IsSettingLine(l, "unloadDistance")).All(l => l.Trim() == "unloadDistance = " + unloadText), "spawnpool.txt: unloadDistance = " + unloadText);
				Func<string[], List<string>> others = lines => lines.Where(l => !IsSettingLine(l, "minSpacing") && !IsSettingLine(l, "unloadDistance")).ToList();
				List<string> was = others(before), now = others(after);
				Check(ref ok, was.SequenceEqual(now), "every other line as it was (" + now.Count + " lines: notes, settings, island list)" +
					(was.SequenceEqual(now) ? "" : " - first different: '" + was.Where((l, i) => i >= now.Count || now[i] != l).FirstOrDefault() + "'"));
				Check(ref ok, Mathf.Approximately(CustomIslandSpawner.MinSpacing, spacing) && Mathf.Approximately(CustomIslandSpawner.UnloadDistance, unload), "the mod uses them (spacing " + CustomIslandSpawner.MinSpacing + ", unload " + CustomIslandSpawner.UnloadDistance + ")");
				Check(ref ok, DefaultsWindow.StatusText.StartsWith("Saved: unloadDistance = " + unloadText), "the window says what was saved (" + DefaultsWindow.StatusText + ")");
				Check(ref ok, !File.Exists(pool + ".tmp") && !File.Exists(pool + ".part"), "written whole (no .tmp left)");

				// Below its range: kept within it, as when the file is read
				fu.text = "100"; fu.onEndEdit.Invoke("100");
				yield return null;
				Check(ref ok, File.ReadAllLines(pool).Any(l => l.Trim() == "unloadDistance = 300") && Mathf.Approximately(CustomIslandSpawner.UnloadDistance, 300f) && fu.text == "300" && DefaultsWindow.StatusText.Contains("kept within"),
					"unloadDistance 100 is kept at 300 (" + DefaultsWindow.StatusText + ")");
				// Not a number: nothing written
				string[] beforeBad = File.ReadAllLines(pool);
				bool refused = false;
				try { CustomIslandSpawner.SetPoolValues(new Dictionary<string, string> { { "minSpacing", "1000" }, { "unloadDistance", "lots" } }); }
				catch (ArgumentException) { refused = true; }
				Check(ref ok, refused && File.ReadAllLines(pool).SequenceEqual(beforeBad), "a bad value among good ones: refused, nothing written");
				DefaultsWindow.Close();
				yield return null;
				Check(ref ok, !DefaultsWindow.IsOpen, "Close closes it");

				// The New Game box's World settings opens it (main menu only)
				if (WorldSettingsWindow.Window != null)
					Check(ref ok, WorldSettingsWindow.Window.GetComponentsInChildren<Button>(true).Any(b => b.name == "Button_Defaults"), "World settings has a Defaults... button");
			}
			finally
			{
				DefaultsWindow.Close();
				try { if (poolBefore != null) File.WriteAllText(pool, poolBefore); else if (File.Exists(pool)) File.Delete(pool); CustomIslandSpawner.LoadPool(true); } catch { }
			}
			Check(ref ok, poolBefore == null || File.ReadAllText(pool) == poolBefore, "spawnpool.txt put back as it was");
			if (ok) Log("PASS: settings window"); else Fail("settings window");
		}
	}
}

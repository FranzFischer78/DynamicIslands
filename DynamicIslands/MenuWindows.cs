using System;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The editor's start window and the world plan editor on Raft's main menu (TODO 5b): a plan is made without the 3D
	/// editor loaded. The plan window and the small windows it opens (choices, names, Check, Progression, the main story
	/// helper, library export and import) are built once per main menu on a canvas of their own; the editor builds its own
	/// set on its canvas as before. Each set goes with its scene, and the windows' instances are always the current scene's.
	/// </summary>
	public static class MenuWindows
	{
		public const string CanvasName = "CustomIslandsMenuWindows";
		static Canvas canvas;

		/// <summary>The menu's canvas (null when not on the main menu).</summary>
		public static Transform Root { get { return canvas != null ? canvas.transform : null; } }

		/// <summary>The windows are the main menu's (not the editor's).</summary>
		public static bool OnMenu { get { return canvas != null && !DynamicIslands.InEditor(); } }

		/// <summary>Builds the menu's windows (the main menu was loaded; HookUI).</summary>
		public static void Ensure()
		{
			if (canvas != null) return;
			// (over Raft's menu and the New Game plan picker (875), under the island library (880), the info boxes (890) and ? popups (900))
			canvas = UIKit.CreateCanvas(CanvasName, 872);
			Transform t = canvas.transform;
			try
			{
				CreateWindow.Create(t);
				WorldPlanWindow.Create(t);
				MainStoryHelper.Create(t);
				PlanCheckWindow.Create(t);
				PlanProgressWindow.Create(t);
				LibraryExportWindow.Create(t);
				LibraryImportWindow.Create(t);
				TextPromptWindow.Create(t);
				ChoiceWindow.Create(t);
			}
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Could not build the editor's menu windows: " + e); }
		}
	}
}

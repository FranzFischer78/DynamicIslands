using System;
using System.Collections.Generic;
using System.Linq;
using CommandUndoRedo;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The island's own settings as undo steps: the name players see, author, description, regrow days, the level up switch,
	/// style, height in the world, the quest, island events, story items and the island's rules. Before, none of these were
	/// undo steps - and the editor's "unsaved changes" (the autosave, leaving by Main menu) count undo steps, so such edits
	/// weren't autosaved and were lost on leaving without a word. Each change is one step: a snapshot before and after.
	/// </summary>
	public static class IslandSettingsUndo
	{
		class Snapshot
		{
			public Dictionary<string, string> Props;
			public int Style;
			public float Elevation;

			public bool Same(Snapshot o)
			{
				return Style == o.Style && Elevation == o.Elevation && Props.Count == o.Props.Count &&
					Props.All(kv => { string v; return o.Props.TryGetValue(kv.Key, out v) && v == kv.Value; });
			}
		}

		static Snapshot Take()
		{
			return new Snapshot { Props = new Dictionary<string, string>(DynamicIslands.currentIslandProps), Style = DynamicIslands.currentStyle, Elevation = DynamicIslands.currentElevation };
		}

		static void Restore(Snapshot s)
		{
			DynamicIslands.currentIslandProps.Clear();
			foreach (var kv in s.Props) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
			if (DynamicIslands.currentStyle != s.Style) DynamicIslands.SetEditorStyle(s.Style);
			DynamicIslands.currentElevation = s.Elevation;
			EditorUI.RefreshIsland();
		}

		class Command : ICommand
		{
			readonly Snapshot before, after;
			public Command(Snapshot before, Snapshot after) { this.before = before; this.after = after; }
			public void Execute() { Restore(after); }
			public void UnExecute() { Restore(before); }
		}

		/// <summary>Makes a change to the island's settings as one undo step (nothing is recorded when nothing changed).</summary>
		public static void Change(Action change)
		{
			Snapshot before = Take();
			change();
			Snapshot after = Take();
			if (!before.Same(after)) UndoRedoManager.Insert(new Command(before, after));
		}

		/// <summary>The same, as a step to add to a bigger one (the generator's): null when nothing changed.</summary>
		public static ICommand Record(Action change)
		{
			Snapshot before = Take();
			change();
			Snapshot after = Take();
			return before.Same(after) ? null : new Command(before, after);
		}
	}
}

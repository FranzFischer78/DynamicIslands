using System;

namespace CommandUndoRedo
{
	public static class UndoRedoManager
	{
		static UndoRedo undoRedo = new UndoRedo();

		public static int maxUndoStored {get {return undoRedo.maxUndoStored;} set {undoRedo.maxUndoStored = value;}}

		public static int UndoCount { get { return undoRedo.UndoCount; } }
		public static int RedoCount { get { return undoRedo.RedoCount; } }

		/// <summary>Counts every change (done, undone, redone) - the editor autosaves when it moved (Custom Islands).</summary>
		public static int Changes { get; private set; }

		public static void Clear()
		{
			undoRedo.Clear();
		}

		public static void Undo()
		{
			int n = undoRedo.UndoCount; undoRedo.Undo(); if (undoRedo.UndoCount != n) Changes++;
		}

		public static void Redo()
		{
			int n = undoRedo.RedoCount; undoRedo.Redo(); if (undoRedo.RedoCount != n) Changes++;
		}

		public static void Insert(ICommand command)
		{
			undoRedo.Insert(command); Changes++;
		}

		public static void AppendToLast(ICommand command)
		{
			undoRedo.AppendToLast(command); Changes++;
		}

		public static void Execute(ICommand command)
		{
			undoRedo.Execute(command); Changes++;
		}
	}
}

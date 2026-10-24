using System;
using System.Collections.Generic;

namespace CommandUndoRedo
{
	public class UndoRedo
	{
		public int maxUndoStored {get {return undoCommands.maxLength;} set {SetMaxLength(value);}}

		DropoutStack<ICommand> undoCommands = new DropoutStack<ICommand>();
		DropoutStack<ICommand> redoCommands = new DropoutStack<ICommand>();

		/// <summary>Undo / redo steps stored (the keyboard test checks Ctrl+Z and Ctrl+Y).</summary>
		public int UndoCount { get { return undoCommands.Count; } }
		public int RedoCount { get { return redoCommands.Count; } }

		public UndoRedo() {}
		public UndoRedo(int maxUndoStored)
		{
			this.maxUndoStored = maxUndoStored;
		}

		public void Clear()
		{
			undoCommands.Clear();
			redoCommands.Clear();
		}

		public void Undo()
		{
			if(undoCommands.Count > 0)
			{
				ICommand command = undoCommands.Pop();
				command.UnExecute();
				redoCommands.Push(command);
			}
		}

		public void Redo()
		{
			if(redoCommands.Count > 0)
			{
				ICommand command = redoCommands.Pop();
				command.Execute();
				undoCommands.Push(command);
			}
		}

		public void Insert(ICommand command)
		{
			if(maxUndoStored <= 0) return;

			undoCommands.Push(command);
			redoCommands.Clear();
		}

		/// <summary>Adds a change to the last undo step (one Ctrl+Z undoes both); a step of its own when there is none.</summary>
		public void AppendToLast(ICommand command)
		{
			if(undoCommands.Count == 0) { Insert(command); return; }
			CommandGroup group = new CommandGroup();
			group.Add(undoCommands.Pop());
			group.Add(command);
			undoCommands.Push(group);
			redoCommands.Clear();
		}

		public void Execute(ICommand command)
		{
			command.Execute();
			Insert(command);
		}

		void SetMaxLength(int max)
		{
			undoCommands.maxLength = max;
			redoCommands.maxLength = max;
		}
	}
}

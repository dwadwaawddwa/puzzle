using System.Collections.Generic;

namespace PuzzleStudio.Studio.App
{
    /// <summary>
    /// Undo / redo over whole-project snapshots (game.json + project.json as text).
    /// The Studio commits a snapshot when edits pause, so typing a word or dragging a slider is one step.
    /// </summary>
    public sealed class UndoHistory
    {
        readonly List<string> _undo = new List<string>();
        readonly List<string> _redo = new List<string>();
        string _committed;

        public int Capacity { get; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int UndoCount => _undo.Count;

        public UndoHistory(int capacity = 100) { Capacity = capacity; }

        /// <summary>Forgets everything; <paramref name="state"/> is the starting point (project just opened).</summary>
        public void Reset(string state)
        {
            _undo.Clear();
            _redo.Clear();
            _committed = state;
        }

        /// <summary>Records the change from the last committed state to <paramref name="state"/> as one step.</summary>
        /// <returns>False when nothing changed.</returns>
        public bool Commit(string state)
        {
            if (state == _committed) return false;
            if (_committed != null)
            {
                _undo.Add(_committed);
                if (_undo.Count > Capacity) _undo.RemoveAt(0);
            }
            _redo.Clear();
            _committed = state;
            return true;
        }

        /// <summary>The state to restore, or null. Commit pending changes first.</summary>
        public string Undo()
        {
            if (_undo.Count == 0) return null;
            _redo.Add(_committed);
            _committed = Pop(_undo);
            return _committed;
        }

        public string Redo()
        {
            if (_redo.Count == 0) return null;
            _undo.Add(_committed);
            _committed = Pop(_redo);
            return _committed;
        }

        static string Pop(List<string> list)
        {
            string s = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return s;
        }
    }
}

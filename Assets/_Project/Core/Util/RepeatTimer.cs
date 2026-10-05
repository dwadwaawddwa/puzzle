namespace PuzzleStudio.Core.Util
{
    /// <summary>
    /// Auto-repeat for a held direction (D-pad, stick, arrow keys): fires on press, again after
    /// <see cref="FirstDelay"/>, then every <see cref="Interval"/>. A new direction fires at once.
    /// </summary>
    public struct RepeatTimer
    {
        public float FirstDelay;
        public float Interval;
        int _key;
        float _wait;

        public RepeatTimer(float firstDelay, float interval)
        {
            FirstDelay = firstDelay;
            Interval = interval;
            _key = 0;
            _wait = 0f;
        }

        /// <param name="key">What is held (any non-zero id per direction), 0 = nothing.</param>
        /// <returns>True when the action should run this frame.</returns>
        public bool Tick(int key, float dt)
        {
            if (key == 0) { _key = 0; return false; }
            if (key != _key)
            {
                _key = key;
                _wait = FirstDelay;
                return true;
            }
            _wait -= dt;
            if (_wait > 0f) return false;
            _wait = _wait < -Interval ? Interval : _wait + Interval;
            return true;
        }

        public void Reset() => _key = 0;
    }
}

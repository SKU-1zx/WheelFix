using System;
using System.Threading;

namespace WheelFix
{
    internal enum WheelFilterState { IDLE, LOCK_UP, LOCK_DOWN }

    internal struct WheelFilterResult
    {
        internal int OutputDelta;
        internal WheelFilterState State;
        internal uint? GapMs;
        internal bool ResetToIdle;
        internal string Reason;
    }

    // Owned by the hook/UI message-loop thread. No timers, replay or voting.
    internal sealed class WheelFilterCore
    {
        internal const int DefaultWindowMs = 450;
        private bool _enabled;
        private int _windowMs;
        private uint _lastEventTime;
        private long _blockedCount;

        public WheelFilterCore(bool enabled, int windowMs)
        {
            _enabled = enabled;
            _windowMs = ClampWindow(windowMs);
        }

        public WheelFilterState State { get; private set; }
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                ResetHistory();
            }
        }

        public int WindowMs
        {
            get { return _windowMs; }
            set
            {
                int clamped = ClampWindow(value);
                if (_windowMs == clamped) return;
                _windowMs = clamped;
                ResetHistory();
            }
        }

        // Kept for existing UI callers: counts suppressed contrary originals,
        // each of which is now replaced with a corrected pulse.
        public long BlockedCount { get { return Interlocked.Read(ref _blockedCount); } }
        public void ResetBlockedCount() { Interlocked.Exchange(ref _blockedCount, 0L); }

        public WheelFilterResult Process(int delta, uint timestamp)
        {
            WheelFilterResult result = new WheelFilterResult();
            result.OutputDelta = delta;
            result.State = State;
            if (!_enabled || delta == 0)
            {
                result.Reason = !_enabled ? "filter_paused" : "zero_delta";
                return result;
            }

            if (State != WheelFilterState.IDLE)
            {
                // Windows event timestamps share a wrapping 32-bit clock.
                uint gap = unchecked(timestamp - _lastEventTime);
                result.GapMs = gap;
                if (gap >= (uint)_windowMs)
                {
                    // Expiration is evaluated before the next physical event.
                    // No timer can race a queued event or extend the deadline.
                    ResetHistory();
                    result.ResetToIdle = true;
                }
            }

            bool newGesture = State == WheelFilterState.IDLE;
            if (newGesture)
                State = delta > 0 ? WheelFilterState.LOCK_UP : WheelFilterState.LOCK_DOWN;

            // ALL nonzero physical pulses refresh the deadline, including
            // arbitrarily long runs of contrary pulses. Magnitude is preserved.
            _lastEventTime = timestamp;
            int direction = State == WheelFilterState.LOCK_UP ? 1 : -1;
            result.OutputDelta = direction * Math.Abs(delta);
            result.State = State;
            result.Reason = newGesture ? "gesture_start" : "same_direction";
            if (result.OutputDelta != delta)
            {
                Interlocked.Increment(ref _blockedCount);
                result.Reason = "corrected_to_lock";
            }
            return result;
        }

        private void ResetHistory()
        {
            State = WheelFilterState.IDLE;
            _lastEventTime = 0U;
        }

        private static int ClampWindow(int value)
        {
            return Math.Max(400, Math.Min(500, value));
        }
    }
}

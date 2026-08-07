using System;
using System.Threading;

namespace WheelFix
{
    internal enum WheelFilterDecision
    {
        Allow,
        Block
    }

    /// <summary>
    /// Filters the short, opposite-direction pulses produced by a worn
    /// mechanical wheel encoder. The class is deliberately independent from
    /// WinForms and the Windows hook so that its behaviour can be tested.
    /// </summary>
    internal sealed class WheelFilterCore
    {
        private const int MinimumWindowMs = 10;
        private const int MaximumWindowMs = 150;

        private bool _enabled;
        private int _windowMs;
        private bool _hasAcceptedEvent;
        private int _lastAcceptedDirection;
        private uint _lastAcceptedTime;
        private int _pendingOppositeDirection;
        private uint _pendingOppositeTime;
        private long _blockedCount;

        public WheelFilterCore(bool enabled, int windowMs)
        {
            _enabled = enabled;
            _windowMs = ClampWindow(windowMs);
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value)
                {
                    return;
                }

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
                if (_windowMs == clamped)
                {
                    return;
                }

                _windowMs = clamped;
                ResetHistory();
            }
        }

        public long BlockedCount
        {
            get { return Interlocked.Read(ref _blockedCount); }
        }

        public void ResetBlockedCount()
        {
            Interlocked.Exchange(ref _blockedCount, 0L);
        }

        public WheelFilterDecision Process(int delta, uint timestamp)
        {
            if (!_enabled || delta == 0)
            {
                return WheelFilterDecision.Allow;
            }

            int direction = delta > 0 ? 1 : -1;

            if (!_hasAcceptedEvent)
            {
                Accept(direction, timestamp);
                return WheelFilterDecision.Allow;
            }

            if (direction == _lastAcceptedDirection)
            {
                Accept(direction, timestamp);
                return WheelFilterDecision.Allow;
            }

            uint sinceLastAccepted = Elapsed(timestamp, _lastAcceptedTime);
            if (sinceLastAccepted > (uint)_windowMs)
            {
                Accept(direction, timestamp);
                return WheelFilterDecision.Allow;
            }

            // A second consecutive pulse in the new direction confirms a real
            // fast reversal. Only the first pulse is sacrificed; no synthetic
            // mouse input is ever generated.
            if (_pendingOppositeDirection == direction &&
                Elapsed(timestamp, _pendingOppositeTime) <= (uint)_windowMs)
            {
                Accept(direction, timestamp);
                return WheelFilterDecision.Allow;
            }

            _pendingOppositeDirection = direction;
            _pendingOppositeTime = timestamp;
            Interlocked.Increment(ref _blockedCount);
            return WheelFilterDecision.Block;
        }

        private void Accept(int direction, uint timestamp)
        {
            _hasAcceptedEvent = true;
            _lastAcceptedDirection = direction;
            _lastAcceptedTime = timestamp;
            _pendingOppositeDirection = 0;
            _pendingOppositeTime = 0U;
        }

        private void ResetHistory()
        {
            _hasAcceptedEvent = false;
            _lastAcceptedDirection = 0;
            _lastAcceptedTime = 0U;
            _pendingOppositeDirection = 0;
            _pendingOppositeTime = 0U;
        }

        private static uint Elapsed(uint current, uint previous)
        {
            // uint subtraction intentionally handles the 32-bit Windows tick
            // counter wrapping roughly every 49.7 days.
            return unchecked(current - previous);
        }

        private static int ClampWindow(int value)
        {
            return Math.Max(MinimumWindowMs, Math.Min(MaximumWindowMs, value));
        }
    }
}

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
        private int _lastAcceptedDirection;
        private uint _lastAcceptedTime;
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

            if (_lastAcceptedDirection == 0)
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

            // A worn encoder can emit several bad pulses in a row. Keep the
            // accepted direction locked for the whole debounce window instead
            // of treating the second opposite pulse as a real reversal.
            Interlocked.Increment(ref _blockedCount);
            return WheelFilterDecision.Block;
        }

        private void Accept(int direction, uint timestamp)
        {
            _lastAcceptedDirection = direction;
            _lastAcceptedTime = timestamp;
        }

        private void ResetHistory()
        {
            _lastAcceptedDirection = 0;
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

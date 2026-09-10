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
        private const int MinimumWindowMs = 200;
        private const int MaximumWindowMs = 1500;

        private bool _enabled;
        private int _windowMs;
        private int _burstDirection;
        private uint _lastEventTime;
        private bool _hasLastEvent;
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

            // ponytail: Windows exposes decoded wheel deltas, not the encoder
            // phases, so opposite intent and severe bounce are indistinguishable
            // mid-burst. The deliberate ceiling is that a real reversal needs
            // an idle pause; raw device data is the upgrade path.
            if (!_hasLastEvent ||
                Elapsed(timestamp, _lastEventTime) > (uint)_windowMs)
            {
                _burstDirection = direction;
                _lastEventTime = timestamp;
                _hasLastEvent = true;
                return WheelFilterDecision.Allow;
            }

            // Every physical wheel event keeps the current burst alive. This
            // matters when a damaged encoder emits several wrong pulses: they
            // must not become a new direction merely because the last good
            // pulse is older than the idle gap.
            _lastEventTime = timestamp;

            if (direction == _burstDirection)
            {
                return WheelFilterDecision.Allow;
            }

            Interlocked.Increment(ref _blockedCount);
            return WheelFilterDecision.Block;
        }

        private void ResetHistory()
        {
            _burstDirection = 0;
            _lastEventTime = 0U;
            _hasLastEvent = false;
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

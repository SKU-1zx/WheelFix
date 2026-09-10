using System;
using System.Threading;

namespace WheelFix
{
    internal enum WheelFilterDecision
    {
        Allow,
        Block,
        Replay,
        ReplayFailed
    }

    /// <summary>
    /// Filters the short, opposite-direction pulses produced by a worn
    /// mechanical wheel encoder. The class is deliberately independent from
    /// WinForms and the Windows hook so that its behaviour can be tested.
    /// </summary>
    internal sealed class WheelFilterCore
    {
        private const int MinimumConfirmationPulses = 2;
        private const int MaximumConfirmationPulses = 4;

        private bool _enabled;
        private int _confirmationPulses;
        private int _lastAcceptedDirection;
        private int _pendingOppositeDirection;
        private int _pendingOppositeCount;
        private int _pendingOppositeDelta;
        private long _blockedCount;

        public WheelFilterCore(bool enabled, int confirmationPulses)
        {
            _enabled = enabled;
            _confirmationPulses = ClampConfirmation(confirmationPulses);
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

        public int ConfirmationPulses
        {
            get { return _confirmationPulses; }
            set
            {
                int clamped = ClampConfirmation(value);
                if (_confirmationPulses == clamped)
                {
                    return;
                }

                _confirmationPulses = clamped;
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

        public WheelFilterDecision Process(int delta, out int replayDelta)
        {
            replayDelta = 0;

            if (!_enabled || delta == 0)
            {
                return WheelFilterDecision.Allow;
            }

            int direction = delta > 0 ? 1 : -1;

            if (_lastAcceptedDirection == 0)
            {
                _lastAcceptedDirection = direction;
                return WheelFilterDecision.Allow;
            }

            if (direction == _lastAcceptedDirection)
            {
                DiscardPendingOpposite();
                return WheelFilterDecision.Allow;
            }

            if (_pendingOppositeDirection != direction)
            {
                ClearPendingOpposite();
                _pendingOppositeDirection = direction;
            }

            // ponytail: Windows exposes wheel deltas, not the encoder phases.
            // One or two isolated reverse pulses cannot be distinguished from
            // the measured bounce; an opt-in passthrough mode is the upgrade
            // path if precision single-notch reversals are later required.
            _pendingOppositeCount++;
            _pendingOppositeDelta += delta;

            if (_pendingOppositeCount < _confirmationPulses)
            {
                return WheelFilterDecision.Block;
            }

            replayDelta = _pendingOppositeDelta;
            _lastAcceptedDirection = direction;
            ClearPendingOpposite();
            return WheelFilterDecision.Replay;
        }

        private void DiscardPendingOpposite()
        {
            if (_pendingOppositeCount != 0)
            {
                Interlocked.Add(ref _blockedCount, _pendingOppositeCount);
                ClearPendingOpposite();
            }
        }

        private void ClearPendingOpposite()
        {
            _pendingOppositeDirection = 0;
            _pendingOppositeCount = 0;
            _pendingOppositeDelta = 0;
        }

        private void ResetHistory()
        {
            _lastAcceptedDirection = 0;
            ClearPendingOpposite();
        }

        private static int ClampConfirmation(int value)
        {
            return Math.Max(MinimumConfirmationPulses,
                Math.Min(MaximumConfirmationPulses, value));
        }
    }
}

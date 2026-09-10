using System;

namespace WheelFix
{
    internal static class WheelFilterCoreTests
    {
        private static int _testsRun;

        private static void Main()
        {
            FirstEventAndSameDirectionAreAllowed();
            FalseReversalIsHeldAndDiscarded();
            ConfirmedReversalReplaysEveryHeldPulse();
            RealDamagedEncoderTraceIsCleaned();
            DisabledFilterAllowsEverything();
            ChangingSettingsResetsHistory();
            ConfirmationSettingIsClamped();

            Console.WriteLine("OK - " + _testsRun + " tests passed.");
        }

        private static void FirstEventAndSameDirectionAreAllowed()
        {
            WheelFilterCore filter = NewFilter();
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void FalseReversalIsHeldAndDiscarded()
        {
            WheelFilterCore filter = NewFilter();
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Block, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Block, 0);
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertEqual(2L, filter.BlockedCount);
        }

        private static void ConfirmedReversalReplaysEveryHeldPulse()
        {
            WheelFilterCore filter = NewFilter();
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Block, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Block, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Replay, 360);
            AssertProcess(filter, 120, WheelFilterDecision.Allow, 0);
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void RealDamagedEncoderTraceIsCleaned()
        {
            const string trace =
                "---------+-+-+----+-+-+-+------+------++--+--+--------+--+------" +
                "---+-+-++-+--++----+------+-++----+--+--+-------+--+--+--+---+-";

            WheelFilterCore filter = NewFilter();
            int outputDelta = 0;
            int upwardOutputEvents = 0;

            foreach (char pulse in trace)
            {
                int delta = pulse == '+' ? 120 : -120;
                int replayDelta;
                WheelFilterDecision decision =
                    filter.Process(delta, out replayDelta);

                int emittedDelta = decision == WheelFilterDecision.Allow
                    ? delta
                    : replayDelta;
                outputDelta += emittedDelta;
                if (emittedDelta > 0)
                {
                    upwardOutputEvents++;
                }
            }

            AssertEqual(-11280L, outputDelta);
            AssertEqual(0L, upwardOutputEvents);
            AssertEqual(33L, filter.BlockedCount);
        }

        private static void DisabledFilterAllowsEverything()
        {
            WheelFilterCore filter = new WheelFilterCore(false, 3);
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Allow, 0);
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void ChangingSettingsResetsHistory()
        {
            WheelFilterCore filter = NewFilter();
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
            AssertProcess(filter, 120, WheelFilterDecision.Block, 0);

            filter.ConfirmationPulses = 4;
            AssertProcess(filter, 120, WheelFilterDecision.Allow, 0);

            filter.Enabled = false;
            filter.Enabled = true;
            AssertProcess(filter, -120, WheelFilterDecision.Allow, 0);
        }

        private static void ConfirmationSettingIsClamped()
        {
            WheelFilterCore filter = new WheelFilterCore(true, 99);
            AssertEqual(4L, filter.ConfirmationPulses);
            filter.ConfirmationPulses = 1;
            AssertEqual(2L, filter.ConfirmationPulses);
        }

        private static WheelFilterCore NewFilter()
        {
            return new WheelFilterCore(true, 3);
        }

        private static void AssertProcess(
            WheelFilterCore filter,
            int delta,
            WheelFilterDecision expectedDecision,
            int expectedReplayDelta)
        {
            int replayDelta;
            WheelFilterDecision actualDecision =
                filter.Process(delta, out replayDelta);

            _testsRun++;
            if (actualDecision != expectedDecision ||
                replayDelta != expectedReplayDelta)
            {
                throw new InvalidOperationException(
                    "Expected " + expectedDecision + "/" +
                    expectedReplayDelta + ", received " + actualDecision +
                    "/" + replayDelta + ".");
            }
        }

        private static void AssertEqual(long expected, long actual)
        {
            _testsRun++;
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    "Expected " + expected + ", received " + actual + ".");
            }
        }
    }
}

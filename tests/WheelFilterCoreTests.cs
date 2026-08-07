using System;

namespace WheelFix
{
    internal static class WheelFilterCoreTests
    {
        private static int _testsRun;

        private static void Main()
        {
            FirstEventAndSameDirectionAreAllowed();
            FastOppositePulseIsBlocked();
            OriginalDirectionCancelsTheFalseReversal();
            TwoOppositePulsesConfirmARealReversal();
            SlowDirectionChangeIsAllowed();
            DisabledFilterAllowsEverything();
            TimestampWrapIsHandled();
            ChangingSettingsResetsHistory();

            Console.WriteLine("OK - " + _testsRun + " tests passed.");
        }

        private static void FirstEventAndSameDirectionAreAllowed()
        {
            WheelFilterCore filter = NewFilter();
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 100U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 120U));
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void FastOppositePulseIsBlocked()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 130U));
            AssertEqual(1L, filter.BlockedCount);
        }

        private static void OriginalDirectionCancelsTheFalseReversal()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 115U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 125U));
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 140U));
        }

        private static void TwoOppositePulsesConfirmARealReversal()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 120U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 135U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 150U));
        }

        private static void SlowDirectionChangeIsAllowed()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 156U));
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void DisabledFilterAllowsEverything()
        {
            WheelFilterCore filter = new WheelFilterCore(false, 55);
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 100U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 101U));
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void TimestampWrapIsHandled()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, uint.MaxValue - 10U);
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 15U));
        }

        private static void ChangingSettingsResetsHistory()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            filter.WindowMs = 90;
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 110U));

            filter.Enabled = false;
            filter.Enabled = true;
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 120U));
        }

        private static WheelFilterCore NewFilter()
        {
            return new WheelFilterCore(true, 55);
        }

        private static void AssertDecision(
            WheelFilterDecision expected,
            WheelFilterDecision actual)
        {
            _testsRun++;
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    "Expected " + expected + ", received " + actual + ".");
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

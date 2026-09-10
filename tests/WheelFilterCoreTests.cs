using System;

namespace WheelFix
{
    internal static class WheelFilterCoreTests
    {
        private static int _testsRun;

        private static void Main()
        {
            FirstEventAndSameDirectionAreAllowed();
            EveryOppositePulseInsideBurstIsBlocked();
            OppositePulsesKeepBurstLocked();
            DirectionChangesAfterIdleGap();
            DisabledFilterAllowsEverything();
            TimestampWrapIsHandled();
            ChangingSettingsResetsHistory();
            WindowSettingIsClamped();

            Console.WriteLine("OK - " + _testsRun + " tests passed.");
        }

        private static void FirstEventAndSameDirectionAreAllowed()
        {
            WheelFilterCore filter = NewFilter();
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 100U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 120U));
            AssertEqual(0L, filter.BlockedCount);
        }

        private static void EveryOppositePulseInsideBurstIsBlocked()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 130U));
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 500U));
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 900U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 950U));
            AssertEqual(3L, filter.BlockedCount);
        }

        private static void OppositePulsesKeepBurstLocked()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 700U));
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 1300U));
            AssertDecision(WheelFilterDecision.Block, filter.Process(120, 1900U));
        }

        private static void DirectionChangesAfterIdleGap()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, 100U);
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 901U));
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 920U));
        }

        private static void DisabledFilterAllowsEverything()
        {
            WheelFilterCore filter = new WheelFilterCore(false, 800);
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
            filter.WindowMs = 1200;
            AssertDecision(WheelFilterDecision.Allow, filter.Process(120, 110U));

            filter.Enabled = false;
            filter.Enabled = true;
            AssertDecision(WheelFilterDecision.Allow, filter.Process(-120, 120U));
        }

        private static void WindowSettingIsClamped()
        {
            WheelFilterCore filter = new WheelFilterCore(true, 1);
            AssertEqual(200L, filter.WindowMs);
            filter.WindowMs = 9999;
            AssertEqual(1500L, filter.WindowMs);
        }

        private static WheelFilterCore NewFilter()
        {
            return new WheelFilterCore(true, 800);
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
